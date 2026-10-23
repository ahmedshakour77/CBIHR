using Microsoft.AspNetCore.Http;

namespace API.HR.Node.Services.Localization;

public sealed class LanguageService : ILanguageService
{
    public const string Arabic = "ar";
    public const string Kurdish = "ku";
    public const string English = "en";

    // English has a dictionary slot but almost no translated UI: picking it
    // flipped the layout to LTR around Arabic text. Until the interface is
    // translated, "en" resolves to Arabic everywhere. Flip this to re-enable.
    public static bool EnglishEnabled { get; } = false;

    private string _currentLanguage = Arabic;

    public LanguageService(IHttpContextAccessor? httpContextAccessor = null)
    {
        var context = httpContextAccessor?.HttpContext;
        if (context != null && context.Request.Cookies.TryGetValue("hr-lang", out var cookieVal))
        {
            if (string.Equals(cookieVal, Kurdish, StringComparison.OrdinalIgnoreCase))
            {
                _currentLanguage = Kurdish;
            }
            else if (EnglishEnabled && string.Equals(cookieVal, English, StringComparison.OrdinalIgnoreCase))
            {
                _currentLanguage = English;
            }
        }
    }

    public string CurrentLanguage => _currentLanguage;
    public bool IsKurdish => _currentLanguage == Kurdish;
    public bool IsArabic => _currentLanguage == Arabic;
    public bool IsEnglish => _currentLanguage == English;
    public bool IsRtl => _currentLanguage != English;
    public string Direction => IsRtl ? "rtl" : "ltr";

    public event Action? OnLanguageChanged;

    private static readonly Dictionary<string, (string Ar, string Ku, string En)> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        // App Shell & Header
        ["app.title"] = ("منظومة الموارد البشرية", "سیستەمی سەرچاوە مرۆییەکان", "HR Enterprise System"),
        ["app.sub"] = ("API.HR.Node", "API.HR.Node", "API.HR.Node"),
        ["app.version"] = ("الإصدار 1.0", "وەشانی 1.0", "Version 1.0"),
        ["auth.login"] = ("تسجيل الدخول", "چوونەژوورەوە", "Sign In"),
        ["auth.logout"] = ("تسجيل الخروج", "دەرچوون", "Sign Out"),
        ["auth.admin"] = ("مسؤول النظام", "بەڕێوەبەری سیستەم", "System Administrator"),
        ["theme.dark"] = ("التحويل إلى النمط الداكن", "گۆڕین بۆ شێوازی تاریک", "Switch to Dark Mode"),
        ["theme.light"] = ("التحويل إلى النمط الفاتح", "گۆڕین بۆ شێوازی ڕووناک", "Switch to Light Mode"),
        ["lang.switch"] = ("تغيير اللغة", "گۆڕینی زمان", "Change Language"),
        ["lang.current"] = ("عربي", "کوردی", "English"),

