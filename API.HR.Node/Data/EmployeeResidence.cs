using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

/// <summary>
/// سجل معلومات السكن وبطاقة السكن المرتبط بالموظف
/// </summary>
public sealed class EmployeeResidence
{
    public int Id { get; set; }

    // رقم الموظف التسلسلي — المفتاح الأجنبي
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // الرقم الوظيفي للبحث والربط
    [MaxLength(50)]
    public string EmployeeIdNumber { get; set; } = string.Empty;

    // المحافظة — قائمة منسدلة
    public int? GovernorateId { get; set; }
    public Governorate? Governorate { get; set; }

    // مكتب المعلومات
    [MaxLength(250)]
    public string? InformationOffice { get; set; }

    // اسم رب الأسرة
    [MaxLength(250)]
    public string? FamilyHeadName { get; set; }

    // نوع السكن: "دار" أو "شقة"
    [MaxLength(50)]
    public string HousingType { get; set; } = "دار";

    // في حالة السكن "دار":
    // الحي
    [MaxLength(150)]
    public string? District { get; set; }

    // محلة
    [MaxLength(100)]
    public string? Mahalla { get; set; }

    // زقاق
    [MaxLength(100)]
    public string? Zuqaq { get; set; }

    // دار
    [MaxLength(100)]
    public string? Dar { get; set; }

    // في حالة السكن "شقة":
    // اسم المجمع
    [MaxLength(200)]
    public string? ComplexName { get; set; }

    // البناية / العمارة
    [MaxLength(100)]
    public string? BuildingNumber { get; set; }

    // الطابق
    [MaxLength(50)]
    public string? FloorNumber { get; set; }

    // الشقة
    [MaxLength(100)]
    public string? ApartmentNumber { get; set; }

    // رقم الاستمارة / بطاقة السكن
    [MaxLength(100)]
    public string? FormNumber { get; set; }

    // تاريخ تنظيم الاستمارة
    public DateOnly? FormDate { get; set; }

    // معلومات الحيوية للموقع (رابط خرائط Google أو إحداثيات أو موقع حيوي)
    [MaxLength(1000)]
    public string? LocationUrl { get; set; }

    // الإحداثيات الجغرافية (خط العرض وخط الطول) للظهور بدقة على الخريطة
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // وجه بطاقة السكن (مفتاح التخزين واسم الملف ونوعه)
    [MaxLength(500)]
    public string? CardFrontKey { get; set; }

    [MaxLength(255)]
    public string? CardFrontFileName { get; set; }

    [MaxLength(100)]
    public string? CardFrontContentType { get; set; }

    // ظهر بطاقة السكن (مفتاح التخزين واسم الملف ونوعه)
    [MaxLength(500)]
    public string? CardBackKey { get; set; }

    [MaxLength(255)]
    public string? CardBackFileName { get; set; }

    [MaxLength(100)]
    public string? CardBackContentType { get; set; }

    // من تاريخ
    public DateOnly? FromDate { get; set; }

    // الى تاريخ (إذا كان فارغاً أو مستقبلياً، يعتبر هذا السكن هو السكن الحالي تلقائياً دون الحاجة لحقل حالة)
    public DateOnly? ToDate { get; set; }

