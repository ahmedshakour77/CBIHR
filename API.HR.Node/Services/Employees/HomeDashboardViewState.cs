namespace API.HR.Node.Services.Employees;

/// <summary>Retains the user's dashboard choices across navigation in their Blazor circuit.</summary>
internal sealed class HomeDashboardViewState
{
    private readonly Dictionary<string, HashSet<string>> filters = [];
    internal HashSet<string> Get(string category) => filters.TryGetValue(category, out var values) ? values.ToHashSet() : [];
    internal void Set(string category, HashSet<string> values) => filters[category] = values.ToHashSet();
}
