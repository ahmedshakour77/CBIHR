using API.HR.Node.Data.Allowances;

namespace API.HR.Node.Contracts;

public sealed class SaveAllowancePeriodRequest
{
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? EndDate { get; set; }
    public decimal BasicSalary { get; set; }
    public string? OrderNumber { get; set; }
    public DateOnly? OrderDate { get; set; }
    public string? ChangeReason { get; set; }
    public string? Notes { get; set; }
    public List<AllowanceItemDraft> Items { get; set; } = [];
}

public sealed class AllowanceItemDraft
{
    public int? AllowanceTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AllowanceCalculationType CalculationType { get; set; } = AllowanceCalculationType.Percentage;
    public decimal? Percentage { get; set; }
    public decimal? FixedAmount { get; set; }
    public string? Notes { get; set; }
}

public sealed class AllowanceCalculationResult
{
    public decimal BasicSalary { get; set; }
    public decimal TotalPercentageRate { get; set; }
    public decimal TotalPercentageAmount { get; set; }
    public decimal TotalFixedAmount { get; set; }
    public decimal GrossSalary { get; set; }
    public List<AllowanceCalculatedItemDto> Items { get; set; } = [];
}

public sealed class AllowanceCalculatedItemDto
{
    public string Name { get; set; } = string.Empty;
    public AllowanceCalculationType CalculationType { get; set; }
    public decimal? Percentage { get; set; }
    public decimal? FixedAmount { get; set; }
    public decimal CalculatedAmount { get; set; }
    public string? Notes { get; set; }
}
