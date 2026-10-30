namespace API.HR.Node.Security;

/// <summary>
/// Defines explicit technical / system accounts that are exempt from the human employee linkage invariant.
/// Any exception must be explicitly declared here; no implicit or arbitrary null EmployeeID is permitted.
/// </summary>
public static class SystemAccounts
{
    public const string BootstrapAdminUserName = "systemadmin";
    public const string BootstrapAdminEmail = "systemadmin@hr.node";

    private static readonly HashSet<string> TechnicalAccountUserNames = new(StringComparer.OrdinalIgnoreCase)
    {
        BootstrapAdminUserName,
        "svc.integration",
        "svc.backup"
    };

    private static readonly HashSet<string> TechnicalAccountEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        BootstrapAdminEmail,
        "svc.integration@cbi.iq",
        "svc.backup@cbi.iq"
    };

    public static IReadOnlyCollection<string> DeclaredUserNames => TechnicalAccountUserNames;
    public static IReadOnlyCollection<string> DeclaredEmails => TechnicalAccountEmails;

    /// <summary>
    /// Checks if a given username or email corresponds to an explicit technical / system service account.
    /// </summary>
    public static bool IsTechnicalAccount(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        var clean = identifier.Trim();
        return TechnicalAccountUserNames.Contains(clean) || TechnicalAccountEmails.Contains(clean);
    }
}
