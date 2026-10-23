using API.HR.Node.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace API.HR.Node.Services.Dashboard;

/// <summary>
/// One employee, flattened to the fields the home dashboard counts. Lookup names are
/// resolved and child tables reduced to flags and counts, so every chart on the page
/// can be computed from this list in memory.
/// </summary>
public sealed class WorkforceRow
{
    public int Id { get; init; }
    public string EmployeeId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Formation { get; init; } = WorkforceSnapshot.Unset;
    public int? FormationId { get; init; }
    public string Unit { get; init; } = WorkforceSnapshot.Unset;
    public string JobTitle { get; init; } = WorkforceSnapshot.Unset;
    public string? Position { get; init; }
    public string Grade { get; init; } = WorkforceSnapshot.Unset;
    public string? GradeCode { get; init; }
    public int GradeSort { get; init; } = 99;
    public string Stage { get; init; } = WorkforceSnapshot.Unset;
    public string? StageCode { get; init; }
    public int? StageNumber { get; init; }
    public string Education { get; init; } = WorkforceSnapshot.Unset;
    public string Specialization { get; init; } = WorkforceSnapshot.Unset;
    public string Gender { get; init; } = WorkforceSnapshot.Unset;
    public string Religion { get; init; } = WorkforceSnapshot.Unset;
    public string Nationality { get; init; } = WorkforceSnapshot.Unset;
    public string Marital { get; init; } = WorkforceSnapshot.Unset;
    public string Status { get; init; } = WorkforceSnapshot.Unset;
    public string? FirstDepartment { get; init; }
    public DateOnly? BirthDate { get; init; }
    public DateOnly? HireDate { get; init; }
    public DateOnly? LastJobTitleDate { get; init; }
    public decimal? LeaveBalance { get; init; }

    public bool HasPhoto { get; init; }
    public bool HasPhone { get; init; }
    public bool HasEmail { get; init; }
    public bool HasAccount { get; init; }
    public bool HasNationalCard { get; init; }
    public bool HasPassport { get; init; }
    public bool HasMartyr { get; init; }
    public string? Governorate { get; init; }
    public int Vehicles { get; init; }
    public int AuthorizedVehicles { get; init; }
    public int Courses { get; init; }
    public int Letters { get; init; }
    public int Penalties { get; init; }

    public bool HasContacts => HasPhone && HasEmail;
}

public sealed record LetterFact(int EmployeeId, string Type, int Count, int SeniorityYears, int SeniorityMonths, int SeniorityDays);
public sealed record LeaveFact(int EmployeeId, string Type, DateOnly Start, DateOnly End, int Days);
public sealed record PenaltyFact(int EmployeeId, string Type, DateOnly? Date);

/// <summary>The whole non-archived workforce as of <see cref="LoadedAt"/>.</summary>
public sealed class WorkforceSnapshot
{
    public const string Unset = "غير محدد";

    public required IReadOnlyList<WorkforceRow> Rows { get; init; }
    public required IReadOnlyList<LetterFact> Letters { get; init; }
    /// <summary>Leaves that touch the current year and were not cancelled or rejected.</summary>
    public required IReadOnlyList<LeaveFact> Leaves { get; init; }
    public required IReadOnlyList<PenaltyFact> Penalties { get; init; }
    public DateTime LoadedAt { get; init; }

    public static WorkforceSnapshot Empty { get; } = new() { Rows = [], Letters = [], Leaves = [], Penalties = [] };
}

/// <summary>
/// Loads <see cref="WorkforceSnapshot"/> once and shares it for two minutes, so a filter
/// change on the dashboard re-counts in memory instead of re-querying SQL.
/// </summary>
public sealed class WorkforceSnapshotService(IServiceScopeFactory scopeFactory, IMemoryCache cache)
{
    private const string CacheKey = "home:workforce-snapshot";
    private const string CancelledLeave = "ملغاة";
    private const string RejectedLeave = "مرفوضة";
    private readonly SemaphoreSlim gate = new(1, 1);

    public void Invalidate() => cache.Remove(CacheKey);

