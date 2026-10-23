using System.Security.Claims;
using API.HR.Node.Data;

namespace API.HR.Node.Services.Audit;

public sealed class AuditLogger(
    ApplicationDbContext dbContext,
    ILogger<AuditLogger> logger)
    : IAuditLogger
{
    public async Task LogAsync(
        string action,
        string employeeID,
        int? photoId,
        bool success,
        ClaimsPrincipal? user,
        string? ipAddress,
        string? userAgent,
        string? failureReason = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? user?.FindFirst("sub")?.Value
                     ?? user?.Identity?.Name;

        var auditLog = new AuditLog
        {
            UserId = userId,
            EmployeeID = employeeID,
            PhotoId = photoId,
            Action = action,
            Timestamp = DateTimeOffset.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Success = success,
            FailureReason = failureReason,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")
        };

        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("AuditLog [{Action}] recorded for EmployeeID: {EmployeeID}, User: {UserId}, Success: {Success}",
            action, employeeID, userId ?? "Anonymous", success);
    }
}
