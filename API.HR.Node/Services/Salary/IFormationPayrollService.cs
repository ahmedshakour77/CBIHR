using API.HR.Node.Contracts;
using API.HR.Node.Data;

namespace API.HR.Node.Services.Salary;

public interface IFormationPayrollService
{
    Task<MonthlyPayrollReportDto> GetMonthlyPayrollReportAsync(PayrollFilterDto filter, CancellationToken cancellationToken = default);
    Task<List<Formation>> GetAvailableFormationsAsync(CancellationToken cancellationToken = default);
    byte[] GeneratePayrollCsv(MonthlyPayrollReportDto report);
}
