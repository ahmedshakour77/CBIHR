using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Data;

/// <summary>
/// إعدادات وميزات المنظومة العامة التي يتحكم بها مسؤول النظام (Admin Control Panel).
/// </summary>
public class SystemFeatureSettings
{
    [Key]
    public int Id { get; set; } = 1;

    /// <summary>
    /// هل خاصية وتبويب "مشاركاتي" مفعلة في المنظومة (القوائم العلوية والجانبية والعائمة، وملف الموظف)؟
    /// </summary>
    public bool EnableMyFeedback { get; set; } = true;

    /// <summary>
    /// هل الزر العائم (FAB) للمشاركة السريعة ظاهر لعموم الموظفين؟
    /// </summary>
    public bool EnableFeedbackFab { get; set; } = true;

    /// <summary>
    /// هل استقبال الأفكار والمقترحات مفعّل؟ (يمكن للإدارة إيقافه مؤقتاً)
    /// </summary>
    public bool AllowIdeas { get; set; } = true;

    /// <summary>
    /// هل استقبال ملاحظات العمل مفعّل؟
    /// </summary>
    public bool AllowNotes { get; set; } = true;

    /// <summary>
    /// هل استقبال الاستفسارات الوظيفية مفعّل؟
    /// </summary>
    public bool AllowInquiries { get; set; } = true;

    /// <summary>
    /// هل استقبال الشكاوى الإدارية مفعّل؟
    /// </summary>
    public bool AllowComplaints { get; set; } = true;

    /// <summary>
    /// هل استقبال المشاركات الأخرى مفعّل؟
    /// </summary>
    public bool AllowOthers { get; set; } = true;

    /// <summary>
    /// السماح برفع المرفقات مع المشاركات؟
    /// </summary>
    public bool AllowAttachments { get; set; } = true;

    /// <summary>
    /// رسالة الإدارة التوضيحية عند إيقاف الأفكار أو المشاركات.
    /// </summary>
    [MaxLength(500)]
    public string? DisabledMessage { get; set; } = "خدمة تقديم الأفكار والمقترحات متوقفة مؤقتاً بقرار الإدارة.";

    /// <summary>
    /// تاريخ آخر تحديث
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// اسم الأدمن الذي قام بآخر تحديث
    /// </summary>
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
}
