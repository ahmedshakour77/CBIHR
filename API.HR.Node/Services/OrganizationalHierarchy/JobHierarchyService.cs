using API.HR.Node.Data;
using API.HR.Node.Data.OrganizationalHierarchy;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.OrganizationalHierarchy;

public sealed class JobHierarchyService : IJobHierarchyService
{
    private readonly ApplicationDbContext _db;
    private readonly IOrganizationalHierarchyService _orgService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobHierarchyService> _logger;

    public JobHierarchyService(
        ApplicationDbContext db,
        IOrganizationalHierarchyService orgService,
        IServiceProvider serviceProvider,
        ILogger<JobHierarchyService> logger)
    {
        _db = db;
        _orgService = orgService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<JobHierarchyOverviewStatsDto> GetOverviewStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalUnits = await _db.AdministrativeUnits.AsNoTracking().CountAsync(cancellationToken);
        var totalEmployeesInHierarchy = await _db.Employees.AsNoTracking()
            .CountAsync(e => e.AdministrativeUnitId.HasValue, cancellationToken);

        var totalAllEmployees = await _db.Employees.AsNoTracking().CountAsync(cancellationToken);

        var leadershipCount = await _db.AdministrativeUnits.AsNoTracking()
            .CountAsync(u => !string.IsNullOrWhiteSpace(u.ManagerEmployeeId) || !string.IsNullOrWhiteSpace(u.ManagerTitle), cancellationToken);

        var topUnit = await _db.AdministrativeUnits.AsNoTracking()
            .Select(u => new
            {
                u.Name,
                Count = _db.Employees.Count(e => e.AdministrativeUnitId == u.Id)
            })
            .OrderByDescending(x => x.Count)
            .FirstOrDefaultAsync(cancellationToken);

        var branchesCount = await _db.AdministrativeUnits.AsNoTracking()
            .CountAsync(u => u.UnitType != null && u.UnitType.Code == "BRN", cancellationToken);

        var sectionsCount = await _db.AdministrativeUnits.AsNoTracking()
            .CountAsync(u => u.UnitType != null && u.UnitType.Code == "SEC", cancellationToken);

        return new JobHierarchyOverviewStatsDto
        {
            TotalEmployees = totalEmployeesInHierarchy > 0 ? totalEmployeesInHierarchy : totalAllEmployees,
            TotalFormations = totalUnits,
            LeadershipPositionsCount = leadershipCount,
            SupervisorySectionsCount = sectionsCount,
            BranchCount = branchesCount,
            TopFormationCount = topUnit?.Count ?? 0,
            TopFormationName = topUnit?.Name ?? "الوحدة الإدارية الرئيسية"
        };
    }

    public async Task<JobHierarchyNodeDto> GetOrgChartTreeAsync(CancellationToken cancellationToken = default)
    {
        // 1. Fetch the exact organizational hierarchy tree that was created
        var treeRoots = await _orgService.GetHierarchyTreeAsync();

        if (treeRoots.Count == 0)
        {
            return new JobHierarchyNodeDto
            {
                Id = "empty-tree",
                Title = "الهيكلية الإدارية فارغة حالياً",
                Category = "غير محدد",
                Tone = "gold",
                LeaderTitle = "يرجى إضافة التشكيلات في صفحة الهيكلية الإدارية"
            };
        }

        // If there is a single root node (e.g. المحافظية), map and return it
        if (treeRoots.Count == 1)
        {
            return MapHierarchyNode(treeRoots[0]);
        }

        // If there are multiple root nodes, create a unified executive umbrella root
        var mappedRoots = treeRoots.Select(MapHierarchyNode).ToList();
        var totalEmps = mappedRoots.Sum(r => r.TotalEmployeesCount);

        return new JobHierarchyNodeDto
        {
            Id = "cbi-umbrella-root",
            Title = "الهيكل الإداري والوظيفي العام",
            Code = "CBI-TREE",
            Category = "رأس الهرم الإداري",
            Tone = "gold",
            LeaderTitle = "الإدارة العليا للبنك المركزي",
            TotalEmployeesCount = totalEmps,
            DirectEmployeesCount = 0,
            Children = mappedRoots
        };
    }

