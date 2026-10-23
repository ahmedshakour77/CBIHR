namespace API.HR.Node.Contracts;

public sealed class MartyrImportRow
{
    public int RowIndex { get; set; }

    /// <summary>الرقم الوظيفي للموظف (يربط مع الجدول الأساسي)</summary>
    public string EmployeeIdNumber { get; set; } = string.Empty;

    /// <summary>اسم الموظف في النظام (للتأكد البصري)</summary>
    public string? MatchedEmployeeName { get; set; }

    /// <summary>اسم الشهيد</summary>
    public string MartyrName { get; set; } = string.Empty;

    /// <summary>نوع الشهادة (ضحايا الإرهاب، الحشد الشعبي، النظام البائد، إلخ)</summary>
    public string MartyrdomType { get; set; } = string.Empty;

    /// <summary>الصلة (أب، أم، ابن، ابنة، أخ، أخت، زوج، زوجة، إلخ)</summary>
    public string Kinship { get; set; } = string.Empty;

    /// <summary>رقم القرار (نص حر)</summary>
    public string? DecisionNumber { get; set; }

    /// <summary>ملاحظات إضافية</summary>
    public string? Notes { get; set; }

    /// <summary>هل الموظف موجود في قاعدة البيانات؟</summary>
    public bool EmployeeExists { get; set; }

    /// <summary>هل السطر صالح للإدخال؟</summary>
    public bool IsValid { get; set; }

    /// <summary>سبب الخطأ أو حالة التحقق</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>هل هذا الشهيد مسجل مسبقاً لنفس الموظف؟</summary>
    public bool IsAlreadyRegistered { get; set; }
}

public sealed class MartyrsImportPreviewResponse
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int DuplicateOrExistingRows { get; set; }
    public List<MartyrImportRow> Rows { get; set; } = [];
}

public sealed class MartyrsImportRequest
{
    public List<MartyrImportRow> Rows { get; set; } = [];
    /// <summary>هل يتم استبدال السجلات السابقة لذوي الشهداء لنفس الموظفين أم الإضافة عليها؟</summary>
    public bool ReplaceExisting { get; set; } = false;
}

public sealed class MartyrsImportResult
{
    public bool Success { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Messages { get; set; } = [];
}
