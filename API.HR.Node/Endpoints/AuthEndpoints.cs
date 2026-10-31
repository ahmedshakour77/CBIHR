using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Audit;
using API.HR.Node.Services.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;

namespace API.HR.Node.Endpoints;

public static class AuthEndpoints
{
    private const string MicrosoftLoginProvider = "MicrosoftEntra";

    // ConfigurationManager caches OpenID Connect metadata and Microsoft signing keys,
    // and refreshes them when Microsoft rotates keys.
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>
        MicrosoftConfigurationManagers = new(StringComparer.OrdinalIgnoreCase);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Authentication")
            .RequireRateLimiting("auth-limit");

        group.MapPost("/login", Login)
            .AllowAnonymous();

        group.MapPost("/microsoft-login", MicrosoftLogin)
            .AllowAnonymous()
            .WithName("MicrosoftMobileLogin")
            .WithSummary("Validates a Microsoft Entra ID token and signs the employee into the HR API.");

        group.MapPost("/ad-login", ActiveDirectoryLogin)
            .AllowAnonymous()
            .WithName("ActiveDirectoryLogin")
            .WithSummary("Validates Active Directory domain credentials (aml.lan) and signs the employee into the HR API.");

        group.MapPost("/refresh", Refresh)
            .AllowAnonymous();

        group.MapGet("/me", GetCurrentUserContext)
            .RequireAuthorization(AuthorizationPolicies.ApiBearer)
            .WithName("GetCurrentUserAuthContext")
            .WithSummary("Returns current user account context, linked employee ID, and assigned roles.");

