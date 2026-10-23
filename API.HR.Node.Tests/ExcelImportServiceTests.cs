using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Services.Import;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class ExcelImportServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task EmployeeTemplate_CanBeGeneratedAndParsedRoundTrip()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // Generate template
        var bytes = service.GenerateEmployeesTemplate();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(100);

        // Parse it back
        using var ms = new MemoryStream(bytes);
        var result = await service.ParseEmployeesExcelAsync(ms);

        result.Should().NotBeNull();
        result.HasErrors.Should().BeFalse();
        result.Rows.Should().HaveCount(2); // 2 sample rows in template
        result.DetectedHeaders.Should().Contain("الرقم الوظيفي");
        result.DetectedHeaders.Should().Contain("الاسم الكامل");

        var firstRow = result.Rows[0];
        firstRow.EmployeeID.Should().Be("1001");
        firstRow.Name.Should().Be("أحمد شاكر كريم");
        firstRow.Formation.Should().Be("مكتب المدير العام");
        firstRow.JobTitle.Should().Be("معاون مدير عام");
    }

    [Fact]
    public async Task AppreciationLettersTemplate_CanBeGeneratedAndParsedRoundTrip()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        var bytes = service.GenerateAppreciationLettersTemplate();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(100);

        using var ms = new MemoryStream(bytes);
        var result = await service.ParseAppreciationLettersExcelAsync(ms);

        result.Should().NotBeNull();
        result.HasErrors.Should().BeFalse();
        result.Rows.Should().HaveCount(2);
        result.DetectedHeaders.Should().Contain("الرقم الوظيفي");
        result.DetectedHeaders.Should().Contain("كتب الشكر");

        var firstRow = result.Rows[0];
        firstRow.EmployeeID.Should().Be("1001");
        firstRow.Title.Should().Contain("شكر وتقدير");
    }

    [Fact]
    public async Task ImportEmployeesAsync_InsertsAndUpsertsCorrectly()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        var rows = new List<EmployeeImportRow>
        {
            new("EMP_EXCEL_1", "محمد علي أحمد", "دائرة العمليات", "مبرمج أقدم", "مسؤول شعبة", "1", "3", "بكالوريوس", "حاسبات", "2023-01-01", "2018-05-01", "ذكر", "مسلم", "عراقي", "العمليات", "1990-03-15", "مستمر بالخدمة"),
            new("EMP_EXCEL_2", "فاطمة حسن كمال", "دائرة الرقابة", "مدقق رئيسي", null, "2", "2", "ماجستير", "محاسبة", null, "2016-09-01", "أنثى", "مسلم", "عراقي", null, "1988-11-20", "مستمر بالخدمة")
        };

        var request = new EmployeeImportRequest("upsert", rows, CreateMissingLookups: true);
        var response = await service.ImportEmployeesAsync(request);

        response.TotalRows.Should().Be(2);
        response.Inserted.Should().Be(2);
        response.Updated.Should().Be(0);

        var savedCount = await dbContext.Employees.CountAsync();
        savedCount.Should().Be(2);

        var emp1 = await dbContext.Employees
            .Include(e => e.Formation)
            .Include(e => e.JobTitle)
            .FirstOrDefaultAsync(e => e.EmployeeID == "EMP_EXCEL_1");

        emp1.Should().NotBeNull();
        emp1!.Name.Should().Be("محمد علي أحمد");
        emp1.Formation.Should().NotBeNull();
        emp1.Formation!.Name.Should().Be("دائرة العمليات");

        // Now upsert with an update
        var updateRows = new List<EmployeeImportRow>
        {
            new("EMP_EXCEL_1", "محمد علي أحمد المحدث", "دائرة العمليات", "رئيس مبرمجين", "مدير قسم", "1", "2", "بكالوريوس", "حاسبات", "2024-01-01", "2018-05-01", "ذكر", "مسلم", "عراقي", "العمليات", "1990-03-15", "مستمر بالخدمة")
        };

        var updateRequest = new EmployeeImportRequest("upsert", updateRows, CreateMissingLookups: true);
        var updateResponse = await service.ImportEmployeesAsync(updateRequest);

        updateResponse.Inserted.Should().Be(0);
        updateResponse.Updated.Should().Be(1);

        var updatedEmp = await dbContext.Employees.FirstOrDefaultAsync(e => e.EmployeeID == "EMP_EXCEL_1");
        updatedEmp!.Name.Should().Be("محمد علي أحمد المحدث");
    }

    [Fact]
    public async Task ImportAppreciationLettersAsync_LinksToEmployeeAndSkipsMissing()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // Add 1 employee first
        dbContext.Employees.Add(new Employee
        {
            EmployeeID = "EMP_500",
            Name = "سالم جاسم"
        });
        await dbContext.SaveChangesAsync();

        var rows = new List<AppreciationLetterImportRow>
        {
            new("EMP_500", "كتاب شكر سنوي", "2024-01-10", "1", "محافظ", "ملاحظة أولى", "أمر/10"),
            new("EMP_NON_EXISTENT", "كتاب شكر لموظف غير موجود", "2024-02-15", "1", "مدير عام", null, "أمر/20")
        };

        var request = new AppreciationLetterImportRequest("upsert", rows, CreateMissingLookups: true);
        var response = await service.ImportAppreciationLettersAsync(request);

        response.TotalRows.Should().Be(2);
        response.Inserted.Should().Be(1);
        response.Skipped.Should().Be(1);
        response.MissingEmployeeIDs.Should().Contain("EMP_NON_EXISTENT");

        var letter = await dbContext.AppreciationLetters
            .Include(l => l.Employee)
            .Include(l => l.Type)
            .FirstOrDefaultAsync(l => l.Employee.EmployeeID == "EMP_500");

        letter.Should().NotBeNull();
        letter!.Title.Should().Be("كتاب شكر سنوي");
        letter.Type.Should().NotBeNull();
        letter.Type!.Name.Should().Be("محافظ");
    }

    [Fact]
    public async Task AppreciationLettersExcel_MatchesEmployeeNumberAndMapsCorrectlyWithExtraColumns()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // Build an Excel workbook with ClosedXML containing exact headers reported by the user
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");
        
        // Headers from user report:
        // اسم الموظف, كتب الشكر, رقم الموظف, تاريخ كتب الشكر, عدد كتب الشكر, نوع كتب شكر, التشكيل, العنوان الوظيفي, الملاحظات, تاريخ اخر علاوة, تاريخ العلاوة, رقم كتاب
        var headers = new[]
        {
            "اسم الموظف", "كتب الشكر", "رقم الموظف", "تاريخ كتب الشكر", "عدد كتب الشكر",
            "نوع كتب شكر", "التشكيل", "العنوان الوظيفي", "الملاحظات", "تاريخ اخر علاوة",
            "تاريخ العلاوة", "رقم كتاب"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }

        ws.Cell(2, 1).Value = "علي حسين كريم";
        ws.Cell(2, 2).Value = "كتاب شكر سنوي متميز";
        ws.Cell(2, 3).Value = "EMP-999";
        ws.Cell(2, 4).Value = "2024-03-01";
        ws.Cell(2, 5).Value = 2;
        ws.Cell(2, 6).Value = "وزاري";
        ws.Cell(2, 7).Value = "قسم تقنية المعلومات";
        ws.Cell(2, 8).Value = "مبرمج أقدم";
        ws.Cell(2, 9).Value = "ملاحظة تدقيق";
        ws.Cell(2, 10).Value = "2023-01-01";
        ws.Cell(2, 11).Value = "2024-01-01";
        ws.Cell(2, 12).Value = "1234/ش";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var result = await service.ParseAppreciationLettersExcelAsync(ms);

        result.Should().NotBeNull();
        result.HasErrors.Should().BeFalse();
        result.Rows.Should().HaveCount(1);
        result.MatchedHeaders.Should().Contain("رقم الموظف");
        result.MatchedHeaders.Should().Contain("كتب الشكر");
        result.UnmappedHeaders.Should().Contain("العنوان الوظيفي");
        result.UnmappedHeaders.Should().Contain("تاريخ اخر علاوة");

        var row = result.Rows[0];
        row.EmployeeID.Should().Be("EMP-999");
        row.Title.Should().Be("كتاب شكر سنوي متميز");
        row.LetterDate.Should().Be("2024-03-01");
        row.Count.Should().Be("2");
        row.Type.Should().Be("وزاري");
        row.Notes.Should().Be("ملاحظة تدقيق");
        row.LetterNumber.Should().Be("1234/ش");
    }

    [Fact]
    public async Task EmployeeExcel_MapsPositionTitleWhenHeaderIsAlMusammaAlWadheefi()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("Employees");
        
        ws.Cell(1, 1).Value = "الرقم الوظيفي";
        ws.Cell(1, 2).Value = "اسم الموظف";
        ws.Cell(1, 3).Value = "المسمى الوظيفي"; // should map to PositionTitle
        ws.Cell(1, 4).Value = "العنوان الوظيفي";   // maps to JobTitle

        ws.Cell(2, 1).Value = "5555";
        ws.Cell(2, 2).Value = "سارة أحمد";
        ws.Cell(2, 3).Value = "مدير قسم الشؤون المالية";
        ws.Cell(2, 4).Value = "مدقق أقدم";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var result = await service.ParseEmployeesExcelAsync(ms);

        result.Should().NotBeNull();
        result.HasErrors.Should().BeFalse();
        result.Rows.Should().HaveCount(1);

        var row = result.Rows[0];
        row.EmployeeID.Should().Be("5555");
        row.Name.Should().Be("سارة أحمد");
        row.PositionTitle.Should().Be("مدير قسم الشؤون المالية");
        row.JobTitle.Should().Be("مدقق أقدم");
    }

    [Fact]
    public async Task LeavesTemplate_CanBeGeneratedAndParsedRoundTrip()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        var bytes = service.GenerateLeavesTemplate();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(100);

        using var ms = new MemoryStream(bytes);
        var result = await service.ParseLeavesExcelAsync(ms);

        result.Should().NotBeNull();
        result.HasErrors.Should().BeFalse();
        result.Rows.Should().HaveCount(2);
        result.DetectedHeaders.Should().Contain("الرقم الوظيفي");
        result.DetectedHeaders.Should().Contain("نوع الإجازة");

        var first = result.Rows[0];
        first.EmployeeID.Should().Be("1001");
        first.LeaveType.Should().Be("إجازة اعتيادية");
        first.DaysCount.Should().Be("5");
        first.Status.Should().Be("معتمدة");
    }

    [Fact]
    public async Task ImportLeavesAsync_SavesAndLinksEmployeeCorrectly()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        dbContext.Employees.Add(new Employee
        {
            EmployeeID = "EMP_LEAVE_1",
            Name = "كرار مهدي"
        });
        await dbContext.SaveChangesAsync();

        var rows = new List<LeaveImportRow>
        {
            new("EMP_LEAVE_1", "مرضية", "2024-05-01", "2024-05-03", "3", "أم/456", "2024-04-30", "إجازة برقد مستشفى", "Approved"),
            new("EMP_UNKNOWN", "اعتيادية", "2024-06-01", "2024-06-05", "5", null, null, null, "Pending")
        };

        var request = new LeaveImportRequest("upsert", rows, CreateMissingLookups: true);
        var response = await service.ImportLeavesAsync(request);

        response.TotalRows.Should().Be(2);
        response.Inserted.Should().Be(1);
        response.Skipped.Should().Be(1);
        response.MissingEmployeeIDs.Should().Contain("EMP_UNKNOWN");

        var savedLeave = await dbContext.EmployeeLeaves
            .Include(l => l.Employee)
            .Include(l => l.LeaveType)
            .FirstOrDefaultAsync(l => l.Employee.EmployeeID == "EMP_LEAVE_1");

        savedLeave.Should().NotBeNull();
        savedLeave!.DaysCount.Should().Be(3);
        savedLeave.LeaveType.Should().NotBeNull();
        savedLeave.LeaveType!.Name.Should().Be("مرضية");
        savedLeave.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task LeavesExcel_ExcludesDepartmentAndMatchesUserHeadersCorrectly()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        dbContext.Employees.Add(new Employee
        {
            EmployeeID = "EMP_DEPT_EXCLUDE",
            Name = "زينب كاظم"
        });
        await dbContext.SaveChangesAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("Leaves");

        // Exact headers from user screenshot:
        // رقم الموظف | اسم الموظف | نوع الإجازة | تاريخ بدء الإجازة | تاريخ انتهاء الإجازة | عدد الايام | اسم الدائرة | ملاحظات
        var headers = new[]
        {
            "رقم الموظف", "اسم الموظف", "نوع الإجازة", "تاريخ بدء الإجازة",
            "تاريخ انتهاء الإجازة", "عدد الايام", "اسم الدائرة", "ملاحظات"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }

        ws.Cell(2, 1).Value = "EMP_DEPT_EXCLUDE";
        ws.Cell(2, 2).Value = "زينب كاظم";
        ws.Cell(2, 3).Value = "اعتيادية";
        ws.Cell(2, 4).Value = "2024-06-01";
        ws.Cell(2, 5).Value = "2024-06-05";
        ws.Cell(2, 6).Value = 5;
        ws.Cell(2, 7).Value = "دائرة العمليات المالية"; // "اسم الدائرة" should be excluded from DB
        ws.Cell(2, 8).Value = "إجازة اعتيادية برصيد سابق";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var result = await service.ParseLeavesExcelAsync(ms);

        result.Should().NotBeNull();
        result.HasErrors.Should().BeFalse();
        result.Rows.Should().HaveCount(1);

        // Verify Matched Headers (Only fields saved in EmployeeLeaves table)
        result.MatchedHeaders.Should().Contain("رقم الموظف");
        result.MatchedHeaders.Should().Contain("نوع الإجازة");
        result.MatchedHeaders.Should().Contain("تاريخ بدء الإجازة");
        result.MatchedHeaders.Should().Contain("تاريخ انتهاء الإجازة");
        result.MatchedHeaders.Should().Contain("عدد الايام");
        result.MatchedHeaders.Should().Contain("ملاحظات");

        // Verify that "اسم الموظف" and "اسم الدائرة" are in UnmappedHeaders (ignored/excluded from EmployeeLeaves table)
        result.UnmappedHeaders.Should().Contain("اسم الموظف");
        result.UnmappedHeaders.Should().Contain("اسم الدائرة");

        var row = result.Rows[0];
        row.EmployeeID.Should().Be("EMP_DEPT_EXCLUDE");
        row.LeaveType.Should().Be("اعتيادية");
        row.StartDate.Should().Be("2024-06-01");
        row.EndDate.Should().Be("2024-06-05");
        row.DaysCount.Should().Be("5");
        row.Notes.Should().Be("إجازة اعتيادية برصيد سابق");

        // Perform import and verify it saves cleanly into database without any department column in EmployeeLeaves
        var importResponse = await service.ImportLeavesAsync(new LeaveImportRequest("upsert", result.Rows, CreateMissingLookups: true));
        importResponse.Inserted.Should().Be(1);

        var savedLeave = await dbContext.EmployeeLeaves
            .Include(l => l.Employee)
            .Include(l => l.LeaveType)
            .FirstOrDefaultAsync(l => l.Employee.EmployeeID == "EMP_DEPT_EXCLUDE");

        savedLeave.Should().NotBeNull();
        savedLeave!.DaysCount.Should().Be(5);
        savedLeave.StartDate.Should().Be(DateOnly.Parse("2024-06-01"));
        savedLeave.EndDate.Should().Be(DateOnly.Parse("2024-06-05"));
        savedLeave.Notes.Should().Be("إجازة اعتيادية برصيد سابق");
        savedLeave.LeaveType!.Name.Should().Be("اعتيادية");
    }

    [Fact]
    public void LeaveContracts_JsonSerializationDoesNotCollide()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        var req = new CreateLeaveRequest(
            EmployeeNumber: "1001",
            EmployeeId: 42,
            LeaveTypeName: "اعتيادية",
            LeaveTypeId: null,
            StartDate: "2024-05-01",
            EndDate: "2024-05-03",
            DaysCount: 3,
            OrderNumber: "123",
            OrderDate: null,
            Notes: "طلب إجازة",
            Status: "معتمدة");

        var jsonReq = System.Text.Json.JsonSerializer.Serialize(req, options);
        jsonReq.Should().Contain("\"employeeNumber\":\"1001\"");
        jsonReq.Should().Contain("\"employeeId\":42");

        var deserializedReq = System.Text.Json.JsonSerializer.Deserialize<CreateLeaveRequest>(jsonReq, options);
        deserializedReq.Should().NotBeNull();
        deserializedReq!.EmployeeID.Should().Be("1001");
        deserializedReq.EmployeeNumber.Should().Be("1001");
        deserializedReq.EmployeeId.Should().Be(42);

        var dto = new LeaveDto(
            1, 42, "1001", "أحمد علي", "دائرة العمليات", null, "اعتيادية",
            DateOnly.Parse("2024-05-01"), DateOnly.Parse("2024-05-03"), 3, null, null, null, "Approved", DateTimeOffset.UtcNow);

        var jsonDto = System.Text.Json.JsonSerializer.Serialize(dto, options);
        jsonDto.Should().Contain("\"employeeNumber\":\"1001\"");
        jsonDto.Should().Contain("\"employeeId\":42");
    }

    [Fact]
    public async Task LeavesImport_UpsertsOnEmployeeAndDatesAndType_InsertsOnDifferentDatesOrType()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        dbContext.Employees.Add(new Employee
        {
            EmployeeID = "EMP_REUPLOAD_TEST",
            Name = "حيدر علي"
        });
        await dbContext.SaveChangesAsync();

        // 1. Initial import of leave
        var row1 = new LeaveImportRow(
            EmployeeID: "EMP_REUPLOAD_TEST",
            LeaveType: "اعتيادية",
            StartDate: "2024-07-01",
            EndDate: "2024-07-10",
            DaysCount: "10",
            OrderNumber: "أمر/1",
            OrderDate: "2024-06-30",
            Notes: "الملاحظة الأصلية",
            Status: "Approved");

        var req1 = new LeaveImportRequest("upsert", new[] { row1 }, CreateMissingLookups: true);
        var res1 = await service.ImportLeavesAsync(req1);

        res1.Inserted.Should().Be(1);
        res1.Updated.Should().Be(0);

        var countAfter1 = await dbContext.EmployeeLeaves.CountAsync();
        countAfter1.Should().Be(1);

        // 2. Re-upload same employee, same start date, same end date, same leave type -> MUST UPDATE
        var rowSame = new LeaveImportRow(
            EmployeeID: "EMP_REUPLOAD_TEST",
            LeaveType: "اعتيادية",
            StartDate: "2024-07-01",
            EndDate: "2024-07-10",
            DaysCount: "10",
            OrderNumber: "أمر/معدل/99",
            OrderDate: "2024-07-01",
            Notes: "تم تحديث الملاحظات بعد المراجعة",
            Status: "Approved");

        var req2 = new LeaveImportRequest("upsert", new[] { rowSame }, CreateMissingLookups: true);
        var res2 = await service.ImportLeavesAsync(req2);

        res2.Inserted.Should().Be(0);
        res2.Updated.Should().Be(1);

        var countAfter2 = await dbContext.EmployeeLeaves.CountAsync();
        countAfter2.Should().Be(1); // No duplicates created!

        var updatedLeave = await dbContext.EmployeeLeaves.FirstAsync();
        updatedLeave.Notes.Should().Be("تم تحديث الملاحظات بعد المراجعة");
        updatedLeave.OrderNumber.Should().Be("أمر/معدل/99");

        // 3. Upload with DIFFERENT end date or DIFFERENT type -> MUST INSERT NEW LEAVE
        var rowDifferent = new LeaveImportRow(
            EmployeeID: "EMP_REUPLOAD_TEST",
            LeaveType: "مرضية", // Different type
            StartDate: "2024-07-01",
            EndDate: "2024-07-03", // Different end date
            DaysCount: "3",
            OrderNumber: "أمر/طبي/5",
            OrderDate: "2024-07-01",
            Notes: "إجازة مرضية جديدة لنفس الموظف",
            Status: "Approved");

        var req3 = new LeaveImportRequest("upsert", new[] { rowDifferent }, CreateMissingLookups: true);
        var res3 = await service.ImportLeavesAsync(req3);

        res3.Inserted.Should().Be(1);
        res3.Updated.Should().Be(0);

        var countAfter3 = await dbContext.EmployeeLeaves.CountAsync();
        countAfter3.Should().Be(2); // New record created as expected
    }

    [Fact]
    public async Task AppreciationLetterType_PersistsSeniorityYearsMonthsDaysCorrectly()
    {
        using var dbContext = CreateInMemoryDbContext();

        var type = new AppreciationLetterType
        {
            Name = "كتاب شكر وزاري",
            SeniorityYears = 0,
            SeniorityMonths = 1,
            SeniorityDays = 15
        };

        dbContext.AppreciationLetterTypes.Add(type);
        await dbContext.SaveChangesAsync();

        var retrieved = await dbContext.AppreciationLetterTypes.FirstOrDefaultAsync(t => t.Name == "كتاب شكر وزاري");
        retrieved.Should().NotBeNull();
        retrieved!.SeniorityYears.Should().Be(0);
        retrieved.SeniorityMonths.Should().Be(1);
        retrieved.SeniorityDays.Should().Be(15);
    }

    [Fact]
    public void AppreciationLetterType_ZeroSeniority_DoesNotYieldDefaultMonth()
    {
        var zeroType = new AppreciationLetterType
        {
            Name = "كتاب شكر بدون قدم",
            SeniorityYears = 0,
            SeniorityMonths = 0,
            SeniorityDays = 0
        };

        var letter = new AppreciationLetter
        {
            Count = 1,
            Type = zeroType
        };

        // Calculation strictly from type values:
        var totalYears = zeroType.SeniorityYears * (letter.Count ?? 1);
        var totalMonths = zeroType.SeniorityMonths * (letter.Count ?? 1);
        var totalDays = zeroType.SeniorityDays * (letter.Count ?? 1);

        totalYears.Should().Be(0);
        totalMonths.Should().Be(0);
        totalDays.Should().Be(0);
    }

    [Fact]
    public async Task CareerMovementsTemplate_CanBeGeneratedAndParsedRoundTrip()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // Generate template
        var bytes = service.GenerateCareerMovementsTemplate();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(100);

        // Parse it back
        using var ms = new MemoryStream(bytes);
        var result = await service.ParseCareerMovementsExcelAsync(ms);

        result.Should().NotBeNull();
        result.HasErrors.Should().BeFalse();
        result.Rows.Should().HaveCount(2);

        // Row 1: inside service -> all 3 flags true
        var row1 = result.Rows[0];
        row1.EmployeeID.Should().Be("1001");
        row1.EmployeeName.Should().Be("أحمد شاكر كريم");
        row1.FormationName.Should().Be("دائرة العمليات والمدفوعات");
        row1.CountForAllowance.Should().BeTrue();
        row1.CountForPromotion.Should().BeTrue();
        row1.CountForPension.Should().BeTrue();

        // Row 2: outside service -> all 3 flags false
        var row2 = result.Rows[1];
        row2.EmployeeID.Should().Be("1001");
        row2.FormationName.Should().Be("خارج الخدمة - إجازة 5 سنوات");
        row2.CountForAllowance.Should().BeFalse();
        row2.CountForPromotion.Should().BeFalse();
        row2.CountForPension.Should().BeFalse();
    }

    [Fact]
    public async Task ImportCareerMovements_UpsertsAndTracksRecognizedServiceFlags()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // Seed an employee
        var employee = new Employee
        {
            Id = 1,
            EmployeeID = "EMP_MOV_TEST",
            Name = "سالم عبد الحسين",
            HireDate = new DateOnly(2015, 1, 1)
        };
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var rows = new[]
        {
            new CareerMovementImportRow(
                EmployeeID: "EMP_MOV_TEST",
                MovementDate: "2015-01-01",
                FormationName: "دائرة العمليات المالية",
                CountForAllowance: true,
                CountForPromotion: true,
                CountForPension: true,
                Notes: "تعيين ومباشرة أولى",
                EmployeeName: "سالم عبد الحسين (يهمل من التخزين)"
            ),
            new CareerMovementImportRow(
                EmployeeID: "EMP_MOV_TEST",
                MovementDate: "2020-06-01",
                FormationName: "خارج الخدمة - إجازة خاصة",
                CountForAllowance: false,
                CountForPromotion: false,
                CountForPension: false,
                Notes: "إجازة 4 سنوات",
                EmployeeName: "سالم عبد الحسين"
            ),
            new CareerMovementImportRow(
                EmployeeID: "EMP_NOT_FOUND_999",
                MovementDate: "2022-01-01",
                FormationName: "مكتب المدير العام",
                CountForAllowance: true,
                CountForPromotion: true,
                CountForPension: true,
                Notes: null,
                EmployeeName: "موظف غير مسجل"
            )
        };

        var request = new CareerMovementImportRequest("upsert", rows);
        var response = await service.ImportCareerMovementsAsync(request);

        response.TotalRows.Should().Be(3);
        response.Inserted.Should().Be(2);
        response.Skipped.Should().Be(1);
        response.MissingEmployeeIDs.Should().Contain("EMP_NOT_FOUND_999");

        var dbMovements = await dbContext.EmployeeCareerMovements
            .Where(m => m.EmployeeId == employee.Id)
            .OrderBy(m => m.MovementDate)
            .ToListAsync();

        dbMovements.Should().HaveCount(2);

        // First movement
        dbMovements[0].MovementDate.Should().Be(new DateOnly(2015, 1, 1));
        dbMovements[0].FormationName.Should().Be("دائرة العمليات المالية");
        dbMovements[0].CountForAllowance.Should().BeTrue();
        dbMovements[0].CountForPromotion.Should().BeTrue();
        dbMovements[0].CountForPension.Should().BeTrue();

        // Second movement (outside service)
        dbMovements[1].MovementDate.Should().Be(new DateOnly(2020, 6, 1));
        dbMovements[1].FormationName.Should().Be("خارج الخدمة - إجازة خاصة");
        dbMovements[1].CountForAllowance.Should().BeFalse();
        dbMovements[1].CountForPromotion.Should().BeFalse();
        dbMovements[1].CountForPension.Should().BeFalse();

        // Re-uploading with updated notes updates existing record instead of duplicating
        var updatedRows = new[]
        {
            new CareerMovementImportRow(
                EmployeeID: "EMP_MOV_TEST",
                MovementDate: "2015-01-01",
                FormationName: "دائرة العمليات والمدفوعات (معدل)",
                CountForAllowance: true,
                CountForPromotion: true,
                CountForPension: true,
                Notes: "تم تعديل الملاحظات والتسمية",
                EmployeeName: null
            )
        };

        var req2 = new CareerMovementImportRequest("upsert", updatedRows);
        var res2 = await service.ImportCareerMovementsAsync(req2);

        res2.Inserted.Should().Be(0);
        res2.Updated.Should().Be(1);

        var countTotal = await dbContext.EmployeeCareerMovements.CountAsync();
        countTotal.Should().Be(2); // Still 2 records

        var updatedMov = await dbContext.EmployeeCareerMovements.FirstAsync(m => m.MovementDate == new DateOnly(2015, 1, 1));
        updatedMov.FormationName.Should().Be("دائرة العمليات والمدفوعات (معدل)");
        updatedMov.Notes.Should().Be("تم تعديل الملاحظات والتسمية");
    }

    [Fact]
    public async Task SummaryLeavesExcel_WithHeaderOnRow3_ParsesAndImportsElasticHeadersCorrectly()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // 1. Seed employees in DB
        dbContext.Employees.AddRange(
            new Employee { Id = 1, EmployeeID = "EMP_ELASTIC_1", Name = "علي أحمد شاكر" },
            new Employee { Id = 2, EmployeeID = "EMP_ELASTIC_2", Name = "سارة كريم حسن" }
        );
        await dbContext.SaveChangesAsync();

        // 2. Build Excel file with Title banner on Row 1, Subtitle on Row 2, and Headers on Row 3
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("خلاصة الإجازات");

        // Row 1: Title Banner
        ws.Cell(1, 1).Value = "كشف خلاصة الإجازات السنوية للموظفين لعام 2024";
        // Row 2: Subtitle
        ws.Cell(2, 1).Value = "تاريخ الإصدار: 2024-09-01";

        // Row 3: The actual headers as requested by user
        // رقم الموظف | أسم الموظف | مجموع  الإجازات | نوع الإجازة | أسم الدائرة
        ws.Cell(3, 1).Value = "رقم الموظف";
        ws.Cell(3, 2).Value = "أسم الموظف";
        ws.Cell(3, 3).Value = "مجموع  الإجازات";
        ws.Cell(3, 4).Value = "نوع الإجازة";
        ws.Cell(3, 5).Value = "أسم الدائرة";

        // Row 4: Data row with Eastern Arabic numerals for days (١٥)
        ws.Cell(4, 1).Value = "EMP_ELASTIC_1";
        ws.Cell(4, 2).Value = "علي أحمد شاكر";
        ws.Cell(4, 3).Value = "١٥"; // 15 in Eastern Arabic
        ws.Cell(4, 4).Value = "إجازة اعتيادية";
        ws.Cell(4, 5).Value = "دائرة المحاسبة المالية";

        // Row 5: Data row with Western Arabic numerals (20)
        ws.Cell(5, 1).Value = "EMP_ELASTIC_2";
        ws.Cell(5, 2).Value = "سارة كريم حسن";
        ws.Cell(5, 3).Value = "20";
        ws.Cell(5, 4).Value = "إجازة مرضية";
        ws.Cell(5, 5).Value = "دائرة الرقابة والتدقيق";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        // 3. Parse Excel file using ExcelImportService
        var parseResult = await service.ParseLeavesExcelAsync(ms);

        parseResult.Should().NotBeNull();
        parseResult.HasErrors.Should().BeFalse();
        parseResult.Rows.Should().HaveCount(2);

        // Check detected headers and unmapped headers
        parseResult.DetectedHeaders.Should().Contain("رقم الموظف");
        parseResult.DetectedHeaders.Should().Contain("مجموع  الإجازات");
        parseResult.DetectedHeaders.Should().Contain("نوع الإجازة");
        parseResult.UnmappedHeaders.Should().Contain("أسم الموظف");  // أسم الموظف يهمل من الحفظ
        parseResult.UnmappedHeaders.Should().Contain("أسم الدائرة"); // أسم الدائرة يهمل من الحفظ

        // Verify row 1 details
        var r1 = parseResult.Rows[0];
        r1.EmployeeID.Should().Be("EMP_ELASTIC_1");
        r1.DaysCount.Should().Be("15"); // Converted from ١٥
        r1.LeaveType.Should().Be("إجازة اعتيادية");

        // Verify row 2 details
        var r2 = parseResult.Rows[1];
        r2.EmployeeID.Should().Be("EMP_ELASTIC_2");
        r2.DaysCount.Should().Be("20");
        r2.LeaveType.Should().Be("إجازة مرضية");

        // 4. Import leaves into database
        var importReq = new LeaveImportRequest("upsert", parseResult.Rows, CreateMissingLookups: true);
        var importRes = await service.ImportLeavesAsync(importReq);

        importRes.Inserted.Should().Be(2);
        importRes.Updated.Should().Be(0);

        // 5. Verify records in DB
        var leave1 = await dbContext.EmployeeLeaves
            .Include(l => l.Employee)
            .Include(l => l.LeaveType)
            .FirstOrDefaultAsync(l => l.Employee.EmployeeID == "EMP_ELASTIC_1");

        leave1.Should().NotBeNull();
        leave1!.DaysCount.Should().Be(15);
        leave1.LeaveType!.Name.Should().Be("إجازة اعتيادية");
        leave1.Status.Should().Be("معتمدة");

        var leave2 = await dbContext.EmployeeLeaves
            .Include(l => l.Employee)
            .Include(l => l.LeaveType)
            .FirstOrDefaultAsync(l => l.Employee.EmployeeID == "EMP_ELASTIC_2");

        leave2.Should().NotBeNull();
        leave2!.DaysCount.Should().Be(20);
        leave2.LeaveType!.Name.Should().Be("إجازة مرضية");

        // 6. Test Upsert: re-importing updated total leaves for EMP_ELASTIC_1 updates existing record without duplicate
        var updatedRows = new[]
        {
            new LeaveImportRow(
                EmployeeID: "EMP_ELASTIC_1",
                LeaveType: "إجازة اعتيادية",
                StartDate: r1.StartDate,
                EndDate: null,
                DaysCount: "25",
                OrderNumber: null,
                OrderDate: null,
                Notes: "تحديث مجموع الإجازات",
                Status: "معتمدة",
                EmployeeName: "علي أحمد شاكر"
            )
        };

        var updateReq = new LeaveImportRequest("upsert", updatedRows, CreateMissingLookups: true);
        var updateRes = await service.ImportLeavesAsync(updateReq);

        updateRes.Inserted.Should().Be(0);
        updateRes.Updated.Should().Be(1);

        var totalLeavesCount = await dbContext.EmployeeLeaves.CountAsync();
        totalLeavesCount.Should().Be(2); // Still 2 records, no duplicates!

        var refreshedLeave1 = await dbContext.EmployeeLeaves.FirstAsync(l => l.Employee.EmployeeID == "EMP_ELASTIC_1");
        refreshedLeave1.DaysCount.Should().Be(25);
    }

    [Theory]
    [InlineData("15", "15")]
    [InlineData(" 20 ", "20")]
    [InlineData("١٥", "15")]
    [InlineData("٣٠.0", "30")]
    [InlineData("5.5", "5.5")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void NormalizeDaysCount_FormatsCorrectly(string? input, string? expected)
    {
        var result = ExcelImportService.NormalizeDaysCount(input);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task JobTitlesTemplate_CanBeGeneratedAndParsedRoundTrip()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // Generate template
        var bytes = service.GenerateJobTitlesTemplate();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(100);

        // Parse it back
        using var ms = new MemoryStream(bytes);
        var result = await service.ParseJobTitlesExcelAsync(ms);

        result.Should().NotBeNull();
        result.HasErrors.Should().BeFalse();
        result.Rows.Should().HaveCount(2);

        // Check sample row 1
        var row1 = result.Rows[0];
        row1.Code.Should().Be("101");
        row1.JobFamily.Should().Be("العائلة الإدارية");
        row1.Name.Should().Be("مدير عام");
        row1.GradeName.Should().Be("الدرجة الأولى");

        // Check sample row 2
        var row2 = result.Rows[1];
        row2.Code.Should().Be("201");
        row2.JobFamily.Should().Be("العائلة المصرفية");
        row2.Name.Should().Be("رئيس أبحاث");
        row2.GradeName.Should().Be("الدرجة الثانية");
    }

    [Fact]
    public async Task ImportJobTitles_CreatesNewAndUpdatesExistingTitles()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        // Seed a grade and an existing job title with a job family
        var grade1 = new Grade { Name = "الدرجة الأولى", Code = "G1" };
        var grade2 = new Grade { Name = "الدرجة الثانية", Code = "G2" };
        dbContext.Grades.AddRange(grade1, grade2);

        var oldFamily = new JobFamily { Name = "قديم" };
        dbContext.JobFamilies.Add(oldFamily);

        var existingTitle = new JobTitle { Name = "مدير عام", Code = "OLD_101", JobFamily = oldFamily };
        dbContext.JobTitles.Add(existingTitle);
        await dbContext.SaveChangesAsync();

        var rows = new List<JobTitleImportRow>
        {
            new(RowNumber: 1, Code: "101", JobFamily: "العائلة الإدارية", Name: "مدير عام", GradeName: "الدرجة الأولى", IsExisting: true),
            new(RowNumber: 2, Code: "201", JobFamily: "العائلة المصرفية", Name: "رئيس أبحاث", GradeName: "الدرجة الثانية", IsExisting: false)
        };

        var response = await service.ImportJobTitlesAsync(new JobTitleImportRequest(rows, AutoLinkGrades: true));

        response.TotalRows.Should().Be(2);
        response.UpdatedCount.Should().Be(1);
        response.CreatedCount.Should().Be(1);

        // Verify updated title
        var updated = await dbContext.JobTitles.Include(j => j.JobFamily).FirstAsync(j => j.Name == "مدير عام");
        updated.Code.Should().Be("101");
        updated.JobFamily.Should().NotBeNull();
        updated.JobFamily!.Name.Should().Be("العائلة الإدارية");
        updated.GradeId.Should().Be(grade1.Id);

        // Verify newly created title
        var created = await dbContext.JobTitles.Include(j => j.JobFamily).FirstAsync(j => j.Name == "رئيس أبحاث");
        created.Code.Should().Be("201");
        created.JobFamily.Should().NotBeNull();
        created.JobFamily!.Name.Should().Be("العائلة المصرفية");
        created.GradeId.Should().Be(grade2.Id);

        // Verify JobFamilies table has the auto-created families
        var families = await dbContext.JobFamilies.ToListAsync();
        families.Should().Contain(f => f.Name == "العائلة الإدارية");
        families.Should().Contain(f => f.Name == "العائلة المصرفية");
    }

    [Fact]
    public async Task ImportJobTitles_LinksGradeByCode_WithVariousFormats()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ExcelImportService(dbContext);

        var grade1 = new Grade { Id = 11, Name = "الدرجه الاولى", Code = "1", SortOrder = 5 };
        var grade2 = new Grade { Id = 9, Name = "الدرجة الثانية", Code = "2", SortOrder = 6 };
        var gradeSpecial = new Grade { Id = 13, Name = "الدرجة العليا أ", Code = "العليا أ", SortOrder = 1 };
        dbContext.Grades.AddRange(grade1, grade2, gradeSpecial);
        await dbContext.SaveChangesAsync();

        var rows = new List<JobTitleImportRow>
        {
            new(RowNumber: 1, Code: "T1", JobFamily: "الإدارة", Name: "معاون مدير عام", GradeName: "1", IsExisting: false),
            new(RowNumber: 2, Code: "T2", JobFamily: "الرقابة", Name: "رئيس مدققين", GradeName: "٢", IsExisting: false), // Eastern Arabic digit
            new(RowNumber: 3, Code: "T3", JobFamily: "القيادة", Name: "مستشار", GradeName: "العليا أ", IsExisting: false),
        };

        var response = await service.ImportJobTitlesAsync(new JobTitleImportRequest(rows, AutoLinkGrades: true));

        response.CreatedCount.Should().Be(3);

        var title1 = await dbContext.JobTitles.Include(j => j.JobFamily).Include(j => j.Grade).FirstAsync(j => j.Name == "معاون مدير عام");
        title1.GradeId.Should().Be(grade1.Id);
        title1.Grade!.Name.Should().Be("الدرجه الاولى");
        title1.JobFamily!.Name.Should().Be("الإدارة");

        var title2 = await dbContext.JobTitles.Include(j => j.JobFamily).Include(j => j.Grade).FirstAsync(j => j.Name == "رئيس مدققين");
        title2.GradeId.Should().Be(grade2.Id);
        title2.Grade!.Name.Should().Be("الدرجة الثانية");
        title2.JobFamily!.Name.Should().Be("الرقابة");

        var title3 = await dbContext.JobTitles.Include(j => j.JobFamily).Include(j => j.Grade).FirstAsync(j => j.Name == "مستشار");
        title3.GradeId.Should().Be(gradeSpecial.Id);
        title3.Grade!.Name.Should().Be("الدرجة العليا أ");
        title3.JobFamily!.Name.Should().Be("القيادة");
    }

    [Fact]
    public async Task UserVideoExcel_CanBeParsedSuccessfully()
    {
        var filePath = @"C:\Users\Administrator\Videos\العناوين الوظيفية.xlsx";
        if (!File.Exists(filePath)) return;

        using var dbContext = CreateInMemoryDbContext();
        var grade1 = new Grade { Id = 1, Name = "الدرجه الاولى", Code = "1", SortOrder = 1 };
        var grade2 = new Grade { Id = 2, Name = "الدرجة الثانية", Code = "2", SortOrder = 2 };
        var grade3 = new Grade { Id = 3, Name = "الدرجة الثالثة", Code = "3", SortOrder = 3 };
        var grade4 = new Grade { Id = 4, Name = "الدرجة الرابعة", Code = "4", SortOrder = 4 };
        var grade5 = new Grade { Id = 5, Name = "الدرجة الخامسة", Code = "5", SortOrder = 5 };
        var grade6 = new Grade { Id = 6, Name = "الدرجة السادسة", Code = "6", SortOrder = 6 };
        var grade7 = new Grade { Id = 7, Name = "الدرجة السابعة", Code = "7", SortOrder = 7 };
        var grade8 = new Grade { Id = 8, Name = "الدرجة الثامنة", Code = "8", SortOrder = 8 };
        var gradeSpecialA = new Grade { Id = 13, Name = "الدرجة العليا أ", Code = "العليا أ", SortOrder = 1 };
        var gradeSpecialB = new Grade { Id = 14, Name = "الدرجة العليا ب", Code = "العليا ب", SortOrder = 2 };
        dbContext.Grades.AddRange(grade1, grade2, grade3, grade4, grade5, grade6, grade7, grade8, gradeSpecialA, gradeSpecialB);
        await dbContext.SaveChangesAsync();

        var service = new ExcelImportService(dbContext);

        await using var fs = File.OpenRead(filePath);
        var result = await service.ParseJobTitlesExcelAsync(fs);

        Assert.NotNull(result);
        Assert.Empty(result.ParseErrors);
        Assert.True(result.Rows.Count >= 100);

        // Perform actual import
        var response = await service.ImportJobTitlesAsync(new JobTitleImportRequest(result.Rows, AutoLinkGrades: true));
        Assert.True(response.CreatedCount > 0);

        var families = await dbContext.JobFamilies.ToListAsync();
        Assert.True(families.Count > 0);
    }
}





