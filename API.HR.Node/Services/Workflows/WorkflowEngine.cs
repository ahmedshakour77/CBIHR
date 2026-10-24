using System.Security.Claims;
using System.Text.Json;
using API.HR.Node.Data;
using API.HR.Node.Data.Workflows;
using API.HR.Node.Data.Notifications;
using API.HR.Node.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Workflows;

public class WorkflowEngine(
    ApplicationDbContext dbContext,
    IWorkflowAssignmentResolver assignmentResolver,
    IWorkflowConditionEvaluator conditionEvaluator,
    IWorkflowTimelineService timelineService,
    IWorkflowAuthorizationService authorizationService,
    IEnumerable<IWorkflowActionHandler> actionHandlers,
    INotificationService? notificationService = null) : IWorkflowEngine
{
    public async Task<WorkflowStartResult> StartAsync(
        StartWorkflowCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch Definition
        var definition = await dbContext.WorkflowDefinitions
            .FirstOrDefaultAsync(d => d.Code == command.WorkflowCode && d.IsEnabled, cancellationToken)
            ?? throw new InvalidOperationException($"WORKFLOW_DEFINITION_NOT_FOUND: Workflow definition '{command.WorkflowCode}' not found or disabled.");

        // 2. Fetch Latest Published Version
        var version = await dbContext.WorkflowVersions
            .Include(v => v.Steps)
            .Include(v => v.Transitions)
            .ThenInclude(t => t.Conditions)
            .FirstOrDefaultAsync(v => v.WorkflowDefinitionId == definition.Id && v.Status == WorkflowVersionStatus.Published, cancellationToken)
            ?? throw new InvalidOperationException($"WORKFLOW_VERSION_NOT_AVAILABLE: No published version available for workflow '{command.WorkflowCode}'.");

        // 3. Verify no duplicate active running workflow instance for same entity
        var existingActive = await dbContext.WorkflowInstances
            .AnyAsync(i => i.EntityType == command.EntityType &&
                           i.EntityId == command.EntityId &&
                           i.Status == WorkflowInstanceStatus.Running, cancellationToken);

        if (existingActive)
        {
            throw new InvalidOperationException($"WORKFLOW_ALREADY_RUNNING: An active workflow instance already exists for {command.EntityType}:{command.EntityId}.");
        }

        // 4. Find Start Step
        var startStep = version.Steps.FirstOrDefault(s => s.IsStart)
            ?? version.Steps.OrderBy(s => s.SortOrder).First();

        // 5. Create WorkflowInstance
        var instance = new WorkflowInstance
        {
            WorkflowVersionId = version.Id,
            EntityType = command.EntityType,
            EntityId = command.EntityId,
            CurrentStepId = startStep.Id,
            Status = WorkflowInstanceStatus.Running,
            InitiatorUserId = command.InitiatorUserId,
            StartedAt = DateTimeOffset.UtcNow
        };

        dbContext.WorkflowInstances.Add(instance);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 6. Record WORKFLOW_STARTED event
        await timelineService.AppendEventAsync(
            workflowInstanceId: instance.Id,
            workflowVersionId: version.Id,
            stepId: startStep.Id,
            eventType: "WORKFLOW_STARTED",
            actionCode: "START",
            fromStepId: null,
            toStepId: startStep.Id,
            userId: command.InitiatorUserId,
            displayName: command.InitiatorUserId,
            comment: "Workflow initialized",
            correlationId: command.CorrelationId,
            cancellationToken: cancellationToken);

        // 7. Check automatic initial transition from Start step
        var initialTransition = version.Transitions
            .Where(t => t.FromStepId == startStep.Id)
            .OrderBy(t => t.Priority)
            .FirstOrDefault(t => conditionEvaluator.EvaluateConditions(t.Conditions, command.InitialContext));

        var targetStep = startStep;
        if (initialTransition != null)
        {
            var nextStep = version.Steps.FirstOrDefault(s => s.Id == initialTransition.ToStepId);
            if (nextStep != null)
            {
                instance.CurrentStepId = nextStep.Id;
                targetStep = nextStep;
                await dbContext.SaveChangesAsync(cancellationToken);

                await timelineService.AppendEventAsync(
                    workflowInstanceId: instance.Id,
                    workflowVersionId: version.Id,
                    stepId: nextStep.Id,
                    eventType: "STEP_ENTERED",
                    actionCode: initialTransition.ActionCode,
                    fromStepId: startStep.Id,
                    toStepId: nextStep.Id,
                    userId: command.InitiatorUserId,
                    displayName: command.InitiatorUserId,
                    comment: $"Advanced to initial step {nextStep.Name}",
                    correlationId: command.CorrelationId,
                    cancellationToken: cancellationToken);
            }
        }

        // 8. Create initial Human Task if step is HumanTask / Review / Approval
        if (targetStep.StepType is WorkflowStepType.HumanTask or WorkflowStepType.Review or WorkflowStepType.Approval)
        {
            await CreateTaskForStepAsync(instance, targetStep, command.InitiatorUserId, cancellationToken);
        }

        return new WorkflowStartResult(
            WorkflowInstanceId: instance.Id,
            WorkflowCode: definition.Code,
            VersionNumber: version.VersionNumber,
            CurrentStepCode: targetStep.Code,
            CurrentStepName: targetStep.Name,
            Status: instance.Status,
            StartedAt: instance.StartedAt);
    }

    public async Task<WorkflowActionResult> ExecuteActionAsync(
        ExecuteWorkflowActionCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Check Idempotency Key (scoped to Instance and ActionCode)
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var existingRecord = await dbContext.WorkflowIdempotencyRecords
                .FirstOrDefaultAsync(r => r.IdempotencyKey == command.IdempotencyKey
                                       && r.WorkflowInstanceId == command.WorkflowInstanceId
                                       && r.ActionCode == command.ActionCode, cancellationToken);

            if (existingRecord != null && !string.IsNullOrWhiteSpace(existingRecord.ResponseJson))
            {
                var cachedResult = JsonSerializer.Deserialize<WorkflowActionResult>(existingRecord.ResponseJson);
                if (cachedResult != null) return cachedResult;
            }
        }

        // 2. Load WorkflowInstance
        var instance = await dbContext.WorkflowInstances
            .Include(i => i.WorkflowVersion)
            .ThenInclude(v => v.WorkflowDefinition)
            .Include(i => i.CurrentStep)
            .FirstOrDefaultAsync(i => i.Id == command.WorkflowInstanceId, cancellationToken)
            ?? throw new InvalidOperationException($"WORKFLOW_INSTANCE_NOT_FOUND: Workflow instance #{command.WorkflowInstanceId} not found.");

        if (instance.Status != WorkflowInstanceStatus.Running)
        {
            throw new InvalidOperationException($"WORKFLOW_ALREADY_COMPLETED: Workflow instance #{command.WorkflowInstanceId} is not active (Status: {instance.Status}).");
        }

        // 3. Optimistic Concurrency Protection
        if (command.RowVersion != null && command.RowVersion.Length > 0)
        {
            if (!command.RowVersion.SequenceEqual(instance.RowVersion))
            {
                throw new InvalidOperationException("WORKFLOW_CONCURRENCY_CONFLICT: The workflow instance was modified by another concurrent request.");
            }
        }

        // 4. Load Version Transitions
        var transitions = await dbContext.WorkflowTransitions
            .Include(t => t.ToStep)
            .Include(t => t.Conditions)
            .Where(t => t.WorkflowVersionId == instance.WorkflowVersionId && t.FromStepId == instance.CurrentStepId)
            .OrderBy(t => t.Priority)
            .ToListAsync(cancellationToken);

        var transition = transitions.FirstOrDefault(t => string.Equals(t.ActionCode, command.ActionCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"WORKFLOW_ACTION_NOT_ALLOWED: Action '{command.ActionCode}' is not valid for current step '{instance.CurrentStep.Code}'.");

        // 5. Evaluate Conditions
        var contextDict = new Dictionary<string, object>
        {
            { "EntityType", instance.EntityType },
            { "EntityId", instance.EntityId },
            { "InitiatorUserId", instance.InitiatorUserId },
            { "ActionCode", command.ActionCode }
        };

        if (!conditionEvaluator.EvaluateConditions(transition.Conditions, contextDict))
        {
            throw new InvalidOperationException($"WORKFLOW_CONDITION_FAILED: Conditions for action '{command.ActionCode}' were not satisfied.");
        }

        // 5.1 Mandatory Security & Assignment Authorization Check
        if (command.User != null)
        {
            var isAuthorized = await authorizationService.CanExecuteActionAsync(command.User, instance, transition, cancellationToken);
            if (!isAuthorized)
            {
                throw new UnauthorizedAccessException($"WORKFLOW_FORBIDDEN: User '{command.UserId}' does not have the required permission or assignment to execute action '{command.ActionCode}' on workflow instance #{instance.Id}.");
            }
        }

        // Begin Single Atomic Transaction for Transition, Events, Tasks, Comments, and Idempotency Record
        await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // 6. Execute Domain Handler BeforeAction hooks
        var handlerContext = new WorkflowActionHandlerContext(
            instance,
            instance.CurrentStep,
            transition,
            command.ActionCode,
            command.UserId,
            command.Comment);

        var handler = actionHandlers.FirstOrDefault(h => string.Equals(h.SupportedEntityType, instance.EntityType, StringComparison.OrdinalIgnoreCase));
        if (handler != null)
        {
            await handler.OnBeforeActionAsync(handlerContext, cancellationToken);
        }

        // 7. Complete pending WorkflowTasks for current step
        var currentTasks = await dbContext.WorkflowTasks
            .Where(t => t.WorkflowInstanceId == instance.Id && t.WorkflowStepId == instance.CurrentStepId && t.Status == WorkflowTaskStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var task in currentTasks)
        {
            task.Status = WorkflowTaskStatus.Completed;
            task.CompletedAt = DateTimeOffset.UtcNow;
        }

        var fromStepCode = instance.CurrentStep.Code;
        var toStep = transition.ToStep;

        // 8. Transition Instance
        instance.CurrentStepId = toStep.Id;

        if (toStep.IsFinal)
        {
            instance.Status = command.ActionCode.ToUpperInvariant() switch
            {
                "REJECT" or "REJECTED" => WorkflowInstanceStatus.Rejected,
                "CANCEL" or "CANCELLED" => WorkflowInstanceStatus.Cancelled,
                _ => WorkflowInstanceStatus.Completed
            };
            instance.CompletedAt = DateTimeOffset.UtcNow;
        }

        // Save state changes
        await dbContext.SaveChangesAsync(cancellationToken);

        // 9. Create WorkflowEvents (Append-only Timeline)
        await timelineService.AppendEventAsync(
            workflowInstanceId: instance.Id,
            workflowVersionId: instance.WorkflowVersionId,
            stepId: instance.CurrentStepId,
            eventType: command.ActionCode.ToUpperInvariant() switch
            {
                "APPROVE" => "APPROVED",
                "REJECT" => "REJECTED",
                "RETURN" => "RETURNED",
                _ => "ACTION_EXECUTED"
            },
            actionCode: command.ActionCode,
            fromStepId: transition.FromStepId,
            toStepId: toStep.Id,
            userId: command.UserId,
            displayName: command.UserId,
            comment: command.Comment,
            correlationId: command.CorrelationId,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent,
            cancellationToken: cancellationToken);

        if (toStep.IsFinal)
        {
            await timelineService.AppendEventAsync(
                workflowInstanceId: instance.Id,
                workflowVersionId: instance.WorkflowVersionId,
                stepId: toStep.Id,
                eventType: instance.Status == WorkflowInstanceStatus.Completed ? "WORKFLOW_COMPLETED" : "WORKFLOW_CANCELLED",
                actionCode: command.ActionCode,
                fromStepId: transition.FromStepId,
                toStepId: toStep.Id,
                userId: command.UserId,
                displayName: command.UserId,
                comment: $"Workflow finished with status: {instance.Status}",
                correlationId: command.CorrelationId,
                cancellationToken: cancellationToken);
        }
        else if (toStep.StepType is WorkflowStepType.HumanTask or WorkflowStepType.Review or WorkflowStepType.Approval)
        {
            await CreateTaskForStepAsync(instance, toStep, instance.InitiatorUserId, cancellationToken);
        }

        // 10. Execute Domain Handler AfterAction hooks
        if (handler != null)
        {
            await handler.OnAfterActionAsync(handlerContext, cancellationToken);
        }

        // 11. Record comment if provided
        if (!string.IsNullOrWhiteSpace(command.Comment))
        {
            dbContext.WorkflowComments.Add(new WorkflowComment
            {
                WorkflowInstanceId = instance.Id,
                UserId = command.UserId,
                CommentType = command.ActionCode,
                Content = command.Comment,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 11.1 Send notification to initiator if action performed by someone else
        if (notificationService != null && !string.IsNullOrWhiteSpace(instance.InitiatorUserId) && instance.InitiatorUserId != command.UserId)
        {
            try
            {
                var defName = instance.WorkflowVersion?.WorkflowDefinition?.Name ?? "المعاملة الإدارية";
                var actionLabel = GetActionLabel(command.ActionCode);
                var notifType = command.ActionCode.ToUpperInvariant() switch
                {
                    "APPROVE" => NotificationType.Success,
                    "REJECT" => NotificationType.Error,
                    "RETURN" => NotificationType.Warning,
                    _ => NotificationType.Info
                };

                await notificationService.SendAsync(
                    userId: instance.InitiatorUserId,
                    title: $"{actionLabel}: معاملة #{instance.Id}",
                    message: $"تم اتخاذ إجراء ({actionLabel}) على معاملتك ({defName}) من قِبل {command.UserId}. {(string.IsNullOrWhiteSpace(command.Comment) ? "" : "السبب: " + command.Comment)}",
                    type: notifType,
                    linkUrl: "/workflows",
                    cancellationToken: cancellationToken);
            }
            catch { /* Notification failure should not roll back transaction */ }
        }

        var result = new WorkflowActionResult(
            WorkflowInstanceId: instance.Id,
            FromStepCode: fromStepCode,
            ToStepCode: toStep.Code,
            ExecutedAction: command.ActionCode,
            Status: instance.Status,
            RowVersion: instance.RowVersion,
            Timestamp: DateTimeOffset.UtcNow);

        // 12. Save Idempotency Record
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            dbContext.WorkflowIdempotencyRecords.Add(new WorkflowIdempotencyRecord
            {
                IdempotencyKey = command.IdempotencyKey,
                ActionCode = command.ActionCode,
                WorkflowInstanceId = instance.Id,
                ResponseJson = JsonSerializer.Serialize(result),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Commit transaction atomically
        await dbTransaction.CommitAsync(cancellationToken);

        return result;
    }

    public async Task<IReadOnlyList<WorkflowAvailableActionDto>> GetAvailableActionsAsync(
        long workflowInstanceId,
        string userId,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default)
    {
        var instance = await dbContext.WorkflowInstances
            .Include(i => i.CurrentStep)
            .Include(i => i.WorkflowVersion)
            .ThenInclude(v => v.WorkflowDefinition)
            .FirstOrDefaultAsync(i => i.Id == workflowInstanceId, cancellationToken);

        if (instance == null || instance.Status != WorkflowInstanceStatus.Running)
        {
            return Array.Empty<WorkflowAvailableActionDto>();
        }

        var transitions = await dbContext.WorkflowTransitions
            .Where(t => t.WorkflowVersionId == instance.WorkflowVersionId && t.FromStepId == instance.CurrentStepId)
            .OrderBy(t => t.Priority)
            .ToListAsync(cancellationToken);

        if (user != null)
        {
            return await authorizationService.FilterAvailableActionsAsync(user, instance, transitions, cancellationToken);
        }

        return transitions.Select(t => new WorkflowAvailableActionDto(
            Code: t.ActionCode,
            Label: GetActionLabel(t.ActionCode),
            RequiresComment: t.ActionCode is "REJECT" or "RETURN",
            RequiredPermission: t.RequiredPermission
        )).ToList();
    }

    public async Task<WorkflowInstanceDto> GetInstanceAsync(
        long workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var instance = await dbContext.WorkflowInstances
            .Include(i => i.WorkflowVersion)
            .ThenInclude(v => v.WorkflowDefinition)
            .Include(i => i.CurrentStep)
            .Include(i => i.Tasks)
            .ThenInclude(t => t.WorkflowStep)
            .FirstOrDefaultAsync(i => i.Id == workflowInstanceId, cancellationToken)
            ?? throw new InvalidOperationException($"WORKFLOW_NOT_FOUND: Instance #{workflowInstanceId} not found.");

        var availableActions = await GetAvailableActionsAsync(instance.Id, instance.InitiatorUserId, user: null, cancellationToken: cancellationToken);

        var taskDtos = instance.Tasks.Select(t => new WorkflowTaskDto(
            Id: t.Id,
            WorkflowInstanceId: t.WorkflowInstanceId,
            StepCode: t.WorkflowStep.Code,
            StepName: t.WorkflowStep.Name,
            Status: t.Status,
            AssignedUserId: t.AssignedUserId,
            AssignedRoleId: t.AssignedRoleId,
            AssignedDepartmentId: t.AssignedDepartmentId,
            CreatedAt: t.CreatedAt,
            DueAt: t.DueAt,
            CompletedAt: t.CompletedAt
        )).ToList();

        return new WorkflowInstanceDto(
            Id: instance.Id,
            WorkflowCode: instance.WorkflowVersion.WorkflowDefinition.Code,
            WorkflowName: instance.WorkflowVersion.WorkflowDefinition.Name,
            VersionNumber: instance.WorkflowVersion.VersionNumber,
            EntityType: instance.EntityType,
            EntityId: instance.EntityId,
            CurrentStepCode: instance.CurrentStep.Code,
            CurrentStepName: instance.CurrentStep.Name,
            Status: instance.Status,
            InitiatorUserId: instance.InitiatorUserId,
            StartedAt: instance.StartedAt,
            CompletedAt: instance.CompletedAt,
            RowVersion: instance.RowVersion,
            Tasks: taskDtos,
            AvailableActions: availableActions.ToList());
    }

    private async Task CreateTaskForStepAsync(
        WorkflowInstance instance,
        WorkflowStep step,
        string initiatorUserId,
        CancellationToken cancellationToken)
    {
        var assignmentContext = new WorkflowAssignmentContext(instance, step, initiatorUserId);
        var assignment = await assignmentResolver.ResolveAsync(assignmentContext, cancellationToken);

        var task = new WorkflowTask
        {
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = step.Id,
            Status = WorkflowTaskStatus.Pending,
            AssignedUserId = assignment.AssignedUserId,
            AssignedRoleId = assignment.AssignedRoleId,
            AssignedDepartmentId = assignment.AssignedDepartmentId,
            CreatedAt = DateTimeOffset.UtcNow,
            DueAt = step.SlaMinutes.HasValue ? DateTimeOffset.UtcNow.AddMinutes(step.SlaMinutes.Value) : null
        };

        dbContext.WorkflowTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);

        await timelineService.AppendEventAsync(
            workflowInstanceId: instance.Id,
            workflowVersionId: instance.WorkflowVersionId,
            stepId: step.Id,
            eventType: "TASK_CREATED",
            actionCode: "ASSIGN",
            fromStepId: null,
            toStepId: step.Id,
            userId: assignment.AssignedUserId ?? initiatorUserId,
            displayName: assignment.AssignedUserId ?? initiatorUserId,
            comment: $"Assigned to user: {assignment.AssignedUserId}, role: {assignment.AssignedRoleId}",
            cancellationToken: cancellationToken);

        // Notify assigned user of new pending task
        if (notificationService != null && !string.IsNullOrWhiteSpace(assignment.AssignedUserId))
        {
            try
            {
                var defName = instance.WorkflowVersion?.WorkflowDefinition?.Name ?? "المسار الإداري";
                await notificationService.SendAsync(
                    userId: assignment.AssignedUserId,
                    title: $"مهمة إدارية جديدة: {step.Name}",
                    message: $"تم توجيه معاملة ({defName}) رقم #{instance.Id} إليك للمراجعة والمصادقة.",
                    type: NotificationType.Workflow,
                    linkUrl: "/workflows",
                    cancellationToken: cancellationToken);
            }
            catch { /* non-fatal */ }
        }
    }

    private static string GetActionLabel(string actionCode) => actionCode.ToUpperInvariant() switch
    {
        "SUBMIT" => "ارسال الطلب",
        "APPROVE" => "موافقة واعتماد",
        "REJECT" => "رفض الطلب",
        "RETURN" => "إعادة للتعديل",
        "CANCEL" => "إلغاء الطلب",
        _ => actionCode
    };
}
