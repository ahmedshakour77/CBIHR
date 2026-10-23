using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using API.HR.Node.Data;
using API.HR.Node.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.IdentityDocuments;

public sealed class NationalCardService(
    ApplicationDbContext db,
    IFileStorageService storage,
    ILogger<NationalCardService> logger) : INationalCardService
{
    public async Task<NationalCardResponseDto> RegisterOrUpdateAsync(
        int employeeId,
        NationalCardRegisterRequest request,
        string? userOrSystem,
        CancellationToken cancellationToken = default)
    {
        var employee = await db.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
            ?? throw new KeyNotFoundException($"الموظف بالمعرّف {employeeId} غير موجود في المنظومة.");

        var cleanDocNumber = request.DocumentNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cleanDocNumber))
        {
            throw new ArgumentException("رقم الوثيقة / رقم الهوية مطلوب ولا يمكن أن يكون فارغاً.", nameof(request.DocumentNumber));
        }

        var existingCard = await db.EmployeeNationalCards
            .FirstOrDefaultAsync(c => c.EmployeeId == employeeId && c.IsActive, cancellationToken);

        var card = existingCard ?? new EmployeeNationalCard
        {
            EmployeeId = employeeId,
            CreatedAt = DateTime.UtcNow
        };

        // 1. البيانات الأساسية
        card.DocumentNumber = cleanDocNumber;
        card.NationalIdNumber = request.GetEffectiveNationalId();
        card.FullNameAr = request.GetEffectiveFullNameAr();
        card.FirstNameEn = string.IsNullOrWhiteSpace(request.FirstNameEn) ? (string.IsNullOrWhiteSpace(request.GivenNames) ? null : request.GivenNames.Trim()) : request.FirstNameEn.Trim();
        card.LastNameEn = string.IsNullOrWhiteSpace(request.LastNameEn) ? (string.IsNullOrWhiteSpace(request.Surname) ? null : request.Surname.Trim()) : request.LastNameEn.Trim();
        card.FullNameEn = request.GetEffectiveFullNameEn();

        card.DateOfBirth = request.GetEffectiveBirthDate();
        card.Gender = NormalizeGender(request.Gender);
        card.Nationality = string.IsNullOrWhiteSpace(request.Nationality) ? "عراقي" : request.Nationality.Trim();
        card.IssuingState = request.GetEffectiveIssuingState();
        card.IssueDate = request.GetEffectiveIssueDate();
        card.ExpiryDate = request.GetEffectiveExpiryDate();
        card.RawMrz = string.IsNullOrWhiteSpace(request.RawMrz) ? null : request.RawMrz.Trim();

        // 2. معالجة وتخزين الصور المشفرة (Base64)
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var bioPhoto = request.GetEffectiveBiometricPhoto();

        if (!string.IsNullOrWhiteSpace(bioPhoto))
        {
            card.BiometricPhotoPath = await SaveBase64ImageAsync(
                bioPhoto,
                $"documents/national-cards/{employeeId}/dg2_face_{timestamp}.jpg",
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.FrontImageBase64))
        {
            card.FrontImagePath = await SaveBase64ImageAsync(
                request.FrontImageBase64,
                $"documents/national-cards/{employeeId}/front_{timestamp}.jpg",
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.BackImageBase64))
        {
            card.BackImagePath = await SaveBase64ImageAsync(
                request.BackImageBase64,
                $"documents/national-cards/{employeeId}/back_{timestamp}.jpg",
                cancellationToken);
        }

        // 3. بيانات التحقق الأمني ونزاهة المستند
        card.IsPassiveAuthenticationValid = request.GetEffectivePassiveAuth();
        card.VerificationTimestamp = request.VerificationTimestamp ?? DateTime.UtcNow;
        card.SodHash = string.IsNullOrWhiteSpace(request.SodHash) ? null : request.SodHash.Trim();
        card.CertificateIssuer = request.GetEffectiveCertificateIssuer();
        card.CreatedBy = userOrSystem ?? "System";
        card.Notes = request.Notes;
        card.IsActive = true;

        // تحديث الحقول المقابلة في ملف الموظف تلقائياً
        if (!string.IsNullOrWhiteSpace(card.NationalIdNumber) && string.IsNullOrWhiteSpace(employee.NationalId))
        {
            employee.NationalId = card.NationalIdNumber;
        }

        if (card.DateOfBirth.HasValue && !employee.BirthDate.HasValue)
        {
            employee.BirthDate = card.DateOfBirth.Value;
        }

        if (existingCard == null)
        {
            db.EmployeeNationalCards.Add(card);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("National Card registered/updated successfully for EmployeeId {EmployeeId}, Doc: {DocumentNumber}", employeeId, card.DocumentNumber);

        return MapToDto(card, employee);
    }

    public async Task<NationalCardResponseDto?> GetByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        var card = await db.EmployeeNationalCards
            .Include(c => c.Employee)
            .Where(c => c.EmployeeId == employeeId && c.IsActive)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return card == null ? null : MapToDto(card, card.Employee!);
    }

    public async Task<NationalCardResponseDto?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default)
    {
        var card = await db.EmployeeNationalCards
            .Include(c => c.Employee)
            .Where(c => c.Employee != null && c.Employee.EmployeeID == employeeNumber && c.IsActive)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return card == null ? null : MapToDto(card, card.Employee!);
    }

    public async Task<NationalCardResponseDto?> GetByDocumentOrNationalIdAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var clean = query.Trim();

        var card = await db.EmployeeNationalCards
            .Include(c => c.Employee)
            .Where(c => c.IsActive && (c.DocumentNumber == clean || c.NationalIdNumber == clean))
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return card == null ? null : MapToDto(card, card.Employee!);
    }

    public async Task<(Stream? Stream, string ContentType)?> GetImageAsync(int cardId, string imageType, CancellationToken cancellationToken = default)
    {
        var card = await db.EmployeeNationalCards.FindAsync([cardId], cancellationToken);
        if (card == null) return null;

        var path = imageType.ToLowerInvariant() switch
        {
            "biometric-photo" or "dg2" => card.BiometricPhotoPath,
            "front-image" or "front" => card.FrontImagePath,
            "back-image" or "back" => card.BackImagePath,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(path)) return null;

        var stream = await storage.OpenReadAsync(path, cancellationToken);
        if (stream == null) return null;

        var contentType = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
            : path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
            : "image/jpeg";

        return (stream, contentType);
    }

    public MrzParseResultDto ParseMrz(string mrzText)
    {
        if (string.IsNullOrWhiteSpace(mrzText))
        {
            return new MrzParseResultDto { IsValid = false, ValidationErrors = ["نص الـ MRZ فارغ."] };
        }

        // تنظيف وفصل الأسطر (معيار TD1 للبطاقة الوطنية: 3 أسطر × 30 حرفاً)
        var lines = mrzText
            .Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => Regex.Replace(l.ToUpperInvariant(), @"[^A-Z0-9<]", ""))
            .Where(l => l.Length >= 28)
            .ToList();

        if (lines.Count < 3)
        {
            return new MrzParseResultDto
            {
                IsValid = false,
                ValidationErrors = ["يجب أن يحتوي الـ MRZ لمعيار البطاقة الموحدة (TD1) على 3 أسطر على الأقل."]
            };
        }

        var l1 = lines[0].PadRight(30, '<')[..30];
        var l2 = lines[1].PadRight(30, '<')[..30];
        var l3 = lines[2].PadRight(30, '<')[..30];

        var errors = new List<string>();

        // Line 1: [0..1] Type (I/ID), [2..4] Country (IRQ), [5..13] DocNumber, [14] CheckDigit, [15..29] Optional
        var docType = l1[..2].Replace("<", "");
        var country = l1[2..5].Replace("<", "");
        var docNumber = l1[5..14].Replace("<", "");
        var optionalData = l1[15..30].Replace("<", "");

        // Line 2: [0..5] DOB (YYMMDD), [6] CheckDigit, [7] Sex, [8..13] Expiry (YYMMDD), [14] CheckDigit, [15..17] Nationality, [18..28] Optional2
        var dobRaw = l2[..6];
        var genderRaw = l2[7].ToString();
        var expiryRaw = l2[8..14];
        var nationality = l2[15..18].Replace("<", "");
        var optional2 = l2[18..30].Replace("<", "");

        DateOnly? dob = ParseMrzDate(dobRaw, isBirthDate: true);
        DateOnly? expiry = ParseMrzDate(expiryRaw, isBirthDate: false);
        var gender = genderRaw == "M" ? "ذكر" : (genderRaw == "F" ? "أنثى" : "غير محدد");

        // Line 3: Names (Surname<<GivenNames)
        var nameParts = l3.Split(new[] { "<<" }, StringSplitOptions.RemoveEmptyEntries);
        var surname = nameParts.Length > 0 ? nameParts[0].Replace('<', ' ').Trim() : string.Empty;
        var givenNames = nameParts.Length > 1 ? nameParts[1].Replace('<', ' ').Trim() : string.Empty;
        var fullNameEn = $"{givenNames} {surname}".Trim();

        // الرقم الوطني الموحد غالباً ما يوضع في OptionalData بالسطر الأول أو الثاني إذا كان 12 رقماً
        var nationalId = Regex.Match(optionalData + optional2, @"\d{12}").Value;
        if (string.IsNullOrEmpty(nationalId) && !string.IsNullOrEmpty(optionalData))
        {
            nationalId = optionalData;
        }

        return new MrzParseResultDto
        {
            IsValid = errors.Count == 0 && !string.IsNullOrEmpty(docNumber),
            DocumentType = docType,
            DocumentNumber = docNumber,
            NationalIdNumber = string.IsNullOrEmpty(nationalId) ? null : nationalId,
            IssuingCountry = country,
            Surname = surname,
            GivenNames = givenNames,
            FullNameEn = fullNameEn,
            Nationality = nationality,
            DateOfBirth = dob,
            Gender = gender,
            ExpiryDate = expiry,
            ValidationErrors = errors
        };
    }

    // ==========================================
    // دوال مساعدة خاصة (Private Helpers)
    // ==========================================

    private async Task<string> SaveBase64ImageAsync(string base64Data, string storageKey, CancellationToken ct)
    {
        var cleanBase64 = base64Data.Trim();
        var match = Regex.Match(cleanBase64, @"data:image\/[a-zA-Z]+;base64,(?<data>.+)");
        if (match.Success)
        {
            cleanBase64 = match.Groups["data"].Value;
        }

        var bytes = Convert.FromBase64String(cleanBase64);
        using var memoryStream = new MemoryStream(bytes);
        return await storage.SaveAsync(memoryStream, storageKey, ct);
    }

    private static NationalCardResponseDto MapToDto(EmployeeNationalCard card, Employee employee)
    {
        return new NationalCardResponseDto
        {
            Id = card.Id,
            EmployeeId = card.EmployeeId,
            EmployeeNumber = employee.EmployeeID,
            EmployeeName = employee.Name,
            DocumentNumber = card.DocumentNumber,
            NationalIdNumber = card.NationalIdNumber,
            FullNameAr = card.FullNameAr,
            FirstNameEn = card.FirstNameEn,
            LastNameEn = card.LastNameEn,
            FullNameEn = card.FullNameEn,
            DateOfBirth = card.DateOfBirth,
            Gender = card.Gender,
            Nationality = card.Nationality,
            IssuingState = card.IssuingState,
            IssueDate = card.IssueDate,
            ExpiryDate = card.ExpiryDate,
            RawMrz = card.RawMrz,
            HasBiometricPhoto = !string.IsNullOrWhiteSpace(card.BiometricPhotoPath),
            HasFrontImage = !string.IsNullOrWhiteSpace(card.FrontImagePath),
            HasBackImage = !string.IsNullOrWhiteSpace(card.BackImagePath),
            BiometricPhotoUrl = !string.IsNullOrWhiteSpace(card.BiometricPhotoPath) ? $"/api/national-cards/{card.Id}/biometric-photo" : null,
            FrontImageUrl = !string.IsNullOrWhiteSpace(card.FrontImagePath) ? $"/api/national-cards/{card.Id}/front-image" : null,
            BackImageUrl = !string.IsNullOrWhiteSpace(card.BackImagePath) ? $"/api/national-cards/{card.Id}/back-image" : null,
            IsPassiveAuthenticationValid = card.IsPassiveAuthenticationValid,
            VerificationTimestamp = card.VerificationTimestamp,
            SodHash = card.SodHash,
            CertificateIssuer = card.CertificateIssuer,
            IsActive = card.IsActive,
            CreatedAt = card.CreatedAt,
            CreatedBy = card.CreatedBy,
            Notes = card.Notes
        };
    }

    private static string CombineNames(string? first, string? last)
    {
        if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last)) return string.Empty;
        if (string.IsNullOrWhiteSpace(first)) return last!.Trim();
        if (string.IsNullOrWhiteSpace(last)) return first.Trim();
        return $"{first.Trim()} {last.Trim()}";
    }

    private static string? NormalizeGender(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var r = raw.Trim().ToUpperInvariant();
        if (r.Contains("M") || r.Contains("ذكر") || r == "MALE") return "ذكر";
        if (r.Contains("F") || r.Contains("أنثى") || r.Contains("انثى") || r == "FEMALE") return "أنثى";
        return raw.Trim();
    }

    private static DateOnly? ParseMrzDate(string raw, bool isBirthDate)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length < 6 || !int.TryParse(raw, out _)) return null;

        var yy = int.Parse(raw[..2]);
        var mm = int.Parse(raw[2..4]);
        var dd = int.Parse(raw[4..6]);

        if (mm < 1 || mm > 12 || dd < 1 || dd > 31) return null;

        var currentCentury = DateTime.UtcNow.Year / 100;
        var currentTwoDigitYear = DateTime.UtcNow.Year % 100;

        int fullYear;
        if (isBirthDate)
        {
            fullYear = (yy > currentTwoDigitYear) ? (currentCentury - 1) * 100 + yy : currentCentury * 100 + yy;
        }
        else
        {
            // Expiry date is usually in current century
            fullYear = currentCentury * 100 + yy;
        }

        try
        {
            return new DateOnly(fullYear, mm, dd);
        }
        catch
        {
            return null;
        }
    }
}
