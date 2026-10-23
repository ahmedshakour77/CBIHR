using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;

namespace API.HR.Node.Services.Security;

[SupportedOSPlatform("windows")]
public sealed class ActiveDirectoryAuthService : IActiveDirectoryAuthService
{
    private readonly string _domain;
    private readonly ILogger<ActiveDirectoryAuthService> _logger;

    public ActiveDirectoryAuthService(IConfiguration configuration, ILogger<ActiveDirectoryAuthService> logger)
    {
        _domain = configuration["ActiveDirectory:Domain"] ?? "aml.lan";
        _logger = logger;
    }

    public bool ValidateCredentials(string username, string password, out AdUserInfo? userInfo)
    {
        userInfo = null;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var cleanUsername = ExtractUsername(username);
        _logger.LogInformation("[AD Auth] Validating credentials for user '{CleanUsername}' against domain '{Domain}'", cleanUsername, _domain);

        try
        {
            using var context = new PrincipalContext(
                ContextType.Domain,
                _domain
            );

            bool isValid = context.ValidateCredentials(
                cleanUsername,
                password,
                ContextOptions.Negotiate
            );

            if (isValid)
            {
                userInfo = new AdUserInfo(cleanUsername, $"{cleanUsername}@{_domain}", cleanUsername);
                _logger.LogInformation("[AD Auth] Credentials validated successfully for user '{CleanUsername}'. Domain Email: '{Email}'", cleanUsername, userInfo.Email);
                return true;
            }
            else
            {
                _logger.LogWarning("[AD Auth] Invalid credentials for user '{CleanUsername}' on domain '{Domain}'", cleanUsername, _domain);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AD Auth] Active Directory authentication failed or domain '{Domain}' is unreachable for user '{Username}'", _domain, cleanUsername);
        }

        return false;
    }

    public static string ExtractUsername(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var clean = input.Trim();

        // If user typed DOMAIN\username (e.g. AML\ahmed)
        var backslashIdx = clean.IndexOf('\\');
        if (backslashIdx >= 0 && backslashIdx < clean.Length - 1)
        {
            clean = clean[(backslashIdx + 1)..];
        }

        // If user typed username@aml.lan
        var atIdx = clean.IndexOf('@');
        if (atIdx > 0)
        {
            clean = clean[..atIdx];
        }

        return clean.Trim();
    }
}
