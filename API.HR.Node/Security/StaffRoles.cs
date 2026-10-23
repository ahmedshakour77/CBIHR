using System.Security.Claims;

namespace API.HR.Node.Security;

/// <summary>
/// The roles that may see other employees' records. Everyone else sees only
/// their own. One definition, so the API and the pages cannot drift apart.
/// </summary>
public static class StaffRoles
{
    public const string Csv = "Admin,HRManager,FormationManager,Supervisor";

    public static readonly string[] All = ["Admin", "HRManager", "FormationManager", "Supervisor"];

    public static bool IsStaff(ClaimsPrincipal principal) => All.Any(principal.IsInRole);
}
