using API.HR.Node.Contracts;
using API.HR.Node.Data;
using API.HR.Node.Data.Allowances;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Allowances;

public sealed class EmployeeAllowanceService(IDbContextFactory<ApplicationDbContext> dbFactory) : IEmployeeAllowanceService
{
    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AllowanceDataSeeder.SeedAsync(db);
    }

    public async Task<List<AllowanceType>> GetAllowanceTypesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.AllowanceTypes.AnyAsync(cancellationToken))
        {
            await AllowanceDataSeeder.SeedAsync(db);
        }

        return await db.AllowanceTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeAllowancePeriod?> GetActivePeriodAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.EmployeeAllowancePeriods
            .AsNoTracking()
            .Include(p => p.Items)
                .ThenInclude(i => i.AllowanceType)
            .OrderByDescending(p => p.StartDate)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.IsCurrent, cancellationToken);
    }

    public async Task<List<EmployeeAllowancePeriod>> GetPeriodsHistoryAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.EmployeeAllowancePeriods
            .AsNoTracking()
            .Include(p => p.Items)
                .ThenInclude(i => i.AllowanceType)
            .Where(p => p.EmployeeId == employeeId)
            .OrderByDescending(p => p.StartDate)
            .ThenByDescending(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetSuggestedBasicSalaryAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var emp = await db.Employees
            .AsNoTracking()
            .Select(e => new { e.Id, e.GradeId, e.StageId })
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

        if (emp == null || !emp.GradeId.HasValue || !emp.StageId.HasValue)
        {
            return 0m;
        }

        var scale = await db.SalaryScales
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.GradeId == emp.GradeId.Value && s.StageId == emp.StageId.Value, cancellationToken);

        if (scale == null)
        {
            return 0m;
        }

        // In Iraqi civil service salary scale, values below 10,000 are recorded in thousands (e.g., 910 = 910,000 IQD)
        return scale.BaseSalary < 10000m ? scale.BaseSalary * 1000m : scale.BaseSalary;
    }

    public AllowanceCalculationResult Calculate(decimal basicSalary, IEnumerable<AllowanceItemDraft> items)
    {
        decimal totalPercentRate = 0m;
        decimal totalPercentAmount = 0m;
        decimal totalFixedAmount = 0m;
        var calcItems = new List<AllowanceCalculatedItemDto>();

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                continue;
            }

            decimal itemAmount = 0m;
            if (item.CalculationType == AllowanceCalculationType.Percentage)
            {
                var rate = item.Percentage ?? 0m;
                totalPercentRate += rate;
                itemAmount = Math.Round((basicSalary * rate) / 100m, 2);
                totalPercentAmount += itemAmount;
            }
            else
            {
                itemAmount = item.FixedAmount ?? 0m;
                totalFixedAmount += itemAmount;
            }

            calcItems.Add(new AllowanceCalculatedItemDto
            {
                Name = item.Name.Trim(),
                CalculationType = item.CalculationType,
                Percentage = item.Percentage,
                FixedAmount = item.FixedAmount,
                CalculatedAmount = itemAmount,
                Notes = item.Notes?.Trim()
            });
        }

        decimal grossSalary = basicSalary + totalPercentAmount + totalFixedAmount;

        return new AllowanceCalculationResult
        {
            BasicSalary = basicSalary,
            TotalPercentageRate = totalPercentRate,
            TotalPercentageAmount = totalPercentAmount,
            TotalFixedAmount = totalFixedAmount,
            GrossSalary = grossSalary,
            Items = calcItems
        };
    }

    public async Task<EmployeeAllowancePeriod> SaveAllowancePeriodAsync(
        int employeeId, 
        SaveAllowancePeriodRequest request, 
        string? user = null, 
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // 1. Close current active periods if any
        var activePeriods = await db.EmployeeAllowancePeriods
            .Where(p => p.EmployeeId == employeeId && p.IsCurrent)
            .ToListAsync(cancellationToken);

        foreach (var active in activePeriods)
        {
            active.IsCurrent = false;
            if (!active.EndDate.HasValue || active.EndDate.Value >= request.StartDate)
            {
                // Terminate previous period on the day before the new period starts
                active.EndDate = request.StartDate > active.StartDate 
                    ? request.StartDate.AddDays(-1) 
                    : request.StartDate;
            }
        }

        // 2. Perform live calculation
        var calc = Calculate(request.BasicSalary, request.Items);

        // 3. Create new period
        var newPeriod = new EmployeeAllowancePeriod
        {
            EmployeeId = employeeId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsCurrent = true,
            BasicSalary = request.BasicSalary,
            TotalPercentageRate = calc.TotalPercentageRate,
            TotalPercentageAmount = calc.TotalPercentageAmount,
            TotalFixedAmount = calc.TotalFixedAmount,
            GrossSalary = calc.GrossSalary,
            OrderNumber = request.OrderNumber?.Trim(),
            OrderDate = request.OrderDate,
            ChangeReason = request.ChangeReason?.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = user,
            Items = []
        };

        foreach (var ci in calc.Items)
        {
            var draftMatch = request.Items.FirstOrDefault(i => i.Name == ci.Name);
            newPeriod.Items.Add(new EmployeeAllowanceItem
            {
                AllowanceTypeId = draftMatch?.AllowanceTypeId,
                Name = ci.Name,
                CalculationType = ci.CalculationType,
                Percentage = ci.Percentage,
                FixedAmount = ci.FixedAmount,
                CalculatedAmount = ci.CalculatedAmount,
                Notes = ci.Notes
            });
        }

        db.EmployeeAllowancePeriods.Add(newPeriod);
        await db.SaveChangesAsync(cancellationToken);

        return newPeriod;
    }

    public async Task DeletePeriodAsync(int periodId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var period = await db.EmployeeAllowancePeriods
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

        if (period == null)
        {
            return;
        }

        var empId = period.EmployeeId;
        var wasCurrent = period.IsCurrent;

        db.EmployeeAllowanceItems.RemoveRange(period.Items);
        db.EmployeeAllowancePeriods.Remove(period);
        await db.SaveChangesAsync(cancellationToken);

        // If the deleted period was current, reactivate the latest previous period
        if (wasCurrent)
        {
            var latest = await db.EmployeeAllowancePeriods
                .Where(p => p.EmployeeId == empId)
                .OrderByDescending(p => p.StartDate)
                .ThenByDescending(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (latest != null)
            {
                latest.IsCurrent = true;
                latest.EndDate = null;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
