using System.Security.Claims;

namespace API.HR.Node.Security;

public sealed record PermissionDefinition(
    string Code,
    string Title,
    string Description,
    bool IsEdit);

public sealed record FeatureDefinition(
    string FeatureKey,
    string FeatureName,
    string Icon,
    string GroupName,
    PermissionDefinition ViewPermission,
    PermissionDefinition EditPermission);

public sealed record MenuGroupDefinition(
    string GroupKey,
    string GroupTitle,
    string Icon,
    IReadOnlyList<FeatureDefinition> Features);

public static class FeaturePermissionCatalog
{
    public static readonly IReadOnlyList<MenuGroupDefinition> Groups =
    [
        new MenuGroupDefinition("insights", "المؤشرات والتقارير", "presentation-chart-bar",
        [
            new FeatureDefinition(
                "dashboard", "الرئيسية ولوحة المؤشرات", "home", "المؤشرات والتقارير",
                new PermissionDefinition(Permissions.DashboardView, "رؤية لوحة المؤشرات", "استعراض لوحة المؤشرات والإحصائيات والرسوم البيانية الرئيسية", false),
                new PermissionDefinition(Permissions.DashboardEdit, "تعديل لوحة المؤشرات", "تخصيص وترتيب البطاقات الإحصائية والودجات في لوحة المؤشرات", true)),

            new FeatureDefinition(
                "formation-dashboard", "لوحة مؤشرات التشكيل الإداري", "building-office-2", "المؤشرات والتقارير",
                new PermissionDefinition(Permissions.FormationDashboardView, "رؤية مؤشرات التشكيل", "استعراض لوحة المؤشرات والتقارير التنفيذية للتشكيلات الإدارية", false),
                new PermissionDefinition(Permissions.FormationDashboardEdit, "تعديل مؤشرات التشكيل", "إدارة وتخصيص مؤشرات وتقارير التشكيلات الإدارية", true)),

            new FeatureDefinition(
                "reports-slider", "التقارير الإحصائية", "presentation-chart-bar", "المؤشرات والتقارير",
                new PermissionDefinition(Permissions.ReportsView, "رؤية التقارير الإحصائية", "استعراض وتصدير شرائح وعروض التقارير الإحصائية", false),
                new PermissionDefinition(Permissions.ReportsEdit, "تعديل التقارير الإحصائية", "إنشاء وتعديل قوالب وتصنيفات التقارير الإحصائية", true)),

            new FeatureDefinition(
                "canva-reports", "تقارير Canva الاحترافية", "sparkles", "المؤشرات والتقارير",
                new PermissionDefinition(Permissions.CanvaView, "رؤية تقارير Canva", "استعراض القوالب والتصاميم المدمجة مع Canva", false),
                new PermissionDefinition(Permissions.CanvaEdit, "تعديل تقارير Canva", "ربط وتعديل وتصدير الإحصائيات في قوالب Canva", true))
        ]),

        new MenuGroupDefinition("records", "شؤون الموظفين", "users",
        [
            new FeatureDefinition(
                "employees", "سجلات الموظفين", "users", "شؤون الموظفين",
                new PermissionDefinition(Permissions.EmployeesView, "رؤية الموظفين", "استعراض والبحث في دليل وملفات وسجلات الموظفين", false),
                new PermissionDefinition(Permissions.EmployeesEdit, "تعديل الموظفين", "إضافة وتعديل وأرشفة بيانات ومستمسكات وسجلات الموظفين", true)),

            new FeatureDefinition(
                "leaves", "الإجازات والأرصدة", "calendar-days", "شؤون الموظفين",
                new PermissionDefinition(Permissions.LeavesView, "رؤية الإجازات", "استعراض سجلات وأرصدة وإحصائيات إجازات الموظفين", false),
                new PermissionDefinition(Permissions.LeavesEdit, "تعديل الإجازات", "تسجيل وتعديل وحذف واستيراد إجازات الموظفين", true)),

            new FeatureDefinition(
                "appreciation-letters", "كتب الشكر والتقدير", "document-text", "شؤون الموظفين",
                new PermissionDefinition(Permissions.AppreciationLettersView, "رؤية كتب الشكر", "استعراض سجل كتب الشكر والتقدير واحتساب القِدم", false),
                new PermissionDefinition(Permissions.AppreciationLettersEdit, "تعديل كتب الشكر", "إضافة وتعديل واستيراد وحذف كتب الشكر والتقدير", true)),

            new FeatureDefinition(
                "courses", "الدورات التدريبية", "academic-cap", "شؤون الموظفين",
                new PermissionDefinition(Permissions.CoursesView, "رؤية الدورات التدريبية", "استعراض سجل الدورات التدريبية ومشاركات الكوادر", false),
                new PermissionDefinition(Permissions.CoursesEdit, "تعديل الدورات التدريبية", "إضافة وتعديل وجرد الدورات التدريبية والمشاركين", true)),

            new FeatureDefinition(
                "employee-feedbacks", "مشاركات وشكاوى الموظفين", "inbox", "شؤون الموظفين",
                new PermissionDefinition(Permissions.FeedbackView, "رؤية مشاركات الموظفين", "استعراض مشاركات ومقترحات وشكاوى واستفسارات الموظفين", false),
                new PermissionDefinition(Permissions.FeedbackEdit, "تعديل ومعالجة المشاركات", "الرد على مقترحات وشكاوى الموظفين وتغيير حالاتها", true))
        ]),

        new MenuGroupDefinition("profiles", "بيانات مكمّلة", "user-group",
        [
            new FeatureDefinition(
                "employee-contacts", "وسائل التواصل والاتصال", "phone", "بيانات مكمّلة",
                new PermissionDefinition(Permissions.ContactsView, "رؤية وسائل التواصل", "استعراض أرقام الهواتف والبريد وحسابات التواصل للموظفين", false),
                new PermissionDefinition(Permissions.ContactsEdit, "تعديل وسائل التواصل", "تحديث وإضافة وسائل الاتصال وحسابات البريد للموظفين", true)),

            new FeatureDefinition(
                "gallery", "معرض الصور وتوثيق الكوادر", "camera", "بيانات مكمّلة",
                new PermissionDefinition(Permissions.GalleryView, "رؤية معرض الصور", "تصفح جدار الصور وتوثيق الموظفين", false),
                new PermissionDefinition(Permissions.GalleryEdit, "تعديل معرض الصور", "رفع وتحديث واستبدال وحذف الصور الشخصية للموظفين", true)),

            new FeatureDefinition(
                "vehicles", "مركبات ومتعلقات الموظفين", "truck", "بيانات مكمّلة",
                new PermissionDefinition(Permissions.VehiclesView, "رؤية المركبات والمتعلقات", "استعراض سجل مركبات الموظفين وتخويلات الدخول للمقرات", false),
                new PermissionDefinition(Permissions.VehiclesEdit, "تعديل المركبات والمتعلقات", "تسجيل وتعديل وتخويل مركبات الموظفين وتحديث بياناتها", true)),

            new FeatureDefinition(
                "residences-map", "خريطة السكن الجغرافية", "map-pin", "بيانات مكمّلة",
                new PermissionDefinition(Permissions.ResidencesView, "رؤية خريطة السكن", "استعراض مواقع ودور وشقق سكن الموظفين على الخريطة", false),
                new PermissionDefinition(Permissions.ResidencesEdit, "تعديل خريطة السكن", "تعديل وتحديث إحداثيات ومواقع سكن الموظفين جغرافياً", true)),

            new FeatureDefinition(
                "birthdays", "المناسبات وأعياد الميلاد", "cake", "بيانات مكمّلة",
                new PermissionDefinition(Permissions.BirthdaysView, "رؤية تقويم المناسبات", "استعراض تقويم أعياد ميلاد الكوادر والمناسبات العامة", false),
                new PermissionDefinition(Permissions.BirthdaysEdit, "تعديل تقويم المناسبات", "إدارة وتعديل تقويم المناسبات وتنبيهات أعياد الميلاد", true)),

            new FeatureDefinition(
                "national-cards", "البطاقات الوطنية الموحدة", "identification", "بيانات مكمّلة",
                new PermissionDefinition(Permissions.NationalCardsView, "رؤية البطاقات الوطنية", "استعراض والبحث في بيانات وصور البطاقات الوطنية الموحدة", false),
                new PermissionDefinition(Permissions.NationalCardsEdit, "تعديل البطاقات الوطنية", "إضافة وتعديل وقراءة بيانات وصور البطاقة الوطنية الموحدة", true)),

            new FeatureDefinition(
                "passports", "جوازات سفر الموظفين", "globe-alt", "بيانات مكمّلة",
                new PermissionDefinition(Permissions.PassportsView, "رؤية جوازات السفر", "استعراض والبحث في بيانات وصور وثائق جوازات السفر للموظفين", false),
                new PermissionDefinition(Permissions.PassportsEdit, "تعديل جوازات السفر", "إضافة وتحديث وتدقيق سجلات وصور جوازات السفر", true))
        ]),

        new MenuGroupDefinition("structure", "الهيكل والرواتب", "rectangle-stack",
        [
            new FeatureDefinition(
                "org-hierarchy", "الهيكلية الإدارية والتشكيلات", "rectangle-stack", "الهيكل والرواتب",
                new PermissionDefinition(Permissions.OrgHierarchyView, "رؤية الهيكلية الإدارية", "استعراض مخطط التشكيلات الإدارية والتبعية التنظيمية", false),
                new PermissionDefinition(Permissions.OrgHierarchyEdit, "تعديل الهيكلية الإدارية", "إضافة وتعديل ونقل التشكيلات الإدارية والأقسام والشعب", true)),

            new FeatureDefinition(
                "job-hierarchy", "الهيكل الوظيفي الشجري", "squares-2x2", "الهيكل والرواتب",
                new PermissionDefinition(Permissions.JobHierarchyView, "رؤية الهيكل الوظيفي", "استعراض مخطط شجرة الهيكل الوظيفي وسلسلة القيادة", false),
                new PermissionDefinition(Permissions.JobHierarchyEdit, "تعديل الهيكل الوظيفي", "تعديل وإعادة هيكلة المسارات الوظيفية الشجرية", true)),

            new FeatureDefinition(
                "job-classifications", "العناوين والعوائل الوظيفية", "briefcase", "الهيكل والرواتب",
                new PermissionDefinition(Permissions.JobTitlesView, "رؤية العناوين الوظيفية", "استعراض دليل وتصنيف العناوين والعوائل والدرجات الوظيفية", false),
                new PermissionDefinition(Permissions.JobTitlesEdit, "تعديل العناوين الوظيفية", "إضافة وتعديل واستيراد وتصنيف العناوين والدرجات الوظيفية", true)),

            new FeatureDefinition(
                "jobtitles", "استحقاق العناوين والترقيات", "arrow-trending-up", "الهيكل والرواتب",
                new PermissionDefinition(Permissions.PromotionsView, "رؤية استحقاق الترقيات", "استعراض وتوقع استحقاق الترفيع والعنوان بالسلم الوظيفي", false),
                new PermissionDefinition(Permissions.PromotionsEdit, "تعديل استحقاق الترقيات", "اعتماد وتحديث جداول وحركات الترفيع والاستحقاق", true)),

            new FeatureDefinition(
                "salary-scale", "السلم الوظيفي وجداول الرواتب", "chart-bar", "الهيكل والرواتب",
                new PermissionDefinition(Permissions.SalaryScaleView, "رؤية السلم الوظيفي", "استعراض جداول الدرجات والمراحل والرواتب الاسمية والعلاوات", false),
                new PermissionDefinition(Permissions.SalaryScaleEdit, "تعديل السلم الوظيفي", "تحديث قيم الدرجات والمراحل والرواتب الاسمية والعلاوات", true)),

            new FeatureDefinition(
                "salaries", "الرواتب الشهرية والتشكيلات", "banknotes", "الهيكل والرواتب",
                new PermissionDefinition(Permissions.SalariesView, "رؤية الرواتب الشهرية", "استعراض واحتساب الرواتب الشهرية ومخصصات التشكيلات", false),
                new PermissionDefinition(Permissions.SalariesEdit, "تعديل الرواتب الشهرية", "تعديل بنود واحتساب الرواتب والمخصصات واستيراد تواريخ العلاوات", true)),

            new FeatureDefinition(
                "workflows", "المسارات الإدارية والاعتمادات", "square-3-stack-3d", "الهيكل والرواتب",
                new PermissionDefinition(Permissions.WorkflowsView, "رؤية المسارات الإدارية", "متابعة واستعراض حركات ومسارات المعاملات والاعتمادات", false),
                new PermissionDefinition(Permissions.WorkflowsEdit, "تعديل المسارات الإدارية", "تصميم ونشر وإدارة المسارات الإدارية والبت في المعاملات", true))
        ]),

        new MenuGroupDefinition("reference", "البيانات المرجعية", "table-cells",
        [
            new FeatureDefinition(
                "data-import", "مركز استيراد البيانات (Excel)", "arrow-up-tray", "البيانات المرجعية",
                new PermissionDefinition(Permissions.DataImportView, "رؤية مركز الاستيراد", "استعراض وتنزيل قوالب ونماذج استيراد Excel المعتمدة", false),
                new PermissionDefinition(Permissions.DataImportEdit, "تنفيذ عمليات الاستيراد", "رفع واستيراد البيانات وتحديث السجلات بالجملة عبر Excel", true)),

            new FeatureDefinition(
                "admin-lookups", "إدارة القوائم المنسدلة والترميز", "table-cells", "البيانات المرجعية",
                new PermissionDefinition(Permissions.LookupsView, "رؤية القوائم المنسدلة", "استعراض كافة القوائم المنسدلة والترميز المرجعي للنظام", false),
                new PermissionDefinition(Permissions.LookupsEdit, "تعديل القوائم المنسدلة", "إضافة وتعديل وتفعيل وحذف عناصر القوائم المنسدلة والترميز", true))
        ]),

        new MenuGroupDefinition("governance", "الحسابات والأمان", "shield-check",
        [
            new FeatureDefinition(
                "users", "المستخدمون وإدارة الحسابات", "users", "الحسابات والأمان",
                new PermissionDefinition(Permissions.UsersView, "رؤية المستخدمين والأدوار", "استعراض حسابات المستخدمين، الأدوار الممنوحة، وربط الموظفين", false),
                new PermissionDefinition(Permissions.UsersEdit, "إدارة حسابات المستخدمين", "إنشاء الحسابات، إعادة ربط الموظفين، القفل، وإعادة تعيين كلمات المرور وتأمين الأجهزة", true)),

            new FeatureDefinition(
                "user-permissions", "صلاحيات وأدوار المستخدمين", "shield-check", "الحسابات والأمان",
                new PermissionDefinition(Permissions.UsersView, "رؤية الصلاحيات والأدوار", "استعراض الأدوار والصلاحيات الممنوحة للمستخدمين", false),
                new PermissionDefinition(Permissions.UsersPermissionsEdit, "تعديل الصلاحيات والأدوار", "إسناد وتعديل الأدوار والصلاحيات المباشرة وحزم الأذونات وتغيير النطاق الإداري", true)),

            new FeatureDefinition(
                "user-devices", "الأجهزة المقترنة بالموظفين", "device-phone-mobile", "الحسابات والأمان",
                new PermissionDefinition(Permissions.DevicesView, "رؤية الأجهزة المقترنة", "استعراض قائمة الهواتف والأجهزة المصرّح لها بدخول النظام", false),
                new PermissionDefinition(Permissions.DevicesEdit, "تعديل وتقييد الأجهزة", "الموافقة على الأجهزة، إلغاء الارتباط، وتقييد دخول الهواتف", true)),

            new FeatureDefinition(
                "sessions", "الجلسات السارية والنشطة", "signal", "الحسابات والأمان",
                new PermissionDefinition(Permissions.SessionsView, "رؤية الجلسات السارية", "استعراض الجلسات النشطة وعناوين IP ومواقيت الدخول", false),
                new PermissionDefinition(Permissions.SessionsEdit, "إنهاء الجلسات السارية", "إنهاء وإلغاء جلسات المستخدمين وإسقاط الاتصال عن بُعد", true)),

            new FeatureDefinition(
                "passkeys", "مفاتيح المرور البيومترية", "finger-print", "الحسابات والأمان",
                new PermissionDefinition(Permissions.PasskeysView, "رؤية مفاتيح المرور", "استعراض مفاتيح المرور البيومترية Passkeys المسجلة للحسابات", false),
                new PermissionDefinition(Permissions.PasskeysEdit, "إدارة مفاتيح المرور", "إلغاء وحذف مفاتيح المرور وإعادة ضبط المصادقة البيومترية", true)),

            new FeatureDefinition(
                "audit", "سجل التدقيق الأمني", "clipboard-document-list", "الحسابات والأمان",
                new PermissionDefinition(Permissions.AuditLogsView, "رؤية سجل التدقيق", "تصفح واستعراض أثر العمليات والتغييرات الأمنية بالنظام", false),
                new PermissionDefinition(Permissions.AuditLogsEdit, "إدارة سجل التدقيق", "تصدير وأرشفة سجلات التدقيق والبحث الأمني المتقدم", true)),

            new FeatureDefinition(
                "admin-features", "إدارة ميزات ووحدات النظام", "cog-6-tooth", "الحسابات والأمان",
                new PermissionDefinition(Permissions.FeaturesView, "رؤية ميزات النظام", "استعراض حالة تفعيل وتعطيل وحدات وميزات المنظومة", false),
                new PermissionDefinition(Permissions.FeaturesEdit, "تعديل ميزات النظام", "التحكم في تشغيل وإيقاف ميزات ووظائف النظام العامة", true)),

            new FeatureDefinition(
                "design-system", "نظام التصميم والمكونات", "square-2-stack", "الحسابات والأمان",
                new PermissionDefinition(Permissions.DesignSystemView, "رؤية نظام التصميم", "معاينة مكتبة ومختبر المكونات ونظام التصميم", false),
                new PermissionDefinition(Permissions.DesignSystemEdit, "تعديل نظام التصميم", "تخصيص ثيمات ورموز التصميم والمختبر التفاعلي", true))
        ]),

        new MenuGroupDefinition("personal", "الملف الشخصي والخدمة الذاتية", "user-circle",
        [
            new FeatureDefinition(
                "portal", "ملفي الوظيفي الإلكتروني", "user-circle", "الملف الشخصي",
                new PermissionDefinition(Permissions.DossierView, "رؤية ملفي الوظيفي", "عرض وتصفح بيانات الملف الوظيفي والخدمة الذاتية", false),
                new PermissionDefinition(Permissions.DossierEdit, "تعديل ملفي الوظيفي", "تحديث البيانات المسموح بها ذاتياً في الملف الوظيفي", true)),

            new FeatureDefinition(
                "my-feedback", "مشاركاتي ومقترحاتي", "chat-bubble-left-right", "الملف الشخصي",
                new PermissionDefinition(Permissions.MyFeedbackView, "رؤية مشاركاتي", "استعراض مقترحاتي واستفساراتي وشكاوى الحساب الذاتي", false),
                new PermissionDefinition(Permissions.MyFeedbackSubmit, "إرسال مشاركات جديدة", "تقديم مشاركات ومقترحات واستفسارات جديدة للإدارة", true))
        ])
    ];

