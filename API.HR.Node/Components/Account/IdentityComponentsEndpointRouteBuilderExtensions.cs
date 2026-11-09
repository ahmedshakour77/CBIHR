using API.HR.Node.Components.Account.Pages;
using API.HR.Node.Components.Account.Pages.Manage;
using API.HR.Node.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using System.Text.Json;

namespace Microsoft.AspNetCore.Routing
{
    internal static class IdentityComponentsEndpointRouteBuilderExtensions
    {
        // These endpoints are required by the Identity Razor components defined in the /Components/Account/Pages directory of this project.
        public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var accountGroup = endpoints.MapGroup("/Account");

            accountGroup.MapPost("/PerformExternalLogin", (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider,
                [FromForm] string returnUrl) =>
            {
                IEnumerable<KeyValuePair<string, StringValues>> query = [
                    new("ReturnUrl", returnUrl),
                    new("Action", ExternalLogin.LoginCallbackAction)];

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/ExternalLogin",
                    QueryString.Create(query));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return TypedResults.Challenge(properties, [provider]);
            }).DisableAntiforgery();

            accountGroup.MapPost("/Logout", async (
                ClaimsPrincipal user,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string returnUrl) =>
            {
                await signInManager.SignOutAsync();
                return TypedResults.LocalRedirect($"~/{returnUrl}");
            }).DisableAntiforgery();

            accountGroup.MapPost("/PerformMagicLogin", async (
                HttpContext context,
                [FromServices] API.HR.Node.Services.Security.IMagicLoginService magicLoginService,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromServices] API.HR.Node.Services.Audit.IAuditLogger auditLogger,
                [FromForm] string token) =>
            {
                var clientIp = context.Connection.RemoteIpAddress?.ToString();
                var userAgent = context.Request.Headers.UserAgent.ToString();

                if (!magicLoginService.TryConsumeToken(token, clientIp, out var validation) || string.IsNullOrWhiteSpace(validation.TargetUserId))
                {
                    var err = Uri.EscapeDataString(validation.ErrorMessage ?? "رابط الدخول غير صالح أو منتهي الصلاحية.");
                    return TypedResults.LocalRedirect($"~/Account/MagicLogin?t={Uri.EscapeDataString(token ?? string.Empty)}&err={err}");
                }

                var user = await userManager.FindByIdAsync(validation.TargetUserId);
                if (user is null)
                {
                    var err = Uri.EscapeDataString("تعذّر العثور على حساب المستخدم.");
                    return TypedResults.LocalRedirect($"~/Account/MagicLogin?err={err}");
                }

                if (await userManager.IsLockedOutAsync(user))
                {
                    await auditLogger.LogAsync(
                        "MAGIC_LINK_LOGIN_LOCKED",
                        user.EmployeeID ?? user.UserName ?? user.Id,
                        null,
                        false,
                        null,
                        clientIp,
                        userAgent,
                        "Account is locked out.");
                    return TypedResults.LocalRedirect("~/Account/Lockout");
                }

                if (!string.Equals(user.SecurityStamp ?? string.Empty, validation.ExpectedSecurityStamp ?? string.Empty, StringComparison.Ordinal))
                {
                    var err = Uri.EscapeDataString("تم تحديث البصمة الأمنية للحساب بعد إنشاء الرابط، مما أبطل صلاحيته.");
                    return TypedResults.LocalRedirect($"~/Account/MagicLogin?err={err}");
                }

                // Sign out any existing session on this browser before signing in as the target user
                await signInManager.SignOutAsync();
                await signInManager.SignInAsync(user, isPersistent: false);

                await auditLogger.LogAsync(
                    "MAGIC_LINK_LOGIN_SUCCESS",
                    user.EmployeeID ?? user.UserName ?? user.Id,
                    null,
                    true,
                    null,
                    clientIp,
                    userAgent,
                    $"Signed in via 2-minute admin magic link created by {validation.CreatedByAdminName} ({validation.CreatedByAdminId}).");

                // Redirect according to the user's effective role and permissions
                var roles = await userManager.GetRolesAsync(user);
                var claims = await userManager.GetClaimsAsync(user);
                var directPerms = claims
                    .Where(c => c.Type == "permission" || c.Type == "Permission")
                    .Select(c => c.Value)
                    .ToList();
                var isCustomized = claims.Any(c => c.Type.Equals(API.HR.Node.Security.DatabaseClaimsSynchronizer.CustomizedPermissionsClaimType, StringComparison.OrdinalIgnoreCase) && c.Value == "true");

                var effectivePerms = API.HR.Node.Security.FeaturePermissionCatalog
                    .CalculateEffectivePermissions(roles, directPerms, ignoreRoleDefaults: isCustomized)
                    .Where(p => p.IsEffective)
                    .Select(p => p.PermissionCode)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var isAdmin = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) ||
                              string.Equals(user.UserName, "systemadmin", StringComparison.OrdinalIgnoreCase);

