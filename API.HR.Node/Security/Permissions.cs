namespace API.HR.Node.Security;

public static class Permissions
{
    // ==========================================
    // 1. المؤشرات والتقارير (Insights)
    // ==========================================
    public const string DashboardView = "Dashboard.View";
    public const string DashboardEdit = "Dashboard.Edit";

    public const string FormationDashboardView = "FormationDashboard.View";
    public const string FormationDashboardEdit = "FormationDashboard.Edit";

    public const string ReportsView = "Reports.View";
    public const string ReportsEdit = "Reports.Edit";

    public const string CanvaView = "Canva.View";
    public const string CanvaEdit = "Canva.Edit";

    // ==========================================
    // 2. شؤون الموظفين (Records)
    // ==========================================
    public const string EmployeesView = "Employees.View";
    public const string EmployeesEdit = "Employees.Edit";

    public const string LeavesView = "Leaves.View";
    public const string LeavesEdit = "Leaves.Edit";

    public const string AppreciationLettersView = "AppreciationLetters.View";
    public const string AppreciationLettersEdit = "AppreciationLetters.Edit";

    public const string CoursesView = "Courses.View";
    public const string CoursesEdit = "Courses.Edit";

    public const string FeedbackView = "Feedback.View";
    public const string FeedbackEdit = "Feedback.Edit";

    // ==========================================
    // 3. بيانات مكمّلة (Profiles)
    // ==========================================
    public const string ContactsView = "Contacts.View";
    public const string ContactsEdit = "Contacts.Edit";

    public const string GalleryView = "Gallery.View";
    public const string GalleryEdit = "Gallery.Edit";

    public const string VehiclesView = "Vehicles.View";
    public const string VehiclesEdit = "Vehicles.Edit";

    public const string ResidencesView = "Residences.View";
    public const string ResidencesEdit = "Residences.Edit";

    public const string BirthdaysView = "Birthdays.View";
    public const string BirthdaysEdit = "Birthdays.Edit";

    public const string NationalCardsView = "NationalCards.View";
    public const string NationalCardsEdit = "NationalCards.Edit";

    public const string PassportsView = "Passports.View";
    public const string PassportsEdit = "Passports.Edit";

    // ==========================================
    // 4. الهيكل والرواتب (Structure)
    // ==========================================
    public const string OrgHierarchyView = "OrgHierarchy.View";
    public const string OrgHierarchyEdit = "OrgHierarchy.Edit";

    public const string JobHierarchyView = "JobHierarchy.View";
    public const string JobHierarchyEdit = "JobHierarchy.Edit";

    public const string JobTitlesView = "JobTitles.View";
    public const string JobTitlesEdit = "JobTitles.Edit";

    public const string PromotionsView = "Promotions.View";
    public const string PromotionsEdit = "Promotions.Edit";

    public const string SalaryScaleView = "SalaryScale.View";
    public const string SalaryScaleEdit = "SalaryScale.Edit";

    public const string SalariesView = "Salaries.View";
    public const string SalariesEdit = "Salaries.Edit";

    public const string WorkflowsView = "Workflows.View";
    public const string WorkflowsEdit = "Workflows.Edit";

    // ==========================================
    // 5. البيانات المرجعية (Reference)
    // ==========================================
    public const string DataImportView = "DataImport.View";
    public const string DataImportEdit = "DataImport.Edit";

    public const string LookupsView = "Lookups.View";
    public const string LookupsEdit = "Lookups.Edit";

    // ==========================================
    // 6. الحسابات والأمان (Governance)
    // ==========================================
    public const string UsersView = "Users.View";
    public const string UsersEdit = "Users.Edit";
    public const string UsersPermissionsEdit = "Users.PermissionsEdit";

    public const string DevicesView = "Devices.View";
    public const string DevicesEdit = "Devices.Edit";

    public const string SessionsView = "Sessions.View";
    public const string SessionsEdit = "Sessions.Edit";

    public const string PasskeysView = "Passkeys.View";
    public const string PasskeysEdit = "Passkeys.Edit";

    public const string AuditLogsView = "AuditLogs.View";
    public const string AuditLogsEdit = "AuditLogs.Edit";

    public const string FeaturesView = "Features.View";
    public const string FeaturesEdit = "Features.Edit";

    public const string DesignSystemView = "DesignSystem.View";
    public const string DesignSystemEdit = "DesignSystem.Edit";

    // ==========================================
    // 7. الملف الشخصي والخدمة الذاتية (Personal)
    // ==========================================
    public const string DossierView = "Dossier.View";
    public const string DossierEdit = "Dossier.Edit";

    public const string MyFeedbackView = "MyFeedback.View";
    public const string MyFeedbackSubmit = "MyFeedback.Submit";

    // ==========================================
    // التوافق مع الشيفرات السابقة (Backwards Compatibility)
    // ==========================================
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
