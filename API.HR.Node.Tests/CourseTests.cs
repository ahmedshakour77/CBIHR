using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Services.Import;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class CourseTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CourseTemplate_CanBeGeneratedAndParsedRoundTrip()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // 1. Generate template
        var bytes = service.GenerateCoursesTemplate();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(100);

        // 2. Parse it back
        using var ms = new MemoryStream(bytes);
        var result = await service.ParseCoursesExcelAsync(ms);

        result.Should().NotBeNull();
        result.HasErrors.Should().BeFalse();
        result.Rows.Should().HaveCount(2); // 2 sample rows in template
        result.DetectedHeaders.Should().Contain("الرقم الوظيفي");
        result.DetectedHeaders.Should().Contain("اسم الدورة");
        result.DetectedHeaders.Should().Contain("تاريخ بداء الدورة");
        result.DetectedHeaders.Should().Contain("تاريخ نهاية الدورة");

        var firstRow = result.Rows[0];
        firstRow.EmployeeID.Should().Be("1001");
        firstRow.CourseName.Should().Be("إدارة المشاريع الاحترافية (PMP)");
        firstRow.StartDate.Should().Be("2024-02-01");
        firstRow.EndDate.Should().Be("2024-02-15");
    }

    [Fact]
    public async Task ParseCoursesExcel_WithUserExact15Columns_ExtractsOnlyRequiredAndIgnoresTheTenColumns()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // Build Excel with the user's EXACT 15 columns:
        // التشكيل (يهمل)
        // الرقم الوظيفي (يربط مع الجدول الاصلي)
        // اسم الموظف (يهمل)
        // الجنس (يهمل)
        // تاريخ الميلاد (يهمل)
        // العنوان الوظيفي (يهمل)
        // المسمى الوظيفي (يهمل)
        // المرحلة (يهمل)
        // الدرجة (يهمل)
        // تاريخ التعيين (يهمل)
        // اسم الدورة (نص حر او قائمة منسدلة)
        // الشهادة (يهمل)
        // تاريخ بداء الدورة
        // تاريخ نهاية الدورة
        // حالة الموظف (يهمل)
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("الدورات");

        var headers = new[]
        {
            "التشكيل",
            "الرقم الوظيفي",
            "اسم الموظف",
            "الجنس",
            "تاريخ الميلاد",
            "العنوان الوظيفي",
            "المسمى  الوظيفي",
            "المرحـلـة",
            "الدرجـة",
            "تاريخ التعيين",
            "اسم الدورة",
            "الشهادة",
            "تاريخ بداء الدورة",
            "تاريخ نهاية الدورة",
            "حالة الموظف"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }

        // Row 1
        ws.Cell(2, 1).Value = "دائرة العمليات المالية";
        ws.Cell(2, 2).Value = "55001";
        ws.Cell(2, 3).Value = "علي حسين كاظم";
        ws.Cell(2, 4).Value = "ذكر";
        ws.Cell(2, 5).Value = "1988-04-12";
        ws.Cell(2, 6).Value = "مشاور قانوني";
        ws.Cell(2, 7).Value = "مسؤول شعبة";
        ws.Cell(2, 8).Value = "3";
        ws.Cell(2, 9).Value = "4";
        ws.Cell(2, 10).Value = "2012-09-01";
        ws.Cell(2, 11).Value = "دورة القيادة الإدارية والتحول الرقمي";
        ws.Cell(2, 12).Value = "بكالوريوس قانون";
        ws.Cell(2, 13).Value = "2026-04-01";
        ws.Cell(2, 14).Value = "2026-04-10";
        ws.Cell(2, 15).Value = "مستمر بالخدمة";

        // Row 2
        ws.Cell(3, 1).Value = "دائرة المحاسبة";
        ws.Cell(3, 2).Value = "55002";
        ws.Cell(3, 3).Value = "مريم عبد الرزاق";
        ws.Cell(3, 4).Value = "أنثى";
        ws.Cell(3, 5).Value = "1992-07-20";
        ws.Cell(3, 6).Value = "محاسب أول";
        ws.Cell(3, 7).Value = "عضو لجنة";
        ws.Cell(3, 8).Value = "4";
        ws.Cell(3, 9).Value = "2";
        ws.Cell(3, 10).Value = "2016-11-15";
        ws.Cell(3, 11).Value = "دورة المعايير الدولية لإعداد التقارير المالية IFRS";
        ws.Cell(3, 12).Value = "ماجستير محاسبة";
        ws.Cell(3, 13).Value = "2026-05-10";
        ws.Cell(3, 14).Value = "2026-05-14";
        ws.Cell(3, 15).Value = "مستمر بالخدمة";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var parseResult = await service.ParseCoursesExcelAsync(ms);

        // Assertions
        parseResult.Should().NotBeNull();
        parseResult.HasErrors.Should().BeFalse();
        parseResult.Rows.Should().HaveCount(2);

        // Core fields must match
        parseResult.Rows[0].EmployeeID.Should().Be("55001");
        parseResult.Rows[0].CourseName.Should().Be("دورة القيادة الإدارية والتحول الرقمي");
        parseResult.Rows[0].StartDate.Should().Be("2026-04-01");
        parseResult.Rows[0].EndDate.Should().Be("2026-04-10");
        parseResult.Rows[0].EmployeeName.Should().Be("علي حسين كاظم");

        parseResult.Rows[1].EmployeeID.Should().Be("55002");
        parseResult.Rows[1].CourseName.Should().Be("دورة المعايير الدولية لإعداد التقارير المالية IFRS");
        parseResult.Rows[1].StartDate.Should().Be("2026-05-10");
        parseResult.Rows[1].EndDate.Should().Be("2026-05-14");

        // The 10 extra non-course columns must be unmapped (ignored)
        parseResult.UnmappedHeaders.Should().Contain(h => h.Contains("التشكيل"));
        parseResult.UnmappedHeaders.Should().Contain(h => h.Contains("الجنس"));
        parseResult.UnmappedHeaders.Should().Contain(h => h.Contains("تاريخ الميلاد"));
        parseResult.UnmappedHeaders.Should().Contain(h => h.Contains("العنوان الوظيفي"));
        parseResult.UnmappedHeaders.Should().Contain(h => h.Contains("المسمى"));
        parseResult.UnmappedHeaders.Should().Contain(h => h.Contains("المرح"));
        parseResult.UnmappedHeaders.Should().Contain(h => h.Contains("الدرج"));
        parseResult.UnmappedHeaders.Should().Contain(h => h.Contains("تاريخ التعيين"));
        parseResult.UnmappedHeaders.Should().Contain(h => h.Contains("الشهادة"));
        parseResult.UnmappedHeaders.Should().Contain(h => h.Contains("حالة الموظف"));
    }

    [Fact]
    public async Task ImportCoursesAsync_SavesNewAndUpsertsExistingCourse()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // Seed an employee
        var employee = new Employee
        {
            Id = 1,
            EmployeeID = "9901",
            Name = "عمر طارق سالم",
            GenderId = 1,
            JobTitleId = 1,
            PositionTitleId = 1,
            StageId = 1,
            GradeId = 1
        };
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        // 1. Initial insert of a course
        var row = new CourseImportRow(
            EmployeeID: "9901",
            CourseName: "الأمن السيبراني وحماية البيانات",
            StartDate: "2026-06-01",
            EndDate: "2026-06-05");

        var insertRequest = new CourseImportRequest("upsert", new[] { row });
        var insertResponse = await service.ImportCoursesAsync(insertRequest);

        insertResponse.TotalRows.Should().Be(1);
        insertResponse.Inserted.Should().Be(1);
        insertResponse.Updated.Should().Be(0);
        insertResponse.Skipped.Should().Be(0);

        var saved = await dbContext.EmployeeCourses.FirstOrDefaultAsync(c => c.EmployeeId == employee.Id);
        saved.Should().NotBeNull();
        saved!.CourseName.Should().Be("الأمن السيبراني وحماية البيانات");
        saved.StartDate.Should().Be(new DateOnly(2026, 6, 1));
        saved.EndDate.Should().Be(new DateOnly(2026, 6, 5));
        saved.DaysCount.Should().Be(5);

        // 2. Upsert same course with updated end date
        var updateRow = new CourseImportRow(
            EmployeeID: "9901",
            CourseName: "الأمن السيبراني وحماية البيانات",
            StartDate: "2026-06-01",
            EndDate: "2026-06-10");

        var upsertRequest = new CourseImportRequest("upsert", new[] { updateRow });
        var upsertResponse = await service.ImportCoursesAsync(upsertRequest);

        upsertResponse.TotalRows.Should().Be(1);
        upsertResponse.Inserted.Should().Be(0);
        upsertResponse.Updated.Should().Be(1);
        upsertResponse.Skipped.Should().Be(0);

        var updated = await dbContext.EmployeeCourses.FirstOrDefaultAsync(c => c.EmployeeId == employee.Id);
        updated!.EndDate.Should().Be(new DateOnly(2026, 6, 10));
        updated.DaysCount.Should().Be(10);
    }

    [Fact]
    public async Task ImportCoursesAsync_SkippedWhenEmployeeDoesNotExist()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        var row = new CourseImportRow(
            EmployeeID: "NON_EXISTENT_999",
            CourseName: "دورة القيادة",
            StartDate: "2026-07-01",
            EndDate: "2026-07-03");

        var request = new CourseImportRequest("upsert", new[] { row });
        var response = await service.ImportCoursesAsync(request);

        response.TotalRows.Should().Be(1);
        response.Inserted.Should().Be(0);
        response.Skipped.Should().Be(1);
        response.MissingEmployeeIDs.Should().Contain("NON_EXISTENT_999");
    }
}
