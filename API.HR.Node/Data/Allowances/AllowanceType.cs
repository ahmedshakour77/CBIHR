using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data.Allowances;

/// <summary>
/// دليل وكتالوج أنواع المخصصات (مثل: الشهادة، الخطورة، المنصب، الزوجية، الأطفال...)
/// </summary>
public sealed class AllowanceType
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public AllowanceCalculationType CalculationType { get; set; } = AllowanceCalculationType.Percentage;

    /// <summary>
    /// النسبة الافتراضية إذا كانت نسبية (مثلاً 45 تعني 45%)
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? DefaultPercentage { get; set; }

    /// <summary>
    /// المبلغ الافتراضي إذا كانت ثابتة بالدينار العراقي (مثلاً 50000 للمخصصات الزوجية)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DefaultAmount { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; } = 0;
}
