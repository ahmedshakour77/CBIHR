using System.Security.Claims;
using API.HR.Node.Data;
using API.HR.Node.Data.Workflows;
using API.HR.Node.Security;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Workflows;

public sealed class WorkflowAuthorizationService(ApplicationDbContext dbContext) : IWorkflowAuthorizationService
{
    public async Task<bool> CanExecuteActionAsync(
        ClaimsPrincipal user,
        WorkflowInstance instance,
        WorkflowTransition transition,
        CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // 1. Admin bypass
        if (IsAdmin(user))
        {
            return true;
        }

        // 2. Workflow must be actively running
        if (instance.Status != WorkflowInstanceStatus.Running)
        {
            return false;
        }

        var appUser = await GetApplicationUserAsync(user, cancellationToken);
        if (appUser == null)
        {
            return false;
        }

        // 3. Required Permission Check
        if (!string.IsNullOrWhiteSpace(transition.RequiredPermission))
        {
            var hasPerm = user.HasPermission(transition.RequiredPermission) ||
                          user.HasClaim("permission", transition.RequiredPermission) ||
                          user.HasClaim("permission", Permissions.ManageWorkflows);

            if (!hasPerm)
            {
                return false;
            }
        }

        // 4. Pending Task & Assignment Check
        var pendingTasks = await dbContext.WorkflowTasks
            .Where(t => t.WorkflowInstanceId == instance.Id &&
                        t.WorkflowStepId == instance.CurrentStepId &&
                        t.Status == WorkflowTaskStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingTasks.Count == 0)
        {
            return false;
        }
            var isDirectlyAssigned = pendingTasks.Any(t =>
                string.Equals(t.AssignedUserId, appUser.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.AssignedUserId, appUser.UserName, StringComparison.OrdinalIgnoreCase));

            var isRoleAssigned = pendingTasks.Any(t =>
                !string.IsNullOrWhiteSpace(t.AssignedRoleId) && user.IsInRole(t.AssignedRoleId));

            var isDepartmentAssigned = appUser.FormationId.HasValue && pendingTasks.Any(t =>
                t.AssignedDepartmentId.HasValue && t.AssignedDepartmentId.Value == appUser.FormationId.Value);

            var isDelegated = false;
            if (!isDirectlyAssigned && !isRoleAssigned && !isDepartmentAssigned)
            {
                var now = DateTimeOffset.UtcNow;
                var assignedUserIds = pendingTasks
                    .Where(t => !string.IsNullOrWhiteSpace(t.AssignedUserId))
                    .Select(t => t.AssignedUserId!)
                    .Distinct()
                    .ToList();

                if (assignedUserIds.Count > 0)
                {
                    var defCode = instance.WorkflowVersion?.WorkflowDefinition?.Code;
                    isDelegated = await dbContext.WorkflowDelegations.AnyAsync(d =>
                        d.IsActive &&
                        d.StartDate <= now &&
                        d.EndDate >= now &&
                        (d.DelegateeUserId == appUser.Id || d.DelegateeUserId == appUser.UserName) &&
                        assignedUserIds.Contains(d.DelegatorUserId) &&
                        (d.WorkflowDefinitionCode == null || d.WorkflowDefinitionCode == defCode),
                        cancellationToken);
                }
            }

        if (!isDirectlyAssigned && !isRoleAssigned && !isDepartmentAssigned && !isDelegated)
        {
            return false;
        }

        // 5. Organizational Scope Check
        return await CheckOrganizationalScopeAsync(appUser, user, instance, cancellationToken);
    }

