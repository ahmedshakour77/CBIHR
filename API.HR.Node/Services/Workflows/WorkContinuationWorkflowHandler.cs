using System.Text.Json;
using API.HR.Node.Data;
using API.HR.Node.Data.Workflows;

namespace API.HR.Node.Services.Workflows;

public class WorkContinuationWorkflowHandler(
    ApplicationDbContext dbContext,
    ILogger<WorkContinuationWorkflowHandler> logger) : IWorkflowActionHandler
{
    public string SupportedEntityType => "WorkContinuationRequest";

    public Task OnBeforeActionAsync(
        WorkflowActionHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[WorkContinuation] Action {ActionCode} starting for Request #{EntityId} at step {StepCode} by user {UserId}.",
            context.ActionCode, context.Instance.EntityId, context.CurrentStep.Code, context.UserId);

        return Task.CompletedTask;
    }

    public async Task OnAfterActionAsync(
        WorkflowActionHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        // If workflow completed (letter issued), send push notification to employee's phone
        if (context.Transition.ToStep.IsFinal && context.Transition.ToStep.Code == "COMPLETED")
        {
            logger.LogInformation(
                "🎉 [WorkContinuation] Letter issued successfully for Request #{EntityId}. Queuing mobile notification for initiator {InitiatorUserId}.",
                context.Instance.EntityId, context.Instance.InitiatorUserId);

            // Queue Mobile Push Notification via Outbox
            var outboxPayload = new
            {
                Title = "صدور كتاب تأييد الاستمرار بالعمل 📄",
                Message = $"تمت المصادقة وإصدار كتاب تأييد الاستمرار بالعمل الخاص برقم المعاملة #{context.Instance.EntityId} بنجاح. يمكنك الآن الاطلاع عليه وتحميله.",
                InitiatorUserId = context.Instance.InitiatorUserId,
                WorkflowInstanceId = context.Instance.Id,
                IssuedAt = DateTimeOffset.UtcNow
            };

            dbContext.WorkflowOutboxMessages.Add(new WorkflowOutboxMessage
            {
                MessageType = "MOBILE_PUSH_NOTIFICATION",
                PayloadJson = JsonSerializer.Serialize(outboxPayload),
                OccurredAt = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