    private static JobHierarchyNodeDto MapHierarchyNode(HierarchyTreeNodeDto node)
    {
        var tone = node.TypeCode?.ToUpperInvariant() switch
        {
            "GOV" => "gold",
            "DEPT" => "teal",
            "DIR" => "blue",
            "DIV" => "purple",
            "SEC" => "emerald",
            "UNIT" => "amber",
            "OFF" => "rose",
            "BRN" => "violet",
            _ => node.HierarchyLevel switch
            {
                1 => "gold",
                2 => "teal",
                3 => "blue",
                4 => "purple",
                5 => "emerald",
                _ => "accent"
            }
        };

        var mappedChildren = node.Children.Select(MapHierarchyNode).ToList();

        var employees = node.Employees.Select(e => new JobHierarchyEmployeeItemDto
        {
            Id = e.Id,
            EmployeeID = e.EmployeeID,
            Name = e.Name,
            JobTitle = e.JobTitle,
            PositionTitle = e.PositionTitle,
            FormationName = e.AdministrativeUnitName,
            IsLeader = e.IsManager
        }).ToList();

        var totalCount = employees.Count + mappedChildren.Sum(c => c.TotalEmployeesCount);

        return new JobHierarchyNodeDto
        {
            Id = $"unit-{node.Id}",
            Title = node.Name,
            Code = node.Code,
            Category = node.TypeName,
            Tone = tone,
            LeaderName = node.ManagerName,
            LeaderTitle = node.ManagerTitle,
            LeaderEmployeeId = node.ManagerEmployeeId,
            DirectEmployeesCount = employees.Count,
            TotalEmployeesCount = totalCount,
            FormationId = node.Id,
            OrderIndex = node.OrderIndex,
            Children = mappedChildren,
            Employees = employees
        };
    }

    public async Task<List<JobGradeHierarchyDto>> GetJobGradesHierarchyAsync(CancellationToken cancellationToken = default)
    {
        var rawEmployees = await _db.Employees.AsNoTracking()
            .Include(e => e.Grade)
            .Include(e => e.JobTitle)
            .Include(e => e.PositionTitle)
            .Include(e => e.AdministrativeUnit)
            .ToListAsync(cancellationToken);

        var grades = await _db.Grades.AsNoTracking()
            .OrderBy(g => g.SortOrder ?? 99)
            .ToListAsync(cancellationToken);

        var result = new List<JobGradeHierarchyDto>();

        foreach (var grade in grades)
        {
            var empsInGrade = rawEmployees.Where(e => e.GradeId == grade.Id).ToList();
            if (empsInGrade.Count == 0) continue;

            var jobTitleBreakdowns = empsInGrade
                .GroupBy(e => e.JobTitle?.Name ?? "غير محدد")
                .Select(g => new JobTitleBreakdownDto
                {
                    JobTitleName = g.Key,
                    EmployeesCount = g.Count(),
                    SampleEmployees = g.Take(5).Select(e => new JobHierarchyEmployeeItemDto
                    {
                        Id = e.Id,
                        EmployeeID = e.EmployeeID,
                        Name = e.Name,
                        JobTitle = e.JobTitle?.Name,
                        PositionTitle = e.PositionTitle?.Name,
                        Grade = grade.Name,
                        FormationName = e.AdministrativeUnit?.Name,
                        HireDate = e.HireDate
                    }).ToList()
                })
                .OrderByDescending(j => j.EmployeesCount)
                .ToList();

            result.Add(new JobGradeHierarchyDto
            {
                GradeName = grade.Name,
                GradeCode = grade.Code,
                SortOrder = grade.SortOrder ?? 0,
                TotalEmployees = empsInGrade.Count,
                JobTitles = jobTitleBreakdowns
            });
        }

        return result;
    }

