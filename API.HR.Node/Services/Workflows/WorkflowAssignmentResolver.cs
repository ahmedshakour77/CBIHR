using System.Text.Json;
using API.HR.Node.Data;
using API.HR.Node.Data.Workflows;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Workflows;

public class WorkflowAssignmentResolver(ApplicationDbContext dbContext) : IWorkflowAssignmentResolver
{
    public async Task<WorkflowAssignmentResult> ResolveAsync(
        WorkflowAssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        string? targetUser = null;
        string? targetRole = null;
        int? targetDept = null;

        if (!string.IsNullOrWhiteSpace(context.Step.ConfigurationJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(context.Step.ConfigurationJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("assignmentType", out var typeElem))
                {
                    var type = typeElem.GetString();
                    switch (type?.ToUpperInvariant())
                    {
                        case "INITIATOR":
                            targetUser = context.InitiatorUserId;
                            break;

                        case "USER":
                            if (root.TryGetProperty("userId", out var u)) targetUser = u.GetString();
                            break;

                        case "ROLE":
                            if (root.TryGetProperty("roleId", out var r)) targetRole = r.GetString();
                            break;

                        case "DEPARTMENT":
                            if (root.TryGetProperty("departmentId", out var d) && d.TryGetInt32(out var deptId))
                            {
                                targetDept = deptId;
                            }
                            break;

                        case "MANAGER_OF_EMPLOYEE":
                            targetUser = await GetManagerForUserAsync(context.InitiatorUserId, cancellationToken) ?? context.InitiatorUserId;
                            break;
                    }
                }
            }
            catch
            {
                // Fallback to Initiator if JSON parsing fails
                targetUser = context.InitiatorUserId;
            }
        }

        // Default fallback if nothing specified
        if (targetUser == null && targetRole == null && targetDept == null)
        {
            targetUser = context.InitiatorUserId;
        }

        // Check active delegations if assigned to a specific user
        if (!string.IsNullOrEmpty(targetUser))
        {
            var now = DateTimeOffset.UtcNow;
            var activeDelegation = await dbContext.WorkflowDelegations
                .FirstOrDefaultAsync(d => d.DelegatorUserId == targetUser &&
                                          d.IsActive &&
                                          d.StartDate <= now &&
                                          d.EndDate >= now, cancellationToken);

            if (activeDelegation != null)
            {
                targetUser = activeDelegation.DelegateeUserId;
            }
        }

        return new WorkflowAssignmentResult(targetUser, targetRole, targetDept);
    }

    private async Task<string?> GetManagerForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user?.Employee == null) return null;

        // 1. Check dynamic Administrative Unit manager first
        if (user.Employee.AdministrativeUnitId != null)
        {
            var unit = await dbContext.AdministrativeUnits
                .AsNoTracking()
                .Include(u => u.ParentUnit)
                .FirstOrDefaultAsync(u => u.Id == user.Employee.AdministrativeUnitId, cancellationToken);

            var managerEmpId = unit?.ManagerEmployeeId;

            // If the user is the manager of this unit, escalate to the parent unit's manager
            if (managerEmpId == user.Employee.EmployeeID && unit?.ParentUnit != null)
            {
                managerEmpId = unit.ParentUnit.ManagerEmployeeId;
            }

            if (!string.IsNullOrWhiteSpace(managerEmpId) && managerEmpId != user.Employee.EmployeeID)
            {
                var managerUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.EmployeeID == managerEmpId, cancellationToken);
                if (managerUser != null)
                {
                    return managerUser.Id;
                }
            }
        }

        // 2. Fallback to legacy Formation supervisor
        if (user.Employee.FormationId != null)
        {
            var manager = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Employee != null && u.Employee.FormationId == user.Employee.FormationId && u.Id != userId, cancellationToken);
            return manager?.Id;
        }

        return null;
    }
}
