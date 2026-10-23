namespace API.HR.Node.Contracts;

/// <summary>
/// Structured mobile device information provided by Flutter/Mobile client apps.
/// Supports both Android (AndroidDeviceInfo) and iOS (IosDeviceInfo).
/// </summary>
public sealed record MobileDeviceInfo(
    string? Platform = null,               // "Android" or "iOS"
    string? Model = null,                  // Device model e.g. "SM-S918B", "Pixel 7", "iPhone"
    string? Manufacturer = null,           // "samsung", "Google", "Apple"
    string? Brand = null,                  // "samsung", "Google", "Apple"
    string? OsVersion = null,              // Android release or iOS systemVersion e.g. "14", "17.4.1"
    int? SdkInt = null,                    // Android sdkInt e.g. 34, 35
    string? BuildId = null,                // Android build ID e.g. "UP1A.231005.007"
    bool? IsPhysicalDevice = null,         // true if real phone, false if emulator/simulator
    string? Hardware = null,               // Hardware / board e.g. "qcom", "universal9925"
    string? DeviceName = null,             // iOS user-custom device name e.g. "Ahmed's iPhone"
    string? SystemName = null,             // iOS systemName e.g. "iOS"
    string? VendorIdentifier = null,       // iOS identifierForVendor UUID e.g. "E621E1F8-C36C-495A-93FC-0C247A3E6E5F"
    string? MachineCode = null,            // iOS machine code e.g. "iPhone15,3"
    string? AppVersion = null              // Mobile application version e.g. "1.0.4"
);
