using API.HR.Node.Services.Employees;

namespace API.HR.Node.Services.Dashboard;

/// <summary>Every field the dashboard's general filter can narrow by.</summary>
public enum WorkforceDimension
{
    Formation, Unit, Status, Grade, Stage, Education, Specialization, JobTitle, Position,
    Gender, Nationality, Religion, Marital, AgeBand, ServiceBand, Generation, Governorate, Digital
}

public sealed record StatItem(string Name, int Count);

/// <summary>One dimension's values and how many employees each would leave in scope.</summary>
public sealed record WorkforceFacet(WorkforceDimension Dimension, IReadOnlyList<string> Options, IReadOnlyDictionary<string, int> Counts);

public sealed record PyramidBand(string Band, int Male, int Female, int Unset);

public sealed record HeatmapCellStat(int Stage, int Count);
public sealed record HeatmapRowStat(string Grade, int Sort, int Total, IReadOnlyList<HeatmapCellStat> Cells);

public sealed record ForecastYear(int Year, int Retirees, int Supervisory, int Cumulative, int Projected, string TopFormation, int TopFormationCount);

public sealed record LeaveTodayItem(WorkforceRow Employee, string Type, DateOnly End);

public sealed record RecordMeter(string Key, string Label, int Count);

/// <summary>
/// The general filter: OR inside one field, AND across fields. The digital-file field
/// keeps <see cref="DashboardDigitalFilter"/>'s rule — union inside a category,
/// intersection between categories.
/// </summary>
public sealed class WorkforceFilter
{
    private readonly Dictionary<WorkforceDimension, HashSet<string>> values = [];

    public IReadOnlySet<string> this[WorkforceDimension dimension] =>
        values.TryGetValue(dimension, out var set) ? set : EmptySet;

    private static readonly HashSet<string> EmptySet = [];

    public IEnumerable<WorkforceDimension> Active => values.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key);
    public int Count => values.Values.Sum(v => v.Count);
    public bool IsEmpty => Count == 0;

    public void Set(WorkforceDimension dimension, IEnumerable<string> selected)
    {
        var set = selected.ToHashSet();
        if (set.Count == 0) values.Remove(dimension); else values[dimension] = set;
    }

    /// <summary>Adds the value, or removes it when it is already selected.</summary>
    public void Toggle(WorkforceDimension dimension, string value)
    {
        var set = this[dimension].ToHashSet();
        if (!set.Remove(value)) set.Add(value);
        Set(dimension, set);
    }

    public void Clear() => values.Clear();

    public bool Matches(WorkforceRow row, DateOnly today, WorkforceDimension? except = null)
    {
        foreach (var (dimension, selected) in values)
        {
            if (selected.Count == 0 || dimension == except) continue;
            if (dimension == WorkforceDimension.Digital)
            {
                if (!MatchesDigital(row, selected)) return false;
            }
            else if (!selected.Contains(WorkforceStats.ValueOf(row, dimension, today)))
            {
                return false;
            }
        }
        return true;
    }

    private static bool MatchesDigital(WorkforceRow row, IReadOnlySet<string> selected)
    {
        var withPhoto = selected.Contains(DashboardDigitalFilter.WithPhoto);
        var withoutPhoto = selected.Contains(DashboardDigitalFilter.WithoutPhoto);
        if (withPhoto != withoutPhoto && row.HasPhoto != withPhoto) return false;

        var complete = selected.Contains(DashboardDigitalFilter.CompleteContacts);
        var missing = selected.Contains(DashboardDigitalFilter.MissingContacts);
        if (complete != missing && row.HasContacts != complete) return false;

        return !selected.Contains(DashboardDigitalFilter.LinkedAccount) || row.HasAccount;
    }
}

/// <summary>Everything the home dashboard draws, for one filter over one snapshot.</summary>
public sealed class WorkforceStatsResult
{
    public required IReadOnlyList<WorkforceRow> Rows { get; init; }
    public int Total => Rows.Count;
    public int BaseTotal { get; init; }
    public required IReadOnlyDictionary<WorkforceDimension, WorkforceFacet> Facets { get; init; }
    public required IReadOnlyDictionary<WorkforceDimension, IReadOnlyList<StatItem>> Breakdowns { get; init; }

