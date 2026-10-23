using System.Text;
using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Data.Allowances;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Salary;

public sealed class FormationPayrollService(IDbContextFactory<ApplicationDbContext> dbFactory) : IFormationPayrollService
{
    private static readonly string[] ArabicMonths =
    [
        "كانون الثاني (يناير)",
        "شباط (فبراير)",
        "آذار (مارس)",
        "نيسان (أبريل)",
        "أيار (مايو)",
        "حزيران (يونيو)",
        "تموز (يوليو)",
        "آب (أغسطس)",
        "أيلول (سبتمبر)",
        "تشرين الأول (أكتوبر)",
        "تشرين الثاني (نوفمبر)",
        "كانون الأول (ديسمبر)"
    ];

    public async Task<List<Formation>> GetAvailableFormationsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Formations
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<MonthlyPayrollReportDto> GetMonthlyPayrollReportAsync(
        PayrollFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Normalize month/year
        if (filter.Year < 2000 || filter.Year > 2100) filter.Year = DateTime.Today.Year;
        if (filter.Month < 1 || filter.Month > 12) filter.Month = DateTime.Today.Month;

        var monthStart = new DateOnly(filter.Year, filter.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        // 1. Load active employees (not archived)
        // 1. Load ALL active employees (not archived) across the institution
        var allEmployeesQuery = db.Employees
            .AsNoTracking()
            .Where(e => !e.IsArchived)
            .Include(e => e.Formation)
            .Include(e => e.JobTitle)
            .Include(e => e.PositionTitle)
            .Include(e => e.Grade)
            .Include(e => e.Stage);

        var allEmployees = await allEmployeesQuery.ToListAsync(cancellationToken);

        if (allEmployees.Count == 0)
        {
            return new MonthlyPayrollReportDto
            {
                Year = filter.Year,
                Month = filter.Month,
                MonthNameAr = ArabicMonths[filter.Month - 1],
                SelectedFormationId = filter.FormationId,
                SelectedFormationName = filter.FormationId.HasValue 
                    ? (await db.Formations.Where(f => f.Id == filter.FormationId.Value).Select(f => f.Name).FirstOrDefaultAsync(cancellationToken))
                    : null
            };
        }

        var allEmployeeIds = allEmployees.Select(e => e.Id).ToList();

        // 2. Load all SalaryScales for scale fallback
        var salaryScales = await db.SalaryScales
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var scaleDict = salaryScales.ToDictionary(s => (s.GradeId, s.StageId));

        // 3. Load active allowance periods covering this month for all employees
        var allowancePeriods = await db.EmployeeAllowancePeriods
            .AsNoTracking()
            .Where(p => allEmployeeIds.Contains(p.EmployeeId) &&
                        p.StartDate <= monthEnd &&
                        (!p.EndDate.HasValue || p.EndDate.Value >= monthStart))
            .Include(p => p.Items)
            .OrderByDescending(p => p.IsCurrent)
            .ThenByDescending(p => p.StartDate)
            .ThenByDescending(p => p.Id)
            .ToListAsync(cancellationToken);

        // Map to EmployeeId -> AllowancePeriod (pick the most relevant for each employee)
        var empPeriodDict = allowancePeriods
            .GroupBy(p => p.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First());

        // 4. Build EmployeePayrollItemDto list for all employees
        var allPayrollItems = new List<EmployeePayrollItemDto>(allEmployees.Count);

        foreach (var emp in allEmployees)
        {
            var item = new EmployeePayrollItemDto
            {
                EmployeeId = emp.Id,
                EmployeeNumber = emp.EmployeeID ?? string.Empty,
                FullName = emp.Name,
                JobTitle = emp.JobTitle?.Name ?? emp.PositionTitle?.Name ?? "غير محدد",
                PositionTitle = emp.PositionTitle?.Name,
                FormationId = emp.FormationId,
                FormationName = emp.Formation?.Name ?? "تشكيل غير محدد",
                GradeName = emp.Grade?.Name,
                StageName = emp.Stage?.Name,
            };

            if (empPeriodDict.TryGetValue(emp.Id, out var period))
            {
                item.HasActiveAllowancePackage = true;
                item.PackageStartDate = period.StartDate;
                item.OrderNumber = period.OrderNumber;
                item.Notes = period.Notes;
                item.BasicSalary = period.BasicSalary;
                item.TotalPercentageRate = period.TotalPercentageRate;
                item.TotalPercentageAmount = period.TotalPercentageAmount;
                item.TotalFixedAmount = period.TotalFixedAmount;
                item.GrossSalary = period.GrossSalary > 0 
                    ? period.GrossSalary 
                    : (period.BasicSalary + period.TotalPercentageAmount + period.TotalFixedAmount);

                if (period.Items != null)
                {
                    item.AllowanceItems = period.Items.Select(ai => new AllowanceCalculatedItemDto
                    {
                        Name = ai.Name,
                        CalculationType = ai.CalculationType,
                        Percentage = ai.Percentage,
                        FixedAmount = ai.FixedAmount,
                        CalculatedAmount = ai.CalculatedAmount,
                        Notes = ai.Notes
                    }).ToList();
                }
            }
            else
            {
                // Fallback to salary scale
                item.HasActiveAllowancePackage = false;
                decimal baseSal = 0m;

                if (emp.GradeId.HasValue && emp.StageId.HasValue &&
                    scaleDict.TryGetValue((emp.GradeId.Value, emp.StageId.Value), out var scale))
                {
                    baseSal = scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
                    item.GradeCode = scale.GradeCode;
                    item.StageCode = scale.StageCode;
                }

                item.BasicSalary = baseSal;
                item.TotalPercentageRate = 0m;
                item.TotalPercentageAmount = 0m;
                item.TotalFixedAmount = 0m;
                item.GrossSalary = baseSal;
            }

            allPayrollItems.Add(item);
        }

        // 5. Total Institution Metrics
        int totalOrgEmployees = allPayrollItems.Count;
        decimal totalOrgGross = allPayrollItems.Sum(x => x.GrossSalary);
        decimal totalOrgBasic = allPayrollItems.Sum(x => x.BasicSalary);
        decimal totalOrgPercentAllowances = allPayrollItems.Sum(x => x.TotalPercentageAmount);
        decimal totalOrgFixedAllowances = allPayrollItems.Sum(x => x.TotalFixedAmount);
        decimal totalOrgAverageSalary = totalOrgEmployees > 0 ? (totalOrgGross / totalOrgEmployees) : 0m;

        // 6. Build Formation Summaries with Headcount Ratio, Spending Ratio, and Spending Intensity Index
        var formationSummaries = allPayrollItems
            .GroupBy(i => new { Id = i.FormationId ?? 0, Name = i.FormationName })
            .Select(g =>
            {
                var empCount = g.Count();
                var gross = g.Sum(x => x.GrossSalary);
                var basic = g.Sum(x => x.BasicSalary);
                var percentAllow = g.Sum(x => x.TotalPercentageAmount);
                var fixedAllow = g.Sum(x => x.TotalFixedAmount);
                var avgSalary = empCount > 0 ? Math.Round(gross / empCount, 0) : 0m;

                var empPct = totalOrgEmployees > 0 ? Math.Round(((decimal)empCount / totalOrgEmployees) * 100m, 2) : 0m;
                var spendPct = totalOrgGross > 0 ? Math.Round((gross / totalOrgGross) * 100m, 2) : 0m;

                // Spending Intensity Ratio: How much the formation spends compared to its proportion of employees
                // Equivalently: (Formation Average Salary) / (Institution Average Salary)
                var intensityRatio = totalOrgAverageSalary > 0 
                    ? Math.Round(avgSalary / totalOrgAverageSalary, 2) 
                    : 1.0m;

                return new FormationPayrollSummaryDto
                {
                    FormationId = g.Key.Id,
                    FormationName = g.Key.Name,
                    EmployeeCount = empCount,
                    EmployeePercentage = empPct,
                    EmployeesWithPackageCount = g.Count(x => x.HasActiveAllowancePackage),
                    TotalBasicSalary = basic,
                    TotalPercentageAllowances = percentAllow,
                    TotalFixedAllowances = fixedAllow,
                    TotalGrossSalary = gross,
                    SpendingPercentage = spendPct,
                    SpendingIntensityRatio = intensityRatio
                };
            })
            .AsEnumerable();

        // 7. Sort Formations according to FormationSortBy
        formationSummaries = (filter.FormationSortBy ?? "intensity_desc") switch
        {
            "intensity_asc" => formationSummaries.OrderBy(f => f.SpendingIntensityRatio).ThenBy(f => f.TotalGrossSalary),
            "employees_desc" => formationSummaries.OrderByDescending(f => f.EmployeeCount).ThenByDescending(f => f.TotalGrossSalary),
            "employees_asc" => formationSummaries.OrderBy(f => f.EmployeeCount).ThenBy(f => f.TotalGrossSalary),
            "gross_desc" => formationSummaries.OrderByDescending(f => f.TotalGrossSalary),
            "gross_asc" => formationSummaries.OrderBy(f => f.TotalGrossSalary),
            "average_desc" => formationSummaries.OrderByDescending(f => f.AverageGrossSalary),
            "allowance_ratio_desc" => formationSummaries.OrderByDescending(f => f.AllowanceToBasicRatio),
            "name_asc" => formationSummaries.OrderBy(f => f.FormationName),
            _ => formationSummaries.OrderByDescending(f => f.SpendingIntensityRatio).ThenByDescending(f => f.TotalGrossSalary) // Default: highest spending relative to staff
        };

        var formationSummariesList = formationSummaries.ToList();

        // 8. Overall Report Header
        var report = new MonthlyPayrollReportDto
        {
            Year = filter.Year,
            Month = filter.Month,
            MonthNameAr = ArabicMonths[filter.Month - 1],
            SelectedFormationId = filter.FormationId,
            SelectedFormationName = filter.FormationId.HasValue 
                ? formationSummariesList.FirstOrDefault(f => f.FormationId == filter.FormationId.Value)?.FormationName 
                : null,
            TotalEmployeesCount = totalOrgEmployees,
            EmployeesWithPackageCount = allPayrollItems.Count(x => x.HasActiveAllowancePackage),
            EmployeesOnScaleOnlyCount = allPayrollItems.Count(x => !x.HasActiveAllowancePackage),
            TotalBasicSalary = totalOrgBasic,
            TotalPercentageAllowances = totalOrgPercentAllowances,
            TotalFixedAllowances = totalOrgFixedAllowances,
            TotalGrossSalary = totalOrgGross,
            Formations = formationSummariesList
        };

        // 9. Filter Employees list for selected formation and filters
        var targetEmployees = allPayrollItems.AsEnumerable();

        if (filter.FormationId.HasValue && filter.FormationId.Value > 0)
        {
            targetEmployees = targetEmployees.Where(e => e.FormationId == filter.FormationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim();
            targetEmployees = targetEmployees.Where(e =>
                e.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.EmployeeNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (e.JobTitle != null && e.JobTitle.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                e.FormationName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.StatusFilter == "withPackage")
        {
            targetEmployees = targetEmployees.Where(e => e.HasActiveAllowancePackage);
        }
        else if (filter.StatusFilter == "scaleOnly")
        {
            targetEmployees = targetEmployees.Where(e => !e.HasActiveAllowancePackage);
        }

        // Apply Sorting on Employees list
        targetEmployees = filter.SortBy switch
        {
            "gross_asc" => targetEmployees.OrderBy(e => e.GrossSalary),
            "basic_desc" => targetEmployees.OrderByDescending(e => e.BasicSalary),
            "basic_asc" => targetEmployees.OrderBy(e => e.BasicSalary),
            "name" => targetEmployees.OrderBy(e => e.FullName),
            "id" => targetEmployees.OrderBy(e => e.EmployeeNumber),
            _ => targetEmployees.OrderByDescending(e => e.GrossSalary)
        };

        report.Employees = targetEmployees.ToList();

        return report;
    }

    public byte[] GeneratePayrollCsv(MonthlyPayrollReportDto report)
    {
        var sb = new StringBuilder();

        // UTF-8 BOM for Arabic in Excel
        sb.Append('\uFEFF');

        // Header info
        sb.AppendLine($"\"تقرير احتساب الرواتب والمخصصات الشهرية حسب التشكيل ومؤشر كثافة الإنفاق\"");
        sb.AppendLine($"\"فترة الاحتساب:\",\"{report.MonthNameAr} {report.Year}\"");
        sb.AppendLine($"\"التشكيل المختار:\",\"{(string.IsNullOrWhiteSpace(report.SelectedFormationName) ? "كافة التشكيلات" : report.SelectedFormationName)}\"");
        sb.AppendLine($"\"المعدل العام لراتب الموظف في المؤسسة:\",\"{report.AverageGrossSalary:N0} د.ع\"");
        sb.AppendLine($"\"تاريخ ووقت التصدير:\",\"{DateTime.Now:yyyy/MM/dd HH:mm}\"");
        sb.AppendLine();

        // Section 1: Formations Analysis & Spending Intensity Table
        sb.AppendLine($"\"--- أولاً: جدول تحليل مقارنة التشكيلات الإدارية وكثافة الإنفاق مقارنة بنسبة الموظفين ---\"");
        sb.AppendLine("\"ت\",\"التشكيل الإداري\",\"عدد الموظفين\",\"نسبة الكادر من الإجمالي %\",\"إجمالي الرواتب الإسمية (د.ع)\",\"إجمالي المخصصات (د.ع)\",\"إجمالي فاتورة الرواتب (د.ع)\",\"نسبة الإنفاق من الميزانية %\",\"متوسط راتب الموظف (د.ع)\",\"مؤشر كثافة الإنفاق (الإنفاق/العدد)\",\"تقييم الإنفاق النسبي\"");

        int fIndex = 1;
        foreach (var f in report.Formations)
        {
            string assessment = f.SpendingIntensityRatio switch
            {
                >= 1.25m => "إنفاق مرتفع جداً مقارنة بالعدد",
                >= 1.05m => "أعلى من المعدل العام",
                >= 0.95m => "متوازن مع النسبة العددية",
                _ => "أقل من المعدل العام مقارنة بالعدد"
            };

            sb.AppendLine($"\"{fIndex++}\",\"{EscapeCsv(f.FormationName)}\",\"{f.EmployeeCount}\",\"{f.EmployeePercentage:F2}%\",\"{f.TotalBasicSalary:N0}\",\"{f.TotalAllowances:N0}\",\"{f.TotalGrossSalary:N0}\",\"{f.SpendingPercentage:F2}%\",\"{f.AverageGrossSalary:N0}\",\"{f.SpendingIntensityRatio:F2}x\",\"{assessment}\"");
        }
        sb.AppendLine();

        // Section 2: Detailed Employee Records
        sb.AppendLine($"\"--- ثانياً: كشف رواتب الموظفين التفصيلي ---\"");
        sb.AppendLine("\"ت\",\"الرقم الوظيفي\",\"اسم الموظف\",\"التشكيل الإداري\",\"العنوان الوظيفي\",\"الدرجة\",\"المرحلة\",\"الراتب الإسمي (د.ع)\",\"مجموع النسب %\",\"مخصصات نسبية (د.ع)\",\"مخصصات مقطوعة (د.ع)\",\"إجمالي المخصصات (د.ع)\",\"إجمالي الراتب المستحق (د.ع)\",\"حالة الحزمة\",\"رقم الأمر الإداري\"");

        int index = 1;
        foreach (var emp in report.Employees)
        {
            var status = emp.HasActiveAllowancePackage ? "حزمة مخصصات معتمدة" : "راتب السلم الوظيفي فقط";
            sb.AppendLine($"\"{index++}\",\"{EscapeCsv(emp.EmployeeNumber)}\",\"{EscapeCsv(emp.FullName)}\",\"{EscapeCsv(emp.FormationName)}\",\"{EscapeCsv(emp.JobTitle)}\",\"{EscapeCsv(emp.GradeName)}\",\"{EscapeCsv(emp.StageName)}\",\"{emp.BasicSalary:N0}\",\"{emp.TotalPercentageRate:G29}%\",\"{emp.TotalPercentageAmount:N0}\",\"{emp.TotalFixedAmount:N0}\",\"{emp.TotalAllowances:N0}\",\"{emp.GrossSalary:N0}\",\"{status}\",\"{EscapeCsv(emp.OrderNumber)}\"");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string? val)
    {
        if (string.IsNullOrEmpty(val)) return string.Empty;
        return val.Replace("\"", "\"\"");
    }
}