    public static readonly IReadOnlyList<FeatureDefinition> AllFeatures =
        Groups.SelectMany(g => g.Features).ToList();

    public static readonly IReadOnlyList<string> AllPermissionCodes =
        Groups.SelectMany(g => g.Features)
              .SelectMany(f => new[] { f.ViewPermission.Code, f.EditPermission.Code })
              .Distinct()
              .ToList();

    public static readonly Dictionary<string, string> AvailablePermissionsMap =
        Groups.SelectMany(g => g.Features)
              .SelectMany(f => new[]
              {
                  new KeyValuePair<string, string>(f.ViewPermission.Code, $"{f.FeatureName} - {f.ViewPermission.Title}"),
                  new KeyValuePair<string, string>(f.EditPermission.Code, $"{f.FeatureName} - {f.EditPermission.Title}")
              })
              .DistinctBy(kv => kv.Key)
              .ToDictionary(kv => kv.Key, kv => kv.Value);

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RolePermissionPackages =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = AllPermissionCodes,

            ["HRManager"] = AllPermissionCodes
                .Where(p => p is not Permissions.UsersEdit
                            and not Permissions.UsersPermissionsEdit
                            and not Permissions.AuditLogsView
                            and not Permissions.AuditLogsEdit
                            and not Permissions.FeaturesView
                            and not Permissions.FeaturesEdit
                            and not Permissions.DesignSystemView
                            and not Permissions.DesignSystemEdit
                            and not Permissions.PasskeysEdit
                            and not Permissions.DevicesEdit)
                .ToList(),

