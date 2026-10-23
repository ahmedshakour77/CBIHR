using System.Security.Claims;

namespace API.HR.Node.Services.OrganizationalHierarchy;

public sealed record FormationOptionDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int EmployeeCount { get; init; }
}

public sealed record DemographicBarItemDto
{
    public string Label { get; init; } = string.Empty;
    public int MaleCount { get; init; }
    public int FemaleCount { get; init; }
    public int TotalCount => MaleCount + FemaleCount;
    public double Percentage { get; init; }
}

public sealed record DistributionItemDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public double Percentage { get; init; }
    public string? ColorToken { get; init; }
}

public sealed record FormationVehicleStatDto
{
    public int TotalVehicles { get; init; }
    public int AuthorizedVehicles { get; init; }
    public int MainBuildingAccess { get; init; }
    public int JadriyaAccess { get; init; }
    public int ErbilAccess { get; init; }
    public int MosulAccess { get; init; }
    public int BasraAccess { get; init; }
}

public sealed record FormationStaffRosterItemDto
{
    public string EmployeeID { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? JobTitle { get; init; }
    public string? PositionTitle { get; init; }
    public string? FormationName { get; init; }
    public string? GradeName { get; init; }
    public string? StageName { get; init; }
    public string? EducationLevel { get; init; }
    public string? Gender { get; init; }
    public int Age { get; init; }
    public double ServiceYears { get; init; }
    public DateOnly? HireDate { get; init; }
    public DateOnly? LastJobTitleDate { get; init; }
    public DateOnly? LastAllowanceDate { get; init; }
    public int AppreciationCount { get; init; }
    public int PenaltyCount { get; init; }
    public int VehicleCount { get; init; }
    public bool HasAuthorizedVehicle { get; init; }
    public decimal? LeaveBalance { get; init; }
    public string? Status { get; init; }
    public bool ApproachingRetirement { get; init; }
}

public sealed record FormationKpiReportDto
{
    public int? FormationId { get; init; }
    public string FormationName { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    // Executive Metric Cards
    public int TotalStaff { get; init; }
    public int ActiveStaff { get; init; }
    public int MaleCount { get; init; }
    public int FemaleCount { get; init; }
    public double MalePercentage { get; init; }
    public double FemalePercentage { get; init; }
    public int NewHiresThisYear { get; init; }
    public int ApproachingRetirementCount { get; init; }
    public int EligibleForPromotionCount { get; init; }
    public int TotalAppreciationLetters { get; init; }
    public int TotalPenalties { get; init; }
    public double CleanDisciplinePercentage { get; init; }
    public decimal TotalLeaveBalanceDays { get; init; }
    public double AvgLeaveBalanceDays { get; init; }
    public double AvgServiceYears { get; init; }
    public double AvgAgeYears { get; init; }
    public int DigitalPhotoCount { get; init; }
    public double DigitalPhotoPercentage { get; init; }

    // Computed Executive Gauge Metrics
    public double ActiveStaffPercentage => TotalStaff > 0 ? Math.Round((double)ActiveStaff / TotalStaff * 100, 1) : 0;
    public double PromotionReadinessPercentage => TotalStaff > 0 ? Math.Round((double)EligibleForPromotionCount / TotalStaff * 100, 1) : 0;
    public int HigherEducationCount => EducationDistribution.Where(e => e.Label.Contains("دكتوراه") || e.Label.Contains("ماجستير") || e.Label.Contains("عالي")).Sum(e => e.Count);
    public double HigherEducationPercentage => TotalStaff > 0 ? Math.Round((double)HigherEducationCount / TotalStaff * 100, 1) : 0;
    public double VehicleCompliancePercentage => VehiclesStats.TotalVehicles > 0 ? Math.Round((double)VehiclesStats.AuthorizedVehicles / VehiclesStats.TotalVehicles * 100, 1) : 100;
    public int ContractStaffCount { get; init; }
    public double ContractStaffPercentage { get; init; }

    // Chart Series
    public List<DemographicBarItemDto> AgePyramid { get; init; } = new();
    public List<DistributionItemDto> EducationDistribution { get; init; } = new();
    public List<DistributionItemDto> GradeDistribution { get; init; } = new();
    public List<DistributionItemDto> ServiceTenureDistribution { get; init; } = new();
    public List<DistributionItemDto> LeaveTypesDistribution { get; init; } = new();
    public List<DistributionItemDto> TopJobTitles { get; init; } = new();
    public FormationVehicleStatDto VehiclesStats { get; init; } = new();

    // Detailed Roster
    public List<FormationStaffRosterItemDto> StaffRoster { get; init; } = new();
}

public interface IFormationKpiService
{
    /// <summary>
    /// Checks what formations the user has access to.
    /// Admins and HR Managers can access all formations.
    /// FormationManagers or users with formation permission can only access their assigned formation.
    /// </summary>
    Task<(bool IsAdminOrGlobal, int? LockedFormationId, List<FormationOptionDto> AvailableFormations)> GetUserFormationAccessAsync(ClaimsPrincipal user);

    /// <summary>
    /// Generates full executive KPI report for the given formation (or all if null and allowed).
    /// </summary>
    Task<FormationKpiReportDto> GetFormationKpiReportAsync(int? formationId);
}
