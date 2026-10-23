using System;
using System.Linq;
using System.Threading.Tasks;
using API.HR.Node.Data;
using API.HR.Node.Data.OrganizationalHierarchy;
using API.HR.Node.Data.Workflows;
using API.HR.Node.Services.OrganizationalHierarchy;
using API.HR.Node.Services.Workflows;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

public class OrganizationalHierarchyStaffTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"OrgHierarchyTestDb_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task AssignEmployeeToUnit_ShouldUpdateAdministrativeUnitId()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var loggerMock = new Mock<ILogger<OrganizationalHierarchyService>>();
        var service = new OrganizationalHierarchyService(dbContext, loggerMock.Object);

        var type = new AdministrativeUnitType { Id = 1, Name = "قسم", Code = "DIV", HierarchyLevel = 3 };
        dbContext.AdministrativeUnitTypes.Add(type);

        var unit = new AdministrativeUnit { Id = 10, Name = "قسم تقنية المعلومات", Code = "DIV-IT", UnitTypeId = 1 };
        dbContext.AdministrativeUnits.Add(unit);

        var emp = new Employee { Id = 100, EmployeeID = "EMP-100", Name = "علي أحمد" };
        dbContext.Employees.Add(emp);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.AssignEmployeeToUnitAsync("EMP-100", 10);

        // Assert
        result.Should().BeTrue();
        var updated = await dbContext.Employees.FindAsync(100);
        updated!.AdministrativeUnitId.Should().Be(10);
    }

    [Fact]
    public async Task GetHierarchyTree_ShouldIncludeManagerNameAndStaffRoster()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var loggerMock = new Mock<ILogger<OrganizationalHierarchyService>>();
        var service = new OrganizationalHierarchyService(dbContext, loggerMock.Object);

        var type = new AdministrativeUnitType { Id = 1, Name = "شعبة", Code = "SEC", HierarchyLevel = 4 };
        dbContext.AdministrativeUnitTypes.Add(type);

        var manager = new Employee { Id = 1, EmployeeID = "MGR-01", Name = "محمد كمال" };
        var staff1 = new Employee { Id = 2, EmployeeID = "EMP-01", Name = "سارة حسن", AdministrativeUnitId = 20 };
        var staff2 = new Employee { Id = 3, EmployeeID = "EMP-02", Name = "ياسين محمود", AdministrativeUnitId = 20 };

        var unit = new AdministrativeUnit
        {
            Id = 20,
            Name = "شعبة الشبكات",
            Code = "SEC-NET",
            UnitTypeId = 1,
            ManagerEmployeeId = "MGR-01",
            ManagerTitle = "مسؤول الشعبة"
        };

        dbContext.AdministrativeUnitTypes.Add(type);
        dbContext.AdministrativeUnits.Add(unit);
        dbContext.Employees.AddRange(manager, staff1, staff2);
        await dbContext.SaveChangesAsync();

        // Act
        var tree = await service.GetHierarchyTreeAsync();

        // Assert
        tree.Should().HaveCount(1);
        var node = tree.First();
        node.Id.Should().Be(20);
        node.ManagerEmployeeId.Should().Be("MGR-01");
        node.ManagerName.Should().Be("محمد كمال");
        node.EmployeesCount.Should().Be(2);
        node.Employees.Should().HaveCount(2);
        node.Employees.Select(e => e.EmployeeID).Should().Contain(["EMP-01", "EMP-02"]);
    }

    [Fact]
    public async Task WorkflowAssignmentResolver_ShouldResolveManagerFromAdministrativeUnit()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();

        var type = new AdministrativeUnitType { Id = 1, Name = "قسم", Code = "DIV", HierarchyLevel = 3 };
        var unit = new AdministrativeUnit
        {
            Id = 30,
            Name = "قسم الشؤون القانونية",
            Code = "DIV-LAW",
            UnitTypeId = 1,
            ManagerEmployeeId = "MGR-LAW"
        };
        dbContext.AdministrativeUnitTypes.Add(type);
        dbContext.AdministrativeUnits.Add(unit);

        var managerEmp = new Employee { Id = 5, EmployeeID = "MGR-LAW", Name = "المحامي خالد" };
        var employee = new Employee { Id = 6, EmployeeID = "EMP-LAW-1", Name = "زينب عمر", AdministrativeUnitId = 30 };
        dbContext.Employees.AddRange(managerEmp, employee);

        var managerUser = new ApplicationUser { Id = "user-mgr", UserName = "khaled", EmployeeID = "MGR-LAW" };
        var staffUser = new ApplicationUser { Id = "user-staff", UserName = "zeinab", EmployeeID = "EMP-LAW-1" };
        dbContext.Users.AddRange(managerUser, staffUser);

        await dbContext.SaveChangesAsync();

        var resolver = new WorkflowAssignmentResolver(dbContext);
        var step = new WorkflowStep
        {
            Code = "STEP_MGR",
            Name = "موافقة مدير القسم",
            ConfigurationJson = "{\"assignmentType\":\"MANAGER_OF_EMPLOYEE\"}"
        };
        var instance = new WorkflowInstance
        {
            EntityType = "Employee",
            EntityId = "EMP-LAW-1",
            InitiatorUserId = "user-staff"
        };
        var context = new WorkflowAssignmentContext(instance, step, "user-staff");

        // Act
        var result = await resolver.ResolveAsync(context);

        // Assert
        result.AssignedUserId.Should().Be("user-mgr");
    }
}
