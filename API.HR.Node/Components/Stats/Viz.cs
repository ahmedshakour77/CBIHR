using System.Globalization;
using API.HR.Node.Services.Dashboard;

namespace API.HR.Node.Components.Stats;

/// <summary>Shared arithmetic and labels for the dashboard charts.</summary>
internal static class Viz
{
    internal static double Share(int count, int total) => total > 0 ? count * 100.0 / total : 0;

    /// <summary>"12.4%"; under a tenth of a percent reads "&lt;0.1%" rather than a misleading zero.</summary>
    internal static string Pct(int count, int total)
    {
        var p = Share(count, total);
        if (p > 0 && p < 0.1) return "<0.1%";
        return p.ToString(p >= 10 ? "0" : "0.#", CultureInfo.InvariantCulture) + "%";
    }

    internal static string Css(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Six categorical series; the seventh and later share the neutral "other" fill.</summary>
    internal static string Series(int index) => index < 6 ? $"is-s{index + 1}" : "is-other";

    /// <summary>The folded tail's key. It is a filter-free bucket, never a value to pick.</summary>
    internal const string Other = "أخرى";

    /// <summary>A value as it reads on screen: data values pass through, the folded tail is translated.</summary>
    internal static string Display(string name, bool kurdish) => kurdish && name == Other ? "هی تر" : name;

    /// <summary>
    /// Keeps the first <paramref name="max"/> values in the order given and folds the tail into
    /// one "other" bucket, except any picked value, which stays its own slice so its mark shows.
    /// A tail of one is shown as itself rather than renamed.
    /// </summary>
    internal static List<StatItem> Fold(IEnumerable<StatItem> items, int max, IReadOnlySet<string>? selected)
    {
        var live = items.Where(i => i.Count > 0).ToList();
        if (live.Count <= max + 1) return live;
        var keep = live.Where((item, i) => i < max || selected?.Contains(item.Name) == true).ToList();
        var rest = live.Except(keep).ToList();
        return rest.Count <= 1 ? [.. keep, .. rest] : [.. keep, new StatItem(Other, rest.Sum(i => i.Count))];
    }

    internal static string NoData(bool kurdish) => kurdish ? "هیچ داتایەک لە چوارچێوەی ئێستادا نییە." : "لا توجد بيانات ضمن النطاق الحالي.";
}
