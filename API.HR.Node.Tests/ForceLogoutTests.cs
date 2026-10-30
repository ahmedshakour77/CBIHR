using API.HR.Node.Data;
using API.HR.Node.Services.Audit;
using API.HR.Node.Services.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class ForceLogoutTests
{
    private (ServiceProvider Provider, ApplicationDbContext DbContext, IUserEmployeeService Service, UserManager<ApplicationUser> UserManager) CreateTestEnvironment()
    {
        var services = new ServiceCollection();
        var dbName = $"ForceLogoutTests_{Guid.NewGuid()}";

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        services.AddIdentity<ApplicationUser, IdentityRole>()
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

    [Fact]
    public async Task ForceLogoutUserAsync_ShouldUpdateSecurityStamp()
    {
        var (_, _, service, userManager) = CreateTestEnvironment();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "test.logout",
            Email = "test@company.iq",
            EmployeeID = "EMP_LOGOUT_01"
        };
        await userManager.CreateAsync(user);

        var originalStamp = await userManager.GetSecurityStampAsync(user);

        var result = await service.ForceLogoutUserAsync(user.Id, "ADMIN_ACTOR");

        result.Succeeded.Should().BeTrue();
        result.SecurityStampUpdated.Should().BeTrue();

        var updatedUser = await userManager.FindByIdAsync(user.Id);
        var newStamp = await userManager.GetSecurityStampAsync(updatedUser!);

        newStamp.Should().NotBeNullOrWhiteSpace();
        newStamp.Should().NotBe(originalStamp, "Force logout must invalidate security stamp to invalidate all sessions and tokens");
    }

    [Fact]
    public async Task ForceLogoutUserAsync_ShouldRevokeAllActiveUserDevices()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "multi.device.user",
            Email = "devices@company.iq",
            EmployeeID = "EMP_DEV_01"
        };
        await userManager.CreateAsync(user);

        // Seed 3 devices: 2 active, 1 already inactive
        db.UserDevices.AddRange(
            new UserDevice
            {
                UserId = user.Id,
                EmployeeID = "EMP_DEV_01",
                DeviceId = "DEV_PHONE",
                IsActive = true
            },
            new UserDevice
            {
                UserId = user.Id,
                EmployeeID = "EMP_DEV_01",
                DeviceId = "DEV_TABLET",
                IsActive = true
            },
            new UserDevice
            {
                UserId = user.Id,
                EmployeeID = "EMP_DEV_01",
                DeviceId = "DEV_OLD_PC",
                IsActive = false
            }
        );
        await db.SaveChangesAsync();

        var result = await service.ForceLogoutUserAsync(user.Id, "SECURITY_OFFICER");

        result.Succeeded.Should().BeTrue();
        result.SessionsRevokedCount.Should().Be(2, "only previously active devices are revoked");

        // Verify all devices for user in database are now inactive
        var userDevices = await db.UserDevices.Where(d => d.UserId == user.Id).ToListAsync();
        userDevices.Should().HaveCount(3);
        userDevices.All(d => !d.IsActive).Should().BeTrue("all user devices must be marked inactive");
    }

    [Fact]
    public async Task SecurityStampChange_ShouldInvalidateCachedSecurityStampValidation()
    {
        var (_, _, service, userManager) = CreateTestEnvironment();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "token.validation.user",
            Email = "tok@company.iq",
            EmployeeID = "EMP_TOK_01"
        };
        await userManager.CreateAsync(user);

        // Capture initial security stamp (analogous to what's stored in a JWT / Refresh Token / Cookie)
        var tokenSecurityStamp = await userManager.GetSecurityStampAsync(user);

        // Call Force Logout
        await service.ForceLogoutUserAsync(user.Id, "ADMIN");

        // Reload user from database and verify stamp comparison
        var freshUser = await userManager.FindByIdAsync(user.Id);
        var currentDbStamp = await userManager.GetSecurityStampAsync(freshUser!);

        // Simulating the check performed during Refresh Token validation / Cookie validation
        var isValid = string.Equals(tokenSecurityStamp, currentDbStamp, StringComparison.Ordinal);
        isValid.Should().BeFalse("token/cookie with previous security stamp must be rejected after force logout");
    }

    [Fact]
    public async Task ForceLogoutUserAsync_NonExistentUser_ShouldReturnFailure()
    {
        var (_, _, service, _) = CreateTestEnvironment();

        var result = await service.ForceLogoutUserAsync("NON_EXISTENT_USER_ID", "ADMIN");

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("غير موجود");
    }

    [Fact]
    public async Task ForceLogoutUserAsync_SingleEmployeeWithMultipleUsers_ShouldOnlyRevokeTargetUserDevices_AndLeaveOtherUserDevicesActive()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        const string sharedEmpId = "EMP_MULTI_LOGOUT";
        db.Employees.Add(new Employee
        {
            EmployeeID = sharedEmpId,
            Name = "موظف متعدد الحسابات"
        });
        await db.SaveChangesAsync();

        var userA = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "user.a",
            Email = "user.a@company.iq",
            EmployeeID = sharedEmpId
        };
        var userB = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "user.b",
            Email = "user.b@company.iq",
            EmployeeID = sharedEmpId
        };
        await userManager.CreateAsync(userA);
        await userManager.CreateAsync(userB);

        var deviceA = new UserDevice
        {
            UserId = userA.Id,
            EmployeeID = sharedEmpId,
            DeviceId = "DEV_USER_A",
            IsActive = true
        };
        var deviceB = new UserDevice
        {
            UserId = userB.Id,
            EmployeeID = sharedEmpId,
            DeviceId = "DEV_USER_B",
            IsActive = true
        };
        db.UserDevices.AddRange(deviceA, deviceB);
        await db.SaveChangesAsync();

        // Perform force logout on User A ONLY
        var result = await service.ForceLogoutUserAsync(userA.Id, "ADMIN_ACTOR");

        result.Succeeded.Should().BeTrue();
        result.SessionsRevokedCount.Should().Be(1);

        // Verify User A device is inactive, but User B device remains active
        var reloadedDeviceA = await db.UserDevices.FirstAsync(d => d.DeviceId == "DEV_USER_A");
        var reloadedDeviceB = await db.UserDevices.FirstAsync(d => d.DeviceId == "DEV_USER_B");

        reloadedDeviceA.IsActive.Should().BeFalse("target user's device must be revoked");
        reloadedDeviceB.IsActive.Should().BeTrue("other user's device for same employee must NOT be affected");
    }

    [Fact]
    public async Task ForceLogoutEmployeeAllUsersAsync_ShouldRevokeDevicesForEveryUserLinkedToEmployee()
    {
        var (_, db, service, userManager) = CreateTestEnvironment();

        const string targetEmpId = "EMP_ALL_LOGOUT";
        db.Employees.Add(new Employee
        {
            EmployeeID = targetEmpId,
            Name = "موظف لإنهاء كافة جلساته"
        });
        await db.SaveChangesAsync();

        var user1 = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "emp.acc1",
            EmployeeID = targetEmpId
        };
        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "emp.acc2",
            EmployeeID = targetEmpId
        };
        await userManager.CreateAsync(user1);
        await userManager.CreateAsync(user2);

        db.UserDevices.AddRange(
            new UserDevice { UserId = user1.Id, EmployeeID = targetEmpId, DeviceId = "D1", IsActive = true },
            new UserDevice { UserId = user2.Id, EmployeeID = targetEmpId, DeviceId = "D2", IsActive = true }
        );
        await db.SaveChangesAsync();

        var result = await service.ForceLogoutEmployeeAllUsersAsync(targetEmpId, "SUPER_ADMIN");

        result.Succeeded.Should().BeTrue();
        result.SessionsRevokedCount.Should().Be(2);

        var activeDevices = await db.UserDevices.Where(d => d.EmployeeID == targetEmpId && d.IsActive).ToListAsync();
        activeDevices.Should().BeEmpty("all devices across all accounts of the employee must be deactivated");
    }
}
