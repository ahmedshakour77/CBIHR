using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Data.Financial;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Financial;

public sealed class EmployeeFinancialService : IEmployeeFinancialService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<EmployeeFinancialService> _logger;

    public EmployeeFinancialService(ApplicationDbContext db, ILogger<EmployeeFinancialService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<EmployeeFinancialAccountDto> GetOrCreateAccountAsync(int employeeId, CancellationToken ct = default)
    {
        var account = await _db.EmployeeFinancialAccounts
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId, ct);

        if (account is not null)
        {
            return MapToDto(account);
        }

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException($"الموظف بالمعرّف {employeeId} غير موجود.");

        account = new EmployeeFinancialAccount
        {
            EmployeeId = employeeId,
            Employee = employee,
            Balance = 0m,
            BalanceLastUpdatedAt = null,
            LoanRemaining = 0m,
            LoanLastUpdatedAt = null,
            AdvanceRemaining = 0m,
            AdvanceLastUpdatedAt = null,
            CreatedAt = DateTime.UtcNow
        };

        _db.EmployeeFinancialAccounts.Add(account);
        await _db.SaveChangesAsync(ct);

        return MapToDto(account);
    }

    public async Task<EmployeeFinancialAccountDto?> GetAccountByEmployeeIdAsync(int employeeId, CancellationToken ct = default)
    {
        var account = await _db.EmployeeFinancialAccounts
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId, ct);

        return account is null ? null : MapToDto(account);
    }

    public async Task<EmployeeFinancialAccountDto> UpdateBalancesAsync(
        int employeeId,
        decimal? balance,
        decimal? loanRemaining,
        decimal? advanceRemaining,
        string? notes = null,
        string? updatedBy = null,
        CancellationToken ct = default)
    {
        var account = await _db.EmployeeFinancialAccounts
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId, ct);

        var now = DateTime.UtcNow;

        if (account is null)
        {
            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
                ?? throw new InvalidOperationException($"الموظف بالمعرّف {employeeId} غير موجود.");

            account = new EmployeeFinancialAccount
            {
                EmployeeId = employeeId,
                Employee = employee,
                CreatedAt = now
            };
            _db.EmployeeFinancialAccounts.Add(account);
        }

        var balanceChanged = false;
        var oldBalance = account.Balance;

        if (balance.HasValue && balance.Value != account.Balance)
        {
            account.Balance = balance.Value;
            account.BalanceLastUpdatedAt = now;
            balanceChanged = true;
        }

        if (loanRemaining.HasValue && loanRemaining.Value != account.LoanRemaining)
        {
            account.LoanRemaining = loanRemaining.Value;
            account.LoanLastUpdatedAt = now;
        }

        if (advanceRemaining.HasValue && advanceRemaining.Value != account.AdvanceRemaining)
        {
            account.AdvanceRemaining = advanceRemaining.Value;
            account.AdvanceLastUpdatedAt = now;
        }

        if (notes is not null)
        {
            account.Notes = notes;
        }

        account.UpdatedAt = now;
        account.LastUpdatedBy = updatedBy;

        // إذا تغير الرصيد مباشرة، نسجل حركة تسوية تلقائياً لتوثيق الحركة بدقة
        if (balanceChanged)
        {
            var diff = account.Balance - oldBalance;
            var trx = new EmployeeBalanceTransaction
            {
                EmployeeFinancialAccountId = account.Id,
                FinancialAccount = account,
                EmployeeId = employeeId,
                TransactionType = FinancialTransactionType.Adjustment,
                Amount = Math.Abs(diff),
                BalanceBefore = oldBalance,
                BalanceAfter = account.Balance,
                TransactionDate = now,
                Description = string.IsNullOrWhiteSpace(notes) ? "تسوية رصيد يدوي / تحديث مباشر" : $"تسوية رصيد: {notes}",
                ReferenceNumber = "ADJ-" + now.ToString("yyyyMMddHHmmss"),
                CreatedBy = updatedBy,
                CreatedAt = now
            };
            _db.EmployeeBalanceTransactions.Add(trx);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            return MapToDto(account);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Financial concurrency conflict on employee account {EmployeeId}", employeeId);
            throw new InvalidOperationException("FINANCIAL_CONCURRENCY_CONFLICT: تم تعديل السجل المالي بالتزامن من قِبل عملية أخرى. يرجى إعادة المحاولة.", ex);
        }
    }

    public async Task<EmployeeBalanceTransactionDto> AddTransactionAsync(
        int employeeId,
        FinancialTransactionType type,
        decimal amount,
        string? description = null,
        string? referenceNumber = null,
        DateTime? transactionDate = null,
        string? createdBy = null,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (amount < 0)
        {
            throw new ArgumentException("قيمة المبلغ يجب أن تكون موجبة.", nameof(amount));
        }

        // 1. Idempotency Check
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingTx = await _db.EmployeeBalanceTransactions
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);
            if (existingTx != null)
            {
                _logger.LogInformation("Returning existing transaction {TransactionId} for idempotency key '{Key}'", existingTx.Id, idempotencyKey);
                return new EmployeeBalanceTransactionDto(
                    existingTx.Id,
                    existingTx.EmployeeFinancialAccountId,
                    existingTx.EmployeeId,
                    existingTx.TransactionType,
                    existingTx.Amount,
                    existingTx.BalanceBefore,
                    existingTx.BalanceAfter,
                    existingTx.TransactionDate,
                    existingTx.Description,
                    existingTx.ReferenceNumber,
                    existingTx.CreatedBy,
                    existingTx.CreatedAt);
            }
        }

        await using var dbTx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var account = await _db.EmployeeFinancialAccounts
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId, ct);

            var now = DateTime.UtcNow;
            var effectiveTxDate = transactionDate ?? now;

            if (account is null)
            {
                var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
                    ?? throw new InvalidOperationException($"الموظف بالمعرّف {employeeId} غير موجود.");

                account = new EmployeeFinancialAccount
                {
                    EmployeeId = employeeId,
                    Employee = employee,
                    Balance = 0m,
                    CreatedAt = now
                };
                _db.EmployeeFinancialAccounts.Add(account);
                await _db.SaveChangesAsync(ct);
            }

            var balanceBefore = account.Balance;
            decimal balanceAfter;

            switch (type)
            {
                case FinancialTransactionType.Deposit:
                    balanceAfter = balanceBefore + amount;
                    break;
                case FinancialTransactionType.Withdrawal:
                    balanceAfter = balanceBefore - amount;
                    break;
                case FinancialTransactionType.InitialBalance:
                case FinancialTransactionType.Adjustment:
                    balanceAfter = amount;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), "نوع الحركة المالية غير مدعوم.");
            }

            account.Balance = balanceAfter;
            account.BalanceLastUpdatedAt = effectiveTxDate;
            account.UpdatedAt = now;
            account.LastUpdatedBy = createdBy;

            var transaction = new EmployeeBalanceTransaction
            {
                EmployeeFinancialAccountId = account.Id,
                FinancialAccount = account,
                EmployeeId = employeeId,
                TransactionType = type,
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                TransactionDate = effectiveTxDate,
                Description = description,
                ReferenceNumber = referenceNumber,
                IdempotencyKey = idempotencyKey,
                CreatedBy = createdBy,
                CreatedAt = now
            };

            _db.EmployeeBalanceTransactions.Add(transaction);
            await _db.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            return new EmployeeBalanceTransactionDto(
                transaction.Id,
                transaction.EmployeeFinancialAccountId,
                transaction.EmployeeId,
                transaction.TransactionType,
                transaction.Amount,
                transaction.BalanceBefore,
                transaction.BalanceAfter,
                transaction.TransactionDate,
                transaction.Description,
                transaction.ReferenceNumber,
                transaction.CreatedBy,
                transaction.CreatedAt);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await dbTx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Financial balance concurrency conflict on Employee #{EmployeeId}", employeeId);
            throw new InvalidOperationException("FINANCIAL_CONCURRENCY_CONFLICT: حدث تعارض تزامني أثناء تحديث الرصيد المالي للموظف. يرجى إعادة المحاولة.", ex);
        }
    }

    public async Task<IReadOnlyList<EmployeeBalanceTransactionDto>> GetTransactionsAsync(int employeeId, int take = 100, CancellationToken ct = default)
    {
        var list = await _db.EmployeeBalanceTransactions
            .Where(t => t.EmployeeId == employeeId)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .Take(take)
            .Select(t => new EmployeeBalanceTransactionDto(
                t.Id,
                t.EmployeeFinancialAccountId,
                t.EmployeeId,
                t.TransactionType,
                t.Amount,
                t.BalanceBefore,
                t.BalanceAfter,
                t.TransactionDate,
                t.Description,
                t.ReferenceNumber,
                t.CreatedBy,
                t.CreatedAt))
            .ToListAsync(ct);

        return list;
    }

    private static EmployeeFinancialAccountDto MapToDto(EmployeeFinancialAccount account)
    {
        return new EmployeeFinancialAccountDto(
            account.Id,
            account.EmployeeId,
            account.Employee?.EmployeeID ?? string.Empty,
            account.Employee?.Name ?? string.Empty,
            account.Balance,
            account.BalanceLastUpdatedAt,
            account.LoanRemaining,
            account.LoanLastUpdatedAt,
            account.AdvanceRemaining,
            account.AdvanceLastUpdatedAt,
            account.Notes,
            account.UpdatedAt,
            account.LastUpdatedBy);
    }
}
