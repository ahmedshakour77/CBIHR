using System.Text.Json.Serialization;

namespace API.HR.Node.Services.Canva;

public class CanvaTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public DateTime ExpiresAtUtc => CreatedUtc.AddSeconds(ExpiresIn > 0 ? ExpiresIn : 14400);

    [JsonIgnore]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc.AddMinutes(-5);
}

public class CanvaBrandTemplatesResponse
{
    [JsonPropertyName("items")]
    public List<CanvaBrandTemplateItem> Items { get; set; } = [];

    [JsonPropertyName("continuation")]
    public string? Continuation { get; set; }
}

public class CanvaBrandTemplateItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("view_url")]
    public string? ViewUrl { get; set; }

    [JsonPropertyName("thumbnail")]
    public CanvaThumbnail? Thumbnail { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public long UpdatedAt { get; set; }
}

public class CanvaThumbnail
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }
}

public class CanvaDatasetResponse
{
    [JsonPropertyName("dataset")]
    public Dictionary<string, CanvaDatasetFieldInfo>? Dataset { get; set; }
}

public class CanvaDatasetFieldInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";
}

public class CanvaAutofillCreateRequest
{
    [JsonPropertyName("brand_template_id")]
    public string BrandTemplateId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, CanvaAutofillDataField> Data { get; set; } = [];
}

public class CanvaAutofillDataField
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("asset_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssetId { get; set; }
}

public class CanvaAutofillJobResponse
{
    [JsonPropertyName("job")]
    public CanvaAutofillJob Job { get; set; } = new();
}

public class CanvaAutofillJob
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "in_progress"; // in_progress | success | failed

    [JsonPropertyName("result")]
    public CanvaAutofillResult? Result { get; set; }

    [JsonPropertyName("error")]
    public CanvaApiError? Error { get; set; }
}

public class CanvaAutofillResult
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("design")]
    public CanvaDesign? Design { get; set; }
}

public class CanvaDesign
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("thumbnail")]
    public CanvaThumbnail? Thumbnail { get; set; }
}

public class CanvaExportCreateRequest
{
    [JsonPropertyName("design_id")]
    public string DesignId { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public CanvaExportFormat Format { get; set; } = new();
}

public class CanvaExportFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "pdf"; // pdf | png | jpg
}

public class CanvaExportJobResponse
{
    [JsonPropertyName("job")]
    public CanvaExportJob Job { get; set; } = new();
}

public class CanvaExportJob
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "in_progress"; // in_progress | success | failed

    [JsonPropertyName("urls")]
    public List<string>? Urls { get; set; }

    [JsonPropertyName("error")]
    public CanvaApiError? Error { get; set; }
}

public class CanvaApiError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class HrCanvaReportPayload
{
    public string ReportTitle { get; set; } = "التقرير الإحصائي الشامل للكوادر والموارد البشرية";
    public string ReportPeriod { get; set; } = DateTime.Now.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ar-IQ"));
    public string GeneratedDate { get; set; } = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
    
    // Core KPIs
    public int TotalEmployees { get; set; }
    public int MaleCount { get; set; }
    public double MalePct { get; set; }
    public int FemaleCount { get; set; }
    public double FemalePct { get; set; }

    // Demographics & Service
    public double AverageAge { get; set; }
    public double AverageServiceYears { get; set; }
    public int PostgraduateCount { get; set; }
    public double PostgraduatePct { get; set; }
    public int NearRetirementCount { get; set; }
    public double NearRetirementPct { get; set; }
    public int NewHiresCount { get; set; }

    // Operations & Leaves
    public int ActiveLeavesToday { get; set; }
    public int TotalFormations { get; set; }
    public int HonoredCount { get; set; }

    // Top Formations (1, 2, 3)
    public string TopFormation1Name { get; set; } = string.Empty;
    public int TopFormation1Count { get; set; }
    public string TopFormation2Name { get; set; } = string.Empty;
    public int TopFormation2Count { get; set; }
    public string TopFormation3Name { get; set; } = string.Empty;
    public int TopFormation3Count { get; set; }

    // Top Titles
    public string TopJobTitle1Name { get; set; } = string.Empty;
    public int TopJobTitle1Count { get; set; }
    public string TopJobTitle2Name { get; set; } = string.Empty;
    public int TopJobTitle2Count { get; set; }

    // Peak Grade Stage
    public string PeakGradeStage { get; set; } = string.Empty;

    public Dictionary<string, string> ToDatasetDictionary()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["report_title"] = ReportTitle,
            ["report_period"] = ReportPeriod,
            ["generated_date"] = GeneratedDate,
            ["total_employees"] = TotalEmployees.ToString("N0"),
            ["male_count"] = MaleCount.ToString("N0"),
            ["male_pct"] = $"{MalePct:F1}%",
            ["female_count"] = FemaleCount.ToString("N0"),
            ["female_pct"] = $"{FemalePct:F1}%",
            ["average_age"] = $"{AverageAge:F1}",
            ["average_service"] = $"{AverageServiceYears:F1}",
            ["postgraduate_count"] = PostgraduateCount.ToString("N0"),
            ["postgraduate_pct"] = $"{PostgraduatePct:F1}%",
            ["near_retirement_count"] = NearRetirementCount.ToString("N0"),
            ["near_retirement_pct"] = $"{NearRetirementPct:F1}%",
            ["new_hires_count"] = NewHiresCount.ToString("N0"),
            ["active_leaves_today"] = ActiveLeavesToday.ToString("N0"),
            ["total_formations"] = TotalFormations.ToString(),
            ["honored_count"] = HonoredCount.ToString("N0"),
            ["top_formation_1_name"] = TopFormation1Name,
            ["top_formation_1_count"] = TopFormation1Count.ToString("N0"),
            ["top_formation_2_name"] = TopFormation2Name,
            ["top_formation_2_count"] = TopFormation2Count.ToString("N0"),
            ["top_formation_3_name"] = TopFormation3Name,
            ["top_formation_3_count"] = TopFormation3Count.ToString("N0"),
            ["top_job_1_name"] = TopJobTitle1Name,
            ["top_job_1_count"] = TopJobTitle1Count.ToString("N0"),
            ["top_job_2_name"] = TopJobTitle2Name,
            ["top_job_2_count"] = TopJobTitle2Count.ToString("N0"),
            ["peak_grade_stage"] = PeakGradeStage
        };
    }
}