        return endpoints;
    }

    private static async Task<IResult> GetCurrentUserContext(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        var hasPhoto = !string.IsNullOrWhiteSpace(user.EmployeeID) &&
            await dbContext.EmployeeProfilePhotos.AnyAsync(p => p.EmployeeID == user.EmployeeID && p.IsActive, cancellationToken);

        var employee = !string.IsNullOrWhiteSpace(user.EmployeeID)
            ? await dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeID == user.EmployeeID, cancellationToken)
            : null;

        var isAdmin = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) ||
                      roles.Contains("HR_Admin", StringComparer.OrdinalIgnoreCase) ||
                      roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase) ||
                      principal.IsInRole("Admin") || principal.IsInRole("HR_Admin");

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.FindAll("permission"))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
            {
                permissions.Add(claim.Value);
            }
        }
        if (isAdmin)
        {
            permissions.Add("VIEW_ALL_EMPLOYEES");
            permissions.Add("MANAGE_DOCUMENTS");
            permissions.Add("VERIFY_DOCUMENTS");
            permissions.Add("EXPORT_REPORTS");
        }

        return Results.Ok(new
        {
            userId = user.Id,
            userName = user.UserName,
            email = user.Email,
            phoneNumber = user.PhoneNumber,
            employeeId = user.EmployeeID,
            employeeID = user.EmployeeID,
            name = employee?.Name ?? user.UserName ?? "",
            roles = roles,
            isAdmin = isAdmin,
            permissions = permissions.ToList(),
            hasProfilePhoto = hasPhoto
        });
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext dbContext,
        IAuditLogger auditLogger,
        ILoggerFactory loggerFactory,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AuthEndpoints");

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new
            {
                message = "Email and password are required."
            });
        }

        var input = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(input)
                ?? await userManager.FindByNameAsync(input);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        // Require human user to be linked to an existing Employee record (SystemAccounts exempted)
        var userEmployeeService = httpContext.RequestServices.GetRequiredService<API.HR.Node.Services.Identity.IUserEmployeeService>();
        var linkResult = await userEmployeeService.ValidateOrRepairUserEmployeeLinkAsync(user, cancellationToken);
        if (!linkResult.IsValid)
        {
            logger.LogWarning("User {UserId} attempted login but is not linked to an employee record. Reason: {Reason}", user.Id, linkResult.FailureReason);

            return Results.Json(new
            {
                message = linkResult.FailureReason ?? "هذا الحساب غير مرتبط بسجل موظف في نظام الموارد البشرية."
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgentInfo = GetDeviceUserAgentString(httpContext, request.DeviceInfo, request.DeviceModel, request.OperatingSystem, request.AppVersion, request.DeviceId);

        if (result.Succeeded)
        {
            var deviceCheckResult = await ValidateAndRegisterDeviceAsync(
                user,
                request.DeviceInfo,
                request.DeviceId,
                request.DeviceModel,
                request.OperatingSystem,
                request.AppVersion,
                ipAddress,
                dbContext,
                logger,
                cancellationToken);

            if (deviceCheckResult is not null)
            {
                await auditLogger.LogAsync(
                    action: "Login_DeviceMismatch_Blocked",
                    employeeID: user.EmployeeID!,
                    photoId: null,
                    success: false,
                    user: null,
                    ipAddress: ipAddress,
                    userAgent: userAgentInfo,
                    failureReason: "UnauthorizedDeviceAttempt",
                    cancellationToken: cancellationToken);

                return deviceCheckResult;
            }

            var principal = await signInManager.CreateUserPrincipalAsync(user);

            await auditLogger.LogAsync(
                action: "Login_Success",
                employeeID: user.EmployeeID,
                photoId: null,
                success: true,
                user: principal,
                ipAddress: ipAddress,
                userAgent: userAgentInfo,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Login succeeded for user {UserId}, EmployeeID: {EmployeeID}, Device: {DeviceModel}, OS: {OperatingSystem}, AppVersion: {AppVersion}, IP: {IpAddress}",
                user.Id, user.EmployeeID, request.DeviceModel ?? "N/A", request.OperatingSystem ?? "N/A", request.AppVersion ?? "N/A", ipAddress ?? "N/A");

            return await CreateBearerTokenResultAsync(
                user,
                principal,
                userManager,
                dbContext,
                bearerTokenOptions,
                timeProvider,
                cancellationToken);
        }

        await auditLogger.LogAsync(
            action: "Login_Failed",
            employeeID: user.EmployeeID,
            photoId: null,
            success: false,
            user: null,
            ipAddress: ipAddress,
            userAgent: userAgentInfo,
            failureReason: result.IsLockedOut ? "AccountLocked" : (result.IsNotAllowed ? "NotAllowed" : "InvalidPassword"),
            cancellationToken: cancellationToken);

        if (result.IsLockedOut)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Account is locked.");
        }

        if (result.IsNotAllowed)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Login is not allowed. Confirm the email first.");
        }

        if (result.RequiresTwoFactor)
        {
            return Results.BadRequest(new
            {
                message = "Two-factor authentication is required."
            });
        }

        return Results.Unauthorized();
    }

    private static async Task<IResult> Refresh(
        RefreshRequest request,
        SignInManager<ApplicationUser> signInManager,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.BadRequest(new
            {
                message = "Refresh token is required."
            });
        }

        var protector = bearerTokenOptions
            .Get(IdentityConstants.BearerScheme)
            .RefreshTokenProtector;

        var ticket = protector.Unprotect(request.RefreshToken);

        // Reject an expired, tampered, or stale refresh token.
        // A security-stamp change invalidates existing refresh tokens.
        if (ticket?.Properties?.ExpiresUtc is not { } expiresUtc ||
            timeProvider.GetUtcNow() >= expiresUtc ||
            await signInManager.ValidateSecurityStampAsync(ticket.Principal) is not { } user)
        {
            return Results.Unauthorized();
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);

        return Results.SignIn(
            principal,
            authenticationScheme: IdentityConstants.BearerScheme);
    }

    private static async Task<IResult> MicrosoftLogin(
        MicrosoftLoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        IAuditLogger auditLogger,
        ILoggerFactory loggerFactory,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AuthEndpoints");

        // This endpoint authenticates with the Microsoft ID token.
        // AccessToken is intentionally NOT used as proof of identity here.
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return Results.BadRequest(new
            {
                message = "Microsoft ID token is required."
            });
        }

        var tenantId = GetRequiredConfigurationValue(
            configuration,
            "MicrosoftAuth:TenantId",
            "AzureAd:TenantId",
            "Authentication:Microsoft:TenantId");

        var clientId = GetRequiredConfigurationValue(
            configuration,
            "MicrosoftAuth:ClientId",
            "AzureAd:ClientId",
            "Authentication:Microsoft:ClientId");

        if (tenantId is null || clientId is null)
        {
            logger.LogCritical(
                "Microsoft authentication is not configured. TenantId configured: {HasTenantId}, ClientId configured: {HasClientId}",
                tenantId is not null,
                clientId is not null);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Microsoft authentication is not configured correctly.");
        }

        MicrosoftIdentity identity;

        try
        {
            identity = await ValidateMicrosoftIdTokenAsync(
                request.IdToken.Trim(),
                tenantId,
                clientId,
                cancellationToken);
        }
        catch (SecurityTokenException ex)
        {
            logger.LogWarning(ex, "Microsoft ID token validation failed.");

            return Results.Json(new
            {
                message = "Microsoft authentication failed. The ID token is invalid, expired, or was not issued for this application."
            }, statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (Exception ex)
        {
            // Metadata/signing-key retrieval can fail because of a temporary Microsoft/network outage.
            logger.LogError(ex, "Microsoft identity metadata or signing keys could not be retrieved.");

            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Microsoft authentication service is temporarily unavailable.");
        }

        var providerKey = $"{identity.TenantId}:{identity.ObjectId}";

        // First use Microsoft's immutable identity (tenant + object id).
        // This avoids depending on a mutable email address for future logins.
        var user = await userManager.FindByLoginAsync(MicrosoftLoginProvider, providerKey);

        if (user is null)
        {
            var normalizedEmail = NormalizeEmail(identity.Email);

            if (normalizedEmail is null)
            {
                logger.LogWarning(
                    "Validated Microsoft identity {TenantId}/{ObjectId} has no usable email and is not yet linked to an HR user.",
                    identity.TenantId,
                    identity.ObjectId);

                return Results.Json(new
                {
                    message = "Microsoft authentication succeeded, but no usable employee email was supplied by Microsoft for first-time account linking."
                }, statusCode: StatusCodes.Status403Forbidden);
            }

            // Try an existing ASP.NET Identity user first.
            user = await userManager.FindByEmailAsync(normalizedEmail)
                ?? await userManager.FindByNameAsync(normalizedEmail);

            if (user is null)
            {
                // The employee email table is the allow-list for first-time provisioning.
                var employeeEmailRecord = await dbContext.EmployeeEmails
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        e => e.Email == normalizedEmail,
                        cancellationToken);

                if (employeeEmailRecord is not null)
                {
                    // Reuse an existing account for the employee if one already exists.
                    user = await userManager.Users
                        .FirstOrDefaultAsync(
                            u => u.EmployeeID == employeeEmailRecord.EmployeeID,
                            cancellationToken);

                    if (user is null)
                    {
                        user = new ApplicationUser
                        {
                            UserName = normalizedEmail,
                            Email = normalizedEmail,
                            EmailConfirmed = true,
                            EmployeeID = employeeEmailRecord.EmployeeID
                        };

                        var createResult = await userManager.CreateAsync(user);

                        if (!createResult.Succeeded)
                        {
                            // Handle a possible concurrent first-login race by trying to resolve
                            // the employee account once more before returning an error.
                            var concurrentlyCreatedUser = await userManager.Users
                                .FirstOrDefaultAsync(
                                    u => u.EmployeeID == employeeEmailRecord.EmployeeID,
                                    cancellationToken);

                            if (concurrentlyCreatedUser is not null)
                            {
                                user = concurrentlyCreatedUser;
                            }
                            else
                            {
                                logger.LogError(
                                    "Failed to auto-create user for employee {EmployeeID}: {Errors}",
                                    employeeEmailRecord.EmployeeID,
                                    string.Join(", ", createResult.Errors.Select(e => e.Code)));

                                return Results.Problem(
                                    statusCode: StatusCodes.Status500InternalServerError,
                                    title: "Could not provision user account for employee.");
                            }
                        }
                    }
                }
            }

            if (user is null)
            {
                return Results.Json(new
                {
                    message = "هذا الحساب غير مسجل في نظام الموارد البشرية."
                }, statusCode: StatusCodes.Status403Forbidden);
            }

            // Persist the stable Microsoft identity in AspNetUserLogins.
            // Future logins can now resolve by tid + oid even if the employee email changes.
            var existingMicrosoftUser = await userManager.FindByLoginAsync(
                MicrosoftLoginProvider,
                providerKey);

            if (existingMicrosoftUser is null)
            {
                var addLoginResult = await userManager.AddLoginAsync(
                    user,
                    new UserLoginInfo(
                        MicrosoftLoginProvider,
                        providerKey,
                        "Microsoft Entra ID"));

                if (!addLoginResult.Succeeded)
                {
                    // Check once more in case another concurrent request linked it first.
                    existingMicrosoftUser = await userManager.FindByLoginAsync(
                        MicrosoftLoginProvider,
                        providerKey);

                    if (existingMicrosoftUser is null || existingMicrosoftUser.Id != user.Id)
                    {
                        logger.LogError(
                            "Could not link Microsoft identity {TenantId}/{ObjectId} to HR user {UserId}: {Errors}",
                            identity.TenantId,
                            identity.ObjectId,
                            user.Id,
                            string.Join(", ", addLoginResult.Errors.Select(e => e.Code)));

                        return Results.Problem(
                            statusCode: StatusCodes.Status409Conflict,
                            title: "The Microsoft identity could not be linked to this HR account.");
                    }

                    user = existingMicrosoftUser;
                }
            }
            else if (existingMicrosoftUser.Id != user.Id)
            {
                logger.LogError(
                    "Microsoft identity {TenantId}/{ObjectId} is already linked to another HR user.",
                    identity.TenantId,
                    identity.ObjectId);

            }
        }

        // Require human user to be linked to an existing Employee record (SystemAccounts exempted)
        var userEmployeeService = httpContext.RequestServices.GetRequiredService<API.HR.Node.Services.Identity.IUserEmployeeService>();
        var linkResult = await userEmployeeService.ValidateOrRepairUserEmployeeLinkAsync(user, cancellationToken);
        if (!linkResult.IsValid)
        {
            logger.LogWarning("User {UserId} attempted Microsoft login but is not linked to an employee record. Reason: {Reason}", user.Id, linkResult.FailureReason);

            return Results.Json(new
            {
                message = linkResult.FailureReason ?? "هذا الحساب غير مرتبط بسجل موظف في نظام الموارد البشرية."
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        // Respect ASP.NET Identity account restrictions for Microsoft sign-in as well.
        if (await userManager.IsLockedOutAsync(user))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Account is locked.");
        }

        if (!await signInManager.CanSignInAsync(user))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Login is not allowed for this account.");
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgentInfo = GetDeviceUserAgentString(httpContext, request.DeviceInfo, request.DeviceModel, request.OperatingSystem, request.AppVersion, request.DeviceId);

        var deviceCheckResult = await ValidateAndRegisterDeviceAsync(
            user,
            request.DeviceInfo,
            request.DeviceId,
            request.DeviceModel,
            request.OperatingSystem,
            request.AppVersion,
            ipAddress,
            dbContext,
            logger,
            cancellationToken);

        if (deviceCheckResult is not null)
        {
            await auditLogger.LogAsync(
                action: "MicrosoftLogin_DeviceMismatch_Blocked",
                employeeID: user.EmployeeID!,
                photoId: null,
                success: false,
                user: null,
                ipAddress: ipAddress,
                userAgent: userAgentInfo,
                failureReason: "UnauthorizedDeviceAttempt",
                cancellationToken: cancellationToken);

            return deviceCheckResult;
        }

        await auditLogger.LogAsync(
            action: "MicrosoftLogin_Success",
            employeeID: user.EmployeeID!,
            photoId: null,
            success: true,
            user: principal,
            ipAddress: ipAddress,
            userAgent: userAgentInfo,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Microsoft login succeeded for user {UserId}, EmployeeID: {EmployeeID}, Device: {DeviceModel}, OS: {OperatingSystem}, AppVersion: {AppVersion}, IP: {IpAddress}",
            user.Id, user.EmployeeID, request.DeviceModel ?? "N/A", request.OperatingSystem ?? "N/A", request.AppVersion ?? "N/A", ipAddress ?? "N/A");

        return await CreateBearerTokenResultAsync(
            user,
            principal,
            userManager,
            dbContext,
            bearerTokenOptions,
            timeProvider,
            cancellationToken);
    }

    private static async Task<MicrosoftIdentity> ValidateMicrosoftIdTokenAsync(
        string idToken,
        string tenantId,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new SecurityTokenException("ID token is empty.");
        }

        var configurationManager = MicrosoftConfigurationManagers.GetOrAdd(
            tenantId,
            static id =>
            {
                var metadataAddress =
                    $"https://login.microsoftonline.com/{Uri.EscapeDataString(id)}/v2.0/.well-known/openid-configuration";

                return new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadataAddress,
                    new OpenIdConnectConfigurationRetriever());
            });

        var openIdConfiguration = await configurationManager
            .GetConfigurationAsync(cancellationToken);

        var v2Issuer = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        var v1Issuer = $"https://sts.windows.net/{tenantId}/";

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = openIdConfiguration.SigningKeys,
            RequireSignedTokens = true,

            ValidateIssuer = true,
            ValidIssuers = new[]
            {
                openIdConfiguration.Issuer,
                v2Issuer,
                v1Issuer
            },

            ValidateAudience = true,
            ValidAudience = clientId,
            RequireAudience = true,

            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromMinutes(2),

            // Microsoft Entra ID currently signs these tokens with RSA SHA-256.
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
        };

        var handler = new JwtSecurityTokenHandler
        {
            // Preserve original Microsoft claim names such as oid, tid and preferred_username.
            MapInboundClaims = false
        };

        var principal = handler.ValidateToken(
            idToken,
            validationParameters,
            out var validatedToken);

        if (validatedToken is not JwtSecurityToken jwtToken)
        {
            throw new SecurityTokenException("Validated token is not a JWT.");
        }

        // Defense in depth: ensure the cryptographically validated token belongs to
        // the configured tenant and contains Microsoft's stable user identifier.
        var tokenTenantId = principal.FindFirst("tid")?.Value;
        var objectId = principal.FindFirst("oid")?.Value;

        if (string.IsNullOrWhiteSpace(tokenTenantId) ||
            !string.Equals(tokenTenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenInvalidIssuerException("Token tenant does not match the configured tenant.");
        }

        if (!Guid.TryParse(tokenTenantId, out _) ||
            string.IsNullOrWhiteSpace(objectId) ||
            !Guid.TryParse(objectId, out _))
        {
            throw new SecurityTokenException("Token does not contain valid tid/oid identity claims.");
        }

        var email = ExtractEmail(principal);

        return new MicrosoftIdentity(
            tokenTenantId,
            objectId,
            email);
    }

    private static string? ExtractEmail(ClaimsPrincipal principal)
    {
        var candidates = new[]
        {
            principal.FindFirst("email")?.Value,
            principal.FindFirst("preferred_username")?.Value,
            principal.FindFirst("upn")?.Value,
            principal.FindFirst(ClaimTypes.Email)?.Value
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var value = candidate.Trim();

            if (MailAddress.TryCreate(value, out var parsed) &&
                string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var value = email.Trim();

        return MailAddress.TryCreate(value, out var parsed) &&
               string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase)
            ? value.ToLowerInvariant()
            : null;
    }

    private static string? GetRequiredConfigurationValue(
        IConfiguration configuration,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string GetDeviceUserAgentString(
        HttpContext httpContext,
        MobileDeviceInfo? deviceInfo,
        string? deviceModel,
        string? operatingSystem,
        string? appVersion,
        string? deviceId)
    {
        var rawUserAgent = httpContext.Request.Headers.UserAgent.ToString();

        var details = new List<string>();

        if (deviceInfo is not null)
        {
            if (!string.IsNullOrWhiteSpace(deviceInfo.Platform)) details.Add($"Platform: {deviceInfo.Platform.Trim()}");
            if (!string.IsNullOrWhiteSpace(deviceInfo.DeviceName)) details.Add($"Name: {deviceInfo.DeviceName.Trim()}");
            if (!string.IsNullOrWhiteSpace(deviceInfo.Model ?? deviceModel)) details.Add($"Model: {(deviceInfo.Model ?? deviceModel)!.Trim()}");
            if (!string.IsNullOrWhiteSpace(deviceInfo.Manufacturer)) details.Add($"Mfr: {deviceInfo.Manufacturer.Trim()}");
            if (!string.IsNullOrWhiteSpace(deviceInfo.Brand)) details.Add($"Brand: {deviceInfo.Brand.Trim()}");
            if (!string.IsNullOrWhiteSpace(deviceInfo.OsVersion ?? operatingSystem)) details.Add($"OS: {(deviceInfo.OsVersion ?? operatingSystem)!.Trim()}");
            if (deviceInfo.SdkInt.HasValue) details.Add($"SDK: {deviceInfo.SdkInt}");
            if (!string.IsNullOrWhiteSpace(deviceInfo.BuildId)) details.Add($"Build: {deviceInfo.BuildId.Trim()}");
            if (deviceInfo.IsPhysicalDevice.HasValue) details.Add($"Physical: {deviceInfo.IsPhysicalDevice}");
            if (!string.IsNullOrWhiteSpace(deviceInfo.Hardware)) details.Add($"Hw: {deviceInfo.Hardware.Trim()}");
            if (!string.IsNullOrWhiteSpace(deviceInfo.VendorIdentifier)) details.Add($"VendorId: {deviceInfo.VendorIdentifier.Trim()}");
            if (!string.IsNullOrWhiteSpace(deviceInfo.MachineCode)) details.Add($"Machine: {deviceInfo.MachineCode.Trim()}");
            if (!string.IsNullOrWhiteSpace(deviceInfo.AppVersion ?? appVersion)) details.Add($"AppVer: {(deviceInfo.AppVersion ?? appVersion)!.Trim()}");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(deviceModel)) details.Add($"Model: {deviceModel.Trim()}");
            if (!string.IsNullOrWhiteSpace(operatingSystem)) details.Add($"OS: {operatingSystem.Trim()}");
            if (!string.IsNullOrWhiteSpace(appVersion)) details.Add($"AppVer: {appVersion.Trim()}");
            if (!string.IsNullOrWhiteSpace(deviceId)) details.Add($"DeviceId: {deviceId.Trim()}");
        }

        var mobileContext = details.Count > 0
            ? $" [MobileDevice: {string.Join(", ", details)}]"
            : string.Empty;

        return string.IsNullOrWhiteSpace(rawUserAgent)
            ? $"MobileDevice{mobileContext}"
            : $"{rawUserAgent}{mobileContext}";
    }

    private static async Task<IResult?> ValidateAndRegisterDeviceAsync(
        ApplicationUser user,
        MobileDeviceInfo? deviceInfo,
        string? deviceId,
        string? deviceModel,
        string? operatingSystem,
        string? appVersion,
        string? ipAddress,
        ApplicationDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var incomingDeviceId = deviceInfo?.VendorIdentifier
            ?? deviceInfo?.BuildId
            ?? deviceId
            ?? (!string.IsNullOrWhiteSpace(deviceInfo?.Model ?? deviceModel) ? $"{deviceInfo?.Manufacturer ?? "Unknown"}_{deviceInfo?.Model ?? deviceModel}" : null);

        if (string.IsNullOrWhiteSpace(incomingDeviceId))
        {
            return null;
        }

        var cleanDeviceId = incomingDeviceId.Trim();

        var activeDevice = await dbContext.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == user.Id && d.IsActive, cancellationToken);

        if (activeDevice is null)
        {
            var newDevice = new UserDevice
            {
                UserId = user.Id,
                EmployeeID = user.EmployeeID!,
                DeviceId = cleanDeviceId,
                Platform = deviceInfo?.Platform ?? operatingSystem,
                DeviceModel = deviceInfo?.Model ?? deviceModel,
                Manufacturer = deviceInfo?.Manufacturer,
                OsVersion = deviceInfo?.OsVersion ?? operatingSystem,
                AppVersion = deviceInfo?.AppVersion ?? appVersion,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                LastLoginAt = DateTimeOffset.UtcNow,
                LastIpAddress = ipAddress
            };

            dbContext.UserDevices.Add(newDevice);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Primary mobile device '{DeviceId}' bound to employee '{EmployeeID}' (User: {UserId}).",
                cleanDeviceId, user.EmployeeID, user.Id);

            return null;
        }

        if (string.Equals(activeDevice.DeviceId, cleanDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            activeDevice.LastLoginAt = DateTimeOffset.UtcNow;
            activeDevice.LastIpAddress = ipAddress;
            if (!string.IsNullOrWhiteSpace(appVersion)) activeDevice.AppVersion = appVersion;

            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        logger.LogWarning(
            "Security Alert: Employee '{EmployeeID}' attempted login from unauthorized device '{IncomingDevice}'. Active bound device is '{BoundDevice}'.",
            user.EmployeeID, cleanDeviceId, activeDevice.DeviceId);

        return Results.Json(new
        {
            message = "عذراً، هذا الحساب مقترن بجهاز هاتف آخر. لا يُسمح بتسجيل الدخول إلا من الهاتف المعتمد فقط."
        }, statusCode: StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> CreateBearerTokenResultAsync(
        ApplicationUser user,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var options = bearerTokenOptions.Get(IdentityConstants.BearerScheme);
        var utcNow = timeProvider.GetUtcNow();

        var accessTicket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            ExpiresUtc = utcNow.Add(options.BearerTokenExpiration)
        }, IdentityConstants.BearerScheme);
        var accessToken = options.BearerTokenProtector.Protect(accessTicket);

        var refreshTicket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            ExpiresUtc = utcNow.Add(options.RefreshTokenExpiration)
        }, IdentityConstants.BearerScheme);
        var refreshToken = options.RefreshTokenProtector.Protect(refreshTicket);

        var roles = await userManager.GetRolesAsync(user);
        var isAdmin = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) ||
                      roles.Contains("HR_Admin", StringComparer.OrdinalIgnoreCase) ||
                      roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase) ||
                      principal.IsInRole("Admin") || principal.IsInRole("HR_Admin");

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.FindAll("permission"))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
            {
                permissions.Add(claim.Value);
            }
        }
        if (isAdmin)
        {
            permissions.Add("VIEW_ALL_EMPLOYEES");
            permissions.Add("MANAGE_DOCUMENTS");
            permissions.Add("VERIFY_DOCUMENTS");
            permissions.Add("EXPORT_REPORTS");
        }

        var employee = !string.IsNullOrWhiteSpace(user.EmployeeID)
            ? await dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeID == user.EmployeeID, cancellationToken)
            : null;

        return Results.Ok(new
        {
            tokenType = "Bearer",
            accessToken = accessToken,
            expiresIn = (long)options.BearerTokenExpiration.TotalSeconds,
            refreshToken = refreshToken,
            employeeID = user.EmployeeID ?? "",
            name = employee?.Name ?? user.UserName ?? "",
            roles = roles,
            isAdmin = isAdmin,
            permissions = permissions.ToList()
        });
    }

    private static async Task<IResult> ActiveDirectoryLogin(
        AdLoginRequest request,
        IActiveDirectoryAuthService adAuthService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext dbContext,
        IAuditLogger auditLogger,
        ILoggerFactory loggerFactory,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AuthEndpoints");
        var account = request.AccountName;

        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new
            {
                message = "اسم المستخدم وكلمة المرور مطلوبان للدخول عبر الدومين."
            });
        }

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgentInfo = GetDeviceUserAgentString(httpContext, request.DeviceInfo, request.DeviceModel, request.OperatingSystem, request.AppVersion, request.DeviceId);

        // 1. Validate Active Directory credentials
        if (!adAuthService.ValidateCredentials(account, request.Password, out var adUser) || adUser is null)
        {
            logger.LogWarning("[AD Login API] Invalid AD credentials for user '{Account}' from IP {Ip}", account, ipAddress);

            await auditLogger.LogAsync(
                action: "AdLogin_Failed_InvalidCredentials",
                employeeID: account,
                photoId: null,
                success: false,
                user: null,
                ipAddress: ipAddress,
                userAgent: userAgentInfo,
                failureReason: "InvalidAdCredentials",
                cancellationToken: cancellationToken);

            return Results.Json(new
            {
                message = "اسم المستخدم أو كلمة مرور الدومين (Active Directory) غير صحيحة."
            }, statusCode: StatusCodes.Status401Unauthorized);
        }

        // 2. Resolve employee record from EmployeeEmails
        var candidateEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            adUser.Email,
            $"{adUser.Username}@aml.lan"
        };
        if (account.Contains('@'))
        {
            candidateEmails.Add(account);
        }

        var candidateList = candidateEmails.Select(e => e.Trim().ToLower()).Distinct().ToList();
        var usernamePrefix = adUser.Username.Trim().ToLower() + "@";

        var empEmail = await dbContext.EmployeeEmails
            .AsNoTracking()
            .FirstOrDefaultAsync(ee =>
                candidateList.Contains(ee.Email.Trim().ToLower())
                || ee.Email.Trim().ToLower().StartsWith(usernamePrefix)
                || ee.Email.Trim().ToLower() == adUser.Username.Trim().ToLower()
                || ee.EmployeeID.Trim() == adUser.Username.Trim(), cancellationToken);

        if (empEmail is null)
        {
            logger.LogWarning("[AD Login API] User '{Username}' verified in AD but not found in EmployeeEmails.", adUser.Username);

            await auditLogger.LogAsync(
                action: "AdLogin_EmployeeRecordNotFound",
                employeeID: adUser.Username,
                photoId: null,
                success: false,
                user: null,
                ipAddress: ipAddress,
                userAgent: userAgentInfo,
                failureReason: "EmployeeEmailNotFound",
                cancellationToken: cancellationToken);

            return Results.Json(new
            {
                message = "هذا الحساب موثق في الدومين لكنه غير مقترن بسجل موظف في نظام الموارد البشرية."
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        // Verify employee exists in Employees table
        var employee = await dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeID == empEmail.EmployeeID, cancellationToken);

        if (employee is null)
        {
            logger.LogWarning("[AD Login API] EmployeeID '{EmployeeID}' not found in Employees table.", empEmail.EmployeeID);
            return Results.Json(new
            {
                message = "سجل الموظف غير موجود في قاعدة بيانات الموارد البشرية."
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        // 3. Find or auto-provision ApplicationUser
        var user = await userManager.FindByNameAsync(adUser.Username)
                ?? await userManager.FindByEmailAsync(empEmail.Email)
                ?? await userManager.FindByNameAsync(empEmail.Email)
                ?? await userManager.FindByEmailAsync(adUser.Email)
                ?? await userManager.FindByNameAsync(adUser.Email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = adUser.Username,
                Email = empEmail.Email,
                EmailConfirmed = true,
                EmployeeID = empEmail.EmployeeID
            };

            var createRes = await userManager.CreateAsync(user);
            if (!createRes.Succeeded)
            {
                logger.LogError("[AD Login API] Failed to auto-provision user '{Username}': {Errors}",
                    adUser.Username, string.Join(", ", createRes.Errors.Select(e => e.Code)));

                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "فشل تهيئة حساب الموظف في النظام.");
            }

            await userManager.AddToRoleAsync(user, "Employee");
            logger.LogInformation("[AD Login API] Successfully auto-provisioned user '{Username}' (EmployeeID: {EmployeeID}) with role Employee.",
                adUser.Username, empEmail.EmployeeID);
        }
        else
        {
            bool needsUpdate = false;
            if (user.EmployeeID != empEmail.EmployeeID)
            {
                user.EmployeeID = empEmail.EmployeeID;
                needsUpdate = true;
            }
            if (!string.Equals(user.Email, empEmail.Email, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = empEmail.Email;
                needsUpdate = true;
            }
            if (needsUpdate)
            {
                await userManager.UpdateAsync(user);
            }
        }

        // 4. Respect ASP.NET Identity lockout
        if (await userManager.IsLockedOutAsync(user))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "الحساب مقفل في النظام بسبب محاولات دخول خاطئة متكررة.");
        }

        if (!await signInManager.CanSignInAsync(user))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "غير مسموح لهذا الحساب بتسجيل الدخول.");
        }

        // 5. Check and register mobile device
        var deviceCheckResult = await ValidateAndRegisterDeviceAsync(
            user,
            request.DeviceInfo,
            request.DeviceId,
            request.DeviceModel,
            request.OperatingSystem,
            request.AppVersion,
            ipAddress,
            dbContext,
            logger,
            cancellationToken);

        if (deviceCheckResult is not null)
        {
            await auditLogger.LogAsync(
                action: "AdLogin_DeviceMismatch_Blocked",
                employeeID: user.EmployeeID!,
                photoId: null,
                success: false,
                user: null,
                ipAddress: ipAddress,
                userAgent: userAgentInfo,
                failureReason: "UnauthorizedDeviceAttempt",
                cancellationToken: cancellationToken);

            return deviceCheckResult;
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);

        await auditLogger.LogAsync(
            action: "AdLogin_Success",
            employeeID: user.EmployeeID!,
            photoId: null,
            success: true,
            user: principal,
            ipAddress: ipAddress,
            userAgent: userAgentInfo,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Active Directory login succeeded for user {UserId}, EmployeeID: {EmployeeID}, Device: {DeviceModel}, OS: {OperatingSystem}, IP: {IpAddress}",
            user.Id, user.EmployeeID, request.DeviceModel ?? "N/A", request.OperatingSystem ?? "N/A", ipAddress ?? "N/A");

        return await CreateBearerTokenResultAsync(
            user,
            principal,
            userManager,
            dbContext,
            bearerTokenOptions,
            timeProvider,
            cancellationToken);
    }

    private sealed record MicrosoftIdentity(
        string TenantId,
        string ObjectId,
        string? Email);
}
