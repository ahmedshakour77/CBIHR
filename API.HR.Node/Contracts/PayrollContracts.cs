using API.HR.Node.Data.Allowances;

namespace API.HR.Node.Contracts;

public sealed class PayrollFilterDto
{
    public int? FormationId { get; set; }
    public int Year { get; set; } = DateTime.Today.Year;
    public int Month { get; set; } = DateTime.Today.Month;
    public string? SearchTerm { get; set; }
    public string StatusFilter { get; set; } = "all"; // all, withPackage, scaleOnly
    public string SortBy { get; set; } = "gross_desc"; // gross_desc, gross_asc, name, id, basic_desc
    public string FormationSortBy { get; set; } = "intensity_desc"; // intensity_desc, intensity_asc, gross_desc, employees_desc, average_desc, allowance_ratio_desc, name_asc
}

public sealed class EmployeePayrollItemDto
{
    public int EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? PositionTitle { get; set; }
    public int? FormationId { get; set; }
    public string FormationName { get; set; } = "غير محدد";
    public string? GradeName { get; set; }
    public string? StageName { get; set; }
    public string? GradeCode { get; set; }
    public string? StageCode { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal TotalPercentageRate { get; set; }
    public decimal TotalPercentageAmount { get; set; }
    public decimal TotalFixedAmount { get; set; }
    public decimal TotalAllowances => TotalPercentageAmount + TotalFixedAmount;
    public decimal GrossSalary { get; set; }
    public bool HasActiveAllowancePackage { get; set; }
    public DateOnly? PackageStartDate { get; set; }
    public string? OrderNumber { get; set; }
    public string? Notes { get; set; }
    public List<AllowanceCalculatedItemDto> AllowanceItems { get; set; } = [];
}

public sealed class FormationPayrollSummaryDto
{
    public int FormationId { get; set; }
    public string FormationName { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int EmployeesWithPackageCount { get; set; }
    public int EmployeesOnScaleOnlyCount => EmployeeCount - EmployeesWithPackageCount;
    public decimal TotalBasicSalary { get; set; }
    public decimal TotalPercentageAllowances { get; set; }
    public decimal TotalFixedAllowances { get; set; }
    public decimal TotalAllowances => TotalPercentageAllowances + TotalFixedAllowances;
    public decimal TotalGrossSalary { get; set; }
    public decimal AverageGrossSalary => EmployeeCount > 0 ? Math.Round(TotalGrossSalary / EmployeeCount, 0) : 0m;

    /// <summary>
    /// نسبة عدد موظفي هذا التشكيل من إجمالي كادر المؤسسة (%)
    /// </summary>
    public decimal EmployeePercentage { get; set; }

    /// <summary>
    /// نسبة إنفاق هذا التشكيل من إجمالي فاتورة رواتب المؤسسة (%)
    /// </summary>
    public decimal SpendingPercentage { get; set; }

    /// <summary>
    /// مؤشر كثافة الإنفاق مقارنة بنسبة عدد الموظفين (Spending-to-Staff Ratio)
    /// القيمة 1.0 تعني توازناً مع المعدل العام للمؤسسة.
    /// القيمة أكبر من 1.0 (مثلاً 1.45×) تعني أن إنفاق التشكيل كبير جداً مقارنة بحجم كادره.
    /// القيمة أقل من 1.0 تعني أن إنفاق التشكيل أقل من حصته العددية.
    /// </summary>
    public decimal SpendingIntensityRatio { get; set; } = 1.0m;

    /// <summary>
    /// نسبة المخصصات المالية إلى الراتب الإسمي في التشكيل (%)
    /// </summary>
    public decimal AllowanceToBasicRatio => TotalBasicSalary > 0 
        ? Math.Round((TotalAllowances / TotalBasicSalary) * 100m, 1) 
        : 0m;
}

public sealed class MonthlyPayrollReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthNameAr { get; set; } = string.Empty;
    public int? SelectedFormationId { get; set; }
    public string? SelectedFormationName { get; set; }

    public int TotalEmployeesCount { get; set; }
    public int EmployeesWithPackageCount { get; set; }
    public int EmployeesOnScaleOnlyCount { get; set; }
    public decimal TotalBasicSalary { get; set; }
    public decimal TotalPercentageAllowances { get; set; }
    public decimal TotalFixedAllowances { get; set; }
    public decimal TotalAllowances => TotalPercentageAllowances + TotalFixedAllowances;
    public decimal TotalGrossSalary { get; set; }
    public decimal AverageGrossSalary => TotalEmployeesCount > 0 ? Math.Round(TotalGrossSalary / TotalEmployeesCount, 0) : 0m;

    public List<FormationPayrollSummaryDto> Formations { get; set; } = [];
    public List<EmployeePayrollItemDto> Employees { get; set; } = [];

    // Analytical Highlights
    public FormationPayrollSummaryDto? HighestIntensityFormation => Formations.Count > 0 
        ? Formations.OrderByDescending(f => f.SpendingIntensityRatio).FirstOrDefault() 
        : null;

    public FormationPayrollSummaryDto? HighestPayrollFormation => Formations.Count > 0 
        ? Formations.OrderByDescending(f => f.TotalGrossSalary).FirstOrDefault() 
        : null;

    public FormationPayrollSummaryDto? LargestStaffFormation => Formations.Count > 0 
        ? Formations.OrderByDescending(f => f.EmployeeCount).FirstOrDefault() 
        : null;
}