    public async Task<WorkforceSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out WorkforceSnapshot? cached) && cached is not null)
            return cached;

        // Two dashboards opening together share one load rather than racing two.
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(CacheKey, out cached) && cached is not null)
                return cached;

            var snapshot = await LoadAsync(cancellationToken);
            cache.Set(CacheKey, snapshot, TimeSpan.FromMinutes(2));
            return snapshot;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<WorkforceSnapshot> LoadAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var employees = await db.Employees.AsNoTracking()
            .Select(e => new
            {
                e.Id,
                e.EmployeeID,
                e.Name,
                e.FormationId,
                Formation = e.Formation != null ? e.Formation.Name : null,
                Unit = e.AdministrativeUnit != null ? e.AdministrativeUnit.Name : null,
                JobTitle = e.JobTitle != null ? e.JobTitle.Name : null,
                Position = e.PositionTitle != null ? e.PositionTitle.Name : null,
                Grade = e.Grade != null ? e.Grade.Name : null,
                GradeCode = e.Grade != null ? e.Grade.Code : null,
                GradeSort = e.Grade != null ? e.Grade.SortOrder : null,
                Stage = e.Stage != null ? e.Stage.Name : null,
                StageCode = e.Stage != null ? e.Stage.Code : null,
                StageNumber = e.Stage != null ? e.Stage.StageNumber : null,
                Education = e.EducationLevel != null ? e.EducationLevel.Name : null,
                Specialization = e.Specialization != null ? e.Specialization.Name : null,
                Gender = e.Gender != null ? e.Gender.Name : null,
                Religion = e.Religion != null ? e.Religion.Name : null,
                Nationality = e.Nationality != null ? e.Nationality.Name : null,
                Marital = e.MaritalStatus != null ? e.MaritalStatus.Name : null,
                Status = e.EmployeeStatus != null ? e.EmployeeStatus.Name : null,
                e.FirstDepartment,
                e.BirthDate,
                e.HireDate,
                e.LastJobTitleDate,
                e.LeaveBalance
            })
            .ToListAsync(ct);

        var ids = employees.Select(e => e.Id).ToHashSet();

        var photos = (await db.EmployeeProfilePhotos.AsNoTracking().Where(p => p.IsActive).Select(p => p.EmployeeID).Distinct().ToListAsync(ct)).ToHashSet();
        var phones = (await db.EmployeePhones.AsNoTracking().Select(p => p.EmployeeID).Distinct().ToListAsync(ct)).ToHashSet();
        var emails = (await db.EmployeeEmails.AsNoTracking().Select(p => p.EmployeeID).Distinct().ToListAsync(ct)).ToHashSet();
        var accounts = (await db.Users.AsNoTracking().Where(u => u.EmployeeID != null).Select(u => u.EmployeeID!).Distinct().ToListAsync(ct)).ToHashSet();
        var cards = (await db.EmployeeNationalCards.AsNoTracking().Select(c => c.EmployeeId).Distinct().ToListAsync(ct)).ToHashSet();
        var passports = (await db.EmployeePassports.AsNoTracking().Select(p => p.EmployeeId).Distinct().ToListAsync(ct)).ToHashSet();
        var martyrs = (await db.EmployeeMartyrs.AsNoTracking().Select(m => m.EmployeeId).Distinct().ToListAsync(ct)).ToHashSet();

        // An employee may have moved; the newest residence names where they live now.
        var governorates = (await db.EmployeeResidences.AsNoTracking()
                .Select(r => new { r.EmployeeId, r.CreatedAt, Name = r.Governorate != null ? r.Governorate.Name : null })
                .ToListAsync(ct))
            .GroupBy(r => r.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First().Name ?? WorkforceSnapshot.Unset);

        var vehicles = await db.EmployeeVehicles.AsNoTracking()
            .GroupBy(v => v.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Total = g.Count(), Authorized = g.Count(v => v.IsAuthorized) })
            .ToDictionaryAsync(v => v.EmployeeId, ct);

        var courses = await db.EmployeeCourses.AsNoTracking()
            .GroupBy(c => c.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(c => c.EmployeeId, c => c.Total, ct);

        var letters = (await db.AppreciationLetters.AsNoTracking()
                .Select(l => new
                {
                    l.EmployeeId,
                    Type = l.Type != null ? l.Type.Name : null,
                    l.Count,
                    Years = l.Type != null ? l.Type.SeniorityYears : 0,
                    Months = l.Type != null ? l.Type.SeniorityMonths : 0,
                    Days = l.Type != null ? l.Type.SeniorityDays : 0
                })
                .ToListAsync(ct))
            .Where(l => ids.Contains(l.EmployeeId))
            .Select(l => new LetterFact(l.EmployeeId, l.Type ?? "عام / غير محدد", l.Count ?? 1, l.Years, l.Months, l.Days))
            .ToList();

        var yearStart = new DateOnly(DateTime.Today.Year, 1, 1);
        var leaves = (await db.EmployeeLeaves.AsNoTracking()
                .Where(l => l.EndDate >= yearStart && l.Status != CancelledLeave && l.Status != RejectedLeave)
                .Select(l => new { l.EmployeeId, Type = l.LeaveType != null ? l.LeaveType.Name : null, l.StartDate, l.EndDate, l.DaysCount })
                .ToListAsync(ct))
            .Where(l => ids.Contains(l.EmployeeId))
            .Select(l => new LeaveFact(l.EmployeeId, l.Type ?? "عام / غير محدد", l.StartDate, l.EndDate, l.DaysCount))
            .ToList();

        var penalties = (await db.EmployeePenalties.AsNoTracking()
                .Select(p => new { p.EmployeeId, Type = p.PenaltyType != null ? p.PenaltyType.Name : null, p.PenaltyDate })
                .ToListAsync(ct))
            .Where(p => ids.Contains(p.EmployeeId))
            .Select(p => new PenaltyFact(p.EmployeeId, p.Type ?? "غير مصنفة", p.PenaltyDate))
            .ToList();

        var letterCounts = letters.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(l => l.Count));
        var penaltyCounts = penalties.GroupBy(p => p.EmployeeId).ToDictionary(g => g.Key, g => g.Count());

        static string Name(string? value) => string.IsNullOrWhiteSpace(value) ? WorkforceSnapshot.Unset : value.Trim();

        var rows = employees.Select(e => new WorkforceRow
        {
            Id = e.Id,
            EmployeeId = e.EmployeeID,
            Name = e.Name,
            FormationId = e.FormationId,
            Formation = Name(e.Formation),
            Unit = Name(e.Unit),
            JobTitle = Name(e.JobTitle),
            Position = string.IsNullOrWhiteSpace(e.Position) || e.Position == WorkforceSnapshot.Unset ? null : e.Position.Trim(),
            Grade = Name(e.Grade),
            GradeCode = e.GradeCode,
            GradeSort = e.GradeSort ?? 99,
            Stage = Name(e.Stage),
            StageCode = e.StageCode,
            StageNumber = e.StageNumber,
            Education = Name(e.Education),
            Specialization = Name(e.Specialization),
            Gender = WorkforceStats.NormalizeGender(e.Gender),
            Religion = Name(e.Religion),
            Nationality = Name(e.Nationality),
            Marital = Name(e.Marital),
            Status = Name(e.Status),
            FirstDepartment = string.IsNullOrWhiteSpace(e.FirstDepartment) ? null : e.FirstDepartment.Trim(),
            BirthDate = e.BirthDate,
            HireDate = e.HireDate,
            LastJobTitleDate = e.LastJobTitleDate,
            LeaveBalance = e.LeaveBalance,
            HasPhoto = photos.Contains(e.EmployeeID),
            HasPhone = phones.Contains(e.EmployeeID),
            HasEmail = emails.Contains(e.EmployeeID),
            HasAccount = accounts.Contains(e.EmployeeID),
            HasNationalCard = cards.Contains(e.Id),
            HasPassport = passports.Contains(e.Id),
            HasMartyr = martyrs.Contains(e.Id),
            Governorate = governorates.GetValueOrDefault(e.Id),
            Vehicles = vehicles.TryGetValue(e.Id, out var v) ? v.Total : 0,
            AuthorizedVehicles = vehicles.TryGetValue(e.Id, out var av) ? av.Authorized : 0,
            Courses = courses.GetValueOrDefault(e.Id),
            Letters = letterCounts.GetValueOrDefault(e.Id),
            Penalties = penaltyCounts.GetValueOrDefault(e.Id)
        }).ToList();

        return new WorkforceSnapshot
        {
            Rows = rows,
            Letters = letters,
            Leaves = leaves,
            Penalties = penalties,
            LoadedAt = DateTime.Now
        };
    }
}
