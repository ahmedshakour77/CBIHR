using System.Text.Json.Serialization;

namespace API.HR.Node.Contracts;

public sealed record EmployeeCivilDataImportRow(
    [property: JsonPropertyName("employeeId")] string EmployeeID,
    [property: JsonPropertyName("nationalId")] string? NationalId,
    [property: JsonPropertyName("unifiedEmployeeNumber")] string? UnifiedEmployeeNumber,
    [property: JsonPropertyName("motherName")] string? MotherName,
    [property: JsonPropertyName("firstName")] string? FirstName,
    [property: JsonPropertyName("secondName")] string? SecondName,
    [property: JsonPropertyName("thirdName")] string? ThirdName,
    [property: JsonPropertyName("fourthName")] string? FourthName,
    [property: JsonPropertyName("fullName")] string? FullName = null);

public sealed record EmployeeCivilDataImportRequest(
    [property: JsonPropertyName("rows")] IReadOnlyList<EmployeeCivilDataImportRow> Rows);

public sealed record EmployeeCivilDataImportResponse(
    [property: JsonPropertyName("totalRows")] int TotalRows,
    [property: JsonPropertyName("updated")] int Updated,
    [property: JsonPropertyName("skipped")] int Skipped,
    [property: JsonPropertyName("missingEmployeeIDs")] IReadOnlyList<string> MissingEmployeeIDs);