        // Navigation Groups & Items
        ["nav.group.workspace"] = ("مساحة العمل", "شوێنی کار", "Workspace"),
        ["nav.group.governance"] = ("الحسابات والأمان", "هەژمارەکان و ئاسایش", "Governance & Security"),
        ["nav.group.tools"] = ("أدوات", "ئامرازەکان", "Tools"),
        ["nav.swagger"] = ("توثيق الواجهات البرمجية", "بەڵگەنامەی API", "Swagger API Docs"),
        ["nav.home"] = ("الرئيسية", "سەرەکی", "Dashboard"),
        ["nav.home.hint"] = ("لوحة المؤشرات والمخططات الإحصائية", "تەختەی ئامار و هێڵکارییەکان", "KPI Metrics & Analytics"),
        ["nav.employees"] = ("الموظفون", "کارمەندان", "Employees"),
        ["nav.employees.hint"] = ("سجلات الموظفين والصور الشخصية", "تۆماری کارمەندان و وێنە کەسییەکان", "Employee Cards & Photos"),
        ["nav.directory"] = ("دليل الموظفين", "ڕێبەری کارمەندان", "Employee Directory"),
        ["nav.directory.hint"] = ("عرض جدولي موسّع مع بحث وفرز", "پیشاندانی خشتەیی بەرفراوان لەگەڵ گەڕان و ڕیزکردن", "Expanded Roster & Sorting"),
        ["nav.profile"] = ("الملف الإلكتروني", "فایلی ئەلیکترۆنی", "Employee Dossier"),
        ["nav.contacts"] = ("وسائل التواصل", "هۆکارەکانی پەیوەندی", "Contact Directory"),
        ["nav.contacts.hint"] = ("الهواتف والبريد وربط الحسابات", "تەلەفۆن و ئیمەیڵ و بەستنەوەی هەژمارەکان", "Phones & Account Linking"),
        ["nav.gallery"] = ("معرض الصور", "پێشانگای وێنەکان", "Photo Gallery"),
        ["nav.gallery.hint"] = ("جدار الصور ومتابعة التوثيق", "دیواری وێنەکان و بەدواداچوونی بەڵگەنامەکردن", "Photo Documentation Wall"),
        ["nav.reports"] = ("التقارير", "ڕاپۆرتەکان", "Reports"),
        ["nav.reports.hint"] = ("عرض تقديمي للإحصائيات", "پێشکەشکردنی ئامارەکان", "Statistical Presentation"),
        ["nav.workflows"] = ("المسارات الإدارية", "ڕێڕەوە کارگێڕییەکان", "Workflows"),
        ["nav.workflows.hint"] = ("تعريف الإجراءات ومراحل الاعتماد", "پێناسەکردنی ڕێکارەکان و قۆناغەکانی پەسەندکردن", "Approval Pipelines"),
        ["nav.users"] = ("المستخدمون والأدوار", "بەکارهێنەران و ڕۆڵەکان", "Users & Roles"),
        ["nav.users.hint"] = ("الصلاحيات وربط الحساب بالموظف", "دەسەڵاتەکان و بەستنەوەی هەژمار بە کارمەند", "Permissions & Employee Binding"),
        ["nav.devices"] = ("الأجهزة المقترنة", "ئامێرە بەستراوەکان", "Paired Devices"),
        ["nav.devices.hint"] = ("الأجهزة المصرّح لها بالدخول", "ئەو ئامێرانەی ڕێگەیان پێدراوە بۆ چوونەژوورەوە", "Authorized Login Devices"),
        ["nav.sessions"] = ("الجلسات السارية", "دانیشتنە چالاکەکان", "Active Sessions"),
        ["nav.sessions.hint"] = ("الجلسات النشطة وإنهاؤها", "دانیشتنە چالاکەکان و کۆتایی پێهێنانیان", "Manage Login Sessions"),
        ["nav.passkeys"] = ("مفاتيح المرور", "کلیلی تێپەڕین (Passkeys)", "Passkeys"),
        ["nav.passkeys.hint"] = ("الدخول بالبصمة ومفاتيح Passkeys", "چوونەژوورەوە بە پەنجەمۆر و کلیلی Passkeys", "Biometric Authentication"),
        ["nav.audit"] = ("سجل التدقيق", "تۆماری وردبینی", "Audit Trail"),
        ["nav.audit.hint"] = ("أثر العمليات والتغييرات", "شوێنپێی کردارەکان و گۆڕانکارییەکان", "Operations & Change Logs"),

        // General Actions
        ["btn.search"] = ("بحث", "گەڕان", "Search"),
        ["btn.add"] = ("إضافة", "زیادکردن", "Add New"),
        ["btn.edit"] = ("تعديل", "دەستکاریکردن", "Edit"),
        ["btn.delete"] = ("حذف", "سڕینەوە", "Delete"),
        ["btn.save"] = ("حفظ", "پاشەکەوتکردن", "Save"),
        ["btn.cancel"] = ("إلغاء", "هەڵوەشاندنەوە", "Cancel"),
        ["btn.details"] = ("تفاصيل", "وردەکارییەکان", "Details"),
        ["btn.print"] = ("طباعة", "چاپکردن", "Print"),
        ["btn.export"] = ("تصدير", "هەناردەکردن", "Export"),
        ["btn.back"] = ("رجوع", "گەڕانەوە", "Back"),
        ["btn.close"] = ("إغلاق", "داخستن", "Close"),
        ["btn.filter"] = ("تصفية", "پاڵاوتن", "Filter"),
        ["btn.refresh"] = ("تحديث", "نوێکردنەوە", "Refresh"),

