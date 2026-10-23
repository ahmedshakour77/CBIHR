using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data.Allowances;

/// <summary>
/// بند مخصص محدد ضمن فترة مخصصات الموظف
/// </summary>
public sealed class EmployeeAllowanceItem
{
    public int Id { get; set; }

    public int PeriodId { get; set; }
    public EmployeeAllowancePeriod Period { get; set; } = null!;

    public int? AllowanceTypeId { get; set; }
    public AllowanceType? AllowanceType { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public AllowanceCalculationType CalculationType { get; set; } = AllowanceCalculationType.Percentage;

    /// <summary>
    /// النسبة المئوية إذا كان المخصص نسبياً (مثلاً: 45%)
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? Percentage { get; set; }

    /// <summary>
    /// المبلغ الثابت الأصلي إذا كان المخصص مقطوعاً
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? FixedAmount { get; set; }

    /// <summary>
    /// القيمة المحتسبة شهرياً بالدينار العراقي:
    /// إذا كان نسبياً = (الراتب الإسمي × النسبة / 100)
    /// إذا كان مقطوعاً = المبلغ الثابت
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CalculatedAmount { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }
}
