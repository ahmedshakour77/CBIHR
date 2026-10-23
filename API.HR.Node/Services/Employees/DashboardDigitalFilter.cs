using API.HR.Node.Data;

namespace API.HR.Node.Services.Employees;

/// <summary>Union within a digital-status category, intersection between categories.</summary>
internal static class DashboardDigitalFilter
{
    internal const string WithPhoto = "صورة شخصية موثقة";
    internal const string WithoutPhoto = "بدون صورة شخصية";
    internal const string CompleteContacts = "بيانات اتصال مكتملة";
    internal const string MissingContacts = "نقص في بيانات الاتصال";
    internal const string LinkedAccount = "حساب مرتبط بالمنظومة";

    internal static IQueryable<Employee> Apply(
        IQueryable<Employee> employees,
        IReadOnlySet<string> selections,
        IQueryable<string> photoIds,
        IQueryable<string> phoneIds,
        IQueryable<string> emailIds,
        IQueryable<string> linkedIds)
    {
        var withPhoto = selections.Contains(WithPhoto);
        var withoutPhoto = selections.Contains(WithoutPhoto);
        if (withPhoto != withoutPhoto)
            employees = withPhoto
                ? employees.Where(e => photoIds.Contains(e.EmployeeID))
                : employees.Where(e => !photoIds.Contains(e.EmployeeID));

        var complete = selections.Contains(CompleteContacts);
        var missing = selections.Contains(MissingContacts);
        if (complete != missing)
            employees = complete
                ? employees.Where(e => phoneIds.Contains(e.EmployeeID) && emailIds.Contains(e.EmployeeID))
                : employees.Where(e => !phoneIds.Contains(e.EmployeeID) || !emailIds.Contains(e.EmployeeID));

        if (selections.Contains(LinkedAccount))
            employees = employees.Where(e => linkedIds.Contains(e.EmployeeID));

        return employees;
    }
}
