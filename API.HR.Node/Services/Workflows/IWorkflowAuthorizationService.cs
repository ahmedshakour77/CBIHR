using System.Security.Claims;
using API.HR.Node.Data.Workflows;

namespace API.HR.Node.Services.Workflows;

public interface IWorkflowAuthorizationService
{
    /// <summary>
    /// Checks whether the user is authorized to execute a specific transition on a workflow instance,
    /// enforcing assigned user / delegation, required permission, task pending status, and organizational scope.
    /// </summary>
    Task<bool> CanExecuteActionAsync(
        ClaimsPrincipal user,
        WorkflowInstance instance,
        WorkflowTransition transition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the user is authorized to view a workflow instance and its timeline history (IDOR protection).
    /// </summary>
    Task<bool> CanViewWorkflowAsync(
        ClaimsPrincipal user,
        WorkflowInstance instance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the user is authorized to view or access a specific workflow task (IDOR protection).
    /// </summary>
    Task<bool> CanViewTaskAsync(
        ClaimsPrincipal user,
        WorkflowTask task,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Filters available transitions to only those the current user is fully authorized to execute.
    /// </summary>
    Task<IReadOnlyList<WorkflowAvailableActionDto>> FilterAvailableActionsAsync(
        ClaimsPrincipal user,
        WorkflowInstance instance,
        IReadOnlyList<WorkflowTransition> transitions,
        CancellationToken cancellationToken = default);
}
