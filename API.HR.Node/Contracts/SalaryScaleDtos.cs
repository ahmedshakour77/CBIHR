namespace API.HR.Node.Contracts;

public sealed class SalaryScaleStageHeader
{
    public int StageNumber { get; set; }
    public string StageCode { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public int TotalEmployees { get; set; }
}

public sealed class SalaryScaleCellDto
{
    public int Id { get; set; }
    public int GradeId { get; set; }
    public int StageId { get; set; }
    public string GradeCode { get; set; } = string.Empty;
    public string StageCode { get; set; } = string.Empty;
    public int StageNumber { get; set; }
    public decimal BaseSalary { get; set; } // In thousands IQD (e.g. 910)
    public decimal FullSalary => BaseSalary * 1000m; // In full IQD (e.g. 910,000)
    public decimal? AnnualAllowance { get; set; }
    public int? RequiredServiceYears { get; set; }
    public int EmployeeCount { get; set; }
}

public sealed class SalaryScaleRowDto
{
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string GradeCode { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public decimal? AnnualAllowance { get; set; }
    public int? RequiredServiceYears { get; set; }
    public int TotalEmployeesInGrade { get; set; }
    public Dictionary<int, SalaryScaleCellDto> Cells { get; set; } = new();
}

public sealed class SalaryScaleMatrixDto
{
    public List<SalaryScaleStageHeader> Stages { get; set; } = new();
    public List<SalaryScaleRowDto> Rows { get; set; } = new();
    public int TotalMappedEmployees { get; set; }
    public decimal TotalEstimatedPayrollThousands { get; set; }
}
