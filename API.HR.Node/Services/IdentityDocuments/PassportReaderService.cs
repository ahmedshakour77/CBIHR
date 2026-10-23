using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace API.HR.Node.Services.IdentityDocuments;

/// <summary>
/// خدمة قراءة وتدقيق جوازات السفر وفق معيار ICAO 9303 TD3 والشريحة البيومترية e-Passport.
/// </summary>
public sealed class PassportReaderService : IPassportReaderService
{
    private readonly ILogger<PassportReaderService> _logger;

    public PassportReaderService(ILogger<PassportReaderService> logger)
    {
        _logger = logger;
    }

    public PassportMrzResult ParseMrz(string mrzText)
    {
        if (string.IsNullOrWhiteSpace(mrzText))
        {
            return new PassportMrzResult
            {
                IsValid = false,
                ValidationErrors = ["نص الـ MRZ فارغ."]
            };
        }

        // تنظيف وفصل الأسطر (معيار TD3 للجوازات: سطرين × 44 حرفاً)
        var lines = mrzText
            .Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => Regex.Replace(l.ToUpperInvariant(), @"[^A-Z0-9<]", ""))
            .Where(l => l.Length >= 40)
            .ToList();

        if (lines.Count < 2)
        {
            return new PassportMrzResult
            {
                IsValid = false,
                ValidationErrors = ["شريط الـ MRZ للجواز (معيار TD3) يتطلب سطرين على الأقل بطول 44 حرفاً لكل سطر."]
            };
        }

        var l1 = lines[0].PadRight(44, '<')[..44];
        var l2 = lines[1].PadRight(44, '<')[..44];

        var errors = new List<string>();

        // Line 1: [0..1] Type (P / PD / PS), [2..4] Country (IRQ), [5..43] Names (Surname<<GivenNames)
        var docTypeRaw = l1[..2].Replace("<", "");
        var docType = docTypeRaw.StartsWith("P") ? "عادي (P)"
            : docTypeRaw.StartsWith("D") ? "دبلوماسي (D)"
            : docTypeRaw.StartsWith("S") ? "خدمة (S)"
            : "عادي (P)";

        var country = l1[2..5].Replace("<", "");

        var nameSection = l1[5..44];
        var nameParts = nameSection.Split(new[] { "<<" }, StringSplitOptions.RemoveEmptyEntries);
        var surname = nameParts.Length > 0 ? nameParts[0].Replace('<', ' ').Trim() : string.Empty;
        var givenNames = nameParts.Length > 1 ? nameParts[1].Replace('<', ' ').Trim() : string.Empty;
        var fullNameEn = $"{givenNames} {surname}".Trim();

        // Line 2:
        // [0..8] Passport Number (9 chars)
        // [9] Check Digit for Passport Number
        // [10..12] Nationality (3 chars)
        // [13..18] Date of Birth (YYMMDD)
        // [19] Check Digit for DOB
        // [20] Sex (M/F/<)
        // [21..26] Expiration Date (YYMMDD)
        // [27] Check Digit for Expiry Date
        // [28..41] Personal Number / National ID (14 chars)
        // [42] Check Digit for Personal Number
        // [43] Composite Check Digit

        var passportNumber = l2[..9].Replace("<", "");
        var passNumCheck = l2[9];
        var nationality = l2[10..13].Replace("<", "");
        var dobRaw = l2[13..19];
        var dobCheck = l2[19];
        var genderRaw = l2[20].ToString();
        var expiryRaw = l2[21..27];
        var expiryCheck = l2[27];
        var personalNumRaw = l2[28..42].Replace("<", "");

        // Mathematical Check Digits Validation (Modulo 7-3-1)
        var isPassValid = VerifyCheckDigit(l2[..9], passNumCheck);
        var isDobValid = VerifyCheckDigit(dobRaw, dobCheck);
        var isExpiryValid = VerifyCheckDigit(expiryRaw, expiryCheck);

        var isCheckDigitsValid = isPassValid && isDobValid && isExpiryValid;
        if (!isPassValid) errors.Add("رقم تدقيق الجواز غير مطابق، يرجى التأكد من وضوح الصورة.");
        if (!isDobValid) errors.Add("رقم تدقيق تاريخ الميلاد غير مطابق.");
        if (!isExpiryValid) errors.Add("رقم تدقيق تاريخ انتهاء الجواز غير مطابق.");

