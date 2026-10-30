using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Audit;
using API.HR.Node.Services.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class UserEmployeeLinkTests
{
    private (ServiceProvider Provider, ApplicationDbContext DbContext, IUserEmployeeService Service, UserManager<ApplicationUser> UserManager) CreateTestEnvironment()
    {
        var services = new ServiceCollection();
        var dbName = $"UserEmployeeLinkTests_{Guid.NewGuid()}";

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        services.AddIdentity<ApplicationUser, IdentityRole>(opts =>
        {
            opts.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+\\/";
        })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var auditLoggerMock = new Mock<IAuditLogger>();
        services.AddSingleton(auditLoggerMock.Object);
        services.AddScoped<IUserEmployeeService, UserEmployeeService>();

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var service = provider.GetRequiredService<IUserEmployeeService>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        return (provider, db, service, userManager);
    }

    [Theory]
    [InlineData("systemadmin")]
    [InlineData("SYSTEMADMIN")]
    [InlineData("svc.integration")]
    [InlineData("svc.backup")]
    public async Task ValidateOrRepairUserEmployeeLinkAsync_TechnicalAccounts_ShouldAlwaysBeValidWithoutEmployee(string techAccountName)
    {
        var (_, _, service, _) = CreateTestEnvironment();

        var techUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = techAccountName,
            Email = $"{techAccountName}@system.local",
            EmployeeID = null // Explicitly null
        };

        var result = await service.ValidateOrRepairUserEmployeeLinkAsync(techUser);

        result.IsValid.Should().BeTrue();
        result.IsTechnicalAccount.Should().BeTrue();
        result.WasRepaired.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOrRepairUserEmployeeLinkAsync_HumanUserWithValidEmployee_ShouldBeValid()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        db.Employees.Add(new Employee
        {
            EmployeeID = "EMP101",
            Name = "علي جاسم"
        });
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "ali.jasim",
            Email = "ali.jasim@company.iq",
            EmployeeID = "EMP101"
        };
        await userManager.CreateAsync(user);

        var result = await service.ValidateOrRepairUserEmployeeLinkAsync(user);

        result.IsValid.Should().BeTrue();
        result.IsTechnicalAccount.Should().BeFalse();
        result.WasRepaired.Should().BeFalse();
        result.EmployeeID.Should().Be("EMP101");
        result.EmployeeName.Should().Be("علي جاسم");
    }

    [Fact]
    public async Task ValidateOrRepairUserEmployeeLinkAsync_HumanUserWithoutEmployeeAndNoMatchingEmail_ShouldFail()
    {
        var (_, _, service, userManager) = CreateTestEnvironment();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "orphan.user",
            Email = "orphan@external.com",
            EmployeeID = null
        };
        await userManager.CreateAsync(user);

        var result = await service.ValidateOrRepairUserEmployeeLinkAsync(user);

        result.IsValid.Should().BeFalse("unlinked human users must not satisfy the employee invariant");
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
        result.WasRepaired.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOrRepairUserEmployeeLinkAsync_OrphanedUser_WithMatchingEmail_ShouldAutoRepairLink()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        // Seed employee and registered work email
        db.Employees.Add(new Employee
        {
            EmployeeID = "EMP202",
            Name = "فاطمة حيدر"
        });
        db.EmployeeEmails.Add(new EmployeeEmail
        {
            EmployeeID = "EMP202",
            Email = "fatima.haidar@company.iq",
            IsPrimary = true
        });
        await db.SaveChangesAsync();

        // Create user with matching email but missing EmployeeID
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "fatima.h",
            Email = "fatima.haidar@company.iq",
            EmployeeID = null
        };
        await userManager.CreateAsync(user);

        var result = await service.ValidateOrRepairUserEmployeeLinkAsync(user);

        result.IsValid.Should().BeTrue();
        result.WasRepaired.Should().BeTrue("matching email must trigger deterministic auto-repair");
        result.EmployeeID.Should().Be("EMP202");
        result.EmployeeName.Should().Be("فاطمة حيدر");

        // Verify database persistence
        var reloadedUser = await userManager.FindByIdAsync(user.Id);
        reloadedUser!.EmployeeID.Should().Be("EMP202");
    }

    [Fact]
    public async Task GetUsersForEmployeeAsync_EmployeeWithMultipleAccounts_ShouldReturnAllAccounts()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        db.Employees.Add(new Employee
        {
            EmployeeID = "EMP303",
            Name = "حسين كاظم"
        });
        await db.SaveChangesAsync();

        var localUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "hussein.k",
            Email = "hussein@company.iq",
            EmployeeID = "EMP303"
        };
        var adUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "CORP\\hussein.k",
            Email = "hussein.k@company.iq",
            EmployeeID = "EMP303"
        };
        await userManager.CreateAsync(localUser);
        await userManager.CreateAsync(adUser);

        var linkedUsers = await service.GetUsersForEmployeeAsync("EMP303");

        linkedUsers.Should().HaveCount(2);
        linkedUsers.Select(u => u.UserName).Should().Contain(["hussein.k", "CORP\\hussein.k"]);
    }

    [Fact]
    public async Task RelinkUserAsync_ValidTargetEmployee_ShouldUpdateEmployeeId_AndForceLogout()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        db.Employees.AddRange(
            new Employee { EmployeeID = "EMP401", Name = "الموظف القديم" },
            new Employee { EmployeeID = "EMP402", Name = "الموظف الجديد" }
        );

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "relink.user",
            Email = "user@company.iq",
            EmployeeID = "EMP401"
        };
        await userManager.CreateAsync(user);

        // Add active device for user
        db.UserDevices.Add(new UserDevice
        {
            UserId = user.Id,
            EmployeeID = "EMP401",
            DeviceId = "DEV_001",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var oldStamp = await userManager.GetSecurityStampAsync(user);

        var result = await service.RelinkUserAsync(user.Id, "EMP402", "ADMIN_01");

        result.Succeeded.Should().BeTrue();
        result.OldEmployeeId.Should().Be("EMP401");
        result.NewEmployeeId.Should().Be("EMP402");

        var updatedUser = await userManager.FindByIdAsync(user.Id);
        updatedUser!.EmployeeID.Should().Be("EMP402");

        // Verify security stamp changed
        var newStamp = await userManager.GetSecurityStampAsync(updatedUser);
        newStamp.Should().NotBe(oldStamp);

        // Verify active devices revoked
        var activeDevices = await db.UserDevices.Where(d => d.UserId == user.Id && d.IsActive).ToListAsync();
        activeDevices.Should().BeEmpty();
    }

    [Fact]
    public async Task RelinkUserAsync_NonExistentEmployee_ShouldFail()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        db.Employees.Add(new Employee { EmployeeID = "EMP501", Name = "الموظف الفعلي" });
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "test.user",
            EmployeeID = "EMP501"
        };
        await userManager.CreateAsync(user);

        var result = await service.RelinkUserAsync(user.Id, "EMP_NON_EXISTENT", "ADMIN_01");

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("غير موجود");
    }

    [Fact]
    public async Task RepairOrphanedUsersAsync_BatchProcess_RepairsEligibleAndReportsUnresolved()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        // 1. Employee with registered email
        db.Employees.Add(new Employee { EmployeeID = "EMP601", Name = "موظف قابل للإصلاح" });
        db.EmployeeEmails.Add(new EmployeeEmail { EmployeeID = "EMP601", Email = "repairable@company.iq" });

        // 2. Already linked employee
        db.Employees.Add(new Employee { EmployeeID = "EMP602", Name = "موظف مرتبط مسبقاً" });
        await db.SaveChangesAsync();

        // User A: Already linked
        await userManager.CreateAsync(new ApplicationUser { UserName = "already.linked", Email = "linked@company.iq", EmployeeID = "EMP602" });

        // User B: Technical account (exempt)
        await userManager.CreateAsync(new ApplicationUser { UserName = "svc.integration", Email = "svc@company.iq", EmployeeID = null });

        // User C: Unlinked but has matching email in EmployeeEmails -> will be repaired
        await userManager.CreateAsync(new ApplicationUser { UserName = "to.repair", Email = "repairable@company.iq", EmployeeID = null });

        // User D: Unlinked with unknown email -> remains unresolved
        await userManager.CreateAsync(new ApplicationUser { UserName = "unresolvable", Email = "unknown@random.com", EmployeeID = null });

        var report = await service.RepairOrphanedUsersAsync("ADMIN");

        report.TotalUsers.Should().Be(4);
        report.AlreadyLinkedCount.Should().Be(2); // 1 linked + 1 technical account
        report.RepairedCount.Should().Be(1);
        report.UnresolvedCount.Should().Be(1);

        report.RepairedUsers.Should().ContainSingle(u => u.UserName == "to.repair" && u.MatchedEmployeeId == "EMP601");
        report.UnresolvedUsers.Should().ContainSingle(u => u.UserName == "unresolvable");
    }
}
