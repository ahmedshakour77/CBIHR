using System.Text;

namespace API.HR.Node.Endpoints;

/// <summary>
/// Normalizes lookup/text values so in-memory matching agrees with SQL Server's
/// collation, which ignores cosmetic characters like the Arabic tatweel. Without
/// this, "مسـلــــم" looks new to C# yet collides with "مسلم" in the database.
/// </summary>
internal static class LookupNormalization
{
    private static readonly char[] IgnorableChars =
    {
        (char)0x0640, // Arabic Tatweel / Kashida (letter-stretching)
        (char)0x200C, // Zero Width Non-Joiner
        (char)0x200D, // Zero Width Joiner
    };

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.IndexOfAny(IgnorableChars) < 0)
        {
            return trimmed;
        }

        var builder = new StringBuilder(trimmed.Length);

        foreach (var character in trimmed)
        {
            if (Array.IndexOf(IgnorableChars, character) < 0)
            {
                builder.Append(character);
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
