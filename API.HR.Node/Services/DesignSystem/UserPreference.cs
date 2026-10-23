using System.ComponentModel.DataAnnotations;
using API.HR.Node.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API.HR.Node.Services.DesignSystem;

public enum GridDensity
{
    Compact,
    Comfortable,
    Spacious
}

public enum AppearanceMode
{
    Light,
    Dark,
    System
}

public class UserPreference
{
    [Key]
    public string UserId { get; set; } = string.Empty;
    public string Language { get; set; } = "ar";
    public AppearanceMode Appearance { get; set; } = AppearanceMode.Light;
    public string BrandTheme { get; set; } = "purple";
    public string? CustomBrandHex { get; set; }
    public GridDensity Density { get; set; } = GridDensity.Comfortable;
    public string Radius { get; set; } = "rounded"; // sharp, soft, rounded
    public string TextScale { get; set; } = "normal"; // small, normal, large
    public string FontFamily { get; set; } = "IBM Plex Sans Arabic"; // IBM Plex Sans Arabic, Cairo
    public bool HighContrast { get; set; }
    public bool ReducedMotion { get; set; }
    public string? DefaultLandingPage { get; set; }
    public string? DataGridPreferencesJson { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string NavbarMode
    {
        get => GetNavbarMode();
        set => SetNavbarModeInternal(value);
    }

    private string _navbarMode = "side";

    public string GetNavbarMode()
    {
        if (!string.IsNullOrEmpty(DataGridPreferencesJson) && DataGridPreferencesJson.Contains("\"navbarMode\""))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(DataGridPreferencesJson);
                if (doc.RootElement.TryGetProperty("navbarMode", out var prop))
                {
                    var val = prop.GetString();
                    if (val is "top" or "floating" or "side") return val;
                }
            }
            catch { }
        }
        return _navbarMode;
    }

    public void SetNavbarModeInternal(string mode)
    {
        _navbarMode = mode is "top" or "floating" ? mode : "side";
        try
        {
            var dict = string.IsNullOrEmpty(DataGridPreferencesJson)
                ? new Dictionary<string, object>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(DataGridPreferencesJson) ?? new();
            dict["navbarMode"] = _navbarMode;
            DataGridPreferencesJson = System.Text.Json.JsonSerializer.Serialize(dict);
        }
        catch { }
    }
}

public interface IUserPreferenceService
{
    UserPreference Current { get; }
    event Action? OnPreferenceChanged;
    void SetDensity(GridDensity density);
    void SetAppearance(AppearanceMode appearance);
    void SetReducedMotion(bool reducedMotion);
    void SetLanguage(string lang);
    void SetFontFamily(string fontFamily);
    void SetTextScale(string textScale);
    void SetRadius(string radius);
    void SetNavbarMode(string mode);
    void Update(Action<UserPreference> updateAction);
    Task LoadForUserAsync(string userId);
    Task SaveAsync(string? userId = null);
}

