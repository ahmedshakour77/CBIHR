using API.HR.Node.Contracts;
using API.HR.Node.Data;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Salary;

public sealed class SalaryScaleService(IDbContextFactory<ApplicationDbContext> dbFactory) : ISalaryScaleService
{
    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await SalaryScaleDataSeeder.SeedAsync(db);
    }

    public async Task<List<SalaryScale>> GetAllScalesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SalaryScales
            .AsNoTracking()
            .Include(s => s.Grade)
            .Include(s => s.Stage)
            .OrderBy(s => s.GradeSortOrder)
            .ThenBy(s => s.StageNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<SalaryScaleMatrixDto> GetMatrixAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Ensure database is populated
        if (!await db.SalaryScales.AnyAsync(cancellationToken))
        {
            await SalaryScaleDataSeeder.SeedAsync(db);
        }

        var scales = await db.SalaryScales
            .AsNoTracking()
            .Include(s => s.Grade)
            .Include(s => s.Stage)
            .OrderBy(s => s.GradeSortOrder)
            .ThenBy(s => s.StageNumber)
            .ToListAsync(cancellationToken);

        // Calculate distribution of employees per (GradeId, StageId)
        var distribution = await db.Employees
            .AsNoTracking()
            .Where(e => e.GradeId != null && e.StageId != null)
            .GroupBy(e => new { GradeId = e.GradeId!.Value, StageId = e.StageId!.Value })
            .Select(g => new
            {
                g.Key.GradeId,
                g.Key.StageId,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var countLookup = distribution.ToDictionary(d => (d.GradeId, d.StageId), d => d.Count);

        // Stages headers (1 to 11)
        var stagesList = await db.Stages
            .AsNoTracking()
            .OrderBy(s => s.StageNumber ?? 99)
            .ToListAsync(cancellationToken);

        var stageHeaders = new List<SalaryScaleStageHeader>();
        for (int i = 1; i <= 11; i++)
        {
            var stg = stagesList.FirstOrDefault(s => s.StageNumber == i || s.Code == i.ToString());
            var totalInStage = scales.Where(s => s.StageNumber == i && countLookup.ContainsKey((s.GradeId, s.StageId)))
                                     .Sum(s => countLookup[(s.GradeId, s.StageId)]);

            stageHeaders.Add(new SalaryScaleStageHeader
            {
                StageNumber = i,
                StageCode = i.ToString(),
                StageName = stg?.Name ?? $"المرحلة {i}",
                TotalEmployees = totalInStage
            });
        }

        // Group rows by Grade
        var rows = new List<SalaryScaleRowDto>();
        var groupedByGrade = scales.GroupBy(s => s.GradeId).OrderBy(g => g.First().GradeSortOrder);

        int totalMapped = 0;
        decimal totalPayrollThousands = 0m;

        foreach (var gradeGroup in groupedByGrade)
        {
            var first = gradeGroup.First();
            var row = new SalaryScaleRowDto
            {
                GradeId = first.GradeId,
                GradeName = first.Grade?.Name ?? first.GradeCode,
                GradeCode = first.GradeCode,
                SortOrder = first.GradeSortOrder,
                AnnualAllowance = first.AnnualAllowance,
                RequiredServiceYears = first.RequiredServiceYears,
                TotalEmployeesInGrade = 0
            };

            foreach (var scale in gradeGroup)
            {
                countLookup.TryGetValue((scale.GradeId, scale.StageId), out int empCount);
                row.TotalEmployeesInGrade += empCount;
                totalMapped += empCount;
                totalPayrollThousands += (empCount * scale.BaseSalary);

                row.Cells[scale.StageNumber] = new SalaryScaleCellDto
                {
                    Id = scale.Id,
                    GradeId = scale.GradeId,
                    StageId = scale.StageId,
                    GradeCode = scale.GradeCode,
                    StageCode = scale.StageCode,
                    StageNumber = scale.StageNumber,
                    BaseSalary = scale.BaseSalary,
                    AnnualAllowance = scale.AnnualAllowance,
                    RequiredServiceYears = scale.RequiredServiceYears,
                    EmployeeCount = empCount
                };
            }

            rows.Add(row);
        }

        return new SalaryScaleMatrixDto
        {
            Stages = stageHeaders,
            Rows = rows,
            TotalMappedEmployees = totalMapped,
            TotalEstimatedPayrollThousands = totalPayrollThousands
        };
    }

    public async Task<SalaryScale?> GetScaleAsync(int gradeId, int stageId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SalaryScales
            .AsNoTracking()
            .Include(s => s.Grade)
            .Include(s => s.Stage)
            .FirstOrDefaultAsync(s => s.GradeId == gradeId && s.StageId == stageId, cancellationToken);
    }

    public async Task<SalaryScale?> GetScaleByCodesAsync(string gradeCode, string stageCode, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SalaryScales
            .AsNoTracking()
            .Include(s => s.Grade)
            .Include(s => s.Stage)
            .FirstOrDefaultAsync(s => s.GradeCode == gradeCode && s.StageCode == stageCode, cancellationToken);
    }

    public async Task UpdateScaleAsync(int id, decimal baseSalary, decimal? allowance, int? serviceYears, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var scale = await db.SalaryScales.FindAsync([id], cancellationToken);
        if (scale == null) return;

        scale.BaseSalary = baseSalary;
        scale.AnnualAllowance = allowance;
        scale.RequiredServiceYears = serviceYears;
        await db.SaveChangesAsync(cancellationToken);
    }
}
