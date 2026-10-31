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

    [Theory]
    [InlineData("svc.unknown")]
    [InlineData("random_tech")]
    [InlineData("svc.malicious")]
    public async Task ValidateOrRepairUserEmployeeLinkAsync_UndeclaredTechnicalAccount_ShouldFailValidation(string invalidTechAccount)
    {
        var (_, _, service, _) = CreateTestEnvironment();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = invalidTechAccount,
            Email = $"{invalidTechAccount}@company.iq",
            EmployeeID = null
        };

        var result = await service.ValidateOrRepairUserEmployeeLinkAsync(user);

        result.IsValid.Should().BeFalse("arbitrary or undeclared technical accounts are not permitted without employee linking");
        result.IsTechnicalAccount.Should().BeFalse();
    }

    [Fact]
    public async Task StartupSeeder_EnsureRolePermissionClaimsAsync_ShouldAddMissingAndRemoveObsoleteClaims()
    {
        var (provider, db, _, _) = CreateTestEnvironment();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();

        // Create Supervisor role
        var supervisorRole = new IdentityRole("Supervisor");
        await roleManager.CreateAsync(supervisorRole);

        // Add an obsolete claim that is NOT part of Supervisor's package in FeaturePermissionCatalog
        var obsoleteClaim = new System.Security.Claims.Claim("permission", "Obsolete.Permission.Old");
        await roleManager.AddClaimAsync(supervisorRole, obsoleteClaim);

        // Run sync
        await StartupSeeder.EnsureRolePermissionClaimsAsync(provider, NullLogger.Instance);

        // Verify claims
        var claims = await roleManager.GetClaimsAsync(supervisorRole);
        var permClaims = claims.Where(c => c.Type.Equals("permission", StringComparison.OrdinalIgnoreCase)).Select(c => c.Value).ToList();

        // Obsolete claim must be removed
        permClaims.Should().NotContain("Obsolete.Permission.Old", "obsolete role claims must be cleaned up");

        // Desired package claims must be present
        var desiredPackage = FeaturePermissionCatalog.RolePermissionPackages["Supervisor"];
        foreach (var expectedPerm in desiredPackage)
        {
            permClaims.Should().Contain(expectedPerm, $"desired permission '{expectedPerm}' must be seeded for role");
        }
    }

    [Fact]
    public async Task CheckSameEmployeePrivilegeEscalationAsync_SameEmployeeNonAdmin_ShouldBlockAndLogAudit()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        db.Employees.Add(new Employee
        {
            EmployeeID = "EMP_ESC_100",
            Name = "سعيد أحمد"
        });
        await db.SaveChangesAsync();

        var userA = new ApplicationUser
        {
            Id = "user-a-guid",
            UserName = "user.a",
            Email = "user.a@company.local",
            EmployeeID = "EMP_ESC_100"
        };
        var userB = new ApplicationUser
        {
            Id = "user-b-guid",
            UserName = "user.b",
            Email = "user.b@company.local",
            EmployeeID = "EMP_ESC_100"
        };
        await userManager.CreateAsync(userA);
        await userManager.CreateAsync(userB);

        // Caller User A is NOT Admin, has Users.Edit
        var identity = new System.Security.Claims.ClaimsIdentity("TestAuth");
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userA.Id));
        identity.AddClaim(new System.Security.Claims.Claim("permission", Permissions.UsersEdit));
        var callerPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);

        var result = await service.CheckSameEmployeePrivilegeEscalationAsync(userA.Id, userB.Id, callerPrincipal);

        result.IsAllowed.Should().BeFalse("non-admin caller cannot modify another account belonging to the same employee");
        result.IsSameEmployee.Should().BeTrue();
        result.CallerEmployeeId.Should().Be("EMP_ESC_100");
        result.TargetEmployeeId.Should().Be("EMP_ESC_100");
    }

    [Fact]
    public async Task CheckSameEmployeePrivilegeEscalationAsync_AdminCaller_ShouldAllowSameEmployeeTarget()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        db.Employees.Add(new Employee
        {
            EmployeeID = "EMP_ESC_200",
            Name = "محمود حسن"
        });
        await db.SaveChangesAsync();

        var userA = new ApplicationUser
        {
            Id = "admin-guid",
            UserName = "admin.user",
            Email = "admin@company.local",
            EmployeeID = "EMP_ESC_200"
        };
        var userB = new ApplicationUser
        {
            Id = "target-guid",
            UserName = "target.user",
            Email = "target@company.local",
            EmployeeID = "EMP_ESC_200"
        };
        await userManager.CreateAsync(userA);
        await userManager.CreateAsync(userB);

        // Caller has Admin role
        var identity = new System.Security.Claims.ClaimsIdentity("TestAuth");
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userA.Id));
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin"));
        var adminPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);

        var result = await service.CheckSameEmployeePrivilegeEscalationAsync(userA.Id, userB.Id, adminPrincipal);

        result.IsAllowed.Should().BeTrue("Admin is allowed full administrative governance over any account");
    }

    [Fact]
    public async Task CheckSameEmployeePrivilegeEscalationAsync_DifferentEmployee_ShouldAllow()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        db.Employees.Add(new Employee { EmployeeID = "EMP_DIFF_1", Name = "زيد علي" });
        db.Employees.Add(new Employee { EmployeeID = "EMP_DIFF_2", Name = "عمر خالد" });
        await db.SaveChangesAsync();

        var userA = new ApplicationUser
        {
            Id = "user-diff-a",
            UserName = "user.diff.a",
            Email = "diff.a@company.local",
            EmployeeID = "EMP_DIFF_1"
        };
        var userC = new ApplicationUser
        {
            Id = "user-diff-c",
            UserName = "user.diff.c",
            Email = "diff.c@company.local",
            EmployeeID = "EMP_DIFF_2"
        };
        await userManager.CreateAsync(userA);
        await userManager.CreateAsync(userC);

        var identity = new System.Security.Claims.ClaimsIdentity("TestAuth");
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userA.Id));
        identity.AddClaim(new System.Security.Claims.Claim("permission", Permissions.UsersPermissionsEdit));
        var callerPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);

        var result = await service.CheckSameEmployeePrivilegeEscalationAsync(userA.Id, userC.Id, callerPrincipal);

        result.IsAllowed.Should().BeTrue();
        result.IsSameEmployee.Should().BeFalse();
    }

    [Fact]
    public async Task CheckSameEmployeePrivilegeEscalationAsync_SelfModification_NonAdmin_ShouldBlock()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        var userA = new ApplicationUser
        {
            Id = "self-user-id",
            UserName = "self.user",
            Email = "self@company.local",
            EmployeeID = "EMP_SELF"
        };
        await userManager.CreateAsync(userA);

        var identity = new System.Security.Claims.ClaimsIdentity("TestAuth");
        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userA.Id));
        identity.AddClaim(new System.Security.Claims.Claim("permission", Permissions.UsersEdit));
        var callerPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);

        var result = await service.CheckSameEmployeePrivilegeEscalationAsync(userA.Id, userA.Id, callerPrincipal);

        result.IsAllowed.Should().BeFalse("self-modification of permissions/roles by non-admin must always be blocked");
    }
}
