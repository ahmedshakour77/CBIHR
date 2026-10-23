using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using API.HR.Node.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.HR.Node.Services.Canva;

public class CanvaService : ICanvaService
{
    private readonly HttpClient _httpClient;
    private readonly CanvaOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CanvaService> _logger;
    private readonly IWebHostEnvironment _env;

    private CanvaTokenResponse? _cachedToken;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly string _tokenFilePath;
    private readonly IDataProtector _tokenProtector;

    public CanvaService(
        HttpClient httpClient,
        IOptions<CanvaOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<CanvaService> logger,
        IWebHostEnvironment env,
        IDataProtectionProvider dataProtection)
    {
        // The access and refresh tokens act for the whole organisation: never store them readable.
        _tokenProtector = dataProtection.CreateProtector("API.HR.Node.Canva.Token.v1");
        _httpClient = httpClient;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _env = env;

        var storageDir = Path.Combine(_env.ContentRootPath, "App_Data", "Storage", "Canva");
        if (!Directory.Exists(storageDir))
        {
            Directory.CreateDirectory(storageDir);
        }
        _tokenFilePath = Path.Combine(storageDir, "canva_token.json");
    }

    public string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(challengeBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    public string GetAuthorizationUrl(string state, string codeVerifier, string? redirectUriOverride = null)
    {
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var scopes = Uri.EscapeDataString(_options.Scopes);
        var uriToUse = !string.IsNullOrWhiteSpace(redirectUriOverride) ? redirectUriOverride : _options.RedirectUri;
        var redirectUri = Uri.EscapeDataString(uriToUse);
        var clientId = Uri.EscapeDataString(_options.ClientId);
        var escapedState = Uri.EscapeDataString(state);

        return $"https://www.canva.com/api/oauth/authorize?" +
               $"code_challenge={codeChallenge}&" +
               $"code_challenge_method=s256&" +
               $"scope={scopes}&" +
               $"response_type=code&" +
               $"client_id={clientId}&" +
               $"redirect_uri={redirectUri}&" +
               $"state={escapedState}";
    }

    public async Task<(bool Success, string? ErrorMessage)> ExchangeCodeForTokenAsync(string code, string codeVerifier, string? redirectUriOverride = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.canva.com/rest/v1/oauth/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var uriToUse = !string.IsNullOrWhiteSpace(redirectUriOverride) ? redirectUriOverride : _options.RedirectUri;
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = codeVerifier,
                ["code"] = code,
                ["redirect_uri"] = uriToUse
            };
            request.Content = new FormUrlEncodedContent(form);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Canva token exchange failed with status {StatusCode}: {Content}", response.StatusCode, content);
                return (false, content);
            }

            var token = JsonSerializer.Deserialize<CanvaTokenResponse>(content);
            if (token != null)
            {
                token.CreatedUtc = DateTime.UtcNow;
                await SaveTokenAsync(token, cancellationToken);
                return (true, null);
            }

