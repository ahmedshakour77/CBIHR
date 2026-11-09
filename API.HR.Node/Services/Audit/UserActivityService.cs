using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace API.HR.Node.Services.Audit;

public static class ActivityCategories
{
    public const string Auth = "Auth";               // تسجيل الدخول والخروج
    public const string Navigation = "Navigation";   // التنقل بين الصفحات ومدة البقاء
    public const string Dropdown = "Dropdown";       // تغيير القوائم المنسدلة
    public const string Search = "Search";           // الكتابة في حقول البحث والإدخال
    public const string Click = "Click";             // نقر الأزرار والتبويبات
    public const string Security = "Security";       // العمليات الأمنية وتغييرات البيانات
}

public sealed record UserActivityEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public string SessionId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string? EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string? UserRole { get; init; }
    public string Category { get; init; } = ActivityCategories.Click;
    public string ActionCode { get; init; } = string.Empty;
    public string ActionTitle { get; init; } = string.Empty;
    public string? PagePath { get; init; }
    public string? PageTitle { get; init; }
    public string? ElementLabel { get; init; }
    public string? ValueText { get; init; }
    public int? DurationSeconds { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public bool Success { get; init; } = true;
}

public sealed record ClientActivityBatchItem(
    string? Category,
    string? ActionCode,
    string? ActionTitle,
    string? PagePath,
    string? PageTitle,
    string? ElementLabel,
    string? ValueText,
    int? DurationSeconds,
    string? SessionId);

public sealed record UserActivitySummaryDto(
    string UserId,
    string UserName,
    string? EmployeeId,
    string? EmployeeName,
    string? UserRole,
    int TotalEvents,
    int LoginCount,
    int PageViewCount,
    int SearchCount,
    int DropdownCount,
    int ClickCount,
    int SecurityCount,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    string? LastPagePath,
    string? LastActionTitle,
    bool IsOnlineNow);

public interface IUserActivityService
{
    void RecordEvent(UserActivityEvent evt);

    void RecordFromPrincipal(
        ClaimsPrincipal? principal,
        string category,
        string actionCode,
        string actionTitle,
        string? pagePath = null,
        string? pageTitle = null,
        string? elementLabel = null,
        string? valueText = null,
        int? durationSeconds = null,
        string? sessionId = null,
        string? ipAddress = null,
        string? userAgent = null,
        bool success = true);

    IReadOnlyList<UserActivityEvent> GetRecentEvents(
        DateTimeOffset? fromUtc = null,
        string? userFilter = null,
        string? categoryFilter = null,
        string? searchQuery = null,
        int maxCount = 1000);

    IReadOnlyList<UserActivitySummaryDto> GetUserSummaries(DateTimeOffset? fromUtc = null);

    static string ResolveArabicPageTitle(string? path) => UserActivityService.GetArabicPageTitle(path);
}

public sealed class UserActivityService : IUserActivityService, IHostedService, IDisposable
{
    private const int MaxMemoryEvents = 25_000;
    private readonly string _logsFolder;
    private readonly ILogger<UserActivityService> _logger;
    private readonly Channel<UserActivityEvent> _writeChannel;
    private readonly ConcurrentQueue<UserActivityEvent> _memoryEvents = new();
    private int _memoryCount;
    private CancellationTokenSource? _cts;
    private Task? _writerTask;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public UserActivityService(IWebHostEnvironment env, ILogger<UserActivityService> logger)
    {
        _logger = logger;
        _logsFolder = Path.Combine(env.ContentRootPath, "App_Data", "ActivityLogs");
        Directory.CreateDirectory(_logsFolder);

        _writeChannel = Channel.CreateBounded<UserActivityEvent>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        LoadRecentFilesIntoMemory();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _writerTask = Task.Run(() => ProcessWriteQueueAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _writeChannel.Writer.TryComplete();
        if (_cts != null)
        {
            try { _cts.Cancel(); } catch { }
        }
        if (_writerTask != null)
        {
            try { await _writerTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken); } catch { }
        }
    }

    public void RecordEvent(UserActivityEvent evt)
    {
        if (string.IsNullOrWhiteSpace(evt.UserId) && string.IsNullOrWhiteSpace(evt.UserName))
        {
            return;
        }

        EnqueueMemory(evt);
        _writeChannel.Writer.TryWrite(evt);
    }

    public void RecordFromPrincipal(
        ClaimsPrincipal? principal,
        string category,
        string actionCode,
        string actionTitle,
        string? pagePath = null,
        string? pageTitle = null,
        string? elementLabel = null,
        string? valueText = null,
        int? durationSeconds = null,
        string? sessionId = null,
        string? ipAddress = null,
        string? userAgent = null,
        bool success = true)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? principal.FindFirst("sub")?.Value
                     ?? principal.Identity?.Name
                     ?? "unknown";

        var userName = principal.Identity?.Name
                       ?? principal.FindFirst(ClaimTypes.Email)?.Value
                       ?? userId;

        var empId = principal.FindFirst("employee_id")?.Value
                    ?? principal.FindFirst("EmployeeID")?.Value;

