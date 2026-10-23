namespace API.HR.Node.Data.OrganizationalHierarchy;

/// <summary>
/// تصنيف ونوع الوحدة الإدارية في الهيكل التنظيمي (المحافظ، دائرة، مديرية، قسم، شعبة، وحدة، مكتب، فرع).
/// </summary>
public class AdministrativeUnitType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameKu { get; set; }
    public string Code { get; set; } = string.Empty;
    public int HierarchyLevel { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AdministrativeUnit> Units { get; set; } = new List<AdministrativeUnit>();
}
