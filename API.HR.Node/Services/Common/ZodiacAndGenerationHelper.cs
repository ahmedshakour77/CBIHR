namespace API.HR.Node.Services.Common;

public sealed record ZodiacSignInfo(
    string NameAr,
    string NameEn,
    string Symbol,
    string Element,
    string ElementColor,
    string DateRange,
    string IconSvg,
    string Trait);

public sealed record GenerationInfo(
    string NameAr,
    string NameEn,
    string YearsRange,
    string Color,
    string TagClass,
    string Description);

public static class ZodiacAndGenerationHelper
{
    private static readonly Dictionary<string, ZodiacSignInfo> ZodiacSigns = new()
    {
        ["Aries"] = new ZodiacSignInfo(
            "الحمل", "Aries", "♈︎", "ناري", "#E11D48", "21 آذار - 19 نيسان",
            "<path d=\"M12 21a9 9 0 0 1-9-9c0-4.97 4.03-9 9-9s9 4.03 9 9a9 9 0 0 1-9 9zM6 9c1.5-2 3.5-3 6-3s4.5 1 6 3M12 6v12\" />",
            "حيوي، قيادي، ومبادر"),
        ["Taurus"] = new ZodiacSignInfo(
            "الثور", "Taurus", "♉︎", "ترابي", "#059669", "20 نيسان - 20 أيار",
            "<path d=\"M12 21a6 6 0 1 0 0-12 6 6 0 0 0 0 12zM6 5c1 3 3 4 6 4s5-1 6-4\" />",
            "صبور، موثوق، وعملي"),
        ["Gemini"] = new ZodiacSignInfo(
            "الجوزاء", "Gemini", "♊︎", "هوائي", "#0284C7", "21 أيار - 20 حزيران",
            "<path d=\"M6 4h12M6 20h12M9 4v16M15 4v16\" />",
            "ذكي، مرن، ومتواصل بارع"),
        ["Cancer"] = new ZodiacSignInfo(
            "السرطان", "Cancer", "♋︎", "مائي", "#4F46E5", "21 حزيران - 22 تموز",
            "<path d=\"M7 9a3 3 0 1 0 6 0A3 3 0 0 0 7 9zm4 6a3 3 0 1 0 6 0 3 3 0 0 0-6 0zM3 9c2-3 5-4 8-4M21 15c-2 3-5 4-8 4\" />",
            "مخلص، ملهم، ومتعاطف"),
        ["Leo"] = new ZodiacSignInfo(
            "الأسد", "Leo", "♌︎", "ناري", "#D97706", "23 تموز - 22 آب",
            "<path d=\"M7 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8zm0-4c2 0 4-2 5-5s3-3 5-1 1 5-2 7-5 3-5 3\" />",
            "واثق، كريم، وشغوف"),
        ["Virgo"] = new ZodiacSignInfo(
            "العذراء", "Virgo", "♍︎", "ترابي", "#10B981", "23 آب - 22 أيلول",
            "<path d=\"M4 6v12M9 6v12M14 6v9c0 3 2 4 4 4s3-2 3-5-1-4-3-4\" />",
            "دقيق، تحليلي، ومثابر"),
        ["Libra"] = new ZodiacSignInfo(
            "الميزان", "Libra", "♎︎", "هوائي", "#38BDF8", "23 أيلول - 22 تشرين الأول",
            "<path d=\"M4 19h16M4 15h16M7 11c1-3 3-4 5-4s4 1 5 4\" />",
            "دبلوماسي، عادل، ومنسجم"),
        ["Scorpio"] = new ZodiacSignInfo(
            "العقرب", "Scorpio", "♏︎", "مائي", "#7C3AED", "23 تشرين الأول - 21 تشرين الثاني",
            "<path d=\"M4 6v12M9 6v12M14 6v10c0 2 2 3 4 1l3 3m0-3l-3 3\" />",
            "عميق، استراتيجي، وشجاع"),
        ["Sagittarius"] = new ZodiacSignInfo(
            "القوس", "Sagittarius", "♐︎", "ناري", "#EA580C", "22 تشرين الثاني - 21 كانون الأول",
            "<path d=\"M7 17l10-10m0 0h-6m6 0v6M5 19l4-4\" />",
            "متفائل، واسع الأفق، وصادق"),
        ["Capricorn"] = new ZodiacSignInfo(
            "الجدي", "Capricorn", "♑︎", "ترابي", "#047857", "22 كانون الأول - 19 كانون الثاني",
            "<path d=\"M5 6l4 12 3-9c1-3 4-3 5 0s0 7-3 7-3-2-3-4\" />",
            "منضبط، طموح، ومسؤول"),
        ["Aquarius"] = new ZodiacSignInfo(
            "الدلو", "Aquarius", "♒︎", "هوائي", "#2563EB", "20 كانون الثاني - 18 شباط",
            "<path d=\"M3 9l3-3 4 4 4-4 4 4 3-3M3 15l3-3 4 4 4-4 4 4 3-3\" />",
            "مبتكر، إنساني، ومستقل"),
        ["Pisces"] = new ZodiacSignInfo(
            "الحوت", "Pisces", "♓︎", "مائي", "#6366F1", "19 شباط - 20 آذار",
            "<path d=\"M5 4c3 4 3 12 0 16M19 4c-3 4-3 12 0 16M3 12h18\" />",
            "حكيم، بديهي، وودود")
    };

