using System.Collections.Concurrent;
using API.HR.Node.Data;
using API.HR.Node.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API.HR.Node.Services.ProfilePhotos;

public sealed class UserAvatarService : IUserAvatarService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFileStorageService _storage;
    private readonly ILogger<UserAvatarService> _logger;
    private readonly ConcurrentDictionary<string, string?> _avatarCache = new(StringComparer.OrdinalIgnoreCase);

    public UserAvatarService(
        IServiceScopeFactory scopeFactory,
        IFileStorageService storage,
        ILogger<UserAvatarService> logger)
    {
        _scopeFactory = scopeFactory;
        _storage = storage;
        _logger = logger;
    }

    public async Task<string?> GetAvatarDataUrlAsync(string? employeeId, string? userName, CancellationToken ct = default)
    {
        // 1. Resolve employeeId if not given
        if (string.IsNullOrWhiteSpace(employeeId) && !string.IsNullOrWhiteSpace(userName))
        {
            employeeId = await ResolveEmployeeIdFromUserNameAsync(userName, ct);
        }

        if (string.IsNullOrWhiteSpace(employeeId))
        {
            return null;
        }

        var key = employeeId.Trim();
        if (_avatarCache.TryGetValue(key, out var cachedDataUrl))
        {
            return cachedDataUrl;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var photo = await db.EmployeeProfilePhotos
                .AsNoTracking()
                .Where(p => p.EmployeeID == key && p.IsActive)
                .OrderByDescending(p => p.Version)
                .FirstOrDefaultAsync(ct);

            if (photo == null)
            {
                _avatarCache[key] = null;
                return null;
            }

            var storageKey = !string.IsNullOrWhiteSpace(photo.ThumbnailStorageKey)
                ? photo.ThumbnailStorageKey
                : photo.StorageKey;

            using var stream = await _storage.OpenReadAsync(storageKey, ct);
            if (stream == null)
            {
                _avatarCache[key] = null;
                return null;
            }

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var base64 = Convert.ToBase64String(ms.ToArray());
            var mime = string.IsNullOrWhiteSpace(photo.ContentType) ? "image/jpeg" : photo.ContentType;
            var dataUrl = $"data:{mime};base64,{base64}";

            _avatarCache[key] = dataUrl;
            return dataUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load avatar thumbnail for employee {EmployeeID}", employeeId);
            return null;
        }
    }

    public void Invalidate(string? employeeId)
    {
        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            _avatarCache.TryRemove(employeeId.Trim(), out _);
        }
    }

    private async Task<string?> ResolveEmployeeIdFromUserNameAsync(string userName, CancellationToken ct)
    {
        var clean = userName.Trim();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Direct check in Employee table
            var empExists = await db.Employees
                .AsNoTracking()
                .AnyAsync(e => e.EmployeeID == clean, ct);
            if (empExists) return clean;

            // Check ApplicationUsers
            var appUser = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => (u.UserName != null && u.UserName == clean) ||
                                          (u.Email != null && u.Email == clean), ct);
            if (appUser != null && !string.IsNullOrWhiteSpace(appUser.EmployeeID))
            {
                return appUser.EmployeeID;
            }

            // Check EmployeeEmails
            var empEmail = await db.EmployeeEmails
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Email == clean, ct);
            if (empEmail != null && !string.IsNullOrWhiteSpace(empEmail.EmployeeID))
            {
                return empEmail.EmployeeID;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
