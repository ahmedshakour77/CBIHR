using System.ComponentModel.DataAnnotations;

namespace API.HR.Node.Services.IdentityDocuments;

/// <summary>
/// نموذج طلب تسجيل أو تحديث بيانات البطاقة الموحدة الإلكترونية عبر الـ API
/// </summary>
public sealed record NationalCardRegisterRequest
{
    /// <summary>
    /// رقم الوثيقة / رقم الهوية (Document Number) - الفهرس الإلزامي للبطاقة والـ MRZ
    /// </summary>
    [Required(ErrorMessage = "رقم الوثيقة / رقم الهوية مطلوب إلزامي.")]
    [MaxLength(50)]
    public string DocumentNumber { get; init; } = string.Empty;

    /// <summary>
    /// الرقم الوطني الموحد (National ID / Personal Number) - مستخرج من DG11 أو الحقل المخصص بالـ MRZ (12 رقماً)
    /// </summary>
    [MaxLength(50)]
    public string? NationalIdNumber { get; init; }

    /// <summary>
    /// الاسم الكامل بالعربية (مستخرج من DG11 أو ماسح OCR)
    /// </summary>
    [MaxLength(300)]
    public string? FullNameAr { get; init; }

    /// <summary>
    /// الاسم اللاتيني الأول (Given Names / First Name من DG1/MRZ)
    /// </summary>
    [MaxLength(150)]
    public string? FirstNameEn { get; init; }

    /// <summary>
    /// اللقب أو الاسم اللاتيني الأخير (Surname / Last Name من DG1/MRZ)
    /// </summary>
    [MaxLength(150)]
    public string? LastNameEn { get; init; }

    /// <summary>
    /// الاسم اللاتيني المركب
    /// </summary>
    [MaxLength(300)]
    public string? FullNameEn { get; init; }

    /// <summary>
    /// تاريخ الميلاد
    /// </summary>
    public DateOnly? DateOfBirth { get; init; }

    /// <summary>
    /// الجنس (M / F / ذكر / أنثى)
    /// </summary>
    [MaxLength(20)]
    public string? Gender { get; init; }

    /// <summary>
    /// الجنسية (عراقي / Iraqi)
    /// </summary>
    [MaxLength(100)]
    public string? Nationality { get; init; }

    /// <summary>
    /// رمز الدولة المصدرة (مثلاً: IRQ)
    /// </summary>
    [MaxLength(10)]
    public string IssuingState { get; init; } = "IRQ";

    /// <summary>
    /// تاريخ الإصدار
    /// </summary>
    public DateOnly? IssueDate { get; init; }

    /// <summary>
    /// تاريخ انتهاء الصلاحية
    /// </summary>
    public DateOnly? ExpiryDate { get; init; }

    /// <summary>
    /// أسطر الـ MRZ الخام المقروءة من البطاقة (معيار TD1 - 3 أسطر × 30 حرفاً)
    /// </summary>
    [MaxLength(200)]
    public string? RawMrz { get; init; }

    // ==========================================
    // الصور الرقمية (Base64 JPEG/PNG)
    // ==========================================

    /// <summary>
    /// الصورة الشخصية البيومترية المستخرجة من شريحة الـ RFID (DG2 Face Image) بصيغة Base64
    /// </summary>
    public string? BiometricFaceBase64 { get; init; }

    /// <summary>
    /// صورة البطاقة الموحدة من الأمام بصيغة Base64
    /// </summary>
    public string? FrontImageBase64 { get; init; }

    /// <summary>
    /// صورة البطاقة الموحدة من الخلف بصيغة Base64
    /// </summary>
    public string? BackImageBase64 { get; init; }

    // ==========================================
    // بيانات التدقيق ونزاهة المستند (ICAO 9303 Audit & Security Proof)
    // ==========================================

    /// <summary>
    /// حالة التحقق السلبي (Passive Authentication Status):
    /// يوضح هل الـ Hashes في ملف الـ SOD مطابقة للـ Data Groups وموقعة بشهادة الـ Country Signing CA
    /// </summary>
    public bool IsPassiveAuthenticationValid { get; init; }

    /// <summary>
    /// تاريخ ووقت التحقق من الشريحة
    /// </summary>
    public DateTime? VerificationTimestamp { get; init; }

    /// <summary>
    /// بصمة الوثيقة (Hash / Fingerprint): حفظ الـ Hash المشفر لملف الـ SOD لإثبات سلامة الجلسة
    /// </summary>
    [MaxLength(256)]
    public string? SodHash { get; init; }

    /// <summary>
    /// شهادة التوقيع / جهة الإصدار المعتمدة (CSCA / Document Signer)
    /// </summary>
    [MaxLength(250)]
    public string? CertificateIssuer { get; init; }

