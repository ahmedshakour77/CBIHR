using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Data;

public static class MaritalStatusDataSeeder
{
    public static readonly string[] DefaultStatuses =
    [
        "أعزب",
        "متزوج",
        "مطلق",
        "أرمل"
    ];

    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var existingNames = await db.MaritalStatuses
            .Select(s => s.Name.Trim())
            .ToListAsync();

        var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        var needsSave = false;
        foreach (var status in DefaultStatuses)
        {
            if (!existingSet.Contains(status))
            {
                db.MaritalStatuses.Add(new MaritalStatus { Name = status });
                needsSave = true;
            }
        }

        if (needsSave)
        {
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// يقوم بتوحيد صيغ الحالة الاجتماعية الشائعة (مثل عزباء -> أعزب، متزوجة -> متزوج، مطلقة -> مطلق، أرملة -> أرمل)
    /// </summary>
    public static string NormalizeStatus(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var clean = raw.Trim().Replace("ة", "ه").Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا");

        if (clean.Contains("عزب") || clean.Contains("اعزب") || clean.Contains("عزوب"))
            return "أعزب";

        if (clean.Contains("تزوج") || clean.Contains("متزوج"))
            return "متزوج";

        if (clean.Contains("طلق") || clean.Contains("مطلق"))
            return "مطلق";

        if (clean.Contains("ارمل") || clean.Contains("رمل"))
            return "أرمل";

        return raw.Trim();
    }
}