    /// <summary>
    /// Each field counted with every other field's filter applied but not its own, so a chart
    /// that picks values for a field keeps its siblings on screen and marks the picked ones.
    /// </summary>
    public required IReadOnlyDictionary<WorkforceDimension, IReadOnlyList<StatItem>> Context { get; init; }

    public int Female { get; init; }
    public int Male { get; init; }
    public double AverageAge { get; init; }
    public double AverageService { get; init; }
    public int Supervisory { get; init; }
    public int Postgraduate { get; init; }
    public int NewHires { get; init; }
    public int Age58Plus { get; init; }
    /// <summary>Reach the retirement age after today and within the next twelve months.</summary>
    public int RetiringIn12Months { get; init; }
    /// <summary>Already at or past the retirement age and still on the active rolls.</summary>
    public int PastRetirementAge { get; init; }
    public int MartyrFamilies { get; init; }

    public required IReadOnlyList<PyramidBand> Pyramid { get; init; }
    public required IReadOnlyList<StatItem> HiresByYear { get; init; }
    public required IReadOnlyList<StatItem> YearsInTitle { get; init; }
    public required IReadOnlyList<StatItem> LeaveBalanceBands { get; init; }
    public required IReadOnlyList<HeatmapRowStat> Heatmap { get; init; }
    public int HeatmapMax { get; init; }
    public required IReadOnlyList<ForecastYear> Forecast { get; init; }

    public required IReadOnlyList<LeaveTodayItem> OnLeaveToday { get; init; }
    public int ReturningThisWeek { get; init; }
    public required IReadOnlyList<StatItem> LeaveTodayByType { get; init; }
    public required IReadOnlyList<StatItem> LeaveYearByType { get; init; }
    public int LeaveYearDays { get; init; }

    public int LettersTotal { get; init; }
    public int HonoredEmployees { get; init; }
    public required IReadOnlyList<StatItem> LettersByType { get; init; }
    public required IReadOnlyList<WorkforceRow> TopHonored { get; init; }
    public string LettersSeniority { get; init; } = string.Empty;

    public int PenaltiesTotal { get; init; }
    public int PenalizedEmployees { get; init; }
    public int PenaltiesLast12Months { get; init; }
    public required IReadOnlyList<StatItem> PenaltiesByType { get; init; }

    public int CoursesTotal { get; init; }
    public int TrainedEmployees { get; init; }
    public int VehicleOwners { get; init; }
    public int VehiclesTotal { get; init; }
    public int VehiclesAuthorized { get; init; }

    public required IReadOnlyList<RecordMeter> Records { get; init; }
    public int RecordsComplete { get; init; }
}

public static class WorkforceStats
{
    public const string Female = "إناث";
    public const string Male = "ذكور";
    public const string NoPosition = "بدون منصب";
    public const string NotRecorded = "غير مسجل";
    public const int RetirementAge = 60;

    public static readonly string[] AgeBands =
        ["أقل من 25", "25 إلى 29", "30 إلى 34", "35 إلى 39", "40 إلى 44", "45 إلى 49", "50 إلى 54", "55 إلى 59", "60 فأكثر"];

    public static readonly string[] ServiceBands =
        ["أقل من 5 سنوات", "من 5 إلى 10 سنوات", "من 10 إلى 20 سنة", "أكثر من 20 سنة"];

    public static readonly (string Name, int From, int To)[] Generations =
    [
        ("الجيل Z", 1997, 2012),
        ("جيل الألفية", 1981, 1996),
        ("الجيل X", 1965, 1980),
        ("طفرة المواليد", 1946, 1964)
    ];

    public static readonly string[] DigitalValues =
    [
        DashboardDigitalFilter.WithPhoto, DashboardDigitalFilter.WithoutPhoto,
        DashboardDigitalFilter.CompleteContacts, DashboardDigitalFilter.MissingContacts,
        DashboardDigitalFilter.LinkedAccount
    ];

