using System.Security.Claims;
using API.HR.Node.Security;

namespace API.HR.Node.Components.Layout;

/// <summary>
/// One destination in the system's navigation with multilingual support, fine-grained permissions, and role-based fallback.
/// </summary>
public sealed record NavEntry(
    string Href,
    string Label,
    string Icon,
    string Hint,
    bool Exact = false,
    string? LabelKu = null,
    string? HintKu = null,
    string[]? Roles = null,
    string? ViewPermission = null,
    string? EditPermission = null)
{
    public string GetLabel(bool isKurdish) =>
        isKurdish && !string.IsNullOrWhiteSpace(LabelKu) ? LabelKu : Label;

    public string GetHint(bool isKurdish) =>
        isKurdish && !string.IsNullOrWhiteSpace(HintKu) ? HintKu : Hint;

    public bool IsAllowedFor(ClaimsPrincipal? user)
    {
        if (user == null || user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // المسؤول (Admin) يملك صلاحية رؤية كافة التبويبات تلقائياً
        if (user.IsInRole("Admin"))
        {
            return true;
        }

        // التبويبات العامة لجميع الموظفين المسجلين (مثل الملف الشخصي ومشاركاتي)
        if (Href is "portal" or "my-feedback")
        {
            return true;
        }

        // 1. إذا كان العنصر محدداً بصلاحية دقيقة (ViewPermission أو EditPermission)، يتم الاعتماد حصراً على الصلاحيات
        var hasSpecificPermissionDefined = !string.IsNullOrWhiteSpace(ViewPermission) || !string.IsNullOrWhiteSpace(EditPermission);
        if (hasSpecificPermissionDefined)
        {
            if (!string.IsNullOrWhiteSpace(ViewPermission) && user.HasPermission(ViewPermission))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(EditPermission) && user.HasPermission(EditPermission))
            {
                return true;
            }

            // فحص التوافق مع الصلاحيات القديمة
            if (CheckLegacyPermissions(user))
            {
                return true;
            }

            // إذا كان لدى المستخدم أي أذونات مخصصة (Claims صريحة)، نعتمد حصراً على أذوناته ميزة بميزة
            var hasAnyExplicitPermissions = user.Claims.Any(c => (c.Type == "permission" || c.Type == "Permission") && !string.IsNullOrWhiteSpace(c.Value));
            if (hasAnyExplicitPermissions)
            {
                return false;
            }

            // للمستخدمين الذين يملكون أدواراً كلاسيكية فقط ولم تُخصص لهم أذونات بعد، نتحقق من الدور
            if (Roles != null && Roles.Length > 0)
            {
                foreach (var role in Roles)
                {
                    if (user.IsInRole(role))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // 2. التحقق من الأدوار الكلاسيكية فقط للعناصر القديمة التي لا تملك صلاحية مخصصة
        if (Roles != null && Roles.Length > 0)
        {
            foreach (var role in Roles)
            {
                if (user.IsInRole(role))
                {
                    return true;
                }
            }
            return false;
        }

        // التبويبات العامة لجميع الموظفين المسجلين (مثل الملف الشخصي ومشاركاتي)
        return true;
    }

    private bool CheckLegacyPermissions(ClaimsPrincipal user)
    {
        if (ViewPermission == Permissions.UsersView && user.HasClaim("permission", Permissions.ManageUsers)) return true;
        if (ViewPermission == Permissions.DevicesView && user.HasClaim("permission", Permissions.ManageDevices)) return true;
        if (ViewPermission == Permissions.AuditLogsView && user.HasClaim("permission", Permissions.ViewAuditLogs)) return true;
        if (ViewPermission == Permissions.WorkflowsView && user.HasClaim("permission", Permissions.ManageWorkflows)) return true;
        if (ViewPermission == Permissions.DataImportView && (user.HasClaim("permission", Permissions.ImportEmployees) || user.HasClaim("permission", Permissions.ImportAppreciationLetters))) return true;
        if (ViewPermission == Permissions.FeedbackView && user.HasClaim("permission", Permissions.ManageFeedback)) return true;
        if (ViewPermission == Permissions.FormationDashboardView && user.HasClaim("permission", Permissions.ViewFormationDashboard)) return true;
        return false;
    }
}

public sealed record NavGroup(
    string Title,
    IReadOnlyList<NavEntry> Items,
    string? TitleKu = null,
    string[]? Roles = null)
{
    public string GetTitle(bool isKurdish) =>
        isKurdish && !string.IsNullOrWhiteSpace(TitleKu) ? TitleKu : Title;

    public bool IsAllowedFor(ClaimsPrincipal? user)
    {
        if (user == null || user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole("Admin"))
        {
            return true;
        }

        // الملف الشخصي مسموح دائماً لجميع الموظفين المسجلين
        if (Title == "الملف الشخصي" || TitleKu == "پڕۆفایلی کەسی")
        {
            return true;
        }

        // تظهر المجموعة إذا كان لدى المستخدم دور صريح فيها أو كان لديه صلاحية الوصول لعنصر واحد على الأقل داخلها
        return Items.Any(item => item.IsAllowedFor(user));
    }
}

/// <summary>
/// The navigation is declared once, here, and read by the rail, the mobile
/// drawer and the command palette alike.
/// </summary>
public static class NavCatalog
{
    public static readonly NavGroup Insights = new("المؤشرات والتقارير", new[]
    {
        new NavEntry("", "الرئيسية", "home", "لوحة المؤشرات والمخططات الإحصائية", Exact: true,
            LabelKu: "سەرەکی", HintKu: "تەختەی ئامار و هێڵکارییەکان",
            Roles: new[] { "Admin", "HRManager", "FormationManager" },
            ViewPermission: Permissions.DashboardView,
            EditPermission: Permissions.DashboardEdit),

        new NavEntry("formation-dashboard", "لوحة مؤشرات التشكيل", "building-office-2", "لوحة المؤشرات والتقارير التنفيذية الشاملة للتشكيل الإداري",
            LabelKu: "تەختەی نیشاندەرەکانی پێکهاتە", HintKu: "تەختەی نیشاندەرە جێبەجێکارییەکان بۆ پێکهاتەی کارگێڕی",
            Roles: new[] { "Admin", "HRManager", "FormationManager" },
            ViewPermission: Permissions.FormationDashboardView,
            EditPermission: Permissions.FormationDashboardEdit),

        new NavEntry("reports-slider", "التقارير", "presentation-chart-bar", "عرض تقديمي للإحصائيات",
            LabelKu: "ڕاپۆرتەکان", HintKu: "پێشکەشکردنی ئامارەکان",
            Roles: new[] { "Admin", "HRManager" },
            ViewPermission: Permissions.ReportsView,
            EditPermission: Permissions.ReportsEdit),

        new NavEntry("canva-reports", "تقارير Canva", "sparkles", "تصدير ودمج الإحصائيات في قوالب Canva الاحترافية",
            LabelKu: "ڕاپۆرتەکانی Canva", HintKu: "تێکەڵکردنی ئامارەکان لە قاڵبەکانی Canva",
            Roles: new[] { "Admin", "HRManager" },
            ViewPermission: Permissions.CanvaView,
            EditPermission: Permissions.CanvaEdit)
    }, TitleKu: "نیشاندەر و ڕاپۆرتەکان");

    public static readonly NavGroup Records = new("شؤون الموظفين", new[]
    {
        new NavEntry("employees", "الموظفون", "users", "سجلات الموظفين، البحث والفرز المتقدم، وتوثيق الصور",
            LabelKu: "کارمەندان", HintKu: "تۆماری کارمەندان، گەڕان و ڕیزکردن و وێنەکان",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.EmployeesView,
            EditPermission: Permissions.EmployeesEdit),

        new NavEntry("leaves", "الإجازات", "calendar-days", "سجل إجازات الموظفين، الأرصدة، والإحصائيات",
            LabelKu: "مۆڵەتەکان", HintKu: "تۆماری مۆڵەتی کارمەندان، باڵانس و ئامارەکان",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.LeavesView,
            EditPermission: Permissions.LeavesEdit),

        new NavEntry("appreciation-letters", "كتب الشكر", "document-text", "سجل كتب الشكر والتقدير، التكريم، واحتساب القِدم",
            LabelKu: "سوپاسنامەکان", HintKu: "تۆماری سوپاسنامە و پێزانین و پێشخستن",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.AppreciationLettersView,
            EditPermission: Permissions.AppreciationLettersEdit),

        new NavEntry("courses", "الدورات التدريبية", "academic-cap", "سجل ومتابعة الدورات التدريبية للموظفين والمشاركات",
            LabelKu: "خولەکانی ڕاهێنان", HintKu: "تۆمار و بەدواداچوونی خولەکانی ڕاهێنانی کارمەندان",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.CoursesView,
            EditPermission: Permissions.CoursesEdit),

        new NavEntry("employee-feedbacks", "مشاركات الموظفين", "inbox", "متابعة واستعراض مشاركات، مقترحات، وشكاوى الموظفين",
            LabelKu: "بەشداریی کارمەندان", HintKu: "بەدواداچوونی پێشنیاز، سکاڵا و سەرنجی کارمەندان",
            Roles: new[] { "Admin", "HRManager" },
            ViewPermission: Permissions.FeedbackView,
            EditPermission: Permissions.FeedbackEdit)
    }, TitleKu: "کاروباری کارمەندان");

    public static readonly NavGroup Profiles = new("بيانات مكمّلة", new[]
    {
        new NavEntry("employee-contacts", "وسائل التواصل", "phone", "الهواتف والبريد وربط الحسابات",
            LabelKu: "هۆکارەکانی پەیوەندی", HintKu: "تەلەفۆن و ئیمەیڵ و بەستنەوەی هەژمارەکان",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.ContactsView,
            EditPermission: Permissions.ContactsEdit),

        new NavEntry("gallery", "معرض الصور", "camera", "جدار الصور ومتابعة التوثيق",
            LabelKu: "پێشانگای وێنەکان", HintKu: "دیواری وێنەکان و بەدواداچوونی بەڵگەنامەکردن",
            Roles: new[] { "Admin", "HRManager" },
            ViewPermission: Permissions.GalleryView,
            EditPermission: Permissions.GalleryEdit),

        new NavEntry("vehicles", "مركبات ومتعلقات الموظفين", "truck", "سجل سيارات الموظفين، أرقام اللوحات، وتخويلات الدخول للمقرات والمباني",
            LabelKu: "ئۆتۆمبێلی کارمەندان", HintKu: "تۆماری ئۆتۆمبێلی کارمەندان، ژمارەی تابلۆ و ڕێپێدانی چوونەژوورەوە",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.VehiclesView,
            EditPermission: Permissions.VehiclesEdit),

        new NavEntry("residences-map", "خريطة السكن", "map-pin", "استعراض مواقع ودور وشقق سكن الموظفين على الخريطة الجغرافية الموحدة",
            LabelKu: "نەخشەی نیشتەجێبوون", HintKu: "پیشاندانی شوێنی نیشتەجێبوونی کارمەندان لەسەر نەخشە",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.ResidencesView,
            EditPermission: Permissions.ResidencesEdit),

        new NavEntry("admin/birthdays", "أعياد الميلاد والمناسبات", "cake", "تقويم أعياد ميلاد الكوادر، الأبراج الفلكية، وتحليل الأجيال وتوافق التواريخ",
            LabelKu: "ڕۆژی لەدایکبوون و بورجەکان", HintKu: "ساڵنامەی لەدایکبوونی کارمەندان، بورجەکان و نەوەکان",
            Roles: new[] { "Admin" },
            ViewPermission: Permissions.BirthdaysView,
            EditPermission: Permissions.BirthdaysEdit)
    }, TitleKu: "زانیاریی تەواوکەر");

    public static readonly NavGroup Structure = new("الهيكل والرواتب", new[]
    {
        new NavEntry("org-hierarchy", "الهيكلية الإدارية", "rectangle-stack", "مخطط التشكيلات ومسارات التبعية لرأس الهرم",
            LabelKu: "پێکهاتەی کارگێڕی", HintKu: "هێڵکاریی پێکهاتە و بەستراوەیی تا لووتکە",
            Roles: new[] { "Admin", "HRManager", "FormationManager" },
            ViewPermission: Permissions.OrgHierarchyView,
            EditPermission: Permissions.OrgHierarchyEdit),

        new NavEntry("job-hierarchy", "الهيكل الوظيفي الشجري", "squares-2x2", "مخطط شجرة الهيكل الوظيفي والتنظيمي وسلسلة القيادة",
            LabelKu: "پێکهاتەی وەزیفی دارێژەیی", HintKu: "هێڵکاریی دارێژەیی پێکهاتەی وەزیفی و سەرکردایەتی",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.JobHierarchyView,
            EditPermission: Permissions.JobHierarchyEdit),

        new NavEntry("admin/job-classifications", "العناوين والعوائل الوظيفية", "briefcase", "دليل وتصنيف العناوين الوظيفية والعوائل والدرجات الوظيفية المعتمدة",
            LabelKu: "ناونیشان و خێزانە وەزیفییەکان", HintKu: "ڕێبەری ناونیشانە وەزیفییەکان و خێزانەکان و پلەکان",
            Roles: new[] { "Admin" },
            ViewPermission: Permissions.JobTitlesView,
            EditPermission: Permissions.JobTitlesEdit),

        new NavEntry("jobtitles", "استحقاق العناوين والترقيات", "arrow-trending-up", "تنبؤ واحتساب استحقاق الترقية والعنوان بالسلم وكتب الشكر والعقوبات",
            LabelKu: "شایستەیی ناونیشان و پلەبەرزکردنەوە", HintKu: "پێشبینیکردنی شایستەیی پلەبەرزکردنەوە بەپێی پەیژە و سوپاسنامە و سزاکان",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.PromotionsView,
            EditPermission: Permissions.PromotionsEdit),

        new NavEntry("salary-scale", "السلم الوظيفي", "chart-bar", "جدول الدرجات والمراحل والرواتب الإسمية والعلاوات",
            LabelKu: "پەیژەی مووچە", HintKu: "خشتەی پلە و قۆناغەکان و مووچەی بنەڕەتی",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.SalaryScaleView,
            EditPermission: Permissions.SalaryScaleEdit),

        new NavEntry("admin/salaries", "الرواتب الشهرية والتشكيلات", "banknotes", "احتساب وجرد الرواتب الشهرية والمخصصات الإجمالية حسب التشكيل الإداري ولكل موظف",
            LabelKu: "مووچەی مانگانەی پێکهاتەکان", HintKu: "هەژمارکردن و پشکنینی مووچەی مانگانە بەپێی پێکهاتەکان بۆ هەر کارمەندێک",
            Roles: new[] { "Admin", "HRManager" },
            ViewPermission: Permissions.SalariesView,
            EditPermission: Permissions.SalariesEdit),

        new NavEntry("workflows", "المسارات الإدارية", "square-3-stack-3d", "تعريف الإجراءات ومراحل الاعتماد",
            LabelKu: "ڕێڕەوە کارگێڕییەکان", HintKu: "پێناسەکردنی ڕێکارەکان و قۆناغەکانی پەسەندکردن",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.WorkflowsView,
            EditPermission: Permissions.WorkflowsEdit)
    }, TitleKu: "پێکهاتە و مووچە");

    public static readonly NavGroup Reference = new("البيانات المرجعية", new[]
    {
        new NavEntry("data-import", "مركز استيراد البيانات (Excel)", "arrow-up-tray", "استيراد وتحديث الموظفين، الإجازات، كتب الشكر، الدورات، وحركات الخدمة من ملفات Excel",
            LabelKu: "ناوەندی هاوردەکردنی داتا (Excel)", HintKu: "هاوردەکردنی کارمەندان، مۆڵەتەکان، سوپاسنامەکان، خولەکان، و خزمەت لە ئیکسڵ",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" },
            ViewPermission: Permissions.DataImportView,
            EditPermission: Permissions.DataImportEdit),

        new NavEntry("admin/lookups", "إدارة القوائم المنسدلة والترميز", "table-cells", "إدارة كافة القوائم المنسدلة، الترميز المرجعي، وترابطات الجداول",
            LabelKu: "بەڕێوەبردنی لیستەکان", HintKu: "بەڕێوەبردنی هەموو لیستە دابەزیوەکان و بەستراوەییەکان",
            Roles: new[] { "Admin" },
            ViewPermission: Permissions.LookupsView,
            EditPermission: Permissions.LookupsEdit)
    }, TitleKu: "داتای بنەڕەتی");

    public static readonly NavGroup Governance = new("الحسابات والأمان", new[]
    {
        new NavEntry("users", "المستخدمون والأدوار", "shield-check", "الصلاحيات وربط الحساب بالموظف",
            LabelKu: "بەکارهێنەران و ڕۆڵەکان", HintKu: "دەسەڵاتەکان و بەستنەوەی هەژمار بە کارمەند",
            Roles: new[] { "Admin" },
            ViewPermission: Permissions.UsersView,
            EditPermission: Permissions.UsersEdit),

        new NavEntry("user-devices", "الأجهزة المقترنة", "device-phone-mobile", "الأجهزة المصرّح لها بالدخول",
            LabelKu: "ئامێرە بەستراوەکان", HintKu: "ئەو ئامێرانەی ڕێگەیان پێدراوە بۆ چوونەژوورەوە",
            Roles: new[] { "Admin" },
            ViewPermission: Permissions.DevicesView,
            EditPermission: Permissions.DevicesEdit),

        new NavEntry("sessions", "الجلسات السارية", "signal", "الجلسات النشطة وإنهاؤها",
            LabelKu: "دانیشتنە چالاکەکان", HintKu: "دانیشتنە چالاکەکان و کۆتایی پێهێنانیان",
            Roles: new[] { "Admin" },
            ViewPermission: Permissions.SessionsView,
            EditPermission: Permissions.SessionsEdit),

        new NavEntry("passkeys", "مفاتيح المرور", "finger-print", "الدخول بالبصمة ومفاتيح Passkeys",
            LabelKu: "کلیلی تێپەڕین (Passkeys)", HintKu: "چوونەژوورەوە بە پەنجەمۆر و کلیلی Passkeys",
            Roles: new[] { "Admin", "HRManager" },
            ViewPermission: Permissions.PasskeysView,
            EditPermission: Permissions.PasskeysEdit),

        new NavEntry("audit", "سجل التدقيق", "clipboard-document-list", "أثر العمليات والتغييرات",
            LabelKu: "تۆماری وردبینی", HintKu: "شوێنپێی کردارەکان و گۆڕانکارییەکان",
            Roles: new[] { "Admin", "HRManager" },
            ViewPermission: Permissions.AuditLogsView,
            EditPermission: Permissions.AuditLogsEdit),

        new NavEntry("admin/features", "إدارة الميزات والنظام", "cog-6-tooth", "التحكم في تفعيل وتعطيل ميزات ووحدات النظام والسمات",
            LabelKu: "بەڕێوەبردنی تایبەتمەندییەکان و سیستەم", HintKu: "کۆنتڕۆڵکردنی چالاککردن و ناچالاککردنی تایبەتمەندییەکانی سیستەم",
            Roles: new[] { "Admin" },
            ViewPermission: Permissions.FeaturesView,
            EditPermission: Permissions.FeaturesEdit),

        new NavEntry("design-system", "نظام التصميم والمكونات", "square-2-stack", "معرض المكونات الحية ومختبر الرموز المعمارية",
            LabelKu: "سیستەمی دیزاین", HintKu: "پێشانگای پێکهاتەکان و تاقیگە",
            Roles: new[] { "Admin" },
            ViewPermission: Permissions.DesignSystemView,
            EditPermission: Permissions.DesignSystemEdit)
    }, TitleKu: "هەژمارەکان و ئاسایش", Roles: new[] { "Admin", "HRManager" });

    public static readonly NavGroup PersonalDossier = new("الملف الشخصي", new[]
    {
        new NavEntry("portal", "ملفي", "user-circle", "عرض وتصفح ملفي الوظيفي الإلكتروني", Exact: true,
            LabelKu: "پڕۆفایلی من", HintKu: "پیشاندانی پڕۆفایلی کەسی و پیشەیی"),

        new NavEntry("my-feedback", "مشاركاتي", "chat-bubble-left-right", "متابعة مشاركاتي، مقترحاتي، واستفساراتي وشكاوى",
            LabelKu: "بەشدارییەکانم", HintKu: "بەدواداچوونی بەشداریکردن و سکاڵا و سەرنجەکانم")
    }, TitleKu: "پڕۆفایلی کەسی");

    public static readonly IReadOnlyList<NavGroup> All = new[] { PersonalDossier, Insights, Records, Profiles, Structure, Reference, Governance };
    public static readonly IReadOnlyList<NavGroup> EmployeeCatalog = new[] { PersonalDossier };

    public static IEnumerable<NavEntry> Entries => All.SelectMany(g => g.Items);

    /// <summary>Case-insensitive match over label, hint, Kurdish labels, and route.</summary>
    public static bool Matches(NavEntry entry, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var q = query.Trim();

        return entry.Label.Contains(q, StringComparison.OrdinalIgnoreCase)
            || (entry.LabelKu != null && entry.LabelKu.Contains(q, StringComparison.OrdinalIgnoreCase))
            || entry.Hint.Contains(q, StringComparison.OrdinalIgnoreCase)
            || (entry.HintKu != null && entry.HintKu.Contains(q, StringComparison.OrdinalIgnoreCase))
            || entry.Href.Contains(q, StringComparison.OrdinalIgnoreCase);
    }
}
