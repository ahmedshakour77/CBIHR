using API.HR.Node.Data.Financial;

namespace API.HR.Node.Contracts;

public sealed record EmployeeFinancialAccountDto(
    int Id,
    int EmployeeId,
    string EmployeeID,
    string EmployeeName,
    decimal Balance,
    DateTime? BalanceLastUpdatedAt,
    decimal LoanRemaining,
    DateTime? LoanLastUpdatedAt,
    decimal AdvanceRemaining,
    DateTime? AdvanceLastUpdatedAt,
    string? Notes,
    DateTime? UpdatedAt,
    string? LastUpdatedBy);

public sealed record EmployeeBalanceTransactionDto(
    int Id,
    int EmployeeFinancialAccountId,
    int EmployeeId,
    FinancialTransactionType TransactionType,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    DateTime TransactionDate,
    string? Description,
    string? ReferenceNumber,
    string? CreatedBy,
    DateTime CreatedAt);

public sealed record CreateBalanceTransactionRequest(
    int EmployeeId,
    FinancialTransactionType TransactionType,
    decimal Amount,
    string? Description = null,
    string? ReferenceNumber = null,
    DateTime? TransactionDate = null);

public sealed record UpdateFinancialAccountRequest(
    int EmployeeId,
    decimal? Balance = null,
    decimal? LoanRemaining = null,
    decimal? AdvanceRemaining = null,
    string? Notes = null);

public sealed record FinancialAccountImportRequest(
    string Mode,
    IReadOnlyList<FinancialAccountImportRow> Rows);

public sealed record FinancialAccountImportRow(
    string EmployeeID,
    string? EmployeeName,
    decimal? Balance,
    decimal? LoanRemaining,
    decimal? AdvanceRemaining,
    string? Notes);

public sealed record FinancialAccountImportResponse(
    int TotalRows,
    int Updated,
    int Skipped,
    IReadOnlyList<string> MissingEmployeeIDs);
