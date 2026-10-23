using API.HR.Node.Contracts;
using API.HR.Node.Data.Allowances;

namespace API.HR.Node.Services.Allowances;

public interface IEmployeeAllowanceService
{
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);

    Task<List<AllowanceType>> GetAllowanceTypesAsync(CancellationToken cancellationToken = default);

    Task<EmployeeAllowancePeriod?> GetActivePeriodAsync(int employeeId, CancellationToken cancellationToken = default);

    Task<List<EmployeeAllowancePeriod>> GetPeriodsHistoryAsync(int employeeId, CancellationToken cancellationToken = default);

    Task<decimal> GetSuggestedBasicSalaryAsync(int employeeId, CancellationToken cancellationToken = default);

    AllowanceCalculationResult Calculate(decimal basicSalary, IEnumerable<AllowanceItemDraft> items);

    Task<EmployeeAllowancePeriod> SaveAllowancePeriodAsync(
        int employeeId, 
        SaveAllowancePeriodRequest request, 
        string? user = null, 
        CancellationToken cancellationToken = default);

    Task DeletePeriodAsync(int periodId, CancellationToken cancellationToken = default);
}
