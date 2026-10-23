namespace API.HR.Node.Contracts;

public sealed record EmployeeContactLookupRequest(
    string? Email = null,
    string? Phone = null,
    string? Contact = null);
