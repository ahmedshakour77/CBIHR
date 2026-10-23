using System.Security.Claims;
using API.HR.Node.Contracts;

namespace API.HR.Node.Services.ProfilePhotos;

public abstract record PhotoServiceResult
{
    public sealed record Success<T>(T Data) : PhotoServiceResult;
    public sealed record NotFound(string Message) : PhotoServiceResult;
    public sealed record Forbidden(string Message) : PhotoServiceResult;
    public sealed record ValidationError(string Message, int StatusCode = 422) : PhotoServiceResult;
    public sealed record ConcurrencyConflict(string Message) : PhotoServiceResult;
    public sealed record NotModified() : PhotoServiceResult;
    public sealed record PhotoStreamData(
        Stream Stream,
        string ContentType,
        string ETag,
        DateTimeOffset LastModified,
        long FileSize) : PhotoServiceResult;
}

public interface IEmployeeProfilePhotoService
{
    Task<PhotoServiceResult> UploadOrReplacePhotoAsync(
        string employeeID,
        IFormFile file,
        ClaimsPrincipal user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<PhotoServiceResult> GetPhotoAsync(
        string employeeID,
        bool thumbnail,
        string? ifNoneMatchHeader,
        ClaimsPrincipal user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<PhotoServiceResult> GetMetadataAsync(
        string employeeID,
        ClaimsPrincipal user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<PhotoServiceResult> DeletePhotoAsync(
        string employeeID,
        ClaimsPrincipal user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
