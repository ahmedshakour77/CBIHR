using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using API.HR.Node.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace API.HR.Node.Security;

/// <summary>
/// Validates HTTP Basic credentials (Authorization: Basic base64(email:password))
/// against ASP.NET Core Identity on every request. Intended for trusted
/// server-to-server callers; browser/mobile clients should use the bearer token.
/// </summary>
public sealed class BasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Basic";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            // No Authorization header: let other schemes (bearer) try.
            return AuthenticateResult.NoResult();
        }

        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var header) ||
            !SchemeName.Equals(header.Scheme, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
        {
            // A different scheme (e.g. Bearer): not ours to handle.
            return AuthenticateResult.NoResult();
        }

        string email;
        string password;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
            var separatorIndex = decoded.IndexOf(':');

            if (separatorIndex < 0)
            {
                return AuthenticateResult.Fail("Invalid Basic authentication header.");
            }

            email = decoded[..separatorIndex];
            password = decoded[(separatorIndex + 1)..];
        }
        catch (FormatException)
        {
            return AuthenticateResult.Fail("Invalid Basic authentication header.");
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return AuthenticateResult.Fail("Email and password are required.");
        }

        // Scoped Identity services must be resolved per request.
        var userManager = Context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = Context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();

        var input = email.Trim();
        var user = await userManager.FindByEmailAsync(input)
                ?? await userManager.FindByNameAsync(input);

        if (user is null)
        {
            return AuthenticateResult.Fail("Invalid credentials.");
        }

        // Verifies the (slow) password hash and enforces lockout + confirmed-account checks.
        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return AuthenticateResult.Fail("Invalid credentials.");
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = $"{SchemeName} realm=\"api\", charset=\"UTF-8\"";
        return base.HandleChallengeAsync(properties);
    }
}
