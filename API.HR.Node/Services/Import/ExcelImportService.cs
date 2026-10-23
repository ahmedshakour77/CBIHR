using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Data.Allowances;
using API.HR.Node.Endpoints;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace API.HR.Node.Services.Import;

public sealed class ExcelImportService : IExcelImportService
{
    private readonly ApplicationDbContext _dbContext;

    public ExcelImportService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ==========================================
    // 1. Employees Import & Parsing
    // ==========================================

    public Task<ExcelParseResult<EmployeeImportRow>> ParseEmployeesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return Task.FromResult(new ExcelParseResult<EmployeeImportRow>(
                Array.Empty<EmployeeImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0));
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchEmployeeField, "EmployeeID");
        if (headerRow is null)
        {
            return Task.FromResult(new ExcelParseResult<EmployeeImportRow>(
                Array.Empty<EmployeeImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة.") },
                0));
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (!colMap.ContainsKey("Name"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الاسم' أو 'اسم الموظف'."));
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(new ExcelParseResult<EmployeeImportRow>(
                Array.Empty<EmployeeImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0));
        }

        var rows = new List<EmployeeImportRow>();
        var totalRows = 0;
        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            totalRows++;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID");
            var name = GetVal("Name");

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: الرقم الوظيفي مطلوب."));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new EmployeeImportError(rowNum, "Name", $"الصف {rowNum}: اسم الموظف مطلوب."));
            }

            rows.Add(new EmployeeImportRow(
                EmployeeID: empId ?? string.Empty,
                Name: name ?? string.Empty,
                Formation: GetVal("Formation"),
                JobTitle: GetVal("JobTitle"),
                PositionTitle: GetVal("PositionTitle"),
                Stage: GetVal("Stage"),
                Grade: GetVal("Grade"),
                EducationLevel: GetVal("EducationLevel"),
                Specialization: GetVal("Specialization"),
                LastJobTitleDate: GetVal("LastJobTitleDate"),
                HireDate: GetVal("HireDate"),
                Gender: GetVal("Gender"),
                Religion: GetVal("Religion"),
                Nationality: GetVal("Nationality"),
                FirstDepartment: GetVal("FirstDepartment"),
                BirthDate: GetVal("BirthDate"),
                EmployeeStatus: GetVal("EmployeeStatus"),
                LastAllowanceDate: GetVal("LastAllowanceDate")
            ));
        }

        return Task.FromResult(new ExcelParseResult<EmployeeImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows));
    }

    public async Task<EmployeeImportResponse> ImportEmployeesAsync(EmployeeImportRequest request, CancellationToken cancellationToken = default)
    {
        var (success, response, errors, message) = await EmployeeImportEndpoints.ExecuteImportAsync(request, _dbContext, cancellationToken);
        if (!success)
        {
            var errorDetails = errors is { Count: > 0 }
                ? string.Join(" | ", errors.Take(5).Select(e => $"صف {e.Row}: {e.Message}"))
                : message ?? "فشل استيراد الموظفين.";

            throw new InvalidOperationException(errorDetails);
        }

        return response!;
    }

    // ==========================================
    // 2. Appreciation Letters Import & Parsing
    // ==========================================

    public Task<ExcelParseResult<AppreciationLetterImportRow>> ParseAppreciationLettersExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return Task.FromResult(new ExcelParseResult<AppreciationLetterImportRow>(
                Array.Empty<AppreciationLetterImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0));
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchLetterField, "EmployeeID");
        if (headerRow is null)
        {
            return Task.FromResult(new ExcelParseResult<AppreciationLetterImportRow>(
                Array.Empty<AppreciationLetterImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة.") },
                0));
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(new ExcelParseResult<AppreciationLetterImportRow>(
                Array.Empty<AppreciationLetterImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0));
        }

        var rows = new List<AppreciationLetterImportRow>();
        var totalRows = 0;
        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            totalRows++;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID");

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: الرقم الوظيفي مطلوب."));
            }

            rows.Add(new AppreciationLetterImportRow(
                EmployeeID: empId ?? string.Empty,
                Title: GetVal("Title"),
                LetterDate: GetVal("LetterDate"),
                Count: GetVal("Count"),
                Type: GetVal("Type"),
                Notes: GetVal("Notes"),
                LetterNumber: GetVal("LetterNumber")
            ));
        }

        return Task.FromResult(new ExcelParseResult<AppreciationLetterImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows));
    }

    public async Task<AppreciationLetterImportResponse> ImportAppreciationLettersAsync(AppreciationLetterImportRequest request, CancellationToken cancellationToken = default)
    {
        var (success, response, errors, message) = await AppreciationLetterImportEndpoints.ExecuteImportAsync(request, _dbContext, cancellationToken);
        if (!success)
        {
            var errorDetails = errors is { Count: > 0 }
                ? string.Join(" | ", errors.Take(5).Select(e => $"صف {e.Row}: {e.Message}"))
                : message ?? "فشل استيراد كتب الشكر.";

            throw new InvalidOperationException(errorDetails);
        }

        return response!;
    }

    // ==========================================
    // 3. Employee Leaves Import & Parsing
    // ==========================================

    public Task<ExcelParseResult<LeaveImportRow>> ParseLeavesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return Task.FromResult(new ExcelParseResult<LeaveImportRow>(
                Array.Empty<LeaveImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0));
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchLeaveField, "EmployeeID");
        if (headerRow is null)
        {
            return Task.FromResult(new ExcelParseResult<LeaveImportRow>(
                Array.Empty<LeaveImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة.") },
                0));
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (!colMap.ContainsKey("StartDate") && !colMap.ContainsKey("DaysCount"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود تاريخ بدء الإجازة ('تاريخ البدء') أو مجموع الإجازات ('مجموع الإجازات' / 'عدد الأيام')."));
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(new ExcelParseResult<LeaveImportRow>(
                Array.Empty<LeaveImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0));
        }

        var rows = new List<LeaveImportRow>();
        var totalRows = 0;
        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID");
            var start = GetVal("StartDate");
            var daysRaw = GetVal("DaysCount");
            var normDays = NormalizeDaysCount(daysRaw);

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(normDays))
            {
                continue;
            }

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: الرقم الوظيفي مطلوب."));
            }

            if (string.IsNullOrWhiteSpace(start))
            {
                if (!string.IsNullOrWhiteSpace(normDays))
                {
                    start = new DateOnly(DateTime.Today.Year, 1, 1).ToString("yyyy-MM-dd");
                }
                else
                {
                    errors.Add(new EmployeeImportError(rowNum, "StartDate", $"الصف {rowNum}: تاريخ بدء الإجازة أو مجموع الإجازات مطلوب."));
                }
            }

            var endVal = GetVal("EndDate");
            if (string.IsNullOrWhiteSpace(endVal) && !string.IsNullOrWhiteSpace(start) && !string.IsNullOrWhiteSpace(normDays))
            {
                if (int.TryParse(normDays, out var d) && d > 0 && DateOnly.TryParse(start, out var sDate))
                {
                    endVal = sDate.AddDays(Math.Max(1, d) - 1).ToString("yyyy-MM-dd");
                }
            }

            rows.Add(new LeaveImportRow(
                EmployeeID: empId ?? string.Empty,
                LeaveType: GetVal("LeaveType"),
                StartDate: start ?? string.Empty,
                EndDate: endVal,
                DaysCount: normDays,
                OrderNumber: GetVal("OrderNumber"),
                OrderDate: GetVal("OrderDate"),
                Notes: GetVal("Notes"),
                Status: GetVal("Status"),
                EmployeeName: GetVal("EmployeeName")
            ));
        }

        return Task.FromResult(new ExcelParseResult<LeaveImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows));
    }

    public async Task<LeaveImportResponse> ImportLeavesAsync(LeaveImportRequest request, CancellationToken cancellationToken = default)
    {
        var (success, response, errors, message) = await LeaveEndpoints.ExecuteImportAsync(request, _dbContext, cancellationToken);
        if (!success)
        {
            var errorDetails = errors is { Count: > 0 }
                ? string.Join(" | ", errors.Take(5).Select(e => $"صف {e.Row}: {e.Message}"))
                : message ?? "فشل استيراد الإجازات.";

            throw new InvalidOperationException(errorDetails);
        }

        return response!;
    }

    public async Task<ExcelParseResult<LeaveBalanceImportRow>> ParseLeaveBalancesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ExcelParseResult<LeaveBalanceImportRow>(
                Array.Empty<LeaveBalanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0);
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindLeaveBalancesHeaderRow(worksheet);
        if (headerRow is null)
        {
            return new ExcelParseResult<LeaveBalanceImportRow>(
                Array.Empty<LeaveBalanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "لم يتم العثور على صف العناوين أو ورقة العمل فارغة.") },
                0);
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (!colMap.ContainsKey("Balance"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عامود باسم 'الرصيد' في ملف الإكسل. يرجى التأكد من تسمية عامود الرصيد بـ 'الرصيد'."));
        }

        if (errors.Count > 0)
        {
            return new ExcelParseResult<LeaveBalanceImportRow>(
                Array.Empty<LeaveBalanceImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0);
        }

        var rawRows = new List<(int RowNum, string EmpId, string? Name, string? RawBalance, decimal? ParsedBalance)>();
        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID")?.Trim();
            var rawBal = GetVal("Balance")?.Trim();
            var name = GetVal("EmployeeName")?.Trim();

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(rawBal))
                continue;

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: الرقم الوظيفي مطلوب."));
                continue;
            }

            decimal? parsedBalance = null;
            if (!string.IsNullOrWhiteSpace(rawBal))
            {
                var sb = new StringBuilder();
                foreach (var ch in rawBal)
                {
                    if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
                    else if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
                    else sb.Append(ch);
                }
                var cleanedDigits = sb.ToString().Trim();
                var match = System.Text.RegularExpressions.Regex.Match(cleanedDigits, @"-?\d+(?:\.\d+)?");
                if (match.Success && decimal.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var bVal))
                {
                    parsedBalance = bVal;
                }
            }

            rawRows.Add((rowNum, empId, name, rawBal, parsedBalance));
        }

        var empIds = rawRows.Select(r => r.EmpId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => empIds.Contains(e.EmployeeID))
            .ToDictionaryAsync(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var rows = new List<LeaveBalanceImportRow>();
        foreach (var r in rawRows)
        {
            var isMatched = employees.TryGetValue(r.EmpId, out var emp);
            var statusMsg = !isMatched
                ? "الرقم الوظيفي غير مسجل بالنظام"
                : (!r.ParsedBalance.HasValue ? "تعذر قراءة رقم الرصيد" : "مطابق وجاهز للتحديث");

            rows.Add(new LeaveBalanceImportRow(
                EmployeeID: r.EmpId,
                RawBalance: r.RawBalance,
                Balance: r.ParsedBalance,
                EmployeeName: r.Name,
                IsMatched: isMatched,
                MatchedName: emp?.Name,
                PreviousBalance: emp?.LeaveBalance,
                StatusMessage: statusMsg
            ));
        }

        return new ExcelParseResult<LeaveBalanceImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows
        );
    }

    public async Task<LeaveBalanceImportResponse> ImportLeaveBalancesAsync(LeaveBalanceImportRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Rows.Count == 0)
        {
            return new LeaveBalanceImportResponse(0, 0, 0, Array.Empty<string>(), "لا توجد صفوف للاستيراد.");
        }

        var validRows = request.Rows.Where(r => !string.IsNullOrWhiteSpace(r.EmployeeID) && r.Balance.HasValue).ToList();
        var empIds = validRows.Select(r => r.EmployeeID.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var employees = await _dbContext.Employees
            .Where(e => empIds.Contains(e.EmployeeID))
            .ToDictionaryAsync(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var updated = 0;
        var skipped = 0;
        var missingIds = new List<string>();

        foreach (var row in validRows)
        {
            if (employees.TryGetValue(row.EmployeeID.Trim(), out var emp))
            {
                emp.LeaveBalance = row.Balance!.Value;
                updated++;
            }
            else
            {
                skipped++;
                missingIds.Add(row.EmployeeID);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var message = $"تم تحديث رصيد الإجازات المتبقي بنجاح لـ {updated} موظفاً" +
                      (skipped > 0 ? $"، وتم تخطي {skipped} لعدم مطابقة الرقم الوظيفي بالنظام." : ".");

        return new LeaveBalanceImportResponse(request.Rows.Count, updated, skipped, missingIds, message);
    }

    // ==========================================
    // 4. Employee Penalties (العقوبات الانضباطية)
    // ==========================================

    private static bool TryParsePenaltyDate(string? rawValue, out DateOnly? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(rawValue)) return true;

        var sb = new StringBuilder();
        foreach (var ch in rawValue.Trim())
        {
            if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim();

        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "yyyy/MM/dd", "d-M-yyyy", "dd-MM-yyyy", "M/d/yyyy", "MM/dd/yyyy" };
        if (DateOnly.TryParseExact(cleaned, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            value = parsed;
            return true;
        }

        if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            value = DateOnly.FromDateTime(dt);
            return true;
        }

        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate) && oaDate > 20000 && oaDate < 70000)
        {
            try
            {
                value = DateOnly.FromDateTime(DateTime.FromOADate(oaDate));
                return true;
            }
            catch { }
        }

        return false;
    }

    public Task<ExcelParseResult<EmployeePenaltyImportRow>> ParsePenaltiesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return Task.FromResult(new ExcelParseResult<EmployeePenaltyImportRow>(
                Array.Empty<EmployeePenaltyImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0));
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchPenaltyField, "EmployeeID");
        if (headerRow is null)
        {
            return Task.FromResult(new ExcelParseResult<EmployeePenaltyImportRow>(
                Array.Empty<EmployeePenaltyImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة أو لم يتم العثور على صف العناوين.") },
                0));
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(new ExcelParseResult<EmployeePenaltyImportRow>(
                Array.Empty<EmployeePenaltyImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0));
        }

        var rows = new List<EmployeePenaltyImportRow>();
        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID")?.Trim();
            var empName = GetVal("EmployeeName")?.Trim();
            var directorate = GetVal("Directorate")?.Trim();
            var jobTitle = GetVal("JobTitle")?.Trim();
            var penaltyName = GetVal("PenaltyName")?.Trim();
            var penaltyDate = GetVal("PenaltyDate")?.Trim();
            var orderNo = GetVal("OrderNumber")?.Trim();
            var notes = GetVal("Notes")?.Trim();

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(empName) && string.IsNullOrWhiteSpace(penaltyName))
                continue;

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: الرقم الوظيفي مطلوب لربط العقوبة."));
            }

            if (!string.IsNullOrWhiteSpace(penaltyDate) && !TryParsePenaltyDate(penaltyDate, out _))
            {
                errors.Add(new EmployeeImportError(rowNum, "PenaltyDate", $"الصف {rowNum}: صيغة تاريخ نيل العقوبة غير صحيحة ({penaltyDate})."));
            }

            rows.Add(new EmployeePenaltyImportRow(
                EmployeeID: empId ?? string.Empty,
                EmployeeName: empName,
                Directorate: directorate,
                JobTitle: jobTitle,
                PenaltyName: penaltyName,
                PenaltyDate: penaltyDate,
                OrderNumber: orderNo,
                Notes: notes
            ));
        }

        return Task.FromResult(new ExcelParseResult<EmployeePenaltyImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows));
    }

    public async Task<EmployeePenaltyImportResponse> ImportPenaltiesAsync(EmployeePenaltyImportRequest request, CancellationToken cancellationToken = default)
    {
        var totalRows = request.Penalties.Count;
        if (totalRows == 0)
        {
            return new EmployeePenaltyImportResponse(0, 0, 0, 0, Array.Empty<string>());
        }

        var isUpsert = request.Mode != "insertOnly";
        var requestedEmpIds = request.Penalties
            .Select(p => p.EmployeeID?.Trim())
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var employees = await _dbContext.Employees
            .Where(e => requestedEmpIds.Contains(e.EmployeeID))
            .ToDictionaryAsync(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var penaltyTypes = await _dbContext.PenaltyTypes
            .ToDictionaryAsync(t => t.Name.Trim(), StringComparer.OrdinalIgnoreCase, cancellationToken);

        var employeeDbIds = employees.Values.Select(e => e.Id).ToList();
        var existingPenalties = isUpsert
            ? await _dbContext.EmployeePenalties
                .Where(p => employeeDbIds.Contains(p.EmployeeId))
                .ToListAsync(cancellationToken)
            : new List<EmployeePenalty>();

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var missingEmployeeIds = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in request.Penalties)
        {
            var empId = row.EmployeeID?.Trim();
            if (string.IsNullOrWhiteSpace(empId) || !employees.TryGetValue(empId, out var emp))
            {
                skipped++;
                if (!string.IsNullOrWhiteSpace(empId))
                    missingEmployeeIds.Add(empId);
                continue;
            }

            TryParsePenaltyDate(row.PenaltyDate, out var penaltyDate);
            var orderNo = string.IsNullOrWhiteSpace(row.OrderNumber) ? null : row.OrderNumber.Trim();
            var penaltyName = string.IsNullOrWhiteSpace(row.PenaltyName) ? null : row.PenaltyName.Trim();
            var notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim();
            var directorate = string.IsNullOrWhiteSpace(row.Directorate) ? null : row.Directorate.Trim();
            var jobTitle = string.IsNullOrWhiteSpace(row.JobTitle) ? null : row.JobTitle.Trim();

            // Resolve or create PenaltyType lookup
            int? penaltyTypeId = null;
            if (!string.IsNullOrWhiteSpace(penaltyName))
            {
                if (!penaltyTypes.TryGetValue(penaltyName, out var pType))
                {
                    pType = new PenaltyType
                    {
                        Name = penaltyName,
                        Description = "تمت إضافتها تلقائياً أثناء الاستيراد"
                    };
                    _dbContext.PenaltyTypes.Add(pType);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    penaltyTypes[penaltyName] = pType;
                }
                penaltyTypeId = pType.Id;
            }

            if (isUpsert)
            {
                var existing = existingPenalties.FirstOrDefault(p =>
                    p.EmployeeId == emp.Id &&
                    (
                        (!string.IsNullOrEmpty(orderNo) && string.Equals(p.OrderNumber, orderNo, StringComparison.OrdinalIgnoreCase)) ||
                        (p.PenaltyDate == penaltyDate && p.PenaltyTypeId == penaltyTypeId)
                    ));

                if (existing != null)
                {
                    existing.PenaltyTypeId = penaltyTypeId ?? existing.PenaltyTypeId;
                    existing.PenaltyDate = penaltyDate ?? existing.PenaltyDate;
                    existing.OrderNumber = orderNo ?? existing.OrderNumber;
                    existing.Notes = notes ?? existing.Notes;
                    updated++;
                    continue;
                }
            }

            var newPenalty = new EmployeePenalty
            {
                EmployeeId = emp.Id,
                PenaltyTypeId = penaltyTypeId,
                PenaltyDate = penaltyDate,
                OrderNumber = orderNo,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.EmployeePenalties.Add(newPenalty);
            existingPenalties.Add(newPenalty);
            inserted++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EmployeePenaltyImportResponse(
            TotalRows: totalRows,
            Inserted: inserted,
            Updated: updated,
            Skipped: skipped,
            MissingEmployeeIDs: missingEmployeeIds.ToList()
        );
    }

    public Task<ExcelParseResult<CareerMovementImportRow>> ParseCareerMovementsExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return Task.FromResult(new ExcelParseResult<CareerMovementImportRow>(
                Array.Empty<CareerMovementImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0));
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchCareerMovementField, "EmployeeID");
        if (headerRow is null)
        {
            return Task.FromResult(new ExcelParseResult<CareerMovementImportRow>(
                Array.Empty<CareerMovementImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة.") },
                0));
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (!colMap.ContainsKey("MovementDate"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود تاريخ التنقل / الحركة ('تاريخ التنقل' أو 'التاريخ')."));
        }

        if (!colMap.ContainsKey("FormationName"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود التشكيل / بيان الحركة ('التشكيل' أو 'بيان الحركة')."));
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(new ExcelParseResult<CareerMovementImportRow>(
                Array.Empty<CareerMovementImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0));
        }

        var rows = new List<CareerMovementImportRow>();
        var totalRows = 0;
        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            totalRows++;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID");
            var movDate = GetVal("MovementDate");
            var formName = GetVal("FormationName") ?? "غير محدد";

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(movDate))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: الرقم الوظيفي مطلوب."));
            }

            if (string.IsNullOrWhiteSpace(movDate))
            {
                errors.Add(new EmployeeImportError(rowNum, "MovementDate", $"الصف {rowNum}: تاريخ التنقل مطلوب."));
            }

            bool? ParseBoolOpt(string? val)
            {
                if (string.IsNullOrWhiteSpace(val)) return null;
                val = val.Trim().ToLowerInvariant();
                if (val is "نعم" or "صح" or "true" or "1" or "yes") return true;
                if (val is "لا" or "خطأ" or "false" or "0" or "no") return false;
                return null;
            }

            var isOut = formName.Contains("خارج الخدمة", StringComparison.OrdinalIgnoreCase) ||
                        formName.Contains("انقطاع", StringComparison.OrdinalIgnoreCase) ||
                        formName.Contains("كف يد", StringComparison.OrdinalIgnoreCase);

            var allowance = ParseBoolOpt(GetVal("CountForAllowance")) ?? !isOut;
            var promotion = ParseBoolOpt(GetVal("CountForPromotion")) ?? !isOut;
            var pension = ParseBoolOpt(GetVal("CountForPension")) ?? !isOut;

            rows.Add(new CareerMovementImportRow(
                EmployeeID: empId ?? string.Empty,
                MovementDate: movDate ?? string.Empty,
                FormationName: formName,
                CountForAllowance: allowance,
                CountForPromotion: promotion,
                CountForPension: pension,
                Notes: GetVal("Notes"),
                EmployeeName: GetVal("Name")
            ));
        }

        return Task.FromResult(new ExcelParseResult<CareerMovementImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows));
    }

    public async Task<CareerMovementImportResponse> ImportCareerMovementsAsync(CareerMovementImportRequest request, CancellationToken cancellationToken = default)
    {
        var (success, response, errors, message) = await CareerMovementEndpoints.ExecuteImportAsync(request, _dbContext, cancellationToken);
        if (!success)
        {
            var errorDetails = errors is { Count: > 0 }
                ? string.Join(" | ", errors.Take(5).Select(e => $"صف {e.Row}: {e.Message}"))
                : message ?? "فشل استيراد حركات الخدمة.";

            throw new InvalidOperationException(errorDetails);
        }

        return response!;
    }

    // ==========================================
    // 5. Training Courses Import & Parsing
    // ==========================================

    public Task<ExcelParseResult<CourseImportRow>> ParseCoursesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return Task.FromResult(new ExcelParseResult<CourseImportRow>(
                Array.Empty<CourseImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0));
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchCourseField, "EmployeeID");
        if (headerRow is null)
        {
            return Task.FromResult(new ExcelParseResult<CourseImportRow>(
                Array.Empty<CourseImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة.") },
                0));
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (!colMap.ContainsKey("CourseName"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'اسم الدورة'."));
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(new ExcelParseResult<CourseImportRow>(
                Array.Empty<CourseImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0));
        }

        var rows = new List<CourseImportRow>();
        var totalRows = 0;
        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID");
            var courseName = GetVal("CourseName");

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(courseName))
            {
                continue;
            }

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: الرقم الوظيفي مطلوب."));
            }

            if (string.IsNullOrWhiteSpace(courseName))
            {
                errors.Add(new EmployeeImportError(rowNum, "CourseName", $"الصف {rowNum}: اسم الدورة مطلوب."));
            }

            var start = GetVal("StartDate");
            var end = GetVal("EndDate");
            var days = NormalizeDaysCount(GetVal("DaysCount"));

            rows.Add(new CourseImportRow(
                EmployeeID: empId ?? string.Empty,
                CourseName: courseName ?? string.Empty,
                StartDate: start,
                EndDate: end,
                EmployeeName: GetVal("EmployeeName"),
                DaysCount: days,
                Notes: GetVal("Notes"),
                Status: GetVal("Status") ?? "معتمدة"
            ));
        }

        return Task.FromResult(new ExcelParseResult<CourseImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows));
    }

    public async Task<CourseImportResponse> ImportCoursesAsync(CourseImportRequest request, CancellationToken cancellationToken = default)
    {
        var (success, response, errors, message) = await CourseEndpoints.ExecuteImportAsync(request, _dbContext, cancellationToken);
        if (!success)
        {
            var errorDetails = errors is { Count: > 0 }
                ? string.Join(" | ", errors.Take(5).Select(e => $"صف {e.Row}: {e.Message}"))
                : message ?? "فشل استيراد الدورات التدريبية.";

            throw new InvalidOperationException(errorDetails);
        }

        return response!;
    }

    // ==========================================
    // 5. Civil Data & Unified Card Import & Parsing
    // ==========================================

    public Task<ExcelParseResult<EmployeeCivilDataImportRow>> ParseCivilDataExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return Task.FromResult(new ExcelParseResult<EmployeeCivilDataImportRow>(
                Array.Empty<EmployeeCivilDataImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0));
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchCivilDataField, "EmployeeID");
        if (headerRow is null)
        {
            return Task.FromResult(new ExcelParseResult<EmployeeCivilDataImportRow>(
                Array.Empty<EmployeeCivilDataImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة.") },
                0));
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'رقم الموظف' أو 'الرقم الوظيفي' للربط والتحديث."));
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(new ExcelParseResult<EmployeeCivilDataImportRow>(
                Array.Empty<EmployeeCivilDataImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0));
        }

        var rows = new List<EmployeeCivilDataImportRow>();
        var totalRows = 0;
        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID");
            var nationalId = GetVal("NationalId");
            var unifiedNum = GetVal("UnifiedEmployeeNumber");
            var motherName = GetVal("MotherName");
            var firstName = GetVal("FirstName");
            var secondName = GetVal("SecondName");
            var thirdName = GetVal("ThirdName");
            var fourthName = GetVal("FourthName");
            var fullName = GetVal("FullName");

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(nationalId) && string.IsNullOrWhiteSpace(fullName))
            {
                continue;
            }

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: رقم الموظف مطلوب للربط والتحديث."));
            }

            rows.Add(new EmployeeCivilDataImportRow(
                EmployeeID: empId ?? string.Empty,
                NationalId: nationalId,
                UnifiedEmployeeNumber: unifiedNum,
                MotherName: motherName,
                FirstName: firstName,
                SecondName: secondName,
                ThirdName: thirdName,
                FourthName: fourthName,
                FullName: fullName
            ));
        }

        return Task.FromResult(new ExcelParseResult<EmployeeCivilDataImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows));
    }

    public async Task<EmployeeCivilDataImportResponse> ImportCivilDataAsync(EmployeeCivilDataImportRequest request, CancellationToken cancellationToken = default)
    {
        var (success, response, errors, message) = await EmployeeCivilDataEndpoints.ExecuteImportAsync(request, _dbContext, cancellationToken);
        if (!success)
        {
            var errorDetails = errors is { Count: > 0 }
                ? string.Join(" | ", errors.Take(5).Select(e => $"صف {e.Row}: {e.Message}"))
                : message ?? "فشل استكمال وتحديث بيانات البطاقة الموحدة.";

            throw new InvalidOperationException(errorDetails);
        }

        return response!;
    }

    // ==========================================
    // 7. Employee Email Accounts Import & Parsing
    // ==========================================

    public Task<ExcelParseResult<EmployeeEmailAccountImportRow>> ParseEmailAccountsExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return Task.FromResult(new ExcelParseResult<EmployeeEmailAccountImportRow>(
                Array.Empty<EmployeeEmailAccountImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0));
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchEmailAccountField, "EmployeeID");
        if (headerRow is null)
        {
            return Task.FromResult(new ExcelParseResult<EmployeeEmailAccountImportRow>(
                Array.Empty<EmployeeEmailAccountImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة.") },
                0));
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (!colMap.ContainsKey("Email"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'البريد الإلكتروني' أو 'البريد'."));
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(new ExcelParseResult<EmployeeEmailAccountImportRow>(
                Array.Empty<EmployeeEmailAccountImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0));
        }

        var empIdCol = colMap["EmployeeID"];
        var emailCol = colMap["Email"];
        var nameCol = colMap.GetValueOrDefault("Name", -1);

        var rows = new List<EmployeeEmailAccountImportRow>();
        var lastRowNumber = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;

        for (var rowNum = headerRow.RowNumber() + 1; rowNum <= lastRowNumber; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            var empId = ExtractCellValue(row.Cell(empIdCol))?.Trim() ?? string.Empty;
            var email = (ExtractCellValue(row.Cell(emailCol))?.Trim() ?? string.Empty).ToLowerInvariant();
            var name = nameCol > 0 ? (ExtractCellValue(row.Cell(nameCol))?.Trim() ?? string.Empty) : string.Empty;

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(email))
                continue;

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", "الرقم الوظيفي مطلوب في هذا الصف."));
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                errors.Add(new EmployeeImportError(rowNum, "Email", "البريد الإلكتروني مطلوب في هذا الصف."));
            }
            else if (!email.Contains('@') || !email.Contains('.'))
            {
                errors.Add(new EmployeeImportError(rowNum, "Email", $"عنوان البريد الإلكتروني غير صالح: '{email}'"));
            }

            rows.Add(new EmployeeEmailAccountImportRow(
                EmployeeID: empId,
                Name: name,
                Email: email
            ));
        }

        return Task.FromResult(new ExcelParseResult<EmployeeEmailAccountImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows));
    }

    public async Task<EmployeeEmailAccountImportResponse> ImportEmailAccountsAsync(EmployeeEmailAccountImportRequest request, CancellationToken cancellationToken = default)
    {
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var details = new List<string>();

        var rows = request.Rows;
        if (rows.Count == 0)
        {
            return new EmployeeEmailAccountImportResponse(0, 0, 0, 0, details);
        }

        var empIds = rows.Select(r => r.EmployeeID.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var existingEmps = await _dbContext.Employees
            .Where(e => empIds.Contains(e.EmployeeID))
            .ToDictionaryAsync(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingEmails = await _dbContext.EmployeeEmails
            .Where(e => empIds.Contains(e.EmployeeID))
            .ToListAsync(cancellationToken);

        var emailsByEmpId = existingEmails
            .GroupBy(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var allEmails = rows.Select(r => r.Email.Trim().ToLowerInvariant()).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var existingAllowed = await _dbContext.AllowedUsers
            .Where(w => w.Email != null && allEmails.Contains(w.Email.ToLower()))
            .ToDictionaryAsync(w => w.Email!.ToLower(), cancellationToken);

        var existingUsers = await _dbContext.Users
            .Where(u => (u.EmployeeID != null && empIds.Contains(u.EmployeeID)) ||
                        (u.Email != null && allEmails.Contains(u.Email.ToLower())))
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            var empId = row.EmployeeID.Trim();
            var name = row.Name.Trim();
            var email = row.Email.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(empId) || string.IsNullOrWhiteSpace(email))
                continue;

            var isNewEmp = !existingEmps.TryGetValue(empId, out var employee);
            if (isNewEmp)
            {
                employee = new Employee
                {
                    EmployeeID = empId,
                    Name = string.IsNullOrWhiteSpace(name) ? $"موظف {empId}" : name
                };
                _dbContext.Employees.Add(employee);
                existingEmps[empId] = employee;

                var newEmailRecord = new EmployeeEmail
                {
                    EmployeeID = empId,
                    Email = email,
                    IsPrimary = true,
                    Label = EmailLabel.Work,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.EmployeeEmails.Add(newEmailRecord);

                inserted++;
                details.Add($"إضافة جديدة: {empId} - {employee.Name} ({email})");
            }
            else
            {
                emailsByEmpId.TryGetValue(empId, out var empEmailList);
                empEmailList ??= [];

                var matchingEmail = empEmailList.FirstOrDefault(e => e.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
                var primaryEmail = empEmailList.FirstOrDefault(e => e.IsPrimary);

                if (matchingEmail != null && matchingEmail.IsPrimary)
                {
                    // Existing and already primary - do not touch
                    skipped++;
                }
                else
                {
                    // Changed or missing email: update
                    if (matchingEmail != null)
                    {
                        if (primaryEmail != null && primaryEmail != matchingEmail) primaryEmail.IsPrimary = false;
                        matchingEmail.IsPrimary = true;
                    }
                    else if (primaryEmail != null)
                    {
                        primaryEmail.Email = email;
                    }
                    else
                    {
                        var newEmailRec = new EmployeeEmail
                        {
                            EmployeeID = empId,
                            Email = email,
                            IsPrimary = true,
                            Label = EmailLabel.Work,
                            CreatedAt = DateTimeOffset.UtcNow
                        };
                        _dbContext.EmployeeEmails.Add(newEmailRec);
                        empEmailList.Add(newEmailRec);
                    }

                    if (!string.IsNullOrWhiteSpace(name) && employee!.Name != name)
                    {
                        employee.Name = name;
                    }

                    updated++;
                    details.Add($"تحديث بريد: {empId} - {employee!.Name} ({email})");
                }
            }

            // Sync Allowed Users Whitelist
            if (!existingAllowed.TryGetValue(email, out var au))
            {
                au = new AllowedUser
                {
                    Email = email,
                    FullName = string.IsNullOrWhiteSpace(name) ? employee?.Name : name,
                    Notes = $"استيراد بريد الموظف {empId}",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.AllowedUsers.Add(au);
                existingAllowed[email] = au;
            }
            else if (!au.IsActive)
            {
                au.IsActive = true;
            }

            // Sync ApplicationUser
            var user = existingUsers.FirstOrDefault(u =>
                (u.EmployeeID != null && u.EmployeeID.Equals(empId, StringComparison.OrdinalIgnoreCase)) ||
                (u.Email != null && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

            if (user != null)
            {
                if (user.EmployeeID != empId) user.EmployeeID = empId;
                if (user.Email != email) user.Email = email;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EmployeeEmailAccountImportResponse(rows.Count, inserted, updated, skipped, details);
    }

    // ==========================================
    // 8. Banking Allowances (المسؤولية والمخصصات المصرفية)
    // ==========================================

    public async Task<ExcelParseResult<BankingAllowanceImportRow>> ParseBankingAllowancesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0);
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchBankingAllowanceField, "EmployeeID");
        if (headerRow is null)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة.") },
                0);
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (!colMap.ContainsKey("Responsibility"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'المسؤولية' أو 'المخصصات المصرفية'."));
        }

        if (errors.Count > 0)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0);
        }

        var rawRows = new List<(int RowNum, string EmpId, string? Name, string? Formation, string? JobTitle, string? Stage, string? Grade, string? Cert, string? Spec, string? HireDate, string? RespRaw, decimal? ExtractedPct)>();

        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID")?.Trim();
            var respRaw = GetVal("Responsibility")?.Trim();
            var name = GetVal("EmployeeName")?.Trim();
            var formation = GetVal("Formation")?.Trim();
            var jobTitle = GetVal("JobTitle")?.Trim();
            var stage = GetVal("Stage")?.Trim();
            var grade = GetVal("Grade")?.Trim();
            var cert = GetVal("Certificate")?.Trim();
            var spec = GetVal("Specialization")?.Trim();
            var hireDate = GetVal("HireDate")?.Trim();

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(respRaw) && string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: رقم الموظف مطلوب للربط."));
            }

            var extractedPct = ExtractPercentageFromResponsibility(respRaw);

            rawRows.Add((rowNum, empId ?? string.Empty, name, formation, jobTitle, stage, grade, cert, spec, hireDate, respRaw, extractedPct));
        }

        // Match against database
        var distinctEmpIds = rawRows.Select(r => r.EmpId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => distinctEmpIds.Contains(e.EmployeeID))
            .Select(e => new { e.Id, e.EmployeeID, e.Name, e.GradeId, e.StageId, e.HireDate })
            .ToListAsync(cancellationToken);

        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);
        var empDbIds = employees.Select(e => e.Id).ToList();

        var activePeriods = await _dbContext.EmployeeAllowancePeriods
            .AsNoTracking()
            .Where(p => empDbIds.Contains(p.EmployeeId) && p.IsCurrent)
            .ToDictionaryAsync(p => p.EmployeeId, cancellationToken);

        var salaryScales = await _dbContext.SalaryScales.AsNoTracking().ToListAsync(cancellationToken);

        var rows = new List<BankingAllowanceImportRow>();

        foreach (var r in rawRows)
        {
            var isMatched = empDict.TryGetValue(r.EmpId, out var emp);
            string? matchedName = null;
            decimal? basicSalary = null;
            decimal? calcAmount = null;
            string statusMsg;

            if (isMatched && emp != null)
            {
                matchedName = emp.Name;
                if (activePeriods.TryGetValue(emp.Id, out var activePeriod))
                {
                    basicSalary = activePeriod.BasicSalary;
                }
                else if (emp.GradeId.HasValue && emp.StageId.HasValue)
                {
                    var scale = salaryScales.FirstOrDefault(s => s.GradeId == emp.GradeId.Value && s.StageId == emp.StageId.Value);
                    if (scale != null)
                    {
                        basicSalary = scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
                    }
                }

                if (r.ExtractedPct.HasValue)
                {
                    if (basicSalary.HasValue && basicSalary.Value > 0)
                    {
                        calcAmount = Math.Round((basicSalary.Value * r.ExtractedPct.Value) / 100m, 2);
                        statusMsg = $"جاهز للإضافة بنسبة {r.ExtractedPct.Value}% بمبلغ احتساب شهري {calcAmount:N0} د.ع";
                    }
                    else
                    {
                        statusMsg = $"جاهز للإضافة بنسبة {r.ExtractedPct.Value}% (لم يُحدد الراتب الإسمي بعد)";
                    }
                }
                else
                {
                    statusMsg = "لم يتم العثور على نسبة مئوية في خانة المسؤولية";
                }
            }
            else
            {
                statusMsg = "الرقم الوظيفي غير موجود في جدول الموظفين بالنظام";
            }

            rows.Add(new BankingAllowanceImportRow(
                RowNumber: r.RowNum,
                EmployeeID: r.EmpId,
                EmployeeName: r.Name,
                Formation: r.Formation,
                JobTitle: r.JobTitle,
                Stage: r.Stage,
                Grade: r.Grade,
                Certificate: r.Cert,
                Specialization: r.Spec,
                HireDateStr: r.HireDate,
                ResponsibilityRaw: r.RespRaw,
                ExtractedPercentage: r.ExtractedPct,
                IsMatched: isMatched,
                MatchedName: matchedName,
                BasicSalary: basicSalary,
                CalculatedAmount: calcAmount,
                StatusMessage: statusMsg
            ));
        }

        return new ExcelParseResult<BankingAllowanceImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows);
    }

    public async Task<BankingAllowanceImportResponse> ImportBankingAllowancesAsync(BankingAllowanceImportRequest request, CancellationToken cancellationToken = default)
    {
        var totalRows = request.Rows.Count;
        var importedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;
        var errors = new List<EmployeeImportError>();

        if (totalRows == 0)
        {
            return new BankingAllowanceImportResponse(0, 0, 0, 0, errors, "لم يتم إرسال أي صفوف للاستيراد.");
        }

        // 1. Ensure Banking Allowance Type exists
        var bankingType = await _dbContext.AllowanceTypes.FirstOrDefaultAsync(t => t.Code == "BANKING", cancellationToken);
        if (bankingType == null)
        {
            bankingType = new AllowanceType
            {
                Name = "المخصصات المصرفية",
                Code = "BANKING",
                CalculationType = AllowanceCalculationType.Percentage,
                IsActive = true,
                SortOrder = 1,
                Description = "مخصصات مصرفية ومسؤولية وظيفية خاصة بالقطاع المصرفي"
            };
            _dbContext.AllowanceTypes.Add(bankingType);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 2. Fetch Employees
        var empIds = request.Rows.Select(r => r.EmployeeID.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var employees = await _dbContext.Employees
            .Where(e => empIds.Contains(e.EmployeeID))
            .ToListAsync(cancellationToken);
        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);

        // 3. Fetch Active Periods with items
        var empDbIds = employees.Select(e => e.Id).ToList();
        var activePeriods = await _dbContext.EmployeeAllowancePeriods
            .Include(p => p.Items)
            .Where(p => empDbIds.Contains(p.EmployeeId) && p.IsCurrent)
            .ToListAsync(cancellationToken);
        var activePeriodsByEmpId = activePeriods.ToDictionary(p => p.EmployeeId);

        var salaryScales = await _dbContext.SalaryScales.AsNoTracking().ToListAsync(cancellationToken);

        foreach (var row in request.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.EmployeeID))
            {
                skippedCount++;
                continue;
            }

            if (!empDict.TryGetValue(row.EmployeeID.Trim(), out var emp))
            {
                skippedCount++;
                errors.Add(new EmployeeImportError(row.RowNumber, "EmployeeID", $"الرقم الوظيفي '{row.EmployeeID}' غير مسجل في قاعدة بيانات الموظفين."));
                continue;
            }

            var pct = row.ExtractedPercentage ?? ExtractPercentageFromResponsibility(row.ResponsibilityRaw);
            if (!pct.HasValue || pct.Value <= 0)
            {
                skippedCount++;
                errors.Add(new EmployeeImportError(row.RowNumber, "Responsibility", $"لم يتم العثور على نسبة مئوية صالحة في حقل المسؤولية '{row.ResponsibilityRaw}' للموظف '{emp.Name}'."));
                continue;
            }

            var percentageVal = pct.Value;

            if (activePeriodsByEmpId.TryGetValue(emp.Id, out var period))
            {
                // Update item in existing active period
                var existingItem = period.Items.FirstOrDefault(i => 
                    (i.AllowanceTypeId.HasValue && i.AllowanceTypeId.Value == bankingType.Id) ||
                    i.Name == "المخصصات المصرفية" || 
                    i.Name.Contains("مصرفي") || 
                    i.Name.Contains("مسؤول"));

                if (existingItem != null)
                {
                    existingItem.Name = "المخصصات المصرفية";
                    existingItem.AllowanceTypeId = bankingType.Id;
                    existingItem.CalculationType = AllowanceCalculationType.Percentage;
                    existingItem.Percentage = percentageVal;
                    existingItem.CalculatedAmount = Math.Round((period.BasicSalary * percentageVal) / 100m, 2);
                    existingItem.Notes = string.IsNullOrWhiteSpace(row.ResponsibilityRaw) ? "تحديث المخصصات المصرفية" : $"مسؤولية: {row.ResponsibilityRaw}";
                }
                else
                {
                    period.Items.Add(new EmployeeAllowanceItem
                    {
                        PeriodId = period.Id,
                        AllowanceTypeId = bankingType.Id,
                        Name = "المخصصات المصرفية",
                        CalculationType = AllowanceCalculationType.Percentage,
                        Percentage = percentageVal,
                        CalculatedAmount = Math.Round((period.BasicSalary * percentageVal) / 100m, 2),
                        Notes = string.IsNullOrWhiteSpace(row.ResponsibilityRaw) ? "إضافة المخصصات المصرفية" : $"مسؤولية: {row.ResponsibilityRaw}"
                    });
                }

                if (request.EffectiveStartDate.HasValue)
                {
                    period.StartDate = request.EffectiveStartDate.Value;
                }

                if (!string.IsNullOrWhiteSpace(request.OrderNumber))
                {
                    period.OrderNumber = request.OrderNumber.Trim();
                }

                if (!string.IsNullOrWhiteSpace(request.ChangeReason))
                {
                    period.ChangeReason = request.ChangeReason.Trim();
                }

                // Recalculate totals
                decimal totalPctRate = 0m;
                decimal totalPctAmt = 0m;
                decimal totalFixedAmt = 0m;

                foreach (var item in period.Items)
                {
                    if (item.CalculationType == AllowanceCalculationType.Percentage)
                    {
                        var rate = item.Percentage ?? 0m;
                        totalPctRate += rate;
                        item.CalculatedAmount = Math.Round((period.BasicSalary * rate) / 100m, 2);
                        totalPctAmt += item.CalculatedAmount;
                    }
                    else
                    {
                        item.CalculatedAmount = item.FixedAmount ?? 0m;
                        totalFixedAmt += item.CalculatedAmount;
                    }
                }

                period.TotalPercentageRate = totalPctRate;
                period.TotalPercentageAmount = totalPctAmt;
                period.TotalFixedAmount = totalFixedAmt;
                period.GrossSalary = period.BasicSalary + totalPctAmt + totalFixedAmt;

                updatedCount++;
            }
            else
            {
                // Create new active period
                decimal basicSalary = 0m;
                if (emp.GradeId.HasValue && emp.StageId.HasValue)
                {
                    var scale = salaryScales.FirstOrDefault(s => s.GradeId == emp.GradeId.Value && s.StageId == emp.StageId.Value);
                    if (scale != null)
                    {
                        basicSalary = scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
                    }
                }

                var startDate = request.EffectiveStartDate ?? (emp.HireDate ?? DateOnly.FromDateTime(DateTime.Today));
                var calcAmount = Math.Round((basicSalary * percentageVal) / 100m, 2);

                var newPeriod = new EmployeeAllowancePeriod
                {
                    EmployeeId = emp.Id,
                    StartDate = startDate,
                    IsCurrent = true,
                    BasicSalary = basicSalary,
                    TotalPercentageRate = percentageVal,
                    TotalPercentageAmount = calcAmount,
                    TotalFixedAmount = 0m,
                    GrossSalary = basicSalary + calcAmount,
                    OrderNumber = request.OrderNumber?.Trim(),
                    ChangeReason = string.IsNullOrWhiteSpace(request.ChangeReason) ? "استيراد المخصصات المصرفية (المسؤولية)" : request.ChangeReason.Trim(),
                    Notes = string.IsNullOrWhiteSpace(row.ResponsibilityRaw) ? "استيراد ملف إكسل للمخصصات المصرفية" : $"المسؤولية: {row.ResponsibilityRaw}",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Items = new List<EmployeeAllowanceItem>
                    {
                        new EmployeeAllowanceItem
                        {
                            AllowanceTypeId = bankingType.Id,
                            Name = "المخصصات المصرفية",
                            CalculationType = AllowanceCalculationType.Percentage,
                            Percentage = percentageVal,
                            CalculatedAmount = calcAmount,
                            Notes = string.IsNullOrWhiteSpace(row.ResponsibilityRaw) ? null : $"مسؤولية: {row.ResponsibilityRaw}"
                        }
                    }
                };

                _dbContext.EmployeeAllowancePeriods.Add(newPeriod);
                activePeriodsByEmpId[emp.Id] = newPeriod;
                importedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var msg = $"تم إكمال استيراد المخصصات المصرفية بنجاح: تم إنشاء فترات جديدة لـ ({importedCount}) موظف، وتحديث المخصصات المصرفية في الفترات الفعالة لـ ({updatedCount}) موظف، وتخطي ({skippedCount}) صفوف.";
        return new BankingAllowanceImportResponse(totalRows, importedCount, updatedCount, skippedCount, errors, msg);
    }

    // ==========================================
    // 9. Engineering Allowances (المخصصات الهندسية)
    // ==========================================

    public async Task<ExcelParseResult<BankingAllowanceImportRow>> ParseEngineeringAllowancesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0);
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchEngineeringAllowanceField, "EmployeeID");
        if (headerRow is null)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة.") },
                0);
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (!colMap.ContainsKey("AllowanceRate"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'المخصصات' أو 'المخصصات الهندسية'."));
        }

        if (errors.Count > 0)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0);
        }

        var rawRows = new List<(int RowNum, string EmpId, string? Name, string? Formation, string? JobTitle, string? Stage, string? Grade, string? Cert, string? Spec, string? HireDate, string? RespRaw, decimal? ExtractedPct)>();

        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID")?.Trim();
            var allowRaw = GetVal("AllowanceRate")?.Trim();
            var name = GetVal("EmployeeName")?.Trim();
            var formation = GetVal("Formation")?.Trim();
            var jobTitle = GetVal("JobTitle")?.Trim() ?? GetVal("PositionTitle")?.Trim();
            var stage = GetVal("Stage")?.Trim();
            var grade = GetVal("Grade")?.Trim();
            var cert = GetVal("Certificate")?.Trim();
            var spec = GetVal("Specialization")?.Trim();
            var hireDate = GetVal("HireDate")?.Trim();

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(allowRaw) && string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: رقم الموظف مطلوب للربط."));
            }

            var extractedPct = ExtractPercentageFromResponsibility(allowRaw);

            rawRows.Add((rowNum, empId ?? string.Empty, name, formation, jobTitle, stage, grade, cert, spec, hireDate, allowRaw, extractedPct));
        }

        // Match against database
        var distinctEmpIds = rawRows.Select(r => r.EmpId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => distinctEmpIds.Contains(e.EmployeeID))
            .Select(e => new { e.Id, e.EmployeeID, e.Name, e.GradeId, e.StageId, e.HireDate })
            .ToListAsync(cancellationToken);

        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);
        var empDbIds = employees.Select(e => e.Id).ToList();

        var activePeriods = await _dbContext.EmployeeAllowancePeriods
            .AsNoTracking()
            .Where(p => empDbIds.Contains(p.EmployeeId) && p.IsCurrent)
            .ToDictionaryAsync(p => p.EmployeeId, cancellationToken);

        var salaryScales = await _dbContext.SalaryScales.AsNoTracking().ToListAsync(cancellationToken);

        var rows = new List<BankingAllowanceImportRow>();

        foreach (var r in rawRows)
        {
            var isMatched = empDict.TryGetValue(r.EmpId, out var emp);
            string? matchedName = null;
            decimal? basicSalary = null;
            decimal? calcAmount = null;
            string statusMsg;

            if (isMatched && emp != null)
            {
                matchedName = emp.Name;
                if (activePeriods.TryGetValue(emp.Id, out var activePeriod))
                {
                    basicSalary = activePeriod.BasicSalary;
                }
                else if (emp.GradeId.HasValue && emp.StageId.HasValue)
                {
                    var scale = salaryScales.FirstOrDefault(s => s.GradeId == emp.GradeId.Value && s.StageId == emp.StageId.Value);
                    if (scale != null)
                    {
                        basicSalary = scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
                    }
                }

                if (r.ExtractedPct.HasValue)
                {
                    if (basicSalary.HasValue && basicSalary.Value > 0)
                    {
                        calcAmount = Math.Round((basicSalary.Value * r.ExtractedPct.Value) / 100m, 2);
                        statusMsg = $"جاهز للإضافة بنسبة {r.ExtractedPct.Value}% بمبلغ احتساب شهري {calcAmount:N0} د.ع";
                    }
                    else
                    {
                        statusMsg = $"جاهز للإضافة بنسبة {r.ExtractedPct.Value}% (لم يُحدد الراتب الإسمي بعد)";
                    }
                }
                else
                {
                    statusMsg = "لم يتم العثور على نسبة في حقل المخصصات الهندسية";
                }
            }
            else
            {
                statusMsg = "الرقم الوظيفي غير موجود في جدول الموظفين بالنظام";
            }

            rows.Add(new BankingAllowanceImportRow(
                RowNumber: r.RowNum,
                EmployeeID: r.EmpId,
                EmployeeName: r.Name,
                Formation: r.Formation,
                JobTitle: r.JobTitle,
                Stage: r.Stage,
                Grade: r.Grade,
                Certificate: r.Cert,
                Specialization: r.Spec,
                HireDateStr: r.HireDate,
                ResponsibilityRaw: r.RespRaw,
                ExtractedPercentage: r.ExtractedPct,
                IsMatched: isMatched,
                MatchedName: matchedName,
                BasicSalary: basicSalary,
                CalculatedAmount: calcAmount,
                StatusMessage: statusMsg
            ));
        }

        return new ExcelParseResult<BankingAllowanceImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows);
    }

    public Task<BankingAllowanceImportResponse> ImportEngineeringAllowancesAsync(BankingAllowanceImportRequest request, CancellationToken cancellationToken = default)
    {
        return ImportAllowanceItemsBatchAsync(request, "ENGINEERING", "المخصصات الهندسية", "المخصصات الهندسية للملاكات الهندسية والفنية", cancellationToken);
    }

    // ==========================================
    // 10. Legal Allowances (المخصصات القانونية - 35%)
    // ==========================================

    public async Task<ExcelParseResult<BankingAllowanceImportRow>> ParseLegalAllowancesExcelAsync(Stream fileStream, decimal defaultPercentage = 35m, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0);
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchLegalAllowanceField, "EmployeeID");
        if (headerRow is null)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة.") },
                0);
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (errors.Count > 0)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0);
        }

        var rawRows = new List<(int RowNum, string EmpId, string? Name, string? Formation, string? JobTitle, string? Stage, string? Grade, string? AllowRaw, decimal ExtractedPct)>();

        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID")?.Trim();
            var allowRaw = colMap.ContainsKey("AllowanceRate") ? GetVal("AllowanceRate")?.Trim() : null;
            var name = GetVal("EmployeeName")?.Trim();
            var formation = GetVal("Formation")?.Trim();
            var jobTitle = GetVal("JobTitle")?.Trim();
            var stage = GetVal("Stage")?.Trim();
            var grade = GetVal("Grade")?.Trim();

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: رقم الموظف مطلوب للربط."));
            }

            // Legal allowance defaults to 35% as requested by user
            var extractedPct = ExtractPercentageFromResponsibility(allowRaw) ?? defaultPercentage;

            rawRows.Add((rowNum, empId ?? string.Empty, name, formation, jobTitle, stage, grade, allowRaw ?? $"{extractedPct}%", extractedPct));
        }

        // Match against database
        var distinctEmpIds = rawRows.Select(r => r.EmpId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => distinctEmpIds.Contains(e.EmployeeID))
            .Select(e => new { e.Id, e.EmployeeID, e.Name, e.GradeId, e.StageId, e.HireDate })
            .ToListAsync(cancellationToken);

        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);
        var empDbIds = employees.Select(e => e.Id).ToList();

        var activePeriods = await _dbContext.EmployeeAllowancePeriods
            .AsNoTracking()
            .Where(p => empDbIds.Contains(p.EmployeeId) && p.IsCurrent)
            .ToDictionaryAsync(p => p.EmployeeId, cancellationToken);

        var salaryScales = await _dbContext.SalaryScales.AsNoTracking().ToListAsync(cancellationToken);

        var rows = new List<BankingAllowanceImportRow>();

        foreach (var r in rawRows)
        {
            var isMatched = empDict.TryGetValue(r.EmpId, out var emp);
            string? matchedName = null;
            decimal? basicSalary = null;
            decimal? calcAmount = null;
            string statusMsg;

            if (isMatched && emp != null)
            {
                matchedName = emp.Name;
                if (activePeriods.TryGetValue(emp.Id, out var activePeriod))
                {
                    basicSalary = activePeriod.BasicSalary;
                }
                else if (emp.GradeId.HasValue && emp.StageId.HasValue)
                {
                    var scale = salaryScales.FirstOrDefault(s => s.GradeId == emp.GradeId.Value && s.StageId == emp.StageId.Value);
                    if (scale != null)
                    {
                        basicSalary = scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
                    }
                }

                if (basicSalary.HasValue && basicSalary.Value > 0)
                {
                    calcAmount = Math.Round((basicSalary.Value * r.ExtractedPct) / 100m, 2);
                    statusMsg = $"جاهز لمنح مخصصات قانونية بنسبة {r.ExtractedPct}% بمبلغ احتساب شهري {calcAmount:N0} د.ع";
                }
                else
                {
                    statusMsg = $"جاهز لمنح مخصصات قانونية بنسبة {r.ExtractedPct}% (لم يُحدد الراتب الإسمي بعد)";
                }
            }
            else
            {
                statusMsg = "الرقم الوظيفي غير موجود في جدول الموظفين بالنظام";
            }

            rows.Add(new BankingAllowanceImportRow(
                RowNumber: r.RowNum,
                EmployeeID: r.EmpId,
                EmployeeName: r.Name,
                Formation: r.Formation,
                JobTitle: r.JobTitle,
                Stage: r.Stage,
                Grade: r.Grade,
                Certificate: null,
                Specialization: null,
                HireDateStr: null,
                ResponsibilityRaw: $"مخصصات قانونية {r.ExtractedPct}%",
                ExtractedPercentage: r.ExtractedPct,
                IsMatched: isMatched,
                MatchedName: matchedName,
                BasicSalary: basicSalary,
                CalculatedAmount: calcAmount,
                StatusMessage: statusMsg
            ));
        }

        return new ExcelParseResult<BankingAllowanceImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows);
    }

    public Task<BankingAllowanceImportResponse> ImportLegalAllowancesAsync(BankingAllowanceImportRequest request, CancellationToken cancellationToken = default)
    {
        return ImportAllowanceItemsBatchAsync(request, "LEGAL", "المخصصات القانونية", "المخصصات القانونية المعتمدة بنسبة 35% للملاكات والحقوقيين", cancellationToken);
    }

    // ==========================================
    // 11. Certificate Allowances (مخصصات الشهادة بحسب السلم)
    // ==========================================

    public async Task<ExcelParseResult<BankingAllowanceImportRow>> ParseCertificateAllowancesExcelAsync(
        Stream fileStream,
        Dictionary<string, decimal>? customRates = null,
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0);
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchCertificateAllowanceField, "EmployeeID");
        if (headerRow is null)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة.") },
                0);
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (errors.Count > 0)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0);
        }

        var rawRows = new List<(int RowNum, string EmpId, string? Name, string? Formation, string? Cert, string? Spec, string? AllowRaw, decimal? ExtractedPct)>();

        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID")?.Trim();
            var cert = colMap.ContainsKey("Certificate") ? GetVal("Certificate")?.Trim() : null;
            var allowRaw = colMap.ContainsKey("AllowanceRate") ? GetVal("AllowanceRate")?.Trim() : null;
            var name = GetVal("EmployeeName")?.Trim();
            var formation = GetVal("Formation")?.Trim();
            var spec = GetVal("Specialization")?.Trim();

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: رقم الموظف مطلوب للربط."));
            }

            decimal? extractedPct = ExtractPercentageFromResponsibility(allowRaw);

            rawRows.Add((rowNum, empId ?? string.Empty, name, formation, cert, spec, allowRaw, extractedPct));
        }

        // Match against database
        var distinctEmpIds = rawRows.Select(r => r.EmpId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Include(e => e.EducationLevel)
            .Where(e => distinctEmpIds.Contains(e.EmployeeID))
            .Select(e => new { e.Id, e.EmployeeID, e.Name, e.GradeId, e.StageId, e.HireDate, EducationName = e.EducationLevel != null ? e.EducationLevel.Name : null })
            .ToListAsync(cancellationToken);

        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);
        var empDbIds = employees.Select(e => e.Id).ToList();

        var activePeriods = await _dbContext.EmployeeAllowancePeriods
            .AsNoTracking()
            .Where(p => empDbIds.Contains(p.EmployeeId) && p.IsCurrent)
            .ToDictionaryAsync(p => p.EmployeeId, cancellationToken);

        var salaryScales = await _dbContext.SalaryScales.AsNoTracking().ToListAsync(cancellationToken);

        var rows = new List<BankingAllowanceImportRow>();

        foreach (var r in rawRows)
        {
            var isMatched = empDict.TryGetValue(r.EmpId, out var emp);
            string? matchedName = null;
            decimal? basicSalary = null;
            decimal? calcAmount = null;
            string statusMsg;

            // Determine certificate: from file or from DB
            var effectiveCert = !string.IsNullOrWhiteSpace(r.Cert) ? r.Cert : (emp?.EducationName ?? null);
            var pct = r.ExtractedPct ?? (effectiveCert != null ? ResolveCertificatePercentage(effectiveCert, customRates) : 0m);

            if (isMatched && emp != null)
            {
                matchedName = emp.Name;
                if (activePeriods.TryGetValue(emp.Id, out var activePeriod))
                {
                    basicSalary = activePeriod.BasicSalary;
                }
                else if (emp.GradeId.HasValue && emp.StageId.HasValue)
                {
                    var scale = salaryScales.FirstOrDefault(s => s.GradeId == emp.GradeId.Value && s.StageId == emp.StageId.Value);
                    if (scale != null)
                    {
                        basicSalary = scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
                    }
                }

                if (pct > 0)
                {
                    if (basicSalary.HasValue && basicSalary.Value > 0)
                    {
                        calcAmount = Math.Round((basicSalary.Value * pct) / 100m, 2);
                        statusMsg = $"الشهادة: ({effectiveCert}) -> نسبة {pct}% بمبلغ شهري {calcAmount:N0} د.ع";
                    }
                    else
                    {
                        statusMsg = $"الشهادة: ({effectiveCert}) -> نسبة {pct}% (الراتب الإسمي غير محدد)";
                    }
                }
                else
                {
                    statusMsg = $"الشهادة غير محددة أو لم يُعثر على نسبة معتمدة في السلم: ({effectiveCert ?? "غير مسجل"})";
                }
            }
            else
            {
                statusMsg = "الرقم الوظيفي غير موجود في جدول الموظفين بالنظام";
            }

            rows.Add(new BankingAllowanceImportRow(
                RowNumber: r.RowNum,
                EmployeeID: r.EmpId,
                EmployeeName: r.Name,
                Formation: r.Formation,
                JobTitle: null,
                Stage: null,
                Grade: null,
                Certificate: effectiveCert,
                Specialization: r.Spec,
                HireDateStr: null,
                ResponsibilityRaw: effectiveCert ?? r.AllowRaw,
                ExtractedPercentage: pct > 0 ? pct : null,
                IsMatched: isMatched,
                MatchedName: matchedName,
                BasicSalary: basicSalary,
                CalculatedAmount: calcAmount,
                StatusMessage: statusMsg
            ));
        }

        return new ExcelParseResult<BankingAllowanceImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows);
    }

    public Task<BankingAllowanceImportResponse> ImportCertificateAllowancesAsync(BankingAllowanceImportRequest request, CancellationToken cancellationToken = default)
    {
        return ImportAllowanceItemsBatchAsync(request, "CERT", "مخصصات الشهادة", "مخصصات الشهادة الأكاديمية والمؤهل العلمي", cancellationToken);
    }

    public async Task<BankingAllowanceImportResponse> ApplyCertificateAllowancesToAllEmployeesAsync(
        DateOnly? effectiveStartDate = null,
        string? orderNumber = null,
        string? changeReason = null,
        Dictionary<string, decimal>? customRates = null,
        CancellationToken cancellationToken = default)
    {
        var certType = await _dbContext.AllowanceTypes.FirstOrDefaultAsync(t => t.Code == "CERT", cancellationToken);
        if (certType == null)
        {
            certType = new AllowanceType
            {
                Code = "CERT",
                Name = "مخصصات الشهادة",
                CalculationType = AllowanceCalculationType.Percentage,
                DefaultPercentage = 45m,
                Description = "مخصصات الشهادة الأكاديمية بحسب سلم الشهادات المعتمد",
                SortOrder = 3
            };
            _dbContext.AllowanceTypes.Add(certType);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var employees = await _dbContext.Employees
            .Include(e => e.EducationLevel)
            .ToListAsync(cancellationToken);

        var activePeriods = await _dbContext.EmployeeAllowancePeriods
            .Include(p => p.Items)
            .Where(p => p.IsCurrent)
            .ToListAsync(cancellationToken);
        var activePeriodsByEmpId = activePeriods.ToDictionary(p => p.EmployeeId);

        var salaryScales = await _dbContext.SalaryScales.AsNoTracking().ToListAsync(cancellationToken);

        var importedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;
        var errors = new List<EmployeeImportError>();

        foreach (var emp in employees)
        {
            var certName = emp.EducationLevel?.Name;
            var pct = ResolveCertificatePercentage(certName, customRates);

            if (pct <= 0)
            {
                skippedCount++;
                continue;
            }

            if (activePeriodsByEmpId.TryGetValue(emp.Id, out var period))
            {
                var existingItem = period.Items.FirstOrDefault(i =>
                    (i.AllowanceTypeId.HasValue && i.AllowanceTypeId.Value == certType.Id) ||
                    i.Name == "مخصصات الشهادة" ||
                    i.Name.Contains("شهادة"));

                if (existingItem != null)
                {
                    existingItem.Name = "مخصصات الشهادة";
                    existingItem.AllowanceTypeId = certType.Id;
                    existingItem.CalculationType = AllowanceCalculationType.Percentage;
                    existingItem.Percentage = pct;
                    existingItem.CalculatedAmount = Math.Round((period.BasicSalary * pct) / 100m, 2);
                    existingItem.Notes = $"شهادة: {certName} بنسبة {pct}%";
                }
                else
                {
                    period.Items.Add(new EmployeeAllowanceItem
                    {
                        PeriodId = period.Id,
                        AllowanceTypeId = certType.Id,
                        Name = "مخصصات الشهادة",
                        CalculationType = AllowanceCalculationType.Percentage,
                        Percentage = pct,
                        CalculatedAmount = Math.Round((period.BasicSalary * pct) / 100m, 2),
                        Notes = $"شهادة: {certName} بنسبة {pct}%"
                    });
                }

                if (effectiveStartDate.HasValue) period.StartDate = effectiveStartDate.Value;
                if (!string.IsNullOrWhiteSpace(orderNumber)) period.OrderNumber = orderNumber.Trim();
                if (!string.IsNullOrWhiteSpace(changeReason)) period.ChangeReason = changeReason.Trim();

                // Recalculate
                decimal totalPctRate = 0m;
                decimal totalPctAmt = 0m;
                decimal totalFixedAmt = 0m;
                foreach (var item in period.Items)
                {
                    if (item.CalculationType == AllowanceCalculationType.Percentage)
                    {
                        var rate = item.Percentage ?? 0m;
                        totalPctRate += rate;
                        item.CalculatedAmount = Math.Round((period.BasicSalary * rate) / 100m, 2);
                        totalPctAmt += item.CalculatedAmount;
                    }
                    else
                    {
                        item.CalculatedAmount = item.FixedAmount ?? 0m;
                        totalFixedAmt += item.CalculatedAmount;
                    }
                }
                period.TotalPercentageRate = totalPctRate;
                period.TotalPercentageAmount = totalPctAmt;
                period.TotalFixedAmount = totalFixedAmt;
                period.GrossSalary = period.BasicSalary + totalPctAmt + totalFixedAmt;

                updatedCount++;
            }
            else
            {
                decimal basicSalary = 0m;
                if (emp.GradeId.HasValue && emp.StageId.HasValue)
                {
                    var scale = salaryScales.FirstOrDefault(s => s.GradeId == emp.GradeId.Value && s.StageId == emp.StageId.Value);
                    if (scale != null)
                    {
                        basicSalary = scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
                    }
                }

                var startDate = effectiveStartDate ?? (emp.HireDate ?? DateOnly.FromDateTime(DateTime.Today));
                var calcAmount = Math.Round((basicSalary * pct) / 100m, 2);

                var newPeriod = new EmployeeAllowancePeriod
                {
                    EmployeeId = emp.Id,
                    StartDate = startDate,
                    IsCurrent = true,
                    BasicSalary = basicSalary,
                    TotalPercentageRate = pct,
                    TotalPercentageAmount = calcAmount,
                    TotalFixedAmount = 0m,
                    GrossSalary = basicSalary + calcAmount,
                    OrderNumber = orderNumber?.Trim(),
                    ChangeReason = string.IsNullOrWhiteSpace(changeReason) ? "تطبيق مخصصات الشهادة حسب السلم المعتمد" : changeReason.Trim(),
                    Notes = $"احتساب آلي للمؤهل العلمي: {certName} ({pct}%)",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Items = new List<EmployeeAllowanceItem>
                    {
                        new EmployeeAllowanceItem
                        {
                            AllowanceTypeId = certType.Id,
                            Name = "مخصصات الشهادة",
                            CalculationType = AllowanceCalculationType.Percentage,
                            Percentage = pct,
                            CalculatedAmount = calcAmount,
                            Notes = $"المؤهل: {certName}"
                        }
                    }
                };

                _dbContext.EmployeeAllowancePeriods.Add(newPeriod);
                activePeriodsByEmpId[emp.Id] = newPeriod;
                importedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var msg = $"تم تطبيق مخصصات الشهادة لجميع الموظفين بنجاح: تم إنشاء فترات جديدة لـ ({importedCount}) موظف، وتحديث مخصصات الشهادة لـ ({updatedCount}) موظف، وتخطي ({skippedCount}) موظف بدون شهادة مسجلة.";
        return new BankingAllowanceImportResponse(employees.Count, importedCount, updatedCount, skippedCount, errors, msg);
    }

    // ==========================================
    // 12. Non-Practice Allowances (مخصصات عدم مزاولة مهنة)
    // ==========================================

    public async Task<ExcelParseResult<BankingAllowanceImportRow>> ParseNonPracticeAllowancesExcelAsync(
        Stream fileStream,
        decimal defaultPercentage = 20m,
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0);
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchNonPracticeAllowanceField, "EmployeeID");
        if (headerRow is null)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة أو لم يتم العثور على صف العناوين.") },
                0);
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف' لربطه مع جدول الموظفين."));
        }

        if (errors.Count > 0)
        {
            return new ExcelParseResult<BankingAllowanceImportRow>(
                Array.Empty<BankingAllowanceImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0);
        }

        var rawRows = new List<(int RowNum, string EmpId, string? Name, string? Formation, string? JobTitle, string? Stage, string? Grade, string? AllowRaw, decimal ExtractedPct)>();

        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID")?.Trim();
            var allowRaw = colMap.ContainsKey("AllowanceRate") ? GetVal("AllowanceRate")?.Trim() : null;
            var name = GetVal("EmployeeName")?.Trim();
            var formation = GetVal("Formation")?.Trim();
            var jobTitle = GetVal("JobTitle")?.Trim();
            var stage = GetVal("Stage")?.Trim();
            var grade = GetVal("Grade")?.Trim();

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: رقم الموظف مطلوب للربط."));
            }

            var extractedPct = ExtractPercentageFromResponsibility(allowRaw) ?? defaultPercentage;

            rawRows.Add((rowNum, empId ?? string.Empty, name, formation, jobTitle, stage, grade, allowRaw ?? $"{extractedPct}%", extractedPct));
        }

        // Match against database
        var distinctEmpIds = rawRows.Select(r => r.EmpId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => distinctEmpIds.Contains(e.EmployeeID))
            .Select(e => new { e.Id, e.EmployeeID, e.Name, e.GradeId, e.StageId, e.HireDate })
            .ToListAsync(cancellationToken);

        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);
        var empDbIds = employees.Select(e => e.Id).ToList();

        var activePeriods = await _dbContext.EmployeeAllowancePeriods
            .AsNoTracking()
            .Where(p => empDbIds.Contains(p.EmployeeId) && p.IsCurrent)
            .ToDictionaryAsync(p => p.EmployeeId, cancellationToken);

        var salaryScales = await _dbContext.SalaryScales.AsNoTracking().ToListAsync(cancellationToken);

        var rows = new List<BankingAllowanceImportRow>();

        foreach (var r in rawRows)
        {
            var isMatched = empDict.TryGetValue(r.EmpId, out var emp);
            string? matchedName = null;
            decimal? basicSalary = null;
            decimal? calcAmount = null;
            string statusMsg;

            if (isMatched && emp != null)
            {
                matchedName = emp.Name;
                if (activePeriods.TryGetValue(emp.Id, out var activePeriod))
                {
                    basicSalary = activePeriod.BasicSalary;
                }
                else if (emp.GradeId.HasValue && emp.StageId.HasValue)
                {
                    var scale = salaryScales.FirstOrDefault(s => s.GradeId == emp.GradeId.Value && s.StageId == emp.StageId.Value);
                    if (scale != null)
                    {
                        basicSalary = scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
                    }
                }

                if (basicSalary.HasValue && basicSalary.Value > 0)
                {
                    calcAmount = Math.Round((basicSalary.Value * r.ExtractedPct) / 100m, 2);
                    statusMsg = $"جاهز لمنح مخصصات عدم مزاولة مهنة بنسبة {r.ExtractedPct}% بمبلغ احتساب شهري {calcAmount:N0} د.ع";
                }
                else
                {
                    statusMsg = $"جاهز لمنح مخصصات عدم مزاولة مهنة بنسبة {r.ExtractedPct}% (لم يُحدد الراتب الإسمي بعد)";
                }
            }
            else
            {
                statusMsg = "الرقم الوظيفي غير موجود في جدول الموظفين بالنظام";
            }

            rows.Add(new BankingAllowanceImportRow(
                RowNumber: r.RowNum,
                EmployeeID: r.EmpId,
                EmployeeName: r.Name,
                Formation: r.Formation,
                JobTitle: r.JobTitle,
                Stage: r.Stage,
                Grade: r.Grade,
                Certificate: null,
                Specialization: null,
                HireDateStr: null,
                ResponsibilityRaw: $"مخصصات عدم مزاولة مهنة {r.ExtractedPct}%",
                ExtractedPercentage: r.ExtractedPct,
                IsMatched: isMatched,
                MatchedName: matchedName,
                BasicSalary: basicSalary,
                CalculatedAmount: calcAmount,
                StatusMessage: statusMsg
            ));
        }

        return new ExcelParseResult<BankingAllowanceImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows);
    }

    public Task<BankingAllowanceImportResponse> ImportNonPracticeAllowancesAsync(BankingAllowanceImportRequest request, CancellationToken cancellationToken = default)
    {
        return ImportAllowanceItemsBatchAsync(request, "NON_PRACTICE", "مخصصات عدم مزاولة مهنة", "مخصصات عدم مزاولة المهنة المعتمدة للملاكات التخصصية كنسبة مئوية من الراتب الاسمي", cancellationToken);
    }

    private async Task<BankingAllowanceImportResponse> ImportAllowanceItemsBatchAsync(
        BankingAllowanceImportRequest request,
        string allowanceCode,
        string allowanceName,
        string defaultNotes,
        CancellationToken cancellationToken)
    {
        var totalRows = request.Rows.Count;
        var importedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;
        var errors = new List<EmployeeImportError>();

        if (totalRows == 0)
        {
            return new BankingAllowanceImportResponse(0, 0, 0, 0, errors, "لم يتم إرسال أي صفوف للاستيراد.");
        }

        // 1. Ensure Allowance Type exists
        var allowanceType = await _dbContext.AllowanceTypes.FirstOrDefaultAsync(t => t.Code == allowanceCode, cancellationToken);
        if (allowanceType == null)
        {
            allowanceType = new AllowanceType
            {
                Name = allowanceName,
                Code = allowanceCode,
                CalculationType = AllowanceCalculationType.Percentage,
                IsActive = true,
                SortOrder = 2,
                Description = defaultNotes
            };
            _dbContext.AllowanceTypes.Add(allowanceType);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 2. Fetch Employees
        var empIds = request.Rows.Select(r => r.EmployeeID.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var employees = await _dbContext.Employees
            .Where(e => empIds.Contains(e.EmployeeID))
            .ToListAsync(cancellationToken);
        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);

        // 3. Fetch Active Periods with items
        var empDbIds = employees.Select(e => e.Id).ToList();
        var activePeriods = await _dbContext.EmployeeAllowancePeriods
            .Include(p => p.Items)
            .Where(p => empDbIds.Contains(p.EmployeeId) && p.IsCurrent)
            .ToListAsync(cancellationToken);
        var activePeriodsByEmpId = activePeriods.ToDictionary(p => p.EmployeeId);

        var salaryScales = await _dbContext.SalaryScales.AsNoTracking().ToListAsync(cancellationToken);

        foreach (var row in request.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.EmployeeID))
            {
                skippedCount++;
                continue;
            }

            if (!empDict.TryGetValue(row.EmployeeID.Trim(), out var emp))
            {
                skippedCount++;
                errors.Add(new EmployeeImportError(row.RowNumber, "EmployeeID", $"الرقم الوظيفي '{row.EmployeeID}' غير مسجل في قاعدة بيانات الموظفين."));
                continue;
            }

            var pct = row.ExtractedPercentage ?? ExtractPercentageFromResponsibility(row.ResponsibilityRaw);
            if (!pct.HasValue || pct.Value <= 0)
            {
                skippedCount++;
                errors.Add(new EmployeeImportError(row.RowNumber, "Allowance", $"لم يتم العثور على نسبة مئوية صالحة للموظف '{emp.Name}'."));
                continue;
            }

            var percentageVal = pct.Value;

            if (activePeriodsByEmpId.TryGetValue(emp.Id, out var period))
            {
                var existingItem = period.Items.FirstOrDefault(i => 
                    (i.AllowanceTypeId.HasValue && i.AllowanceTypeId.Value == allowanceType.Id) ||
                    i.Name == allowanceName || 
                    i.Name.Contains(allowanceName));

                if (existingItem != null)
                {
                    existingItem.Name = allowanceName;
                    existingItem.AllowanceTypeId = allowanceType.Id;
                    existingItem.CalculationType = AllowanceCalculationType.Percentage;
                    existingItem.Percentage = percentageVal;
                    existingItem.CalculatedAmount = Math.Round((period.BasicSalary * percentageVal) / 100m, 2);
                    existingItem.Notes = string.IsNullOrWhiteSpace(row.ResponsibilityRaw) ? $"تحديث {allowanceName}" : $"بيان: {row.ResponsibilityRaw}";
                }
                else
                {
                    period.Items.Add(new EmployeeAllowanceItem
                    {
                        PeriodId = period.Id,
                        AllowanceTypeId = allowanceType.Id,
                        Name = allowanceName,
                        CalculationType = AllowanceCalculationType.Percentage,
                        Percentage = percentageVal,
                        CalculatedAmount = Math.Round((period.BasicSalary * percentageVal) / 100m, 2),
                        Notes = string.IsNullOrWhiteSpace(row.ResponsibilityRaw) ? $"إضافة {allowanceName}" : $"بيان: {row.ResponsibilityRaw}"
                    });
                }

                if (request.EffectiveStartDate.HasValue)
                {
                    period.StartDate = request.EffectiveStartDate.Value;
                }

                if (!string.IsNullOrWhiteSpace(request.OrderNumber))
                {
                    period.OrderNumber = request.OrderNumber.Trim();
                }

                if (!string.IsNullOrWhiteSpace(request.ChangeReason))
                {
                    period.ChangeReason = request.ChangeReason.Trim();
                }

                // Recalculate totals
                decimal totalPctRate = 0m;
                decimal totalPctAmt = 0m;
                decimal totalFixedAmt = 0m;

                foreach (var item in period.Items)
                {
                    if (item.CalculationType == AllowanceCalculationType.Percentage)
                    {
                        var rate = item.Percentage ?? 0m;
                        totalPctRate += rate;
                        item.CalculatedAmount = Math.Round((period.BasicSalary * rate) / 100m, 2);
                        totalPctAmt += item.CalculatedAmount;
                    }
                    else
                    {
                        item.CalculatedAmount = item.FixedAmount ?? 0m;
                        totalFixedAmt += item.CalculatedAmount;
                    }
                }

                period.TotalPercentageRate = totalPctRate;
                period.TotalPercentageAmount = totalPctAmt;
                period.TotalFixedAmount = totalFixedAmt;
                period.GrossSalary = period.BasicSalary + totalPctAmt + totalFixedAmt;

                updatedCount++;
            }
            else
            {
                decimal basicSalary = 0m;
                if (emp.GradeId.HasValue && emp.StageId.HasValue)
                {
                    var scale = salaryScales.FirstOrDefault(s => s.GradeId == emp.GradeId.Value && s.StageId == emp.StageId.Value);
                    if (scale != null)
                    {
                        basicSalary = scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
                    }
                }

                var startDate = request.EffectiveStartDate ?? (emp.HireDate ?? DateOnly.FromDateTime(DateTime.Today));
                var calcAmount = Math.Round((basicSalary * percentageVal) / 100m, 2);

                var newPeriod = new EmployeeAllowancePeriod
                {
                    EmployeeId = emp.Id,
                    StartDate = startDate,
                    IsCurrent = true,
                    BasicSalary = basicSalary,
                    TotalPercentageRate = percentageVal,
                    TotalPercentageAmount = calcAmount,
                    TotalFixedAmount = 0m,
                    GrossSalary = basicSalary + calcAmount,
                    OrderNumber = request.OrderNumber?.Trim(),
                    ChangeReason = string.IsNullOrWhiteSpace(request.ChangeReason) ? $"استيراد {allowanceName}" : request.ChangeReason.Trim(),
                    Notes = string.IsNullOrWhiteSpace(row.ResponsibilityRaw) ? $"استيراد ملف إكسل لـ {allowanceName}" : $"بيان: {row.ResponsibilityRaw}",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Items = new List<EmployeeAllowanceItem>
                    {
                        new EmployeeAllowanceItem
                        {
                            AllowanceTypeId = allowanceType.Id,
                            Name = allowanceName,
                            CalculationType = AllowanceCalculationType.Percentage,
                            Percentage = percentageVal,
                            CalculatedAmount = calcAmount,
                            Notes = string.IsNullOrWhiteSpace(row.ResponsibilityRaw) ? null : $"بيان: {row.ResponsibilityRaw}"
                        }
                    }
                };

                _dbContext.EmployeeAllowancePeriods.Add(newPeriod);
                activePeriodsByEmpId[emp.Id] = newPeriod;
                importedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var msg = $"تم إكمال استيراد {allowanceName} بنجاح: تم إنشاء فترات جديدة لـ ({importedCount}) موظف، وتحديث المخصصات في الفترات الفعالة لـ ({updatedCount}) موظف، وتخطي ({skippedCount}) صفوف.";
        return new BankingAllowanceImportResponse(totalRows, importedCount, updatedCount, skippedCount, errors, msg);
    }

    // ==========================================
    // 13. Craft, Social & Hazard Allowances (مخصصات الحرفة والاجتماعية والخطورة)
    // ==========================================

    public async Task<ExcelParseResult<CraftAndSocialAllowanceImportRow>> ParseCraftAndSocialAllowancesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ExcelParseResult<CraftAndSocialAllowanceImportRow>(
                Array.Empty<CraftAndSocialAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0);
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchCraftAndSocialAllowanceField, "EmployeeID");
        if (headerRow is null)
        {
            return new ExcelParseResult<CraftAndSocialAllowanceImportRow>(
                Array.Empty<CraftAndSocialAllowanceImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة أو لم يتم العثور على صف العناوين.") },
                0);
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف' لربطه مع جدول الموظفين."));
        }

        var hasAnyAllowanceCol = colMap.ContainsKey("Craft") || colMap.ContainsKey("Marriage") || colMap.ContainsKey("Children") || colMap.ContainsKey("Hazard");
        if (!hasAnyAllowanceCol)
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على أي عمود للمخصصات (مخصصات الحرفة، مخصصات زوجية، مخصصات اطفال، مخصصات خطورة)."));
        }

        if (errors.Count > 0)
        {
            return new ExcelParseResult<CraftAndSocialAllowanceImportRow>(
                Array.Empty<CraftAndSocialAllowanceImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0);
        }

        var rawRows = new List<(int RowNum, string EmpId, string? Name, decimal? CraftPct, decimal? MarriageFixed, decimal? ChildrenFixed, decimal? HazardFixed)>();

        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID")?.Trim();
            var name = GetVal("EmployeeName")?.Trim();
            var craftRaw = colMap.ContainsKey("Craft") ? GetVal("Craft")?.Trim() : null;
            var marriageRaw = colMap.ContainsKey("Marriage") ? GetVal("Marriage")?.Trim() : null;
            var childrenRaw = colMap.ContainsKey("Children") ? GetVal("Children")?.Trim() : null;
            var hazardRaw = colMap.ContainsKey("Hazard") ? GetVal("Hazard")?.Trim() : null;

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(name) &&
                string.IsNullOrWhiteSpace(craftRaw) && string.IsNullOrWhiteSpace(marriageRaw) &&
                string.IsNullOrWhiteSpace(childrenRaw) && string.IsNullOrWhiteSpace(hazardRaw))
            {
                continue;
            }

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: رقم الموظف مطلوب للربط."));
            }

            var craftPct = ExtractPercentageFromResponsibility(craftRaw);
            var marriageFixed = ExtractFixedAmount(marriageRaw);
            var childrenFixed = ExtractFixedAmount(childrenRaw);
            var hazardFixed = ExtractFixedAmount(hazardRaw);

            rawRows.Add((rowNum, empId ?? string.Empty, name, craftPct, marriageFixed, childrenFixed, hazardFixed));
        }

        // Match against database
        var distinctEmpIds = rawRows.Select(r => r.EmpId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => distinctEmpIds.Contains(e.EmployeeID))
            .Select(e => new { e.Id, e.EmployeeID, e.Name, e.GradeId, e.StageId, e.HireDate })
            .ToListAsync(cancellationToken);

        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);
        var empDbIds = employees.Select(e => e.Id).ToList();

        var activePeriods = await _dbContext.EmployeeAllowancePeriods
            .AsNoTracking()
            .Where(p => empDbIds.Contains(p.EmployeeId) && p.IsCurrent)
            .ToDictionaryAsync(p => p.EmployeeId, cancellationToken);

        var salaryScales = await _dbContext.SalaryScales.AsNoTracking().ToListAsync(cancellationToken);

        var rows = new List<CraftAndSocialAllowanceImportRow>();

        foreach (var r in rawRows)
        {
            var isMatched = empDict.TryGetValue(r.EmpId, out var emp);
            string? matchedName = null;
            decimal? basicSalary = null;
            decimal? craftCalcAmount = null;
            string statusMsg;

            if (isMatched && emp != null)
            {
                matchedName = emp.Name;
                if (activePeriods.TryGetValue(emp.Id, out var activePeriod))
                {
                    basicSalary = activePeriod.BasicSalary;
                }
                else if (emp.GradeId.HasValue && emp.StageId.HasValue)
                {
                    var scale = salaryScales.FirstOrDefault(s => s.GradeId == emp.GradeId.Value && s.StageId == emp.StageId.Value);
                    if (scale != null)
                    {
                        basicSalary = scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
                    }
                }

                if (r.CraftPct.HasValue && basicSalary.HasValue && basicSalary.Value > 0)
                {
                    craftCalcAmount = Math.Round((basicSalary.Value * r.CraftPct.Value) / 100m, 2);
                }

                var parts = new List<string>();
                if (r.CraftPct.HasValue && r.CraftPct.Value > 0)
                    parts.Add($"حرفة: {r.CraftPct.Value}%" + (craftCalcAmount.HasValue ? $" ({craftCalcAmount.Value:N0} د.ع)" : ""));
                if (r.MarriageFixed.HasValue && r.MarriageFixed.Value > 0)
                    parts.Add($"زوجية: {r.MarriageFixed.Value:N0} د.ع");
                if (r.ChildrenFixed.HasValue && r.ChildrenFixed.Value > 0)
                    parts.Add($"أطفال: {r.ChildrenFixed.Value:N0} د.ع");
                if (r.HazardFixed.HasValue && r.HazardFixed.Value > 0)
                    parts.Add($"خطورة: {r.HazardFixed.Value:N0} د.ع");

                if (parts.Count > 0)
                {
                    statusMsg = "جاهز للتطبيق: " + string.Join(" | ", parts);
                }
                else
                {
                    statusMsg = "لا توجد مخصصات محددة لهذا الموظف في هذا الصف";
                }
            }
            else
            {
                statusMsg = "الرقم الوظيفي غير موجود في جدول الموظفين بالنظام";
            }

            rows.Add(new CraftAndSocialAllowanceImportRow(
                RowNumber: r.RowNum,
                EmployeeID: r.EmpId,
                EmployeeName: r.Name,
                CraftPercentage: r.CraftPct,
                MarriageFixedAmount: r.MarriageFixed,
                ChildrenFixedAmount: r.ChildrenFixed,
                HazardFixedAmount: r.HazardFixed,
                IsMatched: isMatched,
                MatchedName: matchedName,
                BasicSalary: basicSalary,
                CraftCalculatedAmount: craftCalcAmount,
                StatusMessage: statusMsg
            ));
        }

        return new ExcelParseResult<CraftAndSocialAllowanceImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows
        );
    }

    public async Task<CraftAndSocialAllowanceImportResponse> ImportCraftAndSocialAllowancesAsync(CraftAndSocialAllowanceImportRequest request, CancellationToken cancellationToken = default)
    {
        var totalRows = request.Rows.Count;
        var importedCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;
        var errors = new List<EmployeeImportError>();

        if (totalRows == 0)
        {
            return new CraftAndSocialAllowanceImportResponse(0, 0, 0, 0, errors, "لم يتم إرسال أي صفوف للاستيراد.");
        }

        // 1. Ensure allowance types exist
        var craftType = await _dbContext.AllowanceTypes.FirstOrDefaultAsync(t => t.Code == "CRAFT", cancellationToken);
        if (craftType == null)
        {
            craftType = new AllowanceType
            {
                Name = "مخصصات الحرفة",
                Code = "CRAFT",
                CalculationType = AllowanceCalculationType.Percentage,
                DefaultPercentage = 15m,
                IsActive = true,
                SortOrder = 5,
                Description = "مخصصات الحرفة المعتمدة للملاكات المهنية والحرفية كنسبة مئوية من الراتب الاسمي"
            };
            _dbContext.AllowanceTypes.Add(craftType);
        }

        var marriageType = await _dbContext.AllowanceTypes.FirstOrDefaultAsync(t => t.Code == "MARRIAGE", cancellationToken);
        if (marriageType == null)
        {
            marriageType = new AllowanceType
            {
                Name = "المخصصات الزوجية",
                Code = "MARRIAGE",
                CalculationType = AllowanceCalculationType.Fixed,
                DefaultAmount = 50000m,
                IsActive = true,
                SortOrder = 6,
                Description = "مخصصات مقطوعة تمنح للموظف المتزوج بموجب عقد زواج رسمي مصدق"
            };
            _dbContext.AllowanceTypes.Add(marriageType);
        }

        var childrenType = await _dbContext.AllowanceTypes.FirstOrDefaultAsync(t => t.Code == "CHILDREN", cancellationToken);
        if (childrenType == null)
        {
            childrenType = new AllowanceType
            {
                Name = "مخصصات الأطفال والأسرة",
                Code = "CHILDREN",
                CalculationType = AllowanceCalculationType.Fixed,
                DefaultAmount = 10000m,
                IsActive = true,
                SortOrder = 7,
                Description = "مخصصات مقطوعة تمنح عن كل طفل مستحق قانوناً"
            };
            _dbContext.AllowanceTypes.Add(childrenType);
        }

        var hazardType = await _dbContext.AllowanceTypes.FirstOrDefaultAsync(t => t.Code == "HAZARD", cancellationToken);
        if (hazardType == null)
        {
            hazardType = new AllowanceType
            {
                Name = "مخصصات خطورة المهنة وبيئة العمل",
                Code = "HAZARD",
                CalculationType = AllowanceCalculationType.Fixed,
                DefaultAmount = 50000m,
                IsActive = true,
                SortOrder = 8,
                Description = "مخصصات خطورة مقطوعة للعاملين في البيئات الخطرة والأمنية"
            };
            _dbContext.AllowanceTypes.Add(hazardType);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 2. Fetch Employees
        var empIds = request.Rows.Select(r => r.EmployeeID.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var employees = await _dbContext.Employees
            .Where(e => empIds.Contains(e.EmployeeID))
            .ToListAsync(cancellationToken);
        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);

        // 3. Fetch Active Periods with items
        var empDbIds = employees.Select(e => e.Id).ToList();
        var activePeriods = await _dbContext.EmployeeAllowancePeriods
            .Include(p => p.Items)
            .Where(p => empDbIds.Contains(p.EmployeeId) && p.IsCurrent)
            .ToListAsync(cancellationToken);
        var activePeriodsByEmpId = activePeriods.ToDictionary(p => p.EmployeeId);

        var salaryScales = await _dbContext.SalaryScales.AsNoTracking().ToListAsync(cancellationToken);

        foreach (var row in request.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.EmployeeID))
            {
                skippedCount++;
                continue;
            }

            if (!empDict.TryGetValue(row.EmployeeID.Trim(), out var emp))
            {
                skippedCount++;
                errors.Add(new EmployeeImportError(row.RowNumber, "EmployeeID", $"الرقم الوظيفي '{row.EmployeeID}' غير مسجل في قاعدة بيانات الموظفين."));
                continue;
            }

            var hasCraft = row.CraftPercentage.HasValue && row.CraftPercentage.Value > 0;
            var hasMarriage = row.MarriageFixedAmount.HasValue && row.MarriageFixedAmount.Value > 0;
            var hasChildren = row.ChildrenFixedAmount.HasValue && row.ChildrenFixedAmount.Value > 0;
            var hasHazard = row.HazardFixedAmount.HasValue && row.HazardFixedAmount.Value > 0;

            if (!hasCraft && !hasMarriage && !hasChildren && !hasHazard)
            {
                skippedCount++;
                continue;
            }

            if (activePeriodsByEmpId.TryGetValue(emp.Id, out var period))
            {
                // Update or Add CRAFT (Percentage)
                if (hasCraft)
                {
                    var craftRate = row.CraftPercentage!.Value;
                    var item = period.Items.FirstOrDefault(i => (i.AllowanceTypeId.HasValue && i.AllowanceTypeId.Value == craftType.Id) || i.Name.Contains("حرف"));
                    if (item != null)
                    {
                        item.Name = "مخصصات الحرفة";
                        item.AllowanceTypeId = craftType.Id;
                        item.CalculationType = AllowanceCalculationType.Percentage;
                        item.Percentage = craftRate;
                        item.CalculatedAmount = Math.Round((period.BasicSalary * craftRate) / 100m, 2);
                        item.Notes = "تحديث مخصصات الحرفة";
                    }
                    else
                    {
                        period.Items.Add(new EmployeeAllowanceItem
                        {
                            PeriodId = period.Id,
                            AllowanceTypeId = craftType.Id,
                            Name = "مخصصات الحرفة",
                            CalculationType = AllowanceCalculationType.Percentage,
                            Percentage = craftRate,
                            CalculatedAmount = Math.Round((period.BasicSalary * craftRate) / 100m, 2),
                            Notes = "إضافة مخصصات الحرفة"
                        });
                    }
                }

                // Update or Add MARRIAGE (Fixed)
                if (hasMarriage)
                {
                    var marriageAmt = row.MarriageFixedAmount!.Value;
                    var item = period.Items.FirstOrDefault(i => (i.AllowanceTypeId.HasValue && i.AllowanceTypeId.Value == marriageType.Id) || i.Name.Contains("زوج"));
                    if (item != null)
                    {
                        item.Name = "المخصصات الزوجية";
                        item.AllowanceTypeId = marriageType.Id;
                        item.CalculationType = AllowanceCalculationType.Fixed;
                        item.FixedAmount = marriageAmt;
                        item.CalculatedAmount = marriageAmt;
                        item.Notes = "تحديث المخصصات الزوجية";
                    }
                    else
                    {
                        period.Items.Add(new EmployeeAllowanceItem
                        {
                            PeriodId = period.Id,
                            AllowanceTypeId = marriageType.Id,
                            Name = "المخصصات الزوجية",
                            CalculationType = AllowanceCalculationType.Fixed,
                            FixedAmount = marriageAmt,
                            CalculatedAmount = marriageAmt,
                            Notes = "إضافة المخصصات الزوجية"
                        });
                    }
                }

                // Update or Add CHILDREN (Fixed)
                if (hasChildren)
                {
                    var childrenAmt = row.ChildrenFixedAmount!.Value;
                    var item = period.Items.FirstOrDefault(i => (i.AllowanceTypeId.HasValue && i.AllowanceTypeId.Value == childrenType.Id) || i.Name.Contains("طفل") || i.Name.Contains("اطفال") || i.Name.Contains("أولاد") || i.Name.Contains("اولاد"));
                    if (item != null)
                    {
                        item.Name = "مخصصات الأطفال والأسرة";
                        item.AllowanceTypeId = childrenType.Id;
                        item.CalculationType = AllowanceCalculationType.Fixed;
                        item.FixedAmount = childrenAmt;
                        item.CalculatedAmount = childrenAmt;
                        item.Notes = "تحديث مخصصات الأطفال";
                    }
                    else
                    {
                        period.Items.Add(new EmployeeAllowanceItem
                        {
                            PeriodId = period.Id,
                            AllowanceTypeId = childrenType.Id,
                            Name = "مخصصات الأطفال والأسرة",
                            CalculationType = AllowanceCalculationType.Fixed,
                            FixedAmount = childrenAmt,
                            CalculatedAmount = childrenAmt,
                            Notes = "إضافة مخصصات الأطفال"
                        });
                    }
                }

                // Update or Add HAZARD (Fixed)
                if (hasHazard)
                {
                    var hazardAmt = row.HazardFixedAmount!.Value;
                    var item = period.Items.FirstOrDefault(i => (i.AllowanceTypeId.HasValue && i.AllowanceTypeId.Value == hazardType.Id) || i.Name.Contains("خطر") || i.Name.Contains("خطور"));
                    if (item != null)
                    {
                        item.Name = "مخصصات خطورة المهنة وبيئة العمل";
                        item.AllowanceTypeId = hazardType.Id;
                        item.CalculationType = AllowanceCalculationType.Fixed;
                        item.FixedAmount = hazardAmt;
                        item.CalculatedAmount = hazardAmt;
                        item.Notes = "تحديث مخصصات الخطورة المقطوعة";
                    }
                    else
                    {
                        period.Items.Add(new EmployeeAllowanceItem
                        {
                            PeriodId = period.Id,
                            AllowanceTypeId = hazardType.Id,
                            Name = "مخصصات خطورة المهنة وبيئة العمل",
                            CalculationType = AllowanceCalculationType.Fixed,
                            FixedAmount = hazardAmt,
                            CalculatedAmount = hazardAmt,
                            Notes = "إضافة مخصصات الخطورة المقطوعة"
                        });
                    }
                }

                if (request.EffectiveStartDate.HasValue) period.StartDate = request.EffectiveStartDate.Value;
                if (!string.IsNullOrWhiteSpace(request.OrderNumber)) period.OrderNumber = request.OrderNumber.Trim();
                if (!string.IsNullOrWhiteSpace(request.ChangeReason)) period.ChangeReason = request.ChangeReason.Trim();

                // Recalculate totals
                decimal totalPctRate = 0m;
                decimal totalPctAmt = 0m;
                decimal totalFixedAmt = 0m;

                foreach (var item in period.Items)
                {
                    if (item.CalculationType == AllowanceCalculationType.Percentage)
                    {
                        var rate = item.Percentage ?? 0m;
                        totalPctRate += rate;
                        item.CalculatedAmount = Math.Round((period.BasicSalary * rate) / 100m, 2);
                        totalPctAmt += item.CalculatedAmount;
                    }
                    else
                    {
                        item.CalculatedAmount = item.FixedAmount ?? 0m;
                        totalFixedAmt += item.CalculatedAmount;
                    }
                }

                period.TotalPercentageRate = totalPctRate;
                period.TotalPercentageAmount = totalPctAmt;
                period.TotalFixedAmount = totalFixedAmt;
                period.GrossSalary = period.BasicSalary + totalPctAmt + totalFixedAmt;

                updatedCount++;
            }
            else
            {
                // Create new active period
                decimal basicSalary = 0m;
                if (emp.GradeId.HasValue && emp.StageId.HasValue)
                {
                    var scale = salaryScales.FirstOrDefault(s => s.GradeId == emp.GradeId.Value && s.StageId == emp.StageId.Value);
                    if (scale != null)
                    {
                        basicSalary = scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
                    }
                }

                var startDate = request.EffectiveStartDate ?? (emp.HireDate ?? DateOnly.FromDateTime(DateTime.Today));
                var items = new List<EmployeeAllowanceItem>();
                decimal totalPctRate = 0m;
                decimal totalPctAmt = 0m;
                decimal totalFixedAmt = 0m;

                if (hasCraft)
                {
                    var rate = row.CraftPercentage!.Value;
                    var calcAmt = Math.Round((basicSalary * rate) / 100m, 2);
                    totalPctRate += rate;
                    totalPctAmt += calcAmt;
                    items.Add(new EmployeeAllowanceItem
                    {
                        AllowanceTypeId = craftType.Id,
                        Name = "مخصصات الحرفة",
                        CalculationType = AllowanceCalculationType.Percentage,
                        Percentage = rate,
                        CalculatedAmount = calcAmt,
                        Notes = "استيراد مخصصات الحرفة"
                    });
                }

                if (hasMarriage)
                {
                    var amt = row.MarriageFixedAmount!.Value;
                    totalFixedAmt += amt;
                    items.Add(new EmployeeAllowanceItem
                    {
                        AllowanceTypeId = marriageType.Id,
                        Name = "المخصصات الزوجية",
                        CalculationType = AllowanceCalculationType.Fixed,
                        FixedAmount = amt,
                        CalculatedAmount = amt,
                        Notes = "استيراد المخصصات الزوجية"
                    });
                }

                if (hasChildren)
                {
                    var amt = row.ChildrenFixedAmount!.Value;
                    totalFixedAmt += amt;
                    items.Add(new EmployeeAllowanceItem
                    {
                        AllowanceTypeId = childrenType.Id,
                        Name = "مخصصات الأطفال والأسرة",
                        CalculationType = AllowanceCalculationType.Fixed,
                        FixedAmount = amt,
                        CalculatedAmount = amt,
                        Notes = "استيراد مخصصات الأطفال"
                    });
                }

                if (hasHazard)
                {
                    var amt = row.HazardFixedAmount!.Value;
                    totalFixedAmt += amt;
                    items.Add(new EmployeeAllowanceItem
                    {
                        AllowanceTypeId = hazardType.Id,
                        Name = "مخصصات خطورة المهنة وبيئة العمل",
                        CalculationType = AllowanceCalculationType.Fixed,
                        FixedAmount = amt,
                        CalculatedAmount = amt,
                        Notes = "استيراد مخصصات الخطورة المقطوعة"
                    });
                }

                var newPeriod = new EmployeeAllowancePeriod
                {
                    EmployeeId = emp.Id,
                    StartDate = startDate,
                    IsCurrent = true,
                    BasicSalary = basicSalary,
                    TotalPercentageRate = totalPctRate,
                    TotalPercentageAmount = totalPctAmt,
                    TotalFixedAmount = totalFixedAmt,
                    GrossSalary = basicSalary + totalPctAmt + totalFixedAmt,
                    OrderNumber = request.OrderNumber?.Trim(),
                    ChangeReason = string.IsNullOrWhiteSpace(request.ChangeReason) ? "استيراد مخصصات الحرفة والاجتماعية والخطورة" : request.ChangeReason.Trim(),
                    Notes = "استيراد ملف مخصصات الحرفة والاجتماعية والخطورة",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Items = items
                };

                _dbContext.EmployeeAllowancePeriods.Add(newPeriod);
                activePeriodsByEmpId[emp.Id] = newPeriod;
                importedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var msg = $"تم إكمال استيراد مخصصات الحرفة والاجتماعية والخطورة بنجاح: تم إنشاء فترات جديدة لـ ({importedCount}) موظف، وتحديث المخصصات في الفترات الفعالة لـ ({updatedCount}) موظف، وتخطي ({skippedCount}) صفوف.";
        return new CraftAndSocialAllowanceImportResponse(totalRows, importedCount, updatedCount, skippedCount, errors, msg);
    }

    // ==========================================
    // 14. Marital Status (الحالة الاجتماعية للموظفين)
    // ==========================================

    public async Task<ExcelParseResult<MaritalStatusImportRow>> ParseMaritalStatusExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ExcelParseResult<MaritalStatusImportRow>(
                Array.Empty<MaritalStatusImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0);
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchMaritalStatusField, "EmployeeID");
        if (headerRow is null)
        {
            return new ExcelParseResult<MaritalStatusImportRow>(
                Array.Empty<MaritalStatusImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة أو لم يتم العثور على صف العناوين.") },
                0);
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف' لربطه مع جدول الموظفين."));
        }

        if (!colMap.ContainsKey("MaritalStatus"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الحالة الاجتماعية' في ترويسة الملف."));
        }

        if (errors.Count > 0)
        {
            return new ExcelParseResult<MaritalStatusImportRow>(
                Array.Empty<MaritalStatusImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0);
        }

        var rawRows = new List<(int RowNum, string EmpId, string? Name, string? StatusRaw, string? NormalizedStatus)>();

        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID")?.Trim();
            var name = GetVal("EmployeeName")?.Trim();
            var statusRaw = GetVal("MaritalStatus")?.Trim();

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(statusRaw))
            {
                continue;
            }

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: رقم الموظف مطلوب للربط."));
            }

            var normalized = !string.IsNullOrWhiteSpace(statusRaw) ? MaritalStatusDataSeeder.NormalizeStatus(statusRaw) : null;

            rawRows.Add((rowNum, empId ?? string.Empty, name, statusRaw, normalized));
        }

        // Match against database
        var distinctEmpIds = rawRows.Select(r => r.EmpId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Include(e => e.MaritalStatus)
            .Where(e => distinctEmpIds.Contains(e.EmployeeID))
            .Select(e => new { e.Id, e.EmployeeID, e.Name, CurrentStatus = e.MaritalStatus != null ? e.MaritalStatus.Name : null })
            .ToListAsync(cancellationToken);

        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);

        var existingStatuses = await _dbContext.MaritalStatuses
            .AsNoTracking()
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);
        var existingStatusSet = new HashSet<string>(existingStatuses, StringComparer.OrdinalIgnoreCase);

        var rows = new List<MaritalStatusImportRow>();

        foreach (var r in rawRows)
        {
            var isMatched = empDict.TryGetValue(r.EmpId, out var emp);
            string? matchedName = null;
            string? currentStatus = null;
            string statusMsg;

            if (isMatched && emp != null)
            {
                matchedName = emp.Name;
                currentStatus = emp.CurrentStatus;

                if (!string.IsNullOrWhiteSpace(r.NormalizedStatus))
                {
                    if (existingStatusSet.Contains(r.NormalizedStatus))
                    {
                        statusMsg = $"جاهز للتحديث إلى '{r.NormalizedStatus}'" + (!string.IsNullOrWhiteSpace(currentStatus) ? $" (الحالي: {currentStatus})" : " (غير محدد حالياً)");
                    }
                    else
                    {
                        statusMsg = $"سيتم إنشاء حالة اجتماعية جديدة '{r.NormalizedStatus}' وتثبيتها بالنظام للموظف";
                    }
                }
                else
                {
                    statusMsg = "حقل الحالة الاجتماعية فارغ في هذا الصف";
                }
            }
            else
            {
                statusMsg = "الرقم الوظيفي غير موجود في جدول الموظفين بالنظام";
            }

            rows.Add(new MaritalStatusImportRow(
                RowNumber: r.RowNum,
                EmployeeID: r.EmpId,
                EmployeeName: r.Name,
                MaritalStatusRaw: r.StatusRaw,
                IsMatched: isMatched,
                MatchedName: matchedName,
                CurrentMaritalStatus: currentStatus,
                ResolvedMaritalStatus: r.NormalizedStatus,
                StatusMessage: statusMsg
            ));
        }

        return new ExcelParseResult<MaritalStatusImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows
        );
    }

    public async Task<MaritalStatusImportResponse> ImportMaritalStatusAsync(MaritalStatusImportRequest request, CancellationToken cancellationToken = default)
    {
        var totalRows = request.Rows.Count;
        var updatedCount = 0;
        var skippedCount = 0;
        var newStatusesCreatedCount = 0;
        var errors = new List<EmployeeImportError>();

        if (totalRows == 0)
        {
            return new MaritalStatusImportResponse(0, 0, 0, 0, errors, "لم يتم إرسال أي صفوف للاستيراد.");
        }

        // 1. Load existing marital statuses
        var existingStatuses = await _dbContext.MaritalStatuses.ToListAsync(cancellationToken);
        var statusDict = existingStatuses.ToDictionary(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase);

        // 2. Fetch Employees
        var empIds = request.Rows.Select(r => r.EmployeeID.Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var employees = await _dbContext.Employees
            .Where(e => empIds.Contains(e.EmployeeID))
            .ToListAsync(cancellationToken);
        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);

        foreach (var row in request.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.EmployeeID))
            {
                skippedCount++;
                continue;
            }

            if (!empDict.TryGetValue(row.EmployeeID.Trim(), out var emp))
            {
                skippedCount++;
                errors.Add(new EmployeeImportError(row.RowNumber, "EmployeeID", $"الرقم الوظيفي '{row.EmployeeID}' غير مسجل في قاعدة بيانات الموظفين."));
                continue;
            }

            var statusStr = !string.IsNullOrWhiteSpace(row.ResolvedMaritalStatus)
                ? row.ResolvedMaritalStatus.Trim()
                : (!string.IsNullOrWhiteSpace(row.MaritalStatusRaw) ? MaritalStatusDataSeeder.NormalizeStatus(row.MaritalStatusRaw) : null);

            if (string.IsNullOrWhiteSpace(statusStr))
            {
                skippedCount++;
                continue;
            }

            if (!statusDict.TryGetValue(statusStr, out var maritalStatus))
            {
                if (request.CreateMissingStatuses)
                {
                    maritalStatus = new MaritalStatus { Name = statusStr };
                    _dbContext.MaritalStatuses.Add(maritalStatus);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    statusDict[statusStr] = maritalStatus;
                    newStatusesCreatedCount++;
                }
                else
                {
                    skippedCount++;
                    errors.Add(new EmployeeImportError(row.RowNumber, "MaritalStatus", $"الحالة الاجتماعية '{statusStr}' غير معرفة في قائمة النظام."));
                    continue;
                }
            }

            emp.MaritalStatusId = maritalStatus.Id;
            updatedCount++;
        }

        if (updatedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var msg = $"تم إكمال استيراد وتحديث الحالة الاجتماعية بنجاح: تم تحديث ({updatedCount}) موظف، وإضافة ({newStatusesCreatedCount}) حالات جديدة في قائمة النظام، وتخطي ({skippedCount}) صفوف.";
        return new MaritalStatusImportResponse(totalRows, updatedCount, skippedCount, newStatusesCreatedCount, errors, msg);
    }

    public async Task<MartyrsImportPreviewResponse> ParseMartyrsExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new MartyrsImportPreviewResponse
            {
                TotalRows = 0,
                ValidRows = 0,
                InvalidRows = 0,
                Rows = []
            };
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchMartyrField, "EmployeeID");
        if (headerRow is null)
        {
            return new MartyrsImportPreviewResponse
            {
                TotalRows = 0,
                ValidRows = 0,
                InvalidRows = 0,
                Rows = []
            };
        }

        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

        var rawRows = new List<MartyrImportRow>();

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID")?.Trim();
            var martyrName = GetVal("MartyrName")?.Trim();
            var martyrdomType = GetVal("MartyrdomType")?.Trim();
            var kinship = GetVal("Kinship")?.Trim();
            var decisionNumber = GetVal("DecisionNumber")?.Trim();
            var notes = GetVal("Notes")?.Trim();
            var empName = GetVal("EmployeeName")?.Trim();

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(martyrName))
            {
                continue;
            }

            var item = new MartyrImportRow
            {
                RowIndex = rowNum,
                EmployeeIdNumber = empId ?? string.Empty,
                MatchedEmployeeName = empName,
                MartyrName = martyrName ?? string.Empty,
                MartyrdomType = martyrdomType ?? string.Empty,
                Kinship = !string.IsNullOrWhiteSpace(kinship) ? MartyrDataSeeder.NormalizeKinship(kinship) : string.Empty,
                DecisionNumber = decisionNumber,
                Notes = notes
            };

            rawRows.Add(item);
        }

        var distinctEmpIds = rawRows.Select(r => r.EmployeeIdNumber).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => distinctEmpIds.Contains(e.EmployeeID))
            .Select(e => new { e.Id, e.EmployeeID, e.Name })
            .ToListAsync(cancellationToken);

        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);

        var empIdsList = employees.Select(e => e.Id).ToList();
        var existingMartyrs = await _dbContext.EmployeeMartyrs
            .AsNoTracking()
            .Where(m => empIdsList.Contains(m.EmployeeId))
            .Select(m => new { m.EmployeeId, m.MartyrName })
            .ToListAsync(cancellationToken);

        var existingMartyrSet = new HashSet<string>(
            existingMartyrs.Select(m => $"{m.EmployeeId}:{m.MartyrName.Trim().ToLowerInvariant()}"),
            StringComparer.OrdinalIgnoreCase);

        var validCount = 0;
        var invalidCount = 0;
        var dupCount = 0;

        foreach (var r in rawRows)
        {
            if (string.IsNullOrWhiteSpace(r.EmployeeIdNumber))
            {
                r.IsValid = false;
                r.ErrorMessage = "الرقم الوظيفي للموظف مفقود في هذا الصف.";
                invalidCount++;
                continue;
            }

            if (!empDict.TryGetValue(r.EmployeeIdNumber, out var emp))
            {
                r.EmployeeExists = false;
                r.IsValid = false;
                r.ErrorMessage = $"الرقم الوظيفي '{r.EmployeeIdNumber}' غير مسجل في جدول الموظفين.";
                invalidCount++;
                continue;
            }

            r.EmployeeExists = true;
            r.MatchedEmployeeName = emp.Name;

            if (string.IsNullOrWhiteSpace(r.MartyrName))
            {
                r.IsValid = false;
                r.ErrorMessage = "اسم الشهيد مطلوب.";
                invalidCount++;
                continue;
            }

            var martyrKey = $"{emp.Id}:{r.MartyrName.Trim().ToLowerInvariant()}";
            if (existingMartyrSet.Contains(martyrKey))
            {
                r.IsAlreadyRegistered = true;
                dupCount++;
            }

            r.IsValid = true;
            validCount++;
        }

        return new MartyrsImportPreviewResponse
        {
            TotalRows = rawRows.Count,
            ValidRows = validCount,
            InvalidRows = invalidCount,
            DuplicateOrExistingRows = dupCount,
            Rows = rawRows
        };
    }

    public async Task<MartyrsImportResult> ImportMartyrsAsync(MartyrsImportRequest request, string username, CancellationToken cancellationToken = default)
    {
        if (request.Rows.Count == 0)
        {
            return new MartyrsImportResult
            {
                Success = false,
                ImportedCount = 0,
                SkippedCount = 0,
                Messages = ["لم يتم تحديد أي سجلات للاستيراد."]
            };
        }

        var distinctEmpIds = request.Rows
            .Select(r => r.EmployeeIdNumber?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        var employees = await _dbContext.Employees
            .Where(e => distinctEmpIds.Contains(e.EmployeeID))
            .ToListAsync(cancellationToken);

        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);

        // Preload or create lookups
        var allTypes = await _dbContext.MartyrdomTypes.ToListAsync(cancellationToken);
        var typeDict = allTypes.ToDictionary(t => t.Name.Trim(), StringComparer.OrdinalIgnoreCase);

        var allKinships = await _dbContext.MartyrKinships.ToListAsync(cancellationToken);
        var kinshipDict = allKinships.ToDictionary(k => k.Name.Trim(), StringComparer.OrdinalIgnoreCase);

        if (request.ReplaceExisting && employees.Count > 0)
        {
            var empDbIds = employees.Select(e => e.Id).ToList();
            var oldRecords = await _dbContext.EmployeeMartyrs
                .Where(m => empDbIds.Contains(m.EmployeeId))
                .ToListAsync(cancellationToken);
            _dbContext.EmployeeMartyrs.RemoveRange(oldRecords);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var importedCount = 0;
        var skippedCount = 0;
        var messages = new List<string>();

        foreach (var row in request.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.EmployeeIdNumber) || string.IsNullOrWhiteSpace(row.MartyrName))
            {
                skippedCount++;
                continue;
            }

            if (!empDict.TryGetValue(row.EmployeeIdNumber.Trim(), out var emp))
            {
                skippedCount++;
                continue;
            }

            // Resolve MartyrdomType
            int? typeId = null;
            if (!string.IsNullOrWhiteSpace(row.MartyrdomType))
            {
                var cleanType = row.MartyrdomType.Trim();
                if (typeDict.TryGetValue(cleanType, out var t))
                {
                    typeId = t.Id;
                }
                else
                {
                    var newType = new MartyrdomType { Name = cleanType };
                    _dbContext.MartyrdomTypes.Add(newType);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    typeDict[cleanType] = newType;
                    typeId = newType.Id;
                }
            }

            // Resolve Kinship
            int? kinshipId = null;
            var normalizedKinship = !string.IsNullOrWhiteSpace(row.Kinship) ? MartyrDataSeeder.NormalizeKinship(row.Kinship) : null;
            if (!string.IsNullOrWhiteSpace(normalizedKinship))
            {
                if (kinshipDict.TryGetValue(normalizedKinship, out var k))
                {
                    kinshipId = k.Id;
                }
                else
                {
                    var newKinship = new MartyrKinship { Name = normalizedKinship, Degree = 1 };
                    _dbContext.MartyrKinships.Add(newKinship);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    kinshipDict[normalizedKinship] = newKinship;
                    kinshipId = newKinship.Id;
                }
            }

            var record = new EmployeeMartyr
            {
                EmployeeId = emp.Id,
                EmployeeIdNumber = emp.EmployeeID,
                MartyrName = row.MartyrName.Trim(),
                MartyrdomTypeId = typeId,
                KinshipId = kinshipId,
                DecisionNumber = !string.IsNullOrWhiteSpace(row.DecisionNumber) ? row.DecisionNumber.Trim() : null,
                Notes = !string.IsNullOrWhiteSpace(row.Notes) ? row.Notes.Trim() : null,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.EmployeeMartyrs.Add(record);
            importedCount++;
        }

        if (importedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        messages.Add($"تم إنجاز استيراد ذوي الشهداء بنجاح: تم حفظ ({importedCount}) سجلاً، وتخطي ({skippedCount}) سجلاً.");

        return new MartyrsImportResult
        {
            Success = true,
            ImportedCount = importedCount,
            SkippedCount = skippedCount,
            Messages = messages
        };
    }

    public async Task<ExcelParseResult<FinancialAccountImportRow>> ParseFinancialAccountsExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ExcelParseResult<FinancialAccountImportRow>(
                Array.Empty<FinancialAccountImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0);
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchFinancialAccountField, "EmployeeID");
        if (headerRow is null)
        {
            return new ExcelParseResult<FinancialAccountImportRow>(
                Array.Empty<FinancialAccountImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "ورقة العمل فارغة أو لم يتم العثور على صف العناوين.") },
                0);
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' أو 'رقم الموظف'."));
        }

        if (errors.Count > 0)
        {
            return new ExcelParseResult<FinancialAccountImportRow>(
                Array.Empty<FinancialAccountImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0);
        }

        var rawRows = new List<(int RowNum, string EmpId, string? Name, decimal? Balance, decimal? Loan, decimal? Advance, string? Notes)>();
        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = GetVal("EmployeeID")?.Trim();
            var name = GetVal("EmployeeName")?.Trim();
            var rawBal = GetVal("Balance")?.Trim();
            var rawLoan = GetVal("LoanRemaining")?.Trim();
            var rawAdvance = GetVal("AdvanceRemaining")?.Trim();
            var notes = GetVal("Notes")?.Trim();

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(rawBal) && string.IsNullOrWhiteSpace(rawLoan) && string.IsNullOrWhiteSpace(rawAdvance))
                continue;

            totalRows++;

            if (string.IsNullOrWhiteSpace(empId))
            {
                errors.Add(new EmployeeImportError(rowNum, "EmployeeID", $"الصف {rowNum}: الرقم الوظيفي مطلوب."));
                continue;
            }

            var balance = ExtractFixedAmount(rawBal);
            var loan = ExtractFixedAmount(rawLoan);
            var advance = ExtractFixedAmount(rawAdvance);

            rawRows.Add((rowNum, empId, name, balance, loan, advance, notes));
        }

        var empIds = rawRows.Select(r => r.EmpId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => empIds.Contains(e.EmployeeID))
            .ToDictionaryAsync(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var rows = new List<FinancialAccountImportRow>();
        foreach (var r in rawRows)
        {
            var resolvedName = r.Name;
            if (string.IsNullOrWhiteSpace(resolvedName) && employees.TryGetValue(r.EmpId, out var emp))
            {
                resolvedName = emp.Name;
            }

            rows.Add(new FinancialAccountImportRow(
                r.EmpId,
                resolvedName,
                r.Balance,
                r.Loan,
                r.Advance,
                r.Notes));
        }

        return new ExcelParseResult<FinancialAccountImportRow>(
            rows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows);
    }

    public async Task<FinancialAccountImportResponse> ImportFinancialAccountsAsync(
        FinancialAccountImportRequest request,
        string? username = null,
        CancellationToken cancellationToken = default)
    {
        var empIds = request.Rows.Select(r => r.EmployeeID.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var employees = await _dbContext.Employees
            .Where(e => empIds.Contains(e.EmployeeID))
            .ToDictionaryAsync(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var employeeDbIds = employees.Values.Select(e => e.Id).ToList();
        var accounts = await _dbContext.EmployeeFinancialAccounts
            .Where(a => employeeDbIds.Contains(a.EmployeeId))
            .ToDictionaryAsync(a => a.EmployeeId, cancellationToken);

        var updated = 0;
        var skipped = 0;
        var missingEmpIds = new List<string>();
        var now = DateTime.UtcNow;

        foreach (var row in request.Rows)
        {
            var empId = row.EmployeeID.Trim();
            if (!employees.TryGetValue(empId, out var employee))
            {
                missingEmpIds.Add(empId);
                skipped++;
                continue;
            }

            if (!accounts.TryGetValue(employee.Id, out var account))
            {
                account = new Data.Financial.EmployeeFinancialAccount
                {
                    EmployeeId = employee.Id,
                    Employee = employee,
                    CreatedAt = now
                };
                _dbContext.EmployeeFinancialAccounts.Add(account);
                accounts[employee.Id] = account;
            }

            var balanceChanged = false;
            var oldBalance = account.Balance;

            if (row.Balance.HasValue)
            {
                if (row.Balance.Value != account.Balance)
                {
                    account.Balance = row.Balance.Value;
                    account.BalanceLastUpdatedAt = now;
                    balanceChanged = true;
                }
            }

            if (row.LoanRemaining.HasValue)
            {
                account.LoanRemaining = row.LoanRemaining.Value;
                account.LoanLastUpdatedAt = now;
            }

            if (row.AdvanceRemaining.HasValue)
            {
                account.AdvanceRemaining = row.AdvanceRemaining.Value;
                account.AdvanceLastUpdatedAt = now;
            }

            if (!string.IsNullOrWhiteSpace(row.Notes))
            {
                account.Notes = row.Notes.Trim();
            }

            account.UpdatedAt = now;
            account.LastUpdatedBy = username ?? "استيراد ملف Excel";

            if (balanceChanged)
            {
                var diff = account.Balance - oldBalance;
                var trx = new Data.Financial.EmployeeBalanceTransaction
                {
                    EmployeeFinancialAccountId = account.Id,
                    FinancialAccount = account,
                    EmployeeId = employee.Id,
                    TransactionType = account.BalanceLastUpdatedAt == null ? Data.Financial.FinancialTransactionType.InitialBalance : Data.Financial.FinancialTransactionType.Adjustment,
                    Amount = Math.Abs(diff),
                    BalanceBefore = oldBalance,
                    BalanceAfter = account.Balance,
                    TransactionDate = now,
                    Description = !string.IsNullOrWhiteSpace(row.Notes) ? $"استيراد إكسل: {row.Notes.Trim()}" : "تحديث رصيد عبر استيراد Excel",
                    ReferenceNumber = "IMP-" + now.ToString("yyyyMMddHHmmss"),
                    CreatedBy = username ?? "استيراد ملف Excel",
                    CreatedAt = now
                };
                _dbContext.EmployeeBalanceTransactions.Add(trx);
            }

            updated++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new FinancialAccountImportResponse(
            request.Rows.Count,
            updated,
            skipped,
            missingEmpIds);
    }

    // ==========================================
    // 15. Job Titles & Classification Import
    // ==========================================

    public async Task<ExcelParseResult<JobTitleImportRow>> ParseJobTitlesExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);

        // Find the best worksheet that contains JobTitle and preferably JobFamily:
        IXLWorksheet? worksheet = null;
        IXLRow? headerRow = null;
        Dictionary<string, int>? colMap = null;
        List<string>? matchedHeaders = null;
        List<string>? unmappedHeaders = null;

        foreach (var ws in workbook.Worksheets)
        {
            var (hRow, cMap, matched, unmapped) = FindHeaderRow(ws, MatchJobTitleField, "JobTitle");
            if (hRow != null)
            {
                // Prefer sheet that has both JobTitle and JobFamily (e.g. 'العوائل الوظيفي')
                if (cMap.ContainsKey("JobTitle") && cMap.ContainsKey("JobFamily"))
                {
                    worksheet = ws;
                    headerRow = hRow;
                    colMap = cMap;
                    matchedHeaders = matched;
                    unmappedHeaders = unmapped;
                    break;
                }

                if (worksheet == null || cMap.Count > (colMap?.Count ?? 0))
                {
                    worksheet = ws;
                    headerRow = hRow;
                    colMap = cMap;
                    matchedHeaders = matched;
                    unmappedHeaders = unmapped;
                }
            }
        }

        worksheet ??= workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ExcelParseResult<JobTitleImportRow>(
                Array.Empty<JobTitleImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0);
        }

        if (headerRow is null || colMap is null || matchedHeaders is null || unmappedHeaders is null)
        {
            var res = FindHeaderRow(worksheet, MatchJobTitleField, "JobTitle");
            headerRow = res.HeaderRow;
            colMap = res.ColMap;
            matchedHeaders = res.MatchedHeaders;
            unmappedHeaders = res.UnmappedHeaders;
        }

        if (headerRow is null)
        {
            return new ExcelParseResult<JobTitleImportRow>(
                Array.Empty<JobTitleImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", $"ورقة العمل '{worksheet.Name}' فارغة أو لم يتم العثور على صف العناوين. يجب أن يحتوي الملف على عمود 'العنوان الوظيفي'.") },
                0);
        }

        var errors = new List<EmployeeImportError>();

        // Smart column resolution: if JobTitle wasn't explicitly matched by name:
        if (!colMap.ContainsKey("JobTitle"))
        {
            // 1. If there's an unmapped column in the header row, map the first unmapped one
            if (unmappedHeaders.Count > 0)
            {
                foreach (var cell in headerRow.CellsUsed())
                {
                    var text = cell.GetString().Trim();
                    if (unmappedHeaders.Contains(text) && !colMap.Values.Contains(cell.Address.ColumnNumber))
                    {
                        colMap["JobTitle"] = cell.Address.ColumnNumber;
                        matchedHeaders.Add($"{text} (عنوان وظيفي)");
                        unmappedHeaders.Remove(text);
                        break;
                    }
                }
            }

            // 2. Positional fallback: in standard 3/4 column catalog layout (الرمز, العائلة, العنوان, الدرجة)
            if (!colMap.ContainsKey("JobTitle"))
            {
                var usedCols = headerRow.CellsUsed().Select(c => c.Address.ColumnNumber).OrderBy(c => c).ToList();
                var freeCol = usedCols.FirstOrDefault(c => !colMap.Values.Contains(c));
                if (freeCol > 0)
                {
                    colMap["JobTitle"] = freeCol;
                }
            }
        }

        if (!colMap.ContainsKey("JobTitle"))
        {
            var detected = matchedHeaders.Count > 0 ? string.Join("، ", matchedHeaders) : "لا يوجد";
            var unmapped = unmappedHeaders.Count > 0 ? string.Join("، ", unmappedHeaders) : "لا يوجد";
            errors.Add(new EmployeeImportError(
                headerRow.RowNumber(),
                "header",
                $"الصف {headerRow.RowNumber()}: لم يتم العثور على عمود 'العنوان الوظيفي'. الأعمدة التي تم التعرف عليها: ({detected}). الأعمدة غير المعرفة: ({unmapped})."
            ));
        }

        if (errors.Count > 0)
        {
            return new ExcelParseResult<JobTitleImportRow>(
                Array.Empty<JobTitleImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0);
        }

        var rawRows = new List<(int RowNum, string? Code, string? Family, string Title, string? Grade)>();
        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var totalRows = 0;
        string? lastGrade = null;

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var code = GetVal("Code")?.Trim();
            var family = GetVal("JobFamily")?.Trim();
            var title = GetVal("JobTitle")?.Trim();
            var grade = GetVal("Grade")?.Trim();

            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(family) && string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(grade))
            {
                continue;
            }

            // Forward-fill grade if rows are grouped under a single grade header
            if (!string.IsNullOrWhiteSpace(grade))
            {
                lastGrade = grade;
            }
            else if (!string.IsNullOrWhiteSpace(lastGrade))
            {
                grade = lastGrade;
            }

            totalRows++;

            if (string.IsNullOrWhiteSpace(title))
            {
                errors.Add(new EmployeeImportError(rowNum, "JobTitle", $"الصف {rowNum}: اسم العنوان الوظيفي مطلوب."));
                continue;
            }

            rawRows.Add((rowNum, code, family, title, grade));
        }

        var existingTitles = await _dbContext.JobTitles
            .AsNoTracking()
            .Include(j => j.JobFamily)
            .Include(j => j.Grade)
            .ToListAsync(cancellationToken);
        var existingByName = existingTitles.ToDictionary(j => j.Name.Trim(), j => j, StringComparer.OrdinalIgnoreCase);
        var existingFamilies = await _dbContext.JobFamilies.AsNoTracking().ToListAsync(cancellationToken);
        var grades = await _dbContext.Grades.AsNoTracking().ToListAsync(cancellationToken);

        var parsedRows = new List<JobTitleImportRow>();

        foreach (var r in rawRows)
        {
            existingByName.TryGetValue(r.Title, out var existing);
            var isExisting = existing != null;
            var matchedGrade = FindGradeByCodeOrName(grades, r.Grade);

            var matchedFamily = !string.IsNullOrWhiteSpace(r.Family)
                ? existingFamilies.FirstOrDefault(f =>
                    string.Equals(f.Name.Trim(), r.Family.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(f.Code) && string.Equals(f.Code.Trim(), r.Family.Trim(), StringComparison.OrdinalIgnoreCase)))
                : null;

            var statusMsg = isExisting
                ? $"تحديث العنوان المسجل حالياً (معرف: {existing!.Id})"
                : "عنوان وظيفي جديد سيتم إدراجه في النظام";

            if (matchedGrade != null)
            {
                statusMsg += $" • مطابقة الدرجة: {matchedGrade.Name} (الرمز: {matchedGrade.Code})";
            }

            if (!string.IsNullOrWhiteSpace(r.Family))
            {
                if (matchedFamily != null)
                {
                    statusMsg += $" • العائلة مسجلة: {matchedFamily.Name}";
                }
                else
                {
                    statusMsg += $" • عائلة وظيفية جديدة ستضاف تلقائياً: {r.Family}";
                }
            }

            parsedRows.Add(new JobTitleImportRow(
                RowNumber: r.RowNum,
                Code: r.Code,
                JobFamily: matchedFamily != null ? matchedFamily.Name : r.Family,
                Name: r.Title,
                GradeName: matchedGrade != null ? matchedGrade.Name : r.Grade,
                IsExisting: isExisting,
                CurrentCode: existing?.Code,
                CurrentFamily: existing?.JobFamily?.Name,
                CurrentGrade: existing?.Grade?.Name,
                StatusMessage: statusMsg
            ));
        }

        return new ExcelParseResult<JobTitleImportRow>(
            parsedRows,
            matchedHeaders,
            unmappedHeaders,
            errors,
            totalRows
        );
    }

    public async Task<JobTitleImportResponse> ImportJobTitlesAsync(JobTitleImportRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Rows.Count == 0)
        {
            return new JobTitleImportResponse(0, 0, 0, 0, new List<EmployeeImportError>(), "لا توجد صفوف للاستيراد.");
        }

        var errors = new List<EmployeeImportError>();
        var existingTitles = await _dbContext.JobTitles
            .Include(j => j.JobFamily)
            .Include(j => j.Grade)
            .ToListAsync(cancellationToken);
        var existingByName = existingTitles.ToDictionary(j => j.Name.Trim(), j => j, StringComparer.OrdinalIgnoreCase);

        var existingFamilies = await _dbContext.JobFamilies.ToListAsync(cancellationToken);
        var familyByName = existingFamilies.ToDictionary(f => f.Name.Trim(), f => f, StringComparer.OrdinalIgnoreCase);

        var grades = await _dbContext.Grades.AsNoTracking().ToListAsync(cancellationToken);

        int createdCount = 0;
        int updatedCount = 0;
        int skippedCount = 0;
        int newFamiliesCount = 0;

        foreach (var row in request.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                skippedCount++;
                continue;
            }

            // 1. Resolve or Auto-Create JobFamily (عائلة وظيفية جديدة تضاف تلقائياً)
            JobFamily? matchedFamily = null;
            if (!string.IsNullOrWhiteSpace(row.JobFamily))
            {
                var cleanFam = row.JobFamily.Trim();
                if (!familyByName.TryGetValue(cleanFam, out matchedFamily))
                {
                    matchedFamily = existingFamilies.FirstOrDefault(f =>
                        (!string.IsNullOrEmpty(f.Code) && string.Equals(f.Code.Trim(), cleanFam, StringComparison.OrdinalIgnoreCase)));

                    if (matchedFamily == null)
                    {
                        matchedFamily = new JobFamily
                        {
                            Name = cleanFam,
                            CreatedAt = DateTime.UtcNow
                        };
                        _dbContext.JobFamilies.Add(matchedFamily);
                        existingFamilies.Add(matchedFamily);
                        familyByName[cleanFam] = matchedFamily;
                        newFamiliesCount++;
                    }
                }
            }

            // 2. Resolve Grade by Code from Grades table
            int? matchedGradeId = null;
            if (!string.IsNullOrWhiteSpace(row.GradeName) && request.AutoLinkGrades)
            {
                var gradeMatch = FindGradeByCodeOrName(grades, row.GradeName);
                if (gradeMatch != null)
                {
                    matchedGradeId = gradeMatch.Id;
                }
            }

            // 3. Upsert JobTitle
            if (existingByName.TryGetValue(row.Name.Trim(), out var existing))
            {
                if (!string.IsNullOrWhiteSpace(row.Code)) existing.Code = row.Code.Trim();
                if (matchedFamily != null) existing.JobFamily = matchedFamily;
                if (matchedGradeId.HasValue) existing.GradeId = matchedGradeId.Value;

                updatedCount++;
            }
            else
            {
                var newTitle = new JobTitle
                {
                    Name = row.Name.Trim(),
                    Code = row.Code?.Trim(),
                    JobFamily = matchedFamily,
                    GradeId = matchedGradeId
                };
                _dbContext.JobTitles.Add(newTitle);
                existingByName[newTitle.Name] = newTitle;
                createdCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var familyMsg = newFamiliesCount > 0 ? $"، وإضافة {newFamiliesCount} عائلة وظيفية جديدة" : "";
        return new JobTitleImportResponse(
            TotalRows: request.Rows.Count,
            CreatedCount: createdCount,
            UpdatedCount: updatedCount,
            SkippedCount: skippedCount,
            Errors: errors,
            Message: $"تم استيراد وحفظ العناوين الوظيفية بنجاح: إنشاء {createdCount} عنوان جديد، وتحديث {updatedCount} عنوان حالي{familyMsg}."
        );
    }

    // ==========================================
    // 5b. Employee Last Allowance Date (تاريخ آخر علاوة سنوية)
    // ==========================================

    public async Task<ExcelParseResult<LastAllowanceDateImportRow>> ParseLastAllowanceDateExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return new ExcelParseResult<LastAllowanceDateImportRow>(
                Array.Empty<LastAllowanceDateImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", "الملف لا يحتوي على أي أوراق عمل (Sheets).") },
                0);
        }

        var (headerRow, colMap, matchedHeaders, unmappedHeaders) = FindHeaderRow(worksheet, MatchLastAllowanceDateField, "EmployeeID");
        if (headerRow is null)
        {
            return new ExcelParseResult<LastAllowanceDateImportRow>(
                Array.Empty<LastAllowanceDateImportRow>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new EmployeeImportError(0, "sheet", $"ورقة العمل '{worksheet.Name}' فارغة أو لم يتم العثور على ترويسة الأعمدة المطلوبة (الرقم الوظيفي وتاريخ آخر علاوة سنوية).") },
                0);
        }

        var errors = new List<EmployeeImportError>();

        if (!colMap.ContainsKey("EmployeeID"))
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على عمود 'الرقم الوظيفي' للربط مع ملف الموظف."));
        }

        bool hasAllowanceDateCol = colMap.ContainsKey("CurrentYearAllowanceDate")
                                || colMap.ContainsKey("BaseAllowanceDate")
                                || colMap.ContainsKey("LastAllowanceDate");

        if (!hasAllowanceDateCol)
        {
            errors.Add(new EmployeeImportError(headerRow.RowNumber(), "header", "لم يتم العثور على أي عمود لتاريخ العلاوة (مثل: 'تاريخ علاوة السنة الحالية' أو 'تاريخ العلاوة الاساس' أو 'تاريخ آخر علاوة سنوية')."));
        }

        if (errors.Count > 0)
        {
            return new ExcelParseResult<LastAllowanceDateImportRow>(
                Array.Empty<LastAllowanceDateImportRow>(),
                matchedHeaders,
                unmappedHeaders,
                errors,
                0);
        }

        var startRowNum = headerRow.RowNumber() + 1;
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var rawList = new List<(int RowNum, string EmpId, string? EmpName, string? RawCurrentYear, string? RawBase, string? RawGeneral)>();

        for (var rowNum = startRowNum; rowNum <= lastRowNum; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            string? GetVal(string field) =>
                colMap.TryGetValue(field, out var colIdx) ? ExtractCellValue(row.Cell(colIdx)) : null;

            var empId = NormalizeDigits(GetVal("EmployeeID"));
            var empName = GetVal("EmployeeName")?.Trim();
            var rawCurrentYear = GetVal("CurrentYearAllowanceDate")?.Trim();
            var rawBase = GetVal("BaseAllowanceDate")?.Trim();
            var rawGeneral = GetVal("LastAllowanceDate")?.Trim();

            if (string.IsNullOrWhiteSpace(empId) && string.IsNullOrWhiteSpace(rawCurrentYear) && string.IsNullOrWhiteSpace(rawBase) && string.IsNullOrWhiteSpace(rawGeneral))
            {
                continue;
            }

            rawList.Add((rowNum, empId ?? string.Empty, empName, rawCurrentYear, rawBase, rawGeneral));
        }

        var targetEmpIds = rawList
            .Select(r => r.EmpId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var employees = await _dbContext.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => targetEmpIds.Contains(e.EmployeeID))
            .Select(e => new { e.Id, e.EmployeeID, e.Name, e.LastAllowanceDate })
            .ToListAsync(cancellationToken);

        var empDict = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);
        var resultRows = new List<LastAllowanceDateImportRow>();
        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var item in rawList)
        {
            var empId = item.EmpId;
            bool isMatched = false;
            string? matchedName = item.EmpName;
            DateOnly? currentAllowanceDate = null;
            DateOnly? parsedDate = null;
            bool isValid = true;
            string statusMsg;

            // Collect and validate all candidate dates in this row
            var allCandidates = new List<(string Source, DateOnly Date, string Raw)>();

            if (!string.IsNullOrWhiteSpace(item.RawCurrentYear))
            {
                if (TryParseAllowanceDate(item.RawCurrentYear, out var dCurr) && dCurr.HasValue)
                {
                    allCandidates.Add(("علاوة السنة الحالية", dCurr.Value, item.RawCurrentYear));
                }
                else
                {
                    allCandidates.Add(("علاوة السنة الحالية (غير صالحة)", DateOnly.MinValue, item.RawCurrentYear));
                }
            }

            if (!string.IsNullOrWhiteSpace(item.RawBase))
            {
                if (TryParseAllowanceDate(item.RawBase, out var dBase) && dBase.HasValue)
                {
                    allCandidates.Add(("العلاوة الأساس", dBase.Value, item.RawBase));
                }
                else
                {
                    allCandidates.Add(("العلاوة الأساس (غير صالحة)", DateOnly.MinValue, item.RawBase));
                }
            }

            if (!string.IsNullOrWhiteSpace(item.RawGeneral))
            {
                if (TryParseAllowanceDate(item.RawGeneral, out var dGen) && dGen.HasValue)
                {
                    allCandidates.Add(("آخر علاوة", dGen.Value, item.RawGeneral));
                }
                else
                {
                    allCandidates.Add(("آخر علاوة (غير صالحة)", DateOnly.MinValue, item.RawGeneral));
                }
            }

            var displayParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.RawCurrentYear)) displayParts.Add($"الحالية: {item.RawCurrentYear}");
            if (!string.IsNullOrWhiteSpace(item.RawBase)) displayParts.Add($"الأساس: {item.RawBase}");
            if (!string.IsNullOrWhiteSpace(item.RawGeneral)) displayParts.Add($"العلاوة: {item.RawGeneral}");
            var allowanceDateRaw = displayParts.Count > 0 ? string.Join(" | ", displayParts) : "فارغ";

            if (string.IsNullOrWhiteSpace(empId))
            {
                isValid = false;
                statusMsg = "الرقم الوظيفي مفقود في هذا الصف.";
            }
            else if (!empDict.TryGetValue(empId, out var emp))
            {
                isValid = false;
                isMatched = false;
                statusMsg = $"الرقم الوظيفي '{empId}' غير مسجل في ملفات الموظفين.";
            }
            else
            {
                isMatched = true;
                matchedName = emp.Name;
                currentAllowanceDate = emp.LastAllowanceDate;

                var validDates = allCandidates.Where(c => c.Date > DateOnly.MinValue).ToList();

                if (validDates.Count == 0)
                {
                    isValid = false;
                    statusMsg = "صيغة التاريخ غير صالحة أو غير مقروءة في هذا الصف.";
                }
                else
                {
                    // تطبيق قاعدة المستخدم:
                    // 1. أي تاريخ أكبر من تاريخ اليوم يُهمل
                    // 2. من بين التواريخ الأصغر أو تساوي تاريخ اليوم، نأخذ الأقرب لتاريخ اليوم (الأحدث ماضياً)
                    var pastOrTodayDates = validDates
                        .Where(c => c.Date <= today && c.Date.Year >= 1950)
                        .OrderByDescending(c => c.Date)
                        .ToList();

                    var futureIgnored = validDates
                        .Where(c => c.Date > today)
                        .ToList();

                    if (pastOrTodayDates.Count > 0)
                    {
                        var chosen = pastOrTodayDates[0];
                        parsedDate = chosen.Date;

                        if (currentAllowanceDate.HasValue && currentAllowanceDate.Value == parsedDate.Value)
                        {
                            statusMsg = "مطابق للتاريخ الحالي في ملف الموظف (لا تغيير).";
                            if (futureIgnored.Count > 0)
                            {
                                statusMsg += $" (أُهمل التاريخ المستقبلي: {string.Join("، ", futureIgnored.Select(f => $"{f.Source} {f.Date:yyyy-MM-dd}"))})";
                            }
                        }
                        else
                        {
                            var updateText = currentAllowanceDate.HasValue
                                ? $"تعديل من {currentAllowanceDate.Value:yyyy-MM-dd} إلى {parsedDate.Value:yyyy-MM-dd}"
                                : $"تعيين لأول مرة: {parsedDate.Value:yyyy-MM-dd}";

                            if (futureIgnored.Count > 0)
                            {
                                statusMsg = $"{updateText} (أُخذ {chosen.Source} وأُهمل المستقبلي: {string.Join("، ", futureIgnored.Select(f => $"{f.Source} {f.Date:yyyy-MM-dd}"))})";
                            }
                            else if (validDates.Count > 1)
                            {
                                statusMsg = $"{updateText} (أُخذ {chosen.Source} لأنه الأقرب قبل اليوم)";
                            }
                            else
                            {
                                statusMsg = updateText;
                            }
                        }
                    }
                    else
                    {
                        isValid = false;
                        var futureDatesText = string.Join("، ", futureIgnored.Select(f => $"{f.Source}: {f.Date:yyyy-MM-dd}"));
                        statusMsg = $"التاريخ بالملف ({futureDatesText}) أكبر من تاريخ اليوم ({today:yyyy-MM-dd})، تم إهماله.";
                    }
                }
            }

            resultRows.Add(new LastAllowanceDateImportRow(
                RowNumber: item.RowNum,
                EmployeeID: empId,
                EmployeeName: matchedName,
                AllowanceDateRaw: allowanceDateRaw,
                ParsedDate: parsedDate,
                IsMatched: isMatched,
                MatchedName: matchedName,
                CurrentAllowanceDate: currentAllowanceDate,
                IsValid: isValid,
                StatusMessage: statusMsg
            ));
        }

        return new ExcelParseResult<LastAllowanceDateImportRow>(
            Rows: resultRows,
            MatchedHeaders: matchedHeaders,
            UnmappedHeaders: unmappedHeaders,
            ParseErrors: errors,
            TotalRowsInSheet: resultRows.Count
        );
    }

    public async Task<LastAllowanceDateImportResponse> ImportLastAllowanceDateAsync(LastAllowanceDateImportRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Rows.Count == 0)
        {
            return new LastAllowanceDateImportResponse(0, 0, 0, new List<EmployeeImportError>(), "لا توجد صفوف للاستيراد والتحديث.");
        }

        var errors = new List<EmployeeImportError>();
        var validRows = request.Rows
            .Where(r => r.IsValid && r.ParsedDate.HasValue && !string.IsNullOrWhiteSpace(r.EmployeeID))
            .ToList();

        if (validRows.Count == 0)
        {
            return new LastAllowanceDateImportResponse(request.Rows.Count, 0, request.Rows.Count, errors, "لم يتم العثور على أي صفوف صالحة لتحديث تاريخ العلاوة السنوية.");
        }

        var empIds = validRows.Select(r => r.EmployeeID.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var employees = await _dbContext.Employees
            .IgnoreQueryFilters()
            .Where(e => empIds.Contains(e.EmployeeID))
            .ToListAsync(cancellationToken);

        var empMap = employees.ToDictionary(e => e.EmployeeID, StringComparer.OrdinalIgnoreCase);

        int updatedCount = 0;
        int unchangedCount = 0;
        int skippedCount = 0;

        foreach (var row in validRows)
        {
            if (!empMap.TryGetValue(row.EmployeeID.Trim(), out var emp))
            {
                skippedCount++;
                errors.Add(new EmployeeImportError(row.RowNumber, "EmployeeID", $"الموظف بالرقم الوظيفي '{row.EmployeeID}' غير موجود في قاعدة البيانات."));
                continue;
            }

            if (emp.LastAllowanceDate == row.ParsedDate)
            {
                unchangedCount++;
                continue;
            }

            emp.LastAllowanceDate = row.ParsedDate;
            updatedCount++;
        }

        if (updatedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var totalSkipped = (request.Rows.Count - updatedCount);
        var msg = updatedCount > 0
            ? $"تم تحديث تاريخ آخر علاوة سنوية بنجاح لـ {updatedCount} موظفاً{(unchangedCount > 0 ? $" (تم تخطي {unchangedCount} موظفاً لتطابق التاريخ مسبقاً)" : "")}."
            : $"لم يتم إجراء أي تعديل{(unchangedCount > 0 ? $"، جميع التواريخ ({unchangedCount}) مطابقة مسبقاً في ملفات الموظفين." : ".")}";

        return new LastAllowanceDateImportResponse(
            TotalRows: request.Rows.Count,
            UpdatedCount: updatedCount,
            SkippedCount: totalSkipped,
            Errors: errors,
            Message: msg
        );
    }

    // ==========================================
    // 6. Excel Template Generators
    // ==========================================

    public byte[] GenerateEmployeesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("سجلات الموظفين");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي", "الاسم الكامل", "التشكيل الإداري", "العنوان الوظيفي",
            "المسمى الوظيفي / المنصب", "المرحلة", "الدرجة", "التحصيل الدراسي", "الاختصاص",
            "تاريخ آخر عنوان", "تاريخ المباشرة", "الجنس", "الديانة", "القومية",
            "الدائرة الأولى", "تاريخ التولد", "الحالة الوظيفية"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "1001", "أحمد شاكر كريم", "مكتب المدير العام", "معاون مدير عام",
            "مدير قسم", "1", "2", "بكالوريوس", "علوم حاسبات",
            "2023-01-15", "2015-06-01", "ذكر", "مسلم", "عربي",
            "دائرة العمليات والمدفوعات", "1988-04-12", "مستمر بالخدمة"
        };

        var sample2 = new[]
        {
            "1002", "سارة علي حسين", "دائرة الرقابة المالية", "رئيس أقدم",
            "مسؤول شعبة", "2", "3", "ماجستير", "محاسبة وتدقيق",
            "2024-02-10", "2018-09-15", "أنثى", "مسلم", "عربي",
            "دائرة الرقابة المالية", "1992-08-25", "مستمر بالخدمة"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateAppreciationLettersTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("كتب الشكر والتقدير");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي", "كتب الشكر", "تاريخ كتاب الشكر", "عدد كتب الشكر",
            "نوع كتاب شكر", "الملاحظات", "رقم الكتاب"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "1001", "شكر وتقدير لإنجاز نظام الربط المصرفي", "2024-05-12", "1",
            "وزير / محافظ", "عن جهود متميزة في أتمتة الإجراءات", "م.م/452"
        };

        var sample2 = new[]
        {
            "1002", "شكر وتقدير عن تدقيق الحسابات الختامية", "2024-07-20", "1",
            "مدير عام", "إنجاز التدقيق السنوي في الوقت المحدد", "ر.م/890"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateLeavesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("إجازات الموظفين");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي", "نوع الإجازة", "تاريخ بدء الإجازة", "تاريخ نهاية الإجازة",
            "عدد الأيام", "رقم الأمر الإداري", "تاريخ الأمر الإداري", "ملاحظات الإجازة", "حالة الإجازة"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "1001", "إجازة اعتيادية", "2024-03-01", "2024-03-05",
            "5", "أ/123", "2024-02-28", "إجازة اعتيادية سنوية", "معتمدة"
        };

        var sample2 = new[]
        {
            "1002", "إجازة مرضية", "2024-04-10", "2024-04-12",
            "3", "أ/245", "2024-04-10", "تقرير طبي مستشفى اليرموك", "معتمدة"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateLeaveBalancesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("أرصدة إجازات الموظفين");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي", "اسم الموظف (اختياري)", "الرصيد"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "1001", "أحمد شاكر كريم", "45"
        };

        var sample2 = new[]
        {
            "1002", "سارة عادل محمود", "120"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateCareerMovementsTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("حركات الخدمة والتنقلات");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي", "اسم الموظف (اختياري)", "تاريخ التنقل", "التشكيل",
            "محتسب للعلاوة (نعم/لا)", "محتسب للترفيع (نعم/لا)", "محتسب للتقاعد (نعم/لا)", "ملاحظات"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "1001", "أحمد شاكر كريم", "2015-06-01", "دائرة العمليات والمدفوعات",
            "نعم", "نعم", "نعم", "مباشرة تعيين أولى"
        };

        var sample2 = new[]
        {
            "1001", "أحمد شاكر كريم", "2019-01-01", "خارج الخدمة - إجازة 5 سنوات",
            "لا", "لا", "لا", "إجازة بدون راتب خمس سنوات"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateCoursesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("الدورات التدريبية");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي", "اسم الموظف", "اسم الدورة", "تاريخ بداء الدورة",
            "تاريخ نهاية الدورة", "التشكيل", "العنوان الوظيفي", "ملاحظات"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "1001", "أحمد شاكر كريم", "إدارة المشاريع الاحترافية (PMP)", "2024-02-01",
            "2024-02-15", "مكتب المدير العام", "معاون مدير عام", "اجتياز الدورة بنجاح"
        };

        var sample2 = new[]
        {
            "1002", "سارة علي حسين", "التحقيق الإداري ومكافحة الفساد المالي", "2024-05-10",
            "2024-05-14", "دائرة الرقابة المالية", "رئيس أقدم", "مشاركة متميزة"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateCivilDataTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("بيانات البطاقة الموحدة");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "التشكيل",
            "رقم الموظف",
            "تاريخ التولد",
            "الرقم الوطني(البطاقة الموحدة)",
            "تاريخ التعيين",
            "اسم الام",
            "الاسم الاول",
            "الاسم الثاني",
            "الاسم الثالث",
            "الاسم الرابع",
            "الاسم الكامل",
            "البطاقة الموحدة",
            "الجنس",
            "الرقم الوظيفي الموحد",
            "حالة الموظف"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "مكتب المدير العام", "1001", "1988-04-12", "198812345678",
            "2015-06-01", "فاطمة حسن علي", "أحمد", "شاكر",
            "كريم", "محمد", "أحمد شاكر كريم محمد", "نعم",
            "ذكر", "EMP-90021", "مستمر بالخدمة"
        };

        var sample2 = new[]
        {
            "دائرة الرقابة المالية", "1002", "1992-08-25", "199298765432",
            "2018-09-15", "زينب عبد الرضا", "سارة", "علي",
            "حسين", "كاظم", "سارة علي حسين كاظم", "نعم",
            "أنثى", "EMP-90022", "مستمر بالخدمة"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateEmailAccountsTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("البريد المؤسسي للموظفين");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي",
            "اسم الموظف",
            "البريد الإلكتروني المؤسسي"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "1001", "أحمد علي حسن", "ahmed.ali@cbi.iq"
        };

        var sample2 = new[]
        {
            "1002", "سارة محمد كمال", "sara.m@aml.lan"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateBankingAllowancesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("المخصصات المصرفية والمسؤولية");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "التشكيل",
            "رقم الموظف",
            "اسم الموظف",
            "العنوان الوظيفي",
            "المحل الوظيفي",
            "المرحلة",
            "الدرجة",
            "الشهادة",
            "الاختصاص",
            "تاريخ التعيين",
            "المسؤولية"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "دائرة المحاسبة", "1001", "أحمد علي حسن", "مدير أقدم", "المقر", "1", "3", "بكالوريوس", "علوم مالية ومصرفية", "2015-01-10", "مدير قسم 25%"
        };

        var sample2 = new[]
        {
            "دائرة العمليات المالية", "1002", "سارة محمد كمال", "معاون مدير", "المقر", "2", "4", "بكالوريوس", "إدارة واقتصاد", "2018-05-15", "مسؤول شعبة 15%"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateEngineeringAllowancesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("المخصصات الهندسية");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "التشكيل",
            "رقم الموظف",
            "اسم الموظف",
            "العنوان الوظيفي",
            "المسمى الوظيفي",
            "المرحلة",
            "الدرجة",
            "الشهادة",
            "التخصص",
            "تاريخ التعيين",
            "المخصصات"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "الدائرة الهندسية", "2001", "حيدر جاسم كاظم", "مهندس أقدم", "مدير مشروع", "2", "3", "بكالوريوس", "هندسة مدنية", "2016-04-12", "35%"
        };

        var sample2 = new[]
        {
            "دائرة تكنولوجيا المعلومات", "2002", "زينب عبد الكريم", "مهندس", "مسؤول وحدة شبكات", "3", "4", "بكالوريوس", "هندسة برمجيات", "2019-09-01", "35%"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateLegalAllowancesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("المخصصات القانونية");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "التشكيل",
            "رقم الموظف",
            "اسم الموظف",
            "العنوان الوظيفي",
            "الدرجة",
            "المرحلة",
            "المخصصات القانونية"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "الدائرة القانونية", "3001", "مصطفى قاسم رحيم", "مشاور قانوني أقدم", "2", "3", "35%"
        };

        var sample2 = new[]
        {
            "قسم العقود والاتفاقيات", "3002", "نور الهدى صادق", "مشاور قانوني", "3", "4", "35%"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateCertificateAllowancesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("مخصصات الشهادة");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "التشكيل",
            "رقم الموظف",
            "اسم الموظف",
            "الشهادة",
            "التخصص",
            "نسبة المخصص"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "دائرة الأبحاث والإحصاء", "4001", "د. علي هادي سلمان", "دكتوراه", "علوم اقتصادية", "150%"
        };

        var sample2 = new[]
        {
            "دائرة الرقابة المصرفية", "4002", "مريم سعدون جابر", "ماجستير", "محاسبة وتدقيق", "125%"
        };

        var sample3 = new[]
        {
            "دائرة العمليات المالية", "4003", "أحمد كاظم حسن", "بكلوريوس", "إدارة أعمال", "45%"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var c1 = ws.Cell(2, i + 1);
            c1.SetValue(sample1[i]);
            c1.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            c1.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");

            var c2 = ws.Cell(3, i + 1);
            c2.SetValue(sample2[i]);
            c2.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            c2.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");

            var c3 = ws.Cell(4, i + 1);
            c3.SetValue(sample3[i]);
            c3.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            c3.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateNonPracticeAllowancesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("مخصصات عدم مزاولة مهنة");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "التشكيل",
            "الرقم الوظيفي",
            "اسم الموظف",
            "العنوان الوظيفي",
            "الدرجة",
            "المرحلة",
            "نسبة عدم مزاولة المهنة"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "الدائرة القانونية", "3001", "أحمد شاكر كريم", "مشاور قانوني أقدم", "2", "3", "20%"
        };

        var sample2 = new[]
        {
            "قسم الرقابة والتدقيق", "3002", "سارة عادل محمود", "محاسب رئيسي", "3", "4", "20%"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateCraftAndSocialAllowancesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("مخصصات الحرفة والاجتماعية");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي",
            "اسم الموظف",
            "مخصصات الحرفة",
            "مخصصات زوجية",
            "مخصصات اطفال",
            "مخصصات خطورة"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "1001", "أحمد علي حسن", "15%", "50000", "20000", "75000"
        };

        var sample2 = new[]
        {
            "1002", "سارة محمد كمال", "20%", "0", "10000", "50000"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateMaritalStatusTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("الحالة الاجتماعية للموظفين");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي",
            "اسم الموظف",
            "الحالة الاجتماعية"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[] { "1001", "أحمد علي حسن", "متزوج" };
        var sample2 = new[] { "1002", "سارة محمد كمال", "عزباء" };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GeneratePenaltiesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("العقوبات الانضباطية");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "أسم المديرية",
            "رقم الموظف",
            "أسم الموظف",
            "العنوان الوظيفي",
            "العقوبة",
            "تاريخ نيل العقوبة",
            "رقم الكتاب",
            "نوع العقوبة",
            "الملاحظات"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[]
        {
            "دائرة العمليات المصرفية", "1001", "أحمد شاكر كريم", "مشاور قانوني", "لفت نظر", "2024-03-15", "ع/102", "مخالفة تعليمات الدوام", "تنبيه أول"
        };

        var sample2 = new[]
        {
            "المديرية العامة للرقابة", "1002", "سارة عادل محمود", "معاون محاسب", "إنذار", "2024-06-20", "ع/245", "تقصير في أداء الواجب", "بموجب محضر التحقيق"
        };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateMartyrsTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("سجل ذوي الشهداء");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي",
            "اسم الشهيد",
            "نوع الشهادة",
            "الصلة",
            "رقم القرار"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[] { "1001", "حسين شاكر كريم", "ضحايا العمليات الحربية والأخطاء العسكرية والعمليات الإرهابية", "أخ", "1425/2016" };
        var sample2 = new[] { "1002", "علي حسن علوان", "شهداء العمليات العسكرية والحشد الشعبي", "أب", "882/2017" };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateFinancialAccountsTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("البيانات المالية والحسابات");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي",
            "اسم الموظف",
            "الرصيد المالي",
            "متبقي القرض",
            "متبقي السلفة",
            "الملاحظات والبيان"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[] { "1001", "أحمد شاكر كريم", "2500000", "5000000", "1250000", "قرض إسكان وسلفة زواج" };
        var sample2 = new[] { "1002", "سارة عادل محمود", "750000", "0", "500000", "سلفة شخصية بدون قرض" };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateJobTitlesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("العناوين والعوائل الوظيفية");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرمز",
            "العائلة الوظيفية",
            "العنوان الوظيفي",
            "الدرجة الوظيفية"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[] { "101", "العائلة الإدارية", "مدير عام", "الدرجة الأولى" };
        var sample2 = new[] { "201", "العائلة المصرفية", "رئيس أبحاث", "الدرجة الثانية" };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateLastAllowanceDateTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("آخر علاوة سنوية");
        ws.RightToLeft = true;

        var headers = new[]
        {
            "الرقم الوظيفي",
            "اسم الموظف (اختياري)",
            "تاريخ علاوة السنة الحالية",
            "تاريخ العلاوة الاساس"
        };

        ApplyHeaderStyles(ws, headers);

        var sample1 = new[] { "1001", "أحمد شاكر كريم", "2026-04-01", "2025-04-01" };
        var sample2 = new[] { "1002", "زينب عبد الكريم جاسم", "2026-11-15", "2025-11-15" };

        ApplySampleRows(ws, sample1, sample2);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void ApplyHeaderStyles(IXLWorksheet ws, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontName = "Calibri";
            cell.Style.Font.FontSize = 11;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0A1628");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#C0714C");
        }
        ws.Row(1).Height = 28;
    }

    private static void ApplySampleRows(IXLWorksheet ws, string[] sample1, string[] sample2)
    {
        for (var i = 0; i < sample1.Length; i++)
        {
            var c1 = ws.Cell(2, i + 1);
            c1.SetValue(sample1[i]);
            c1.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            c1.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");

            var c2 = ws.Cell(3, i + 1);
            c2.SetValue(sample2[i]);
            c2.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            c2.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");
        }
    }

    private static string? ExtractCellValue(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;

        if (cell.DataType == XLDataType.DateTime)
        {
            var dt = cell.GetDateTime();
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (cell.DataType == XLDataType.Number)
        {
            if (cell.Style.NumberFormat.Format.Contains("yy") || cell.Style.NumberFormat.Format.Contains("mm") || cell.Style.NumberFormat.Format.Contains("dd"))
            {
                try
                {
                    var dt = cell.GetDateTime();
                    return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                catch
                {
                    // ignore
                }
            }
            return cell.GetDouble().ToString("G", CultureInfo.InvariantCulture);
        }

        var text = cell.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    // ==========================================
    // 5. Bulletproof Header Synonyms & Elastic Row Detection
    // ==========================================

    public static (IXLRow? HeaderRow, Dictionary<string, int> ColMap, List<string> MatchedHeaders, List<string> UnmappedHeaders)
        FindHeaderRow(IXLWorksheet worksheet, Func<string, string?> fieldMatcher, string requiredPrimaryField = "EmployeeID")
    {
        var firstRowUsed = worksheet.FirstRowUsed();
        if (firstRowUsed is null)
        {
            return (null, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), new List<string>(), new List<string>());
        }

        var firstRowNum = firstRowUsed.RowNumber();
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? firstRowNum;
        var maxHeaderScanRow = Math.Min(lastRowNum, firstRowNum + 15);

        IXLRow? bestRow = null;
        Dictionary<string, int>? bestColMap = null;
        List<string>? bestMatched = null;
        List<string>? bestUnmapped = null;
        var maxScore = -1;

        for (var rowNum = firstRowNum; rowNum <= maxHeaderScanRow; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            var currentMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var currentMatched = new List<string>();
            var currentUnmapped = new List<string>();

            foreach (var cell in row.CellsUsed())
            {
                var text = cell.GetString().Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                var field = fieldMatcher(text);
                if (field is not null)
                {
                    if (!currentMap.ContainsKey(field))
                    {
                        currentMap[field] = cell.Address.ColumnNumber;
                        currentMatched.Add(text);
                    }
                }
                else
                {
                    currentUnmapped.Add(text);
                }
            }

            var hasPrimary = currentMap.ContainsKey(requiredPrimaryField);
            var recognizedCount = currentMap.Count;

            // Give priority to rows containing the primary field (e.g. EmployeeID)
            if (hasPrimary && recognizedCount > maxScore)
            {
                maxScore = recognizedCount;
                bestRow = row;
                bestColMap = currentMap;
                bestMatched = currentMatched;
                bestUnmapped = currentUnmapped;
            }
        }

        if (bestRow is not null)
        {
            return (bestRow, bestColMap!, bestMatched!, bestUnmapped!);
        }

        // Fallback: use firstRowUsed if no row matched EmployeeID
        var fallbackMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var fallbackMatched = new List<string>();
        var fallbackUnmapped = new List<string>();

        foreach (var cell in firstRowUsed.CellsUsed())
        {
            var text = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            var field = fieldMatcher(text);
            if (field is not null)
            {
                if (!fallbackMap.ContainsKey(field))
                {
                    fallbackMap[field] = cell.Address.ColumnNumber;
                    fallbackMatched.Add(text);
                }
            }
            else
            {
                fallbackUnmapped.Add(text);
            }
        }

        return (firstRowUsed, fallbackMap, fallbackMatched, fallbackUnmapped);
    }

    public static (IXLRow? HeaderRow, Dictionary<string, int> ColMap, List<string> MatchedHeaders, List<string> UnmappedHeaders)
        FindLeaveBalancesHeaderRow(IXLWorksheet worksheet)
    {
        var firstRowUsed = worksheet.FirstRowUsed();
        if (firstRowUsed is null)
        {
            return (null, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), new List<string>(), new List<string>());
        }

        var firstRowNum = firstRowUsed.RowNumber();
        var lastRowNum = worksheet.LastRowUsed()?.RowNumber() ?? firstRowNum;
        var maxHeaderScanRow = Math.Min(lastRowNum, firstRowNum + 15);

        IXLRow? bestRow = null;
        Dictionary<string, int>? bestMap = null;
        List<string>? bestMatched = null;
        List<string>? bestUnmapped = null;

        for (var rowNum = firstRowNum; rowNum <= maxHeaderScanRow; rowNum++)
        {
            var row = worksheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            int? empIdCol = null;
            string? empIdHeader = null;

            int? exactBalanceCol = null;
            string? exactBalanceHeader = null;

            int? altBalanceCol = null;
            string? altBalanceHeader = null;

            int? nameCol = null;
            string? nameHeader = null;

            var unmapped = new List<string>();

            foreach (var cell in row.CellsUsed())
            {
                var text = cell.GetString().Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                var clean = NormalizeHeader(text);
                var compact = clean.Replace(" ", "").Replace("_", "").Replace("-", "").Replace("\"", "").Replace("'", "").Replace("(", "").Replace(")", "").Replace("/", "").Replace(":", "");

                if (IsEmployeeIdHeader(text))
                {
                    empIdCol ??= cell.Address.ColumnNumber;
                    empIdHeader ??= text;
                }
                else if (compact is "الرصيد" or "رصيد")
                {
                    // Strict Priority 1: Exact "الرصيد"
                    exactBalanceCol ??= cell.Address.ColumnNumber;
                    exactBalanceHeader ??= text;
                }
                else if (clean.Contains("سابق") || clean.Contains("مستحق") || clean.Contains("استحقاق") ||
                         clean.Contains("مستنفذ") || clean.Contains("ماخوذ") || clean.Contains("مأخوذ") ||
                         clean.Contains("تمتع") || clean.Contains("متمتع") || clean.Contains("مدور") ||
                         clean.Contains("منقول") || clean.Contains("ايام") || clean.Contains("يوم") ||
                         clean.Contains("عدد") || clean.Contains("تاريخ") || clean.Contains("كتاب") ||
                         clean.Contains("امر"))
                {
                    // Explicitly ignore non-balance columns (previous balance, entitlement, days taken, etc.)
                    unmapped.Add(text);
                }
                else if (compact is "الرصيدالمتبقي" or "رصيدالمتبقي" or "صافيالرصيد" or "رصيدصافي" or "رصيدالاجازات" or "رصيدالاجازاتالمتبقي")
                {
                    // Priority 2 fallback
                    altBalanceCol ??= cell.Address.ColumnNumber;
                    altBalanceHeader ??= text;
                }
                else if (clean.Contains("اسم") && (clean.Contains("موظف") || clean.Contains("كامل") || clean == "الاسم" || clean == "اسم"))
                {
                    nameCol ??= cell.Address.ColumnNumber;
                    nameHeader ??= text;
                }
                else
                {
                    unmapped.Add(text);
                }
            }

            var chosenBalanceCol = exactBalanceCol ?? altBalanceCol;
            var chosenBalanceHeader = exactBalanceHeader ?? altBalanceHeader;

            if (empIdCol.HasValue && chosenBalanceCol.HasValue)
            {
                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EmployeeID"] = empIdCol.Value,
                    ["Balance"] = chosenBalanceCol.Value
                };
                var matched = new List<string> { empIdHeader!, chosenBalanceHeader! };
                if (nameCol.HasValue)
                {
                    map["EmployeeName"] = nameCol.Value;
                    matched.Add(nameHeader!);
                }
                return (row, map, matched, unmapped);
            }

            if (empIdCol.HasValue && bestRow == null)
            {
                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EmployeeID"] = empIdCol.Value
                };
                var matched = new List<string> { empIdHeader! };
                if (chosenBalanceCol.HasValue)
                {
                    map["Balance"] = chosenBalanceCol.Value;
                    matched.Add(chosenBalanceHeader!);
                }
                if (nameCol.HasValue)
                {
                    map["EmployeeName"] = nameCol.Value;
                    matched.Add(nameHeader!);
                }
                bestRow = row;
                bestMap = map;
                bestMatched = matched;
                bestUnmapped = unmapped;
            }
        }

        if (bestRow != null && bestMap != null)
        {
            return (bestRow, bestMap, bestMatched ?? new List<string>(), bestUnmapped ?? new List<string>());
        }

        return (null, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), new List<string>(), new List<string>());
    }

    public static string? NormalizeDaysCount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var sb = new StringBuilder();
        foreach (var ch in raw.Trim())
        {
            if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim();

        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && d >= 0)
        {
            if (Math.Abs(d - Math.Round(d)) < 0.001)
            {
                return ((int)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
            }
            return d.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return cleaned;
    }

    private static string NormalizeHeader(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        return text
            .Replace("ـ", "") // tatweel
            .Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا")
            .Replace("ة", "ه")
            .Replace("ى", "ي")
            .Replace("ئ", "ي").Replace("ؤ", "و")
            .Trim()
            .ToLowerInvariant();
    }

    private static bool IsEmployeeIdHeader(string raw)
    {
        var clean = NormalizeHeader(raw);

        // Exclude letter/order numbers
        if (clean.Contains("كتاب") || clean.Contains("امر") || clean.Contains("قرار") || clean.Contains("علاو") || clean.Contains("صادر"))
            return false;

        if (clean.Contains("employee") || clean.Contains("empid"))
            return true;

        // Check if both "رقم" and ("موظف" or "وظيف" or "هوي" or "سجل") are present
        if (clean.Contains("رقم") && (clean.Contains("موظف") || clean.Contains("وظيف") || clean.Contains("هوي") || clean.Contains("سجل")))
            return true;

        var compact = clean.Replace(" ", "").Replace("_", "").Replace("-", "");
        return compact is "الرقم" or "رقم" or "id" or "الرقمالوظيفي" or "رقمالموظف";
    }

    private static string? MatchEmployeeField(string raw)
    {
        var clean = NormalizeHeader(raw);

        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // Name
        if (clean.Contains("اسم") || clean.Contains("name")) return "Name";

        // Formation / Department
        if (clean.Contains("تشكيل") || clean.Contains("دائره") || clean.Contains("قسم") || clean.Contains("formation") || clean.Contains("department"))
            return "Formation";

        // "المسمى الوظيفي هو نفسة المنصب"
        if ((clean.Contains("منصب") || clean.Contains("مسمي") || clean.Contains("مسمى") || clean.Contains("position")) && !clean.Contains("عنوان"))
            return "PositionTitle";

        // JobTitle
        if (clean.Contains("عنوان") && !clean.Contains("تاريخ"))
            return "JobTitle";

        // Stage & Grade
        if (clean.Contains("مرحله") || clean.Contains("stage")) return "Stage";
        if (clean.Contains("درجه") || clean.Contains("grade")) return "Grade";

        // Education & Specialization
        if (clean.Contains("تحصيل") || clean.Contains("شهاده") || clean.Contains("موهل") || clean.Contains("education") || clean.Contains("degree"))
            return "EducationLevel";
        if (clean.Contains("اختصاص") || clean.Contains("تخصص") || clean.Contains("specialization"))
            return "Specialization";

        // Dates
        if (clean.Contains("اخرعنوان") || (clean.Contains("تاريخ") && clean.Contains("عنوان"))) return "LastJobTitleDate";
        if (clean.Contains("اخرعلاو") || clean.Contains("اخروعلاو") || (clean.Contains("تاريخ") && clean.Contains("علاو"))) return "LastAllowanceDate";
        if (clean.Contains("مباشره") || clean.Contains("تعيين") || clean.Contains("hiredate")) return "HireDate";
        if (clean.Contains("تولد") || clean.Contains("ميلاد") || clean.Contains("birthdate") || clean.Contains("dob")) return "BirthDate";

        // Demographic lookups
        if ((clean.Contains("جنس") || (clean.Contains("نوع") && !clean.Contains("اجاز") && !clean.Contains("كتاب") && !clean.Contains("شكر"))) || clean.Contains("gender")) return "Gender";
        if (clean.Contains("ديان") || clean.Contains("دين") || clean.Contains("religion")) return "Religion";
        if (clean.Contains("قومي") || clean.Contains("جنسي") || clean.Contains("nationality")) return "Nationality";
        if (clean.Contains("دائرهاولي") || clean.Contains("اولدائره") || clean.Contains("firstdept")) return "FirstDepartment";
        if (clean.Contains("حاله") || clean.Contains("وضع") || clean.Contains("status")) return "EmployeeStatus";

        return null;
    }

    private static string? MatchLetterField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Explicitly Ignored Columns (أعمدة يتم تجاهلها بأمان)
        // - اسم الموظف (يهمل لأن الربط عبر الرقم الوظيفي مع جدول الموظفين)
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean.Contains("كامل") || clean.Contains("منتسب") || clean == "الاسم" || clean == "اسم"))
            return null;

        // - التشكيل / الدائرة / القسم
        if (clean.Contains("تشكيل") || clean.Contains("دائر") || clean.Contains("داير") || clean.Contains("قسم") || clean.Contains("department") || clean.Contains("directorate"))
            return null;

        // - العنوان الوظيفي / المنصب
        if (clean.Contains("عنوان") && clean.Contains("وظيف"))
            return null;
        if (clean == "المنصب" || clean.Contains("منصب"))
            return null;

        // - تاريخ العلاوة / تاريخ اخر علاوة / علاوة / ترفيع / مباشرة
        if (clean.Contains("علاو") || clean.Contains("ترفيع") || clean.Contains("مباشر") || clean.Contains("تولد") || clean.Contains("ميلاد"))
            return null;

        // 2. Employee ID: رقم الموظف / الرقم الوظيفي
        if (IsEmployeeIdHeader(raw))
            return "EmployeeID";

        // 3. Letter Date: تاريخ كتب الشكر / تاريخ كتاب الشكر / تاريخ الكتاب / تاريخ الصدور / التاريخ
        if (clean.Contains("تاريخ") || clean.Contains("date"))
            return "LetterDate";

        // 4. Letter Count: عدد كتب الشكر / عدد كتب شكر / عدد الكتب / العدد
        if (clean.Contains("عدد") && (clean.Contains("شكر") || clean.Contains("كتب") || clean.Contains("كتاب") || clean.Contains("count")))
            return "Count";
        if (clean == "عدد كتب الشكر" || clean == "عدد كتب شكر" || clean == "العدد" || clean == "عدد")
            return "Count";

        // 5. Letter Type: نوع كتب شكر / نوع كتاب الشكر / نوع الكتاب / نوع الشكر / التصنيف
        if (clean.Contains("نوع") || clean.Contains("type") || clean.Contains("تصنيف"))
            return "Type";

        // 6. Letter Number: رقم كتاب / رقم الكتاب / رقم الأمر / الصادر
        if (clean.Contains("رقم") && (clean.Contains("كتاب") || clean.Contains("امر") || clean.Contains("قرار") || clean.Contains("صادر")))
            return "LetterNumber";
        if (clean == "رقم كتاب" || clean == "رقم الكتاب" || clean == "رقماكتاب" || (clean.StartsWith("رقم") && !clean.Contains("موظف") && !clean.Contains("تاريخ")))
            return "LetterNumber";

        // 7. Letter Title / Category: كتب الشكر / كتاب الشكر / عنوان كتاب الشكر / الموضوع
        if (clean == "كتب الشكر" || clean == "كتب شكر" || clean == "كتاب الشكر" || clean == "كتاب شكر" ||
            clean.Contains("موضوع") || clean.Contains("title") || clean.Contains("subject") ||
            (clean.Contains("كتب") && clean.Contains("شكر")) ||
            (clean.Contains("عنوان") && clean.Contains("شكر")) ||
            (clean.Contains("عنوان") && clean.Contains("كتاب")))
            return "Title";

        // 8. Notes: الملاحظات / ملاحظات
        if (clean.Contains("ملاحظ") || clean.Contains("notes") || clean.Contains("remarks") || clean.Contains("بيان"))
            return "Notes";

        return null;
    }

    private static string? MatchLeaveField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID: رقم الموظف / الرقم الوظيفي
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // Exclude / Ignore: أسم الموظف وأسم الدائرة (يهملان بالكامل من جدول الإجازات لأن الربط يتم عبر الرقم الوظيفي)
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean.Contains("كامل") || clean.Contains("منتسب") || clean.Contains("شخص") || clean == "الاسم" || clean == "اسم"))
            return null;

        if (clean.Contains("دائر") || clean.Contains("داير") || clean.Contains("تشكيل") || clean.Contains("قسم") || clean.Contains("department") || clean.Contains("directorate"))
            return null;

        // 4. Days Count: مجموع الإجازات / عدد الأيام / رصيد الإجازات
        // Checked BEFORE generic LeaveType so "مجموع الإجازات" or "رصيد الإجازات" matches DaysCount!
        if (clean.Contains("مجموع") || clean.Contains("رصيد") || clean.Contains("ايام") || clean.Contains("يوم") || clean.Contains("مده") || clean.Contains("days") ||
            (clean.Contains("عدد") && !clean.Contains("امر") && !clean.Contains("كتاب")))
            return "DaysCount";

        // 5. Start / End Dates
        if (clean.Contains("بدء") || clean.Contains("بداي") || clean.Contains("منتاريخ") || clean.Contains("start"))
            return "StartDate";

        if (clean.Contains("نهاي") || clean.Contains("انتهاء") || clean.Contains("الىتاريخ") || clean.Contains("اليتاريخ") || clean.Contains("end"))
            return "EndDate";

        // 6. Status (checked before generic LeaveType so "حالة الإجازة" is matched to Status)
        if (clean.Contains("حاله") || clean.Contains("وضع") || clean.Contains("status"))
            return "Status";

        // 7. Order Number & Date
        if ((clean.Contains("رقم") || clean.Contains("عدد")) && (clean.Contains("امر") || clean.Contains("كتاب") || clean.Contains("قرار")))
            return "OrderNumber";

        if (clean.Contains("تاريخ") && (clean.Contains("امر") || clean.Contains("كتاب") || clean.Contains("قرار")))
            return "OrderDate";

        // 8. Notes
        if (clean.Contains("ملاحظ") || clean.Contains("سبب") || clean.Contains("notes") || clean.Contains("reason"))
            return "Notes";

        // 9. Leave Type: نوع الإجازة / نوع الاجازة / الإجازة
        if (clean.Contains("نوع") || clean.Contains("type") || clean.Contains("اجاز") || clean.Contains("leave"))
            return "LeaveType";

        return null;
    }

    private static string? MatchCareerMovementField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Name (preview only, ignored by DB)
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean.Contains("كامل") || clean.Contains("منتسب") || clean == "الاسم" || clean == "اسم"))
            return "Name";

        // 3. Flags (Allowance, Promotion, Pension)
        if (clean.Contains("علاو")) return "CountForAllowance";
        if (clean.Contains("ترفيع")) return "CountForPromotion";
        if (clean.Contains("تقاعد")) return "CountForPension";

        // 4. Movement Date
        if (clean.Contains("تنقل") || clean.Contains("حرك") || (clean.Contains("تاريخ") && !clean.Contains("امر") && !clean.Contains("كتاب")))
            return "MovementDate";

        // 5. Formation Name / Description
        if (clean.Contains("تشكيل") || clean.Contains("بيان") || clean.Contains("دائر") || clean.Contains("قسم") || clean.Contains("مقر") || clean.Contains("جهة") || clean.Contains("موقع"))
            return "FormationName";

        // 6. Notes
        if (clean.Contains("ملاحظ") || clean.Contains("notes") || clean.Contains("remarks") || clean.Contains("سبب"))
            return "Notes";

        return null;
    }

    private static string? MatchCourseField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID: الرقم الوظيفي / رقم الموظف
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Dates of course (Check before generic course name):
        // تاريخ بداء الدورة / تاريخ بدء الدورة / تاريخ البدء / من تاريخ
        if (clean.Contains("تاريخ") && (clean.Contains("بدء") || clean.Contains("بداء") || clean.Contains("بداي") || clean.Contains("من") || clean.Contains("start")))
            return "StartDate";
        if (clean.StartsWith("بدء") || clean.StartsWith("بداء") || clean.StartsWith("بداي"))
            return "StartDate";

        // تاريخ نهاية الدورة / تاريخ النهاية / الى تاريخ
        if (clean.Contains("تاريخ") && (clean.Contains("نهاي") || clean.Contains("انتهاء") || clean.Contains("الى") || clean.Contains("الي") || clean.Contains("end")))
            return "EndDate";
        if (clean.StartsWith("نهاي") || clean.StartsWith("انتهاء"))
            return "EndDate";

        // 3. Course Name: اسم الدورة / عنوان الدورة / الدورة التدريبية / الدورة
        if (clean.Contains("دورة") || clean.Contains("دوره") || clean.Contains("course"))
            return "CourseName";

        // 4. Employee Name (Mapped for UI preview only, ignored by DB)
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean.Contains("كامل") || clean.Contains("منتسب") || clean == "الاسم" || clean == "اسم"))
            return "EmployeeName";

        // 5. Explicitly Ignored Columns (أعمدة يتم تجاهلها بأمان كما حدد المستخدم):
        // التشكيل / الدائرة
        if (clean.Contains("تشكيل") || clean.Contains("دائر") || clean.Contains("داير") || clean.Contains("قسم"))
            return null;

        // الجنس
        if (clean.Contains("جنس") || clean.Contains("gender"))
            return null;

        // تاريخ الميلاد / التولد
        if (clean.Contains("تولد") || clean.Contains("ميلاد") || clean.Contains("birth"))
            return null;

        // العنوان الوظيفي / المسمى الوظيفي / المنصب
        if (clean.Contains("عنوان") || clean.Contains("مسمي") || clean.Contains("مسمى") || clean.Contains("منصب"))
            return null;

        // المرحلة / الدرجة
        if (clean.Contains("مرحله") || clean.Contains("درجه") || clean.Contains("grade") || clean.Contains("stage"))
            return null;

        // تاريخ التعيين / المباشرة
        if (clean.Contains("تعيين") || clean.Contains("مباشر") || clean.Contains("hire"))
            return null;

        // الشهادة / التحصيل الدراسي
        if (clean.Contains("شهاده") || clean.Contains("تحصيل") || clean.Contains("موهل"))
            return null;

        // حالة الموظف
        if (clean.Contains("حاله") || clean.Contains("وضع") || clean.Contains("status"))
            return null;

        // Days Count (optional if present)
        if (clean.Contains("ايام") || clean.Contains("يوم") || clean.Contains("مده") || clean.Contains("days") || clean.Contains("عدد"))
            return "DaysCount";

        // Notes
        if (clean.Contains("ملاحظ") || clean.Contains("notes") || clean.Contains("remarks"))
            return "Notes";

        return null;
    }

    public static string? MatchCivilDataField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. National ID / Unified Civil Card: الرقم الوطني(البطاقة الموحدة) / الرقم الوطني / البطاقة الوطنية
        // User explicitly specified: "الرقم الوطني(البطاقة الموحدة) يضاف هذا العامود"
        // Checked FIRST so it doesn't get captured by "موحد" or "بطاقة"
        if (clean.Contains("وطني") || clean.Contains("nationalid") || (clean.Contains("رقم") && clean.Contains("بطاق") && !clean.Contains("وظيف")))
            return "NationalId";

        // 2. Unified Employee Number (الرقم الوظيفي الموحد)
        // User explicitly specified: "الرقم الوظيفي الموحد يضاف"
        if (clean.Contains("موحد") && (clean.Contains("وظيف") || clean.Contains("employe")))
            return "UnifiedEmployeeNumber";
        if (clean == "الرقم الوظيفي الموحد" || clean == "الرقم الموحد" || clean == "الرقم الوظيفي الوطني")
            return "UnifiedEmployeeNumber";

        // 3. Employee ID (رقم الموظف / الرقم الوظيفي) - Key to match and update
        if (IsEmployeeIdHeader(raw))
            return "EmployeeID";

        // 4. Mother Name (اسم الام / أسم الأم / اسم والدة)
        // Must NOT match "الكامل" (which contains "ام")
        if (clean.Contains("والد") || clean.Contains("mother") || clean.Contains("الام") || clean.Contains("ام ") || clean.EndsWith(" ام") || clean.Contains("اسم ام"))
            return "MotherName";

        // 5. Four Name Parts:
        // First Name: الاسم الاول / الاسم الأول
        if ((clean.Contains("اسم") || clean.Contains("أسم")) && (clean.Contains("اول") || clean.Contains("أول") || clean.Contains("1") || clean.Contains("first")) || clean == "الاول" || clean == "الأول")
            return "FirstName";

        // Second Name: الاسم الثاني / الاسم 2
        if ((clean.Contains("اسم") || clean.Contains("أسم")) && (clean.Contains("ثاني") || clean.Contains("ثانى") || clean.Contains("2") || clean.Contains("second")) || clean == "الثاني" || clean == "الثانى")
            return "SecondName";

        // Third Name: الاسم الثالث / الاسم 3
        if ((clean.Contains("اسم") || clean.Contains("أسم")) && (clean.Contains("ثالث") || clean.Contains("3") || clean.Contains("third")) || clean == "الثالث")
            return "ThirdName";

        // Fourth Name: الاسم الرابع / الاسم 4
        if ((clean.Contains("اسم") || clean.Contains("أسم")) && (clean.Contains("رابع") || clean.Contains("4") || clean.Contains("fourth")) || clean == "الرابع")
            return "FourthName";

        // 6. Full Name (الاسم الكامل / اسم الموظف) - For preview only, ignored by DB update
        if (clean.Contains("كامل") || clean.Contains("fullname"))
            return "FullName";

        // 7. Explicitly Ignored Columns (يهمل):
        // التشكيل
        if (clean.Contains("تشكيل") || clean.Contains("دائر") || clean.Contains("قسم"))
            return null;

        // تاريخ التولد
        if (clean.Contains("تولد") || clean.Contains("ميلاد"))
            return null;

        // تاريخ التعيين
        if (clean.Contains("تعيين") || clean.Contains("مباشر"))
            return null;

        // البطاقة الموحدة (when standalone and not containing وطني)
        if (clean.Contains("بطاق") || clean.Contains("موحد"))
            return null;

        // الجنس
        if (clean.Contains("جنس"))
            return null;

        // حالة الموظف
        if (clean.Contains("حاله") || clean.Contains("وضع") || clean.Contains("status"))
            return null;

        return null;
    }

    private static string? MatchEmailAccountField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Email: البريد الالكتروني / البريد / email / mail
        if (clean.Contains("بريد") || clean.Contains("ايميل") || clean.Contains("إيميل") || clean.Contains("email") || clean.Contains("mail"))
            return "Email";

        // 3. Name: الاسم / اسم الموظف / name
        if (clean.Contains("اسم") || clean.Contains("أسم") || clean.Contains("name"))
            return "Name";

        return null;
    }

    public static decimal? ExtractPercentageFromResponsibility(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 1. Convert Eastern Arabic numerals (٠-٩ and ۰-۹) to standard ASCII
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim();

        // 2. Regex to extract numeric sequence (ignoring words like "مدير قسم", "مسؤول شعبة", etc.)
        var match = Regex.Match(cleaned, @"(\d+(?:\.\d+)?)");
        if (match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
        {
            // If the cell had an Excel decimal representation like 0.25 (which means 25%), convert to 25
            if (val > 0 && val <= 1)
            {
                val = val * 100m;
            }
            return val;
        }

        return null;
    }

    public static string? MatchBankingAllowanceField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID: رقم الموظف / الرقم الوظيفي
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Responsibility / Banking Allowance: المسؤولية / مسؤولية / مخصصات مصرفية / المخصصات المصرفية / منصب
        if (clean.Contains("مسؤول") || clean.Contains("مسوول") || clean.Contains("مصرف") || clean.Contains("منصب"))
            return "Responsibility";

        // 3. Employee Name: اسم الموظف / الاسم
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean == "اسم" || clean == "الاسم" || clean.Contains("كامل")))
            return "EmployeeName";

        // 4. Formation: التشكيل / الدائرة / القسم
        if (clean.Contains("تشكيل") || clean.Contains("دائر") || clean.Contains("قسم"))
            return "Formation";

        // 5. Job Title: العنوان الوظيفي / العنوان
        if (clean.Contains("عنوان") || clean.Contains("مسمي") || clean.Contains("مسمى"))
            return "JobTitle";

        // 6. Stage: المرحلة
        if (clean.Contains("مرحل"))
            return "Stage";

        // 7. Grade: الدرجة
        if (clean.Contains("درج"))
            return "Grade";

        // 8. Certificate: الشهادة / التحصيل
        if (clean.Contains("شهاد") || clean.Contains("تحصيل"))
            return "Certificate";

        // 9. Specialization: الاختصاص / التخصص
        if (clean.Contains("اختصاص") || clean.Contains("تخصص"))
            return "Specialization";

        // 10. Hire Date: تاريخ التعيين / التعيين / المباشرة
        if (clean.Contains("تعيين") || clean.Contains("مباشر"))
            return "HireDate";

        return null;
    }

    public static string? MatchEngineeringAllowanceField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID: رقم الموظف / الرقم الوظيفي
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Allowance Rate: المخصصات / المخصصات الهندسية / هندسي / نسبة
        if (clean.Contains("مخصص") || clean.Contains("نسبة") || clean.Contains("هندس"))
            return "AllowanceRate";

        // 3. Employee Name: اسم الموظف / الاسم
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean == "اسم" || clean == "الاسم" || clean.Contains("كامل")))
            return "EmployeeName";

        // 4. Formation: التشكيل / الدائرة / القسم
        if (clean.Contains("تشكيل") || clean.Contains("دائر") || clean.Contains("قسم"))
            return "Formation";

        // 5. Position Title: المسمى الوظيفي
        if (clean.Contains("منصب") || (clean.Contains("مسمي") && !clean.Contains("عنوان")) || (clean.Contains("مسمى") && !clean.Contains("عنوان")))
            return "PositionTitle";

        // 6. Job Title: العنوان الوظيفي / العنوان
        if (clean.Contains("عنوان"))
            return "JobTitle";

        // 7. Stage: المرحلة
        if (clean.Contains("مرحل"))
            return "Stage";

        // 8. Grade: الدرجة
        if (clean.Contains("درج"))
            return "Grade";

        // 9. Certificate: الشهادة / التحصيل
        if (clean.Contains("شهاد") || clean.Contains("تحصيل"))
            return "Certificate";

        // 10. Specialization: الاختصاص / التخصص
        if (clean.Contains("اختصاص") || clean.Contains("تخصص"))
            return "Specialization";

        // 11. Hire Date: تاريخ التعيين / التعيين / المباشرة
        if (clean.Contains("تعيين") || clean.Contains("مباشر"))
            return "HireDate";

        return null;
    }

    public static string? MatchLegalAllowanceField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Allowance Rate: المخصصات / المخصصات القانونية / نسبة
        if (clean.Contains("مخصص") || clean.Contains("قانون") || clean.Contains("نسبة"))
            return "AllowanceRate";

        // 3. Employee Name
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean == "اسم" || clean == "الاسم" || clean.Contains("كامل")))
            return "EmployeeName";

        // 4. Formation
        if (clean.Contains("تشكيل") || clean.Contains("دائر") || clean.Contains("قسم"))
            return "Formation";

        // 5. Job Title
        if (clean.Contains("عنوان") || clean.Contains("مسمي") || clean.Contains("مسمى"))
            return "JobTitle";

        // 6. Grade
        if (clean.Contains("درج"))
            return "Grade";

        // 7. Stage
        if (clean.Contains("مرحل"))
            return "Stage";

        return null;
    }

    public static string? MatchNonPracticeAllowanceField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID: رقم الموظف / الرقم الوظيفي
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Allowance Rate: عدم مزاولة مهنة / عدم ممارسة / مخصصات / النسبة
        if (clean.Contains("مزاول") || clean.Contains("ممارس") || clean.Contains("مخصص") || clean.Contains("نسبة") || clean.Contains("rate"))
            return "AllowanceRate";

        // 3. Employee Name
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean == "اسم" || clean == "الاسم" || clean.Contains("كامل")))
            return "EmployeeName";

        // 4. Formation
        if (clean.Contains("تشكيل") || clean.Contains("دائر") || clean.Contains("قسم"))
            return "Formation";

        // 5. Job Title
        if (clean.Contains("عنوان") || clean.Contains("مسمي") || clean.Contains("مسمى"))
            return "JobTitle";

        // 6. Grade
        if (clean.Contains("درج"))
            return "Grade";

        // 7. Stage
        if (clean.Contains("مرحل"))
            return "Stage";

        return null;
    }

    public static string? MatchCertificateAllowanceField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Certificate: الشهادة / المؤهل العلمي / التحصيل
        if (clean.Contains("شهاد") || clean.Contains("تحصيل") || clean.Contains("مؤهل") || clean.Contains("موهل"))
            return "Certificate";

        // 3. Allowance Rate: المخصصات / النسبة
        if (clean.Contains("مخصص") || clean.Contains("نسبة") || clean.Contains("مبلغ"))
            return "AllowanceRate";

        // 4. Employee Name
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean == "اسم" || clean == "الاسم" || clean.Contains("كامل")))
            return "EmployeeName";

        // 5. Formation
        if (clean.Contains("تشكيل") || clean.Contains("دائر") || clean.Contains("قسم"))
            return "Formation";

        // 6. Specialization
        if (clean.Contains("اختصاص") || clean.Contains("تخصص"))
            return "Specialization";

        return null;
    }

    public static readonly Dictionary<string, decimal> DefaultCertificateRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["دكتوراه"] = 150m,
        ["دبلوم عالي/ماجستير"] = 125m,
        ["ماجستير"] = 125m,
        ["دكتوراه 2"] = 100m,
        ["ماجستير 2"] = 75m,
        ["دبلوم عالي"] = 55m,
        ["بكلوريوس"] = 45m,
        ["بكالوريوس"] = 45m,
        ["دبلوم"] = 35m,
        ["دبلوم فني"] = 35m,
        ["ابتدائية"] = 25m,
        ["أعدادية تجارة"] = 25m,
        ["إعدادية تجارة"] = 25m,
        ["أعدادية صناعة"] = 25m,
        ["إعدادية صناعة"] = 25m,
        ["اعدادية علمي وادبي"] = 25m,
        ["إعدادية علمي وأدبي"] = 25m,
        ["إعدادية"] = 25m,
        ["اعدادية"] = 25m,
        ["متوسطة"] = 15m,
        ["يقرأ ويكتب"] = 15m,
        ["امي"] = 15m,
        ["أمي"] = 15m
    };

    public static decimal ResolveCertificatePercentage(string? certName, Dictionary<string, decimal>? customRates = null)
    {
        if (string.IsNullOrWhiteSpace(certName)) return 0m;

        var rates = customRates ?? DefaultCertificateRates;
        var clean = NormalizeHeader(certName);

        // Direct key lookup
        foreach (var (k, v) in rates)
        {
            if (clean == NormalizeHeader(k)) return v;
        }

        // Fuzzy matches according to user's table
        if (clean.Contains("دكتورا"))
        {
            if (clean.Contains("2") || clean.Contains("ثاني") || clean.Contains("٢")) return rates.GetValueOrDefault("دكتوراه 2", 100m);
            return rates.GetValueOrDefault("دكتوراه", 150m);
        }

        if (clean.Contains("ماجستير"))
        {
            if (clean.Contains("دبلوم")) return rates.GetValueOrDefault("دبلوم عالي/ماجستير", 125m);
            if (clean.Contains("2") || clean.Contains("ثاني") || clean.Contains("٢")) return rates.GetValueOrDefault("ماجستير 2", 75m);
            return rates.GetValueOrDefault("ماجستير", 125m);
        }

        if (clean.Contains("دبلوم") && (clean.Contains("عالي") || clean.Contains("عالى")))
        {
            return rates.GetValueOrDefault("دبلوم عالي", 55m);
        }

        if (clean.Contains("بكلوريوس") || clean.Contains("بكالوريوس"))
        {
            return rates.GetValueOrDefault("بكلوريوس", 45m);
        }

        if (clean.Contains("دبلوم") && clean.Contains("فني"))
        {
            return rates.GetValueOrDefault("دبلوم فني", 35m);
        }

        if (clean.Contains("دبلوم"))
        {
            return rates.GetValueOrDefault("دبلوم", 35m);
        }

        if (clean.Contains("تجار"))
        {
            return rates.GetValueOrDefault("أعدادية تجارة", 25m);
        }

        if (clean.Contains("صناع"))
        {
            return rates.GetValueOrDefault("أعدادية صناعة", 25m);
        }

        if (clean.Contains("اعدادي") || clean.Contains("علمي") || clean.Contains("ادبي"))
        {
            return rates.GetValueOrDefault("اعدادية علمي وادبي", 25m);
        }

        if (clean.Contains("ابتدائي"))
        {
            return rates.GetValueOrDefault("ابتدائية", 25m);
        }

        if (clean.Contains("متوسط"))
        {
            return rates.GetValueOrDefault("متوسطة", 15m);
        }

        if (clean.Contains("يقرا") || clean.Contains("يقرأ") || clean.Contains("امي") || clean.Contains("أمي"))
        {
            return rates.GetValueOrDefault("يقرأ ويكتب", 15m);
        }

        return 0m;
    }

    public static string? MatchLeaveBalanceField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID: رقم الموظف / الرقم الوظيفي
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Leave Balance: يجب أن يكون عامود "الرصيد" حصراً
        var compact = clean.Replace(" ", "").Replace("_", "").Replace("-", "").Replace("\"", "").Replace("'", "").Replace("(", "").Replace(")", "").Replace("/", "").Replace(":", "");
        if (compact is "الرصيد" or "رصيد")
            return "Balance";

        // استبعاد أي أعمدة أخرى صراحة (رصيد سابق، مستحق، مستنفذ، أيام، تاريخ...)
        if (clean.Contains("سابق") || clean.Contains("مستحق") || clean.Contains("استحقاق") ||
            clean.Contains("مستنفذ") || clean.Contains("ماخوذ") || clean.Contains("مأخوذ") ||
            clean.Contains("تمتع") || clean.Contains("متمتع") || clean.Contains("مدور") ||
            clean.Contains("منقول") || clean.Contains("ايام") || clean.Contains("يوم") ||
            clean.Contains("عدد") || clean.Contains("تاريخ") || clean.Contains("كتاب") ||
            clean.Contains("امر"))
        {
            return null;
        }

        if (compact is "الرصيدالمتبقي" or "رصيدالمتبقي" or "صافيالرصيد" or "رصيدصافي" or "رصيدالاجازات" or "رصيدالاجازاتالمتبقي")
            return "Balance";

        // 3. Employee Name: (للمعاينة فقط)
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean.Contains("كامل") || clean == "الاسم" || clean == "اسم"))
            return "EmployeeName";

        return null;
    }

    public static string? MatchPenaltyField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID: رقم الموظف / الرقم الوظيفي
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Penalty Date: تاريخ نيل العقوبة / تاريخ العقوبة / تاريخ الصدور
        if (clean.Contains("تاريخ") && (clean.Contains("عقوب") || clean.Contains("نيل") || clean.Contains("صدور")))
            return "PenaltyDate";

        // 3. Order Number: رقم الكتاب / رقم الامر / رقم القرار
        if ((clean.Contains("رقم") && (clean.Contains("كتاب") || clean.Contains("امر") || clean.Contains("قرار"))) || clean == "رقم الكتاب" || clean == "رقم الامر")
            return "OrderNumber";

        // 4. Notes: نوع العقوبة / الملاحظات (كما طلب المستخدم: "ايضا نوع العقوبة لكن يكون اسمه بالنظام الملاحضات")
        if (clean.Contains("نوع العقوب") || clean.Contains("ملاحظ") || clean == "الملاحظات" || clean == "نوع العقوبة")
            return "Notes";

        // 5. Penalty Name: العقوبة (اسم العقوبة)
        if (clean == "العقوبة" || clean == "عقوبة" || (clean.Contains("عقوب") && !clean.Contains("نوع") && !clean.Contains("تاريخ")))
            return "PenaltyName";

        // 6. Directorate: أسم المديرية / التشكيل / الدائرة
        if (clean.Contains("مديري") || clean.Contains("اسم المديرية") || clean.Contains("تشكيل") || clean.Contains("دائر"))
            return "Directorate";

        // 7. Job Title: العنوان الوظيفي / المسمى الوظيفي
        if ((clean.Contains("عنوان") && clean.Contains("وظي")) || clean == "العنوان الوظيفي" || clean.Contains("مسمي"))
            return "JobTitle";

        // 8. Employee Name: أسم الموظف / الاسم الكامل
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean == "اسم" || clean == "الاسم" || clean.Contains("كامل")) && !clean.Contains("مديري"))
            return "EmployeeName";

        return null;
    }

    public static string? MatchFinancialAccountField(string raw)
    {
        var clean = NormalizeHeader(raw);
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean == "اسم" || clean == "الاسم" || clean.Contains("كامل")) && !clean.Contains("مديري")) return "EmployeeName";

        if (clean.Contains("رصيد") || clean == "الرصيد" || clean.Contains("الحساب") || clean.Contains("رصيد الموظف"))
            return "Balance";

        if (clean.Contains("قرض") || clean.Contains("متبقي القرض") || clean.Contains("سلفه عقاريه") || clean.Contains("القرض"))
            return "LoanRemaining";

        if (clean.Contains("سلف") || clean.Contains("سلفة") || clean.Contains("متبقي السلفة") || clean.Contains("السلفه") || clean.Contains("السلفة"))
            return "AdvanceRemaining";

        if (clean.Contains("ملاحظ") || clean.Contains("بيان") || clean.Contains("تفاصيل"))
            return "Notes";

        return null;
    }

    public static decimal? ExtractFixedAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else if (char.IsDigit(ch) || ch == '.') sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }
        return null;
    }

    public static string? MatchCraftAndSocialAllowanceField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID: رقم الموظف / الرقم الوظيفي
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Craft Allowance (نسبة)
        if (clean.Contains("حرف"))
            return "Craft";

        // 3. Marriage Allowance (مقطوعة)
        if (clean.Contains("زوج"))
            return "Marriage";

        // 4. Children Allowance (مقطوعة)
        if (clean.Contains("طفل") || clean.Contains("اطفال") || clean.Contains("ولد") || clean.Contains("اولاد"))
            return "Children";

        // 5. Hazard Allowance (مقطوعة)
        if (clean.Contains("خطر") || clean.Contains("خطور"))
            return "Hazard";

        // 6. Employee Name: أسم الموظف / الاسم الكامل
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean == "اسم" || clean == "الاسم" || clean.Contains("كامل")))
            return "EmployeeName";

        return null;
    }

    public static string? MatchMaritalStatusField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID: رقم الموظف / الرقم الوظيفي
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Marital Status: الحالة الاجتماعية / الحالة العائلية / الوضع العائلي / الحالة
        if (clean.Contains("اجتماع") || clean.Contains("عائلي") || clean.Contains("مدني") ||
            clean.Contains("marital") || clean.Contains("social") || clean == "الحالة" || clean == "حالة" ||
            clean.Contains("زوج") || clean.Contains("عزوب"))
            return "MaritalStatus";

        // 3. Employee Name: أسم الموظف / الاسم الكامل
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean == "اسم" || clean == "الاسم" || clean.Contains("كامل")))
            return "EmployeeName";

        return null;
    }

    public static string? MatchMartyrField(string raw)
    {
        var clean = NormalizeHeader(raw);

        // 1. Employee ID: الرقم الوظيفي / رقم الموظف
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Martyr Name: اسم الشهيد / الشهيد
        if (clean.Contains("شهيد") && (clean.Contains("اسم") || clean == "شهيد" || clean == "الشهيد"))
            return "MartyrName";
        if (clean == "اسم الشهيد" || clean == "اسم_الشهيد" || clean.Contains("martyrname") || clean == "martyr")
            return "MartyrName";

        // 3. Martyrdom Type: نوع الشهادة / فئة الشهادة / صنف الشهادة
        if (clean.Contains("نوع") && (clean.Contains("شها") || clean.Contains("شهيد")))
            return "MartyrdomType";
        if (clean.Contains("فئة") || clean.Contains("صنف") || clean == "نوع الشهادة" || clean.Contains("martyrdomtype"))
            return "MartyrdomType";

        // 4. Kinship: الصلة / صلة القرابة / درجة القرابة / القرابة
        if (clean.Contains("صل") || clean.Contains("قراب") || clean.Contains("درج") || clean.Contains("kinship") || clean.Contains("relation"))
            return "Kinship";

        // 5. Decision Number: رقم القرار / القرار / كتاب الشهادة / قرار الشهادة
        if (clean.Contains("قرار") || clean.Contains("decision"))
            return "DecisionNumber";

        // 6. Notes: ملاحظات / السند
        if (clean.Contains("ملاحظ") || clean.Contains("notes"))
            return "Notes";

        // 7. Employee Name (optional preview)
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean == "اسم" || clean == "الاسم" || clean.Contains("كامل")))
            return "EmployeeName";

        return null;
    }

    public static string NormalizeJobHeader(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            // Arabic tashkeel unicode range: \u064B - \u065F, \u0670
            if (ch >= '\u064B' && ch <= '\u065F') continue;
            if (ch == '\u0670') continue;
            // Zero-width characters & BOM
            if (ch == '\u200B' || ch == '\u200C' || ch == '\u200D' || ch == '\uFEFF') continue;
            // Replace newlines/tabs/non-breaking space with regular space
            if (ch == '\r' || ch == '\n' || ch == '\t' || ch == '\u00A0')
            {
                sb.Append(' ');
                continue;
            }
            sb.Append(ch);
        }

        var cleaned = sb.ToString()
            .Replace("ـ", "") // tatweel
            .Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا").Replace("ٱ", "ا")
            .Replace("ة", "ه")
            .Replace("ى", "ي")
            .Replace("ئ", "ي").Replace("ؤ", "و")
            .Trim()
            .ToLowerInvariant();

        return Regex.Replace(cleaned, @"\s+", " ");
    }

    public static string? MatchJobTitleField(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var clean = NormalizeJobHeader(raw);

        // 1. Code: الرمز / رمز / رمز العنوان / كود / كود العنوان / الكود / ت / تسلسل / code / no
        if (clean == "الرمز" || clean == "رمز" || clean.Contains("رمز") || clean.Contains("كود") || clean == "ت" || clean == "تسلسل" || clean == "id" || clean == "code" || clean == "no")
            return "Code";

        // 2. JobFamily: العائلة الوظيفية / العائلة / عائلة / العوائل / family
        if (clean.Contains("عائل") || clean.Contains("عايل") || clean.Contains("عوائل") || clean.Contains("عوايل") || clean.Contains("family"))
            return "JobFamily";

        // 3. Grade: الدرجة الوظيفية / الدرجة / درجة / مرتبة / grade
        if (clean.Contains("درج") || clean.Contains("مرتب") || clean.Contains("grade"))
            return "Grade";

        // 4. JobTitle: العنوان الوظيفي / العناون الوظيفي / العنوان / عناوين / الوظيفة / المسمى / title / job / position
        if (clean.Contains("عنوان") || clean.Contains("عناون") || clean.Contains("عناوين") ||
            clean.Contains("مسم") || clean.Contains("منصب") ||
            clean.Contains("وظيف") || clean.Contains("وظايف") ||
            clean.Contains("title") || clean.Contains("position") ||
            clean == "job" || clean.Contains("job title") || clean.Contains("jobtitle"))
            return "JobTitle";

        return null;
    }

    public static Grade? FindGradeByCodeOrName(IEnumerable<Grade> grades, string? rawGrade)
    {
        if (string.IsNullOrWhiteSpace(rawGrade)) return null;

        var clean = rawGrade.Trim();

        // Convert Eastern Arabic numerals (٠-٩) to standard ASCII digits (0-9)
        var sb = new StringBuilder();
        foreach (var ch in clean)
        {
            if (ch >= '٠' && ch <= '٩')
                sb.Append((char)('0' + (ch - '٠')));
            else
                sb.Append(ch);
        }
        var normalized = sb.ToString().Trim();

        // 1. Direct match on Grade.Code (High Priority: User rule is to match Excel Grade with Grade.Code)
        var matchByCode = grades.FirstOrDefault(g =>
            !string.IsNullOrWhiteSpace(g.Code) &&
            string.Equals(g.Code.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
        if (matchByCode != null) return matchByCode;

        // 2. Numeric parsing on Grade.Code (handles "01" -> "1", "1.0" -> "1", "الدرجة 1" -> "1")
        var numMatch = Regex.Match(normalized, @"\b(\d+)\b");
        if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out var parsedNum))
        {
            var parsedNumStr = parsedNum.ToString();
            var matchByParsedCode = grades.FirstOrDefault(g =>
                !string.IsNullOrWhiteSpace(g.Code) &&
                (g.Code.Trim() == parsedNumStr || (int.TryParse(g.Code.Trim(), out var gc) && gc == parsedNum)));
            if (matchByParsedCode != null) return matchByParsedCode;
        }

        // 3. Normalized string matching on Grade.Code (handles special grades: "العليا أ", "عليا أ", "الخاصة أ", "خاصة أ")
        var normalizedArabic = NormalizeJobHeader(clean);
        foreach (var g in grades)
        {
            if (string.IsNullOrWhiteSpace(g.Code)) continue;
            var normCode = NormalizeJobHeader(g.Code);
            if (normCode == normalizedArabic ||
                (normCode.Length >= 4 && normalizedArabic.Contains(normCode)) ||
                (normalizedArabic.Length >= 4 && normCode.Contains(normalizedArabic)))
            {
                return g;
            }
        }

        // 4. Exact match on Grade.Name
        var matchByName = grades.FirstOrDefault(g =>
            !string.IsNullOrWhiteSpace(g.Name) &&
            string.Equals(g.Name.Trim(), clean, StringComparison.OrdinalIgnoreCase));
        if (matchByName != null) return matchByName;

        // 5. Textual Arabic word fallback (الاولى -> Code "1", الثانية -> Code "2", etc.)
        if (normalizedArabic.Contains("عليا ا")) return grades.FirstOrDefault(g => g.Code == "العليا أ" || (g.Name != null && g.Name.Contains("العليا أ")));
        if (normalizedArabic.Contains("عليا ب")) return grades.FirstOrDefault(g => g.Code == "العليا ب" || (g.Name != null && g.Name.Contains("العليا ب")));
        if (normalizedArabic.Contains("خاصه ا") || normalizedArabic.Contains("خاصة أ")) return grades.FirstOrDefault(g => g.Code == "الخاصة أ" || (g.Name != null && g.Name.Contains("الخاصة أ")));
        if (normalizedArabic.Contains("خاصه ب") || normalizedArabic.Contains("خاصة ب")) return grades.FirstOrDefault(g => g.Code == "الخاصة ب" || (g.Name != null && g.Name.Contains("الخاصة ب")));

        if (normalizedArabic.Contains("اولي") || normalizedArabic.Contains("اولى")) return grades.FirstOrDefault(g => g.Code == "1");
        if (normalizedArabic.Contains("ثانيه") || normalizedArabic.Contains("ثانية")) return grades.FirstOrDefault(g => g.Code == "2");
        if (normalizedArabic.Contains("ثالثه") || normalizedArabic.Contains("ثالثة")) return grades.FirstOrDefault(g => g.Code == "3");
        if (normalizedArabic.Contains("رابعه") || normalizedArabic.Contains("رابعة")) return grades.FirstOrDefault(g => g.Code == "4");
        if (normalizedArabic.Contains("خامسه") || normalizedArabic.Contains("خامسة")) return grades.FirstOrDefault(g => g.Code == "5");
        if (normalizedArabic.Contains("سادسه") || normalizedArabic.Contains("سادسة")) return grades.FirstOrDefault(g => g.Code == "6");
        if (normalizedArabic.Contains("سابعه") || normalizedArabic.Contains("سابعة")) return grades.FirstOrDefault(g => g.Code == "7");
        if (normalizedArabic.Contains("ثامنه") || normalizedArabic.Contains("ثامنة")) return grades.FirstOrDefault(g => g.Code == "8");
        if (normalizedArabic.Contains("تاسعه") || normalizedArabic.Contains("تاسعة")) return grades.FirstOrDefault(g => g.Code == "9");
        if (normalizedArabic.Contains("عاشره") || normalizedArabic.Contains("عاشرة")) return grades.FirstOrDefault(g => g.Code == "10");

        return null;
    }

    public static string? MatchLastAllowanceDateField(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var clean = NormalizeHeader(raw);

        // 1. Employee ID: الرقم الوظيفي / رقم الموظف / رقم الحاسبة / كود الموظف
        if (IsEmployeeIdHeader(raw)) return "EmployeeID";

        // 2. Current Year Allowance: تاريخ علاوة السنة الحالية / علاوة السنة الحالية / علاوة حالية
        if (clean.Contains("حالي") || (clean.Contains("علاو") && (clean.Contains("سنه حاليه") || clean.Contains("سنة حالية"))))
            return "CurrentYearAllowanceDate";
        if (clean.Contains("currentyear") || clean.Contains("current_year"))
            return "CurrentYearAllowanceDate";

        // 3. Base Allowance: تاريخ العلاوة الاساس / علاوة الاساس / تاريخ الاساس
        if (clean.Contains("اساس") || clean.Contains("أساس") || clean.Contains("base"))
            return "BaseAllowanceDate";

        // 4. Last Allowance Date: تاريخ آخر علاوة سنوية / تاريخ اخر علاوة / العلاوة السنوية / آخر علاوة
        if (clean.Contains("علاو") && (clean.Contains("تاريخ") || clean.Contains("اخر") || clean.Contains("سنوي") || clean == "علاوة" || clean == "العلاوة" || clean == "علاوه" || clean == "العلاوه"))
            return "LastAllowanceDate";
        if (clean.Contains("allowance") || clean.Contains("increment"))
            return "LastAllowanceDate";

        // 5. Employee Name (optional preview)
        if (clean.Contains("اسم") && (clean.Contains("موظف") || clean == "اسم" || clean == "الاسم" || clean.Contains("كامل")) && !clean.Contains("مديري"))
            return "EmployeeName";

        return null;
    }

    private static bool TryParseAllowanceDate(string? rawValue, out DateOnly? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(rawValue)) return false;

        var sb = new StringBuilder(rawValue.Length);
        foreach (var ch in rawValue.Trim())
        {
            if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim();

        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "yyyy/MM/dd", "d-M-yyyy", "dd-MM-yyyy", "M/d/yyyy", "MM/dd/yyyy", "yyyy.MM.dd", "dd.MM.yyyy" };
        if (DateOnly.TryParseExact(cleaned, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            value = parsed;
            return true;
        }

        if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            value = DateOnly.FromDateTime(dt);
            return true;
        }

        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate) && oaDate > 20000 && oaDate < 70000)
        {
            try
            {
                value = DateOnly.FromDateTime(DateTime.FromOADate(oaDate));
                return true;
            }
            catch { }
        }

        return false;
    }

    private static string? NormalizeDigits(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text?.Trim();
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.Trim())
        {
            if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else sb.Append(ch);
        }
        return sb.ToString().Trim();
    }
}