    /// <summary>
    /// ملاحظات تدقيقية أو أمنية
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; init; }

    // ==========================================
    // حقول إضافية وبديلة مرسلة من تطبيقات الموبايل (Flutter / NFC Reader Compatibility)
    // ==========================================
    public string? DocumentType { get; init; }
    public string? NationalNumber { get; init; }
    public string? PersonalNumber { get; init; }
    public string? FullNameArabic { get; init; }
    public string? FullNameEnglish { get; init; }
    public string? Surname { get; init; }
    public string? GivenNames { get; init; }
    public string? MotherName { get; init; }
    public string? PlaceOfBirth { get; init; }
    public string? BloodType { get; init; }
    public string? IssuingCountry { get; init; }
    public string? IssuingAuthority { get; init; }
    public string? BirthDate { get; init; }
    public bool? IsBacAuthenticated { get; init; }
    public bool? IsSodHashVerified { get; init; }
    public string? FacePhotoBase64 { get; init; }

    // دوال مساعدة لتوحيد واستخراج الحقول الفعالة أياً كان الاسم المرسل من الهاتف
    public string? GetEffectiveNationalId() =>
        !string.IsNullOrWhiteSpace(NationalIdNumber) ? NationalIdNumber.Trim() :
        (!string.IsNullOrWhiteSpace(NationalNumber) ? NationalNumber.Trim() :
        (!string.IsNullOrWhiteSpace(PersonalNumber) ? PersonalNumber.Trim() : null));

    public string? GetEffectiveFullNameAr() =>
        !string.IsNullOrWhiteSpace(FullNameAr) ? FullNameAr.Trim() :
        (!string.IsNullOrWhiteSpace(FullNameArabic) ? FullNameArabic.Trim() : null);

    public string? GetEffectiveFullNameEn()
    {
        if (!string.IsNullOrWhiteSpace(FullNameEn)) return FullNameEn.Trim();
        if (!string.IsNullOrWhiteSpace(FullNameEnglish)) return FullNameEnglish.Trim();
        var combined = $"{GivenNames} {Surname}".Trim();
        return !string.IsNullOrWhiteSpace(combined) ? combined : null;
    }

    public DateOnly? GetEffectiveBirthDate()
    {
        if (DateOfBirth.HasValue) return DateOfBirth.Value;
        if (!string.IsNullOrWhiteSpace(BirthDate) && DateOnly.TryParse(BirthDate, out var b)) return b;
        return null;
    }

    public DateOnly? GetEffectiveIssueDate() => IssueDate;

    public DateOnly? GetEffectiveExpiryDate() => ExpiryDate;

    public string? GetEffectiveBiometricPhoto() =>
        !string.IsNullOrWhiteSpace(BiometricFaceBase64) ? BiometricFaceBase64 :
        (!string.IsNullOrWhiteSpace(FacePhotoBase64) ? FacePhotoBase64 : null);

    public bool GetEffectivePassiveAuth() =>
        IsPassiveAuthenticationValid || IsSodHashVerified == true || IsBacAuthenticated == true;

    public string GetEffectiveIssuingState() =>
        !string.IsNullOrWhiteSpace(IssuingState) ? IssuingState.Trim() :
        (!string.IsNullOrWhiteSpace(IssuingCountry) ? IssuingCountry.Trim() : "IRQ");

    public string? GetEffectiveCertificateIssuer() =>
        !string.IsNullOrWhiteSpace(CertificateIssuer) ? CertificateIssuer.Trim() :
        (!string.IsNullOrWhiteSpace(IssuingAuthority) ? IssuingAuthority.Trim() : null);
}

/// <summary>
/// كائن الاستجابة الكامل للبطاقة الوطنية الموحدة
/// </summary>
public sealed record NationalCardResponseDto
{
    public int Id { get; init; }
    public int EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;

    public string DocumentNumber { get; init; } = string.Empty;
    public string? NationalIdNumber { get; init; }
    public string? FullNameAr { get; init; }
    public string? FirstNameEn { get; init; }
    public string? LastNameEn { get; init; }
    public string? FullNameEn { get; init; }

    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? Nationality { get; init; }
    public string IssuingState { get; init; } = "IRQ";
    public DateOnly? IssueDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public string? RawMrz { get; init; }

    // روابط استرجاع الصور عبر الـ API
    public string? BiometricPhotoUrl { get; init; }
    public string? FrontImageUrl { get; init; }
    public string? BackImageUrl { get; init; }
    public bool HasBiometricPhoto { get; init; }
    public bool HasFrontImage { get; init; }
    public bool HasBackImage { get; init; }

    // إثباتات الأمان والتحقق
    public bool IsPassiveAuthenticationValid { get; init; }
    public DateTime? VerificationTimestamp { get; init; }
    public string? SodHash { get; init; }
    public string? CertificateIssuer { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// نموذج فك تشفير والتحقق من أسطر الـ MRZ لمعيار TD1 (البطاقة الوطنية العراقية)
/// </summary>
public sealed record MrzParseRequest
{
    [Required(ErrorMessage = "نص الـ MRZ مطلوب.")]
    public string MrzText { get; init; } = string.Empty;
}

public sealed record MrzParseResultDto
{
    public bool IsValid { get; init; }
    public string DocumentType { get; init; } = "I";
    public string DocumentNumber { get; init; } = string.Empty;
    public string? NationalIdNumber { get; init; }
    public string IssuingCountry { get; init; } = "IRQ";
    public string? Surname { get; init; }
    public string? GivenNames { get; init; }
    public string? FullNameEn { get; init; }
    public string Nationality { get; init; } = "IRQ";
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public List<string> ValidationErrors { get; init; } = new();
}
