using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data.Workflows;

public enum WorkflowVersionStatus
{
    Draft = 0,
    Published = 1,
    Retired = 2
}

public enum WorkflowStepType
{
    Start = 0,
    HumanTask = 1,
    Review = 2,
    Approval = 3,
    Decision = 4,
    SystemAction = 5,
    ParallelGateway = 6,
    JoinGateway = 7,
    Wait = 8,
    End = 9
}

public enum WorkflowInstanceStatus
{
    Running = 0,
    Completed = 1,
    Rejected = 2,
    Cancelled = 3,
    Suspended = 4,
    Failed = 5
}

public enum WorkflowTaskStatus
{
    Pending = 0,
    Assigned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    Expired = 5
}

public sealed class WorkflowDefinition
{
    public long Id { get; set; }

    [MaxLength(100)]
    public required string Code { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public string? FormSchemaJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [MaxLength(256)]
    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    [MaxLength(256)]
    public string? UpdatedBy { get; set; }

    public ICollection<WorkflowVersion> Versions { get; set; } = new List<WorkflowVersion>();
}

public sealed class WorkflowVersion
{
    public long Id { get; set; }

    public long WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;

    public int VersionNumber { get; set; }
    public WorkflowVersionStatus Status { get; set; } = WorkflowVersionStatus.Draft;

    [MaxLength(1000)]
    public string? ChangeDescription { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [MaxLength(256)]
    public string? CreatedBy { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
    [MaxLength(256)]
    public string? PublishedBy { get; set; }

    public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
}

public sealed class WorkflowStep
{
    public long Id { get; set; }

    public long WorkflowVersionId { get; set; }
    public WorkflowVersion WorkflowVersion { get; set; } = null!;

    [MaxLength(100)]
    public required string Code { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public WorkflowStepType StepType { get; set; } = WorkflowStepType.HumanTask;

    public bool IsStart { get; set; }
    public bool IsFinal { get; set; }
    public int SortOrder { get; set; }

    public bool AllowReject { get; set; } = true;
    public bool AllowReturn { get; set; } = true;
    public bool AllowCancel { get; set; } = true;
    public bool AllowDelegate { get; set; } = true;
    public bool AllowReassign { get; set; } = true;

    public int? SlaMinutes { get; set; }

    // JSON configuration for assignment rules, custom permissions, handler parameters
    public string? ConfigurationJson { get; set; }
}

public sealed class WorkflowTransition
{
    public long Id { get; set; }

    public long WorkflowVersionId { get; set; }
    public WorkflowVersion WorkflowVersion { get; set; } = null!;

    public long FromStepId { get; set; }
    public WorkflowStep FromStep { get; set; } = null!;

    public long ToStepId { get; set; }
    public WorkflowStep ToStep { get; set; } = null!;

    [MaxLength(100)]
    public required string ActionCode { get; set; }

    public int Priority { get; set; }
    public bool IsAutomatic { get; set; }

    public string? ConditionConfiguration { get; set; }

    [MaxLength(200)]
    public string? RequiredPermission { get; set; }

    public string? ConfigurationJson { get; set; }

    public ICollection<WorkflowCondition> Conditions { get; set; } = new List<WorkflowCondition>();
}

public sealed class WorkflowCondition
{
    public long Id { get; set; }

    public long TransitionId { get; set; }
    public WorkflowTransition Transition { get; set; } = null!;

    [MaxLength(100)]
    public required string FieldName { get; set; }

    [MaxLength(20)]
    public required string Operator { get; set; } // ==, !=, >, >=, <, <=, Contains, In

    [MaxLength(500)]
    public required string Value { get; set; }

    [MaxLength(50)]
    public string? LogicGroup { get; set; }
}
