using System.Reflection;
using API.HR.Node.Components.Pages;
using API.HR.Node.Data;
using API.HR.Node.Services.Audit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

/// <summary>
/// Renders the user-activity page to static HTML over synthetic activity, one file per tab,
/// so it can be looked at without a database or a login. Runs only when UA_PREVIEW_DIR
/// names an output folder; the people and events it draws are invented.
/// </summary>
public sealed class UserActivityPreview
{
    [Fact]
    public async Task RendersEveryTabOverSyntheticActivity()
    {
        var outDir = Environment.GetEnvironmentVariable("UA_PREVIEW_DIR");
        var events = SyntheticEvents();

        foreach (var tab in new[] { "overview", "live", "day", "user" })
        {
            var html = await RenderAsync(events, tab);
            Assert.Contains("hr-ua", html);
            if (!string.IsNullOrWhiteSpace(outDir))
            {
                await File.WriteAllTextAsync(Path.Combine(outDir, $"{tab}.html"), Shell(html));
            }
        }
    }

    private static async Task<string> RenderAsync(List<UserActivityEvent> events, string tab)
    {
        var activity = new Mock<IUserActivityService>();
        activity.Setup(a => a.GetRecentEvents(It.IsAny<DateTimeOffset?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns<DateTimeOffset?, string?, string?, string?, int>((from, _, _, _, max) =>
                events.Where(e => from is null || e.TimestampUtc >= from).OrderByDescending(e => e.TimestampUtc).Take(max).ToList());

        var dbName = Guid.NewGuid().ToString();
        var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        dbFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options));

        var services = new ServiceCollection();
        services.AddSingleton(activity.Object);
        services.AddSingleton(dbFactory.Object);
        services.AddSingleton<IJSRuntime>(Mock.Of<IJSRuntime>());
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new PreviewRenderer(provider, tab);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = renderer.BeginRenderingComponent(typeof(AdminUserAnalyticsPage), ParameterView.Empty);
            await root.QuiescenceTask;

            // The Day tab opens with a session picked, so the preview shows its steps too.
            if (tab == "day" && renderer.Page is { } page)
            {
                var type = page.GetType();
                var sessions = (System.Collections.IList)type.GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page)!;
                var first = sessions.Cast<object>().First();
                var id = (string)first.GetType().GetProperty("Id")!.GetValue(first)!;
                type.GetField("_daySessionId", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(page, id);
                type.GetMethod("StateHasChanged", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(page, null);
            }

            var html = root.ToHtmlString();
            (renderer.Page as IDisposable)?.Dispose();
            return html;
        });
    }

    private static string Shell(string body) => $$"""
        <!doctype html>
        <html lang="ar" dir="rtl" data-theme="{{(Environment.GetEnvironmentVariable("UA_PREVIEW_THEME") ?? "light")}}">
        <head>
        <meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans+Arabic:wght@400;500;600;700&family=Inter:wght@400;500;600;700&display=swap">
        <link rel="stylesheet" href="{{Asset("css/hr-tokens.css")}}">
        <link rel="stylesheet" href="{{Asset("app.css")}}">
        <link rel="stylesheet" href="{{Asset("css/hr-components.css")}}">
        <link rel="stylesheet" href="{{Asset("hr-ui.css")}}">
        </head>
        <body style="margin:0;background:var(--ground)"><main style="padding:1.5rem 1.75rem">{{body}}</main></body>
        </html>
        """;

    private static string Asset(string path)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "API.HR.Node", "wwwroot"))) dir = dir.Parent;
        return new Uri(Path.Combine(dir!.FullName, "API.HR.Node", "wwwroot", path)).AbsoluteUri;
    }

    private static List<UserActivityEvent> SyntheticEvents()
    {
        var rnd = new Random(5);
        var now = DateTimeOffset.UtcNow;
        (string User, string Name, string Role)[] people =
        [
            ("sara.j", "سارة الجبوري", "HR"), ("ali.h", "علي حسن", "Admin"), ("maryam.k", "مريم كاظم", "HR"),
            ("haider.a", "حيدر عباس", "HR"), ("zainab.a", "زينب علي", "Viewer"), ("karrar.m", "كرار محمد", "HR"),
            ("hussein.j", "حسين جواد", "Viewer"), ("noor.s", "نور صالح", "HR"), ("omar.f", "عمر فاضل", "Viewer")
        ];
        (string Path, string Title)[] pages =
        [
            ("/Employees", "دليل الموظفين"), ("/employees/10442", "ملف الموظف (10442)"), ("/employees/10391", "ملف الموظف (10391)"),
            ("/gallery", "معرض الصور"), ("/", "الرئيسية ولوحة المؤشرات"), ("/employee-contacts", "جهات الاتصال"),
            ("/users", "المستخدمون"), ("/audit", "سجل التدقيق"), ("/Leaves", "الإجازات")
        ];
        double[] weights = [5, 3, 2, 3, 2, 1.5, 1, .6, 1];
        (string Label, string[] Values)[] dropdowns =
        [
            ("الحالة الوظيفية", ["مستمر بالخدمة", "منسّب", "مجاز طويلاً"]), ("التشكيل", ["دائرة تقنية المعلومات", "دائرة الموارد البشرية", "فرع البصرة"]),
            ("العنوان الوظيفي", ["محاسب", "مبرمج", "مدقق"]), ("حجم الصفحة", ["24", "48"])
        ];
        string[] searches = ["أحمد كريم", "10442", "زينب", "علي حسين", "مدقق", "10391", "حيدر"];
        string[] clicks = ["رفع صورة", "حفظ", "تصدير", "عرض الصورة", "إعادة تعيين"];

        (string, string) PickPage()
        {
            var r = rnd.NextDouble() * weights.Sum();
            for (var i = 0; i < pages.Length; i++) { r -= weights[i]; if (r <= 0) return pages[i]; }
            return pages[0];
        }

        var list = new List<UserActivityEvent>();
        for (var day = 6; day >= 0; day--)
        {
            var date = DateTime.Today.AddDays(-day);
            if (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday) continue;
            foreach (var (person, idx) in people.Select((p, i) => (p, i)))
            {
                if (rnd.NextDouble() < .25 && idx > 2) continue;
                var start = new DateTimeOffset(date.AddHours(7.8 + rnd.NextDouble() * 2.2));
                var length = TimeSpan.FromMinutes(90 + rnd.Next(300));
                var online = day == 0 && idx is 0 or 1 or 2 or 6;
                var end = online ? now : start + length;
                if (end > now) end = now.AddMinutes(-40 - idx * 7);
                if (online) end = now;
                if (start >= end) continue;

                var ip = $"10.20.{idx + 1}.{17 + idx * 3}";
                UserActivityEvent Ev(DateTimeOffset t, string cat, string code, string title, string? path = null, string? pageTitle = null, string? el = null, string? val = null, int? dur = null, bool ok = true) => new()
                {
                    TimestampUtc = t, UserId = person.User, UserName = person.User, EmployeeName = person.Name, EmployeeId = (10200 + idx).ToString(),
                    UserRole = person.Role, Category = cat, ActionCode = code, ActionTitle = title, PagePath = path, PageTitle = pageTitle,
                    ElementLabel = el, ValueText = val, DurationSeconds = dur, IpAddress = ip, Success = ok
                };

                if (idx == 4 && day == 1) list.Add(Ev(start.AddMinutes(-2), ActivityCategories.Auth, "USER_LOGIN_FAILED", "محاولة دخول فاشلة", val: "كلمة المرور غير صحيحة", ok: false));
                list.Add(Ev(start, ActivityCategories.Auth, "USER_LOGIN_SUCCESS", "تسجيل دخول ناجح إلى النظام"));

                var t = start.AddSeconds(20);
                DateTimeOffset? lastPageAt = null;
                while (t < end)
                {
                    var (path, title) = PickPage();
                    list.Add(Ev(t, ActivityCategories.Navigation, "PAGE_VIEW", $"الانتقال إلى صفحة: {title}", path, title,
                        dur: lastPageAt is null ? null : (int)(t - lastPageAt.Value).TotalSeconds));
                    lastPageAt = t;
                    var stay = TimeSpan.FromMinutes(2 + rnd.Next(40));
                    var stepAt = t;
                    for (var k = rnd.Next(1, 6); k > 0; k--)
                    {
                        stepAt = stepAt.AddSeconds(20 + rnd.Next(200));
                        if (stepAt >= end || stepAt >= t + stay) break;
                        var kind = rnd.Next(10);
                        if (kind < 4)
                        {
                            var d = dropdowns[rnd.Next(dropdowns.Length)];
                            var v = d.Values[(int)(Math.Pow(rnd.NextDouble(), 2) * d.Values.Length)];
                            list.Add(Ev(stepAt, ActivityCategories.Dropdown, "DROPDOWN_CHANGE", $"تغيير «{d.Label}»", path, title, d.Label, v));
                        }
                        else if (kind < 7)
                        {
                            list.Add(Ev(stepAt, ActivityCategories.Search, "SEARCH_QUERY", "بحث", path, title, "بحث بالاسم أو الرقم", searches[(int)(Math.Pow(rnd.NextDouble(), 1.6) * searches.Length)]));
                        }
                        else if (kind < 9)
                        {
                            var c = clicks[rnd.Next(clicks.Length)];
                            list.Add(Ev(stepAt, ActivityCategories.Click, "BUTTON_CLICK", $"نقر «{c}»", path, title, c));
                        }
                        else
                        {
                            list.Add(Ev(stepAt, ActivityCategories.Security, "PHOTO_UPLOAD", "رفع صورة شخصية", path, title, "10391", ok: rnd.NextDouble() > .1));
                        }
                    }
                    t += stay;
                }

                if (!online) list.Add(Ev(end, ActivityCategories.Auth, "USER_LOGOUT", "تسجيل خروج من النظام"));
            }
        }
        return list;
    }

    // The page declares InteractiveServer; a static preview renders it in place, on the requested tab.
    private sealed class PreviewRenderer(IServiceProvider services, string tab) : StaticHtmlRenderer(services, NullLoggerFactory.Instance)
    {
        public IComponent? Page { get; private set; }

        protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId,
            IComponentActivator componentActivator, IComponentRenderMode renderMode)
        {
            var component = componentActivator.CreateInstance(componentType);
            if (component is AdminUserAnalyticsPage)
            {
                componentType.GetField("_activeTab", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(component, tab);
                componentType.GetField("_autoRefresh", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(component, false);
                Page = component;
            }
            return component;
        }
    }
}
