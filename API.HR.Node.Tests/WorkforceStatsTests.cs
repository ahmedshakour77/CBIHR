using API.HR.Node.Services.Dashboard;
using API.HR.Node.Services.Employees;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class WorkforceStatsTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);

    private static WorkforceRow Emp(int id, string formation = "أ", string gender = WorkforceStats.Male, string religion = "مسلم",
        DateOnly? birth = null, DateOnly? hire = null, bool photo = true, bool phone = true, bool email = true, bool account = false) => new()
    {
        Id = id, EmployeeId = $"E{id}", Name = $"موظف {id}", Formation = formation, Gender = gender, Religion = religion,
        BirthDate = birth, HireDate = hire, HasPhoto = photo, HasPhone = phone, HasEmail = email, HasAccount = account
    };

    private static WorkforceStatsResult Compute(IReadOnlyList<WorkforceRow> rows, WorkforceFilter filter, IReadOnlyList<LeaveFact>? leaves = null) =>
        WorkforceStats.Compute(rows, filter, new WorkforceSnapshot { Rows = rows, Letters = [], Leaves = leaves ?? [], Penalties = [] }, Today);

    [Fact]
    public void ValuesInOneFieldAreOrAndFieldsAreAnd()
    {
        WorkforceRow[] rows = [Emp(1, "أ"), Emp(2, "ب"), Emp(3, "ج"), Emp(4, "أ", WorkforceStats.Female)];
        var filter = new WorkforceFilter();
        filter.Set(WorkforceDimension.Formation, ["أ", "ب"]);
        Assert.Equal(3, Compute(rows, filter).Total);

        filter.Set(WorkforceDimension.Gender, [WorkforceStats.Female]);
        Assert.Equal([4], Compute(rows, filter).Rows.Select(r => r.Id));
    }

    [Fact]
    public void FacetCountsIgnoreTheirOwnFieldSoOtherValuesStayPickable()
    {
        WorkforceRow[] rows = [Emp(1, "أ"), Emp(2, "ب"), Emp(3, "ب", WorkforceStats.Female)];
        var filter = new WorkforceFilter();
        filter.Set(WorkforceDimension.Formation, ["أ"]);

        var result = Compute(rows, filter);

        Assert.Equal(1, result.Total);
        Assert.Equal(2, result.Facets[WorkforceDimension.Formation].Counts["ب"]);
        Assert.Equal(0, result.Facets[WorkforceDimension.Gender].Counts[WorkforceStats.Female]);
    }

    [Fact]
    public void ContextKeepsAFieldsOwnSiblingsButAppliesEveryOtherFilter()
    {
        WorkforceRow[] rows = [Emp(1, "أ"), Emp(2, "ب"), Emp(3, "ب", WorkforceStats.Female), Emp(4, "ج", WorkforceStats.Female)];
        var filter = new WorkforceFilter();
        filter.Set(WorkforceDimension.Formation, ["أ"]);
        filter.Set(WorkforceDimension.Gender, [WorkforceStats.Male]);

        var result = Compute(rows, filter);

        // The formation chart still shows ب beside the picked أ, counted under the gender filter only.
        Assert.Equal([("أ", 1), ("ب", 1)], result.Context[WorkforceDimension.Formation].Select(i => (i.Name, i.Count)));
        Assert.Equal([("أ", 1)], result.Breakdowns[WorkforceDimension.Formation].Select(i => (i.Name, i.Count)));
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public void PyramidIgnoresItsOwnAgeBandFilter()
    {
        WorkforceRow[] rows = [Emp(1, birth: new DateOnly(1970, 1, 1)), Emp(2, birth: new DateOnly(2000, 1, 1))];
        var filter = new WorkforceFilter();
        filter.Set(WorkforceDimension.AgeBand, ["55 إلى 59"]);

        var result = Compute(rows, filter);

        Assert.Equal(1, result.Total);
        Assert.Equal(2, result.Pyramid.Sum(b => b.Male));
    }

    [Fact]
    public void DigitalFieldUnionsInsideACategoryAndIntersectsBetweenThem()
    {
        WorkforceRow[] rows = [Emp(1, photo: true, phone: true), Emp(2, photo: false, phone: true), Emp(3, photo: false, phone: false)];
        var filter = new WorkforceFilter();
        filter.Set(WorkforceDimension.Digital, [DashboardDigitalFilter.WithPhoto, DashboardDigitalFilter.WithoutPhoto]);
        Assert.Equal(3, Compute(rows, filter).Total);

        filter.Set(WorkforceDimension.Digital, [DashboardDigitalFilter.WithoutPhoto, DashboardDigitalFilter.MissingContacts]);
        Assert.Equal([3], Compute(rows, filter).Rows.Select(r => r.Id));
    }

    [Fact]
    public void ToggleAddsThenRemovesAValue()
    {
        var filter = new WorkforceFilter();
        filter.Toggle(WorkforceDimension.Religion, "مسيحي");
        Assert.Contains("مسيحي", filter[WorkforceDimension.Religion]);
        filter.Toggle(WorkforceDimension.Religion, "مسيحي");
        Assert.True(filter.IsEmpty);
    }

    [Fact]
    public void ForecastPutsAnyoneAlreadyPastTheAgeInTheFirstYear()
    {
        WorkforceRow[] rows =
        [
            Emp(1, birth: new DateOnly(1964, 1, 1)),
            Emp(2, birth: new DateOnly(1966, 12, 1)),
            Emp(3, birth: new DateOnly(1968, 3, 1)),
            Emp(4, birth: new DateOnly(1990, 1, 1))
        ];

        var result = Compute(rows, new WorkforceFilter());

        Assert.Equal(2026, result.Forecast[0].Year);
        Assert.Equal(2, result.Forecast[0].Retirees);
        Assert.Equal(1, result.Forecast[2].Retirees);
        Assert.Equal(3, result.Forecast[^1].Cumulative);
        Assert.Equal(1, result.Forecast[^1].Projected);
        // 1966-12-01 turns 60 within the year; 1964 is already past it and is counted apart.
        Assert.Equal(1, result.RetiringIn12Months);
        Assert.Equal(1, result.PastRetirementAge);
    }

    [Theory]
    [InlineData("2024-10-20", true)]   // the day after two years ago
    [InlineData("2024-10-19", false)]  // exactly two years ago: outside "the last two years"
    [InlineData("2023-11-01", false)]  // 2.97 years: used to count, since whole years read 2
    [InlineData("2026-10-19", true)]   // today
    [InlineData("2026-11-01", false)]  // future-dated record
    public void NewHireMeansHiredWithinTheLastTwoYears(string hired, bool expected) =>
        Assert.Equal(expected, WorkforceStats.IsNewHire(Emp(1, hire: DateOnly.Parse(hired)), Today));

    [Fact]
    public void TurningTheAgeTodayCountsAsPastNotUpcoming()
    {
        var row = Emp(1, birth: Today.AddYears(-WorkforceStats.RetirementAge));
        Assert.True(WorkforceStats.IsPastRetirementAge(row, Today));
        Assert.False(WorkforceStats.IsRetiringWithinYear(row, Today));
    }

    [Fact]
    public void LeaveTodayCountsPeopleNotRecordsAndOnlyInScope()
    {
        WorkforceRow[] rows = [Emp(1, "أ"), Emp(2, "ب")];
        LeaveFact[] leaves =
        [
            new(1, "اعتيادية", Today.AddDays(-2), Today.AddDays(1), 4),
            new(1, "مرضية", Today, Today.AddDays(9), 10),
            new(2, "اعتيادية", Today, Today, 1)
        ];
        var filter = new WorkforceFilter();
        filter.Set(WorkforceDimension.Formation, ["أ"]);

        var result = Compute(rows, filter, leaves);

        var only = Assert.Single(result.OnLeaveToday);
        Assert.Equal(Today.AddDays(9), only.End);
        Assert.Equal(0, result.ReturningThisWeek);
    }

    [Theory]
    [InlineData("المرحلة الحادية عشرة", 11)]
    [InlineData("المرحلة الأولى", 1)]
    [InlineData("المرحلة العاشرة", 10)]
    public void StageNamesNormaliseWithoutReadingElevenAsOne(string name, int expected) =>
        Assert.Equal(expected, WorkforceStats.NormalizeStageNumber(null, null, name));

    [Theory]
    [InlineData("أنثى", WorkforceStats.Female)]
    [InlineData("انثى", WorkforceStats.Female)]
    [InlineData("ذكر", WorkforceStats.Male)]
    [InlineData(null, "غير محدد")]
    public void GenderNamesNormalise(string? name, string expected) =>
        Assert.Equal(expected, WorkforceStats.NormalizeGender(name));

    [Fact]
    public void BandsKeepTheirNaturalOrder()
    {
        WorkforceRow[] rows =
        [
            Emp(1, birth: new DateOnly(1970, 1, 1)), Emp(2, birth: new DateOnly(1970, 5, 1)),
            Emp(3, birth: new DateOnly(2000, 1, 1))
        ];
        var bands = Compute(rows, new WorkforceFilter()).Breakdowns[WorkforceDimension.AgeBand].Select(b => b.Name).ToList();
        Assert.Equal(["25 إلى 29", "55 إلى 59"], bands);
    }
}
