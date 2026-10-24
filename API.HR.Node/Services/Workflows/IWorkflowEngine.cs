using System.Security.Claims;
using API.HR.Node.Data.Workflows;

namespace API.HR.Node.Services.Workflows;

public record StartWorkflowCommand(
    string WorkflowCode,
    string EntityType,
    string EntityId,
    string InitiatorUserId,
    string? CorrelationId = null,
    Dictionary<string, object>? InitialContext = null);

public record ExecuteWorkflowActionCommand(
    long WorkflowInstanceId,
    string ActionCode,
    string UserId,
    string? Comment = null,
    byte[]? RowVersion = null,
    string? IdempotencyKey = null,
    string? CorrelationId = null,
    string? IpAddress = null,
    string? UserAgent = null,
    ClaimsPrincipal? User = null);

public record WorkflowStartResult(
    long WorkflowInstanceId,
    string WorkflowCode,
    int VersionNumber,
    string CurrentStepCode,
    string CurrentStepName,
    WorkflowInstanceStatus Status,
    DateTimeOffset StartedAt);

public record WorkflowActionResult(
    long WorkflowInstanceId,
    string FromStepCode,
    string ToStepCode,
    string ExecutedAction,
    WorkflowInstanceStatus Status,
    byte[] RowVersion,
    DateTimeOffset Timestamp);

public record WorkflowAvailableActionDto(
    string Code,
    string Label,
    bool RequiresComment,
    string? RequiredPermission);

public record WorkflowTaskDto(
    long Id,
    long WorkflowInstanceId,
    string StepCode,
    string StepName,
    WorkflowTaskStatus Status,
    string? AssignedUserId,
    string? AssignedRoleId,
    int? AssignedDepartmentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt);

public record WorkflowInstanceDto(
    long Id,
    string WorkflowCode,
    string WorkflowName,
    int VersionNumber,
    string EntityType,
    string EntityId,
    string CurrentStepCode,
    string CurrentStepName,
    WorkflowInstanceStatus Status,
    string InitiatorUserId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    byte[] RowVersion,
    List<WorkflowTaskDto> Tasks,
    List<WorkflowAvailableActionDto> AvailableActions);

public record WorkflowTimelineItemDto(
    long EventId,
    string Type,
    string Title,
    string? StepCode,
    string? FromStep,
    string? ToStep,
    string PerformedByUserId,
    string? PerformedByDisplayName,
    string? Comment,
    DateTimeOffset OccurredAt,
    string? MetadataJson);

public interface IWorkflowEngine
{
    Task<WorkflowStartResult> StartAsync(StartWorkflowCommand command, CancellationToken cancellationToken = default);
    Task<WorkflowActionResult> ExecuteActionAsync(ExecuteWorkflowActionCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowAvailableActionDto>> GetAvailableActionsAsync(long workflowInstanceId, string userId, ClaimsPrincipal? user = null, CancellationToken cancellationToken = default);
    Task<WorkflowInstanceDto> GetInstanceAsync(long workflowInstanceId, CancellationToken cancellationToken = default);
}