        // Dashboard & KPIs
        ["kpi.headcount"] = ("إجمالي الكادر الوظيفي", "سەرجەمی کارمەندان", "Total Headcount"),
        ["kpi.males"] = ("ذكور", "نێر", "Males"),
        ["kpi.females"] = ("إناث", "مێ", "Females"),
        ["kpi.service"] = ("متوسط الخدمة الوظيفية", "تێکڕای خزمەتی وەزیفی", "Avg Service Length"),
        ["kpi.retirement"] = ("مؤشر سن التقاعد", "نیشاندەری تەمەنی خانەنشینی", "Retirement Horizon"),
        ["kpi.honors"] = ("كتب الشكر والتقدير", "سوپاس و پێزانین", "Appreciation Letters"),
        ["kpi.degrees"] = ("الشهادات العليا", "بڕوانامە باڵاکان", "Higher Degrees"),
        ["kpi.photos"] = ("توثيق الصور الرسمية", "بەڵگەنامەکردنی وێنە فەرمییەکان", "Photo Coverage"),
        ["kpi.leadership"] = ("الكادر القيادي والإشرافي", "ستافی سەرکردایەتی و سەرپەرشتیاری", "Leadership & Supervisors"),
        ["kpi.cohorts"] = ("الأجيال الوظيفية", "نەوە وەزیفییەکان (Cohorts)", "Workforce Generations"),
        ["kpi.demographics"] = ("الهرم العمري والديموغرافيا", "هەرەمی تەمەن و دیمۆگرافیا", "Age Pyramid & Demographics"),

        // Cohorts
        ["cohort.alpha"] = ("جيل ألفا", "نەوەی ئەلفا (Alpha)", "Gen Alpha"),
        ["cohort.genz"] = ("الجيل Z", "نەوەی Z", "Gen Z"),
        ["cohort.millennials"] = ("جيل الألفية (Y)", "نەوەی هەزارە (Y)", "Millennials (Y)"),
        ["cohort.genx"] = ("الجيل X", "نەوەی X", "Gen X"),
        ["cohort.boomers"] = ("طفرة المواليد (بومرز)", "نەوەی بوومەرز (Boomers)", "Baby Boomers"),
        ["cohort.silent"] = ("الجيل الصامت", "نەوەی بێدەنگ (Silent)", "Silent Generation"),

        // Shell, navigation and shared components (audit G8)
        ["nav.main"] = ("التنقل الرئيسي", "ڕێنیشاندەری سەرەکی", "Main navigation"),
        ["nav.group.prefs"] = ("التفضيلات", "تایبەتمەندییەکان", "Preferences"),
        ["nav.prefs"] = ("إعدادات العرض والمظهر", "شاشە و ڕێکخستن", "Display & appearance"),
        ["nav.features"] = ("لوحة التحكم وإدارة الميزات", "کۆنتڕۆڵ پانێڵی سیستم", "Control panel"),
        ["nav.features.hint"] = ("لوحة التحكم بالميزات والإعدادات", "کۆنتڕۆڵی تایبەتمەندی و ڕێکخستنەکان", "Features and settings"),
        ["nav.more"] = ("المزيد", "زیاتر", "More"),
        ["nav.open"] = ("فتح قائمة التنقل", "کردنەوەی لیستی ڕێنیشاندەر", "Open navigation"),
        ["nav.close"] = ("إغلاق قائمة التنقل", "داخستنی لیستی ڕێنیشاندەر", "Close navigation"),
        ["nav.drawer"] = ("التنقل", "ڕێنیشاندەر", "Navigation"),
        ["nav.all"] = ("كل الأقسام", "هەموو بەشەکان", "All sections"),
        ["nav.dock"] = ("شريط التنقل العائم", "شریتی ڕێنیشاندەری بزۆک", "Floating navigation"),
        ["common.close"] = ("إغلاق", "داخستن", "Close"),
        ["common.new_tab"] = ("(يفتح في تبويب جديد)", "(لە تابێکی نوێ دەکرێتەوە)", "(opens in a new tab)"),
        ["a11y.skip"] = ("تخطَّ إلى المحتوى الرئيسي", "بازدان بۆ ناوەڕۆکی سەرەکی", "Skip to main content"),
        ["error.unexpected"] = ("حدث خطأ غير متوقع في النظام.", "هەڵەیەکی چاوەڕواننەکراو ڕوویدا.", "An unexpected error occurred."),
        ["error.reload"] = ("إعادة التحميل", "دووبارە بارکردنەوە", "Reload"),
        ["error.dismiss"] = ("إخفاء التنبيه", "شاردنەوەی ئاگادارکردنەوە", "Dismiss"),
        ["toast.close"] = ("إغلاق التنبيه", "داخستنی ئاگادارکردنەوە", "Close notification"),
        ["auth.user"] = ("المستخدم", "بەکارهێنەر", "User"),
        ["auth.employee"] = ("الموظف", "کارمەند", "Employee"),

