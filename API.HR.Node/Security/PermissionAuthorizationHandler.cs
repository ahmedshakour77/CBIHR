using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace API.HR.Node.Security;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity is not { IsAuthenticated: true })
        {
            return Task.CompletedTask;
        }

        // Admins bypass specific permission checks
        if (context.User.IsInRole("Admin") || context.User.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var req = requirement.Permission;

        // Role-based operational compatibility for existing staff
        var isHRManager = context.User.IsInRole("HRManager") || context.User.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value.Equals("HRManager", StringComparison.OrdinalIgnoreCase));
        if (isHRManager && !req.Equals(Permissions.ManageUsers, StringComparison.OrdinalIgnoreCase) && !req.Equals(Permissions.ViewAuditLogs, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var isSupervisor = context.User.IsInRole("Supervisor") || context.User.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value.Equals("Supervisor", StringComparison.OrdinalIgnoreCase));
        if (isSupervisor && (req.EndsWith(".View", StringComparison.OrdinalIgnoreCase) || req is Permissions.LeavesEdit or Permissions.WorkflowsEdit or Permissions.AppreciationLettersEdit))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if user has explicit matching permission claim or implied Edit permission
        var hasPermission = context.User.HasClaim(c =>
        {
            if (c.Type != "permission" && c.Type != "Permission") return false;

            // Direct match
            if (c.Value.Equals(req, StringComparison.OrdinalIgnoreCase)) return true;

            // If checking for View, an Edit permission for the same feature automatically grants View
            if (req.EndsWith(".View", StringComparison.OrdinalIgnoreCase))
            {
                var editEquiv = req[..^5] + ".Edit";
                if (c.Value.Equals(editEquiv, StringComparison.OrdinalIgnoreCase)) return true;
            }

            // Legacy mappings check
            if (IsLegacyPermissionMatch(c.Value, req)) return true;

            return false;
        });

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool IsLegacyPermissionMatch(string userClaim, string requestedPermission)
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
