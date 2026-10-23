using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data;

/// <summary>
/// يمثل سجل البطاقة الوطنية الموحدة الإلكترونية للموظف (Electronic National ID Card - ICAO 9303 compliant).
/// يحتوي على البيانات المقروءة رقمياً (NFC/e-MRTD)، والصورة البيومترية (DG2)، وبيانات التحقق الأمني (Passive Authentication & SOD Hash)،
/// وصور واجهة البطاقة وخلفيتها.
/// </summary>
public sealed class EmployeeNationalCard
{
    public int Id { get; set; }

    [Required]
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    // ==========================================
    // 1. البيانات الأساسية للنظام (النصوص المباشرة)
    // ==========================================

    /// <summary>
    /// رقم الوثيقة / رقم الهوية (Document Number) - الفهرس الرئيسي للبحث والـ MRZ
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>
    /// الرقم الوطني الموحد (National ID / Personal Number) - مستخرج من DG11 أو حقل الـ MRZ (12 رقماً)
    /// </summary>
    [MaxLength(50)]
    public string? NationalIdNumber { get; set; }

    /// <summary>
    /// الاسم الكامل بالعربية (مستخرج من DG11 أو OCR لواجهة البطاقة)
    /// </summary>
    [MaxLength(300)]
    public string? FullNameAr { get; set; }

    /// <summary>
    /// الاسم اللاتيني الأول (Given Names / First Name - مستخرج من DG1 / MRZ)
    /// </summary>
    [MaxLength(150)]
    public string? FirstNameEn { get; set; }

    /// <summary>
    /// اللقب أو الاسم اللاتيني الأخير (Surname / Last Name - مستخرج من DG1 / MRZ)
    /// </summary>
    [MaxLength(150)]
    public string? LastNameEn { get; set; }

    /// <summary>
    /// الاسم اللاتيني الكامل المركب
    /// </summary>
    [MaxLength(300)]
    public string? FullNameEn { get; set; }

    /// <summary>
    /// تاريخ الميلاد
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// الجنس (M / F / ذكر / أنثى)
    /// </summary>
    [MaxLength(20)]
    public string? Gender { get; set; }

    /// <summary>
    /// الجنسية (مثلاً: عراقي / عراقية / Iraqi)
    /// </summary>
    [MaxLength(100)]
    public string? Nationality { get; set; }

    /// <summary>
    /// رمز الدولة المصدرة (Issuing State - الافتراضي: IRQ)
    /// </summary>
    [MaxLength(10)]
    public string IssuingState { get; set; } = "IRQ";

    /// <summary>
    /// تاريخ إصدار البطاقة
    /// </summary>
    public DateOnly? IssueDate { get; set; }

    /// <summary>
    /// تاريخ انتهاء صلاحية البطاقة
    /// </summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>
    /// نص الـ MRZ الخام المقروء من البطاقة (3 أسطر × 30 حرفاً لمعيار TD1)
    /// </summary>
    [MaxLength(200)]
    public string? RawMrz { get; set; }

    // ==========================================
    // 2. الصور الرقمية والبيومترية (Digital & Biometric Images)
    // ==========================================

    /// <summary>
    /// مسار الصورة الشخصية البيومترية المستخرجة من شريحة الـ RFID (DG2 Face Image)
    /// تُستخدم لاحقاً لمطابقة الوجه مع صورة السيلفي (Face Match / Liveness Detection)
    /// </summary>
    [MaxLength(500)]
    public string? BiometricPhotoPath { get; set; }

    /// <summary>
    /// مسار حفظ صورة واجهة البطاقة الموحدة (Front Image Scan)
    /// </summary>
    [MaxLength(500)]
    public string? FrontImagePath { get; set; }

    /// <summary>
    /// مسار حفظ صورة ظهر البطاقة الموحدة (Back Image Scan)
    /// </summary>
    [MaxLength(500)]
    public string? BackImagePath { get; set; }

    // ==========================================
    // 3. بيانات التحقق الأمني ونزاهة المستند (Audit & Security Proof - ICAO 9303)
    // ==========================================

    /// <summary>
    /// حالة التحقق السلبي (Passive Authentication Status):
    /// يوضح هل الـ Hashes في ملف الـ SOD مطابقة للـ Data Groups وموقعة بشهادة الـ Country Signing CA
    /// </summary>
    public bool IsPassiveAuthenticationValid { get; set; }

    /// <summary>
    /// تاريخ ووقت التحقق الأمني من الشريحة
    /// </summary>
    public DateTime? VerificationTimestamp { get; set; }

    /// <summary>
    /// بصمة الوثيقة (Hash / Fingerprint): حفظ الـ Hash المشفر لملف الـ SOD كدليل قطعي على سلامة الجلسة
    /// </summary>
    [MaxLength(256)]
    public string? SodHash { get; set; }

    /// <summary>
    /// الجهة أو الشهادة المعتمدة في التحقق (Country Signing Certificate Authority - CSCA / DS)
    /// </summary>
    [MaxLength(250)]
    public string? CertificateIssuer { get; set; }

    /// <summary>
    /// هل الوثيقة معتمدة ونشطة للموظف؟
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// تاريخ وتوقيت تسجيل البطاقة في النظام
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// اسم أو معرّف المستخدم أو النظام الذي قام بعملية القراءة والإدخال
    /// </summary>
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// ملاحظات تدقيقية أو أمنية إضافية
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }
}
