using System.Security.Claims;

namespace API.HR.Node.Components.Layout;

/// <summary>
/// One destination in the system's navigation with multilingual support and role-based permissions.
/// </summary>
public sealed record NavEntry(
    string Href,
    string Label,
    string Icon,
    string Hint,
    bool Exact = false,
    string? LabelKu = null,
    string? HintKu = null,
    string[]? Roles = null)
{
    public string GetLabel(bool isKurdish) =>
        isKurdish && !string.IsNullOrWhiteSpace(LabelKu) ? LabelKu : Label;

    public string GetHint(bool isKurdish) =>
        isKurdish && !string.IsNullOrWhiteSpace(HintKu) ? HintKu : Hint;

    public bool IsAllowedFor(ClaimsPrincipal? user)
    {
        if (Roles == null || Roles.Length == 0)
        {
            return true;
        }

        if (user == null || user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        foreach (var role in Roles)
        {
            if (user.IsInRole(role))
            {
                return true;
            }
        }

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
        if (Roles != null && Roles.Length > 0)
        {
            if (user == null || user.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            bool hasGroupRole = false;
            foreach (var role in Roles)
            {
                if (user.IsInRole(role))
                {
                    hasGroupRole = true;
                    break;
                }
            }

            if (!hasGroupRole)
            {
                return false;
            }
        }

        return Items.Any(item => item.IsAllowedFor(user));
    }
}

/// <summary>
/// The navigation is declared once, here, and read by the rail, the mobile
/// drawer and the command palette alike.
/// </summary>
public static class NavCatalog
{
    // The staff workspace, split by the job at hand rather than one flat
    // list of 22 links. Each destination has an icon no other entry uses.
    public static readonly NavGroup Insights = new("المؤشرات والتقارير", new[]
    {
        new NavEntry("", "الرئيسية", "home", "لوحة المؤشرات والمخططات الإحصائية", Exact: true,
            LabelKu: "سەرەکی", HintKu: "تەختەی ئامار و هێڵکارییەکان",
            Roles: new[] { "Admin", "HRManager", "FormationManager" }),

        new NavEntry("formation-dashboard", "لوحة مؤشرات التشكيل", "building-office-2", "لوحة المؤشرات والتقارير التنفيذية الشاملة للتشكيل الإداري",
            LabelKu: "تەختەی نیشاندەرەکانی پێکهاتە", HintKu: "تەختەی نیشاندەرە جێبەجێکارییەکان بۆ پێکهاتەی کارگێڕی",
            Roles: new[] { "Admin", "HRManager", "FormationManager" }),

        new NavEntry("reports-slider", "التقارير", "presentation-chart-bar", "عرض تقديمي للإحصائيات",
            LabelKu: "ڕاپۆرتەکان", HintKu: "پێشکەشکردنی ئامارەکان",
            Roles: new[] { "Admin", "HRManager" }),

        new NavEntry("canva-reports", "تقارير Canva", "sparkles", "تصدير ودمج الإحصائيات في قوالب Canva الاحترافية",
            LabelKu: "ڕاپۆرتەکانی Canva", HintKu: "تێکەڵکردنی ئامارەکان لە قاڵبەکانی Canva",
            Roles: new[] { "Admin", "HRManager" })
    }, TitleKu: "نیشاندەر و ڕاپۆرتەکان");

    public static readonly NavGroup Records = new("شؤون الموظفين", new[]
    {
        new NavEntry("employees", "الموظفون", "users", "سجلات الموظفين، البحث والفرز المتقدم، وتوثيق الصور",
            LabelKu: "کارمەندان", HintKu: "تۆماری کارمەندان، گەڕان و ڕیزکردن و وێنەکان",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" }),

        new NavEntry("leaves", "الإجازات", "calendar-days", "سجل إجازات الموظفين، الأرصدة، والإحصائيات",
            LabelKu: "مۆڵەتەکان", HintKu: "تۆماری مۆڵەتی کارمەندان، باڵانس و ئامارەکان",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" }),

        new NavEntry("appreciation-letters", "كتب الشكر", "document-text", "سجل كتب الشكر والتقدير، التكريم، واحتساب القِدم",
            LabelKu: "سوپاسنامەکان", HintKu: "تۆماری سوپاسنامە و پێزانین و پێشخستن",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" }),

        new NavEntry("courses", "الدورات التدريبية", "academic-cap", "سجل ومتابعة الدورات التدريبية للموظفين والمشاركات",
            LabelKu: "خولەکانی ڕاهێنان", HintKu: "تۆمار و بەدواداچوونی خولەکانی ڕاهێنانی کارمەندان",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" }),

        new NavEntry("employee-feedbacks", "مشاركات الموظفين", "inbox", "متابعة واستعراض مشاركات، مقترحات، وشكاوى الموظفين",
            LabelKu: "بەشداریی کارمەندان", HintKu: "بەدواداچوونی پێشنیاز، سکاڵا و سەرنجی کارمەندان",
            Roles: new[] { "Admin", "HRManager" })
    }, TitleKu: "کاروباری کارمەندان");

    public static readonly NavGroup Profiles = new("بيانات مكمّلة", new[]
    {
        new NavEntry("employee-contacts", "وسائل التواصل", "phone", "الهواتف والبريد وربط الحسابات",
            LabelKu: "هۆکارەکانی پەیوەندی", HintKu: "تەلەفۆن و ئیمەیڵ و بەستنەوەی هەژمارەکان",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" }),

        new NavEntry("gallery", "معرض الصور", "camera", "جدار الصور ومتابعة التوثيق",
            LabelKu: "پێشانگای وێنەکان", HintKu: "دیواری وێنەکان و بەدواداچوونی بەڵگەنامەکردن",
            Roles: new[] { "Admin", "HRManager" }),

        new NavEntry("vehicles", "مركبات ومتعلقات الموظفين", "truck", "سجل سيارات الموظفين، أرقام اللوحات، وتخويلات الدخول للمقرات والمباني",
            LabelKu: "ئۆتۆمبێلی کارمەندان", HintKu: "تۆماری ئۆتۆمبێلی کارمەندان، ژمارەی تابلۆ و ڕێپێدانی چوونەژوورەوە",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" }),

        new NavEntry("residences-map", "خريطة السكن", "map-pin", "استعراض مواقع ودور وشقق سكن الموظفين على الخريطة الجغرافية الموحدة",
            LabelKu: "نەخشەی نیشتەجێبوون", HintKu: "پیشاندانی شوێنی نیشتەجێبوونی کارمەندان لەسەر نەخشە",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" }),

        new NavEntry("admin/birthdays", "أعياد الميلاد والمناسبات", "cake", "تقويم أعياد ميلاد الكوادر، الأبراج الفلكية، وتحليل الأجيال وتوافق التواريخ",
            LabelKu: "ڕۆژی لەدایکبوون و بورجەکان", HintKu: "ساڵنامەی لەدایکبوونی کارمەندان، بورجەکان و نەوەکان",
            Roles: new[] { "Admin" })
    }, TitleKu: "زانیاریی تەواوکەر");

    public static readonly NavGroup Structure = new("الهيكل والرواتب", new[]
    {
        new NavEntry("org-hierarchy", "الهيكلية الإدارية", "rectangle-stack", "مخطط التشكيلات ومسارات التبعية لرأس الهرم",
            LabelKu: "پێکهاتەی کارگێڕی", HintKu: "هێڵکاریی پێکهاتە و بەستراوەیی تا لووتکە",
            Roles: new[] { "Admin", "HRManager", "FormationManager" }),

        new NavEntry("job-hierarchy", "الهيكل الوظيفي الشجري", "squares-2x2", "مخطط شجرة الهيكل الوظيفي والتنظيمي وسلسلة القيادة",
            LabelKu: "پێکهاتەی وەزیفی دارێژەیی", HintKu: "هێڵکاریی دارێژەیی پێکهاتەی وەزیفی و سەرکردایەتی",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" }),

        new NavEntry("admin/job-classifications", "العناوين والعوائل الوظيفية", "briefcase", "دليل وتصنيف العناوين الوظيفية والعوائل والدرجات الوظيفية المعتمدة",
            LabelKu: "ناونیشان و خێزانە وەزیفییەکان", HintKu: "ڕێبەری ناونیشانە وەزیفییەکان و خێزانەکان و پلەکان",
            Roles: new[] { "Admin" }),

        new NavEntry("jobtitles", "استحقاق العناوين والترقيات", "arrow-trending-up", "تنبؤ واحتساب استحقاق الترقية والعنوان بالسلم وكتب الشكر والعقوبات",
            LabelKu: "شایستەیی ناونیشان و پلەبەرزکردنەوە", HintKu: "پێشبینیکردنی شایستەیی پلەبەرزکردنەوە بەپێی پەیژە و سوپاسنامە و سزاکان",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" }),

        new NavEntry("salary-scale", "السلم الوظيفي", "chart-bar", "جدول الدرجات والمراحل والرواتب الإسمية والعلاوات",
            LabelKu: "پەیژەی مووچە", HintKu: "خشتەی پلە و قۆناغەکان و مووچەی بنەڕەتی",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" }),

        new NavEntry("admin/salaries", "الرواتب الشهرية والتشكيلات", "banknotes", "احتساب وجرد الرواتب الشهرية والمخصصات الإجمالية حسب التشكيل الإداري ولكل موظف",
            LabelKu: "مووچەی مانگانەی پێکهاتەکان", HintKu: "هەژمارکردن و پشکنینی مووچەی مانگانە بەپێی پێکهاتەکان بۆ هەر کارمەندێک",
            Roles: new[] { "Admin", "HRManager" }),

        new NavEntry("workflows", "المسارات الإدارية", "square-3-stack-3d", "تعريف الإجراءات ومراحل الاعتماد",
            LabelKu: "ڕێڕەوە کارگێڕییەکان", HintKu: "پێناسەکردنی ڕێکارەکان و قۆناغەکانی پەسەندکردن",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" })
    }, TitleKu: "پێکهاتە و مووچە");

    public static readonly NavGroup Reference = new("البيانات المرجعية", new[]
    {
        new NavEntry("data-import", "مركز استيراد البيانات (Excel)", "arrow-up-tray", "استيراد وتحديث الموظفين، الإجازات، كتب الشكر، الدورات، وحركات الخدمة من ملفات Excel",
            LabelKu: "ناوەندی هاوردەکردنی داتا (Excel)", HintKu: "هاوردەکردنی کارمەندان، مۆڵەتەکان، سوپاسنامەکان، خولەکان، و خزمەت لە ئیکسڵ",
            Roles: new[] { "Admin", "HRManager", "FormationManager", "Supervisor" }),

        new NavEntry("admin/lookups", "إدارة القوائم المنسدلة والترميز", "table-cells", "إدارة كافة القوائم المنسدلة، الترميز المرجعي، وترابطات الجداول",
            LabelKu: "بەڕێوەبردنی لیستەکان", HintKu: "بەڕێوەبردنی هەموو لیستە دابەزیوەکان و بەستراوەییەکان",
            Roles: new[] { "Admin" })
    }, TitleKu: "داتای بنەڕەتی");

    public static readonly NavGroup Governance = new("الحسابات والأمان", new[]
    {
        new NavEntry("users", "المستخدمون والأدوار", "shield-check", "الصلاحيات وربط الحساب بالموظف",
            LabelKu: "بەکارهێنەران و ڕۆڵەکان", HintKu: "دەسەڵاتەکان و بەستنەوەی هەژمار بە کارمەند",
            Roles: new[] { "Admin" }),

        new NavEntry("user-devices", "الأجهزة المقترنة", "device-phone-mobile", "الأجهزة المصرّح لها بالدخول",
            LabelKu: "ئامێرە بەستراوەکان", HintKu: "ئەو ئامێرانەی ڕێگەیان پێدراوە بۆ چوونەژوورەوە",
            Roles: new[] { "Admin" }),

        new NavEntry("sessions", "الجلسات السارية", "signal", "الجلسات النشطة وإنهاؤها",
            LabelKu: "دانیشتنە چالاکەکان", HintKu: "دانیشتنە چالاکەکان و کۆتایی پێهێنانیان",
            Roles: new[] { "Admin" }),

        new NavEntry("passkeys", "مفاتيح المرور", "finger-print", "الدخول بالبصمة ومفاتيح Passkeys",
            LabelKu: "کلیلی تێپەڕین (Passkeys)", HintKu: "چوونەژوورەوە بە پەنجەمۆر و کلیلی Passkeys",
            Roles: new[] { "Admin", "HRManager" }),

        new NavEntry("audit", "سجل التدقيق", "clipboard-document-list", "أثر العمليات والتغييرات",
            LabelKu: "تۆماری وردبینی", HintKu: "شوێنپێی کردارەکان و گۆڕانکارییەکان",
            Roles: new[] { "Admin", "HRManager" }),

        new NavEntry("admin/features", "إدارة الميزات والنظام", "cog-6-tooth", "التحكم في تفعيل وتعطيل ميزات ووحدات النظام والسمات",
            LabelKu: "بەڕێوەبردنی تایبەتمەندییەکان و سیستەم", HintKu: "کۆنتڕۆڵکردنی چالاککردن و ناچالاککردنی تایبەتمەندییەکانی سیستەم",
            Roles: new[] { "Admin" }),

        new NavEntry("design-system", "نظام التصميم والمكونات", "square-2-stack", "معرض المكونات الحية ومختبر الرموز المعمارية",
            LabelKu: "سیستەمی دیزاین", HintKu: "پێشانگای پێکهاتەکان و تاقیگە",
            Roles: new[] { "Admin" }),
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