    private static readonly string[] Grades =
        ["العليا أ", "العليا ب", "الخاصة أ", "الخاصة ب", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"];

    public static string Label(WorkforceDimension dimension) => dimension switch
    {
        WorkforceDimension.Formation => "التشكيل",
        WorkforceDimension.Unit => "الوحدة الإدارية",
        WorkforceDimension.Status => "الحالة الوظيفية",
        WorkforceDimension.Grade => "الدرجة",
        WorkforceDimension.Stage => "المرحلة",
        WorkforceDimension.Education => "التحصيل الدراسي",
        WorkforceDimension.Specialization => "التخصص",
        WorkforceDimension.JobTitle => "العنوان الوظيفي",
        WorkforceDimension.Position => "المنصب",
        WorkforceDimension.Gender => "الجنس",
        WorkforceDimension.Nationality => "الجنسية",
        WorkforceDimension.Religion => "الديانة",
        WorkforceDimension.Marital => "الحالة الزوجية",
        WorkforceDimension.AgeBand => "الفئة العمرية",
        WorkforceDimension.ServiceBand => "مدة الخدمة",
        WorkforceDimension.Generation => "الجيل",
        WorkforceDimension.Governorate => "محافظة السكن",
        WorkforceDimension.Digital => "الملف الرقمي",
        _ => dimension.ToString()
    };

    public static string Label(WorkforceDimension dimension, bool kurdish) => !kurdish ? Label(dimension) : dimension switch
    {
        WorkforceDimension.Formation => "پێکهاتە",
        WorkforceDimension.Unit => "یەکەی کارگێڕی",
        WorkforceDimension.Status => "باری وەزیفی",
        WorkforceDimension.Grade => "پلە",
        WorkforceDimension.Stage => "قۆناغ",
        WorkforceDimension.Education => "ئاستی خوێندن",
        WorkforceDimension.Specialization => "پسپۆڕی",
        WorkforceDimension.JobTitle => "ناونیشانی وەزیفی",
        WorkforceDimension.Position => "پۆست",
        WorkforceDimension.Gender => "ڕەگەز",
        WorkforceDimension.Nationality => "ڕەگەزنامە",
        WorkforceDimension.Religion => "ئایین",
        WorkforceDimension.Marital => "باری خێزانی",
        WorkforceDimension.AgeBand => "پۆلی تەمەن",
        WorkforceDimension.ServiceBand => "ماوەی خزمەت",
        WorkforceDimension.Generation => "نەوە",
        WorkforceDimension.Governorate => "پارێزگای نیشتەجێبوون",
        WorkforceDimension.Digital => "دۆسیەی دیجیتاڵی",
        _ => dimension.ToString()
    };

    public static string NormalizeGender(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return WorkforceSnapshot.Unset;
        var n = name.Trim();
        if (n.Contains("أنث") || n.Contains("انث") || n.Equals("Female", StringComparison.OrdinalIgnoreCase)) return Female;
        if (n.Contains("ذكر") || n.Equals("Male", StringComparison.OrdinalIgnoreCase)) return Male;
        return WorkforceSnapshot.Unset;
    }

    /// <summary>Hired within the last two years: after this day two years ago, and not in the future.</summary>
    public static bool IsNewHire(WorkforceRow row, DateOnly today) =>
        row.HireDate is { } h && h > today.AddYears(-2) && h <= today;

    public static DateOnly? RetirementDate(WorkforceRow row) => row.BirthDate?.AddYears(RetirementAge);

    /// <summary>Turns the retirement age after today and on or before this day next year.</summary>
    public static bool IsRetiringWithinYear(WorkforceRow row, DateOnly today) =>
        RetirementDate(row) is { } d && d > today && d <= today.AddYears(1);

    /// <summary>Has reached the retirement age (today included) and is still counted in the workforce.</summary>
    public static bool IsPastRetirementAge(WorkforceRow row, DateOnly today) =>
        RetirementDate(row) is { } d && d <= today;

    public static int? YearsBetween(DateOnly? from, DateOnly today)
    {
        if (from is not { } d || d > today) return null;
        var years = today.Year - d.Year;
        if (d > today.AddYears(-years)) years--;
        return Math.Max(0, years);
    }

    public static string AgeBand(int? age) => age switch
    {
        null or < 18 or > 90 => NotRecorded,
        < 25 => AgeBands[0],
        < 30 => AgeBands[1],
        < 35 => AgeBands[2],
        < 40 => AgeBands[3],
        < 45 => AgeBands[4],
        < 50 => AgeBands[5],
        < 55 => AgeBands[6],
        < 60 => AgeBands[7],
        _ => AgeBands[8]
    };

    public static string ServiceBand(int? years) => years switch
    {
        null => NotRecorded,
        < 5 => ServiceBands[0],
        <= 10 => ServiceBands[1],
        <= 20 => ServiceBands[2],
        _ => ServiceBands[3]
    };

    public static string Generation(DateOnly? birth)
    {
        if (birth is not { } b) return NotRecorded;
        foreach (var (name, from, to) in Generations)
            if (b.Year >= from && b.Year <= to) return name;
        return b.Year < 1946 ? "الجيل الصامت" : "جيل ألفا";
    }

    public static string ValueOf(WorkforceRow row, WorkforceDimension dimension, DateOnly today) => dimension switch
    {
        WorkforceDimension.Formation => row.Formation,
        WorkforceDimension.Unit => row.Unit,
        WorkforceDimension.Status => row.Status,
        WorkforceDimension.Grade => row.Grade,
        WorkforceDimension.Stage => row.Stage,
        WorkforceDimension.Education => row.Education,
        WorkforceDimension.Specialization => row.Specialization,
        WorkforceDimension.JobTitle => row.JobTitle,
        WorkforceDimension.Position => row.Position ?? NoPosition,
        WorkforceDimension.Gender => row.Gender,
        WorkforceDimension.Nationality => row.Nationality,
        WorkforceDimension.Religion => row.Religion,
        WorkforceDimension.Marital => row.Marital,
        WorkforceDimension.AgeBand => AgeBand(YearsBetween(row.BirthDate, today)),
        WorkforceDimension.ServiceBand => ServiceBand(YearsBetween(row.HireDate, today)),
        WorkforceDimension.Generation => Generation(row.BirthDate),
        WorkforceDimension.Governorate => row.Governorate ?? NotRecorded,
        _ => string.Empty
    };

    /// <summary>Bands keep their natural order; every other field ranks by size.</summary>
    private static IEnumerable<string> Order(WorkforceDimension dimension, IReadOnlyDictionary<string, int> counts)
    {
        string[]? natural = dimension switch
        {
            WorkforceDimension.AgeBand => AgeBands,
            WorkforceDimension.ServiceBand => ServiceBands,
            WorkforceDimension.Generation => ["جيل ألفا", .. Generations.Select(g => g.Name), "الجيل الصامت"],
            WorkforceDimension.Digital => DigitalValues,
            _ => null
        };
        if (natural is not null)
            return natural.Where(counts.ContainsKey).Concat(counts.Keys.Where(k => !natural.Contains(k)));
        return counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key);
    }

