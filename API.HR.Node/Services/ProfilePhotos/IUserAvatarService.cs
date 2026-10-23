namespace API.HR.Node.Services.ProfilePhotos;

public interface IUserAvatarService
{
    Task<string?> GetAvatarDataUrlAsync(string? employeeId, string? userName, CancellationToken ct = default);
    void Invalidate(string? employeeId);
}
