using API.HR.Node.Data;
using API.HR.Node.Data.OrganizationalHierarchy;

namespace API.HR.Node.Services.Pdf;

public sealed class EmployeeDossierPdfModel
{
    public required Employee Employee { get; set; }
    public AdministrativeUnit? EffectiveUnit { get; set; }
    public List<AdministrativeUnit> ManagedUnits { get; set; } = [];
    public List<EmployeePhone> Phones { get; set; } = [];
    public List<EmployeeEmail> Emails { get; set; } = [];
    public List<AppreciationLetter> AppreciationLetters { get; set; } = [];
    public List<EmployeeLeave> Leaves { get; set; } = [];
    public List<EmployeePenalty> Penalties { get; set; } = [];
    public List<EmployeeMartyr> Martyrs { get; set; } = [];
    public List<EmployeeResidence> Residences { get; set; } = [];
    public List<EmployeeCareerMovement> CareerMovements { get; set; } = [];
    public List<EmployeeCourse> Courses { get; set; } = [];
    public int ServiceYears { get; set; }
    public int ServiceMonths { get; set; }
    public int ServiceDays { get; set; }
    public int AgeYears { get; set; }
    public int AgeMonths { get; set; }
    public byte[]? ProfilePhotoBytes { get; set; }
    public byte[]? QrCodeBytes { get; set; }
    public string? PortalVerificationUrl { get; set; }
    public string? VerificationToken { get; set; }
    public string? SnapshotDataDigest { get; set; }
    public byte[]? LogoBytes { get; set; }
    public bool IsUnitManager { get; set; }
    public string? UnitManagerTitle { get; set; }
    public string? AdministrativeParentUnitName { get; set; }
    public List<string> AdministrativeLineage { get; set; } = [];
    public API.HR.Node.Data.Allowances.EmployeeAllowancePeriod? ActiveAllowancePeriod { get; set; }
}
