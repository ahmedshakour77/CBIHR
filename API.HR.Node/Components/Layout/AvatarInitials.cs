namespace API.HR.Node.Components.Layout;

public static class AvatarInitials
{
    public static string FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "؟";
        }

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 1
            ? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
            : $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
    }
}
