namespace API.HR.Node.Data.OrganizationalHierarchy;

/// <summary>
/// الوحدة الإدارية الفعلية في الهيكل التنظيمي وعلاقة التبعية المباشرة مع الوحدة الأب.
/// </summary>
public class AdministrativeUnit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameKu { get; set; }
    public string Code { get; set; } = string.Empty;

    public int UnitTypeId { get; set; }
    public AdministrativeUnitType? UnitType { get; set; }

    public int? ParentUnitId { get; set; }
    public AdministrativeUnit? ParentUnit { get; set; }
    public ICollection<AdministrativeUnit> SubUnits { get; set; } = new List<AdministrativeUnit>();

    public string? ManagerTitle { get; set; }
    public string? ManagerEmployeeId { get; set; }
    public int OrderIndex { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // موظفو الوحدة الإدارية
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();

    // مسارات التبعية الشجرية (Closure Table Relationships)
    public ICollection<AdministrativeUnitClosure> AncestorClosures { get; set; } = new List<AdministrativeUnitClosure>();
    public ICollection<AdministrativeUnitClosure> DescendantClosures { get; set; } = new List<AdministrativeUnitClosure>();
}
