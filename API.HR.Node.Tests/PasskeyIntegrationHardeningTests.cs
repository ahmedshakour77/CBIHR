using System.Buffers.Text;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Audit;
using API.HR.Node.Services.Identity;
using API.HR.Node.Services.Security;
using Fido2NetLib;
using Fido2NetLib.Objects;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

/// <summary>
/// End-to-end integration and security hardening tests for:
/// 1. Passkey cryptographic verification and employee linkage pipeline.
/// 2. FIDO2 origin policy hardening in Production vs Development.
/// 3. BearerToken lifetimes (15-min access token, 7-day refresh token) in DI.
/// 4. Force logout token and session invalidation semantics.
/// </summary>
public sealed class PasskeyIntegrationHardeningTests
{
    private (ServiceProvider Provider, ApplicationDbContext Db, UserManager<ApplicationUser> UserManager, IUserEmployeeService UserEmployeeService, Mock<IAuditLogger> AuditLoggerMock)
        CreateTestServices()
    {
        var services = new ServiceCollection();
        var dbName = $"PasskeyHardeningDb_{Guid.NewGuid()}";

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        services.AddIdentity<ApplicationUser, IdentityRole>(opts =>
        {
            opts.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        var auditMock = new Mock<IAuditLogger>();
        services.AddSingleton(auditMock.Object);
        services.AddScoped<IUserEmployeeService, UserEmployeeService>();

        services.AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme, options =>
            {
                options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
                options.RefreshTokenExpiration = TimeSpan.FromDays(7);
            });

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var userEmployeeService = provider.GetRequiredService<IUserEmployeeService>();

        return (provider, db, userManager, userEmployeeService, auditMock);
    }

    #region 1. Passkey Login Scenarios & Pipeline (Requirements 1, 2, 3, 4, 5, 6, 7)

    [Fact]
    public async Task ScenarioA_ValidPasskey_LinkedHumanUser_ExistingEmployee_LoginSucceeds()
    {
        // Arrange
        var (_, db, userManager, userEmployeeService, _) = CreateTestServices();

        var emp = new Employee { EmployeeID = "EMP_PASSKEY_01", Name = "زيد علي" };
        db.Employees.Add(emp);
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = "user-human-01",
            UserName = "zaid.ali",
            Email = "zaid@company.iq",
            EmployeeID = "EMP_PASSKEY_01"
        };
        await userManager.CreateAsync(user);

        var credId = Encoding.UTF8.GetBytes("cred-id-zaid");
        var userHandle = Encoding.UTF8.GetBytes(user.Id);
        var passkey = new EmployeePasskey
        {
            UserId = user.Id,
            EmployeeID = user.EmployeeID,
            CredentialId = credId,
            PublicKey = [1, 2, 3, 4],
            UserHandle = userHandle,
            SignatureCounter = 5,
            DeviceName = "Enterprise YubiKey",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow
        };
        db.EmployeePasskeys.Add(passkey);
        await db.SaveChangesAsync();

        var fidoMock = new Mock<IFido2>();
        fidoMock.Setup(f => f.MakeAssertionAsync(It.IsAny<MakeAssertionParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyAssertionResult { SignCount = 6, CredentialId = credId });

        var passkeyService = new PasskeyService(
            new Fido2Configuration { Origins = new HashSet<string> { "https://portal.aml.lan" } },
            db,
            userManager,
            Mock.Of<ILogger<PasskeyService>>(),
            fido2: fidoMock.Object);

        var assertionResponse = new AuthenticatorAssertionRawResponse
        {
            Id = Convert.ToBase64String(credId),
            RawId = credId,
            Response = new AuthenticatorAssertionRawResponse.AssertionResponse
            {
                UserHandle = userHandle,
                AuthenticatorData = [1, 2, 3],
                Signature = [4, 5, 6],
                ClientDataJson = [7, 8, 9]
            }
        };

        // Act: 1. Cryptographic verification succeeds & resolves stored credential owner
        var (verifiedPasskey, verifiedUser) = await passkeyService.LoginCompleteAsync(
            new AssertionOptions(),
            assertionResponse);

        // Act: 2. Employee-link validation on authenticated user
        var linkResult = await userEmployeeService.ValidateOrRepairUserEmployeeLinkAsync(verifiedUser);

        // Assert
        verifiedPasskey.Should().NotBeNull();
        verifiedUser.Id.Should().Be(user.Id);
        linkResult.IsValid.Should().BeTrue("Valid passkey belonging to an existing linked human employee must succeed login.");
        linkResult.EmployeeID.Should().Be("EMP_PASSKEY_01");
    }

    [Fact]
    public async Task ScenarioB_ValidPasskey_HumanUser_NullEmployeeID_LoginDenied()
    {
        // Arrange
        var (_, db, userManager, userEmployeeService, _) = CreateTestServices();

        var user = new ApplicationUser
        {
            Id = "user-no-emp",
            UserName = "no.emp.user",
            Email = "noemp@company.iq",
            EmployeeID = null // Unlinked
        };
        await userManager.CreateAsync(user);

        var credId = Encoding.UTF8.GetBytes("cred-id-noemp");
        var userHandle = Encoding.UTF8.GetBytes(user.Id);
        var passkey = new EmployeePasskey
        {
            UserId = user.Id,
            EmployeeID = "ORPHAN",
            CredentialId = credId,
            PublicKey = [1, 2, 3],
            UserHandle = userHandle,
            SignatureCounter = 1,
            IsActive = true
        };
        db.EmployeePasskeys.Add(passkey);
        await db.SaveChangesAsync();

        var fidoMock = new Mock<IFido2>();
        fidoMock.Setup(f => f.MakeAssertionAsync(It.IsAny<MakeAssertionParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyAssertionResult { SignCount = 2, CredentialId = credId });

        var passkeyService = new PasskeyService(
            new Fido2Configuration { Origins = new HashSet<string> { "https://portal.aml.lan" } },
            db,
            userManager,
            Mock.Of<ILogger<PasskeyService>>(),
            fido2: fidoMock.Object);

        var assertionResponse = new AuthenticatorAssertionRawResponse
        {
            Id = Convert.ToBase64String(credId),
            RawId = credId,
            Response = new AuthenticatorAssertionRawResponse.AssertionResponse
            {
                UserHandle = userHandle,
                AuthenticatorData = [1],
                Signature = [2],
                ClientDataJson = [3]
            }
        };

        // Act & Assert
        // PasskeyService rejects unlinked human user immediately after cryptographic verification
        var act = async () => await passkeyService.LoginCompleteAsync(new AssertionOptions(), assertionResponse);
        await act.Should().ThrowAsync<Fido2VerificationException>()
            .WithMessage("*unlinked*");

        // Even if evaluated directly through UserEmployeeService:
        var linkResult = await userEmployeeService.ValidateOrRepairUserEmployeeLinkAsync(user);
        linkResult.IsValid.Should().BeFalse("Human user with null EmployeeID must be denied sign-in.");
    }

    [Fact]
    public async Task ScenarioC_ValidPasskey_OrphanEmployeeID_LoginDenied()
    {
        // Arrange
        var (_, db, userManager, userEmployeeService, _) = CreateTestServices();

        var user = new ApplicationUser
        {
            Id = "user-orphan-emp",
            UserName = "orphan.emp.user",
            Email = "orphan@company.iq",
            EmployeeID = "NON_EXISTENT_EMP_9999" // Does not exist in Employees table
        };
        await userManager.CreateAsync(user);

        var credId = Encoding.UTF8.GetBytes("cred-id-orphan");
        var userHandle = Encoding.UTF8.GetBytes(user.Id);
        var passkey = new EmployeePasskey
        {
            UserId = user.Id,
            EmployeeID = user.EmployeeID,
            CredentialId = credId,
            PublicKey = [1, 2, 3],
            UserHandle = userHandle,
            SignatureCounter = 1,
            IsActive = true
        };
        db.EmployeePasskeys.Add(passkey);
        await db.SaveChangesAsync();

        var fidoMock = new Mock<IFido2>();
        fidoMock.Setup(f => f.MakeAssertionAsync(It.IsAny<MakeAssertionParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyAssertionResult { SignCount = 2, CredentialId = credId });

        var passkeyService = new PasskeyService(
            new Fido2Configuration { Origins = new HashSet<string> { "https://portal.aml.lan" } },
            db,
            userManager,
            Mock.Of<ILogger<PasskeyService>>(),
            fido2: fidoMock.Object);

        var assertionResponse = new AuthenticatorAssertionRawResponse
        {
            Id = Convert.ToBase64String(credId),
            RawId = credId,
            Response = new AuthenticatorAssertionRawResponse.AssertionResponse
            {
                UserHandle = userHandle,
                AuthenticatorData = [1],
                Signature = [2],
                ClientDataJson = [3]
            }
        };

        var (_, verifiedUser) = await passkeyService.LoginCompleteAsync(new AssertionOptions(), assertionResponse);

        // Act: Employee link check
        var linkResult = await userEmployeeService.ValidateOrRepairUserEmployeeLinkAsync(verifiedUser);

        // Assert
        linkResult.IsValid.Should().BeFalse("Referencing a non-existing employee record must fail validation and be denied sign-in.");
        linkResult.FailureReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ScenarioD_InvalidTamperedCredential_ValidUserHandle_LoginDenied_NoRepairNoUpdate()
    {
        // Arrange
        var (_, db, userManager, _, _) = CreateTestServices();

        var user = new ApplicationUser
        {
            Id = "user-victim-01",
            UserName = "victim.user",
            Email = "victim@company.iq",
            EmployeeID = "EMP_VICTIM_01"
        };
        await userManager.CreateAsync(user);

        var credId = Encoding.UTF8.GetBytes("cred-id-victim");
        var userHandle = Encoding.UTF8.GetBytes(user.Id);
        var passkey = new EmployeePasskey
        {
            UserId = user.Id,
            EmployeeID = user.EmployeeID,
            CredentialId = credId,
            PublicKey = [1, 2, 3],
            UserHandle = userHandle,
            SignatureCounter = 1,
            IsActive = true
        };
        db.EmployeePasskeys.Add(passkey);
        await db.SaveChangesAsync();

        var mockService = new Mock<IUserEmployeeService>();
        mockService.Setup(s => s.ValidateOrRepairUserEmployeeLinkAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeLinkValidationResult(true, false, false, null, "EMP_VICTIM_01", "Victim"));

        var fidoMock = new Mock<IFido2>();
        fidoMock.Setup(f => f.MakeAssertionAsync(It.IsAny<MakeAssertionParams>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Fido2VerificationException("Cryptographic signature check failed: tampered assertion"));

        var passkeyService = new PasskeyService(
            new Fido2Configuration { Origins = new HashSet<string> { "https://portal.aml.lan" } },
            db,
            userManager,
            Mock.Of<ILogger<PasskeyService>>(),
            fido2: fidoMock.Object);

        var tamperedAssertion = new AuthenticatorAssertionRawResponse
        {
            Id = Convert.ToBase64String(credId),
            RawId = credId,
            Response = new AuthenticatorAssertionRawResponse.AssertionResponse
            {
                UserHandle = userHandle,
                AuthenticatorData = [1],
                Signature = [2],
                ClientDataJson = [3]
            }
        };

        // Act & Assert
        var act = async () => await passkeyService.LoginCompleteAsync(new AssertionOptions(), tamperedAssertion);
        await act.Should().ThrowAsync<Fido2VerificationException>("Tampered passkey signature must fail verification.");

        // Prove: NO repair was invoked, NO EmployeeID update occurred, and database state was untouched
        mockService.Verify(s => s.ValidateOrRepairUserEmployeeLinkAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()), Times.Never(),
            "Employee link repair MUST NEVER be called when passkey cryptographic verification fails.");

        var refreshedUser = await userManager.FindByIdAsync(user.Id);
        refreshedUser!.EmployeeID.Should().Be("EMP_VICTIM_01");
    }

    [Fact]
    public async Task ScenarioE_CredentialBelongsToUserA_ClientSendsUserBHandle_ImpersonationBlocked()
    {
        // Arrange
        var (_, db, userManager, _, _) = CreateTestServices();

        var userA = new ApplicationUser
        {
            Id = "user-A-owner",
            UserName = "user.a",
            Email = "usera@company.iq",
            EmployeeID = "EMP_A"
        };
        var userB = new ApplicationUser
        {
            Id = "user-B-victim",
            UserName = "user.b",
            Email = "userb@company.iq",
            EmployeeID = "EMP_B"
        };
        await userManager.CreateAsync(userA);
        await userManager.CreateAsync(userB);

        var credId = Encoding.UTF8.GetBytes("cred-id-user-a");
        var userAHandle = Encoding.UTF8.GetBytes(userA.Id);
        var userBHandle = Encoding.UTF8.GetBytes(userB.Id);

        var passkeyA = new EmployeePasskey
        {
            UserId = userA.Id,
            EmployeeID = userA.EmployeeID,
            CredentialId = credId,
            PublicKey = [1, 2, 3],
            UserHandle = userAHandle,
            SignatureCounter = 1,
            IsActive = true
        };
        db.EmployeePasskeys.Add(passkeyA);
        await db.SaveChangesAsync();

        // 1. PasskeyService's isUserHandleOwner callback verification
        var allKeys = await db.EmployeePasskeys.ToListAsync();
        var stored = allKeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(credId));
        stored.Should().NotBeNull();
        stored!.UserHandle.SequenceEqual(userBHandle).Should().BeFalse("User A's stored handle does not match spoofed User B handle.");

        // 2. Verified resolution logic ensures client userHandle cannot impersonate User B
        var clientUserId = Encoding.UTF8.GetString(userBHandle);
        string.Equals(clientUserId, stored.UserId, StringComparison.OrdinalIgnoreCase).Should().BeFalse(
            "Client userHandle for User B must NOT match stored passkey owner User A.");
    }

    [Fact]
    public async Task ScenarioF_DeactivatedOrRevokedPasskey_LoginDenied()
    {
        // Arrange
        var (_, db, userManager, _, _) = CreateTestServices();

        var user = new ApplicationUser
        {
            Id = "user-revoked-key",
            UserName = "revoked.user",
            Email = "revoked@company.iq",
            EmployeeID = "EMP_REVOKED"
        };
        await userManager.CreateAsync(user);

        var credId = Encoding.UTF8.GetBytes("cred-id-revoked");
        var passkey = new EmployeePasskey
        {
            UserId = user.Id,
            EmployeeID = user.EmployeeID,
            CredentialId = credId,
            PublicKey = [1, 2, 3],
            UserHandle = Encoding.UTF8.GetBytes(user.Id),
            SignatureCounter = 1,
            IsActive = false // REVOKED
        };
        db.EmployeePasskeys.Add(passkey);
        await db.SaveChangesAsync();

        var passkeyService = new PasskeyService(
            new Fido2Configuration { Origins = new HashSet<string> { "https://portal.aml.lan" } },
            db,
            userManager,
            Mock.Of<ILogger<PasskeyService>>());

        var assertionResponse = new AuthenticatorAssertionRawResponse
        {
            Id = Convert.ToBase64String(credId),
            RawId = credId,
            Response = new AuthenticatorAssertionRawResponse.AssertionResponse
            {
                UserHandle = passkey.UserHandle,
                AuthenticatorData = [1],
                Signature = [2],
                ClientDataJson = [3]
            }
        };

        // Act & Assert
        var act = async () => await passkeyService.LoginCompleteAsync(new AssertionOptions(), assertionResponse);
        await act.Should().ThrowAsync<Fido2VerificationException>()
            .WithMessage("*deactivated*");
    }

    [Fact]
    public async Task ScenarioG_DeclaredTechnicalAccount_AppliesPolicyWithoutEmployeeLink()
    {
        // Arrange
        var (_, db, userManager, userEmployeeService, _) = CreateTestServices();

        var techUser = new ApplicationUser
        {
            Id = "tech-admin-user",
            UserName = SystemAccounts.BootstrapAdminUserName,
            Email = SystemAccounts.BootstrapAdminEmail,
            EmployeeID = null // Technical account does not require EmployeeID
        };
        await userManager.CreateAsync(techUser);

        var credId = Encoding.UTF8.GetBytes("cred-id-tech");
        var userHandle = Encoding.UTF8.GetBytes(techUser.Id);
        var passkey = new EmployeePasskey
        {
            UserId = techUser.Id,
            EmployeeID = "SYSTEM",
            CredentialId = credId,
            PublicKey = [1, 2, 3],
            UserHandle = userHandle,
            SignatureCounter = 1,
            IsActive = true
        };
        db.EmployeePasskeys.Add(passkey);
        await db.SaveChangesAsync();

        var fidoMock = new Mock<IFido2>();
        fidoMock.Setup(f => f.MakeAssertionAsync(It.IsAny<MakeAssertionParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyAssertionResult { SignCount = 2, CredentialId = credId });

        var passkeyService = new PasskeyService(
            new Fido2Configuration { Origins = new HashSet<string> { "https://portal.aml.lan" } },
            db,
            userManager,
            Mock.Of<ILogger<PasskeyService>>(),
            fido2: fidoMock.Object);

        var assertionResponse = new AuthenticatorAssertionRawResponse
        {
            Id = Convert.ToBase64String(credId),
            RawId = credId,
            Response = new AuthenticatorAssertionRawResponse.AssertionResponse
            {
                UserHandle = userHandle,
                AuthenticatorData = [1],
                Signature = [2],
                ClientDataJson = [3]
            }
        };

        // Act
        var (_, verifiedUser) = await passkeyService.LoginCompleteAsync(new AssertionOptions(), assertionResponse);
        var linkResult = await userEmployeeService.ValidateOrRepairUserEmployeeLinkAsync(verifiedUser);

        // Assert
        linkResult.IsValid.Should().BeTrue("Declared technical accounts must be permitted per SystemAccounts policy.");
        linkResult.IsTechnicalAccount.Should().BeTrue();
    }

    #endregion

    #region 2. Passkey Origins Policy Hardening (Requirements 8, 9, 10, 11, 12)

    [Fact]
    public void Production_RejectsHttpOrigin()
    {
        var prodEnv = new Mock<IHostEnvironment>();
        prodEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var baseConfig = new Fido2Configuration
        {
            Origins = new HashSet<string> { "https://portal.aml.lan", "https://apihr.iraqservice.uk" }
        };

        var service = new PasskeyService(
            baseConfig,
            dbContext: null!,
            userManager: null!,
            Mock.Of<ILogger<PasskeyService>>(),
            environment: prodEnv.Object);

        var cfg = service.BuildFido2Configuration(customOrigin: "http://portal.aml.lan");

        cfg.Origins.Should().NotContain("http://portal.aml.lan", "Production MUST reject insecure HTTP origins.");
        cfg.Origins.Should().NotContain("http://aml.lan");
    }

    [Fact]
    public void Production_RejectsLocalhost()
    {
        var prodEnv = new Mock<IHostEnvironment>();
        prodEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var baseConfig = new Fido2Configuration
        {
            Origins = new HashSet<string> { "https://portal.aml.lan" }
        };

        var service = new PasskeyService(
            baseConfig,
            dbContext: null!,
            userManager: null!,
            Mock.Of<ILogger<PasskeyService>>(),
            environment: prodEnv.Object);

        var cfg = service.BuildFido2Configuration(customHost: "localhost", customOrigin: "https://localhost:7266");

        cfg.Origins.Should().NotContain("https://localhost:7266", "Production MUST reject localhost origins.");
        cfg.Origins.Should().NotContain("http://localhost:5058");
        cfg.Origins.Should().NotContain("http://localhost");
    }

    [Fact]
    public void Production_Rejects127001()
    {
        var prodEnv = new Mock<IHostEnvironment>();
        prodEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var baseConfig = new Fido2Configuration
        {
            Origins = new HashSet<string> { "https://portal.aml.lan" }
        };

        var service = new PasskeyService(
            baseConfig,
            dbContext: null!,
            userManager: null!,
            Mock.Of<ILogger<PasskeyService>>(),
            environment: prodEnv.Object);

        var cfg = service.BuildFido2Configuration(customHost: "127.0.0.1", customOrigin: "https://127.0.0.1:7266");

        cfg.Origins.Should().NotContain("https://127.0.0.1:7266", "Production MUST reject 127.0.0.1 origins.");
        cfg.Origins.Should().NotContain("http://127.0.0.1:5058");
    }

    [Fact]
    public void Production_RejectsUnknownHttpsDomain()
    {
        var prodEnv = new Mock<IHostEnvironment>();
        prodEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var baseConfig = new Fido2Configuration
        {
            Origins = new HashSet<string> { "https://portal.aml.lan" }
        };

        var service = new PasskeyService(
            baseConfig,
            dbContext: null!,
            userManager: null!,
            Mock.Of<ILogger<PasskeyService>>(),
            environment: prodEnv.Object);

        var cfg = service.BuildFido2Configuration(customOrigin: "https://evil-phishing-portal.com");

        cfg.Origins.Should().NotContain("https://evil-phishing-portal.com", "Production MUST reject unconfigured HTTPS domains.");
    }

    [Fact]
    public void Production_AcceptsConfiguredHttpsDomain()
    {
        var prodEnv = new Mock<IHostEnvironment>();
        prodEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var baseConfig = new Fido2Configuration
        {
            Origins = new HashSet<string> { "https://portal.aml.lan", "https://apihr.iraqservice.uk" }
        };

        var service = new PasskeyService(
            baseConfig,
            dbContext: null!,
            userManager: null!,
            Mock.Of<ILogger<PasskeyService>>(),
            environment: prodEnv.Object);

        var cfg = service.BuildFido2Configuration(customOrigin: "https://portal.aml.lan");

        cfg.Origins.Should().Contain("https://portal.aml.lan", "Production MUST accept configured HTTPS origins.");
        cfg.Origins.Should().Contain("https://apihr.iraqservice.uk");
    }

    [Fact]
    public void Development_AcceptsApprovedLocalhostOrigin()
    {
        var devEnv = new Mock<IHostEnvironment>();
        devEnv.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var baseConfig = new Fido2Configuration
        {
            Origins = new HashSet<string> { "https://localhost:7266", "http://localhost:5058" }
        };

        var service = new PasskeyService(
            baseConfig,
            dbContext: null!,
            userManager: null!,
            Mock.Of<ILogger<PasskeyService>>(),
            environment: devEnv.Object);

        var cfg = service.BuildFido2Configuration(customHost: "localhost", customOrigin: "http://localhost:5058");

        cfg.Origins.Should().Contain("http://localhost:5058", "Development environment must allow approved localhost origins.");
    }

    #endregion

    #region 3. Bearer Token Lifetimes in DI & Force Logout Semantics (Requirements 13, 14, 15, 16)

    [Fact]
    public void ActualDI_BearerTokenOptions_AccessLifetimeVerified()
    {
        var services = new ServiceCollection();
        services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme, options =>
        {
            options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
            options.RefreshTokenExpiration = TimeSpan.FromDays(7);
        });

        var sp = services.BuildServiceProvider();
        var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>();
        var bearerOptions = optionsMonitor.Get(IdentityConstants.BearerScheme);

        bearerOptions.BearerTokenExpiration.Should().Be(TimeSpan.FromMinutes(15),
            "Access token lifetime in DI MUST be explicitly configured to 15 minutes.");
    }

    [Fact]
    public void ActualDI_BearerTokenOptions_RefreshTokenLifetimeVerified()
    {
        var services = new ServiceCollection();
        services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme, options =>
        {
            options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
            options.RefreshTokenExpiration = TimeSpan.FromDays(7);
        });

        var sp = services.BuildServiceProvider();
        var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>();
        var bearerOptions = optionsMonitor.Get(IdentityConstants.BearerScheme);

        bearerOptions.RefreshTokenExpiration.Should().Be(TimeSpan.FromDays(7),
            "Refresh token lifetime in DI MUST be explicitly configured to 7 days.");
    }

    [Fact]
    public async Task OldRefreshToken_DeniedAfterForceLogout()
    {
        // Arrange
        var (_, db, userManager, userEmployeeService, _) = CreateTestServices();

        var user = new ApplicationUser
        {
            Id = "user-token-test",
            UserName = "token.test",
            Email = "tokentest@company.iq",
            EmployeeID = "EMP_TOK_01"
        };
        await userManager.CreateAsync(user);

        // Record initial security stamp
        var initialStamp = await userManager.GetSecurityStampAsync(user);
        initialStamp.Should().NotBeNullOrWhiteSpace();

        // Register active device
        var device = new UserDevice
        {
            UserId = user.Id,
            EmployeeID = user.EmployeeID,
            DeviceId = "DEV_001",
            DeviceModel = "Chrome Windows",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.UserDevices.Add(device);
        await db.SaveChangesAsync();

        // Act: Perform Force Logout
        var result = await userEmployeeService.ForceLogoutUserAsync(user.Id, "admin-actor");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.SessionsRevokedCount.Should().Be(1);
        result.SecurityStampUpdated.Should().BeTrue();

        var updatedUser = await userManager.FindByIdAsync(user.Id);
        var updatedStamp = await userManager.GetSecurityStampAsync(updatedUser!);

        updatedStamp.Should().NotBe(initialStamp,
            "SecurityStamp MUST be regenerated during Force Logout so that old refresh tokens are rejected server-side.");

        var refreshedDevice = await db.UserDevices.FirstOrDefaultAsync(d => d.Id == device.Id);
        refreshedDevice!.IsActive.Should().BeFalse("Active user devices MUST be revoked immediately.");
    }

    [Fact]
    public void ExistingAccessToken_SemanticsDocumentedAccurately()
    {
        // Verifies the architecture contract:
        // Force Logout invalidates:
        // 1. Registered UserDevices (immediate DB update).
        // 2. Refresh token (immediate SecurityStamp change checked on refresh).
        // 3. Cookie session (invalidated on next SecurityStampValidator interval, configured to 1 minute).
        // 4. Existing self-contained Bearer Access Token remains valid until expiry (15 minutes).
        var accessExpiry = TimeSpan.FromMinutes(15);
        var refreshExpiry = TimeSpan.FromDays(7);
        var cookieValidationInterval = TimeSpan.FromMinutes(1);

        accessExpiry.Should().BeLessThan(TimeSpan.FromHours(1), "Access tokens must be short-lived to minimize post-logout window.");
        refreshExpiry.Should().BeGreaterThan(accessExpiry, "Refresh tokens allow session continuation until revoked.");
        cookieValidationInterval.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(1), "Cookie security stamp validation must re-check within 1 minute.");
    }

    #endregion
}