        var empName = principal.FindFirst("employee_name")?.Value
                      ?? principal.FindFirst("full_name")?.Value;

        var roles = principal.FindAll(ClaimTypes.Role).Select(r => r.Value)
            .Concat(principal.FindAll("role").Select(r => r.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var cleanPath = NormalizePath(pagePath);
        var resolvedPageTitle = !string.IsNullOrWhiteSpace(pageTitle)
            ? pageTitle
            : GetArabicPageTitle(cleanPath);

        var evt = new UserActivityEvent
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            SessionId = !string.IsNullOrWhiteSpace(sessionId) ? sessionId : $"{userId}_{DateTime.UtcNow:yyyyMMdd}",
            UserId = userId,
            UserName = userName,
            EmployeeId = empId,
            EmployeeName = empName,
            UserRole = roles.Count > 0 ? string.Join(", ", roles) : "Employee",
            Category = category,
            ActionCode = actionCode,
            ActionTitle = actionTitle,
            PagePath = cleanPath,
            PageTitle = resolvedPageTitle,
            ElementLabel = Truncate(elementLabel, 200),
            ValueText = Truncate(valueText, 400),
            DurationSeconds = durationSeconds,
            IpAddress = ipAddress,
            UserAgent = Truncate(userAgent, 300),
            Success = success
        };

        RecordEvent(evt);
    }

