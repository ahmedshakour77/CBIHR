using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data.Workflows;

public sealed class WorkflowInstance
{
    public long Id { get; set; }

    public long WorkflowVersionId { get; set; }
    public WorkflowVersion WorkflowVersion { get; set; } = null!;

    [MaxLength(100)]
    public required string EntityType { get; set; }

    [MaxLength(100)]
    public required string EntityId { get; set; }

    public long CurrentStepId { get; set; }
    public WorkflowStep CurrentStep { get; set; } = null!;

    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Running;

    [MaxLength(256)]
    public required string InitiatorUserId { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<WorkflowTask> Tasks { get; set; } = new List<WorkflowTask>();
    public ICollection<WorkflowEvent> Events { get; set; } = new List<WorkflowEvent>();
    public ICollection<WorkflowComment> Comments { get; set; } = new List<WorkflowComment>();
}

public sealed class WorkflowTask
{
    public long Id { get; set; }

    public long WorkflowInstanceId { get; set; }
    public WorkflowInstance WorkflowInstance { get; set; } = null!;

    public long WorkflowStepId { get; set; }
    public WorkflowStep WorkflowStep { get; set; } = null!;

    public WorkflowTaskStatus Status { get; set; } = WorkflowTaskStatus.Pending;

    [MaxLength(256)]
    public string? AssignedUserId { get; set; }

    [MaxLength(256)]
    public string? AssignedRoleId { get; set; }

    public int? AssignedDepartmentId { get; set; } // References FormationId

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public int Priority { get; set; }

    [MaxLength(256)]
    public string? DelegatedFromUserId { get; set; }

    [MaxLength(256)]
    public string? ReassignedFromUserId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}

public sealed class WorkflowEvent
{
    public long Id { get; set; }

    public long WorkflowInstanceId { get; set; }
    public WorkflowInstance WorkflowInstance { get; set; } = null!;

    public long WorkflowVersionId { get; set; }

    public long? StepId { get; set; }

    [MaxLength(100)]
    public required string EventType { get; set; } // WORKFLOW_STARTED, STEP_ENTERED, STEP_COMPLETED, ACTION_EXECUTED, APPROVED, REJECTED, RETURNED, REASSIGNED, DELEGATED, WORKFLOW_COMPLETED, WORKFLOW_CANCELLED

    [MaxLength(100)]
    public string? ActionCode { get; set; }

    public long? FromStepId { get; set; }
    public long? ToStepId { get; set; }

    [MaxLength(256)]
    public required string PerformedByUserId { get; set; }

    [MaxLength(300)]
    public string? PerformedByDisplayName { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(2000)]
    public string? Comment { get; set; }

    public string? MetadataJson { get; set; }

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }
}

public sealed class WorkflowComment
{
    public long Id { get; set; }

    public long WorkflowInstanceId { get; set; }
    public WorkflowInstance WorkflowInstance { get; set; } = null!;

    public long? WorkflowTaskId { get; set; }
    public WorkflowTask? WorkflowTask { get; set; }

    [MaxLength(256)]
    public required string UserId { get; set; }

    [MaxLength(50)]
    public required string CommentType { get; set; } // General, Decision, Rejection, Return

    [MaxLength(2000)]
    public required string Content { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkflowDelegation
{
    public long Id { get; set; }

    [MaxLength(256)]
    public required string DelegatorUserId { get; set; }

    [MaxLength(256)]
    public required string DelegateeUserId { get; set; }

    [MaxLength(100)]
    public string? WorkflowDefinitionCode { get; set; }

    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkflowIdempotencyRecord
{
    public long Id { get; set; }

    [MaxLength(128)]
    public required string IdempotencyKey { get; set; }

    [MaxLength(100)]
    public required string ActionCode { get; set; }

    public long WorkflowInstanceId { get; set; }

    [MaxLength(64)]
    public string? RequestHash { get; set; }

    public string? ResponseJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkflowOutboxMessage
{
    public long Id { get; set; }

    [MaxLength(100)]
    public required string MessageType { get; set; }

    public required string PayloadJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }

    [MaxLength(1000)]
    public string? Error { get; set; }
}
