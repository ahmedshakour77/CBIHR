using System.Security.Claims;

namespace API.HR.Node.Security;

/// <summary>
/// Centralized unified resolver for authorization checks and permission implications.
/// Ensures consistent authorization decisions across AuthorizationHandlers, Blazor UI components,
/// controllers/endpoints, and internal service policies.
/// </summary>
public static class PermissionResolver
{
    /// <summary>
    /// Checks whether the caller is a full administrative user based strictly on authoritative Role claims.
    /// Username-based decisions (e.g. Identity.Name == "admin") are forbidden.
    /// </summary>
    public static bool IsAdmin(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not { IsAuthenticated: true })
        {
            return false;
        }

        return principal.IsInRole("Admin") ||
               principal.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") &&
                                       c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines whether the user satisfies the requested permission code.
    /// Supports:
    /// - Admin role bypass
    /// - Exact permission claim match
    /// - *.Edit implies *.View for the same feature
    /// - Users.PermissionsEdit implies Users.View
    /// - Strict legacy permission compatibility mappings
    /// </summary>
    public static bool HasPermission(ClaimsPrincipal? principal, string requestedPermission)
    {
        if (principal?.Identity is not { IsAuthenticated: true } || string.IsNullOrWhiteSpace(requestedPermission))
        {
            return false;
        }

        // 1. Full Admin role bypass
        if (IsAdmin(principal))
        {
            return true;
        }

        var req = requestedPermission.Trim();

        // 2. Direct claim or implied permission check
        return principal.HasClaim(c =>
        {
            if (c.Type != "permission" && c.Type != "Permission")
            {
                return false;
            }

            var claimValue = c.Value.Trim();

            // Direct match
            if (claimValue.Equals(req, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // View implication: An Edit permission for the same feature automatically grants View
            if (req.EndsWith(".View", StringComparison.OrdinalIgnoreCase))
            {
                var editEquiv = req[..^5] + ".Edit";
                if (claimValue.Equals(editEquiv, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Users.PermissionsEdit implies Users.View
                if (req.Equals(Permissions.UsersView, StringComparison.OrdinalIgnoreCase) &&
                    claimValue.Equals(Permissions.UsersPermissionsEdit, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Legacy mappings check
            if (IsLegacyPermissionMatch(claimValue, req))
            {
                return true;
            }

            return false;
        });
    }

    /// <summary>
    /// Maps legacy coarse permissions to modern granular feature permissions.
    /// Note: ManageUsers grants Users.View and Users.Edit, but explicitly DENIES Users.PermissionsEdit.
    /// </summary>
    public static bool IsLegacyPermissionMatch(string userClaim, string requestedPermission)
    {
        return (userClaim, requestedPermission) switch
        {
            (Permissions.ManageUsers, Permissions.UsersView or Permissions.UsersEdit) => true,
            (Permissions.ManageDevices, Permissions.DevicesView or Permissions.DevicesEdit) => true,
            (Permissions.ViewAuditLogs, Permissions.AuditLogsView) => true,
            (Permissions.ManageWorkflows, Permissions.WorkflowsView or Permissions.WorkflowsEdit) => true,
            (Permissions.ImportEmployees, Permissions.DataImportView or Permissions.DataImportEdit) => true,
            (Permissions.ImportAppreciationLetters, Permissions.AppreciationLettersView or Permissions.AppreciationLettersEdit) => true,
            (Permissions.ImportJobTitles, Permissions.JobTitlesView or Permissions.JobTitlesEdit) => true,
            (Permissions.ImportLastAllowanceDate, Permissions.SalariesView or Permissions.SalariesEdit) => true,
            (Permissions.ManageFeedback, Permissions.FeedbackView or Permissions.FeedbackEdit) => true,
            (Permissions.ViewFormationDashboard, Permissions.FormationDashboardView) => true,
            _ => false
        };
    }
}
