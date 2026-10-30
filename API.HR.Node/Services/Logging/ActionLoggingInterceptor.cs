using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace API.HR.Node.Services.Logging;

// Records EF save operations in the application log without storing property values.
public sealed class ActionLoggingInterceptor(
    ILogger<ActionLoggingInterceptor> logger,
    IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    private readonly ConcurrentDictionary<DbContext, PendingSave> _pending = new();

    private void Capture(DbContext? context)
    {
        if (context is null) return;
        var http = httpContextAccessor.HttpContext;
        var user = http?.User;
        var actor = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
        var changes = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new Change(e, e.State.ToString(), e.State == EntityState.Modified
                ? string.Join(",", e.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name))
                : ""))
            .ToArray();
        _pending[context] = new PendingSave(actor ?? "unknown", Activity.Current?.TraceId.ToString() ?? http?.TraceIdentifier ?? "none", changes);
    }

    private void Complete(DbContext? context, string outcome)
    {
        if (context is null || !_pending.TryRemove(context, out var save)) return;
        foreach (var change in save.Changes)
        {
            // Only numeric and GUID keys are recorded; string keys may contain personal data or secrets.
            var key = change.Entry.Properties.Where(p => p.Metadata.IsPrimaryKey())
                .Select(p => p.CurrentValue)
                .Select(value => value is byte or short or int or long or Guid ? value.ToString() : null)
                .Where(value => value is not null);
            logger.LogInformation("Action {Outcome}: {Operation} {EntityType} Key={EntityKey} Fields={Fields} Actor={Actor} Trace={TraceId}",
                outcome, change.Operation, change.Entry.Metadata.ClrType.Name, string.Join(",", key), change.Fields, save.Actor, save.TraceId);
        }
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    { Capture(eventData.Context); return result; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Capture(eventData.Context); return ValueTask.FromResult(result); }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    { Complete(eventData.Context, "saved"); return result; }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    { Complete(eventData.Context, "saved"); return ValueTask.FromResult(result); }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Complete(eventData.Context, "failed");
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    { Complete(eventData.Context, "failed"); return Task.CompletedTask; }
    public override void SaveChangesCanceled(DbContextEventData eventData) => Complete(eventData.Context, "canceled");
    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    { Complete(eventData.Context, "canceled"); return Task.CompletedTask; }
    public override InterceptionResult ThrowingConcurrencyException(ConcurrencyExceptionEventData eventData, InterceptionResult result)
    { Complete(eventData.Context, "concurrency conflict"); return result; }
    public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(ConcurrencyExceptionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    { Complete(eventData.Context, "concurrency conflict"); return ValueTask.FromResult(result); }

    private sealed record Change(EntityEntry Entry, string Operation, string Fields);
    private sealed record PendingSave(string Actor, string TraceId, Change[] Changes);
}
