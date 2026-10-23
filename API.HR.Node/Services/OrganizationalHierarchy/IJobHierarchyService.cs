namespace API.HR.Node.Services.OrganizationalHierarchy;

public sealed record JobHierarchyEmployeeItemDto
{
    public int Id { get; init; }
    public string EmployeeID { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? JobTitle { get; init; }
    public string? PositionTitle { get; init; }
    public string? Grade { get; init; }
    public string? Stage { get; init; }
    public string? Gender { get; init; }
    public string? FormationName { get; init; }
    public DateOnly? HireDate { get; init; }
    public bool IsLeader { get; init; }
}

public sealed record JobHierarchyNodeDto
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string Category { get; init; } = string.Empty; // "القيادة العليا", "قطاع رقابي", "دائرة عامة", "مديرية", "قسم", "فرع إقليمي", "شعبة"
    public string Tone { get; init; } = "accent"; // "gold", "accent", "teal", "blue", "emerald", "purple"
    public string? LeaderName { get; init; }
    public string? LeaderTitle { get; init; }
    public string? LeaderEmployeeId { get; init; }
    public int TotalEmployeesCount { get; set; }
    public int DirectEmployeesCount { get; set; }
    public int? FormationId { get; init; }
    public int OrderIndex { get; init; }
    public List<JobHierarchyNodeDto> Children { get; set; } = new();
    public List<JobHierarchyEmployeeItemDto> Employees { get; set; } = new();
}

public sealed record JobGradeHierarchyDto
{
    public string GradeName { get; init; } = string.Empty;
    public string? GradeCode { get; init; }
    public int SortOrder { get; init; }
    public int TotalEmployees { get; init; }
    public List<JobTitleBreakdownDto> JobTitles { get; init; } = new();
}

public sealed record JobTitleBreakdownDto
{
    public string JobTitleName { get; init; } = string.Empty;
    public int EmployeesCount { get; init; }
    public List<JobHierarchyEmployeeItemDto> SampleEmployees { get; init; } = new();
}

public sealed record JobHierarchyOverviewStatsDto
{
    public int TotalEmployees { get; init; }
    public int TotalFormations { get; init; }
    public int LeadershipPositionsCount { get; init; }
    public int BranchCount { get; init; }
    public int TopFormationCount { get; init; }
    public string TopFormationName { get; init; } = string.Empty;
    public int SupervisorySectionsCount { get; init; }
}

public interface IJobHierarchyService
{
    Task<JobHierarchyNodeDto> GetOrgChartTreeAsync(CancellationToken cancellationToken = default);
    Task<List<JobGradeHierarchyDto>> GetJobGradesHierarchyAsync(CancellationToken cancellationToken = default);
    Task<JobHierarchyOverviewStatsDto> GetOverviewStatsAsync(CancellationToken cancellationToken = default);
    Task<int> SyncFormationsToAdministrativeUnitsAsync(CancellationToken cancellationToken = default);
}
