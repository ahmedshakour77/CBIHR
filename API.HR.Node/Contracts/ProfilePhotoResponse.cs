namespace API.HR.Node.Contracts;

public sealed record ProfilePhotoResponse(
    int PhotoId,
    string EmployeeID,
    int Version,
    string ContentType,
    long FileSize,
    int Width,
    int Height,
    string Sha256,
    DateTimeOffset CreatedAt);
