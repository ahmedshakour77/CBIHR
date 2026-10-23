using API.HR.Node.Data;
using API.HR.Node.Data.Workflows;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Workflows;

public interface IWorkflowTimelineService
{
    Task<List<WorkflowTimelineItemDto>> GetTimelineAsync(
        long workflowInstanceId,
        CancellationToken cancellationToken = default);

    Task AppendEventAsync(
        long workflowInstanceId,
        long workflowVersionId,
        long? stepId,
        string eventType,
        string? actionCode,
        long? fromStepId,
        long? toStepId,
        string userId,
        string? displayName,
        string? comment,
        string? metadataJson = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
}

public class WorkflowTimelineService(ApplicationDbContext dbContext) : IWorkflowTimelineService
{
    public async Task<List<WorkflowTimelineItemDto>> GetTimelineAsync(
        long workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var events = await dbContext.WorkflowEvents
            .Where(e => e.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        // Fetch step names for display
        var stepIds = events.Where(e => e.StepId.HasValue).Select(e => e.StepId!.Value)
            .Concat(events.Where(e => e.FromStepId.HasValue).Select(e => e.FromStepId!.Value))
            .Concat(events.Where(e => e.ToStepId.HasValue).Select(e => e.ToStepId!.Value))
            .Distinct()
            .ToList();

        var steps = await dbContext.WorkflowSteps
            .Where(s => stepIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        return events.Select(e => new WorkflowTimelineItemDto(
            EventId: e.Id,
            Type: e.EventType,
            Title: FormatEventTitle(e, steps),
            StepCode: e.StepId.HasValue && steps.TryGetValue(e.StepId.Value, out var sName) ? sName : null,
            FromStep: e.FromStepId.HasValue && steps.TryGetValue(e.FromStepId.Value, out var fName) ? fName : null,
            ToStep: e.ToStepId.HasValue && steps.TryGetValue(e.ToStepId.Value, out var tName) ? tName : null,
            PerformedByUserId: e.PerformedByUserId,
            PerformedByDisplayName: e.PerformedByDisplayName ?? e.PerformedByUserId,
            Comment: e.Comment,
            OccurredAt: e.OccurredAt,
            MetadataJson: e.MetadataJson
        )).ToList();
    }

    public async Task AppendEventAsync(
        long workflowInstanceId,
        long workflowVersionId,
        long? stepId,
        string eventType,
        string? actionCode,
        long? fromStepId,
        long? toStepId,
        string userId,
        string? displayName,
        string? comment,
        string? metadataJson = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var evt = new WorkflowEvent
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowVersionId = workflowVersionId,
            StepId = stepId,
            EventType = eventType,
            ActionCode = actionCode,
            FromStepId = fromStepId,
            ToStepId = toStepId,
            PerformedByUserId = userId,
            PerformedByDisplayName = displayName,
            OccurredAt = DateTimeOffset.UtcNow,
            Comment = comment,
            MetadataJson = metadataJson,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        dbContext.WorkflowEvents.Add(evt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string FormatEventTitle(WorkflowEvent e, Dictionary<long, string> steps)
    {
        return e.EventType switch
        {
            "WORKFLOW_STARTED" => "بدء إجراء المسار الإداري",
            "STEP_ENTERED" => e.ToStepId.HasValue && steps.TryGetValue(e.ToStepId.Value, out var toName)
                ? $"الانتقال إلى مرحلة ({toName})"
                : "الانتقال لمرحلة جديدة",
            "STEP_COMPLETED" => "إكمال المرحلة الحالية",
            "APPROVED" => "موافقة واعتماد الطلب",
            "REJECTED" => "رفض الطلب",
            "RETURNED" => "إعادة الطلب للتعديل",
            "TASK_CREATED" => "إنشاء مهمة عمل سارية",
            "TASK_ASSIGNED" => "توجيه المهمة للمستفيد",
            "REASSIGNED" => "تحويل المهمة لمستخدم آخر",
            "DELEGATED" => "تفويض المهمة",
            "WORKFLOW_COMPLETED" => "إكتمال المسار الإداري بنجاح",
            "WORKFLOW_CANCELLED" => "إلغاء مسار العمل",
            _ => e.ActionCode ?? e.EventType
        };
    }
}
