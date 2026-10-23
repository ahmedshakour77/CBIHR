using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Data;

/// <summary>
/// بذر وتوحيد أنواع العقوبات الانضباطية وتحديد مدد تأخير الترفيع والعلاوة
/// مستنداً إلى قانون انضباط موظفي الدولة والجدول المعتمد
/// </summary>
public static class PenaltyTypeDataSeeder
{
    public sealed record PenaltyDef(string Name, int DelayMonths, string Description, bool IsDismissal = false);

    public static readonly PenaltyDef[] DefaultPenalties =
    [
        new("لفت نظر", 3, "تأخير الترفيع والعلاوة لمدة 3 أشهر"),
        new("إنذار", 6, "تأخير الترفيع والعلاوة لمدة 6 أشهر"),
        new("عقوبة التوبيخ", 12, "تأخير الترفيع والعلاوة لمدة سنة (12 شهر)"),
        new("إنقاص راتب", 24, "تأخير الترفيع والعلاوة لمدة سنتين (24 شهر)"),
        new("تنزيل درجة", 24, "تأخير الترفيع لمدة سنتين (24 شهر)"),
        new("قطع راتب", 5, "5 أشهر إذا كانت مدة القطع لا تتجاوز 5 أيام، وشهر عن كل يوم إضافي"),
        new("سحب يد", 0, "متغيرة المدة حسب مدة سحب اليد المحددة بالأمر"),
        new("عزل", 0, "يحرم الموظف من كل الترفيعات", IsDismissal: true),
        new("إلغاء العزل", 0, "بدون تأثير"),
        new("تنبيه", 0, "بدون تأثير"),
        new("توجيه", 0, "بدون تأثير"),
        new("توجيه / بصمة", 0, "بدون تأثير"),
        new("توجيه / باج", 0, "بدون تأثير"),
        new("إلغاء سحب اليد", 0, "بدون تأثير"),
        new("رفع عقوبة الانذار", 0, "بدون تأثير"),
        new("انتهاء عقوبة تنزيل الدرجة", 0, "بدون تأثير"),
        new("رفع عقوبة لفت النظر", 0, "بدون تأثير"),
        new("إلغاء عقوبة التوبيخ", 0, "بدون تأثير"),
        new("تنبيه/توجيهات", 0, "بدون تأثير"),
        new("المباشرة بعد سحب اليد", 0, "بدون تأثير"),
        new("هدر المال العام", 0, "تحدد حسب الإجراء القانوني")
    ];

    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var existing = await db.PenaltyTypes.ToListAsync();
        var existingDict = existing.ToDictionary(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase);

        var needsSave = false;

        foreach (var def in DefaultPenalties)
        {
            if (existingDict.TryGetValue(def.Name, out var existingType))
            {
                if (existingType.DelayMonths != def.DelayMonths || existingType.Description != def.Description)
                {
                    existingType.DelayMonths = def.DelayMonths;
                    existingType.Description = def.Description;
                    needsSave = true;
                }
            }
            else
            {
                db.PenaltyTypes.Add(new PenaltyType
                {
                    Name = def.Name,
                    DelayMonths = def.DelayMonths,
                    Description = def.Description
                });
                needsSave = true;
            }
        }

        if (needsSave)
        {
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// احتساب أثر العقوبة الانضباطية (أشهر التأخير وهل هناك حرمان من الترفيع)
    /// </summary>
    public static (int DelayMonths, bool IsBarredFromPromotion) CalculateImpact(PenaltyType? pType, string? penaltyNotes = null)
    {
        var name = pType?.Name?.Trim() ?? string.Empty;
        var clean = name.Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا").Replace("ة", "ه");

        // 1. حالات العزل (حرمان كامل من كل الترفيعات)
        if (clean.Contains("عزل") && !clean.Contains("الغاء") && !clean.Contains("رفع"))
        {
            return (0, true);
        }

        // 2. الحالات التي لا تؤثر (0 شهر)
        if (clean.Contains("الغاء") || clean.Contains("رفع") || clean.Contains("انتهاء") ||
            clean.Contains("تنبيه") || clean.Contains("توجيه") || clean.Contains("مباشره"))
        {
            return (0, false);
        }

        // 3. تنزيل درجة (24 شهر = سنتان)
        if (clean.Contains("تنزيل درجه"))
        {
            return (24, false);
        }

        // 4. إنقاص راتب (24 شهر = سنتان)
        if (clean.Contains("انقاص راتب"))
        {
            return (24, false);
        }

        // 5. التوبيخ (12 شهر = سنة)
        if (clean.Contains("توبيخ"))
        {
            return (12, false);
        }

        // 6. إنذار (6 أشهر)
        if (clean.Contains("انذار"))
        {
            return (6, false);
        }

        // 7. لفت نظر (3 أشهر)
        if (clean.Contains("لفت نظر"))
        {
            return (3, false);
        }

        // 8. قطع راتب: 5 أشهر إذا كانت مدة القطع لا تتجاوز 5 أيام، وشهر عن كل يوم إضافي
        if (clean.Contains("قطع راتب") || clean.Contains("قطع"))
        {
            var days = ExtractDaysFromNotes(penaltyNotes);
            if (days.HasValue && days.Value > 0)
            {
                if (days.Value <= 5) return (5, false);
                return (5 + (days.Value - 5), false);
            }
            return (5, false); // الافتراضي لا يتجاوز 5 أيام = 5 أشهر
        }

        // 9. سحب يد: يعتمد على المدة المسجلة في الملاحظات أو 0 كافتراضي
        if (clean.Contains("سحب يد"))
        {
            var months = ExtractMonthsFromNotes(penaltyNotes);
            return (months ?? 0, false);
        }

        // 10. الاعتماد على DelayMonths المحدد في جدول الأنواع إذا كان متوفراً
        if (pType != null && pType.DelayMonths > 0)
        {
            return (pType.DelayMonths, false);
        }

        return (0, false);
    }

    private static int? ExtractDaysFromNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;

        var m = Regex.Match(notes, @"(?<num>\d+)\s*(يوم|ايام|أيام)", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups["num"].Value, out var val))
        {
            return val;
        }
        return null;
    }

    private static int? ExtractMonthsFromNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;

        var m = Regex.Match(notes, @"(?<num>\d+)\s*(شهر|اشهر|أشهر)", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups["num"].Value, out var val))
        {
            return val;
        }
        return null;
    }
}
