using System.Text.Json.Nodes;

namespace API.HR.Node.Contracts;

public sealed record PasskeyRegisterOptionsRequest(
    string? Email = null,
    string? EmployeeID = null,
    string? DeviceName = null
);

public sealed record PasskeyRegisterCompleteRequest(
    string? FlowId = null,
    string? OptionsJson = null,
    string AttestationResponseJson = "",
    string? DeviceName = null,
    MobileDeviceInfo? DeviceInfo = null
);

public sealed record PasskeyLoginOptionsRequest(
    string? Email = null,
    string? EmployeeID = null
);

public sealed record PasskeyLoginCompleteRequest(
    string? FlowId = null,
    string? OptionsJson = null,
    string AssertionResponseJson = "",
    MobileDeviceInfo? DeviceInfo = null,
    string? DeviceModel = null,
    string? OperatingSystem = null,
    string? AppVersion = null,
    string? DeviceId = null
);

public sealed record UserPasskeyDto(
    long Id,
    string EmployeeID,
    string? DeviceName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt
);
