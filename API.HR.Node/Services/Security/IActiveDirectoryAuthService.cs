namespace API.HR.Node.Services.Security;

public record AdUserInfo(string Username, string Email, string? DisplayName);

public interface IActiveDirectoryAuthService
{
    bool ValidateCredentials(string username, string password, out AdUserInfo? userInfo);
}
