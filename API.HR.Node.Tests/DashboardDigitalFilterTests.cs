using API.HR.Node.Data;
using API.HR.Node.Services.Employees;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class DashboardDigitalFilterTests
{
    [Fact]
    public void OppositePhotoSelectionsIncludeBothPopulations()
    {
        var employees = Employees();
        var result = Apply(employees, [DashboardDigitalFilter.WithPhoto, DashboardDigitalFilter.WithoutPhoto]);
        Assert.Equal(3, result.Count());
    }

    [Fact]
    public void OppositeContactSelectionsDoNotEraseTheWorkforce()
    {
        var result = Apply(Employees(), [DashboardDigitalFilter.CompleteContacts, DashboardDigitalFilter.MissingContacts]);
        Assert.Equal(3, result.Count());
    }

    [Fact]
    public void DifferentCategoriesIntersectAndMissingContactsIncludesEitherMissingField()
    {
        var result = Apply(Employees(), [DashboardDigitalFilter.WithPhoto, DashboardDigitalFilter.MissingContacts]);
        Assert.Equal("B", Assert.Single(result).EmployeeID);
    }

    [Fact]
    public void SelectingBothPhotoStatesStillHonorsTheAccountCategory()
    {
        var result = Apply(Employees(), [DashboardDigitalFilter.WithPhoto, DashboardDigitalFilter.WithoutPhoto, DashboardDigitalFilter.LinkedAccount]);
        Assert.Equal("A", Assert.Single(result).EmployeeID);
    }

    [Fact]
    public void FiltersTranslateToSqlForBothDashboardAndReportQueries()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options);
        var query = DashboardDigitalFilter.Apply(db.Employees,
            new HashSet<string> { DashboardDigitalFilter.WithoutPhoto, DashboardDigitalFilter.CompleteContacts },
            db.EmployeeProfilePhotos.Where(p => p.IsActive).Select(p => p.EmployeeID),
            db.EmployeePhones.Select(p => p.EmployeeID), db.EmployeeEmails.Select(p => p.EmployeeID),
            db.Users.Where(u => u.EmployeeID != null).Select(u => u.EmployeeID!));
        // SQL generation verifies translation without connecting to a database.
        var sql = query.ToQueryString();
        Assert.Contains("EmployeeProfilePhotos", sql);
        Assert.Contains("EmployeePhones", sql);
        Assert.Contains("EmployeeEmails", sql);
    }

    private static IQueryable<Employee> Employees() => new[]
    {
        new Employee { EmployeeID = "A", Name = "ألف" },
        new Employee { EmployeeID = "B", Name = "باء" },
        new Employee { EmployeeID = "C", Name = "جيم" }
    }.AsQueryable();

    private static IQueryable<Employee> Apply(IQueryable<Employee> employees, string[] selections) =>
        DashboardDigitalFilter.Apply(employees, selections.ToHashSet(),
            new[] { "A", "B" }.AsQueryable(), new[] { "A", "B" }.AsQueryable(),
            new[] { "A" }.AsQueryable(), new[] { "A" }.AsQueryable());
}