        // Command palette
        ["cmd.trigger"] = ("بحث سريع…", "گەڕانی خێرا…", "Quick search…"),
        ["cmd.trigger.label"] = ("البحث السريع (Ctrl + K)", "گەڕانی خێرا (Ctrl + K)", "Quick search (Ctrl + K)"),
        ["cmd.dialog"] = ("البحث السريع وشريط الأوامر", "گەڕانی خێرا و فەرمانەکان", "Quick search and commands"),
        ["cmd.placeholder"] = ("ابحث عن صفحة أو إجراء…", "بگەڕێ بۆ پەڕە یان کردار…", "Search pages or actions…"),
        ["cmd.placeholder.staff"] = ("ابحث عن صفحة، موظف، أو إجراء…", "بگەڕێ بۆ پەڕە، کارمەند یان کردار…", "Search pages, employees or actions…"),
        ["cmd.searching"] = ("جارٍ البحث…", "گەڕان…", "Searching…"),
        ["cmd.empty"] = ("لا توجد نتائج مطابقة", "هیچ ئەنجامێک نەدۆزرایەوە", "No matching results"),
        ["cmd.count"] = ("{0} نتيجة", "{0} ئەنجام", "{0} results"),
        ["cmd.hint.navigate"] = ("للتنقل", "بۆ گواستنەوە", "to move"),
        ["cmd.hint.select"] = ("للاختيار", "بۆ هەڵبژاردن", "to open"),
        ["cmd.hint.close"] = ("للإغلاق", "بۆ داخستن", "to close"),
        ["cmd.group.employees"] = ("الموظفون", "کارمەندان", "Employees"),
        ["cmd.group.actions"] = ("إجراءات سريعة", "کردارە خێراکان", "Quick actions"),
        ["cmd.employee.id"] = ("الرقم:", "ژمارە:", "ID:"),
        ["cmd.act.prefs.sub"] = ("الخط والحجم والسمة وكثافة الجداول", "فۆنت، قەبارە، ڕووکار و چڕی خشتەکان", "Font, size, theme and density"),
        ["cmd.act.theme"] = ("تبديل المظهر (فاتح / داكن)", "گۆڕینی ڕووکار (ڕووناک / تاریک)", "Toggle light / dark"),
        ["cmd.act.logout.sub"] = ("إنهاء الجلسة الحالية بأمان", "کۆتاییهێنان بە دانیشتنەکە بە سەلامەتی", "End this session securely"),

        // Shared component labels (audit R2)
        ["feedback.type"] = ("نوع المشاركة", "جۆری بەشداری", "Submission type"),
        ["file.remove"] = ("إزالة الملف", "لابردنی فایل", "Remove file"),
        ["notif.open"] = ("فتح الإشعار:", "کردنەوەی ئاگادارکردنەوە:", "Open notification:"),
        ["notif.unread"] = ("غير مقروء", "نەخوێندراوە", "Unread"),
        ["notif.mark_read"] = ("تحديد كمقروء", "وەک خوێندراوە نیشان بدە", "Mark as read"),
        ["notif.delete"] = ("حذف الإشعار", "سڕینەوەی ئاگادارکردنەوە", "Delete notification"),
        ["alert.close"] = ("إغلاق التنبيه", "داخستنی ئاگاداری", "Dismiss alert"),
        ["grid.clear_search"] = ("مسح البحث", "سڕینەوەی گەڕان", "Clear search"),
        ["grid.bulk"] = ("إجراءات جماعية", "کردارە بەکۆمەڵەکان", "Bulk actions"),
        ["grid.density"] = ("كثافة الجدول", "چڕی خشتە", "Table density"),
        ["grid.density.compact"] = ("عرض مضغوط", "پیشاندانی چڕ", "Compact"),
        ["grid.density.comfortable"] = ("عرض مريح", "پیشاندانی ئاسوودە", "Comfortable"),
        ["drawer.close"] = ("إغلاق اللوحة", "داخستنی پانێڵ", "Close panel"),
    };

    public void SetLanguage(string languageCode)
    {
        var normalized = languageCode.ToLowerInvariant().Trim();
        if (normalized != Kurdish && !(EnglishEnabled && normalized == English))
        {
            normalized = Arabic;
        }

        if (_currentLanguage != normalized)
        {
            _currentLanguage = normalized;
            OnLanguageChanged?.Invoke();
        }
    }

    public void ToggleLanguage()
    {
        var next = _currentLanguage switch
        {
            Arabic => Kurdish,
            Kurdish => English,
            _ => Arabic
        };
        SetLanguage(next);
    }

    public string T(string key) => T(key, key);

    public string T(string key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key)) return fallback;

        if (Translations.TryGetValue(key, out var entry))
        {
            return _currentLanguage switch
            {
                Kurdish => entry.Ku,
                English => entry.En,
                _ => entry.Ar
            };
        }

        return fallback;
    }
}
