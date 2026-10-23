using System.Text.Json.Serialization;

namespace API.HR.Node.Contracts;

public sealed record CareerMovementDto(
    int Id,
    int EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    DateOnly MovementDate,
    string FormationName,
    bool CountForAllowance,
    bool CountForPromotion,
    bool CountForPension,
    string? Notes,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateCareerMovementRequest(
    string? EmployeeNumber,
    int? EmployeeId,
    string MovementDate,
    string FormationName,
    bool? CountForAllowance,
    bool? CountForPromotion,
    bool? CountForPension,
    string? Notes);

public sealed record UpdateCareerMovementRequest(
    string MovementDate,
    string FormationName,
    bool CountForAllowance,
    bool CountForPromotion,
    bool CountForPension,
    string? Notes);

public sealed record CareerMovementImportRow(
    string EmployeeID,
    string MovementDate,
    string FormationName,
    bool? CountForAllowance = null,
    bool? CountForPromotion = null,
    bool? CountForPension = null,
    string? Notes = null,
    string? EmployeeName = null);

public sealed record CareerMovementImportRequest(
    string Mode,
    IReadOnlyList<CareerMovementImportRow> Movements);

public sealed record CareerMovementImportResponse(
    int TotalRows,
    int Inserted,
    int Updated,
    int Skipped,
    IReadOnlyList<string> MissingEmployeeIDs);
