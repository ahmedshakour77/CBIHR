using System.Security.Claims;

namespace API.HR.Node.Services.Audit;

public interface IAuditLogger
{
    Task LogAsync(
        string action,
        string employeeID,
        int? photoId,
        bool success,
        ClaimsPrincipal? user,
        string? ipAddress,
        string? userAgent,
        string? failureReason = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