                string destination = "~/";
                if (!isAdmin && !effectivePerms.Contains(API.HR.Node.Security.Permissions.DashboardView))
                {
                    if (!string.IsNullOrWhiteSpace(user.EmployeeID) && effectivePerms.Contains(API.HR.Node.Security.Permissions.EmployeesView))
                    {
                        destination = $"~/employees/{Uri.EscapeDataString(user.EmployeeID)}";
                    }
                    else
                    {
                        destination = "~/portal";
                    }
                }

                return TypedResults.LocalRedirect(destination);
            }).DisableAntiforgery();

            accountGroup.MapPost("/PasskeyCreationOptions", async (
                HttpContext context,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] SignInManager<ApplicationUser> signInManager) =>
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
                }

                var userId = await userManager.GetUserIdAsync(user);
                var userName = await userManager.GetUserNameAsync(user) ?? "User";
                var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new()
                {
                    Id = userId,
                    Name = userName,
                    DisplayName = userName
                });
                return TypedResults.Content(optionsJson, contentType: "application/json");
            }).RequireAuthorization().DisableAntiforgery();

            accountGroup.MapPost("/PasskeyRequestOptions", async (
                HttpContext context,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromQuery] string? username) =>
            {
                var user = string.IsNullOrEmpty(username)
                    ? null
                    : (await userManager.FindByNameAsync(username) ?? await userManager.FindByEmailAsync(username));
                var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);
                return TypedResults.Content(optionsJson, contentType: "application/json");
            }).DisableAntiforgery();

            var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();

            manageGroup.MapPost("/LinkExternalLogin", async (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider) =>
            {
                // Clear the existing external cookie to ensure a clean login process
                await context.SignOutAsync(IdentityConstants.ExternalScheme);

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/Manage/ExternalLogins",
                    QueryString.Create("Action", ExternalLogins.LinkLoginCallbackAction));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, signInManager.UserManager.GetUserId(context.User));
                return TypedResults.Challenge(properties, [provider]);
            });

            var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var downloadLogger = loggerFactory.CreateLogger("DownloadPersonalData");

            manageGroup.MapPost("/DownloadPersonalData", async (
                HttpContext context,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] AuthenticationStateProvider authenticationStateProvider) =>
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
                }

                var userId = await userManager.GetUserIdAsync(user);
                downloadLogger.LogInformation("User with ID '{UserId}' asked for their personal data.", userId);

                // Only include personal data for download
                var personalData = new Dictionary<string, string>();
                var personalDataProps = typeof(ApplicationUser).GetProperties().Where(
                    prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
                foreach (var p in personalDataProps)
                {
                    personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
                }

                var logins = await userManager.GetLoginsAsync(user);
                foreach (var l in logins)
                {
                    personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
                }

                personalData.Add("Authenticator Key", (await userManager.GetAuthenticatorKeyAsync(user))!);
                var fileBytes = JsonSerializer.SerializeToUtf8Bytes(personalData);

                context.Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
                return TypedResults.File(fileBytes, contentType: "application/json", fileDownloadName: "PersonalData.json");
            });

            return accountGroup;
        }
    }
}
