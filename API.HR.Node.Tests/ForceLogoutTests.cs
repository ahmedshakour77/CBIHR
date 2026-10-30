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
}