            return (false, "تعذر معالجة رمز الدخول المستلم من كانفا.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Canva code token exchange");
            return (false, ex.Message);
        }
    }

    public async Task<CanvaTokenResponse?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken == null && File.Exists(_tokenFilePath))
            {
                var stored = await File.ReadAllTextAsync(_tokenFilePath, cancellationToken);
                // Files written before encryption are plain JSON; they are re-encrypted on the next save.
                try
                {
                    var json = stored.TrimStart().StartsWith('{') ? stored : _tokenProtector.Unprotect(stored);
                    _cachedToken = JsonSerializer.Deserialize<CanvaTokenResponse>(json);
                }
                catch (CryptographicException ex)
                {
                    // Keys rotated or lost: treat as disconnected; an admin reconnects once.
                    _logger.LogWarning(ex, "Stored Canva token could not be decrypted; Canva must be reconnected.");
                }
            }

            if (_cachedToken == null)
            {
                return null;
            }

            if (_cachedToken.IsExpired && !string.IsNullOrEmpty(_cachedToken.RefreshToken))
            {
                _logger.LogInformation("Canva access token expired. Refreshing using refresh token...");
                var refreshed = await RefreshAccessTokenAsync(_cachedToken.RefreshToken, cancellationToken);
                if (refreshed != null)
                {
                    _cachedToken = refreshed;
                    await SaveTokenInternalAsync(_cachedToken, cancellationToken);
                }
            }

            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<CanvaTokenResponse?> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.canva.com/rest/v1/oauth/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            };
            request.Content = new FormUrlEncodedContent(form);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Canva token refresh failed with status {StatusCode}: {Content}", response.StatusCode, content);
                return null;
            }

            var token = JsonSerializer.Deserialize<CanvaTokenResponse>(content);
            if (token != null)
            {
                token.CreatedUtc = DateTime.UtcNow;
                if (string.IsNullOrEmpty(token.RefreshToken))
                {
                    token.RefreshToken = refreshToken;
                }
                return token;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception refreshing Canva token");
            return null;
        }
    }

    private async Task SaveTokenAsync(CanvaTokenResponse token, CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            _cachedToken = token;
            await SaveTokenInternalAsync(token, cancellationToken);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task SaveTokenInternalAsync(CanvaTokenResponse token, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(token);
        await File.WriteAllTextAsync(_tokenFilePath, _tokenProtector.Protect(json), cancellationToken);
    }

    public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync(cancellationToken);
        return token != null && !string.IsNullOrEmpty(token.AccessToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            _cachedToken = null;
            if (File.Exists(_tokenFilePath))
            {
                File.Delete(_tokenFilePath);
            }
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task<List<CanvaBrandTemplateItem>> GetBrandTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync(cancellationToken);
        if (token == null)
        {
            _logger.LogWarning("Cannot get brand templates: not authenticated with Canva.");
            return [];
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.canva.com/rest/v1/brand-templates");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GetBrandTemplates failed with status {StatusCode}: {Content}", response.StatusCode, content);
                return [];
            }

            var result = JsonSerializer.Deserialize<CanvaBrandTemplatesResponse>(content);
            return result?.Items ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting Canva brand templates");
            return [];
        }
    }

    public async Task<CanvaDatasetResponse?> GetBrandTemplateDatasetAsync(string brandTemplateId, CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync(cancellationToken);
        if (token == null) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.canva.com/rest/v1/brand-templates/{brandTemplateId}/dataset");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GetBrandTemplateDataset failed for {Id}: {Content}", brandTemplateId, content);
                return null;
            }

            return JsonSerializer.Deserialize<CanvaDatasetResponse>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting brand template dataset");
            return null;
        }
    }

    public async Task<CanvaAutofillJob?> CreateAutofillDesignAsync(
        string brandTemplateId,
        string title,
        Dictionary<string, string> textData,
        CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync(cancellationToken);
        if (token == null)
        {
            throw new InvalidOperationException("الحساب غير متصل بمنصة Canva. يرجى تسجيل الدخول أولاً.");
        }

        var payload = new CanvaAutofillCreateRequest
        {
            BrandTemplateId = brandTemplateId,
            Title = title,
            Data = textData.ToDictionary(
                kv => kv.Key,
                kv => new CanvaAutofillDataField { Type = "text", Text = kv.Value })
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.canva.com/rest/v1/autofills");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("CreateAutofill failed: {StatusCode} - {Content}", response.StatusCode, content);
            if (content.Contains("design_not_fillable", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("القالب المحدد في Canva لا يحتوي على حقول بيانات مفعّلة (Autofill Data Fields). يرجى فتح القالب في كانفا وتعيين العناصر المراد تعبئتها كـ Data Field ثم إعادة حفظه كقالب علامة تجارية.");
            }
            throw new HttpRequestException($"فشل إنشاء التقرير في كانفا: {content}");
        }

        var initialJob = JsonSerializer.Deserialize<CanvaAutofillJobResponse>(content)?.Job;
        if (initialJob == null)
        {
            throw new InvalidOperationException("استجابة كانفا غير صالحة.");
        }

        // Poll until success or failed (maximum 20 seconds)
        var jobId = initialJob.Id;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(1000, cancellationToken);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.canva.com/rest/v1/autofills/{jobId}");
            pollRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            var pollResponse = await _httpClient.SendAsync(pollRequest, cancellationToken);
            var pollContent = await pollResponse.Content.ReadAsStringAsync(cancellationToken);

            if (pollResponse.IsSuccessStatusCode)
            {
                var jobStatus = JsonSerializer.Deserialize<CanvaAutofillJobResponse>(pollContent)?.Job;
                if (jobStatus != null)
                {
                    if (jobStatus.Status.Equals("success", StringComparison.OrdinalIgnoreCase))
                    {
                        return jobStatus;
                    }
                    if (jobStatus.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogError("Canva autofill job failed: {Error}", jobStatus.Error?.Message);
                        return jobStatus;
                    }
                }
            }
        }

        return initialJob;
    }

    public async Task<CanvaExportJob?> ExportDesignAsync(string designId, string format = "pdf", CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync(cancellationToken);
        if (token == null) return null;

        var payload = new CanvaExportCreateRequest
        {
            DesignId = designId,
            Format = new CanvaExportFormat { Type = format.ToLowerInvariant() }
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.canva.com/rest/v1/exports");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ExportDesign failed: {Content}", content);
            return null;
        }

        var initialJob = JsonSerializer.Deserialize<CanvaExportJobResponse>(content)?.Job;
        if (initialJob == null) return null;

        var jobId = initialJob.Id;
        for (int i = 0; i < 25; i++)
        {
            await Task.Delay(1000, cancellationToken);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.canva.com/rest/v1/exports/{jobId}");
            pollRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

            var pollResponse = await _httpClient.SendAsync(pollRequest, cancellationToken);
            var pollContent = await pollResponse.Content.ReadAsStringAsync(cancellationToken);

            if (pollResponse.IsSuccessStatusCode)
            {
                var jobStatus = JsonSerializer.Deserialize<CanvaExportJobResponse>(pollContent)?.Job;
                if (jobStatus != null)
                {
                    if (jobStatus.Status.Equals("success", StringComparison.OrdinalIgnoreCase) ||
                        jobStatus.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
                    {
                        return jobStatus;
                    }
                }
            }
        }

        return initialJob;
    }

    public async Task<HrCanvaReportPayload> BuildCurrentHrStatisticsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var totalEmployees = await db.Employees.AsNoTracking().CountAsync(cancellationToken);

        var femaleCount = await db.Employees.AsNoTracking()
            .CountAsync(e => e.Gender != null && (e.Gender.Name.Contains("أنث") || e.Gender.Name.Contains("انث") || e.Gender.Name.Equals("Female")), cancellationToken);
        var maleCount = Math.Max(0, totalEmployees - femaleCount);

        var malePct = totalEmployees > 0 ? Math.Round((double)maleCount * 100.0 / totalEmployees, 1) : 0;
        var femalePct = totalEmployees > 0 ? Math.Round((double)femaleCount * 100.0 / totalEmployees, 1) : 0;

        var totalFormations = await db.Formations.AsNoTracking().CountAsync(cancellationToken);

        // Demographics
        var today = DateOnly.FromDateTime(DateTime.Today);
        var employeesDemographics = await db.Employees.AsNoTracking()
            .Select(e => new
            {
                e.BirthDate,
                e.HireDate,
                Education = e.EducationLevel != null ? e.EducationLevel.Name : "",
                Formation = e.Formation != null ? e.Formation.Name : "",
                Job = e.JobTitle != null ? e.JobTitle.Name : "",
                GradeCode = e.Grade != null ? e.Grade.Code : "",
                GradeName = e.Grade != null ? e.Grade.Name : "",
                StageNumber = e.Stage != null ? (e.Stage.StageNumber ?? 0) : 0
            })
            .ToListAsync(cancellationToken);

        int ageRecorded = 0;
        double totalAge = 0;
        int nearRetirement = 0;

        int hireRecorded = 0;
        double totalService = 0;
        int newHires = 0;

        int postgradCount = 0;

        foreach (var emp in employeesDemographics)
        {
            if (emp.BirthDate.HasValue)
            {
                var age = today.Year - emp.BirthDate.Value.Year;
                if (emp.BirthDate.Value > today.AddYears(-age)) age--;
                if (age >= 18 && age <= 90)
                {
                    totalAge += age;
                    ageRecorded++;
                    if (age >= 58) nearRetirement++;
                }
            }

            if (emp.HireDate.HasValue)
            {
                var service = today.Year - emp.HireDate.Value.Year;
                if (emp.HireDate.Value > today.AddYears(-service)) service--;
                service = Math.Max(0, service);
                totalService += service;
                hireRecorded++;
                if (service <= 2) newHires++;
            }

            if (!string.IsNullOrWhiteSpace(emp.Education) &&
                (emp.Education.Contains("دكتوراه") || emp.Education.Contains("ماجستير") || emp.Education.Contains("دبلوم عالي")))
            {
                postgradCount++;
            }
        }

        var avgAge = ageRecorded > 0 ? Math.Round(totalAge / ageRecorded, 1) : 0;
        var avgService = hireRecorded > 0 ? Math.Round(totalService / hireRecorded, 1) : 0;
        var postgradPct = totalEmployees > 0 ? Math.Round((double)postgradCount * 100.0 / totalEmployees, 1) : 0;
        var nearRetirePct = totalEmployees > 0 ? Math.Round((double)nearRetirement * 100.0 / totalEmployees, 1) : 0;

        // Top formations
        var topFormations = employeesDemographics
            .Where(e => !string.IsNullOrWhiteSpace(e.Formation))
            .GroupBy(e => e.Formation)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => (Name: g.Key, Count: g.Count()))
            .ToList();

        // Top job titles
        var topJobs = employeesDemographics
            .Where(e => !string.IsNullOrWhiteSpace(e.Job))
            .GroupBy(e => e.Job)
            .OrderByDescending(g => g.Count())
            .Take(2)
            .Select(g => (Name: g.Key, Count: g.Count()))
            .ToList();

        // Peak Grade Stage
        var peakGroup = employeesDemographics
            .Where(e => !string.IsNullOrWhiteSpace(e.GradeCode) && e.StageNumber > 0)
            .GroupBy(e => $"{e.GradeCode}-{e.StageNumber}")
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        var peakText = peakGroup != null ? $"الدرجة {peakGroup.Key.Split('-')[0]} · المرحلة {peakGroup.Key.Split('-')[1]} ({peakGroup.Count()} موظف)" : "الدرجة 4 · المرحلة 3";

        // Active leaves
        var todayDate = DateOnly.FromDateTime(DateTime.Today);
        var activeLeaves = await db.EmployeeLeaves.AsNoTracking()
            .CountAsync(l => l.StartDate <= todayDate && l.EndDate >= todayDate && l.Status != "ملغاة" && l.Status != "مرفوضة", cancellationToken);

        var honoredCount = await db.AppreciationLetters.AsNoTracking()
            .Select(l => l.EmployeeId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new HrCanvaReportPayload
        {
            TotalEmployees = totalEmployees,
            MaleCount = maleCount,
            MalePct = malePct,
            FemaleCount = femaleCount,
            FemalePct = femalePct,
            AverageAge = avgAge,
            AverageServiceYears = avgService,
            PostgraduateCount = postgradCount,
            PostgraduatePct = postgradPct,
            NearRetirementCount = nearRetirement,
            NearRetirementPct = nearRetirePct,
            NewHiresCount = newHires,
            ActiveLeavesToday = activeLeaves,
            TotalFormations = totalFormations,
            HonoredCount = honoredCount,
            TopFormation1Name = topFormations.Count > 0 ? topFormations[0].Name : "مديرية شؤون الموارد البشرية",
            TopFormation1Count = topFormations.Count > 0 ? topFormations[0].Count : 0,
            TopFormation2Name = topFormations.Count > 1 ? topFormations[1].Name : "المديرية العامة للمحاسبة",
            TopFormation2Count = topFormations.Count > 1 ? topFormations[1].Count : 0,
            TopFormation3Name = topFormations.Count > 2 ? topFormations[2].Name : "دائرة الصيرفة والائتمان",
            TopFormation3Count = topFormations.Count > 2 ? topFormations[2].Count : 0,
            TopJobTitle1Name = topJobs.Count > 0 ? topJobs[0].Name : "رئيس ملاحظين",
            TopJobTitle1Count = topJobs.Count > 0 ? topJobs[0].Count : 0,
            TopJobTitle2Name = topJobs.Count > 1 ? topJobs[1].Name : "ملاحظ",
            TopJobTitle2Count = topJobs.Count > 1 ? topJobs[1].Count : 0,
            PeakGradeStage = peakText
        };
    }
}
