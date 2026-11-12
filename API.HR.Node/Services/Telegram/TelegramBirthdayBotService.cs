using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using API.HR.Node.Data;
using API.HR.Node.Services.Common;
using API.HR.Node.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Telegram;

public sealed class TelegramAuthorizedChat
{
    public long ChatId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Username { get; set; }
    public DateTimeOffset AddedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool ReceiveDailyBroadcast { get; set; } = true;
}

public sealed class TelegramPendingChat
{
    public long ChatId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Username { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TelegramBirthdayBotConfig
{
    public string BotToken { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool AllowAllUsers { get; set; }
    public string AccessPin { get; set; } = "1234";
    public bool SendPhotosAutomatically { get; set; } = true;
    public bool DailyAutoBroadcastEnabled { get; set; } = true;
    public int DailyBroadcastHourLocal { get; set; } = 8;
    public string? LastDailyBroadcastDate { get; set; }
    public List<TelegramAuthorizedChat> AuthorizedChats { get; set; } = [];
    public List<TelegramPendingChat> PendingChats { get; set; } = [];
}

public sealed record BotEmployeeBirthdayCard(
    int Id,
    string EmployeeID,
    string Name,
    DateOnly BirthDate,
    string? JobTitle,
    string? Formation,
    string? Gender,
    int Age,
    int DaysUntilNext,
    DateOnly NextBirthdayDate,
    ZodiacSignInfo Zodiac,
    GenerationInfo Generation,
    string? StorageKey,
    string? ThumbnailStorageKey,
    string? PhotoContentType);

public interface ITelegramBirthdayBotService
{
    TelegramBirthdayBotConfig GetConfig();
    Task SaveConfigAsync(TelegramBirthdayBotConfig updated);
    string? BotUsername { get; }
    bool IsConnected { get; }
    string? LastError { get; }
    DateTimeOffset? LastPollUtc { get; }
    Task<(bool Ok, string Message)> TestConnectionAsync(string? candidateToken = null);
    Task<(int SentCount, string Message)> BroadcastTodayBirthdaysNowAsync();
    Task<(bool Ok, string Message)> SendEmployeeBirthdayToChatsAsync(string employeeId);
}

public sealed class TelegramBirthdayBotService : BackgroundService, ITelegramBirthdayBotService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFileStorageService _storageService;
    private readonly ILogger<TelegramBirthdayBotService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _configFilePath;
    private readonly SemaphoreSlim _configLock = new(1, 1);
    private readonly SemaphoreSlim _pollLock = new(1, 1);

    private TelegramBirthdayBotConfig _config = new();
    private long _lastUpdateOffset;
    private int _consecutiveConflicts;
    private CancellationTokenSource? _pollingRestartCts;

    public string? BotUsername { get; private set; }
    public bool IsConnected { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? LastPollUtc { get; private set; }

    public TelegramBirthdayBotService(
        IServiceScopeFactory scopeFactory,
        IFileStorageService storageService,
        IHostEnvironment env,
        ILogger<TelegramBirthdayBotService> logger)
    {
        _scopeFactory = scopeFactory;
        _storageService = storageService;
        _logger = logger;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };

        var appDataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataDir);
        _configFilePath = Path.Combine(appDataDir, "telegram-birthday-bot.json");

        LoadConfigFromDisk();
    }

    public TelegramBirthdayBotConfig GetConfig()
    {
        var json = JsonSerializer.Serialize(_config);
        return JsonSerializer.Deserialize<TelegramBirthdayBotConfig>(json) ?? new TelegramBirthdayBotConfig();
    }

    public async Task SaveConfigAsync(TelegramBirthdayBotConfig updated)
    {
        await _configLock.WaitAsync();
        try
        {
            updated.BotToken = (updated.BotToken ?? string.Empty).Trim();
            updated.AccessPin = string.IsNullOrWhiteSpace(updated.AccessPin) ? "1234" : updated.AccessPin.Trim();
            updated.DailyBroadcastHourLocal = Math.Clamp(updated.DailyBroadcastHourLocal, 0, 23);

            var tokenChanged = !string.Equals(_config.BotToken, updated.BotToken, StringComparison.Ordinal);
            var enabledChanged = _config.IsEnabled != updated.IsEnabled;

            _config = updated;
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configFilePath, json, Encoding.UTF8);

            // Only interrupt the polling loop if the token changed or the bot was toggled off/on.
            // Interrupting an active getUpdates request unnecessarily causes a 409 Conflict with itself!
            if (tokenChanged || enabledChanged)
            {
                try { _pollingRestartCts?.Cancel(); } catch { }
            }
        }
        finally
        {
            _configLock.Release();
        }
    }