    public async Task<bool> CanViewWorkflowAsync(
        ClaimsPrincipal user,
        WorkflowInstance instance,
        CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (IsAdmin(user))
        {
            return true;
        }

        var appUser = await GetApplicationUserAsync(user, cancellationToken);
        if (appUser == null)
        {
            return false;
        }

        // Initiator can view their own instance
        if (string.Equals(instance.InitiatorUserId, appUser.Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(instance.InitiatorUserId, appUser.UserName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Target employee can view their own instance
        if (string.Equals(instance.EntityType, "Employee", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(appUser.EmployeeID) &&
            string.Equals(instance.EntityId, appUser.EmployeeID, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Assigned to any task (past or pending)
        var isTaskParticipant = await dbContext.WorkflowTasks.AnyAsync(t =>
            t.WorkflowInstanceId == instance.Id &&
            (t.AssignedUserId == appUser.Id || t.AssignedUserId == appUser.UserName),
            cancellationToken);

        if (isTaskParticipant)
        {
            return true;
        }

        // Staff / Workflow view permission with matching organizational scope
        var hasViewPerm = user.HasPermission(Permissions.WorkflowsView) ||
                          user.HasPermission(Permissions.WorkflowsEdit) ||
                          user.HasClaim("permission", Permissions.ManageWorkflows) ||
                          user.IsInRole("HRManager") ||
                          user.IsInRole("Supervisor");

        if (hasViewPerm)
        {
            return await CheckOrganizationalScopeAsync(appUser, user, instance, cancellationToken);
        }

        return false;
    }

    public async Task<bool> CanViewTaskAsync(
        ClaimsPrincipal user,
        WorkflowTask task,
        CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (IsAdmin(user))
        {
            return true;
        }

        var appUser = await GetApplicationUserAsync(user, cancellationToken);
        if (appUser == null)
        {
            return false;
        }

        // Directly assigned
        if (string.Equals(task.AssignedUserId, appUser.Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(task.AssignedUserId, appUser.UserName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Role assigned
        if (!string.IsNullOrWhiteSpace(task.AssignedRoleId) && user.IsInRole(task.AssignedRoleId))
        {
            return true;
        }

        // Department assigned
        if (task.AssignedDepartmentId.HasValue && appUser.FormationId.HasValue &&
            task.AssignedDepartmentId.Value == appUser.FormationId.Value)
        {
            return true;
        }

        // Check if user is delegated
        if (!string.IsNullOrWhiteSpace(task.AssignedUserId))
        {
            var now = DateTimeOffset.UtcNow;
            var isDelegated = await dbContext.WorkflowDelegations.AnyAsync(d =>
                d.IsActive &&
                d.StartDate <= now &&
                d.EndDate >= now &&
                (d.DelegateeUserId == appUser.Id || d.DelegateeUserId == appUser.UserName) &&
                d.DelegatorUserId == task.AssignedUserId,
                cancellationToken);

            if (isDelegated)
            {
                return true;
            }
        }

        // Check parent workflow instance access
        var instance = task.WorkflowInstance ?? await dbContext.WorkflowInstances
            .Include(i => i.WorkflowVersion)
            .ThenInclude(v => v.WorkflowDefinition)
            .FirstOrDefaultAsync(i => i.Id == task.WorkflowInstanceId, cancellationToken);

        if (instance != null)
        {
            return await CanViewWorkflowAsync(user, instance, cancellationToken);
        }

        return false;
    }

    public async Task<IReadOnlyList<WorkflowAvailableActionDto>> FilterAvailableActionsAsync(
        ClaimsPrincipal user,
        WorkflowInstance instance,
        IReadOnlyList<WorkflowTransition> transitions,
        CancellationToken cancellationToken = default)
    {
        var authorizedActions = new List<WorkflowAvailableActionDto>();

        foreach (var transition in transitions)
        {
            var isAllowed = await CanExecuteActionAsync(user, instance, transition, cancellationToken);
            if (isAllowed)
            {
                authorizedActions.Add(new WorkflowAvailableActionDto(
                    Code: transition.ActionCode,
                    Label: GetActionLabel(transition.ActionCode),
                    RequiresComment: transition.ActionCode is "REJECT" or "RETURN",
                    RequiredPermission: transition.RequiredPermission
                ));
            }
        }

        return authorizedActions;
    }

    private static bool IsAdmin(ClaimsPrincipal user) =>
        user.IsInRole("Admin") ||
        user.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase));

    private async Task<ApplicationUser?> GetApplicationUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = user.Identity?.Name;

        return await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => (userId != null && u.Id == userId) || (userName != null && u.UserName == userName), cancellationToken);
    }

    private async Task<bool> CheckOrganizationalScopeAsync(
        ApplicationUser appUser,
        ClaimsPrincipal user,
        WorkflowInstance instance,
        CancellationToken cancellationToken)
    {
        // System-wide HR managers and Admins have global scope
        if (user.IsInRole("Admin") || user.IsInRole("HRManager") || user.HasPermission(Permissions.EmployeesView))
        {
            return true;
        }

        // If user has a formation scope assigned, enforce that entity belongs to the same formation
        if (appUser.FormationId.HasValue && string.Equals(instance.EntityType, "Employee", StringComparison.OrdinalIgnoreCase))
        {
            var employeeFormationId = await dbContext.Employees
                .Where(e => e.EmployeeID == instance.EntityId)
                .Select(e => e.FormationId)
                .FirstOrDefaultAsync(cancellationToken);

            if (employeeFormationId.HasValue && employeeFormationId.Value != appUser.FormationId.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static string GetActionLabel(string code) => code.ToUpperInvariant() switch
    {
        "APPROVE" => "موافقة",
        "REJECT" => "رفض",
        "RETURN" => "إعادة للتدقيق",
        "CANCEL" => "إلغاء",
        "START" => "بدء",
        _ => code
    };
}
