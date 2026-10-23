namespace API.HR.Node.Services.Network;

/// <summary>
/// خدمة فحص ومتابعة حالة الاتصال بشبكة ونطاق مكتب مكافحة غسل الأموال (aml.lan)
/// لعرض الشعار المعتمد عند وجود اتصال والرجوع لشعار البنك فقط عند عدم وجود وصول.
/// </summary>
public interface IAmlConnectivityService
{
    /// <summary>
    /// هل يوجد اتصال أو وصول إلى شبكة / نطاق aml.lan؟
    /// </summary>
    bool IsAmlConnected { get; }

    /// <summary>
    /// فحص فوري لحالة الاتصال بنطاق aml.lan
    /// </summary>
    Task<bool> CheckConnectivityAsync(CancellationToken ct = default);

    /// <summary>
    /// حدث يتم إطلاقه عند تغيّر حالة الاتصال بالشبكة
    /// </summary>
    event Action<bool>? OnConnectivityChanged;
}
