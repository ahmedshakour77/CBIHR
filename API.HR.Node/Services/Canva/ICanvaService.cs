namespace API.HR.Node.Services.Canva;

public interface ICanvaService
{
    string GenerateCodeVerifier();
    string GetAuthorizationUrl(string state, string codeVerifier, string? redirectUriOverride = null);
    Task<(bool Success, string? ErrorMessage)> ExchangeCodeForTokenAsync(string code, string codeVerifier, string? redirectUriOverride = null, CancellationToken cancellationToken = default);
    Task<CanvaTokenResponse?> GetTokenAsync(CancellationToken cancellationToken = default);
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<List<CanvaBrandTemplateItem>> GetBrandTemplatesAsync(CancellationToken cancellationToken = default);
    Task<CanvaDatasetResponse?> GetBrandTemplateDatasetAsync(string brandTemplateId, CancellationToken cancellationToken = default);
    Task<CanvaAutofillJob?> CreateAutofillDesignAsync(string brandTemplateId, string title, Dictionary<string, string> textData, CancellationToken cancellationToken = default);
    Task<CanvaExportJob?> ExportDesignAsync(string designId, string format = "pdf", CancellationToken cancellationToken = default);
    Task<HrCanvaReportPayload> BuildCurrentHrStatisticsAsync(CancellationToken cancellationToken = default);
}
