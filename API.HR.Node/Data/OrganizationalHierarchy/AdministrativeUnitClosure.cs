namespace API.HR.Node.Data.OrganizationalHierarchy;

/// <summary>
/// جدول مسارات التبعية الهرمية التراكمية (Transitive Closure Table)
/// يربط كل وحدة إدارية بكافة أسلافها صعوداً إلى رأس الهرم، وبكافة فروعها نزولاً.
/// </summary>
public class AdministrativeUnitClosure
{
    public int AncestorId { get; set; }
    public AdministrativeUnit? Ancestor { get; set; }

    public int DescendantId { get; set; }
    public AdministrativeUnit? Descendant { get; set; }

    /// <summary>
    /// عمق التبعية: 0 للوحدة مع نفسها، 1 للتبعية المباشرة، 2 وما فوق للتبعية غير المباشرة.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// مسار التبعية النصي الكامل من رأس الهرم مثل: /1/4/12/35/
    /// </summary>
    public string LineagePath { get; set; } = string.Empty;
}
