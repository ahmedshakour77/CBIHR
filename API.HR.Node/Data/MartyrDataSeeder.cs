using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Data;

public static class MartyrDataSeeder
{
    public static readonly string[] DefaultMartyrdomTypes =
    [
        "ضحايا العمليات الحربية والأخطاء العسكرية والعمليات الإرهابية",
        "شهداء العمليات العسكرية والحشد الشعبي",
        "شهداء جرائم حزب البعث (النظام البائد)",
        "شهداء الواجب والخدمة وقوى الأمن الداخلي"
    ];

    public static readonly (string Name, int Degree)[] DefaultKinships =
    [
        ("أب", 1),
        ("أم", 1),
        ("ابن", 1),
        ("ابنة", 1),
        ("زوج", 1),
        ("زوجة", 1),
        ("أخ", 2),
        ("أخت", 2),
        ("جد", 2),
        ("جدة", 2),
        ("حفيد", 2),
        ("حفيدة", 2)
    ];

    public static async Task SeedAsync(ApplicationDbContext db)
    {
        // 1. Seed Martyrdom Types
        var existingTypes = await db.MartyrdomTypes
            .Select(t => t.Name.Trim())
            .ToListAsync();
        var existingTypeSet = new HashSet<string>(existingTypes, StringComparer.OrdinalIgnoreCase);

        var needsSave = false;
        foreach (var typeName in DefaultMartyrdomTypes)
        {
            if (!existingTypeSet.Contains(typeName))
            {
                db.MartyrdomTypes.Add(new MartyrdomType { Name = typeName });
                needsSave = true;
            }
        }

        // 2. Seed Kinships
        var existingKinships = await db.MartyrKinships
            .Select(k => k.Name.Trim())
            .ToListAsync();
        var existingKinshipSet = new HashSet<string>(existingKinships, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, degree) in DefaultKinships)
        {
            if (!existingKinshipSet.Contains(name))
            {
                db.MartyrKinships.Add(new MartyrKinship { Name = name, Degree = degree });
                needsSave = true;
            }
        }

        if (needsSave)
        {
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// توحيد صيغ صلة القرابة الشائعة (مثل اب -> أب، اخ -> أخ، إبن -> ابن، الزوجة -> زوجة، إلخ)
    /// </summary>
    public static string NormalizeKinship(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var clean = raw.Trim();
        if (clean.StartsWith("ال") && clean.Length > 2)
        {
            clean = clean[2..].Trim();
        }

        var normalized = clean.Replace("ة", "ه").Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا");

        if (normalized.Equals("اب", StringComparison.OrdinalIgnoreCase) || normalized.Equals("والد", StringComparison.OrdinalIgnoreCase))
            return "أب";

        if (normalized.Equals("ام", StringComparison.OrdinalIgnoreCase) || normalized.Equals("والده", StringComparison.OrdinalIgnoreCase))
            return "أم";

        if (normalized.Equals("ابن", StringComparison.OrdinalIgnoreCase) || normalized.Equals("ولد", StringComparison.OrdinalIgnoreCase))
            return "ابن";

        if (normalized.Equals("ابنه", StringComparison.OrdinalIgnoreCase) || normalized.Equals("بنت", StringComparison.OrdinalIgnoreCase))
            return "ابنة";

        if (normalized.Equals("اخ", StringComparison.OrdinalIgnoreCase) || normalized.Equals("شقيق", StringComparison.OrdinalIgnoreCase))
            return "أخ";

        if (normalized.Equals("اخت", StringComparison.OrdinalIgnoreCase) || normalized.Equals("شقيقه", StringComparison.OrdinalIgnoreCase))
            return "أخت";

        if (normalized.Equals("زوج", StringComparison.OrdinalIgnoreCase) || normalized.Equals("زوجها", StringComparison.OrdinalIgnoreCase))
            return "زوج";

        if (normalized.Equals("زوجه", StringComparison.OrdinalIgnoreCase) || normalized.Equals("زوجته", StringComparison.OrdinalIgnoreCase))
            return "زوجة";

        if (normalized.Equals("جد", StringComparison.OrdinalIgnoreCase))
            return "جد";

        if (normalized.Equals("جده", StringComparison.OrdinalIgnoreCase))
            return "جدة";

        if (normalized.Equals("حفيد", StringComparison.OrdinalIgnoreCase))
            return "حفيد";

        if (normalized.Equals("حفيده", StringComparison.OrdinalIgnoreCase))
            return "حفيدة";

        return raw.Trim();
    }
}
