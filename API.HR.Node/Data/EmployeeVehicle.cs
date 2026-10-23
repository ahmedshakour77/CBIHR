using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.HR.Node.Data;

/// <summary>
/// متعلقات الموظف — سجل المركبات والسيارات المصرح لها بالدخول
/// </summary>
public sealed class EmployeeVehicle
{
    public int Id { get; set; }

    // رقم الموظف المرتبط
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // تفاصيل المركبة
    /// <summary>البراند / الشركة المصنعة (مثل: تويوتا، نيسان، كيا، هيونداي...)</summary>
    [MaxLength(100)]
    public required string Brand { get; set; }

    /// <summary>الموديل (مثل: كورولا، كامري، سورينتو، إلنترا...)</summary>
    [MaxLength(100)]
    public required string Model { get; set; }

    /// <summary>اللون (مثل: أبيض، أسود، فضي، رصاصي...)</summary>
    [MaxLength(50)]
    public string? Color { get; set; }

    /// <summary>رقم لوحة التسجيل (مثل: 12345)</summary>
    [MaxLength(30)]
    public required string PlateNumber { get; set; }

    /// <summary>حرف اللوحة (مثل: أ، ب، جـ، د... أو بغداد / أربيل)</summary>
    [MaxLength(30)]
    public string? PlateLetter { get; set; }

    /// <summary>المحافظة (مثل: بغداد، البصرة، أربيل، نينوى، كركوك...)</summary>
    [MaxLength(50)]
    public required string Governorate { get; set; }

    /// <summary>رقم الشاصي (VIN / Chassis Number)</summary>
    [MaxLength(100)]
    public string? Vin { get; set; }

    /// <summary>سنة الصنع (الموديل سنة)</summary>
    public int? ManufacturingYear { get; set; }

    // حالة التخويل بالدخول
    /// <summary>هل السيارة مخولة للدخول؟</summary>
    public bool IsAuthorized { get; set; } = true;

    // المواقع المسموح بالدخول إليها:
    // 1- المبنى الرئيسي
    public bool AllowMainBuilding { get; set; } = false;

    // 2- الجادرية
    public bool AllowJadriya { get; set; } = false;

    // 3- أربيل
    public bool AllowErbil { get; set; } = false;

    // 4- الموصل
    public bool AllowMosul { get; set; } = false;

    // 5- البصرة
    public bool AllowBasra { get; set; } = false;

    /// <summary>هل السائق هو الموظف نفسه؟ (الافتراضي: نعم)</summary>
    public bool IsDriverSelf { get; set; } = true;

    /// <summary>اسم السائق في حال كان شخصاً آخر غير الموظف</summary>
    [MaxLength(150)]
    public string? DriverName { get; set; }

    /// <summary>رقم هاتف السائق للتواصل عند البوابات</summary>
    [MaxLength(30)]
    public string? DriverPhone { get; set; }

    /// <summary>ملاحظات إضافية (مثل: نوع الوقود، رقم الباج الخاص بالسيارة، تصريح مؤقت...)</summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    /// <summary>
    /// الاسم المعروض للسائق (إما الموظف نفسه أو اسم السائق المسجل)
    /// </summary>
    [NotMapped]
    public string DriverDisplayName =>
        IsDriverSelf
            ? (!string.IsNullOrWhiteSpace(Employee?.Name) ? $"{Employee.Name} (الموظف نفسه)" : "الموظف نفسه")
            : (!string.IsNullOrWhiteSpace(DriverName) ? DriverName : "سائق مخصص");

    /// <summary>
    /// دالة مساعدة لتجميع قائمة بأسماء المواقع المصرح بها
    /// </summary>
    [NotMapped]
    public List<string> PermittedLocationsList
    {
        get
        {
            var list = new List<string>();
            if (AllowMainBuilding) list.Add("المبنى الرئيسي");
            if (AllowJadriya) list.Add("الجادرية");
            if (AllowErbil) list.Add("أربيل");
            if (AllowMosul) list.Add("الموصل");
            if (AllowBasra) list.Add("البصرة");
            return list;
        }
    }

    [NotMapped]
    public string PermittedLocationsDisplay =>
        PermittedLocationsList.Count > 0
            ? string.Join("، ", PermittedLocationsList)
            : "غير محدد أي موقع";
}
