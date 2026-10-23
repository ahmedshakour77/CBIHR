using API.HR.Node.Data.Workflows;

namespace API.HR.Node.Services.Workflows;

public record WorkflowAssignmentContext(
    WorkflowInstance Instance,
    WorkflowStep Step,
    string InitiatorUserId,
    string? PerformedByUserId = null);

public record WorkflowAssignmentResult(
    string? AssignedUserId,
    string? AssignedRoleId,
    int? AssignedDepartmentId);

public interface IWorkflowAssignmentResolver
{
    Task<WorkflowAssignmentResult> ResolveAsync(
        WorkflowAssignmentContext context,
        CancellationToken cancellationToken = default);
}