            ["Supervisor"] =
            [
                Permissions.DashboardView,
                Permissions.EmployeesView,
                Permissions.EmployeesEdit,
                Permissions.LeavesView,
                Permissions.LeavesEdit,
                Permissions.SalaryScaleView,
                Permissions.SalariesView,
                Permissions.PassportsView,
                Permissions.NationalCardsView,
                Permissions.AppreciationLettersView,
                Permissions.AppreciationLettersEdit,
                Permissions.WorkflowsView,
                Permissions.WorkflowsEdit,
                Permissions.CoursesView,
                Permissions.CoursesEdit,
                Permissions.ContactsView,
                Permissions.ReportsView,
                Permissions.DossierView,
                Permissions.MyFeedbackView,
                Permissions.MyFeedbackSubmit
            ],

            ["FormationManager"] =
            [
                Permissions.DashboardView,
                Permissions.FormationDashboardView,
                Permissions.FormationDashboardEdit,
                Permissions.EmployeesView,
                Permissions.LeavesView,
                Permissions.WorkflowsView,
                Permissions.WorkflowsEdit,
                Permissions.AppreciationLettersView,
                Permissions.ReportsView,
                Permissions.DossierView,
                Permissions.MyFeedbackView,
                Permissions.MyFeedbackSubmit
            ],

