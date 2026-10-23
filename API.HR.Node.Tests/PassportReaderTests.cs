using API.HR.Node.Services.IdentityDocuments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.HR.Node.Tests;

public sealed class PassportReaderTests
{
    private readonly PassportReaderService _service = new(NullLogger<PassportReaderService>.Instance);

    [Fact]
    public void CalculateCheckDigit_CalculatesAccurateModulo731()
    {
        // Passport number A12345678
        var checkDigit = PassportReaderService.CalculateCheckDigit("A12345678");
        checkDigit.Should().Be('4');

        // Date of birth 850715 (1985-07-15)
        var dobCheck = PassportReaderService.CalculateCheckDigit("850715");
        dobCheck.Should().Be('8');

        // Expiry date 320715 (2032-07-15)
        var expCheck = PassportReaderService.CalculateCheckDigit("320715");
        expCheck.Should().Be('4');
    }

    [Fact]
    public void ParseMrz_ValidIraqiPassport_ExtractsAndValidatesAccurately()
    {
        // Line 1: Type (P), Country (IRQ), Surname (AL), Given names (AHMED ALI)
        var line1 = "P<IRQAL<<AHMED<ALI<<<<<<<<<<<<<<<<<<<<<<<<<<";
        // Line 2: Passport A12345678, check 4, Nationality IRQ, DOB 850715, check 8, Sex M, Expiry 320715, check 4, National ID 198512345678
        var line2 = "A123456784IRQ8507158M3207154198512345678<<<<0";

        var mrz = $"{line1}\n{line2}";

        var result = _service.ParseMrz(mrz);

        result.IsValid.Should().BeTrue();
        result.IsCheckDigitsValid.Should().BeTrue();
        result.PassportNumber.Should().Be("A12345678");
        result.IssuingCountry.Should().Be("IRQ");
        result.Nationality.Should().Be("IRQ");
        result.Surname.Should().Be("AL");
        result.GivenNames.Should().Be("AHMED ALI");
        result.FullNameEn.Should().Be("AHMED ALI AL");
        result.Gender.Should().Be("ذكر");
        result.DateOfBirth.Should().Be(new DateOnly(1985, 7, 15));
        result.ExpiryDate.Should().Be(new DateOnly(2032, 7, 15));
        result.NationalIdNumber.Should().Be("198512345678");
        result.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public void ParseMrz_MismatchedCheckDigit_FlagsCheckDigitError()
    {
        var line1 = "P<IRQAL<<AHMED<ALI<<<<<<<<<<<<<<<<<<<<<<<<<<";
        // Check digit for passport number is intentionally wrong ('9' instead of '4')
        var line2 = "A123456789IRQ8507158M3207154198512345678<<<<0";

        var mrz = $"{line1}\n{line2}";

        var result = _service.ParseMrz(mrz);

        result.IsValid.Should().BeTrue();
        result.IsCheckDigitsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.Contains("تدقيق الجواز غير مطابق"));
    }

    [Fact]
    public void ParseMrz_IncompleteOrEmptyLines_ReturnsInvalidResult()
    {
        var resultEmpty = _service.ParseMrz("");
        resultEmpty.IsValid.Should().BeFalse();
        resultEmpty.ValidationErrors.Should().NotBeEmpty();

        var resultOneLine = _service.ParseMrz("P<IRQAL<<AHMED<ALI<<<<<<<<<<<<<<<<<<<<<<<<<<");
        resultOneLine.IsValid.Should().BeFalse();
        resultOneLine.ValidationErrors.Should().Contain(e => e.Contains("سطرين على الأقل"));
    }

    [Fact]
    public void ParseChipData_WithDg1_Dg2_Dg11_ExtractsAllBiometricAndArabicData()
    {
        var line1 = "P<IRQAL<<AHMED<ALI<<<<<<<<<<<<<<<<<<<<<<<<<<";
        var line2 = "A123456784IRQ8507158M3207154198512345678<<<<0";
        var fakeJpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00 };
        var photoBase64 = Convert.ToBase64String(fakeJpegBytes);

        var request = new PassportChipDataRequest
        {
            Dg1Mrz = $"{line1}\n{line2}",
            Dg2BiometricPhotoBase64 = photoBase64,
            Dg11FullNameAr = "أحمد علي حسين آل مهدي",
            Dg11MotherName = "فاطمة حسن كاظم",
            Dg11PlaceOfBirth = "بغداد - الكرخ",
            Dg11NationalId = "198512345678"
        };

        var result = _service.ParseChipData(request);

        result.Success.Should().BeTrue();
        result.FullNameAr.Should().Be("أحمد علي حسين آل مهدي");
        result.MotherName.Should().Be("فاطمة حسن كاظم");
        result.PlaceOfBirth.Should().Be("بغداد - الكرخ");
        result.NationalId.Should().Be("198512345678");

        result.MrzInfo.Should().NotBeNull();
        result.MrzInfo!.PassportNumber.Should().Be("A12345678");
        result.MrzInfo.FullNameEn.Should().Be("AHMED ALI AL");

        result.BiometricPhotoBytes.Should().NotBeNull();
        result.BiometricPhotoBytes.Should().Equal(fakeJpegBytes);
        result.BiometricPhotoMimeType.Should().Be("image/jpeg");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ParseChipData_CorruptedPhotoBase64_ReportsErrorGracefully()
    {
        var request = new PassportChipDataRequest
        {
            Dg1Mrz = "P<IRQAL<<AHMED<ALI<<<<<<<<<<<<<<<<<<<<<<<<<<\nA123456784IRQ8507158M3207154198512345678<<<<0",
            Dg2BiometricPhotoBase64 = "THIS_IS_NOT_VALID_BASE64_!@#$%"
        };

        var result = _service.ParseChipData(request);

        result.Errors.Should().Contain(e => e.Contains("الصورة البيومترية"));
        result.BiometricPhotoBytes.Should().BeNull();
    }
}
