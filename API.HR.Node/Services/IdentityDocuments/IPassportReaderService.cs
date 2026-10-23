namespace API.HR.Node.Services.IdentityDocuments;

public interface IPassportReaderService
{
    /// <summary>
    /// فك تشفير وتدقيق شريط الـ MRZ لمعيار ICAO 9303 TD3 (سطرين × 44 حرفاً للجواز العراقي والدولي)
    /// مع التحقق الرياضي من أرقام التدقيق (Check Digits - Modulo 7-3-1).
    /// </summary>
    PassportMrzResult ParseMrz(string mrzText);

    /// <summary>
    /// قراءة وتفريغ بيانات الشريحة الإلكترونية البيومترية (e-Passport Chip: DG1, DG2, DG11)
    /// </summary>
    PassportChipResult ParseChipData(PassportChipDataRequest request);
}
