using API.HR.Node.Contracts;
using API.HR.Node.Data.Financial;

namespace API.HR.Node.Services.Financial;

public interface IEmployeeFinancialService
{
    Task<EmployeeFinancialAccountDto> GetOrCreateAccountAsync(int employeeId, CancellationToken ct = default);

    Task<EmployeeFinancialAccountDto?> GetAccountByEmployeeIdAsync(int employeeId, CancellationToken ct = default);

    Task<EmployeeFinancialAccountDto> UpdateBalancesAsync(
        int employeeId,
        decimal? balance,
        decimal? loanRemaining,
        decimal? advanceRemaining,
        string? notes = null,
        string? updatedBy = null,
        CancellationToken ct = default);

    Task<EmployeeBalanceTransactionDto> AddTransactionAsync(
        int employeeId,
        FinancialTransactionType type,
        decimal amount,
        string? description = null,
        string? referenceNumber = null,
        DateTime? transactionDate = null,
        string? createdBy = null,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<EmployeeBalanceTransactionDto>> GetTransactionsAsync(int employeeId, int take = 100, CancellationToken ct = default);
}
