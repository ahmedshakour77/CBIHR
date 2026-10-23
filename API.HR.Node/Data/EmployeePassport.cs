using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data;

/// <summary>
/// يمثل سجل جواز السفر للموظف (Iraqi / Electronic Passport - ICAO 9303 Doc 9303-4 compliant).
/// يحتوي على البيانات الرسمية المدققة، وصورة صفحة البيانات الرئيسية (Bio/Data Page)،
/// والصورة البيومترية من الشريحة إن وجدت.
/// </summary>
public sealed class EmployeePassport
{
    public int Id { get; set; }

    [Required]
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    /// <summary>
    /// رقم جواز السفر (Passport Number) - مثل A12345678 أو B...
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string PassportNumber { get; set; } = string.Empty;

    /// <summary>
    /// نوع الجواز (Passport Type - عادي P، دبلوماسي D، خدمة S، خاص Sp)
    /// </summary>
    [MaxLength(30)]
    public string PassportType { get; set; } = "عادي (P)";

    /// <summary>
    /// هل الجواز إلكتروني (ذو شريحة ذكية بيومترية e-Passport) أم عادي تقليدي مقروء آلياً (MRP)
    /// </summary>
    public bool IsElectronic { get; set; } = false;

    /// <summary>
    /// فئة الجواز (إلكتروني / عادي)
    /// </summary>
    [NotMapped]
    public string PassportCategory => IsElectronic ? "إلكتروني" : "عادي";

    /// <summary>
    /// رمز الدولة المصدرة (Issuing Country Code - الافتراضي IRQ)
    /// </summary>
    [MaxLength(10)]
    public string CountryCode { get; set; } = "IRQ";

    /// <summary>
    /// الدولة المصدرة (Issuing State / Country)
    /// </summary>
    [MaxLength(100)]
    public string IssuingCountry { get; set; } = "العراق";

    /// <summary>
    /// جهة أو سلطة الإصدار (Authority / Issuing Office)
    /// </summary>
    [MaxLength(150)]
    public string? IssuingAuthority { get; set; }

    /// <summary>
    /// الرقم الوطني الموحد المثبت بالجواز (National ID / Personal Number)
    /// </summary>
    [MaxLength(50)]
    public string? NationalIdNumber { get; set; }

    /// <summary>
    /// الاسم الكامل بالعربية
    /// </summary>
    [MaxLength(300)]
    public string? FullNameAr { get; set; }

    /// <summary>
    /// الاسم الكامل بالإنجليزية / اللاتينية (Surname & Given Names)
    /// </summary>
    [MaxLength(300)]
    public string? FullNameEn { get; set; }

    /// <summary>
    /// اسم الأم (كما هو مثبت في الجواز العراقي)
    /// </summary>
    [MaxLength(150)]
    public string? MotherName { get; set; }

    /// <summary>
    /// محل الولادة (Place of Birth)
    /// </summary>
    [MaxLength(150)]
    public string? PlaceOfBirth { get; set; }

    /// <summary>
    /// تاريخ الميلاد (Date of Birth)
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// الجنس (Gender: ذكر / أنثى / M / F)
    /// </summary>
    [MaxLength(20)]
    public string? Gender { get; set; }

    /// <summary>
    /// تاريخ الإصدار (Date of Issue)
    /// </summary>
    public DateOnly? IssueDate { get; set; }

    /// <summary>
    /// تاريخ انتهاء الصلاحية (Date of Expiry)
    /// </summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>
    /// شريط القراءة الآلية (MRZ - سطرين × 44 حرفاً)
    /// </summary>
    [MaxLength(200)]
    public string? RawMrz { get; set; }

    /// <summary>
    /// مفتاح التخزين لصورة صفحة البيانات الرئيسية للجواز (Passport Bio/Data Page Scan)
    /// </summary>
    [MaxLength(500)]
    public string? PassportImageKey { get; set; }

    [MaxLength(255)]
    public string? PassportImageFileName { get; set; }

    [MaxLength(100)]
    public string? PassportImageContentType { get; set; }

    /// <summary>
    /// مفتاح التخزين للصورة الشخصية البيومترية من الشريحة (إن وجدت)
    /// </summary>
    [MaxLength(500)]
    public string? BiometricPhotoKey { get; set; }

    [MaxLength(255)]
    public string? BiometricPhotoFileName { get; set; }

    [MaxLength(100)]
    public string? BiometricPhotoContentType { get; set; }

    /// <summary>
    /// هل الجواز هو الجواز المعتمد والساري للموظف حالياً
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// تاريخ التسجيل في المنظومة
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ملاحظات تدقيقية أو أمنية
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }
}
