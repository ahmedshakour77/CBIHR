namespace API.HR.Node.Security;

public static class AuthorizationPolicies
{
    public const string ApiBearer = "ApiBearer";
    public const string ProfilePhotoRead = "ProfilePhotoRead";
    public const string ProfilePhotoUpload = "ProfilePhotoUpload";
    public const string ProfilePhotoReplace = "ProfilePhotoReplace";
    public const string ProfilePhotoDelete = "ProfilePhotoDelete";

    public const string ManageUsers = "ManageUsers";
    public const string ManageDevices = "ManageDevices";
    public const string ViewAuditLogs = "ViewAuditLogs";
    public const string ManageWorkflows = "ManageWorkflows";
    public const string ImportEmployees = "ImportEmployees";
    public const string ImportAppreciationLetters = "ImportAppreciationLetters";
    public const string ManageFeedback = "ManageFeedback";
    public const string AdminOnly = "AdminOnly";
    public const string Staff = "Staff";
    public const string ManageCanva = "ManageCanva";
    public const string FormationDashboard = "FormationDashboard";
}