    public IReadOnlyList<UserActivityEvent> GetRecentEvents(
        DateTimeOffset? fromUtc = null,
        string? userFilter = null,
        string? categoryFilter = null,
        string? searchQuery = null,
        int maxCount = 1000)
    {
        var query = _memoryEvents.AsEnumerable();

        if (fromUtc.HasValue)
        {
            query = query.Where(e => e.TimestampUtc >= fromUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(userFilter) && !string.Equals(userFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e =>
                string.Equals(e.UserId, userFilter, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.UserName, userFilter, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.EmployeeId, userFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(categoryFilter) && !string.Equals(categoryFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e => string.Equals(e.Category, categoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var q = searchQuery.Trim();
            query = query.Where(e =>
                (e.UserName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.EmployeeName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.EmployeeId?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.ActionTitle?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.PageTitle?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.PagePath?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.ElementLabel?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.ValueText?.Contains(q, StringComparison.OrdinalIgnoreCase) == true));
        }

        return query
            .OrderByDescending(e => e.TimestampUtc)
            .Take(maxCount)
            .ToList();
    }

    public IReadOnlyList<UserActivitySummaryDto> GetUserSummaries(DateTimeOffset? fromUtc = null)
    {
        var now = DateTimeOffset.UtcNow;
        var onlineCutoff = now.AddMinutes(-15);

        var query = _memoryEvents.AsEnumerable();
        if (fromUtc.HasValue)
        {
            query = query.Where(e => e.TimestampUtc >= fromUtc.Value);
        }

        return query
            .GroupBy(e => e.UserName.ToLowerInvariant())
            .Select(g =>
            {
                var ordered = g.OrderByDescending(x => x.TimestampUtc).ToList();
                var latest = ordered[0];
                var first = ordered[^1];
                var isOnline = latest.TimestampUtc >= onlineCutoff &&
                               !string.Equals(latest.ActionCode, "USER_LOGOUT", StringComparison.OrdinalIgnoreCase);

                return new UserActivitySummaryDto(
                    UserId: latest.UserId,
                    UserName: latest.UserName,
                    EmployeeId: ordered.Select(x => x.EmployeeId).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                    EmployeeName: ordered.Select(x => x.EmployeeName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                    UserRole: ordered.Select(x => x.UserRole).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                    TotalEvents: ordered.Count,
                    LoginCount: ordered.Count(x => x.Category == ActivityCategories.Auth && x.ActionCode.Contains("LOGIN", StringComparison.OrdinalIgnoreCase)),
                    PageViewCount: ordered.Count(x => x.Category == ActivityCategories.Navigation),
                    SearchCount: ordered.Count(x => x.Category == ActivityCategories.Search),
                    DropdownCount: ordered.Count(x => x.Category == ActivityCategories.Dropdown),
                    ClickCount: ordered.Count(x => x.Category == ActivityCategories.Click),
                    SecurityCount: ordered.Count(x => x.Category == ActivityCategories.Security),
                    FirstSeenUtc: first.TimestampUtc,
                    LastSeenUtc: latest.TimestampUtc,
                    LastPagePath: latest.PageTitle ?? latest.PagePath,
                    LastActionTitle: latest.ActionTitle,
                    IsOnlineNow: isOnline);
            })
            .OrderByDescending(u => u.IsOnlineNow)
            .ThenByDescending(u => u.LastSeenUtc)
            .ToList();
    }

    private void EnqueueMemory(UserActivityEvent evt)
    {
        _memoryEvents.Enqueue(evt);
        var count = Interlocked.Increment(ref _memoryCount);
        while (count > MaxMemoryEvents && _memoryEvents.TryDequeue(out _))
        {
            count = Interlocked.Decrement(ref _memoryCount);
        }
    }

    private async Task ProcessWriteQueueAsync(CancellationToken ct)
    {
        var batch = new List<UserActivityEvent>(64);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await _writeChannel.Reader.WaitToReadAsync(ct))
                {
                    break;
                }

                while (_writeChannel.Reader.TryRead(out var item))
                {
                    batch.Add(item);
                    if (batch.Count >= 64)
                    {
                        break;
                    }
                }

                if (batch.Count > 0)
                {
                    await FlushBatchToDiskAsync(batch, ct);
                    batch.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write user activity batch to disk.");
                batch.Clear();
                await Task.Delay(1000, ct);
            }
        }

        // Flush remaining items on shutdown
        while (_writeChannel.Reader.TryRead(out var remaining))
        {
            batch.Add(remaining);
        }
        if (batch.Count > 0)
        {
            try { await FlushBatchToDiskAsync(batch, CancellationToken.None); } catch { }
        }
    }

    private async Task FlushBatchToDiskAsync(List<UserActivityEvent> batch, CancellationToken ct)
    {
        var groupedByDay = batch.GroupBy(e => e.TimestampUtc.UtcDateTime.ToString("yyyy-MM-dd"));
        foreach (var dayGroup in groupedByDay)
        {
            var filePath = Path.Combine(_logsFolder, $"activity-{dayGroup.Key}.jsonl");
            var sb = new StringBuilder();
            foreach (var evt in dayGroup)
            {
                sb.AppendLine(JsonSerializer.Serialize(evt, JsonOptions));
            }
            await File.AppendAllTextAsync(filePath, sb.ToString(), Encoding.UTF8, ct);
        }
    }

    private void LoadRecentFilesIntoMemory()
    {
        try
        {
            var files = Directory.GetFiles(_logsFolder, "activity-*.jsonl")
                .OrderByDescending(f => f)
                .Take(7)
                .OrderBy(f => f)
                .ToList();

            foreach (var file in files)
            {
                foreach (var line in File.ReadLines(file, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var evt = JsonSerializer.Deserialize<UserActivityEvent>(line, JsonOptions);
                        if (evt != null)
                        {
                            EnqueueMemory(evt);
                        }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to preload historical user activity files.");
        }
    }

    public static string NormalizePath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return "/";
        var p = rawPath.Trim();
        if (Uri.TryCreate(p, UriKind.Absolute, out var uri))
        {
            p = uri.PathAndQuery;
        }
        if (!p.StartsWith('/')) p = "/" + p;
        return p;
    }

    public static string GetArabicPageTitle(string? path)
    {
        var clean = NormalizePath(path).Split('?')[0].Trim('/').ToLowerInvariant();
        if (string.IsNullOrEmpty(clean)) return "الرئيسية ولوحة المؤشرات";
        if (clean.StartsWith("employees/")) return $"ملف الموظف ({clean["employees/".Length..]})";

        return clean switch
        {
            "portal" => "ملفي الوظيفي",
            "my-feedback" => "مشاركاتي",
            "formation-dashboard" => "لوحة مؤشرات التشكيل الإداري",
            "reports-slider" => "التقارير الإحصائية",
            "general-map" => "الخريطة العامة (خارطة العراق)",
            "canva-reports" => "تقارير Canva الاحترافية",
            "employees" => "سجلات الموظفين",
            "leaves" => "الإجازات والأرصدة",
            "appreciation-letters" => "كتب الشكر والتقدير",
            "courses" => "الدورات التدريبية",
            "admin/feedback" => "تواصل ومشاركات الموظفين",
            "employees/contacts" => "دليل اتصالات الموظفين",
            "employees/gallery" => "معرض صور الموظفين",
            "admin/vehicles" => "عجلات الموظفين",
            "admin/residences" => "عناوين سكن الموظفين",
            "admin/birthdays" => "أعياد ميلاد الموظفين",
            "admin/national-cards" => "البطاقات الوطنية الموحدة",
            "admin/passports" => "جوازات سفر الموظفين",
            "org-hierarchy" => "الهيكل التنظيمي",
            "job-hierarchy" => "الهيكل الوظيفي الشجري",
            "admin/job-classifications" => "العناوين والعوائل الوظيفية",
            "jobtitles" => "استحقاق العناوين والترقيات",
            "salary-scale" => "السلم الوظيفي",
            "admin/salaries" => "الرواتب الشهرية والتشكيلات",
            "workflows" => "المسارات الإدارية",
            "data-import" => "مركز استيراد البيانات (Excel)",
            "admin/lookups" => "إدارة القوائم المنسدلة والترميز",
            "users" => "المستخدمون والأدوار والصلاحيات",
            "user-devices" => "الأجهزة المقترنة",
            "sessions" => "الجلسات السارية",
            "passkeys" => "مفاتيح المرور (Passkeys)",
            "audit" => "سجل التدقيق الأمني",
            "admin/user-analytics" => "تحليلات ومراقبة نشاط المستخدمين",
            "admin/features" => "إدارة الميزات والنظام",
            "design-system" => "نظام التصميم والمكونات",
            "account/login" => "صفحة تسجيل الدخول",
            "account/magiclogin" => "الدخول السريع المؤقت (QR)",
            _ => "/" + clean
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}

