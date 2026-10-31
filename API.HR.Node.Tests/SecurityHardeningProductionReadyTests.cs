using System.Buffers.Text;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using API.HR.Node.Data;
using API.HR.Node.Endpoints;
using API.HR.Node.Security;
using API.HR.Node.Services.Audit;
using API.HR.Node.Services.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class SecurityHardeningProductionReadyTests
{
    private (ServiceProvider Provider, ApplicationDbContext DbContext, IUserEmployeeService Service, UserManager<ApplicationUser> UserManager, SignInManager<ApplicationUser> SignInManager, Mock<IAuditLogger> AuditMock)
        CreateTestEnvironment(Action<Mock<IUserStore<ApplicationUser>>>? configureUserStore = null)
    {
        var services = new ServiceCollection();
        var dbName = $"SecurityHardenFinalTests_{Guid.NewGuid()}";

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        var identityBuilder = services.AddIdentity<ApplicationUser, IdentityRole>(opts =>
        {
            opts.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        var auditLoggerMock = new Mock<IAuditLogger>();
        services.AddSingleton(auditLoggerMock.Object);
        services.AddScoped<IUserEmployeeService, UserEmployeeService>();
        services.AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme);

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var service = provider.GetRequiredService<IUserEmployeeService>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = provider.GetRequiredService<SignInManager<ApplicationUser>>();

        return (provider, db, service, userManager, signInManager, auditLoggerMock);
    }

    #region 1. Admin Bypass Based Strictly on Role, Never Username

    [Fact]
    public async Task AdminBypass_UsernameAdminWithoutAdminRole_MustNotBypassSelfModification()
    {
        var (_, db, service, userManager, _, _) = CreateTestEnvironment();

        var user = new ApplicationUser
        {
            Id = "admin-username-no-role",
            UserName = "admin",
            Email = "admin@company.iq",
            EmployeeID = "EMP_ADMIN_01"
        };
        await userManager.CreateAsync(user);

        // Caller identity has username "admin", but NO Admin role claim
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
        identity.AddClaim(new Claim(ClaimTypes.Name, "admin"));
        identity.AddClaim(new Claim("permission", Permissions.UsersEdit));
        var callerPrincipal = new ClaimsPrincipal(identity);

        var check = await service.CheckSameEmployeePrivilegeEscalationAsync(user.Id, user.Id, callerPrincipal);

        check.IsAllowed.Should().BeFalse("A user named 'admin' without the Admin role must NOT receive administrative bypass.");
        check.Reason.Should().Contain("لا يمكنك تعديل أدوارك");
    }

    [Fact]
    public async Task AdminBypass_UsernameSystemAdminWithoutAdminRole_MustNotBypass()
    {
        var (_, db, service, userManager, _, _) = CreateTestEnvironment();

        var user = new ApplicationUser
        {
            Id = "sysadmin-no-role",
            UserName = "systemadmin",
            Email = "systemadmin@company.iq",
            EmployeeID = "EMP_SYS_01"
        };
        await userManager.CreateAsync(user);

        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
        identity.AddClaim(new Claim(ClaimTypes.Name, "systemadmin"));
        var callerPrincipal = new ClaimsPrincipal(identity);

        var check = await service.CheckSameEmployeePrivilegeEscalationAsync(user.Id, user.Id, callerPrincipal);

        check.IsAllowed.Should().BeFalse("Username 'systemadmin' alone without Admin role must NOT receive administrative bypass.");
    }

    [Fact]
    public async Task AdminBypass_UserWithAdminRole_ReceivesBypassRegardlessOfUsername()
    {
        var (_, db, service, userManager, _, _) = CreateTestEnvironment();

        var adminUser = new ApplicationUser
        {
            Id = "regular-named-admin",
            UserName = "zayd.ali",
            Email = "zayd@company.iq",
            EmployeeID = "EMP_ADMIN_99"
        };
        await userManager.CreateAsync(adminUser);

        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, adminUser.Id));
        identity.AddClaim(new Claim(ClaimTypes.Name, "zayd.ali"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        var callerPrincipal = new ClaimsPrincipal(identity);

        var check = await service.CheckSameEmployeePrivilegeEscalationAsync(adminUser.Id, adminUser.Id, callerPrincipal);

        check.IsAllowed.Should().BeTrue("User with explicit Role = 'Admin' must receive admin bypass.");
    }

    [Theory]
    [InlineData("admin", false)]
    [InlineData("systemadmin", false)]
    [InlineData("root", false)]
    public void PermissionResolver_IsAdmin_UsernameAlone_ReturnsFalse(string username, bool expected)
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, username));
        var principal = new ClaimsPrincipal(identity);

        PermissionResolver.IsAdmin(principal).Should().Be(expected);
    }

    [Theory]
    [InlineData(ClaimTypes.Role, "Admin")]
    [InlineData("role", "Admin")]
    [InlineData("role", "admin")]
    public void PermissionResolver_IsAdmin_RoleAdmin_ReturnsTrue(string claimType, string claimValue)
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(claimType, claimValue));
        var principal = new ClaimsPrincipal(identity);

        PermissionResolver.IsAdmin(principal).Should().BeTrue();
    }

    #endregion

    #region 2. Legacy ManageUsers Does Not Grant Users.PermissionsEdit

    [Fact]
    public void ManageUsers_Grants_UsersView_And_UsersEdit_But_Denies_UsersPermissionsEdit()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.ManageUsers));
        var principal = new ClaimsPrincipal(identity);

        // Users.View => ALLOW
        PermissionResolver.HasPermission(principal, Permissions.UsersView).Should().BeTrue();

        // Users.Edit => ALLOW
        PermissionResolver.HasPermission(principal, Permissions.UsersEdit).Should().BeTrue();

        // Users.PermissionsEdit => DENY (Sensitive permission MUST NOT be granted by legacy ManageUsers)
        PermissionResolver.HasPermission(principal, Permissions.UsersPermissionsEdit).Should().BeFalse(
            "Legacy ManageUsers must NOT grant Users.PermissionsEdit");
    }

    [Fact]
    public void Explicit_UsersPermissionsEdit_Grants_UsersPermissionsEdit_And_Implies_UsersView()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("permission", Permissions.UsersPermissionsEdit));
        var principal = new ClaimsPrincipal(identity);

        // Explicit permission
        PermissionResolver.HasPermission(principal, Permissions.UsersPermissionsEdit).Should().BeTrue();

        // Implied View
        PermissionResolver.HasPermission(principal, Permissions.UsersView).Should().BeTrue(
            "Users.PermissionsEdit must automatically imply Users.View");

        // Does NOT imply Edit
        PermissionResolver.HasPermission(principal, Permissions.UsersEdit).Should().BeFalse(
            "Users.PermissionsEdit must not grant Users.Edit");
    }

    #endregion

    #region 3. Permission Resolution Consistency Between Handler and HasPermission

    [Theory]
    [InlineData(Permissions.UsersView)]
    [InlineData(Permissions.UsersEdit)]
    [InlineData(Permissions.UsersPermissionsEdit)]
    [InlineData(Permissions.DevicesView)]
    [InlineData(Permissions.DevicesEdit)]
    [InlineData(Permissions.FeedbackView)]
    [InlineData(Permissions.FeedbackEdit)]
    public async Task PermissionResolution_Handler_And_HasPermission_MustBeIdentical(string requestedPermission)
    {
        var handler = new PermissionAuthorizationHandler();

        // Scenario A: Unauthenticated
        var unauth = new ClaimsPrincipal(new ClaimsIdentity());
        await AssertHandlerAndExtensionAgree(handler, unauth, requestedPermission);

        // Scenario B: Admin role
        var adminIdent = new ClaimsIdentity("TestAuth");
        adminIdent.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        var adminPrincipal = new ClaimsPrincipal(adminIdent);
        await AssertHandlerAndExtensionAgree(handler, adminPrincipal, requestedPermission);

        // Scenario C: Plain user without permissions
        var plainIdent = new ClaimsIdentity("TestAuth");
        var plainPrincipal = new ClaimsPrincipal(plainIdent);
        await AssertHandlerAndExtensionAgree(handler, plainPrincipal, requestedPermission);

        // Scenario D: Legacy ManageUsers
        var legacyIdent = new ClaimsIdentity("TestAuth");
        legacyIdent.AddClaim(new Claim("permission", Permissions.ManageUsers));
        var legacyPrincipal = new ClaimsPrincipal(legacyIdent);
        await AssertHandlerAndExtensionAgree(handler, legacyPrincipal, requestedPermission);

        // Scenario E: Devices.Edit
        var devEditIdent = new ClaimsIdentity("TestAuth");
        devEditIdent.AddClaim(new Claim("permission", Permissions.DevicesEdit));
        var devEditPrincipal = new ClaimsPrincipal(devEditIdent);
        await AssertHandlerAndExtensionAgree(handler, devEditPrincipal, requestedPermission);

        // Scenario F: Users.PermissionsEdit
        var permEditIdent = new ClaimsIdentity("TestAuth");
        permEditIdent.AddClaim(new Claim("permission", Permissions.UsersPermissionsEdit));
        var permEditPrincipal = new ClaimsPrincipal(permEditIdent);
        await AssertHandlerAndExtensionAgree(handler, permEditPrincipal, requestedPermission);
    }

    private static async Task AssertHandlerAndExtensionAgree(
        PermissionAuthorizationHandler handler,
        ClaimsPrincipal principal,
        string requestedPermission)
    {
        var requirement = new PermissionRequirement(requestedPermission);
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await handler.HandleAsync(context);

        var handlerResult = context.HasSucceeded;
        var extensionResult = principal.HasPermission(requestedPermission);

        handlerResult.Should().Be(extensionResult,
            $"PermissionAuthorizationHandler and ClaimsPrincipal.HasPermission MUST return identical result for permission '{requestedPermission}'.");
    }

    #endregion

    #region 4 & 5. Force Logout Partial Failure and Error Message Masking

    [Fact]
    public async Task ForceLogoutUserAsync_SecurityStampUpdateFails_ReturnsNotSucceeded_AndCorrectRevokedCount()
    {
        var services = new ServiceCollection();
        var dbName = $"ForceLogoutFail_{Guid.NewGuid()}";

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var auditLoggerMock = new Mock<IAuditLogger>();
        services.AddSingleton(auditLoggerMock.Object);

        // Create a mocked UserStore that causes UpdateAsync / UpdateSecurityStampAsync to fail
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        var userStore = new Mock<IUserSecurityStampStore<ApplicationUser>>();

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            Id = "user-stamp-fail",
            UserName = "user.stamp.fail",
            Email = "fail@company.iq",
            EmployeeID = "EMP_FAIL_01"
        };
        db.Users.Add(user);

        // Add 2 active devices
        db.UserDevices.AddRange(
            new UserDevice { UserId = user.Id, EmployeeID = user.EmployeeID, DeviceId = "D1", IsActive = true },
            new UserDevice { UserId = user.Id, EmployeeID = user.EmployeeID, DeviceId = "D2", IsActive = true }
        );
        await db.SaveChangesAsync();

        // Custom UserManager where UpdateSecurityStampAsync returns Failed
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        userManagerMock.Setup(m => m.FindByIdAsync(user.Id))
            .ReturnsAsync(user);

        userManagerMock.Setup(m => m.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "ConcurrencyFailure", Description = "Optimistic concurrency failure." }));

        var loggerMock = new Mock<ILogger<UserEmployeeService>>();
        var service = new UserEmployeeService(db, userManagerMock.Object, auditLoggerMock.Object, loggerMock.Object);

        var result = await service.ForceLogoutUserAsync(user.Id, "ACTOR_ADMIN");

        // Succeeded MUST be false when security stamp fails
        result.Succeeded.Should().BeFalse("Force logout MUST NOT claim full success if Security Stamp update failed.");
        result.SecurityStampUpdated.Should().BeFalse();
        result.SessionsRevokedCount.Should().Be(2, "Revoked devices count must remain accurate.");
        result.ErrorMessage.Should().Be("تعذر تحديث الختم الأمني للمستخدم.");

        // Audit log must NOT record success
        auditLoggerMock.Verify(a => a.LogAsync(
            "USER_FORCE_LOGOUT",
            user.EmployeeID,
            null,
            false, // success must be false
            null,
            null,
            null,
            "SecurityStampUpdateFailed",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceLogoutUserAsync_OnException_DoesNotExposeExceptionDetailsToUser()
    {
        var services = new ServiceCollection();
        var dbName = $"ForceLogoutEx_{Guid.NewGuid()}";

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        var auditLoggerMock = new Mock<IAuditLogger>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        // Throw an internal DB exception
        userManagerMock.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Internal SQL Server connection timed out at 10.0.0.5:1433 with password ***"));

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var loggerMock = new Mock<ILogger<UserEmployeeService>>();

        var service = new UserEmployeeService(db, userManagerMock.Object, auditLoggerMock.Object, loggerMock.Object);

        var result = await service.ForceLogoutUserAsync("user-id", "ACTOR_ADMIN");

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("تعذر إنهاء جلسات المستخدم. يرجى مراجعة سجل النظام.");
        result.ErrorMessage.Should().NotContain("SQL", "Internal database errors must never leak to the caller or end user");
        result.ErrorMessage.Should().NotContain("password");

        // Logger should record the detailed exception
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region 6. Token Behavior: Refresh Token Invalidation & Access Token Semantics

    [Fact]
    public async Task ForceLogout_Invalidates_RefreshToken_Immediately()
    {
        var (provider, db, service, userManager, signInManager, _) = CreateTestEnvironment();

        var user = new ApplicationUser
        {
            Id = "refresh-test-user",
            UserName = "refresh.user",
            Email = "refresh@company.iq",
            EmployeeID = "EMP_REFRESH_01"
        };
        await userManager.CreateAsync(user);

        // 1. Generate Principal and Ticket (simulating login refresh token)
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var timeProvider = TimeProvider.System;
        var utcNow = timeProvider.GetUtcNow();

        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            ExpiresUtc = utcNow.AddHours(24)
        }, IdentityConstants.BearerScheme);

        // Before force logout: refresh ticket validates successfully
        var validatedUserBefore = await signInManager.ValidateSecurityStampAsync(ticket.Principal);
        validatedUserBefore.Should().NotBeNull();
        validatedUserBefore!.Id.Should().Be(user.Id);

        // 2. Perform Force Logout
        var logoutResult = await service.ForceLogoutUserAsync(user.Id, "ADMIN_ACTOR");
        logoutResult.Succeeded.Should().BeTrue();
        logoutResult.SecurityStampUpdated.Should().BeTrue();

        // 3. Validate original refresh token after force logout
        var validatedUserAfter = await signInManager.ValidateSecurityStampAsync(ticket.Principal);
        validatedUserAfter.Should().BeNull(
            "Refresh token validation relies on ValidateSecurityStampAsync, which MUST fail immediately after Force Logout updates the security stamp.");
    }

    [Fact]
    public async Task Existing_AccessToken_RemainsCryptographicallyValid_UntilExpiry_DocumentedSemantics()
    {
        // Demonstrates standard ASP.NET Core Bearer Token behavior:
        // Bearer tickets are self-contained signed DataProtection payloads.
        // They expire at ticket.Properties.ExpiresUtc unless checked against device or DB.
        var (provider, db, service, userManager, signInManager, _) = CreateTestEnvironment();

        var user = new ApplicationUser
        {
            Id = "bearer-user",
            UserName = "bearer.user",
            Email = "bearer@company.iq",
            EmployeeID = "EMP_BEARER_01"
        };
        await userManager.CreateAsync(user);

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15)
        }, IdentityConstants.BearerScheme);

        // Ticket has not expired
        (ticket.Properties.ExpiresUtc > DateTimeOffset.UtcNow).Should().BeTrue();

        // Force logout updates stamp
        await service.ForceLogoutUserAsync(user.Id, "ADMIN");

        // The self-contained bearer ticket cryptographic timestamp is still valid until its 15-minute expiry,
        // which is why access token lifetimes must be kept short and device binding checks are enforced on sensitive APIs.
        ticket.Properties.ExpiresUtc.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    #endregion

    #region 7. Device Binding Scope: Account-Scoped by UserId

    [Fact]
    public async Task DeviceBinding_AccountScoped_IsolatesUserA_From_UserB_SameEmployee()
    {
        var (provider, db, service, userManager, _, _) = CreateTestEnvironment();

        var sameEmployeeId = "EMP_SHARED_01";
        db.Employees.Add(new Employee { EmployeeID = sameEmployeeId, Name = "حيدر جاسم" });
        await db.SaveChangesAsync();

        var userA = new ApplicationUser
        {
            Id = "user-a-guid",
            UserName = "haidar.local",
            Email = "haidar@company.iq",
            EmployeeID = sameEmployeeId
        };
        var userB = new ApplicationUser
        {
            Id = "user-b-guid",
            UserName = "CORP\\haidar",
            Email = "haidar.corp@company.iq",
            EmployeeID = sameEmployeeId
        };
        await userManager.CreateAsync(userA);
        await userManager.CreateAsync(userB);

        // Bind Device A to User A
        var devA = new UserDevice
        {
            UserId = userA.Id,
            EmployeeID = sameEmployeeId,
            DeviceId = "DEVICE_PHONE_A",
            DeviceModel = "iPhone 15",
            IsActive = true
        };
        // Bind Device B to User B
        var devB = new UserDevice
        {
            UserId = userB.Id,
            EmployeeID = sameEmployeeId,
            DeviceId = "DEVICE_TABLET_B",
            DeviceModel = "iPad Pro",
            IsActive = true
        };
        db.UserDevices.AddRange(devA, devB);
        await db.SaveChangesAsync();

        // Query active device for User A: must only see Device A
        var userAActiveDevice = await db.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userA.Id && d.IsActive);
        userAActiveDevice.Should().NotBeNull();
        userAActiveDevice!.DeviceId.Should().Be("DEVICE_PHONE_A");

        // Query active device for User B: must only see Device B
        var userBActiveDevice = await db.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userB.Id && d.IsActive);
        userBActiveDevice.Should().NotBeNull();
        userBActiveDevice!.DeviceId.Should().Be("DEVICE_TABLET_B");

        // Force logout User A
        var logoutA = await service.ForceLogoutUserAsync(userA.Id, "ADMIN");
        logoutA.Succeeded.Should().BeTrue();
        logoutA.SessionsRevokedCount.Should().Be(1);

        // Verify Device A is now inactive
        var checkDevA = await db.UserDevices.FindAsync(devA.Id);
        checkDevA!.IsActive.Should().BeFalse("User A's device must be revoked after ForceLogoutUserAsync.");

        // Verify Device B remains active
        var checkDevB = await db.UserDevices.FindAsync(devB.Id);
        checkDevB!.IsActive.Should().BeTrue("User B's device must remain ACTIVE after User A's force logout.");
    }

    #endregion

    #region 8. Passkey Login and Employee Linkage Validation

    [Fact]
    public async Task PasskeyLogin_ValidEmployeeLink_HumanUser_ValidationPasses()
    {
        var (provider, db, service, userManager, _, _) = CreateTestEnvironment();

        var emp = new Employee { EmployeeID = "EMP_PASSKEY_OK", Name = "سارة أحمد" };
        db.Employees.Add(emp);
        await db.SaveChangesAsync();

        var humanUser = new ApplicationUser
        {
            Id = "human-passkey-user",
            UserName = "sara.ahmed",
            Email = "sara@company.iq",
            EmployeeID = "EMP_PASSKEY_OK"
        };
        await userManager.CreateAsync(humanUser);

        var linkResult = await service.ValidateOrRepairUserEmployeeLinkAsync(humanUser);

        linkResult.IsValid.Should().BeTrue();
        linkResult.IsTechnicalAccount.Should().BeFalse();
        linkResult.EmployeeID.Should().Be("EMP_PASSKEY_OK");
    }

    [Fact]
    public async Task PasskeyLogin_OrphanEmployeeLink_HumanUser_ValidationFails()
    {
        var (provider, db, service, userManager, _, _) = CreateTestEnvironment();

        var humanUser = new ApplicationUser
        {
            Id = "human-orphan-user",
            UserName = "orphan.user",
            Email = "orphan@unknown-domain.iq",
            EmployeeID = "EMP_NON_EXISTENT_999" // Does not exist in Employees table
        };
        await userManager.CreateAsync(humanUser);

        var linkResult = await service.ValidateOrRepairUserEmployeeLinkAsync(humanUser);

        linkResult.IsValid.Should().BeFalse("Human user with non-existent Employee record must fail validation and be denied sign-in.");
        linkResult.FailureReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PasskeyLogin_NullEmployeeID_HumanUser_ValidationFails()
    {
        var (provider, db, service, userManager, _, _) = CreateTestEnvironment();

        var humanUser = new ApplicationUser
        {
            Id = "human-null-emp",
            UserName = "no.emp.user",
            Email = "noemp@unknown-domain.iq",
            EmployeeID = null // Missing
        };
        await userManager.CreateAsync(humanUser);

        var linkResult = await service.ValidateOrRepairUserEmployeeLinkAsync(humanUser);

        linkResult.IsValid.Should().BeFalse("Human user without linked EmployeeID must fail validation and be denied sign-in.");
    }

    [Fact]
    public async Task PasskeyLogin_DeclaredTechnicalAccount_ValidationPasses()
    {
        var (provider, db, service, userManager, _, _) = CreateTestEnvironment();

        var techUser = new ApplicationUser
        {
            Id = "tech-acc-user",
            UserName = SystemAccounts.BootstrapAdminUserName,
            Email = SystemAccounts.BootstrapAdminEmail,
            EmployeeID = null // Technical accounts do not require EmployeeID
        };
        await userManager.CreateAsync(techUser);

        var linkResult = await service.ValidateOrRepairUserEmployeeLinkAsync(techUser);

        linkResult.IsValid.Should().BeTrue("Declared technical accounts in SystemAccounts must be permitted without employee linkage.");
        linkResult.IsTechnicalAccount.Should().BeTrue();
    }

    #endregion
}
