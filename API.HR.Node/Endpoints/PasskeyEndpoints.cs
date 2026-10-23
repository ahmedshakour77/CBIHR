using System.Security.Claims;
using System.Text.Json;
using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Security;
using API.HR.Node.Services.Audit;
using API.HR.Node.Services.Security;
using Fido2NetLib;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Endpoints;

public static class PasskeyEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEndpointRouteBuilder MapPasskeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth/passkey")
            .WithTags("Passkey / FIDO2 Authentication");

        group.MapPost("/register/options", PasskeyRegisterOptions)
            .WithName("PasskeyRegisterOptions")
            .WithSummary("Generates FIDO2 Passkey registration options and challenge for an authenticated user.")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        group.MapPost("/register/complete", PasskeyRegisterComplete)
            .WithName("PasskeyRegisterComplete")
            .WithSummary("Finalizes FIDO2 Passkey registration and stores public key credentials.")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        group.MapPost("/login/options", PasskeyLoginOptions)
            .WithName("PasskeyLoginOptions")
            .WithSummary("Generates FIDO2 Passkey login assertion challenge options.")
            .AllowAnonymous();

        group.MapPost("/login/complete", PasskeyLoginComplete)
            .WithName("PasskeyLoginComplete")
            .WithSummary("Verifies FIDO2 Passkey biometric assertion signature and issues JWT Bearer tokens.")
            .AllowAnonymous();

        group.MapGet("/my-passkeys", GetMyPasskeys)
            .WithName("GetMyPasskeys")
            .WithSummary("Gets all registered passkeys for the authenticated employee account.")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        group.MapDelete("/{id:long}", DeletePasskey)
            .WithName("DeletePasskey")
            .WithSummary("Revokes or permanently deletes a registered passkey by ID.")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        group.MapPost("/{id:long}/unfreeze", UnfreezePasskey)
            .WithName("UnfreezePasskey")
            .WithSummary("Unfreezes and reactivates a previously revoked passkey by ID.")
            .RequireAuthorization(AuthorizationPolicies.ApiBearer);

        return endpoints;
    }

    private static async Task<IResult> PasskeyRegisterOptions(
        PasskeyRegisterOptionsRequest? request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null || string.IsNullOrWhiteSpace(user.EmployeeID))
        {
            return Results.Json(new
            {
                message = "هذا الحساب غير مرتبط بسجل موظف في نظام الموارد البشرية."
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        var options = await passkeyService.MakeRegistrationOptionsAsync(user, cancellationToken);
        var optionsJson = options.ToJson();

        return Results.Ok(new
        {
            optionsJson,
            options
        });
    }

    private static async Task<IResult> PasskeyRegisterComplete(
        PasskeyRegisterCompleteRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IPasskeyService passkeyService,
        IAuditLogger auditLogger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null || string.IsNullOrWhiteSpace(user.EmployeeID))
        {
            return Results.Json(new
            {
                message = "هذا الحساب غير مرتبط بسجل موظف في نظام الموارد البشرية."
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        try
        {
            var options = CredentialCreateOptions.FromJson(request.OptionsJson);
            var attestation = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(request.AttestationResponseJson, JsonOptions);

            if (attestation is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid Attestation JSON payload.");
            }

            var passkey = await passkeyService.RegisterCompleteAsync(
                user,
                options,
                attestation,
                request.DeviceName,
                cancellationToken);

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();

            await auditLogger.LogAsync(
                action: "Passkey_Registered",
                employeeID: user.EmployeeID,
                photoId: null,
                success: true,
                user: principal,
                ipAddress: ipAddress,
                userAgent: userAgent,
                cancellationToken: cancellationToken);

            return Results.Ok(new
            {
                message = "تم تسجيل مفتاح المرور (Passkey) بنجاح.",
                passkeyId = passkey.Id,
                deviceName = passkey.DeviceName,
                createdAt = passkey.CreatedAt
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Passkey Registration Failed",
                detail: ex.Message);
        }
    }

    private static async Task<IResult> PasskeyLoginOptions(
        PasskeyLoginOptionsRequest? request,
        IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        var options = await passkeyService.MakeLoginOptionsAsync(request?.Email ?? request?.EmployeeID, cancellationToken);
        var optionsJson = options.ToJson();

        return Results.Ok(new
        {
            optionsJson,
            options
        });
    }

    private static async Task<IResult> PasskeyLoginComplete(
        PasskeyLoginCompleteRequest request,
        IPasskeyService passkeyService,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IAuditLogger auditLogger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = AssertionOptions.FromJson(request.OptionsJson);
            var assertion = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(request.AssertionResponseJson, JsonOptions);

            if (assertion is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid Assertion JSON payload.");
            }

            var (passkey, user) = await passkeyService.LoginCompleteAsync(options, assertion, cancellationToken);

            if (await userManager.IsLockedOutAsync(user))
            {
                return Results.Problem(statusCode: StatusCodes.Status423Locked, title: "Account is locked.");
            }

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            var userAgentInfo = httpContext.Request.Headers.UserAgent.ToString();

            // Device Binding Check
            var deviceCheckResult = await ValidateAndRegisterDeviceAsync(
                user,
                request.DeviceInfo,
                request.DeviceId,
                request.DeviceModel,
                request.OperatingSystem,
                request.AppVersion,
                ipAddress,
                dbContext,
                httpContext.RequestServices.GetRequiredService<ILogger<Program>>(),
                cancellationToken);

            if (deviceCheckResult is not null)
            {
                await auditLogger.LogAsync(
                    action: "PasskeyLogin_DeviceMismatch_Blocked",
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
                action: "PasskeyLogin_Success",
                employeeID: user.EmployeeID!,
                photoId: null,
                success: true,
                user: principal,
                ipAddress: ipAddress,
                userAgent: userAgentInfo,
                cancellationToken: cancellationToken);

            return Results.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Passkey Biometric Authentication Failed",
                detail: ex.Message);
        }
    }

    private static async Task<IResult> GetMyPasskeys(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var passkeys = await passkeyService.GetUserPasskeysAsync(user.Id, cancellationToken);
        return Results.Ok(passkeys);
    }

    private static async Task<IResult> DeletePasskey(
        long id,
        bool? permanent,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        bool success;
        if (permanent == true)
        {
            success = await passkeyService.DeletePasskeyPermanentlyAsync(id, user.Id, cancellationToken);
        }
        else
        {
            success = await passkeyService.RevokePasskeyAsync(id, user.Id, cancellationToken);
        }

        if (!success) return Results.NotFound();

        return Results.Ok(new { message = permanent == true ? "تم محو مفتاح المرور نهائياً." : "تم إلغاء وتجميد مفتاح المرور بنجاح." });
    }

    private static async Task<IResult> UnfreezePasskey(
        long id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var success = await passkeyService.UnfreezePasskeyAsync(id, user.Id, cancellationToken);
        if (!success) return Results.NotFound();

        return Results.Ok(new { message = "تم إيقاف تجميد وإعادة تفعيل مفتاح المرور بنجاح." });
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
            .FirstOrDefaultAsync(d => d.EmployeeID == user.EmployeeID && d.IsActive, cancellationToken);

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

        return Results.Json(new
        {
            message = "عذراً، هذا الحساب مقترن بجهاز هاتف آخر. لا يُسمح بتسجيل الدخول إلا من الهاتف المعتمد فقط."
        }, statusCode: StatusCodes.Status403Forbidden);
    }
}
