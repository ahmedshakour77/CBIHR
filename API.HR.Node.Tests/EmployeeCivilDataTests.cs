using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Endpoints;
using API.HR.Node.Services.Import;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class EmployeeCivilDataTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Theory]
    [InlineData("التشكيل", null)]
    [InlineData("رقم الموظف", "EmployeeID")]
    [InlineData("تاريخ التولد", null)]
    [InlineData("الرقم الوطني(البطاقة الموحدة)", "NationalId")]
    [InlineData("الرقم الوطني", "NationalId")]
    [InlineData("تاريخ التعيين", null)]
    [InlineData("اسم الام", "MotherName")]
    [InlineData("أسم الأم", "MotherName")]
    [InlineData("الاسم الاول", "FirstName")]
    [InlineData("الاسم الأول", "FirstName")]
    [InlineData("الاسم الثاني", "SecondName")]
    [InlineData("الاسم الثالث", "ThirdName")]
    [InlineData("الاسم الرابع", "FourthName")]
    [InlineData("الاسم الكامل", "FullName")]
    [InlineData("البطاقة الموحدة", null)]
    [InlineData("الجنس", null)]
    [InlineData("الرقم الوظيفي الموحد", "UnifiedEmployeeNumber")]
    [InlineData("حالة الموظف", null)]
    public void MatchCivilDataField_MapsTargetColumnsAndIgnoresSpecified(string header, string? expectedField)
    {
        var actual = ExcelImportService.MatchCivilDataField(header);
        actual.Should().Be(expectedField);
    }

    [Fact]
    public async Task CivilDataTemplate_CanBeGeneratedAndParsedRoundTrip()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // 1. Generate template
        var bytes = service.GenerateCivilDataTemplate();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(100);

        // 2. Parse it back
        using var ms = new MemoryStream(bytes);
        var result = await service.ParseCivilDataExcelAsync(ms);

        result.Should().NotBeNull();
        result.HasErrors.Should().BeFalse();
        result.Rows.Should().HaveCount(2);

        var firstRow = result.Rows[0];
        firstRow.EmployeeID.Should().Be("1001");
        firstRow.NationalId.Should().Be("198812345678");
        firstRow.UnifiedEmployeeNumber.Should().Be("EMP-90021");
        firstRow.MotherName.Should().Be("فاطمة حسن علي");
        firstRow.FirstName.Should().Be("أحمد");
        firstRow.SecondName.Should().Be("شاكر");
        firstRow.ThirdName.Should().Be("كريم");
        firstRow.FourthName.Should().Be("محمد");
    }

    [Fact]
    public async Task ParseCivilDataExcel_WithExactUserColumns_ExtractsTargetsAndIgnoresOthers()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");

        // Headers exactly as requested by user
        var headers = new[]
        {
            "التشكيل",                      // يهمل
            "رقم الموظف",                   // يربط
            "تاريخ التولد",                 // يهمل
            "الرقم الوطني(البطاقة الموحدة)", // يضاف
            "تاريخ التعيين",                // يهمل
            "اسم الام",                     // يضاف
            "الاسم الاول",                  // يضاف
            "الاسم الثالث",                 // يضاف
            "الاسم الثاني",                 // يضاف
            "الاسم الرابع",                 // يضاف
            "الاسم الكامل",                 // يهمل
            "البطاقة الموحدة",              // يهمل
            "الجنس",                        // يهمل
            "الرقم الوظيفي الموحد",         // يضاف
            "حالة الموظف"                   // يهمل
        };

        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }

        var sampleRow = new[]
        {
            "الدائرة القانونية",
            "5501",
            "1985-07-20",
            "198500112233",
            "2012-03-01",
            "مريم جواد كاظم",
            "حسين",
            "رضا",
            "علي",
            "حميد",
            "حسين علي رضا حميد",
            "نعم",
            "ذكر",
            "IRQ-5501",
            "مستمر بالخدمة"
        };

        for (var i = 0; i < sampleRow.Length; i++)
        {
            ws.Cell(2, i + 1).Value = sampleRow[i];
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var result = await service.ParseCivilDataExcelAsync(ms);

        result.HasErrors.Should().BeFalse();
        result.Rows.Should().HaveCount(1);

        var row = result.Rows[0];
        row.EmployeeID.Should().Be("5501");
        row.NationalId.Should().Be("198500112233");
        row.UnifiedEmployeeNumber.Should().Be("IRQ-5501");
        row.MotherName.Should().Be("مريم جواد كاظم");
        row.FirstName.Should().Be("حسين");
        row.SecondName.Should().Be("علي");
        row.ThirdName.Should().Be("رضا");
        row.FourthName.Should().Be("حميد");

        // Verify ignored headers were detected as unmapped
        result.UnmappedHeaders.Should().Contain("التشكيل");
        result.UnmappedHeaders.Should().Contain("تاريخ التولد");
        result.UnmappedHeaders.Should().Contain("تاريخ التعيين");
        result.UnmappedHeaders.Should().Contain("البطاقة الموحدة");
        result.UnmappedHeaders.Should().Contain("الجنس");
        result.UnmappedHeaders.Should().Contain("حالة الموظف");
    }

    [Fact]
    public async Task ExecuteImportAsync_UpdatesMatchingEmployeesInDatabase()
    {
        using var dbContext = CreateInMemoryDbContext();

        // Seed existing employee
        var employee = new Employee
        {
            EmployeeID = "9901",
            Name = "عمر طارق خليل",
            NationalId = null,
            UnifiedEmployeeNumber = null,
            MotherName = null
        };
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var request = new EmployeeCivilDataImportRequest(new[]
        {
            new EmployeeCivilDataImportRow(
                EmployeeID: "9901",
                NationalId: "199012349901",
                UnifiedEmployeeNumber: "UNI-9901",
                MotherName: "سناء عبد الوهاب",
                FirstName: "عمر",
                SecondName: "طارق",
                ThirdName: "خليل",
                FourthName: "البغدادي",
                FullName: "عمر طارق خليل البغدادي"
            ),
            new EmployeeCivilDataImportRow(
                EmployeeID: "8888", // Missing employee in DB
                NationalId: "198811118888",
                UnifiedEmployeeNumber: "UNI-8888",
                MotherName: "زينب",
                FirstName: "زيد",
                SecondName: "علي",
                ThirdName: "حسين",
                FourthName: "الجبوري"
            )
        });

        var (success, response, errors, message) = await EmployeeCivilDataEndpoints.ExecuteImportAsync(request, dbContext, CancellationToken.None);

        success.Should().BeTrue();
        response.Should().NotBeNull();
        response!.TotalRows.Should().Be(2);
        response.Updated.Should().Be(1);
        response.Skipped.Should().Be(1);
        response.MissingEmployeeIDs.Should().Contain("8888");

        // Verify database state
        var updatedEmp = await dbContext.Employees.FirstAsync(e => e.EmployeeID == "9901");
        updatedEmp.NationalId.Should().Be("199012349901");
        updatedEmp.UnifiedEmployeeNumber.Should().Be("UNI-9901");
        updatedEmp.MotherName.Should().Be("سناء عبد الوهاب");
        updatedEmp.FirstName.Should().Be("عمر");
        updatedEmp.SecondName.Should().Be("طارق");
        updatedEmp.ThirdName.Should().Be("خليل");
        updatedEmp.FourthName.Should().Be("البغدادي");
    }
}
