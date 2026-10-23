using System.Security.Claims;
using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Services.Audit;
using API.HR.Node.Services.Images;
using API.HR.Node.Security;
using API.HR.Node.Services.Security;
using API.HR.Node.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.ProfilePhotos;

public sealed class EmployeeProfilePhotoService(
    ApplicationDbContext dbContext,
    IFileStorageService storageService,
    IImageProcessor imageProcessor,
    IFileMalwareScanner malwareScanner,
    IAuditLogger auditLogger,
    ILogger<EmployeeProfilePhotoService> logger)
    : IEmployeeProfilePhotoService
{
    public async Task<PhotoServiceResult> UploadOrReplacePhotoAsync(
        string employeeID,
        IFormFile file,
        ClaimsPrincipal user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify Employee existence
        var employeeExists = await dbContext.Employees
            .AsNoTracking()
            .AnyAsync(e => e.EmployeeID == employeeID, cancellationToken);

        if (!employeeExists)
        {
            await auditLogger.LogAsync("ProfilePhotoUploadFailed", employeeID, null, false, user, ipAddress, userAgent, "Employee not found", cancellationToken: cancellationToken);
            return new PhotoServiceResult.NotFound("Employee was not found.");
        }

        // 2. Validate file stream and format
        await using var uploadStream = file.OpenReadStream();
        var validation = await imageProcessor.ValidateImageAsync(
            uploadStream,
            file.FileName,
            file.ContentType,
            file.Length,
            cancellationToken);

        if (!validation.IsValid)
        {
            await auditLogger.LogAsync("ProfilePhotoUploadFailed", employeeID, null, false, user, ipAddress, userAgent, validation.ErrorMessage, cancellationToken: cancellationToken);
            return new PhotoServiceResult.ValidationError(validation.ErrorMessage ?? "Invalid photo upload.", 422);
        }

        // 3. Malware Scan
        uploadStream.Position = 0;
        var malwareResult = await malwareScanner.ScanAsync(uploadStream, file.FileName, cancellationToken);
        if (malwareResult.IsRejected())
        {
            await auditLogger.LogAsync("ProfilePhotoUploadFailed", employeeID, null, false, user, ipAddress, userAgent, $"Malware scan {malwareResult.Status}: {malwareResult.Details}", cancellationToken: cancellationToken);
            return new PhotoServiceResult.ValidationError("Uploaded file failed security scan and was rejected.", 400);
        }

        // 4. Decode & Process & Generate 150x150 Thumbnail
        uploadStream.Position = 0;
        var processed = await imageProcessor.ProcessAndGenerateThumbnailAsync(
            uploadStream,
            validation.DetectedContentType,
            cancellationToken);

        // 5. Generate secure random storage keys (UUID based)
        var randomId = Guid.NewGuid().ToString("N");
        var originalStorageKey = $"employees/{employeeID}/profile/{randomId}/original";
        var thumbnailStorageKey = $"employees/{employeeID}/profile/{randomId}/thumbnail";

        string? savedOriginalKey = null;
        string? savedThumbnailKey = null;

        try
        {
            // Save physical files to Private Storage outside wwwroot
            using var originalStream = new MemoryStream(processed.OptimizedOriginalBytes);
            savedOriginalKey = await storageService.SaveAsync(originalStream, originalStorageKey, cancellationToken);

            using var thumbnailStream = new MemoryStream(processed.ThumbnailBytes);
            savedThumbnailKey = await storageService.SaveAsync(thumbnailStream, thumbnailStorageKey, cancellationToken);

            // 6. DB Transaction to save metadata & manage single active photo version
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var existingActivePhoto = await dbContext.EmployeeProfilePhotos
                .FirstOrDefaultAsync(p => p.EmployeeID == employeeID && p.IsActive, cancellationToken);

            int nextVersion = 1;
            string action = "ProfilePhotoUploaded";
            if (existingActivePhoto != null)
            {
                var claimEmpId = user.FindFirst("employee_id")?.Value;
                var isOwnPhoto = !string.IsNullOrWhiteSpace(claimEmpId) && claimEmpId.Equals(employeeID, StringComparison.OrdinalIgnoreCase);

                if (!isOwnPhoto && user.Identity?.IsAuthenticated == true)
                {
                    var appUser = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == user.Identity.Name || u.Email == user.Identity.Name, cancellationToken);
                    if (appUser != null && !string.IsNullOrWhiteSpace(appUser.EmployeeID) && appUser.EmployeeID.Equals(employeeID, StringComparison.OrdinalIgnoreCase))
                    {
                        isOwnPhoto = true;
                    }
                }

                var canReplace = isOwnPhoto || user.IsInRole("Admin") || user.HasClaim(c =>
                    (c.Type == "permission" || c.Type == "Permission") &&
                    c.Value.Equals(Permissions.ProfilePhotoReplace, StringComparison.OrdinalIgnoreCase));

                if (!canReplace)
                {
                    await CleanupStorageFilesAsync(savedOriginalKey, savedThumbnailKey);
                    await auditLogger.LogAsync("ProfilePhotoReplaceFailed", employeeID, existingActivePhoto.Id, false, user, ipAddress, userAgent, "Permission ProfilePhotoReplace required", cancellationToken: cancellationToken);
                    return new PhotoServiceResult.Forbidden("Permission 'Employee.ProfilePhoto.Replace' is required to replace an existing active profile photo.");
                }

                existingActivePhoto.IsActive = false;
                existingActivePhoto.UpdatedAt = DateTimeOffset.UtcNow;
                existingActivePhoto.UpdatedBy = GetUserName(user);
                nextVersion = existingActivePhoto.Version + 1;
                action = "ProfilePhotoReplaced";
            }

            var newPhoto = new EmployeeProfilePhoto
            {
                EmployeeID = employeeID,
                StorageKey = savedOriginalKey,
                ThumbnailStorageKey = savedThumbnailKey,
                OriginalFileName = Path.GetFileName(file.FileName),
                ContentType = processed.ContentType,
                FileSize = processed.OptimizedOriginalBytes.Length,
                Width = processed.Width,
                Height = processed.Height,
                Sha256 = processed.Sha256Hash,
                Version = nextVersion,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = GetUserName(user)
            };

            dbContext.EmployeeProfilePhotos.Add(newPhoto);
            await dbContext.SaveChangesAsync(cancellationToken);

            await auditLogger.LogAsync(action, employeeID, newPhoto.Id, true, user, ipAddress, userAgent, cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var response = new ProfilePhotoResponse(
                newPhoto.Id,
                newPhoto.EmployeeID,
                newPhoto.Version,
                newPhoto.ContentType,
                newPhoto.FileSize,
                newPhoto.Width,
                newPhoto.Height,
                newPhoto.Sha256,
                newPhoto.CreatedAt);

            return new PhotoServiceResult.Success<ProfilePhotoResponse>(response);
        }
        catch (DbUpdateException dbEx)
        {
            logger.LogError(dbEx, "Concurrency or DB failure while saving profile photo for EmployeeID: {EmployeeID}", employeeID);
            
            // Clean up physical storage files to prevent orphans
            await CleanupStorageFilesAsync(savedOriginalKey, savedThumbnailKey);
            await auditLogger.LogAsync("ProfilePhotoUploadFailed", employeeID, null, false, user, ipAddress, userAgent, "Concurrency conflict during DB save", cancellationToken: cancellationToken);

            return new PhotoServiceResult.ConcurrencyConflict("A concurrent profile photo upload was detected. Please retry.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload profile photo for EmployeeID: {EmployeeID}", employeeID);
            
            // Clean up physical storage files
            await CleanupStorageFilesAsync(savedOriginalKey, savedThumbnailKey);
            await auditLogger.LogAsync("ProfilePhotoUploadFailed", employeeID, null, false, user, ipAddress, userAgent, ex.Message, cancellationToken: cancellationToken);

            throw;
        }
    }

    public async Task<PhotoServiceResult> GetPhotoAsync(
        string employeeID,
        bool thumbnail,
        string? ifNoneMatchHeader,
        ClaimsPrincipal user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var photo = await dbContext.EmployeeProfilePhotos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.EmployeeID == employeeID && p.IsActive, cancellationToken);

        if (photo == null)
        {
            return new PhotoServiceResult.NotFound("Profile photo was not found for this employee.");
        }

        // Build ETag based on Sha256 + Version
        var etag = $"\"{photo.Sha256}-v{photo.Version}{(thumbnail ? "-thumb" : "")}\"";

        if (!string.IsNullOrEmpty(ifNoneMatchHeader) && ifNoneMatchHeader.Contains(etag))
        {
            return new PhotoServiceResult.NotModified();
        }

        var keyToRead = thumbnail ? photo.ThumbnailStorageKey : photo.StorageKey;
        var stream = await storageService.OpenReadAsync(keyToRead, cancellationToken);

        if (stream == null)
        {
            logger.LogError("Physical storage file missing for photo ID {PhotoId}, key: {Key}", photo.Id, keyToRead);
            return new PhotoServiceResult.NotFound("Photo file storage was not found.");
        }

        var action = thumbnail ? "ProfilePhotoThumbnailViewed" : "ProfilePhotoViewed";
        await auditLogger.LogAsync(action, employeeID, photo.Id, true, user, ipAddress, userAgent, cancellationToken: cancellationToken);

        return new PhotoServiceResult.PhotoStreamData(
            stream,
            photo.ContentType,
            etag,
            photo.UpdatedAt ?? photo.CreatedAt,
            stream.Length);
    }

    public async Task<PhotoServiceResult> GetMetadataAsync(
        string employeeID,
        ClaimsPrincipal user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var photo = await dbContext.EmployeeProfilePhotos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.EmployeeID == employeeID && p.IsActive, cancellationToken);

        if (photo == null)
        {
            return new PhotoServiceResult.NotFound("Active profile photo was not found.");
        }

        var response = new ProfilePhotoResponse(
            photo.Id,
            photo.EmployeeID,
            photo.Version,
            photo.ContentType,
            photo.FileSize,
            photo.Width,
            photo.Height,
            photo.Sha256,
            photo.CreatedAt);

        return new PhotoServiceResult.Success<ProfilePhotoResponse>(response);
    }

    public async Task<PhotoServiceResult> DeletePhotoAsync(
        string employeeID,
        ClaimsPrincipal user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var photo = await dbContext.EmployeeProfilePhotos
            .FirstOrDefaultAsync(p => p.EmployeeID == employeeID && p.IsActive, cancellationToken);

        if (photo == null)
        {
            await auditLogger.LogAsync("ProfilePhotoDeleteFailed", employeeID, null, false, user, ipAddress, userAgent, "No active photo to delete", cancellationToken: cancellationToken);
            return new PhotoServiceResult.NotFound("No active profile photo found to delete.");
        }

        photo.IsActive = false;
        photo.DeletedAt = DateTimeOffset.UtcNow;
        photo.DeletedBy = GetUserName(user);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync("ProfilePhotoDeleted", employeeID, photo.Id, true, user, ipAddress, userAgent, cancellationToken: cancellationToken);

        return new PhotoServiceResult.Success<bool>(true);
    }

    private async Task CleanupStorageFilesAsync(string? originalKey, string? thumbnailKey)
    {
        if (!string.IsNullOrEmpty(originalKey))
        {
            try { await storageService.DeleteAsync(originalKey); } catch { /* Ignore cleanup errors */ }
        }
        if (!string.IsNullOrEmpty(thumbnailKey))
        {
            try { await storageService.DeleteAsync(thumbnailKey); } catch { /* Ignore cleanup errors */ }
        }
    }

    private static string GetUserName(ClaimsPrincipal user)
    {
        return user.Identity?.Name ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "SystemUser";
    }
}
