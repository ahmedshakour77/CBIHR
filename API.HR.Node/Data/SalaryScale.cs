using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data;

/// <summary>
/// جدول السلم الوظيفي الذي يربط بين الدرجات والمراحل
/// مستنداً إلى قانون رواتب موظفي الدولة وجدول الدرجات والمراحل المعتمد
/// </summary>
public sealed class SalaryScale
{
    public int Id { get; set; }

    public int GradeId { get; set; }
    public Grade Grade { get; set; } = null!;

    public int StageId { get; set; }
    public Stage Stage { get; set; } = null!;

    /// <summary>
    /// الحقل النصي للدرجة، مثل: "العليا أ", "العليا ب", "الخاصة أ", "الخاصة ب", "1" .. "10"
    /// </summary>
    [MaxLength(50)]
    public required string GradeCode { get; set; }

    /// <summary>
    /// الحقل النصي للمرحلة، مثل: "1" .. "11"
    /// </summary>
    [MaxLength(50)]
    public required string StageCode { get; set; }

    /// <summary>
    /// الرقم التسلسلي للمرحلة (1 - 11) لتسهيل الفرز والعمليات الحسابية
    /// </summary>
    public int StageNumber { get; set; }

    /// <summary>
    /// ترتيب الدرجة الهيكلي (1 العليا أ، 2 العليا ب، 3 الخاصة أ، 4 الخاصة ب، 5 الدرجة 1 ... 14 الدرجة 10)
    /// </summary>
    public int GradeSortOrder { get; set; }

    /// <summary>
    /// الراتب الإسمي الأساسي (المبين في الجدول، مثلاً: 4000، 2413، 910، 170 بآلاف الدنانير)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseSalary { get; set; }

    /// <summary>
    /// العلاوة السنوية (مثلاً: 83، 20، 17، 10، 8، 6، 3 أو فارغة للدرجة العليا أ)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? AnnualAllowance { get; set; }

    /// <summary>
    /// سنوات الخدمة الأصغرية المطلوبة للترفيع (مثلاً: 5 أو 4 سنوات، أو فارغة)
    /// </summary>
    public int? RequiredServiceYears { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }
}