    private static Dictionary<string, int> CountBy(IEnumerable<WorkforceRow> rows, WorkforceDimension dimension, DateOnly today)
    {
        var counts = new Dictionary<string, int>();
        if (dimension == WorkforceDimension.Digital)
        {
            foreach (var r in rows)
            {
                Bump(counts, r.HasPhoto ? DashboardDigitalFilter.WithPhoto : DashboardDigitalFilter.WithoutPhoto);
                Bump(counts, r.HasContacts ? DashboardDigitalFilter.CompleteContacts : DashboardDigitalFilter.MissingContacts);
                if (r.HasAccount) Bump(counts, DashboardDigitalFilter.LinkedAccount);
            }
            return counts;
        }
        foreach (var r in rows) Bump(counts, ValueOf(r, dimension, today));
        return counts;
    }

    private static void Bump(Dictionary<string, int> counts, string key) =>
        counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;

    private static List<StatItem> Ranked(IEnumerable<string> keys) =>
        keys.GroupBy(k => k).Select(g => new StatItem(g.Key, g.Count()))
            .OrderByDescending(s => s.Count).ThenBy(s => s.Name, StringComparer.Ordinal).ToList();

    public static WorkforceStatsResult Compute(IReadOnlyList<WorkforceRow> scope, WorkforceFilter filter,
        WorkforceSnapshot snapshot, DateOnly today)
    {
        var rows = scope.Where(r => filter.Matches(r, today)).ToList();
        var ids = rows.Select(r => r.Id).ToHashSet();
        var dimensions = Enum.GetValues<WorkforceDimension>();

        // Each facet counts with every other field's filter applied, so a dropdown
        // shows what picking that value would leave, not what is already picked.
        var facets = new Dictionary<WorkforceDimension, WorkforceFacet>();
        var breakdowns = new Dictionary<WorkforceDimension, IReadOnlyList<StatItem>>();
        var context = new Dictionary<WorkforceDimension, IReadOnlyList<StatItem>>();
        foreach (var d in dimensions)
        {
            var facetRows = filter[d].Count == 0 ? rows : scope.Where(r => filter.Matches(r, today, except: d));
            var counts = CountBy(facetRows, d, today);
            context[d] = Order(d, counts).Select(k => new StatItem(k, counts[k])).ToList();
            var all = CountBy(scope, d, today);
            foreach (var key in all.Keys) counts.TryAdd(key, 0);
            facets[d] = new WorkforceFacet(d, Order(d, all).ToList(), counts);

            var own = filter[d].Count == 0 ? counts.Where(kv => kv.Value > 0).ToDictionary() : CountBy(rows, d, today);
            breakdowns[d] = Order(d, own).Select(k => new StatItem(k, own[k])).ToList();
        }

        var ages = rows.Select(r => YearsBetween(r.BirthDate, today)).Where(a => a is >= 18 and <= 90).Select(a => a!.Value).ToList();
        var service = rows.Select(r => YearsBetween(r.HireDate, today)).Where(s => s.HasValue).Select(s => s!.Value).ToList();

        // The pyramid picks age bands, so like any picking chart it ignores the age-band filter.
        var pyramidRows = filter[WorkforceDimension.AgeBand].Count == 0
            ? rows
            : scope.Where(r => filter.Matches(r, today, except: WorkforceDimension.AgeBand)).ToList();
        var pyramid = AgeBands.Select(band =>
        {
            var inBand = pyramidRows.Where(r => AgeBand(YearsBetween(r.BirthDate, today)) == band).ToList();
            return new PyramidBand(band, inBand.Count(r => r.Gender == Male), inBand.Count(r => r.Gender == Female),
                inBand.Count(r => r.Gender != Male && r.Gender != Female));
        }).ToList();

        var firstYear = today.Year - 19;
        var hires = rows.Where(r => r.HireDate.HasValue && r.HireDate.Value <= today).GroupBy(r => Math.Max(r.HireDate!.Value.Year, firstYear - 1))
            .ToDictionary(g => g.Key, g => g.Count());
        var hiresByYear = Enumerable.Range(firstYear - 1, 21)
            .Select(y => new StatItem(y < firstYear ? $"قبل {firstYear}" : y.ToString(), hires.GetValueOrDefault(y))).ToList();

        string[] titleBands = ["أقل من سنة", "1 إلى 2", "3 إلى 4", "5 إلى 9", "10 فأكثر"];
        var yearsInTitle = titleBands.Select(b => new StatItem(b, 0)).ToList();
        foreach (var r in rows)
        {
            var y = YearsBetween(r.LastJobTitleDate, today);
            if (y is null) continue;
            var i = y < 1 ? 0 : y <= 2 ? 1 : y <= 4 ? 2 : y <= 9 ? 3 : 4;
            yearsInTitle[i] = yearsInTitle[i] with { Count = yearsInTitle[i].Count + 1 };
        }

        string[] balanceBands = ["لا رصيد", "1 إلى 30", "31 إلى 90", "91 إلى 180", "أكثر من 180"];
        var leaveBalance = balanceBands.Select(b => new StatItem(b, 0)).ToList();
        foreach (var r in rows)
        {
            if (r.LeaveBalance is not { } b) continue;
            var i = b <= 0 ? 0 : b <= 30 ? 1 : b <= 90 ? 2 : b <= 180 ? 3 : 4;
            leaveBalance[i] = leaveBalance[i] with { Count = leaveBalance[i].Count + 1 };
        }

        var cells = rows.GroupBy(r => (Grade: NormalizeGradeCode(r.GradeCode, r.Grade), Stage: NormalizeStageNumber(r.StageNumber, r.StageCode, r.Stage)))
            .ToDictionary(g => g.Key, g => g.Count());
        var heatmap = Grades.Select((g, i) =>
        {
            var row = Enumerable.Range(1, 11).Select(s => new HeatmapCellStat(s, cells.GetValueOrDefault((g, s)))).ToList();
            return new HeatmapRowStat(g, i, row.Sum(c => c.Count), row);
        }).Where(r => r.Total > 0).ToList();

        // Statutory retirement at 60: anyone already past it counts in the current year.
        var retiring = rows.Where(r => r.BirthDate.HasValue)
            .Select(r => (Row: r, Date: r.BirthDate!.Value.AddYears(RetirementAge))).ToList();
        var forecast = new List<ForecastYear>(10);
        var cumulative = 0;
        for (var offset = 0; offset < 10; offset++)
        {
            var year = today.Year + offset;
            var inYear = retiring.Where(x => offset == 0 ? x.Date.Year <= year : x.Date.Year == year).ToList();
            cumulative += inYear.Count;
            var top = inYear.GroupBy(x => x.Row.Formation).OrderByDescending(g => g.Count()).FirstOrDefault();
            forecast.Add(new ForecastYear(year, inYear.Count, inYear.Count(x => x.Row.Position is not null), cumulative,
                Math.Max(0, rows.Count - cumulative), top?.Key ?? "—", top?.Count() ?? 0));
        }

        var leaves = snapshot.Leaves.Where(l => ids.Contains(l.EmployeeId)).ToList();
        var byId = rows.ToDictionary(r => r.Id);
        var outToday = leaves.Where(l => l.Start <= today && l.End >= today)
            .GroupBy(l => l.EmployeeId)
            .Select(g => g.OrderByDescending(l => l.End).First())
            .Select(l => new LeaveTodayItem(byId[l.EmployeeId], l.Type, l.End))
            .OrderBy(l => l.End).ToList();
        var yearStart = new DateOnly(today.Year, 1, 1);
        var leavesThisYear = leaves.Where(l => l.Start >= yearStart || l.End >= yearStart).ToList();

        var letters = snapshot.Letters.Where(l => ids.Contains(l.EmployeeId)).ToList();
        int sy = 0, sm = 0, sd = 0;
        foreach (var l in letters) { sy += l.SeniorityYears * l.Count; sm += l.SeniorityMonths * l.Count; sd += l.SeniorityDays * l.Count; }
        sm += sd / 30; sd %= 30; sy += sm / 12; sm %= 12;
        var seniority = string.Join(" و", new[] { sy > 0 ? $"{sy} سنة" : null, sm > 0 ? $"{sm} شهر" : null, sd > 0 ? $"{sd} يوم" : null }.Where(s => s is not null));

        var penalties = snapshot.Penalties.Where(p => ids.Contains(p.EmployeeId)).ToList();

        var records = new List<RecordMeter>
        {
            new("photo", "صورة شخصية", rows.Count(r => r.HasPhoto)),
            new("phone", "رقم هاتف", rows.Count(r => r.HasPhone)),
            new("email", "بريد إلكتروني", rows.Count(r => r.HasEmail)),
            new("account", "حساب في المنظومة", rows.Count(r => r.HasAccount)),
            new("card", "البطاقة الوطنية", rows.Count(r => r.HasNationalCard)),
            new("passport", "جواز السفر", rows.Count(r => r.HasPassport)),
            new("residence", "بيانات السكن", rows.Count(r => r.Governorate is not null))
        };

        return new WorkforceStatsResult
        {
            Rows = rows,
            BaseTotal = scope.Count,
            Facets = facets,
            Breakdowns = breakdowns,
            Context = context,
            Female = rows.Count(r => r.Gender == Female),
            Male = rows.Count(r => r.Gender == Male),
            AverageAge = ages.Count > 0 ? Math.Round(ages.Average(), 1) : 0,
            AverageService = service.Count > 0 ? Math.Round(service.Average(), 1) : 0,
            Supervisory = rows.Count(r => r.Position is not null),
            Postgraduate = rows.Count(r => IsPostgraduate(r.Education)),
            NewHires = rows.Count(r => IsNewHire(r, today)),
            Age58Plus = ages.Count(a => a >= 58),
            RetiringIn12Months = rows.Count(r => IsRetiringWithinYear(r, today)),
            PastRetirementAge = rows.Count(r => IsPastRetirementAge(r, today)),
            MartyrFamilies = rows.Count(r => r.HasMartyr),
            Pyramid = pyramid,
            HiresByYear = hiresByYear,
            YearsInTitle = yearsInTitle,
            LeaveBalanceBands = leaveBalance,
            Heatmap = heatmap,
            HeatmapMax = heatmap.SelectMany(r => r.Cells).Select(c => c.Count).DefaultIfEmpty(0).Max(),
            Forecast = forecast,
            OnLeaveToday = outToday,
            ReturningThisWeek = outToday.Count(l => l.End <= today.AddDays(6)),
            LeaveTodayByType = Ranked(outToday.Select(l => l.Type)),
            LeaveYearByType = Ranked(leavesThisYear.Select(l => l.Type)),
            LeaveYearDays = leavesThisYear.Sum(l => l.Days),
            LettersTotal = letters.Sum(l => l.Count),
            HonoredEmployees = letters.Select(l => l.EmployeeId).Distinct().Count(),
            LettersByType = letters.GroupBy(l => l.Type).Select(g => new StatItem(g.Key, g.Sum(l => l.Count)))
                .OrderByDescending(s => s.Count).ToList(),
            TopHonored = rows.Where(r => r.Letters > 0).OrderByDescending(r => r.Letters).ThenBy(r => r.Name).Take(8).ToList(),
            LettersSeniority = seniority,
            PenaltiesTotal = penalties.Count,
            PenalizedEmployees = penalties.Select(p => p.EmployeeId).Distinct().Count(),
            PenaltiesLast12Months = penalties.Count(p => p.Date is { } d && d > today.AddYears(-1) && d <= today),
            PenaltiesByType = Ranked(penalties.Select(p => p.Type)),
            CoursesTotal = rows.Sum(r => r.Courses),
            TrainedEmployees = rows.Count(r => r.Courses > 0),
            VehicleOwners = rows.Count(r => r.Vehicles > 0),
            VehiclesTotal = rows.Sum(r => r.Vehicles),
            VehiclesAuthorized = rows.Sum(r => r.AuthorizedVehicles),
            Records = records,
            RecordsComplete = rows.Count(r => r.HasPhoto && r.HasContacts && r.HasNationalCard)
        };
    }

