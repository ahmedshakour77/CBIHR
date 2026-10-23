using API.HR.Node.Data;
using API.HR.Node.Data.Workflows;

namespace API.HR.Node.Services.Workflows;

public class AppreciationLetterWorkflowHandler(
    ApplicationDbContext dbContext,
    ILogger<AppreciationLetterWorkflowHandler> logger) : IWorkflowActionHandler
{
    public string SupportedEntityType => "AppreciationLetter";

    public Task OnBeforeActionAsync(
        WorkflowActionHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Executing workflow action {ActionCode} for AppreciationLetter #{EntityId} by user {UserId}.",
            context.ActionCode, context.Instance.EntityId, context.UserId);

        return Task.CompletedTask;
    }

    public async Task OnAfterActionAsync(
        WorkflowActionHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        if (int.TryParse(context.Instance.EntityId, out var letterId))
        {
            var letter = await dbContext.AppreciationLetters.FindAsync([letterId], cancellationToken);
            if (letter != null)
            {
                // Update domain notes with workflow audit stamp
                letter.Notes = $"[Workflow #{context.Instance.Id} - {context.ActionCode} at {DateTime.UtcNow:yyyy-MM-dd HH:mm} by {context.UserId}] {(string.IsNullOrWhiteSpace(context.Comment) ? "" : "Reason: " + context.Comment)}";
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
