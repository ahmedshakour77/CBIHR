using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using API.HR.Node.Data;
using API.HR.Node.Data.Feedback;
using API.HR.Node.Services.Security;
using Microsoft.AspNetCore.Authorization;
using API.HR.Node.Security;
using API.HR.Node.Services.Storage;

namespace API.HR.Node.Services.Feedback;

public class EmployeeFeedbackService(
    ApplicationDbContext dbContext,
    IFileStorageService fileStorage,
    IFileMalwareScanner malwareScanner,
    API.HR.Node.Services.Features.ISystemFeatureService featureService,
    IAuthorizationService authorizationService,
    ILogger<EmployeeFeedbackService> logger) : IEmployeeFeedbackService
{
    public const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB limit
    public const int MinWordCount = 5;
    public const int MaxWordCount = 500;

    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var words = Regex.Split(text.Trim(), @"\s+");
        return words.Count(w => !string.IsNullOrWhiteSpace(w));
    }

    public async Task<FeedbackSubmissionResult> SubmitFeedbackAsync(
        FeedbackSubmissionDto dto,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 0. Feature Settings Validation (Admin Control Panel)
            var settings = await featureService.GetSettingsAsync(cancellationToken);
            if (!settings.EnableMyFeedback)
            {
                return new(false, null, string.IsNullOrWhiteSpace(settings.DisabledMessage)
                    ? "خدمة تقديم المشاركات متوقفة مؤقتاً بقرار الإدارة."
                    : settings.DisabledMessage);
            }

            var cleanType = (dto.FeedbackType ?? "فكرة").Trim();
            if (cleanType == "فكرة" && !settings.AllowIdeas)
            {
                return new(false, null, "تم إيقاف استقبال الأفكار والمقترحات مؤقتاً بقرار الإدارة.");
            }
            if (cleanType == "ملاحظة" && !settings.AllowNotes)
            {
                return new(false, null, "تم إيقاف استقبال ملاحظات العمل مؤقتاً بقرار الإدارة.");
            }
            if (cleanType == "استفسار" && !settings.AllowInquiries)
            {
                return new(false, null, "تم إيقاف استقبال الاستفسارات مؤقتاً بقرار الإدارة.");
            }
            if (cleanType == "شكوى" && !settings.AllowComplaints)
            {
                return new(false, null, "تم إيقاف استقبال الشكاوى مؤقتاً بقرار الإدارة.");
            }
            if (cleanType == "أخرى" && !settings.AllowOthers)
            {
                return new(false, null, "تم إيقاف استقبال هذا النوع من المشاركات مؤقتاً بقرار الإدارة.");
            }
            if (dto.FileSize.HasValue && dto.FileSize.Value > 0 && !settings.AllowAttachments)
            {
                return new(false, null, "تم إيقاف إرفاق الملفات مع المشاركات بقرار الإدارة.");
            }

            // 1. Validation
            var title = dto.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return new(false, null, "يرجى كتابة عنوان مختصر للمشاركة.");
            }

            if (title.Length > 200)
            {
                return new(false, null, "عنوان المشاركة يجب ألا يتجاوز 200 حرف.");
            }

            var description = dto.Description?.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                return new(false, null, "يرجى كتابة وصف توضيحي للمشاركة.");
            }

            var wordCount = CountWords(description);
            if (wordCount < MinWordCount)
            {
                return new(false, null, $"الوصف قصير جداً ({wordCount} كلمة). الحد الأدنى المطلوب هو {MinWordCount} كلمات لتوضيح الطلب.");
            }

            if (wordCount > MaxWordCount)
            {
                return new(false, null, $"الوصف يتجاوز الحد الأقصى المسموح ({wordCount} / {MaxWordCount} كلمة). يرجى اختصار الوصف.");
            }

            // Attachment validation
            if (dto.FileSize.HasValue && dto.FileSize.Value > MaxFileSizeBytes)
            {
                return new(false, null, "حجم الملف المرفق يتجاوز 2 ميغابايت (2 MB). يرجى اختيار ملف أصغر حجماً.");
            }

            // 2. Identify sender
            string? userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string? userEmail = user?.FindFirst(ClaimTypes.Email)?.Value ?? user?.Identity?.Name;
            string? submitterEmail = userEmail;
            int? employeeId = null;
            string? submitterName = user?.Identity?.Name;
            string? submitterJob = null;
            string? submitterDept = null;

            if (int.TryParse(user?.FindFirst("employee_id")?.Value, out var empIdClaim))
            {
                employeeId = empIdClaim;
            }

            // Look up employee record if exists
            Employee? emp = null;
            if (employeeId.HasValue)
            {
                emp = await dbContext.Employees
                    .Include(e => e.JobTitle)
                    .Include(e => e.Formation)
                    .FirstOrDefaultAsync(e => e.Id == employeeId.Value, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(userEmail))
            {
                var matchedEmail = await dbContext.EmployeeEmails
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Email == userEmail, cancellationToken);

                if (matchedEmail != null)
                {
                    emp = await dbContext.Employees
                        .Include(e => e.JobTitle)
                        .Include(e => e.Formation)
                        .FirstOrDefaultAsync(e => e.EmployeeID == matchedEmail.EmployeeID, cancellationToken);
                }
            }

            if (emp != null)
            {
                employeeId = emp.Id;
                submitterName = emp.Name;
                submitterJob = emp.JobTitle?.Name;
                submitterDept = emp.Formation?.Name;
            }

            // 3. Generate Reference Number
            var todayPrefix = $"FB-{DateTime.UtcNow:yyyyMMdd}";
            var todayCount = await dbContext.EmployeeFeedbacks
                .CountAsync(f => f.ReferenceNumber.StartsWith(todayPrefix), cancellationToken);
            var refNumber = $"{todayPrefix}-{(todayCount + 1):D4}";

            // 4. Handle file storage
            string? attachmentKey = null;
            string? safeFileName = null;
            long? attachmentSize = dto.FileSize;

            if (dto.FileStream != null && !string.IsNullOrWhiteSpace(dto.FileName))
            {
                safeFileName = Path.GetFileName(dto.FileName);
                var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
                var cleanBase = Path.GetFileNameWithoutExtension(safeFileName);
                if (cleanBase.Length > 40) cleanBase = cleanBase[..40];

                attachmentKey = $"feedback/{DateTime.UtcNow:yyyy}/{refNumber}_{cleanBase}{extension}";

                // Guard size during stream copy
                if (dto.FileStream.CanSeek)
                {
                    dto.FileStream.Position = 0;
                    attachmentSize = dto.FileStream.Length;
                    if (attachmentSize > MaxFileSizeBytes)
                    {
                        return new(false, null, "حجم الملف المرفق يتجاوز 2 ميغابايت (2 MB).");
                    }
                }

                var scan = await malwareScanner.ScanAsync(dto.FileStream, safeFileName, cancellationToken);
                if (scan.IsRejected())
                {
                    return new(false, null, "تعذر قبول الملف المرفق: لم يجتز الفحص الأمني.");
                }

                await fileStorage.SaveAsync(dto.FileStream, attachmentKey, cancellationToken);
            }

            // 5. Create Entity
            var feedback = new EmployeeFeedback
            {
                ReferenceNumber = refNumber,
                FeedbackType = string.IsNullOrWhiteSpace(dto.FeedbackType) ? "فكرة" : dto.FeedbackType.Trim(),
                Title = title,
                Description = description,
                WordCount = wordCount,
                EmployeeId = employeeId,
                UserId = userId,
                SubmitterName = submitterName ?? "موظف",
                SubmitterEmail = submitterEmail,
                SubmitterJobTitle = submitterJob,
                SubmitterDepartment = submitterDept,
                AttachmentFileName = safeFileName,
                AttachmentStorageKey = attachmentKey,
                AttachmentSizeBytes = attachmentSize,
                AttachmentContentType = dto.ContentType,
                Status = FeedbackStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.EmployeeFeedbacks.Add(feedback);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Feedback submitted successfully: Ref={ReferenceNumber}, Type={Type}, Sender={Sender}",
                refNumber, feedback.FeedbackType, feedback.SubmitterName);

            return new(true, refNumber, null, feedback);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit employee feedback.");
            return new(false, null, $"حدث خطأ غير متوقع أثناء إرسال المشاركة: {ex.Message}");
        }
    }

    public async Task<FeedbackPagedResult<EmployeeFeedback>> GetFeedbacksForAdminAsync(
        FeedbackFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.EmployeeFeedbacks
            .Include(f => f.Employee)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Type) && filter.Type != "all")
        {
            query = query.Where(f => f.FeedbackType == filter.Type);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(f => f.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim();
            query = query.Where(f =>
                f.ReferenceNumber.Contains(search) ||
                f.Title.Contains(search) ||
                f.Description.Contains(search) ||
                (f.SubmitterName != null && f.SubmitterName.Contains(search)) ||
                (f.Employee != null && (f.Employee.Name.Contains(search) || f.Employee.EmployeeID.ToString().Contains(search))));
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(f => f.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(f => f.CreatedAt <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new(items, totalCount, page, pageSize);
    }

    public async Task<FeedbackKpisDto> GetFeedbackKpisAsync(CancellationToken cancellationToken = default)
    {
        var list = await dbContext.EmployeeFeedbacks
            .Select(f => new { f.Status, f.FeedbackType })
            .ToListAsync(cancellationToken);

        return new FeedbackKpisDto(
            TotalCount: list.Count,
            PendingCount: list.Count(f => f.Status == FeedbackStatus.Pending),
            InProgressCount: list.Count(f => f.Status == FeedbackStatus.InProgress),
            ResolvedCount: list.Count(f => f.Status == FeedbackStatus.Resolved),
            RejectedCount: list.Count(f => f.Status == FeedbackStatus.Rejected),
            IdeasCount: list.Count(f => f.FeedbackType == "فكرة"),
            NotesCount: list.Count(f => f.FeedbackType == "ملاحظة"),
            InquiriesCount: list.Count(f => f.FeedbackType == "استفسار"),
            ComplaintsCount: list.Count(f => f.FeedbackType == "شكوى"),
            OthersCount: list.Count(f => f.FeedbackType == "أخرى")
        );
    }

    public async Task<IReadOnlyList<EmployeeFeedback>> GetFeedbacksForEmployeeAsync(
        int? employeeId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.EmployeeFeedbacks.AsNoTracking().AsQueryable();

        if (employeeId.HasValue && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(f => f.EmployeeId == employeeId.Value || f.UserId == userId);
        }
        else if (employeeId.HasValue)
        {
            query = query.Where(f => f.EmployeeId == employeeId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(f => f.UserId == userId);
        }
        else
        {
            return Array.Empty<EmployeeFeedback>();
        }

        return await query
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeFeedback?> GetFeedbackByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.EmployeeFeedbacks
            .Include(f => f.Employee)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<bool> UpdateFeedbackStatusAsync(
        int id,
        FeedbackStatus status,
        string? adminNotes,
        ClaimsPrincipal adminUser,
        CancellationToken cancellationToken = default)
    {
        // 1. Server-side Authorization Check: Caller MUST satisfy Feedback.Edit (Canonical)
        var authResult = await authorizationService.AuthorizeAsync(adminUser, null, new PermissionRequirement(Permissions.FeedbackEdit));
        if (!authResult.Succeeded)
        {
            logger.LogWarning("SECURITY ALERT: Unauthorized attempt to update feedback status without Feedback.Edit: FeedbackId={Id}, User={User}",
                id, adminUser.Identity?.Name ?? "UNKNOWN");
            return false;
        }

        var feedback = await dbContext.EmployeeFeedbacks.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (feedback == null)
        {
            return false;
        }

        feedback.Status = status;
        feedback.AdminNotes = adminNotes?.Trim();
        feedback.RespondedByUserId = adminUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        feedback.RespondedByUserName = adminUser.Identity?.Name ?? "الإدارة";
        feedback.RespondedAt = DateTimeOffset.UtcNow;
        feedback.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Feedback {Ref} status updated to {Status} by {User}",
            feedback.ReferenceNumber, status, feedback.RespondedByUserName);

        return true;
    }

    public async Task<(Stream? Stream, string FileName, string ContentType)?> GetAttachmentStreamAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var feedback = await dbContext.EmployeeFeedbacks.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (feedback == null || string.IsNullOrWhiteSpace(feedback.AttachmentStorageKey))
        {
            return null;
        }

        // Authorization check: Staff with Feedback.View (Canonical) OR the author of the feedback
        var authResult = await authorizationService.AuthorizeAsync(user, null, new PermissionRequirement(Permissions.FeedbackView));
        var isStaff = authResult.Succeeded;
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(user.FindFirst("employee_id")?.Value, out var empId);

        var isOwner = (userId != null && feedback.UserId == userId) || (empId > 0 && feedback.EmployeeId == empId);

        if (!isStaff && !isOwner)
        {
            logger.LogWarning("Unauthorized access attempt to feedback attachment: FeedbackId={Id}, User={User}", id, user.Identity?.Name);
            return null;
        }

        var stream = await fileStorage.OpenReadAsync(feedback.AttachmentStorageKey, cancellationToken);
        if (stream == null)
        {
            return null;
        }

        var contentType = feedback.AttachmentContentType ?? "application/octet-stream";
        var fileName = feedback.AttachmentFileName ?? "attachment";

        return (stream, fileName, contentType);
    }
}
