namespace API.HR.Node.Services.IdentityDocuments;

public interface INationalCardService
{
    /// <summary>
    /// تسجيل أو تحديث البطاقة الوطنية الموحدة لموظف محدد مع حفظ الصور البيومترية والممسوحة ضوئياً وتدقيق الـ SOD
    /// </summary>
    Task<NationalCardResponseDto> RegisterOrUpdateAsync(int employeeId, NationalCardRegisterRequest request, string? userOrSystem, CancellationToken cancellationToken = default);

    /// <summary>
    /// جلب البطاقة الوطنية الموحدة النشطة لموظف محدد بواسطة المعرّف الداخلي
    /// </summary>
    Task<NationalCardResponseDto?> GetByEmployeeIdAsync(int employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// جلب البطاقة الوطنية الموحدة النشطة لموظف محدد بواسطة رقمه الوظيفي
    /// </summary>
    Task<NationalCardResponseDto?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// البحث عن بطاقة وطنية بواسطة رقم الوثيقة أو الرقم الوطني الموحد (12 رقماً)
    /// </summary>
    Task<NationalCardResponseDto?> GetByDocumentOrNationalIdAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// استرجاع ملف الصورة (صورة بيومترية DG2، واجهة أمامية، أو خلفية) لدفقها للمتصفح أو التطبيق
    /// </summary>
    Task<(Stream? Stream, string ContentType)?> GetImageAsync(int cardId, string imageType, CancellationToken cancellationToken = default);

    /// <summary>
    /// فك تشفير والتحقق من أسطر الـ MRZ لمعيار ICAO 9303 TD1 (البطاقة الوطنية)
    /// </summary>
    MrzParseResultDto ParseMrz(string mrzText);
}
