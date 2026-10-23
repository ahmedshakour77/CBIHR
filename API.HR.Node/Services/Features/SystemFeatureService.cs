using API.HR.Node.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API.HR.Node.Services.Features;

public interface ISystemFeatureService
{
    event Action? OnSettingsChanged;
    Task<SystemFeatureSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateSettingsAsync(SystemFeatureSettings updated, string? updatedBy = null, CancellationToken cancellationToken = default);
    Task<bool> IsFeatureEnabledAsync(string featureKey, CancellationToken cancellationToken = default);
}

public class SystemFeatureService : ISystemFeatureService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SystemFeatureService> _logger;
    private SystemFeatureSettings? _cachedSettings;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action? OnSettingsChanged;

    public SystemFeatureService(IServiceScopeFactory scopeFactory, ILogger<SystemFeatureService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<SystemFeatureSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSettings != null)
        {
            return Clone(_cachedSettings);
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedSettings != null)
            {
                return Clone(_cachedSettings);
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var entity = await db.SystemFeatureSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, cancellationToken);
            if (entity == null)
            {
                entity = new SystemFeatureSettings
                {
                    Id = 1,
                    EnableMyFeedback = true,
                    EnableFeedbackFab = true,
                    AllowIdeas = true,
                    AllowNotes = true,
                    AllowInquiries = true,
                    AllowComplaints = true,
                    AllowOthers = true,
                    AllowAttachments = true,
                    DisabledMessage = "خدمة تقديم الأفكار والمقترحات متوقفة مؤقتاً بقرار الإدارة.",
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedBy = "System"
                };
                db.SystemFeatureSettings.Add(entity);
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not insert default SystemFeatureSettings.");
                }
            }

            _cachedSettings = entity;
            return Clone(_cachedSettings);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> UpdateSettingsAsync(SystemFeatureSettings updated, string? updatedBy = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var existing = await db.SystemFeatureSettings.FirstOrDefaultAsync(s => s.Id == 1, cancellationToken);
            if (existing == null)
            {
                existing = new SystemFeatureSettings { Id = 1 };
                db.SystemFeatureSettings.Add(existing);
            }

            existing.EnableMyFeedback = updated.EnableMyFeedback;
            existing.EnableFeedbackFab = updated.EnableFeedbackFab;
            existing.AllowIdeas = updated.AllowIdeas;
            existing.AllowNotes = updated.AllowNotes;
            existing.AllowInquiries = updated.AllowInquiries;
            existing.AllowComplaints = updated.AllowComplaints;
            existing.AllowOthers = updated.AllowOthers;
            existing.AllowAttachments = updated.AllowAttachments;
            existing.DisabledMessage = updated.DisabledMessage;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedBy = updatedBy ?? "Admin";

            await db.SaveChangesAsync(cancellationToken);

            _cachedSettings = Clone(existing);
            _logger.LogInformation("SystemFeatureSettings updated by {User}.", updatedBy ?? "Admin");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update SystemFeatureSettings.");
            return false;
        }
        finally
        {
            _lock.Release();
        }

        try
        {
            OnSettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error firing OnSettingsChanged.");
        }

        return true;
    }

    public async Task<bool> IsFeatureEnabledAsync(string featureKey, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return featureKey.ToLowerInvariant() switch
        {
            "myfeedback" or "feedback" => settings.EnableMyFeedback,
            "feedbackfab" or "fab" => settings.EnableFeedbackFab && settings.EnableMyFeedback,
            "ideas" => settings.AllowIdeas && settings.EnableMyFeedback,
            "notes" => settings.AllowNotes && settings.EnableMyFeedback,
            "inquiries" => settings.AllowInquiries && settings.EnableMyFeedback,
            "complaints" => settings.AllowComplaints && settings.EnableMyFeedback,
            "others" => settings.AllowOthers && settings.EnableMyFeedback,
            "attachments" => settings.AllowAttachments,
            _ => true
        };
    }

    private static SystemFeatureSettings Clone(SystemFeatureSettings source) => new()
    {
        Id = source.Id,
        EnableMyFeedback = source.EnableMyFeedback,
        EnableFeedbackFab = source.EnableFeedbackFab,
        AllowIdeas = source.AllowIdeas,
        AllowNotes = source.AllowNotes,
        AllowInquiries = source.AllowInquiries,
        AllowComplaints = source.AllowComplaints,
        AllowOthers = source.AllowOthers,
        AllowAttachments = source.AllowAttachments,
        DisabledMessage = source.DisabledMessage,
        UpdatedAtUtc = source.UpdatedAtUtc,
        UpdatedBy = source.UpdatedBy
    };
}
