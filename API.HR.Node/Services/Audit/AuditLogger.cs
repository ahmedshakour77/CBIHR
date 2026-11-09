using System.Security.Claims;
using API.HR.Node.Data;

namespace API.HR.Node.Services.Audit;

public sealed class AuditLogger(
    ApplicationDbContext dbContext,
    ILogger<AuditLogger> logger,
    IUserActivityService? activityService = null)
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

        try
        {
            if (activityService != null)
            {
                var isAuth = action.Contains("LOGIN", StringComparison.OrdinalIgnoreCase) ||
                             action.Contains("LOGOUT", StringComparison.OrdinalIgnoreCase);

                activityService.RecordEvent(new UserActivityEvent
                {
                    TimestampUtc = auditLog.Timestamp,
                    SessionId = auditLog.CorrelationId ?? string.Empty,
                    UserId = userId ?? employeeID,
                    UserName = user?.Identity?.Name ?? userId ?? employeeID,
                    EmployeeId = employeeID,
                    UserRole = user?.IsInRole("Admin") == true ? "Admin" : null,
                    Category = isAuth ? ActivityCategories.Auth : ActivityCategories.Security,
                    ActionCode = action,
                    ActionTitle = FormatAuditActionTitle(action),
                    ElementLabel = employeeID,
                    ValueText = failureReason ?? userAgent,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Success = success
                });
            }
        }
        catch { }

        logger.LogInformation("AuditLog [{Action}] recorded for EmployeeID: {EmployeeID}, User: {UserId}, Success: {Success}",
            action, employeeID, userId ?? "Anonymous", success);
    }

    private static string FormatAuditActionTitle(string action) => action switch
    {
        "USER_LOGIN_SUCCESS" => "تسجيل دخول ناجح إلى النظام",
        "USER_LOGOUT" => "تسجيل خروج من النظام",
        "MAGIC_LINK_CREATED" => "توليد رابط دخول مؤقت (QR)",
        "MAGIC_LINK_LOGIN_SUCCESS" => "تسجيل دخول عبر الرابط المؤقت (QR)",
        "MAGIC_LINK_REVOKED" => "إبطال رابط دخول مؤقت",
        "FORCE_LOGOUT_USER" => "إنهاء جلسات مستخدم إجبارياً (Force Logout)",
        "RELINK_USER_EMPLOYEE" => "تغيير الموظف المرتبط بحساب مستخدم",
        "ProfilePhotoUploaded" => "رفع صورة شخصية لموظف",
        "ProfilePhotoReplaced" => "تحديث الصورة الشخصية لموظف",
        "ProfilePhotoDeleted" => "حذف الصورة الشخصية لموظف",
        "ProfilePhotoUploadFailed" => "فشل رفع صورة شخصية",
        _ => $"عملية أمنية / تدقيق: {action}"
    };
}
