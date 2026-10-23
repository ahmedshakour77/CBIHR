using API.HR.Node.Contracts;
using API.HR.Node.Data;

namespace API.HR.Node.Services.Salary;

public interface ISalaryScaleService
{
    Task<SalaryScaleMatrixDto> GetMatrixAsync(CancellationToken cancellationToken = default);
    Task<List<SalaryScale>> GetAllScalesAsync(CancellationToken cancellationToken = default);
    Task<SalaryScale?> GetScaleAsync(int gradeId, int stageId, CancellationToken cancellationToken = default);
    Task<SalaryScale?> GetScaleByCodesAsync(string gradeCode, string stageCode, CancellationToken cancellationToken = default);
    Task UpdateScaleAsync(int id, decimal baseSalary, decimal? allowance, int? serviceYears, CancellationToken cancellationToken = default);
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);
}
