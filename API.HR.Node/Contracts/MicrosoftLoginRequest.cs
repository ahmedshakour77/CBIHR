namespace API.HR.Node.Contracts;

public record MicrosoftLoginRequest(
    string? IdToken,
    string? Email = null,
    string? AccessToken = null,
    MobileDeviceInfo? DeviceInfo = null,
    string? DeviceModel = null,
    string? OperatingSystem = null,
    string? AppVersion = null,
    string? DeviceId = null);