public sealed class UserPreferenceService : IUserPreferenceService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<UserPreferenceService>? _logger;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor? _httpContextAccessor;
    private readonly UserPreference _preference = new();

    public UserPreferenceService(
        IServiceScopeFactory? scopeFactory = null,
        ILogger<UserPreferenceService>? logger = null,
        Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        InitializeFromContext();
    }

    private void InitializeFromContext()
    {
        try
        {
            var http = _httpContextAccessor?.HttpContext;
            if (http == null) return;

            var cookies = http.Request.Cookies;
            if (cookies.TryGetValue("hr-nav-mode", out var navMode) && !string.IsNullOrWhiteSpace(navMode))
            {
                _preference.NavbarMode = navMode is "top" or "floating" ? navMode : "side";
            }

            if (cookies.TryGetValue("hr-theme", out var theme) && !string.IsNullOrWhiteSpace(theme))
            {
                _preference.Appearance = theme switch
                {
                    "dark" => AppearanceMode.Dark,
                    "system" => AppearanceMode.System,
                    _ => AppearanceMode.Light
                };
            }

            if (cookies.TryGetValue("hr-brand", out var brand) && !string.IsNullOrWhiteSpace(brand))
            {
                _preference.BrandTheme = brand;
            }

            if (cookies.TryGetValue("hr-font", out var font) && !string.IsNullOrWhiteSpace(font))
            {
                _preference.FontFamily = font switch
                {
                    "cairo" => "Cairo",
                    "tajawal" => "Tajawal",
                    "noto" => "Noto Sans Arabic",
                    "alexandria" => "Alexandria",
                    _ => "IBM Plex Sans Arabic"
                };
            }

            if (cookies.TryGetValue("hr-density", out var density) && !string.IsNullOrWhiteSpace(density))
            {
                _preference.Density = density switch
                {
                    "compact" => GridDensity.Compact,
                    "spacious" => GridDensity.Spacious,
                    _ => GridDensity.Comfortable
                };
            }

            if (cookies.TryGetValue("hr-font-size", out var fontSize) && !string.IsNullOrWhiteSpace(fontSize))
            {
                _preference.TextScale = fontSize switch
                {
                    "sm" => "small",
                    "lg" => "large",
                    "xl" => "xlarge",
                    _ => "normal"
                };
            }

            var user = http.User;
            if (user?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(user.Identity.Name))
            {
                _preference.UserId = user.Identity.Name;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Could not initialize preferences from HttpContext");
        }
    }

    public UserPreference Current => _preference;
    public event Action? OnPreferenceChanged;

    public void SetDensity(GridDensity density)
    {
        if (_preference.Density != density)
        {
            _preference.Density = density;
            _preference.UpdatedAtUtc = DateTime.UtcNow;
            OnPreferenceChanged?.Invoke();
        }
    }

    public void SetAppearance(AppearanceMode appearance)
    {
        if (_preference.Appearance != appearance)
        {
            _preference.Appearance = appearance;
            _preference.UpdatedAtUtc = DateTime.UtcNow;
            OnPreferenceChanged?.Invoke();
        }
    }

    public void SetReducedMotion(bool reducedMotion)
    {
        if (_preference.ReducedMotion != reducedMotion)
        {
            _preference.ReducedMotion = reducedMotion;
            _preference.UpdatedAtUtc = DateTime.UtcNow;
            OnPreferenceChanged?.Invoke();
        }
    }

    public void SetLanguage(string lang)
    {
        if (_preference.Language != lang)
        {
            _preference.Language = lang;
            _preference.UpdatedAtUtc = DateTime.UtcNow;
            OnPreferenceChanged?.Invoke();
        }
    }

    public void SetFontFamily(string fontFamily)
    {
        if (!string.IsNullOrWhiteSpace(fontFamily) && _preference.FontFamily != fontFamily)
        {
            _preference.FontFamily = fontFamily;
            _preference.UpdatedAtUtc = DateTime.UtcNow;
            OnPreferenceChanged?.Invoke();
        }
    }

    public void SetTextScale(string textScale)
    {
        if (!string.IsNullOrWhiteSpace(textScale) && _preference.TextScale != textScale)
        {
            _preference.TextScale = textScale;
            _preference.UpdatedAtUtc = DateTime.UtcNow;
            OnPreferenceChanged?.Invoke();
        }
    }

    public void SetRadius(string radius)
    {
        if (!string.IsNullOrWhiteSpace(radius) && _preference.Radius != radius)
        {
            _preference.Radius = radius;
            _preference.UpdatedAtUtc = DateTime.UtcNow;
            OnPreferenceChanged?.Invoke();
        }
    }

    public void SetNavbarMode(string mode)
    {
        var normalized = mode switch
        {
            "top" => "top",
            "floating" => "floating",
            _ => "side"
        };
        if (_preference.NavbarMode != normalized)
        {
            _preference.NavbarMode = normalized;
            _preference.UpdatedAtUtc = DateTime.UtcNow;
            OnPreferenceChanged?.Invoke();
        }
    }

    public void Update(Action<UserPreference> updateAction)
    {
        updateAction(_preference);
        _preference.UpdatedAtUtc = DateTime.UtcNow;
        OnPreferenceChanged?.Invoke();
    }

    public async Task LoadForUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || _scopeFactory == null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var entity = await db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
            if (entity != null)
            {
                _preference.UserId = entity.UserId;
                _preference.Language = entity.Language;
                _preference.Appearance = entity.Appearance;
                _preference.BrandTheme = entity.BrandTheme;
                _preference.CustomBrandHex = entity.CustomBrandHex;
                _preference.Density = entity.Density;
                _preference.Radius = entity.Radius;
                _preference.TextScale = entity.TextScale;
                _preference.FontFamily = entity.FontFamily;
                _preference.HighContrast = entity.HighContrast;
                _preference.ReducedMotion = entity.ReducedMotion;
                _preference.DefaultLandingPage = entity.DefaultLandingPage;
                _preference.DataGridPreferencesJson = entity.DataGridPreferencesJson;
                _preference.UpdatedAtUtc = entity.UpdatedAtUtc;
                OnPreferenceChanged?.Invoke();
            }
            else
            {
                _preference.UserId = userId;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load preferences for user {UserId}", userId);
        }
    }

    public async Task SaveAsync(string? userId = null)
    {
        var targetUser = !string.IsNullOrWhiteSpace(userId) ? userId : _preference.UserId;
        if (string.IsNullOrWhiteSpace(targetUser) || _scopeFactory == null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var existing = await db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == targetUser);
            if (existing == null)
            {
                var newEntity = new UserPreference
                {
                    UserId = targetUser,
                    Language = _preference.Language,
                    Appearance = _preference.Appearance,
                    BrandTheme = _preference.BrandTheme,
                    CustomBrandHex = _preference.CustomBrandHex,
                    Density = _preference.Density,
                    Radius = _preference.Radius,
                    TextScale = _preference.TextScale,
                    FontFamily = _preference.FontFamily,
                    HighContrast = _preference.HighContrast,
                    ReducedMotion = _preference.ReducedMotion,
                    DefaultLandingPage = _preference.DefaultLandingPage,
                    DataGridPreferencesJson = _preference.DataGridPreferencesJson,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                db.UserPreferences.Add(newEntity);
            }
            else
            {
                existing.Language = _preference.Language;
                existing.Appearance = _preference.Appearance;
                existing.BrandTheme = _preference.BrandTheme;
                existing.CustomBrandHex = _preference.CustomBrandHex;
                existing.Density = _preference.Density;
                existing.Radius = _preference.Radius;
                existing.TextScale = _preference.TextScale;
                existing.FontFamily = _preference.FontFamily;
                existing.HighContrast = _preference.HighContrast;
                existing.ReducedMotion = _preference.ReducedMotion;
                existing.DefaultLandingPage = _preference.DefaultLandingPage;
                existing.DataGridPreferencesJson = _preference.DataGridPreferencesJson;
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to persist user preferences for user {UserId}", targetUser);
        }
    }
}
