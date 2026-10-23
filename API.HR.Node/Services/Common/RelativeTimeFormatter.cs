using System.Globalization;

namespace API.HR.Node.Services.Common;

/// <summary>
/// منسق الوقت النسبي (Relative Time Formatter) باللغة العربية
/// متوافق مع معايير Moment.js / Humanizer لعرض الفترات الزمنية بطريقة واضحة ومقروءة.
/// </summary>
public static class RelativeTimeFormatter
{
    private static readonly CultureInfo ArabicCulture = new("ar-IQ");

    /// <summary>
    /// يحول التاريخ والوقت إلى نص نسبي باللغة العربية (مثل: منذ لحظات، منذ 5 دقائق، منذ ساعتين، منذ 3 أيام)
    /// </summary>
    public static string ToRelativeArabic(this DateTime? dateTime, DateTime? referenceTime = null)
    {
        if (!dateTime.HasValue)
        {
            return "لم يتم التحديث بعد";
        }

        return ToRelativeArabic(dateTime.Value, referenceTime);
    }

    /// <summary>
    /// يحول التاريخ والوقت إلى نص نسبي باللغة العربية
    /// </summary>
    public static string ToRelativeArabic(this DateTime dateTime, DateTime? referenceTime = null)
    {
        var now = referenceTime ?? (dateTime.Kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now);

        // إذا كان أحدهما Utc والآخر محلي
        if (dateTime.Kind == DateTimeKind.Utc && now.Kind != DateTimeKind.Utc)
        {
            now = DateTime.UtcNow;
        }
        else if (dateTime.Kind != DateTimeKind.Utc && now.Kind == DateTimeKind.Utc)
        {
            now = DateTime.Now;
        }

        var diff = now - dateTime;
        var isFuture = diff.TotalSeconds < 0;
        var span = isFuture ? diff.Negate() : diff;

        var seconds = span.TotalSeconds;
        var minutes = span.TotalMinutes;
        var hours = span.TotalHours;
        var days = span.TotalDays;

        string relativeText;

        if (seconds < 45)
        {
            relativeText = isFuture ? "بعد لحظات" : "منذ لحظات";
        }
        else if (seconds < 90)
        {
            relativeText = isFuture ? "بعد دقيقة" : "منذ دقيقة";
        }
        else if (minutes < 45)
        {
            var roundedMinutes = (int)Math.Round(minutes);
            relativeText = FormatPlural(roundedMinutes, "دقيقة", "دقيقتين", "دقائق", "دقيقة", isFuture);
        }
        else if (minutes < 90)
        {
            relativeText = isFuture ? "بعد ساعة" : "منذ ساعة";
        }
        else if (hours < 22)
        {
            var roundedHours = (int)Math.Round(hours);
            relativeText = FormatPlural(roundedHours, "ساعة", "ساعتين", "ساعات", "ساعة", isFuture);
        }
        else if (hours < 36)
        {
            relativeText = isFuture ? "غداً" : "أمس (منذ يوم)";
        }
        else if (days < 26)
        {
            var roundedDays = (int)Math.Round(days);
            relativeText = FormatPlural(roundedDays, "يوم", "يومين", "أيام", "يوماً", isFuture);
        }
        else if (days < 45)
        {
            relativeText = isFuture ? "بعد شهر" : "منذ شهر";
        }
        else if (days < 320)
        {
            var months = (int)Math.Round(days / 30.44);
            relativeText = FormatPlural(months, "شهر", "شهرين", "أشهر", "شهراً", isFuture);
        }
        else if (days < 545)
        {
            relativeText = isFuture ? "بعد سنة" : "منذ سنة";
        }
        else
        {
            var years = (int)Math.Round(days / 365.25);
            relativeText = FormatPlural(years, "سنة", "سنتين", "سنوات", "سنة", isFuture);
        }

        return relativeText;
    }

    /// <summary>
    /// صياغة الجمع والمثنى العربية السليمة
    /// </summary>
    private static string FormatPlural(int count, string singular, string dual, string pluralFew, string pluralMany, bool isFuture)
    {
        var prefix = isFuture ? "بعد " : "منذ ";

        if (count == 1) return prefix + singular;
        if (count == 2) return prefix + dual;
        if (count is >= 3 and <= 10) return $"{prefix}{count} {pluralFew}";
        return $"{prefix}{count} {pluralMany}";
    }

    /// <summary>
    /// يرجع التاريخ والوقت الدقيق بصيغة واضحة للعرض في تلميحات الشاشة (Tooltips)
    /// </summary>
    public static string ToExactString(this DateTime? dateTime)
    {
        if (!dateTime.HasValue) return "غير محدد";
        return ToExactString(dateTime.Value);
    }

    /// <summary>
    /// يرجع التاريخ والوقت الدقيق بصيغة واضحة
    /// </summary>
    public static string ToExactString(this DateTime dateTime)
    {
        var localTime = dateTime.Kind == DateTimeKind.Utc ? dateTime.ToLocalTime() : dateTime;
        return localTime.ToString("yyyy/MM/dd  hh:mm tt", ArabicCulture);
    }
}
