namespace API.HR.Node.Data.Allowances;

/// <summary>
/// نوع احتساب المخصص: نسبية (مضروبة في الراتب الاسمي) أو مقطوعة (مبلغ ثابت)
/// </summary>
public enum AllowanceCalculationType
{
    /// <summary>
    /// مخصصات نسبية (تعتمد نسبة مئوية تضرب في الراتب الاسمي)
    /// </summary>
    Percentage = 1,

    /// <summary>
    /// مخصصات ثابتة / مقطوعة (مبلغ نقدي محدد بالدينار العراقي)
    /// </summary>
    Fixed = 2
}
