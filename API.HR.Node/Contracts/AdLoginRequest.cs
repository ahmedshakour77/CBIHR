namespace API.HR.Node.Contracts;

public sealed record AdLoginRequest(
    string? UserName = null,
    string? Email = null,
    string Password = "",
    MobileDeviceInfo? DeviceInfo = null,
    string? DeviceModel = null,
    string? OperatingSystem = null,
    string? AppVersion = null,
    string? DeviceId = null)
{
    public string AccountName => !string.IsNullOrWhiteSpace(UserName)
        ? UserName.Trim()
        : (Email?.Trim() ?? string.Empty);
}
