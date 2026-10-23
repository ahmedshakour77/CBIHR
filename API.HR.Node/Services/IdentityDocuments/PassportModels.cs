using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Services.IdentityDocuments;

/// <summary>
/// نتيجة فك تشفير وتدقيق شريط القراءة الآلية للجواز (ICAO 9303 TD3 - 2 lines x 44 chars)
/// </summary>
public sealed record PassportMrzResult
{
    public bool IsValid { get; init; }
    public string DocumentType { get; init; } = "P";
    public string PassportNumber { get; init; } = string.Empty;
    public string IssuingCountry { get; init; } = "IRQ";
    public string Nationality { get; init; } = "IRQ";
    public string Surname { get; init; } = string.Empty;
    public string GivenNames { get; init; } = string.Empty;
    public string FullNameEn { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string Gender { get; init; } = "ذكر";
    public DateOnly? ExpiryDate { get; init; }
    public string? NationalIdNumber { get; init; }
    public bool IsCheckDigitsValid { get; init; } = true;
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
}

/// <summary>
/// نموذج استيراد وقراءة الشريحة الإلكترونية للجواز البيومتري (e-Passport Chip / NFC)
/// </summary>
public sealed record PassportChipDataRequest
{
    /// <summary>
    /// نص الـ MRZ المستخرج من شريحة DG1
    /// </summary>
    public string? Dg1Mrz { get; init; }

    /// <summary>
    /// الصورة البيومترية عالية الدقة المستخرجة من شريحة DG2 (Base64 JPEG/JP2)
    /// </summary>
    public string? Dg2BiometricPhotoBase64 { get; init; }

    /// <summary>
    /// الاسم الكامل بالعربية من شريحة DG11 (إن وجد)
    /// </summary>
    public string? Dg11FullNameAr { get; init; }

    /// <summary>
    /// اسم الأم من شريحة DG11
    /// </summary>
    public string? Dg11MotherName { get; init; }

    /// <summary>
    /// الرقم الوطني الموحد من شريحة DG11
    /// </summary>
    public string? Dg11NationalId { get; init; }

    /// <summary>
    /// محل الولادة من شريحة DG11
    /// </summary>
    public string? Dg11PlaceOfBirth { get; init; }

    /// <summary>
    /// سلطة الإصدار
    /// </summary>
    public string? IssuingAuthority { get; init; }
}

/// <summary>
/// نتيجة قراءة وتفريغ بيانات الشريحة الإلكترونية
/// </summary>
public sealed record PassportChipResult
{
    public bool Success { get; init; }
    public PassportMrzResult? MrzInfo { get; init; }
    public string? FullNameAr { get; init; }
    public string? MotherName { get; init; }
    public string? NationalId { get; init; }
    public string? PlaceOfBirth { get; init; }
    public byte[]? BiometricPhotoBytes { get; init; }
    public string? BiometricPhotoMimeType { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