    public static bool IsPostgraduate(string education) =>
        education.Contains("دكتوراه") || education.Contains("ماجستير") || education.Contains("دبلوم عالي")
        || education.Contains("PhD") || education.Contains("Master");

    public static string NormalizeGradeCode(string? code, string? name)
    {
        if (!string.IsNullOrWhiteSpace(code)) return code.Trim();
        if (string.IsNullOrWhiteSpace(name)) return WorkforceSnapshot.Unset;
        var clean = name.Trim();
        foreach (var special in new[] { "العليا أ", "العليا ب", "الخاصة أ", "الخاصة ب" })
            if (clean.Contains(special)) return special;
        string[][] ordinals =
        [
            ["اولى", "الأولى", "الاولى"], ["ثانية"], ["ثالثة"], ["رابعة"], ["خامسة"],
            ["سادسة"], ["سابعة"], ["ثامنة"], ["تاسعة"], ["عاشرة"]
        ];
        for (var i = 0; i < ordinals.Length; i++)
            if (clean == (i + 1).ToString() || ordinals[i].Any(clean.Contains)) return (i + 1).ToString();
        return clean;
    }

    public static int NormalizeStageNumber(int? number, string? code, string? name)
    {
        if (number is >= 1 and <= 11) return number.Value;
        if (!string.IsNullOrWhiteSpace(code) && int.TryParse(code.Trim(), out var n) && n is >= 1 and <= 11) return n;
        if (string.IsNullOrWhiteSpace(name)) return 0;
        var clean = name.Trim();
        // Checked from eleven down, so "الحادية عشرة" is not read as the first stage.
        string[][] ordinals =
        [
            ["أولى", "اولى"], ["ثانية"], ["ثالثة"], ["رابعة"], ["خامسة"], ["سادسة"],
            ["سابعة"], ["ثامنة"], ["تاسعة"], ["عاشرة"], ["حادية عشر"]
        ];
        for (var i = ordinals.Length - 1; i >= 0; i--)
            if (clean == (i + 1).ToString() || ordinals[i].Any(clean.Contains)) return i + 1;
        return 0;
    }
}