    public static ZodiacSignInfo GetZodiacSign(DateOnly? date)
    {
        if (!date.HasValue)
        {
            return new ZodiacSignInfo("غير محدد", "Unknown", "—", "عام", "#64748B", "غير متوفر", "", "—");
        }

        var m = date.Value.Month;
        var d = date.Value.Day;

        var key = (m, d) switch
        {
            (1, <= 19) => "Capricorn",
            (1, >= 20) => "Aquarius",
            (2, <= 18) => "Aquarius",
            (2, >= 19) => "Pisces",
            (3, <= 20) => "Pisces",
            (3, >= 21) => "Aries",
            (4, <= 19) => "Aries",
            (4, >= 20) => "Taurus",
            (5, <= 20) => "Taurus",
            (5, >= 21) => "Gemini",
            (6, <= 20) => "Gemini",
            (6, >= 21) => "Cancer",
            (7, <= 22) => "Cancer",
            (7, >= 23) => "Leo",
            (8, <= 22) => "Leo",
            (8, >= 23) => "Virgo",
            (9, <= 22) => "Virgo",
            (9, >= 23) => "Libra",
            (10, <= 22) => "Libra",
            (10, >= 23) => "Scorpio",
            (11, <= 21) => "Scorpio",
            (11, >= 22) => "Sagittarius",
            (12, <= 21) => "Sagittarius",
            _ => "Capricorn"
        };

        return ZodiacSigns.TryGetValue(key, out var info) ? info : ZodiacSigns["Aries"];
    }

    public static GenerationInfo GetGeneration(int? birthYear)
    {
        if (!birthYear.HasValue)
        {
            return new GenerationInfo("غير محدد", "Unknown", "—", "#64748B", "hr-badge", "غير مسجل سنة التولد");
        }

        var y = birthYear.Value;

        if (y >= 2013)
        {
            return new GenerationInfo("جيل ألفا", "Gen Alpha", "2013 - فما بعد", "#EC4899", "hr-badge-accent", "جيل الذكاء الاصطناعي والتفاعل فائق السرعة");
        }
        if (y >= 1997)
        {
            return new GenerationInfo("الجيل Z (Zoomers)", "Gen Z", "1997 - 2012", "#8B5CF6", "hr-badge-accent", "جيل الرقمنة الكاملة، الإبداع والابتكار السريع");
        }
        if (y >= 1981)
        {
            return new GenerationInfo("جيل الألفية (Millennials)", "Gen Y", "1981 - 1996", "#3B82F6", "hr-badge-ok", "جيل ريادة الأعمال، التكيف التقني، والعمل الجماعي");
        }
        if (y >= 1965)
        {
            return new GenerationInfo("الجيل X", "Gen X", "1965 - 1980", "#10B981", "hr-badge-warn", "جيل الخبرة العميقة، الاعتماد على الذات، والتوازن القيادي");
        }
        if (y >= 1946)
        {
            return new GenerationInfo("طفرة المواليد (Boomers)", "Baby Boomers", "1946 - 1964", "#F59E0B", "hr-badge", "جيل بناء المؤسسات، الانضباط المهني العالي، والوفاء المؤسسي");
        }

        return new GenerationInfo("الجيل الصامت (Silent)", "Silent Gen", "1928 - 1945", "#64748B", "hr-badge", "جيل التأسيس والحكمة والريادة التاريخية");
    }

    public static int DaysUntilBirthday(DateOnly birthDate, DateOnly today)
    {
        var nextBday = new DateOnly(today.Year, birthDate.Month, Math.Min(birthDate.Day, DateTime.DaysInMonth(today.Year, birthDate.Month)));
        if (nextBday < today)
        {
            var nextYear = today.Year + 1;
            nextBday = new DateOnly(nextYear, birthDate.Month, Math.Min(birthDate.Day, DateTime.DaysInMonth(nextYear, birthDate.Month)));
        }

        return nextBday.DayNumber - today.DayNumber;
    }

    public static int CalculateAge(DateOnly birthDate, DateOnly asOfDate)
    {
        var age = asOfDate.Year - birthDate.Year;
        if (asOfDate.Month < birthDate.Month || (asOfDate.Month == birthDate.Month && asOfDate.Day < birthDate.Day))
        {
            age--;
        }
        return Math.Max(0, age);
    }

    public static string FormatMonthArabic(int month) => month switch
    {
        1 => "كانون الثاني (يناير)",
        2 => "شباط (فبراير)",
        3 => "آذار (مارس)",
        4 => "نيسان (أبريل)",
        5 => "أيار (مايو)",
        6 => "حزيران (يونيو)",
        7 => "تموز (يوليو)",
        8 => "آب (أغسطس)",
        9 => "أيلول (سبتمبر)",
        10 => "تشرين الأول (أكتوبر)",
        11 => "تشرين الثاني (نوفمبر)",
        12 => "كانون الأول (ديسمبر)",
        _ => $"شهر {month}"
    };
}
