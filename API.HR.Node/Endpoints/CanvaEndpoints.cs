using API.HR.Node.Security;
using API.HR.Node.Services.Canva;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace API.HR.Node.Endpoints;

public static class CanvaEndpoints
{
    public static IEndpointRouteBuilder MapCanvaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/canva")
            .WithTags("Canva Integration")
            // One organisation-wide Canva connection: only the people who run reports may change it.
            .RequireAuthorization(AuthorizationPolicies.ManageCanva);

        // 1. Initiate Canva OAuth2 Authorization
        group.MapGet("/connect", (
            [FromServices] ICanvaService canvaService,
            [FromServices] IMemoryCache memoryCache,
            HttpContext httpContext,
            [FromQuery] string? returnUrl,
            [FromQuery] string? redirectUri) =>
        {
            var state = Guid.NewGuid().ToString("N");
            var codeVerifier = canvaService.GenerateCodeVerifier();

            var scheme = httpContext.Request.Scheme;
            var host = httpContext.Request.Host.Value;
            var effectiveRedirectUri = IsCallbackUri(redirectUri)
                ? redirectUri!
                : $"{scheme}://{host}/api/canva/callback";

            var cacheKey = $"canva_oauth_state_{state}";
            memoryCache.Set(cacheKey, (codeVerifier, IsLocalPath(returnUrl) ? returnUrl! : "/canva-reports", effectiveRedirectUri), TimeSpan.FromMinutes(15));

            var authUrl = canvaService.GetAuthorizationUrl(state, codeVerifier, effectiveRedirectUri);
            return Results.Redirect(authUrl);
        });

        // 2. Canva OAuth2 Callback (Must be anonymous as Canva redirects back directly)
        group.MapGet("/callback", async (
            [FromServices] ICanvaService canvaService,
            [FromServices] IMemoryCache memoryCache,
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            [FromQuery] string? error_description) =>
        {
            if (!string.IsNullOrEmpty(error))
            {
                return Results.Redirect($"/canva-reports?error={Uri.EscapeDataString(error_description ?? error)}");
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return Results.Redirect("/canva-reports?error=missing_code_or_state");
            }

            var cacheKey = $"canva_oauth_state_{state}";
            if (!memoryCache.TryGetValue<(string codeVerifier, string returnUrl, string redirectUri)>(cacheKey, out var cacheData))
            {
                return Results.Redirect("/canva-reports?error=invalid_or_expired_state");
            }

            memoryCache.Remove(cacheKey);

            var (success, errorMsg) = await canvaService.ExchangeCodeForTokenAsync(code, cacheData.codeVerifier, cacheData.redirectUri);
            if (!success)
            {
                var err = !string.IsNullOrWhiteSpace(errorMsg) ? Uri.EscapeDataString(errorMsg) : "token_exchange_failed";
                return Results.Redirect($"/canva-reports?error={err}");
            }

            var target = cacheData.returnUrl.Contains('?') 
                ? $"{cacheData.returnUrl}&connected=true" 
                : $"{cacheData.returnUrl}?connected=true";

            return Results.Redirect(target);
        })
        .AllowAnonymous();

        // 3. Status
        group.MapGet("/status", async ([FromServices] ICanvaService canvaService) =>
        {
            var isConnected = await canvaService.IsConnectedAsync();
            var token = await canvaService.GetTokenAsync();
            return Results.Ok(new
            {
                IsConnected = isConnected,
                ExpiresAtUtc = token?.ExpiresAtUtc,
                Scope = token?.Scope
            });
        });

        // 4. Disconnect
        group.MapPost("/disconnect", async ([FromServices] ICanvaService canvaService) =>
        {
            await canvaService.DisconnectAsync();
            return Results.Ok(new { message = "تم إلغاء الاتصال بنجاح." });
        });

        // 5. Available Brand Templates
        group.MapGet("/templates", async ([FromServices] ICanvaService canvaService) =>
        {
            var isConnected = await canvaService.IsConnectedAsync();
            if (!isConnected)
            {
                return Results.Unauthorized();
            }

            var templates = await canvaService.GetBrandTemplatesAsync();
            return Results.Ok(templates);
        });

        // 6. Preview Live HR Statistics Payload
        group.MapGet("/preview-data", async ([FromServices] ICanvaService canvaService) =>
        {
            var stats = await canvaService.BuildCurrentHrStatisticsAsync();
            return Results.Ok(new
            {
                Payload = stats,
                DataDictionary = stats.ToDatasetDictionary()
            });
        });

        // 7. Generate Autofilled Design in Canva
        group.MapPost("/generate", async (
            [FromServices] ICanvaService canvaService,
            [FromBody] CanvaGenerateRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.BrandTemplateId))
            {
                return Results.BadRequest(new { error = "معرف القالب (BrandTemplateId) مطلوب." });
            }

            var stats = await canvaService.BuildCurrentHrStatisticsAsync();
            var textData = stats.ToDatasetDictionary();

            // Allow custom overrides if passed
            if (request.CustomFields != null)
            {
                foreach (var (k, v) in request.CustomFields)
                {
                    textData[k] = v;
                }
            }

            var title = string.IsNullOrWhiteSpace(request.Title)
                ? $"تقرير إحصائيات الموارد البشرية - {DateTime.Now:yyyy/MM/dd}"
                : request.Title;

            try
            {
                var job = await canvaService.CreateAutofillDesignAsync(request.BrandTemplateId, title, textData);
                return Results.Ok(new
                {
                    JobId = job?.Id,
                    Status = job?.Status,
                    Design = job?.Result?.Design,
                    Error = job?.Error
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        // 8. Export Design to PDF
        group.MapPost("/export", async (
            [FromServices] ICanvaService canvaService,
            [FromBody] CanvaExportRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.DesignId))
            {
                return Results.BadRequest(new { error = "معرف التصميم (DesignId) مطلوب." });
            }

            var job = await canvaService.ExportDesignAsync(request.DesignId, request.Format ?? "pdf");
            return Results.Ok(new
            {
                JobId = job?.Id,
                Status = job?.Status,
                DownloadUrls = job?.Urls,
                Error = job?.Error
            });
        });

        return app;
    }

    /// <summary>A same-site path only: blocks redirects to another site after the OAuth round trip.</summary>
    private static bool IsLocalPath(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.StartsWith('/')
        && !url.StartsWith("//")
        && !url.StartsWith("/\\")
        && Uri.TryCreate(url, UriKind.Relative, out _);

    /// <summary>An override must still point at this app's callback route.</summary>
    private static bool IsCallbackUri(string? uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var u)
        && (u.Scheme == Uri.UriSchemeHttps || u.IsLoopback)
        && u.AbsolutePath.Equals("/api/canva/callback", StringComparison.OrdinalIgnoreCase)
        && string.IsNullOrEmpty(u.Query);
}

public class CanvaGenerateRequest
{
    public string BrandTemplateId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public Dictionary<string, string>? CustomFields { get; set; }
}

public class CanvaExportRequest
{
    public string DesignId { get; set; } = string.Empty;
    public string? Format { get; set; } = "pdf";

}