            ["Employee"] =
            [
                Permissions.DossierView,
                Permissions.MyFeedbackView,
                Permissions.MyFeedbackSubmit
            ]
        };

    /// <summary>
    /// Represents the resolved status of a single permission across role claims and direct user claims.
    /// </summary>
    public sealed record EffectivePermissionItem(
        string PermissionCode,
        string FeatureName,
        string PermissionTitle,
        bool IsEdit,
        bool GrantedByRole,
        IReadOnlyList<string> GrantedByRolesList,
        bool GrantedDirectly,
        bool IsEffective,
        string SourceReason);

    public static IReadOnlyList<EffectivePermissionItem> CalculateEffectivePermissions(
        IEnumerable<string> assignedRoles,
        IEnumerable<string> directPermissionClaims)
    {
        var roleList = assignedRoles.ToList();
        var directClaimsSet = new HashSet<string>(directPermissionClaims, StringComparer.OrdinalIgnoreCase);
        var isAdmin = roleList.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase));

        var rolePermissions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roleList)
        {
            if (RolePermissionPackages.TryGetValue(role, out var perms))
            {
                foreach (var p in perms)
                {
                    if (!rolePermissions.TryGetValue(p, out var rolesGranting))
                    {
                        rolesGranting = [];
                        rolePermissions[p] = rolesGranting;
                    }
                    rolesGranting.Add(role);
                }
            }
        }

        var results = new List<EffectivePermissionItem>();

        foreach (var feature in AllFeatures)
        {
            foreach (var permDef in new[] { feature.ViewPermission, feature.EditPermission })
            {
                var code = permDef.Code;
                var hasRoleGrant = rolePermissions.TryGetValue(code, out var grantingRolesList);
                var grantingRoles = hasRoleGrant ? grantingRolesList! : [];

                var hasDirectGrant = directClaimsSet.Contains(code);

                // Edit permission implies View permission
                var hasImpliedRoleGrant = false;
                var hasImpliedDirectGrant = false;
                if (permDef == feature.ViewPermission)
                {
                    if (rolePermissions.TryGetValue(feature.EditPermission.Code, out var editRoles))
                    {
                        hasImpliedRoleGrant = true;
                        grantingRoles = grantingRoles.Concat(editRoles).Distinct().ToList();
                    }
                    if (directClaimsSet.Contains(feature.EditPermission.Code))
                    {
                        hasImpliedDirectGrant = true;
                    }
                }

                var isEffective = isAdmin || hasRoleGrant || hasDirectGrant || hasImpliedRoleGrant || hasImpliedDirectGrant;

                string reason;
                if (isAdmin)
                {
                    reason = "صلاحية كاملة (مدير النظام Admin)";
                }
                else if (hasDirectGrant && (hasRoleGrant || hasImpliedRoleGrant))
                {
                    reason = $"ممنوحة مباشرة وعبر الدور ({string.Join(", ", grantingRoles)})";
                }
                else if (hasRoleGrant || hasImpliedRoleGrant)
                {
                    reason = $"ممنوحة من دور: {string.Join(", ", grantingRoles)}";
                }
                else if (hasDirectGrant || hasImpliedDirectGrant)
                {
                    reason = "ممنوحة للمستخدم مباشرة";
                }
                else
                {
                    reason = "غير ممنوحة (معطلة)";
                }

                results.Add(new EffectivePermissionItem(
                    PermissionCode: code,
                    FeatureName: feature.FeatureName,
                    PermissionTitle: permDef.Title,
                    IsEdit: permDef.IsEdit,
                    GrantedByRole: hasRoleGrant || hasImpliedRoleGrant,
                    GrantedByRolesList: grantingRoles,
                    GrantedDirectly: hasDirectGrant || hasImpliedDirectGrant,
                    IsEffective: isEffective,
                    SourceReason: reason));
            }
        }

        return results
            .GroupBy(p => p.PermissionCode, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                var anyEffective = g.Any(x => x.IsEffective);
                var anyRoleGrant = g.Any(x => x.GrantedByRole);
                var anyDirectGrant = g.Any(x => x.GrantedDirectly);
                var allRoles = g.SelectMany(x => x.GrantedByRolesList).Distinct().ToList();
                var bestReason = g.FirstOrDefault(x => x.IsEffective)?.SourceReason ?? first.SourceReason;

                return new EffectivePermissionItem(
                    PermissionCode: g.Key,
                    FeatureName: first.FeatureName,
                    PermissionTitle: first.PermissionTitle,
                    IsEdit: first.IsEdit,
                    GrantedByRole: anyRoleGrant,
                    GrantedByRolesList: allRoles,
                    GrantedDirectly: anyDirectGrant,
                    IsEffective: anyEffective,
                    SourceReason: bestReason);
            })
            .ToList();
    }

    /// <summary>
    /// فحص ما إذا كان المستخدم يملك إذن رؤية أو تعديل لخاصية معينة
    /// </summary>
    public static bool HasPermission(this ClaimsPrincipal? user, string permissionCode)
    {
        if (user == null || user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole("Admin") || user.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // مطابقة صريحة لادعاء الصلاحية
        if (user.HasClaim(c => (c.Type == "permission" || c.Type == "Permission") &&
                               c.Value.Equals(permissionCode, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // صلاحية التعديل (.Edit) تتضمن تلقائياً صلاحية الرؤية (.View)
        if (permissionCode.EndsWith(".View", StringComparison.OrdinalIgnoreCase))
        {
            var editEquivalent = permissionCode[..^5] + ".Edit";
            if (user.HasClaim(c => (c.Type == "permission" || c.Type == "Permission") &&
                                   c.Value.Equals(editEquivalent, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // مطابقة الصلاحيات القديمة للتوافق
        if (user.Claims.Any(c => (c.Type == "permission" || c.Type == "Permission") &&
                                 PermissionAuthorizationHandler.IsLegacyPermissionMatch(c.Value, permissionCode)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// فحص صلاحية الرؤية لخاصية معينة مع خيار تمرير أدوار بديلة
    /// </summary>
    public static bool CanView(this ClaimsPrincipal? user, string viewPermissionCode, params string[] fallbackRoles)
    {
        if (user.HasPermission(viewPermissionCode))
        {
            return true;
        }

        if (fallbackRoles != null && fallbackRoles.Length > 0 && user != null)
        {
            foreach (var role in fallbackRoles)
            {
                if (user.IsInRole(role)) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// فحص صلاحية التعديل لخاصية معينة مع خيار تمرير أدوار بديلة
    /// </summary>
    public static bool CanEdit(this ClaimsPrincipal? user, string editPermissionCode, params string[] fallbackRoles)
    {
        if (user.HasPermission(editPermissionCode))
        {
            return true;
        }

        if (fallbackRoles != null && fallbackRoles.Length > 0 && user != null)
        {
            foreach (var role in fallbackRoles)
            {
                if (user.IsInRole(role)) return true;
            }
        }

        return false;
    }
}
