using API.HR.Node.Data.Workflows;

namespace API.HR.Node.Services.Workflows;

public record WorkflowActionHandlerContext(
    WorkflowInstance Instance,
    WorkflowStep CurrentStep,
    WorkflowTransition Transition,
    string ActionCode,
    string UserId,
    string? Comment);

public interface IWorkflowActionHandler
{
    string SupportedEntityType { get; }

    Task OnBeforeActionAsync(
        WorkflowActionHandlerContext context,
        CancellationToken cancellationToken = default);

    Task OnAfterActionAsync(
        WorkflowActionHandlerContext context,
        CancellationToken cancellationToken = default);
}
