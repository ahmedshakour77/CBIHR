namespace API.HR.Node.Security;

public static class Permissions
{
    public const string ProfilePhotoRead = "Employee.ProfilePhoto.Read";
    public const string ProfilePhotoUpload = "Employee.ProfilePhoto.Upload";
    public const string ProfilePhotoReplace = "Employee.ProfilePhoto.Replace";
    public const string ProfilePhotoDelete = "Employee.ProfilePhoto.Delete";

    public const string ManageUsers = "Users.Manage";
    public const string ManageDevices = "Devices.Manage";
    public const string ViewAuditLogs = "AuditLogs.View";
    public const string ManageWorkflows = "Workflows.Manage";
    public const string ImportEmployees = "Employees.Import";
    public const string ImportAppreciationLetters = "AppreciationLetters.Import";
    public const string ImportJobTitles = "JobTitles.Import";
    public const string ImportLastAllowanceDate = "LastAllowanceDate.Import";
    public const string ManageFeedback = "EmployeeFeedback.Manage";
    public const string ViewFormationDashboard = "Formation.Dashboard.View";
}
