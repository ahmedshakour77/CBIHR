namespace API.HR.Node.Contracts;

public sealed record LoginRequest(
    string Email,
    string Password,
    MobileDeviceInfo? DeviceInfo = null,
    string? DeviceModel = null,
    string? OperatingSystem = null,
    string? AppVersion = null,
    string? DeviceId = null);