    private void LoadConfigFromDisk()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var raw = File.ReadAllText(_configFilePath, Encoding.UTF8);
                var parsed = JsonSerializer.Deserialize<TelegramBirthdayBotConfig>(raw);
                if (parsed is not null)
                {
                    _config = parsed;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load telegram-birthday-bot.json; using defaults.");
        }
    }

    public async Task<(bool Ok, string Message)> TestConnectionAsync(string? candidateToken = null)
    {
        var token = (candidateToken ?? _config.BotToken ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "يرجى إدخال رمز البوت (Bot Token) أولاً.");
        }

        try
        {
            var url = $"https://api.telegram.org/bot{token}/getMe";
            using var resp = await _httpClient.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();
            var node = JsonNode.Parse(body);

            if (resp.IsSuccessStatusCode && node?["ok"]?.GetValue<bool>() == true)
            {
                var result = node["result"];
                var username = result?["username"]?.GetValue<string>();
                var firstName = result?["first_name"]?.GetValue<string>();
                BotUsername = username;
                IsConnected = true;
                LastError = null;
                _consecutiveConflicts = 0;

                // Ensure any stale webhook is removed so getUpdates never conflicts with a webhook
                try
                {
                    await _httpClient.GetAsync($"https://api.telegram.org/bot{token}/deleteWebhook?drop_pending_updates=false");
                }
                catch { }

                // Register bot commands menu in Telegram
                await SetBotCommandsAsync(token);

                return (true, $"تم الاتصال بنجاح بالبوت: {firstName} (@{username})");
            }

            var desc = node?["description"]?.GetValue<string>() ?? "رمز البوت غير صالح.";
            IsConnected = false;
            LastError = desc;
            return (false, $"فشل الاتصال بتلكرام: {desc}");
        }
        catch (Exception ex)
        {
            IsConnected = false;
            LastError = ex.Message;
            return (false, $"تعذر الوصول إلى خوادم تيليجرام: {ex.Message}");
        }
    }

    private async Task SetBotCommandsAsync(string token)
    {
        try
        {
            var commands = new object[]
            {
                new { command = "start", description = "القائمة الرئيسية لأعياد الميلاد" },
                new { command = "today", description = "مواليد اليوم مع الصور والتفاصيل" },
                new { command = "tomorrow", description = "مواليد الغد" },
                new { command = "week", description = "مواليد خلال ٧ أيام" },
                new { command = "month", description = "مواليد الشهر الحالي" },
                new { command = "nextmonth", description = "مواليد الشهر القادم" },
                new { command = "search", description = "بحث بالاسم أو التشكيل أو التاريخ" }
            };

            var payload = JsonSerializer.Serialize(new { commands });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync($"https://api.telegram.org/bot{token}/setMyCommands", content);
        }
        catch
        {
            // Non-critical
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_config.IsEnabled || string.IsNullOrWhiteSpace(_config.BotToken))
                {
                    IsConnected = false;
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }

                _pollingRestartCts?.Dispose();
                _pollingRestartCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var loopToken = _pollingRestartCts.Token;

                var test = await TestConnectionAsync(_config.BotToken);
                if (!test.Ok)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), loopToken);
                    continue;
                }

                while (!loopToken.IsCancellationRequested && _config.IsEnabled && !string.IsNullOrWhiteSpace(_config.BotToken))
                {
                    await CheckAndRunDailyBroadcastAsync(loopToken);
                    await PollTelegramUpdatesOnceAsync(_config.BotToken, loopToken);
                }
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // Config updated — restart loop immediately
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                IsConnected = false;
                _logger.LogWarning(ex, "Telegram birthday bot polling error.");
                await Task.Delay(TimeSpan.FromSeconds(6), stoppingToken);
            }
        }
    }

    private async Task CheckAndRunDailyBroadcastAsync(CancellationToken ct)
    {
        if (!_config.DailyAutoBroadcastEnabled) return;

        var nowLocal = DateTime.Now;
        var todayKey = nowLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (nowLocal.Hour == _config.DailyBroadcastHourLocal &&
            !string.Equals(_config.LastDailyBroadcastDate, todayKey, StringComparison.Ordinal))
        {
            _config.LastDailyBroadcastDate = todayKey;
            await SaveConfigAsync(_config);
            await BroadcastTodayBirthdaysNowAsync();
        }
    }

    private async Task PollTelegramUpdatesOnceAsync(string token, CancellationToken ct)
    {
        if (!await _pollLock.WaitAsync(0, ct))
        {
            return;
        }

        try
        {
            var url = $"https://api.telegram.org/bot{token}/getUpdates?offset={_lastUpdateOffset + 1}&timeout=4&allowed_updates=%5B%22message%22,%22callback_query%22%5D";
            using var resp = await _httpClient.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            LastPollUtc = DateTimeOffset.UtcNow;

            var root = JsonNode.Parse(body);
            if (root?["ok"]?.GetValue<bool>() != true)
            {
                var desc = root?["description"]?.GetValue<string>() ?? "Telegram getUpdates error";
                var isConflict = (int)resp.StatusCode == 409 || desc.Contains("Conflict", StringComparison.OrdinalIgnoreCase);

                if (isConflict)
                {
                    _consecutiveConflicts++;
                    // Transient overlap (e.g., server hot-restart or previous poll closing): keep IsConnected = true and retry quietly
                    if (_consecutiveConflicts <= 4)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), ct);
                        return;
                    }

                    // Persistent conflict: another instance (e.g., IIS + Visual Studio at the same time) is polling the same token
                    IsConnected = true; // Bot token itself is valid; keep trying automatically
                    LastError = "تنبيه: توجد نسخة ثانية من النظام تعمل حالياً بنفس رمز البوت (مثلاً نسخة السيرفر المنشورة مع نسخة Visual Studio). سيستمر البوت بالعمل تلقائياً فور إغلاق النسخة الأخرى.";
                    await Task.Delay(TimeSpan.FromSeconds(4), ct);
                    return;
                }

                IsConnected = false;
                LastError = desc;
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return;
            }

            _consecutiveConflicts = 0;
            IsConnected = true;
            LastError = null;

            var updates = root["result"]?.AsArray();
            if (updates is null || updates.Count == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
                return;
            }

            foreach (var upd in updates)
            {
                if (upd is null) continue;
                var updateId = upd["update_id"]?.GetValue<long>() ?? 0;
                if (updateId > _lastUpdateOffset)
                {
                    _lastUpdateOffset = updateId;
                }

                try
                {
                    if (upd["message"] is JsonNode msgNode)
                    {
                        await HandleIncomingMessageAsync(token, msgNode, ct);
                    }
                    else if (upd["callback_query"] is JsonNode cbNode)
                    {
                        await HandleCallbackQueryAsync(token, cbNode, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error handling Telegram update {UpdateId}", updateId);
                }
            }
        }
        finally
        {
            _pollLock.Release();
        }
    }

    private bool IsChatAuthorized(long chatId)
    {
        if (_config.AllowAllUsers) return true;
        return _config.AuthorizedChats.Any(c => c.ChatId == chatId);
    }

    private async Task RegisterOrAuthorizeChatAsync(long chatId, string title, string? username, bool authorizeNow)
    {
        await _configLock.WaitAsync();
        try
        {
            if (authorizeNow)
            {
                _config.PendingChats.RemoveAll(p => p.ChatId == chatId);
                if (_config.AuthorizedChats.All(c => c.ChatId != chatId))
                {
                    _config.AuthorizedChats.Add(new TelegramAuthorizedChat
                    {
                        ChatId = chatId,
                        Title = title,
                        Username = username,
                        AddedAtUtc = DateTimeOffset.UtcNow,
                        ReceiveDailyBroadcast = true
                    });
                }
            }
            else
            {
                if (_config.AuthorizedChats.All(c => c.ChatId != chatId) &&
                    _config.PendingChats.All(p => p.ChatId != chatId))
                {
                    _config.PendingChats.Add(new TelegramPendingChat
                    {
                        ChatId = chatId,
                        Title = title,
                        Username = username,
                        RequestedAtUtc = DateTimeOffset.UtcNow
                    });
                }
            }

            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configFilePath, json, Encoding.UTF8);
        }
        finally
        {
            _configLock.Release();
        }
    }

    private static (long ChatId, string Title, string? Username) ExtractChatInfo(JsonNode? chatNode, JsonNode? fromNode)
    {
        var chatId = chatNode?["id"]?.GetValue<long>() ?? 0;
        var chatTitle = chatNode?["title"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(chatTitle))
        {
            var first = fromNode?["first_name"]?.GetValue<string>() ?? chatNode?["first_name"]?.GetValue<string>() ?? "";
            var last = fromNode?["last_name"]?.GetValue<string>() ?? chatNode?["last_name"]?.GetValue<string>() ?? "";
            chatTitle = $"{first} {last}".Trim();
        }
        if (string.IsNullOrWhiteSpace(chatTitle))
        {
            chatTitle = $"Chat {chatId}";
        }
        var username = fromNode?["username"]?.GetValue<string>() ?? chatNode?["username"]?.GetValue<string>();
        return (chatId, chatTitle, username);
    }

    private async Task HandleIncomingMessageAsync(string token, JsonNode message, CancellationToken ct)
    {
        var (chatId, chatTitle, username) = ExtractChatInfo(message["chat"], message["from"]);
        if (chatId == 0) return;

        var text = (message["text"]?.GetValue<string>() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        // Check PIN authorization command: /auth 1234 or direct PIN if unauthorized
        if (!IsChatAuthorized(chatId))
        {
            var pinCandidate = text.StartsWith("/auth", StringComparison.OrdinalIgnoreCase)
                ? text["/auth".Length..].Trim()
                : text;

            if (!string.IsNullOrWhiteSpace(_config.AccessPin) &&
                string.Equals(pinCandidate, _config.AccessPin.Trim(), StringComparison.Ordinal))
            {
                await RegisterOrAuthorizeChatAsync(chatId, chatTitle, username, authorizeNow: true);
                await SendTextMessageAsync(
                    token,
                    chatId,
                    $"✅ <b>تم تفعيل صلاحية وصولك بنجاح!</b>\nمرحباً بك <b>{EscapeHtml(chatTitle)}</b> في بوت أعياد ميلاد الموظفين (الموظف ٣٦٠°).",
                    BuildMainMenuKeyboard(),
                    ct);
                return;
            }

            await RegisterOrAuthorizeChatAsync(chatId, chatTitle, username, authorizeNow: false);
            await SendTextMessageAsync(
                token,
                chatId,
                $"🔒 <b>هذا البوت مخصص للمخولين في نظام الموارد البشرية (الموظف ٣٦٠°).</b>\n\n" +
                $"📌 معرف المحادثة الخاص بك (Chat ID): <code>{chatId}</code>\n" +
                $"• تم إرسال طلب انضمامك إلى لوحة تحكم الأدمن للموافقة.\n" +
                $"• أو يمكنك إرسال رمز التفعيل السري مباشرة هنا لتفعيل الحساب فوراً.",
                null,
                ct);
            return;
        }

        // Authorized commands
        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("القائمة", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("رجوع", StringComparison.OrdinalIgnoreCase))
        {
            await SendMainMenuAsync(token, chatId, chatTitle, ct);
            return;
        }

        if (text.StartsWith("/today", StringComparison.OrdinalIgnoreCase) || text.Contains("مواليد اليوم"))
        {
            await SendBirthdayQueryResultAsync(token, chatId, "today", 0, sendPhotos: _config.SendPhotosAutomatically, ct);
            return;
        }

        if (text.StartsWith("/tomorrow", StringComparison.OrdinalIgnoreCase) || text.Contains("مواليد الغد"))
        {
            await SendBirthdayQueryResultAsync(token, chatId, "tomorrow", 0, sendPhotos: _config.SendPhotosAutomatically, ct);
            return;
        }

        if (text.StartsWith("/week", StringComparison.OrdinalIgnoreCase))
        {
            await SendBirthdayQueryResultAsync(token, chatId, "week", 0, sendPhotos: false, ct);
            return;
        }

        if (text.StartsWith("/month", StringComparison.OrdinalIgnoreCase))
        {
            await SendBirthdayQueryResultAsync(token, chatId, "cur_month", 0, sendPhotos: false, ct);
            return;
        }

        if (text.StartsWith("/nextmonth", StringComparison.OrdinalIgnoreCase) || text.Contains("الشهر القادم"))
        {
            await SendBirthdayQueryResultAsync(token, chatId, "next_month", 0, sendPhotos: false, ct);
            return;
        }

        if (text.StartsWith("/search", StringComparison.OrdinalIgnoreCase))
        {
            var q = text["/search".Length..].Trim();
            if (string.IsNullOrWhiteSpace(q))
            {
                await SendSearchInstructionsAsync(token, chatId, ct);
            }
            else
            {
                await HandleFreeTextSearchAsync(token, chatId, q, ct);
            }
            return;
        }

        // Any other text is treated as an intelligent search (by name, employee ID, formation, or date DD/MM)
        await HandleFreeTextSearchAsync(token, chatId, text, ct);
    }

    private async Task HandleCallbackQueryAsync(string token, JsonNode cbQuery, CancellationToken ct)
    {
        var cbId = cbQuery["id"]?.GetValue<string>();
        var data = cbQuery["data"]?.GetValue<string>() ?? string.Empty;
        var msg = cbQuery["message"];
        var (chatId, chatTitle, _) = ExtractChatInfo(msg?["chat"], cbQuery["from"]);

        if (!string.IsNullOrWhiteSpace(cbId))
        {
            _ = AnswerCallbackQueryAsync(token, cbId, ct);
        }

        if (chatId == 0 || !IsChatAuthorized(chatId))
        {
            return;
        }

        // Callback format examples:
        // bday:menu
        // bday:today:0
        // bday:tomorrow:0
        // bday:week:0
        // bday:cur_month:0
        // bday:next_month:0
        // bday:m_4:0  (month 4, page 0)
        // bday:pick_month
        // bday:pick_formation:0
        // bday:f_3:0  (formation index 3, page 0)
        // bday:emp:1027050 (send full card + photo for employee)
        // bday:photos:today:0 (send photos for all items on this page)
        // bday:search_help

        var parts = data.Split(':');
        if (parts.Length < 2 || parts[0] != "bday") return;

        var action = parts[1];
        var page = parts.Length >= 3 && int.TryParse(parts[2], out var p) ? p : 0;

        switch (action)
        {
            case "menu":
                await SendMainMenuAsync(token, chatId, chatTitle, ct);
                break;

            case "pick_month":
                await SendMonthPickerMenuAsync(token, chatId, ct);
                break;

            case "pick_formation":
                await SendFormationPickerMenuAsync(token, chatId, page, ct);
                break;

            case "search_help":
                await SendSearchInstructionsAsync(token, chatId, ct);
                break;

            case "emp":
                if (parts.Length >= 3)
                {
                    await SendSingleEmployeePhotoCardAsync(token, chatId, parts[2], ct);
                }
                break;

            case "photos":
                if (parts.Length >= 3)
                {
                    var subQuery = parts[2];
                    var subPage = parts.Length >= 4 && int.TryParse(parts[3], out var sp) ? sp : 0;
                    await SendBirthdayQueryResultAsync(token, chatId, subQuery, subPage, sendPhotos: true, ct);
                }
                break;

            default:
                var autoPhotos = (action == "today" || action == "tomorrow") && page == 0 && _config.SendPhotosAutomatically;
                await SendBirthdayQueryResultAsync(token, chatId, action, page, sendPhotos: autoPhotos, ct);
                break;
        }
    }

    private async Task SendMainMenuAsync(string token, long chatId, string chatTitle, CancellationToken ct)
    {
        var all = await LoadActiveEmployeeBirthdaysAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var nextMonth = today.AddMonths(1).Month;

        var todayCount = all.Count(e => e.DaysUntilNext == 0);
        var tomorrowCount = all.Count(e => e.DaysUntilNext == 1);
        var weekCount = all.Count(e => e.DaysUntilNext <= 7);
        var curMonthCount = all.Count(e => e.BirthDate.Month == today.Month);
        var nextMonthCount = all.Count(e => e.BirthDate.Month == nextMonth);

        var sb = new StringBuilder();
        sb.AppendLine("🎂 <b>بوت أعياد ميلاد الموظفين — الموظف ٣٦٠°</b>");
        sb.AppendLine($"مرحباً بك <b>{EscapeHtml(chatTitle)}</b> 👋");
        sb.AppendLine($"📅 التاريخ اليوم: <b>{today:yyyy/MM/dd}</b>");
        sb.AppendLine("──────────────────────");
        sb.AppendLine($"🎉 مواليد اليوم: <b>{todayCount}</b> موظف");
        sb.AppendLine($"🌅 مواليد الغد: <b>{tomorrowCount}</b> موظف");
        sb.AppendLine($"📆 خلال ٧ أيام: <b>{weekCount}</b> موظف");
        sb.AppendLine($"🗓️ الشهر الحالي ({ZodiacAndGenerationHelper.FormatMonthArabic(today.Month)}): <b>{curMonthCount}</b> موظف");
        sb.AppendLine($"⏭️ الشهر القادم ({ZodiacAndGenerationHelper.FormatMonthArabic(nextMonth)}): <b>{nextMonthCount}</b> موظف");
        sb.AppendLine("──────────────────────");
        sb.AppendLine("💡 <i>اختر من الأزرار أدناه أو اكتب مباشرة اسم موظف، أو رقم وظيفي، أو اسم تشكيل، أو تاريخ (مثل 15/5):</i>");

        await SendTextMessageAsync(token, chatId, sb.ToString(), BuildMainMenuKeyboard(todayCount, curMonthCount, nextMonthCount), ct);
    }

    private static JsonObject BuildMainMenuKeyboard(int? todayCount = null, int? curMonthCount = null, int? nextMonthCount = null)
    {
        var todayLabel = todayCount.HasValue ? $"🎂 مواليد اليوم ({todayCount})" : "🎂 مواليد اليوم";
        var curMonthLabel = curMonthCount.HasValue ? $"🗓️ الشهر الحالي ({curMonthCount})" : "🗓️ الشهر الحالي";
        var nextMonthLabel = nextMonthCount.HasValue ? $"⏭️ الشهر القادم ({nextMonthCount})" : "⏭️ الشهر القادم";

        return BuildInlineKeyboard(
        [
            [
                Btn(todayLabel, "bday:today:0"),
                Btn("🌅 مواليد الغد", "bday:tomorrow:0")
            ],
            [
                Btn(curMonthLabel, "bday:cur_month:0"),
                Btn(nextMonthLabel, "bday:next_month:0")
            ],
            [
                Btn("📆 خلال ٧ أيام", "bday:week:0"),
                Btn("📅 خلال ٣٠ يوماً", "bday:month30:0")
            ],
            [
                Btn("🔢 اختيار شهر (١ - ١٢)", "bday:pick_month"),
                Btn("🏢 حسب التشكيل / الفرع", "bday:pick_formation:0")
            ],
            [
                Btn("🔍 البحث الحر وحسب الطلب", "bday:search_help")
            ]
        ]);
    }

    private async Task SendMonthPickerMenuAsync(string token, long chatId, CancellationToken ct)
    {
        var all = await LoadActiveEmployeeBirthdaysAsync(ct);
        var rows = new List<List<JsonObject>>();

        for (int m = 1; m <= 12; m += 3)
        {
            var row = new List<JsonObject>();
            for (int k = m; k < m + 3 && k <= 12; k++)
            {
                var count = all.Count(e => e.BirthDate.Month == k);
                var monthAr = ZodiacAndGenerationHelper.FormatMonthArabic(k);
                row.Add(Btn($"{k}. {monthAr} ({count})", $"bday:m_{k}:0"));
            }
            rows.Add(row);
        }

        rows.Add([Btn("🏠 القائمة الرئيسية", "bday:menu")]);

        await SendTextMessageAsync(
            token,
            chatId,
            "🔢 <b>اختر الشهر المطلوب لعرض موظفيه وتفاصيل أعياد ميلادهم:</b>",
            BuildInlineKeyboard(rows),
            ct);
    }

    private async Task SendFormationPickerMenuAsync(string token, long chatId, int page, CancellationToken ct)
    {
        var all = await LoadActiveEmployeeBirthdaysAsync(ct);
        var formations = all
            .Where(e => !string.IsNullOrWhiteSpace(e.Formation))
            .GroupBy(e => e.Formation!.Trim())
            .OrderByDescending(g => g.Count())
            .Select((g, idx) => new { Index = idx, Name = g.Key, Count = g.Count() })
            .ToList();

        const int pageSize = 8;
        var totalPages = Math.Max(1, (int)Math.Ceiling(formations.Count / (double)pageSize));
        page = Math.Clamp(page, 0, totalPages - 1);

        var slice = formations.Skip(page * pageSize).Take(pageSize).ToList();
        var rows = new List<List<JsonObject>>();

        foreach (var f in slice)
        {
            rows.Add([Btn($"🏢 {f.Name} ({f.Count} موظف)", $"bday:f_{f.Index}:0")]);
        }

        var navRow = new List<JsonObject>();
        if (page > 0) navRow.Add(Btn("➡️ السابق", $"bday:pick_formation:{page - 1}"));
        if (page < totalPages - 1) navRow.Add(Btn("⬅️ التالي", $"bday:pick_formation:{page + 1}"));
        if (navRow.Count > 0) rows.Add(navRow);

        rows.Add([Btn("🏠 القائمة الرئيسية", "bday:menu")]);

        await SendTextMessageAsync(
            token,
            chatId,
            $"🏢 <b>اختر التشكيل أو الفرع (صفحة {page + 1} من {totalPages}):</b>",
            BuildInlineKeyboard(rows),
            ct);
    }

    private async Task SendSearchInstructionsAsync(string token, long chatId, CancellationToken ct)
    {
        var msg =
            "🔍 <b>البحث الذكي حسب متطلباتك:</b>\n\n" +
            "يمكنك كتابة أي مما يلي مباشرة في المحادثة:\n" +
            "1️⃣ <b>اسم الموظف</b>: مثل <code>أحمد</code> أو <code>محمد علي</code>\n" +
            "2️⃣ <b>الرقم الوظيفي</b>: مثل <code>1027050</code>\n" +
            "3️⃣ <b>اسم التشكيل أو الفرع</b>: مثل <code>فرع الموصل</code> أو <code>فرع أربيل</code> أو <code>فرع البصرة</code>\n" +
            "4️⃣ <b>تاريخ محدد (يوم/شهر)</b>: مثل <code>15/5</code> أو <code>25/12</code>\n\n" +
            "✨ <i>سيقوم البوت فوراً بجلب الموظفين المطابقين مع صورهم وتفاصيلهم الكاملة!</i>";

        await SendTextMessageAsync(
            token,
            chatId,
            msg,
            BuildInlineKeyboard([[Btn("🏠 القائمة الرئيسية", "bday:menu")]]),
            ct);
    }

    private async Task HandleFreeTextSearchAsync(string token, long chatId, string queryText, CancellationToken ct)
    {
        var q = queryText.Trim();
        var all = await LoadActiveEmployeeBirthdaysAsync(ct);

        List<BotEmployeeBirthdayCard> matches;
        string title;

        // Check if query is day/month like "15/5" or "15-05"
        var dateParts = q.Split(['/', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (dateParts.Length == 2 &&
            int.TryParse(dateParts[0], out var d) &&
            int.TryParse(dateParts[1], out var m) &&
            d is >= 1 and <= 31 && m is >= 1 and <= 12)
        {
            matches = all
                .Where(e => e.BirthDate.Day == d && e.BirthDate.Month == m)
                .OrderBy(e => e.Name)
                .ToList();
            title = $"مواليد تاريخ {d:00}/{m:00} ({ZodiacAndGenerationHelper.FormatMonthArabic(m)})";
        }
        else
        {
            matches = all
                .Where(e =>
                    e.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    e.EmployeeID.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (e.Formation != null && e.Formation.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (e.JobTitle != null && e.JobTitle.Contains(q, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(e => e.DaysUntilNext)
                .ToList();
            title = $"نتائج البحث عن: «{q}»";
        }

        if (matches.Count == 0)
        {
            await SendTextMessageAsync(
                token,
                chatId,
                $"ℹ️ لم يتم العثور على موظفين حاليين مطابقين لـ «<b>{EscapeHtml(q)}</b>».",
                BuildInlineKeyboard([[Btn("🏠 القائمة الرئيسية", "bday:menu")]]),
                ct);
            return;
        }

        if (matches.Count <= 5)
        {
            // Automatically send full photo cards when 1..5 matches are found!
            await SendTextMessageAsync(
                token,
                chatId,
                $"✅ <b>{EscapeHtml(title)} ({matches.Count} موظف):</b>\nجارٍ إرسال البطاقات مع الصور...",
                null,
                ct);

            foreach (var emp in matches)
            {
                await SendEmployeeCardWithPhotoInternalAsync(token, chatId, emp, ct);
            }

            await SendTextMessageAsync(
                token,
                chatId,
                "✨ تم عرض كافة النتائج المطابقة.",
                BuildInlineKeyboard([[Btn("🏠 القائمة الرئيسية", "bday:menu")]]),
                ct);
            return;
        }

        // More than 5 matches: send summary + inline buttons to open any employee's photo card
        await RenderAndSendListPageAsync(token, chatId, title, matches, page: 0, queryCode: $"search", sendPhotos: false, ct);
    }

    private async Task SendBirthdayQueryResultAsync(
        string token,
        long chatId,
        string queryCode,
        int page,
        bool sendPhotos,
        CancellationToken ct)
    {
        var all = await LoadActiveEmployeeBirthdaysAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var nextMonth = today.AddMonths(1).Month;

        string title;
        List<BotEmployeeBirthdayCard> list;

        if (queryCode == "today")
        {
            title = $"🎂 مواليد اليوم ({today:yyyy/MM/dd})";
            list = all.Where(e => e.DaysUntilNext == 0).OrderBy(e => e.Name).ToList();
        }
        else if (queryCode == "tomorrow")
        {
            title = $"🌅 مواليد الغد ({today.AddDays(1):yyyy/MM/dd})";
            list = all.Where(e => e.DaysUntilNext == 1).OrderBy(e => e.Name).ToList();
        }
        else if (queryCode == "week")
        {
            title = "📆 أعياد الميلاد خلال ٧ أيام القادمة";
            list = all.Where(e => e.DaysUntilNext <= 7).OrderBy(e => e.DaysUntilNext).ThenBy(e => e.Name).ToList();
        }
        else if (queryCode == "month30")
        {
            title = "📅 أعياد الميلاد خلال ٣٠ يوماً القادمة";
            list = all.Where(e => e.DaysUntilNext <= 30).OrderBy(e => e.DaysUntilNext).ThenBy(e => e.Name).ToList();
        }
        else if (queryCode == "cur_month")
        {
            var mName = ZodiacAndGenerationHelper.FormatMonthArabic(today.Month);
            title = $"🗓️ مواليد الشهر الحالي — {mName} (شهر {today.Month})";
            list = all.Where(e => e.BirthDate.Month == today.Month).OrderBy(e => e.BirthDate.Day).ThenBy(e => e.Name).ToList();
        }
        else if (queryCode == "next_month")
        {
            var mName = ZodiacAndGenerationHelper.FormatMonthArabic(nextMonth);
            title = $"⏭️ مواليد الشهر القادم — {mName} (شهر {nextMonth})";
            list = all.Where(e => e.BirthDate.Month == nextMonth).OrderBy(e => e.BirthDate.Day).ThenBy(e => e.Name).ToList();
        }
        else if (queryCode.StartsWith("m_") && int.TryParse(queryCode[2..], out var mNum) && mNum is >= 1 and <= 12)
        {
            var mName = ZodiacAndGenerationHelper.FormatMonthArabic(mNum);
            title = $"🗓️ مواليد شهر {mName} ({mNum})";
            list = all.Where(e => e.BirthDate.Month == mNum).OrderBy(e => e.BirthDate.Day).ThenBy(e => e.Name).ToList();
        }
        else if (queryCode.StartsWith("f_") && int.TryParse(queryCode[2..], out var fIdx))
        {
            var formations = all
                .Where(e => !string.IsNullOrWhiteSpace(e.Formation))
                .GroupBy(e => e.Formation!.Trim())
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .ToList();

            var fName = fIdx >= 0 && fIdx < formations.Count ? formations[fIdx] : "";
            title = $"🏢 مواليد تشكيل: {fName}";
            list = all.Where(e => string.Equals(e.Formation?.Trim(), fName, StringComparison.OrdinalIgnoreCase))
                      .OrderBy(e => e.DaysUntilNext)
                      .ToList();
        }
        else
        {
            title = "🎂 قائمة أعياد الميلاد";
            list = all.OrderBy(e => e.DaysUntilNext).ToList();
        }

        await RenderAndSendListPageAsync(token, chatId, title, list, page, queryCode, sendPhotos, ct);
    }

    private async Task RenderAndSendListPageAsync(
        string token,
        long chatId,
        string title,
        List<BotEmployeeBirthdayCard> list,
        int page,
        string queryCode,
        bool sendPhotos,
        CancellationToken ct)
    {
        if (list.Count == 0)
        {
            await SendTextMessageAsync(
                token,
                chatId,
                $"<b>{EscapeHtml(title)}</b>\n\nلا يوجد موظفون مطابقون في هذه الفترة.",
                BuildInlineKeyboard([[Btn("🏠 القائمة الرئيسية", "bday:menu")]]),
                ct);
            return;
        }

        const int pageSize = 8;
        var totalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)pageSize));
        page = Math.Clamp(page, 0, totalPages - 1);
        var slice = list.Skip(page * pageSize).Take(pageSize).ToList();

        if (sendPhotos)
        {
            await SendTextMessageAsync(
                token,
                chatId,
                $"📸 <b>{EscapeHtml(title)}</b>\nإرسال بطاقات وصور الموظفين ({slice.Count} من أصل {list.Count})...",
                null,
                ct);

            foreach (var emp in slice)
            {
                await SendEmployeeCardWithPhotoInternalAsync(token, chatId, emp, ct);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"<b>{EscapeHtml(title)}</b>");
        sb.AppendLine($"إجمالي العدد: <b>{list.Count}</b> موظف | صفحة <b>{page + 1}</b> من <b>{totalPages}</b>");
        sb.AppendLine("──────────────────────");

        var index = page * pageSize + 1;
        foreach (var emp in slice)
        {
            var whenBadge = emp.DaysUntilNext == 0
                ? "🎉 <b>اليوم!</b>"
                : emp.DaysUntilNext == 1
                    ? "🌅 <b>غداً</b>"
                    : $"بعد {emp.DaysUntilNext} يوم";

            var photoBadge = !string.IsNullOrWhiteSpace(emp.StorageKey) ? "📷" : "👤";
            sb.AppendLine($"{index}. {photoBadge} <b>{EscapeHtml(emp.Name)}</b> (<code>#{emp.EmployeeID}</code>)");
            sb.AppendLine($"   🏢 {EscapeHtml(emp.Formation ?? "غير محدد")} — {EscapeHtml(emp.JobTitle ?? "موظف")}");
            sb.AppendLine($"   📅 الميلاد: <b>{emp.BirthDate:yyyy/MM/dd}</b> | العمر: <b>{emp.Age}</b> سنة | {whenBadge}");
            sb.AppendLine($"   {emp.Zodiac.Symbol} برج {emp.Zodiac.NameAr} • {emp.Generation.NameAr}");
            sb.AppendLine();
            index++;
        }

        sb.AppendLine("👇 <i>اضغط على اسم أي موظف بالأسفل لعرض صورته الشخصية وبطاقته الكاملة:</i>");

        var rows = new List<List<JsonObject>>();

        // Employee quick photo buttons (2 per row)
        for (int i = 0; i < slice.Count; i += 2)
        {
            var row = new List<JsonObject>
            {
                Btn($"📸 {TruncateName(slice[i].Name)}", $"bday:emp:{slice[i].EmployeeID}")
            };
            if (i + 1 < slice.Count)
            {
                row.Add(Btn($"📸 {TruncateName(slice[i + 1].Name)}", $"bday:emp:{slice[i + 1].EmployeeID}"));
            }
            rows.Add(row);
        }

        if (queryCode != "search")
        {
            rows.Add([Btn($"🖼️ إرسال صور جميع موظفي هذه الصفحة ({slice.Count})", $"bday:photos:{queryCode}:{page}")]);

            var navRow = new List<JsonObject>();
            if (page > 0) navRow.Add(Btn("➡️ الصفحة السابقة", $"bday:{queryCode}:{page - 1}"));
            if (page < totalPages - 1) navRow.Add(Btn("⬅️ الصفحة التالية", $"bday:{queryCode}:{page + 1}"));
            if (navRow.Count > 0) rows.Add(navRow);
        }

        rows.Add([Btn("🏠 القائمة الرئيسية", "bday:menu")]);

        await SendTextMessageAsync(token, chatId, sb.ToString(), BuildInlineKeyboard(rows), ct);
    }

    private static string TruncateName(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 3) return name;
        return $"{parts[0]} {parts[1]} {parts[^1]}";
    }

    private async Task SendSingleEmployeePhotoCardAsync(string token, long chatId, string employeeId, CancellationToken ct)
    {
        var all = await LoadActiveEmployeeBirthdaysAsync(ct);
        var emp = all.FirstOrDefault(e => string.Equals(e.EmployeeID, employeeId, StringComparison.OrdinalIgnoreCase));
        if (emp is null)
        {
            await SendTextMessageAsync(token, chatId, "⚠️ لم يتم العثور على بيانات الموظف.", null, ct);
            return;
        }

        await SendEmployeeCardWithPhotoInternalAsync(token, chatId, emp, ct);
    }

    private async Task SendEmployeeCardWithPhotoInternalAsync(
        string token,
        long chatId,
        BotEmployeeBirthdayCard emp,
        CancellationToken ct)
    {
        var monthNameAr = ZodiacAndGenerationHelper.FormatMonthArabic(emp.BirthDate.Month);
        var whenText = emp.DaysUntilNext == 0
            ? "🎉 يصادف عيد ميلاده اليوم! كل عام وهو بألف خير 🎂"
            : emp.DaysUntilNext == 1
                ? "🌅 يصادف عيد ميلاده يوم غد"
                : $"⏳ متبقي لعيد ميلاده القادم: {emp.DaysUntilNext} يوم ({emp.NextBirthdayDate:yyyy/MM/dd})";

        var caption = new StringBuilder();
        caption.AppendLine($"🎂 <b>بطاقة عيد ميلاد موظف — الموظف ٣٦٠°</b>");
        caption.AppendLine("──────────────────────");
        caption.AppendLine($"👤 <b>الاسم الكامل:</b> {EscapeHtml(emp.Name)}");
        caption.AppendLine($"🆔 <b>الرقم الوظيفي:</b> <code>#{EscapeHtml(emp.EmployeeID)}</code>");
        caption.AppendLine($"🏢 <b>التشكيل / الفرع:</b> {EscapeHtml(emp.Formation ?? "غير محدد")}");
        caption.AppendLine($"💼 <b>العنوان الوظيفي:</b> {EscapeHtml(emp.JobTitle ?? "غير محدد")}");
        caption.AppendLine($"📅 <b>تاريخ الميلاد:</b> {emp.BirthDate:yyyy/MM/dd} ({emp.BirthDate.Day} {monthNameAr})");
        caption.AppendLine($"🎈 <b>العمر الحالي:</b> {emp.Age} سنة (العمر الجديد: {emp.Age + (emp.DaysUntilNext == 0 ? 0 : 1)} سنة)");
        caption.AppendLine($"✨ <b>البرج والجيل:</b> {emp.Zodiac.Symbol} {emp.Zodiac.NameAr} ({emp.Zodiac.Element}) • {emp.Generation.NameAr}");
        caption.AppendLine("──────────────────────");
        caption.AppendLine($"<b>{whenText}</b>");

        // Try reading physical photo from storage service
        Stream? photoStream = null;
        try
        {
            var key = !string.IsNullOrWhiteSpace(emp.StorageKey) ? emp.StorageKey : emp.ThumbnailStorageKey;
            if (!string.IsNullOrWhiteSpace(key))
            {
                photoStream = await _storageService.OpenReadAsync(key, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not open profile photo for employee {EmployeeID}", emp.EmployeeID);
        }

        if (photoStream is not null)
        {
            await using (photoStream)
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(chatId.ToString(CultureInfo.InvariantCulture)), "chat_id");
                form.Add(new StringContent(caption.ToString()), "caption");
                form.Add(new StringContent("HTML"), "parse_mode");

                var streamContent = new StreamContent(photoStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(emp.PhotoContentType ?? "image/jpeg");
                form.Add(streamContent, "photo", $"employee_{emp.EmployeeID}.jpg");

                var resp = await _httpClient.PostAsync($"https://api.telegram.org/bot{token}/sendPhoto", form, ct);
                if (resp.IsSuccessStatusCode)
                {
                    return;
                }
            }
        }

        // Fallback if employee has no uploaded photo yet: send rich text card
        await SendTextMessageAsync(token, chatId, caption.ToString(), null, ct);
    }

    public async Task<(int SentCount, string Message)> BroadcastTodayBirthdaysNowAsync()
    {
        var token = (_config.BotToken ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return (0, "يرجى إدخال رمز البوت (Bot Token) أولاً.");
        }

        var recipients = _config.AuthorizedChats.Where(c => c.ReceiveDailyBroadcast).ToList();
        if (recipients.Count == 0)
        {
            return (0, "لا توجد محادثات مفوّضة ومفعّل لها استلام الإشعارات حالياً.");
        }

        var sentChats = 0;
        foreach (var chat in recipients)
        {
            try
            {
                await SendBirthdayQueryResultAsync(token, chat.ChatId, "today", 0, sendPhotos: _config.SendPhotosAutomatically, CancellationToken.None);
                sentChats++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed broadcasting today birthdays to chat {ChatId}", chat.ChatId);
            }
        }

        return (sentChats, $"تم إرسال كشف وصور أعياد ميلاد اليوم إلى {sentChats} محادثة على تيليجرام بنجاح.");
    }

    public async Task<(bool Ok, string Message)> SendEmployeeBirthdayToChatsAsync(string employeeId)
    {
        var token = (_config.BotToken ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "يرجى إعداد رمز البوت (Bot Token) أولاً من زر إعدادات بوت تيليجرام.");
        }

        if (_config.AuthorizedChats.Count == 0)
        {
            return (false, "لا توجد محادثات تيليجرام مفوّضة حالياً لإرسال البطاقة إليها.");
        }

        var all = await LoadActiveEmployeeBirthdaysAsync(CancellationToken.None);
        var emp = all.FirstOrDefault(e => string.Equals(e.EmployeeID, employeeId, StringComparison.OrdinalIgnoreCase));
        if (emp is null)
        {
            return (false, "تعذر العثور على بيانات ميلاد الموظف.");
        }

        var count = 0;
        foreach (var chat in _config.AuthorizedChats)
        {
            try
            {
                await SendEmployeeCardWithPhotoInternalAsync(token, chat.ChatId, emp, CancellationToken.None);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed sending employee birthday card to chat {ChatId}", chat.ChatId);
            }
        }

        return (count > 0, $"تم إرسال بطاقة وصورة الموظف ({emp.Name}) إلى {count} محادثة على تيليجرام.");
    }

    private async Task<List<BotEmployeeBirthdayCard>> LoadActiveEmployeeBirthdaysAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var today = DateOnly.FromDateTime(DateTime.Now);

        var rawEmps = await db.Employees
            .AsNoTracking()
            .Where(e => !e.IsArchived && e.BirthDate.HasValue)
            .Select(e => new
            {
                e.Id,
                e.EmployeeID,
                e.Name,
                BirthDate = e.BirthDate!.Value,
                JobTitle = e.JobTitle != null ? e.JobTitle.Name : null,
                Formation = e.Formation != null ? e.Formation.Name : null,
                Gender = e.Gender != null ? e.Gender.Name : null
            })
            .ToListAsync(ct);

        var activePhotos = await db.EmployeeProfilePhotos
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.EmployeeID,
                p.StorageKey,
                p.ThumbnailStorageKey,
                p.ContentType
            })
            .ToListAsync(ct);

        var photoMap = activePhotos
            .GroupBy(p => p.EmployeeID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var list = new List<BotEmployeeBirthdayCard>(rawEmps.Count);
        foreach (var e in rawEmps)
        {
            var bDate = e.BirthDate;
            var age = ZodiacAndGenerationHelper.CalculateAge(bDate, today);
            var daysUntil = ZodiacAndGenerationHelper.DaysUntilBirthday(bDate, today);

            var nextBday = new DateOnly(today.Year, bDate.Month, Math.Min(bDate.Day, DateTime.DaysInMonth(today.Year, bDate.Month)));
            if (nextBday < today)
            {
                nextBday = new DateOnly(today.Year + 1, bDate.Month, Math.Min(bDate.Day, DateTime.DaysInMonth(today.Year + 1, bDate.Month)));
            }

            photoMap.TryGetValue(e.EmployeeID, out var photoInfo);

            list.Add(new BotEmployeeBirthdayCard(
                e.Id,
                e.EmployeeID,
                e.Name,
                bDate,
                e.JobTitle,
                e.Formation,
                e.Gender,
                age,
                daysUntil,
                nextBday,
                ZodiacAndGenerationHelper.GetZodiacSign(bDate),
                ZodiacAndGenerationHelper.GetGeneration(bDate.Year),
                photoInfo?.StorageKey,
                photoInfo?.ThumbnailStorageKey,
                photoInfo?.ContentType));
        }

        return list;
    }

    private async Task SendTextMessageAsync(
        string token,
        long chatId,
        string htmlText,
        JsonObject? replyMarkup,
        CancellationToken ct)
    {
        var payloadObj = new JsonObject
        {
            ["chat_id"] = chatId,
            ["text"] = htmlText,
            ["parse_mode"] = "HTML",
            ["disable_web_page_preview"] = true
        };

        if (replyMarkup is not null)
        {
            payloadObj["reply_markup"] = replyMarkup;
        }

        using var content = new StringContent(payloadObj.ToJsonString(), Encoding.UTF8, "application/json");
        await _httpClient.PostAsync($"https://api.telegram.org/bot{token}/sendMessage", content, ct);
    }

    private async Task AnswerCallbackQueryAsync(string token, string callbackQueryId, CancellationToken ct)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { callback_query_id = callbackQueryId });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync($"https://api.telegram.org/bot{token}/answerCallbackQuery", content, ct);
        }
        catch { }
    }

    private static JsonObject Btn(string text, string callbackData) => new()
    {
        ["text"] = text,
        ["callback_data"] = callbackData
    };

    private static JsonObject BuildInlineKeyboard(IEnumerable<IEnumerable<JsonObject>> rows)
    {
        var keyboardArray = new JsonArray();
        foreach (var r in rows)
        {
            var rowArray = new JsonArray();
            foreach (var b in r)
            {
                rowArray.Add(b);
            }
            keyboardArray.Add(rowArray);
        }

        return new JsonObject
        {
            ["inline_keyboard"] = keyboardArray
        };
    }

    private static string EscapeHtml(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}

