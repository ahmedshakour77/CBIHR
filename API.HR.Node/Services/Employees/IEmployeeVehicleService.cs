using API.HR.Node.Data;

namespace API.HR.Node.Services.Employees;

public sealed record VehicleKpiSummary(
    int TotalVehicles,
    int AuthorizedVehicles,
    int UnauthorizedVehicles,
    int MainBuildingCount,
    int JadriyaCount,
    int ErbilCount,
    int MosulCount,
    int BasraCount
);

public interface IEmployeeVehicleService
{
    Task<List<EmployeeVehicle>> GetVehiclesByEmployeeIdAsync(int employeeId, CancellationToken ct = default);
    Task<List<EmployeeVehicle>> GetVehiclesByEmployeeCodeAsync(string employeeCode, CancellationToken ct = default);
    Task<List<EmployeeVehicle>> GetAllVehiclesAsync(string? search = null, string? location = null, bool? isAuthorized = null, CancellationToken ct = default);
    Task<EmployeeVehicle?> GetVehicleByIdAsync(int id, CancellationToken ct = default);
    Task<EmployeeVehicle> AddVehicleAsync(EmployeeVehicle vehicle, CancellationToken ct = default);
    Task<EmployeeVehicle> UpdateVehicleAsync(EmployeeVehicle vehicle, CancellationToken ct = default);
    Task<bool> DeleteVehicleAsync(int id, CancellationToken ct = default);
    Task<bool> ToggleAuthorizationAsync(int id, bool isAuthorized, CancellationToken ct = default);
    Task<VehicleKpiSummary> GetKpiSummaryAsync(CancellationToken ct = default);
}
