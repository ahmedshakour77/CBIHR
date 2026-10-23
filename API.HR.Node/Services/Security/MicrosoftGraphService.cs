using System.Net.Http.Headers;
using System.Text.Json;

namespace API.HR.Node.Services.Security;

public class MicrosoftGraphService : IMicrosoftGraphService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MicrosoftGraphService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MicrosoftGraphService(IHttpClientFactory httpClientFactory, ILogger<MicrosoftGraphService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<MicrosoftUserProfile?> GetUserProfileAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var trimmedToken = accessToken.Trim();

        // A valid Microsoft OAuth/OIDC JWT token must be in 3-part Compact Serialization Format (Header.Payload.Signature containing 2 dots '.').
        // Skip calling Microsoft Graph API if the token is incomplete or non-JWT string.
        if (trimmedToken.Split('.').Length < 3)
        {
            _logger.LogDebug("Skipping Microsoft Graph API call because the provided token is not a complete 3-part JWT format.");
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("MicrosoftGraph");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());

            var response = await client.GetAsync("https://graph.microsoft.com/v1.0/me", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Microsoft Graph API /v1.0/me returned status code {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode, errorBody);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var profile = JsonSerializer.Deserialize<MicrosoftUserProfile>(jsonContent, JsonOptions);

            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while calling Microsoft Graph API /v1.0/me.");
            return null;
        }
    }
}