    public async Task<int> SyncFormationsToAdministrativeUnitsAsync(CancellationToken cancellationToken = default)
    {
        // 1. Ensure baseline AdministrativeUnitTypes exist
        var types = await _db.AdministrativeUnitTypes.ToListAsync(cancellationToken);
        if (types.Count == 0)
        {
            await OrganizationalHierarchySeeder.SeedAsync(_serviceProvider);
            types = await _db.AdministrativeUnitTypes.ToListAsync(cancellationToken);
        }

        var govType = types.FirstOrDefault(t => t.Code == "GOV") ?? types.First();
        var deptType = types.FirstOrDefault(t => t.Code == "DEPT") ?? types.First();
        var dirType = types.FirstOrDefault(t => t.Code == "DIR") ?? types.First();
        var brnType = types.FirstOrDefault(t => t.Code == "BRN") ?? types.First();
        var offType = types.FirstOrDefault(t => t.Code == "OFF") ?? types.First();

        var existingUnits = await _db.AdministrativeUnits.ToListAsync(cancellationToken);
        var existingByName = existingUnits.ToDictionary(u => u.Name, u => u, StringComparer.OrdinalIgnoreCase);

        // Ensure Root: المحافظية
        if (!existingByName.TryGetValue("المحافظية", out var rootGovUnit))
        {
            rootGovUnit = new AdministrativeUnit
            {
                Name = "المحافظية",
                Code = "GOV-01",
                UnitTypeId = govType.Id,
                ParentUnitId = null,
                ManagerTitle = "محافظ البنك المركزي العراقي",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.AdministrativeUnits.Add(rootGovUnit);
            await _db.SaveChangesAsync(cancellationToken);
            existingByName["المحافظية"] = rootGovUnit;
        }

        // Add each Formation as an AdministrativeUnit under المحافظية
        var formations = await _db.Formations.AsNoTracking().ToListAsync(cancellationToken);
        int addedCount = 0;

        foreach (var f in formations)
        {
            if (f.Name.Equals("المحافظية", StringComparison.OrdinalIgnoreCase)) continue;

            if (!existingByName.TryGetValue(f.Name, out var unit))
            {
                int typeId = deptType.Id;
                if (f.Name.Contains("فرع")) typeId = brnType.Id;
                else if (f.Name.Contains("مديرية")) typeId = dirType.Id;
                else if (f.Name.Contains("مكتب")) typeId = offType.Id;

                unit = new AdministrativeUnit
                {
                    Name = f.Name,
                    Code = $"U-{f.Id:D2}",
                    UnitTypeId = typeId,
                    ParentUnitId = rootGovUnit.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.AdministrativeUnits.Add(unit);
                addedCount++;
                existingByName[f.Name] = unit;
            }
        }

        if (addedCount > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Map employees to AdministrativeUnits based on Formation Name
        var allUnits = await _db.AdministrativeUnits.ToListAsync(cancellationToken);
        var unitLookup = allUnits.ToDictionary(u => u.Name, u => u.Id, StringComparer.OrdinalIgnoreCase);

        var employees = await _db.Employees.Include(e => e.Formation).ToListAsync(cancellationToken);
        int mappedCount = 0;

        foreach (var emp in employees)
        {
            if (emp.Formation != null && unitLookup.TryGetValue(emp.Formation.Name, out var uId))
            {
                if (emp.AdministrativeUnitId != uId)
                {
                    emp.AdministrativeUnitId = uId;
                    mappedCount++;
                }
            }
        }

        if (mappedCount > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Synced {Added} formations and mapped {Mapped} employees to AdministrativeUnits.", addedCount, mappedCount);
        return addedCount + mappedCount;
    }
}