    // ملاحظات إضافية
    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// هل يعتبر هذا السكن هو السكن الحالي للموظف؟
    /// يكون حالياً إذا كان تاريخ الانتهاء غير محدد أو بعد تاريخ اليوم.
    /// </summary>
    public bool IsCurrent => !ToDate.HasValue || ToDate.Value >= DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// نص تفصيلي للعنوان بناءً على نوع السكن (دار أو شقة)
    /// </summary>
    public string FormattedAddress
    {
        get
        {
            if (HousingType == "شقة")
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(ComplexName)) parts.Add($"مجمع {ComplexName}");
                if (!string.IsNullOrWhiteSpace(BuildingNumber)) parts.Add($"عمارة/بناية {BuildingNumber}");
                if (!string.IsNullOrWhiteSpace(FloorNumber)) parts.Add($"طابق {FloorNumber}");
                if (!string.IsNullOrWhiteSpace(ApartmentNumber)) parts.Add($"شقة {ApartmentNumber}");
                return parts.Count > 0 ? string.Join("، ", parts) : "شقة سكنية";
            }
            else
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(District)) parts.Add($"حي {District}");
                if (!string.IsNullOrWhiteSpace(Mahalla)) parts.Add($"محلة: {Mahalla}");
                if (!string.IsNullOrWhiteSpace(Zuqaq)) parts.Add($"زقاق: {Zuqaq}");
                if (!string.IsNullOrWhiteSpace(Dar)) parts.Add($"دار: {Dar}");
                return parts.Count > 0 ? string.Join("، ", parts) : "دار سكنية";
            }
        }
    }

    /// <summary>
    /// استخراج أو محاولة قراءة الإحداثيات من رابط الموقع أو النص المسجل
    /// </summary>
    public (double Lat, double Lng)? TryGetCoordinates()
    {
        if (Latitude.HasValue && Longitude.HasValue)
        {
            return (Latitude.Value, Longitude.Value);
        }

        if (string.IsNullOrWhiteSpace(LocationUrl)) return null;

        return ParseCoordinates(LocationUrl);
    }

    /// <summary>
    /// استخراج ذكي للإحداثيات من نصوص وروابط Google Maps المتنوعة
    /// ويدعم إحداثيات الدبوس (!3d!4d)، والمعاملات (q, ll)، والدرجات (DMS)، والعكس التلقائي للإحداثيات المقلوبة.
    /// </summary>
    public static (double Lat, double Lng)? ParseCoordinates(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        text = text.Trim();

        // 1. Google Maps exact pin coordinates (!3d{lat}!4d{lng})
        var pinMatch = System.Text.RegularExpressions.Regex.Match(text, @"!3d(-?[0-9]{1,2}(?:\.[0-9]+)?)!4d(-?[0-9]{1,3}(?:\.[0-9]+)?)");
        if (pinMatch.Success)
        {
            if (TryNormalize(pinMatch.Groups[1].Value, pinMatch.Groups[2].Value, out var coords))
                return coords;
        }

        // 2. Query parameters (?q=lat,lng or &q=lat,lng or ll=lat,lng or query=lat,lng)
        var qMatch = System.Text.RegularExpressions.Regex.Match(text, @"(?:[\?&](?:q|query|ll|sll)=|destination=)(-?[0-9]{1,2}(?:\.[0-9]+)?)[,\s]+(-?[0-9]{1,3}(?:\.[0-9]+)?)");
        if (qMatch.Success)
        {
            if (TryNormalize(qMatch.Groups[1].Value, qMatch.Groups[2].Value, out var coords))
                return coords;
        }

        // 3. Google Maps viewport center (@lat,lng)
        var atMatch = System.Text.RegularExpressions.Regex.Match(text, @"@(-?[0-9]{1,2}(?:\.[0-9]+)?)[,\s]+(-?[0-9]{1,3}(?:\.[0-9]+)?)");
        if (atMatch.Success)
        {
            if (TryNormalize(atMatch.Groups[1].Value, atMatch.Groups[2].Value, out var coords))
                return coords;
        }

        // 4. DMS degrees format (e.g. 33°18'54.7"N 44°21'58.0"E)
        var dmsMatch = System.Text.RegularExpressions.Regex.Match(text, @"([0-9]{1,2})[°\s]+([0-9]{1,2})['\s]+([0-9]+(?:\.[0-9]+)?)?[""'\s]*([NSns])[, \s]+([0-9]{1,3})[°\s]+([0-9]{1,2})['\s]+([0-9]+(?:\.[0-9]+)?)?[""'\s]*([EWew])");
        if (dmsMatch.Success)
        {
            if (double.TryParse(dmsMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var latD) &&
                double.TryParse(dmsMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture, out var latM))
            {
                var latS = dmsMatch.Groups[3].Success && double.TryParse(dmsMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture, out var s1) ? s1 : 0.0;
                var lat = latD + (latM / 60.0) + (latS / 3600.0);
                if (dmsMatch.Groups[4].Value.Equals("S", StringComparison.OrdinalIgnoreCase)) lat = -lat;

                if (double.TryParse(dmsMatch.Groups[5].Value, System.Globalization.CultureInfo.InvariantCulture, out var lngD) &&
                    double.TryParse(dmsMatch.Groups[6].Value, System.Globalization.CultureInfo.InvariantCulture, out var lngM))
                {
                    var lngS = dmsMatch.Groups[7].Success && double.TryParse(dmsMatch.Groups[7].Value, System.Globalization.CultureInfo.InvariantCulture, out var s2) ? s2 : 0.0;
                    var lng = lngD + (lngM / 60.0) + (lngS / 3600.0);
                    if (dmsMatch.Groups[8].Value.Equals("W", StringComparison.OrdinalIgnoreCase)) lng = -lng;

                    return (Math.Round(lat, 6), Math.Round(lng, 6));
                }
            }
        }

        // 5. Standard coordinates pairs (e.g. "33.3152, 44.3661" or "33.3152 44.3661")
        var genMatch = System.Text.RegularExpressions.Regex.Match(text, @"(?:\b|[@=/?])([0-9]{1,2}(?:\.[0-9]+)?)[,\s]+([0-9]{1,3}(?:\.[0-9]+)?)");
        if (genMatch.Success)
        {
            if (TryNormalize(genMatch.Groups[1].Value, genMatch.Groups[2].Value, out var coords))
                return coords;
        }

        return null;
    }

    public static bool TryNormalize(string val1, string val2, out (double Lat, double Lng) result)
    {
        result = default;
        if (double.TryParse(val1, System.Globalization.CultureInfo.InvariantCulture, out var n1) &&
            double.TryParse(val2, System.Globalization.CultureInfo.InvariantCulture, out var n2))
        {
            // تصحيح حالة الإدخال المعكوس (خط الطول أولاً ثم خط العرض: مثل 44.36, 33.31 في العراق)
            if (n1 >= 38.0 && n1 <= 50.0 && n2 >= 28.0 && n2 <= 38.5)
            {
                result = (Math.Round(n2, 6), Math.Round(n1, 6));
                return true;
            }

            // الحالة الطبيعية (خط العرض ثم خط الطول: مثل 33.31, 44.36)
            if (n1 >= -90.0 && n1 <= 90.0 && n2 >= -180.0 && n2 <= 180.0)
            {
                result = (Math.Round(n1, 6), Math.Round(n2, 6));
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// حل الروابط المختصرة لـ Google Maps (مثل maps.app.goo.gl أو goo.gl/maps) واستخراج الإحداثيات الفعلية منها
    /// </summary>
    // One shared client with a short timeout: a new HttpClient per call exhausts
    // sockets, and the default 100-second timeout stalled the residences map.
    private static readonly HttpClient ShortLinkClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
    })
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    // A short link always resolves to the same place; remember it for the process lifetime.
    private static readonly ConcurrentDictionary<string, (double Lat, double Lng)?> ShortLinkCache = new(StringComparer.Ordinal);

    // Only Google's own short-link hosts are fetched. The old check was a substring
    // match, so "http://169.254.169.254/?maps.app.goo.gl" made the server call an
    // internal address on behalf of whoever saved that link.
    private static bool IsGoogleMapsShortLink(string text, out Uri uri)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out uri!) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return uri.Host.Equals("maps.app.goo.gl", StringComparison.OrdinalIgnoreCase)
            || (uri.Host.Equals("goo.gl", StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.StartsWith("/maps", StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<(double Lat, double Lng)?> ResolveUrlAndCoordinatesAsync(string? text, HttpClient? client = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var direct = ParseCoordinates(text);
        if (direct.HasValue) return direct;

        var trimmed = text.Trim();
        if (!IsGoogleMapsShortLink(trimmed, out var shortLink))
        {
            return null;
        }

        if (ShortLinkCache.TryGetValue(trimmed, out var cached))
        {
            return cached;
        }

        try
        {
            var http = client ?? ShortLinkClient;
            using var req = new HttpRequestMessage(HttpMethod.Get, shortLink);
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            var targetUri = resp.RequestMessage?.RequestUri?.ToString();
            var resolved = string.IsNullOrWhiteSpace(targetUri) ? null : ParseCoordinates(targetUri);
            ShortLinkCache[trimmed] = resolved;
            return resolved;
        }
        catch
        {
            // فشل الاتصال بالشبكة لحل الرابط — not cached, so a later attempt can succeed.
            return null;
        }
    }
}

/// <summary>
/// جدول قائمة المحافظات العراقية (Governorates Lookup Table)
/// </summary>
public sealed class Governorate : ILookupEntity
{
    public int Id { get; set; }

    [MaxLength(150)]
    public required string Name { get; set; }

    [MaxLength(150)]
    public string? NameKu { get; set; }

    public int DisplayOrder { get; set; }

    public double DefaultLatitude { get; set; } = 33.3152;
    public double DefaultLongitude { get; set; } = 44.3661;
}