        DateOnly? dob = ParseMrzDate(dobRaw, isBirthDate: true);
        DateOnly? expiry = ParseMrzDate(expiryRaw, isBirthDate: false);
        var gender = genderRaw == "M" ? "ذكر" : (genderRaw == "F" ? "أنثى" : "غير محدد");

        return new PassportMrzResult
        {
            IsValid = !string.IsNullOrEmpty(passportNumber),
            DocumentType = docType,
            PassportNumber = passportNumber,
            IssuingCountry = string.IsNullOrEmpty(country) ? "IRQ" : country,
            Nationality = string.IsNullOrEmpty(nationality) ? "IRQ" : nationality,
            Surname = surname,
            GivenNames = givenNames,
            FullNameEn = fullNameEn,
            DateOfBirth = dob,
            Gender = gender,
            ExpiryDate = expiry,
            NationalIdNumber = string.IsNullOrWhiteSpace(personalNumRaw) ? null : personalNumRaw,
            IsCheckDigitsValid = isCheckDigitsValid,
            ValidationErrors = errors
        };
    }

    public PassportChipResult ParseChipData(PassportChipDataRequest request)
    {
        var errors = new List<string>();

        PassportMrzResult? mrzResult = null;
        if (!string.IsNullOrWhiteSpace(request.Dg1Mrz))
        {
            mrzResult = ParseMrz(request.Dg1Mrz);
        }

        byte[]? photoBytes = null;
        string? mimeType = null;

        if (!string.IsNullOrWhiteSpace(request.Dg2BiometricPhotoBase64))
        {
            try
            {
                var cleanBase64 = request.Dg2BiometricPhotoBase64;
                if (cleanBase64.Contains(','))
                {
                    cleanBase64 = cleanBase64.Split(',')[1];
                }
                photoBytes = Convert.FromBase64String(cleanBase64);
                mimeType = "image/jpeg";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse DG2 biometric photo from chip");
                errors.Add("تعذر قراءة الصورة البيومترية من الشريحة (DG2).");
            }
        }

        return new PassportChipResult
        {
            Success = errors.Count == 0 && (mrzResult?.IsValid == true || !string.IsNullOrWhiteSpace(request.Dg11FullNameAr)),
            MrzInfo = mrzResult,
            FullNameAr = request.Dg11FullNameAr,
            MotherName = request.Dg11MotherName,
            NationalId = request.Dg11NationalId ?? mrzResult?.NationalIdNumber,
            PlaceOfBirth = request.Dg11PlaceOfBirth,
            BiometricPhotoBytes = photoBytes,
            BiometricPhotoMimeType = mimeType,
            Errors = errors
        };
    }

    /// <summary>
    /// حساب والتحقق من رقم التدقيق ICAO 9303 Modulo 7-3-1
    /// </summary>
    public static bool VerifyCheckDigit(string input, char expectedCheckDigit)
    {
        if (expectedCheckDigit == '<') expectedCheckDigit = '0';
        var calculated = CalculateCheckDigit(input);
        return calculated == expectedCheckDigit;
    }

    public static char CalculateCheckDigit(string input)
    {
        int[] weights = [7, 3, 1];
        var sum = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var c = char.ToUpperInvariant(input[i]);
            int val;
            if (c >= '0' && c <= '9') val = c - '0';
            else if (c >= 'A' && c <= 'Z') val = c - 'A' + 10;
            else val = 0; // '<' or others

            sum += val * weights[i % 3];
        }

        return (char)('0' + (sum % 10));
    }

    private static DateOnly? ParseMrzDate(string raw, bool isBirthDate)
    {
        if (raw.Length != 6 || !raw.All(char.IsDigit)) return null;

        var yy = int.Parse(raw[..2]);
        var mm = int.Parse(raw[2..4]);
        var dd = int.Parse(raw[4..6]);

        if (mm < 1 || mm > 12 || dd < 1 || dd > 31) return null;

        var currentYear = DateTime.UtcNow.Year;
        var currentCentury = currentYear / 100 * 100;
        var currentYearTwoDigit = currentYear % 100;

        int fullYear;
        if (isBirthDate)
        {
            fullYear = (yy > currentYearTwoDigit) ? (currentCentury - 100 + yy) : (currentCentury + yy);
        }
        else
        {
            fullYear = currentCentury + yy;
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
