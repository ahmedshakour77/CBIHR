using System.Security.Claims;
using API.HR.Node.Components.Pages;
using API.HR.Node.Services.Dashboard;
using API.HR.Node.Services.Employees;
using API.HR.Node.Services.Localization;
using API.HR.Node.Services.OrganizationalHierarchy;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

/// <summary>
/// Renders the home dashboard to static HTML over a synthetic workforce, so the page
/// can be looked at without a database or a login. Runs only when HOME_PREVIEW_OUT
/// names an output file; the data it draws is invented and labelled as such.
/// </summary>
public sealed class HomeDashboardPreview
{
    [Fact]
    public async Task RendersTheDashboardOverSyntheticData()
    {
        var output = Environment.GetEnvironmentVariable("HOME_PREVIEW_OUT");
        var rows = SyntheticRows(2400);
        var html = await RenderAsync(rows);

        Assert.Contains("home-brief", html);
        Assert.Contains("ch-records", html);
        if (!string.IsNullOrWhiteSpace(output)) await File.WriteAllTextAsync(output, html);
    }

    private static async Task<string> RenderAsync(IReadOnlyList<WorkforceRow> rows)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rnd = new Random(11);
        var leaves = rows.Where(_ => rnd.NextDouble() < .06)
            .Select(r => new LeaveFact(r.Id, rnd.Next(3) switch { 0 => "إجازة اعتيادية", 1 => "إجازة مرضية", _ => "إجازة أمومة" },
                today.AddDays(-rnd.Next(1, 10)), today.AddDays(rnd.Next(0, 20)), rnd.Next(2, 25)))
            .ToList();
        cache.Set("home:workforce-snapshot", new WorkforceSnapshot
        {
            Rows = rows,
            Leaves = leaves,
            Letters = rows.Where(r => r.Letters > 0).Select(r => new LetterFact(r.Id, rnd.Next(2) == 0 ? "شكر وزاري" : "شكر محافظ", r.Letters, 0, 1, 0)).ToList(),
            Penalties = rows.Where(r => r.Penalties > 0).Select(r => new PenaltyFact(r.Id, rnd.Next(3) switch { 0 => "لفت نظر", 1 => "إنذار", _ => "قطع راتب" }, today.AddDays(-rnd.Next(10, 900)))).ToList(),
            LoadedAt = DateTime.Now
        });

        // HOME_PREVIEW_KURDISH=1 renders in Sorani; HOME_PREVIEW_FILTER="Formation=x|y;Gender=z" starts filtered.
        var kurdish = Environment.GetEnvironmentVariable("HOME_PREVIEW_KURDISH") == "1";
        var viewState = new HomeDashboardViewState();
        foreach (var part in (Environment.GetEnvironmentVariable("HOME_PREVIEW_FILTER") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, values) = (part.Split('=')[0], part.Split('=')[1]);
            viewState.Set(name, values.Split('|').ToHashSet());
        }

        var lang = new Mock<ILanguageService>();
        lang.SetupGet(l => l.IsKurdish).Returns(kurdish);
        lang.SetupGet(l => l.IsRtl).Returns(true);
        lang.Setup(l => l.T(It.IsAny<string>())).Returns<string>(k => k);
        lang.Setup(l => l.T(It.IsAny<string>(), It.IsAny<string>())).Returns<string, string>((_, f) => f);
        var kpi = new Mock<IFormationKpiService>();
        kpi.Setup(k => k.GetUserFormationAccessAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((true, (int?)null, new List<FormationOptionDto>()));

        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache>(cache);
        services.AddSingleton(sp => new WorkforceSnapshotService(Mock.Of<IServiceScopeFactory>(), cache));
        services.AddSingleton(viewState);
        services.AddSingleton<IJSRuntime>(Mock.Of<IJSRuntime>());
        services.AddSingleton<NavigationManager, PreviewNavigation>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(lang.Object);
        services.AddSingleton<AuthenticationStateProvider, AdminAuth>();
        services.AddSingleton(kpi.Object);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new PreviewRenderer(provider);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = renderer.BeginRenderingComponent(typeof(Home), ParameterView.Empty);
            await root.QuiescenceTask;
            return root.ToHtmlString();
        });
    }

    private static List<WorkforceRow> SyntheticRows(int count)
    {
        var rnd = new Random(7);
        string[] formations = ["دائرة العمليات المصرفية", "دائرة تقنية المعلومات", "دائرة الموارد البشرية", "دائرة الإحصاء والأبحاث", "دائرة المحاسبة",
            "دائرة الإصدار والخزائن", "دائرة مراقبة الصيرفة", "دائرة الشؤون القانونية", "فرع البصرة", "فرع أربيل", "فرع الموصل", "دائرة الاستثمار", "مكتب المحافظ", "دائرة التدقيق الداخلي"];
        string[] units = ["قسم الحسابات الجارية", "قسم الشبكات", "قسم التوظيف", "قسم المدفوعات", "قسم الأرشيف", "شعبة الخدمات", "قسم البرمجيات", "قسم الرواتب"];
        string[] statuses = ["مستمر بالخدمة", "مستمر بالخدمة", "مستمر بالخدمة", "مستمر بالخدمة", "مستمر بالخدمة", "منسّب", "مجاز طويلاً", "موقوف"];
        string[] grades = ["الدرجة الأولى", "الدرجة الثانية", "الدرجة الثالثة", "الدرجة الرابعة", "الدرجة الخامسة", "الدرجة السادسة", "الدرجة السابعة", "الدرجة الثامنة", "الخاصة ب"];
        string[] gradeCodes = ["1", "2", "3", "4", "5", "6", "7", "8", "الخاصة ب"];
        string[] education = ["بكالوريوس", "بكالوريوس", "بكالوريوس", "دبلوم", "إعدادية", "ماجستير", "دكتوراه", "دبلوم عالي", "متوسطة"];
        string[] specs = ["محاسبة", "إدارة أعمال", "علوم حاسبات", "اقتصاد", "قانون", "هندسة برمجيات", "إحصاء", "مالية ومصرفية"];
        string[] titles = ["محاسب", "مبرمج", "معاون مدير", "باحث اقتصادي", "مدقق", "ملاحظ", "مهندس", "كاتب", "مشاور قانوني", "أمين صندوق"];
        string[] positions = ["مدير قسم", "مدير شعبة", "معاون مدير عام", "مسؤول وحدة"];
        string[] religions = ["مسلم", "مسلم", "مسلم", "مسلم", "مسلم", "مسلم", "مسلم", "مسلم", "مسيحي", "صابئي"];
        string[] nationalities = ["عراقية", "عراقية", "عراقية", "عراقية", "عراقية", "عراقية", "عراقية", "عراقية", "عراقية", "أردنية"];
        string[] marital = ["متزوج", "متزوج", "أعزب", "متزوج", "أعزب", "مطلق", "أرمل"];
        string[] governorates = ["بغداد", "بغداد", "بغداد", "البصرة", "أربيل", "نينوى", "النجف", "بابل"];
        string[] first = ["الإدارة العامة", "فرع البصرة", "فرع أربيل", "دائرة الإصدار"];
        string[] names = ["أحمد", "علي", "حسين", "زينب", "مريم", "محمد", "سارة", "عمر", "نور", "حيدر", "فاطمة", "يوسف", "رقية", "كرار", "هدى"];
        var today = DateOnly.FromDateTime(DateTime.Today);

        T Pick<T>(T[] values) => values[rnd.Next(values.Length)];
        return Enumerable.Range(1, count).Select(i =>
        {
            var age = 23 + (int)(Math.Pow(rnd.NextDouble(), 0.9) * 38);
            var service = Math.Max(0, Math.Min(age - 21, (int)(rnd.NextDouble() * (age - 20))));
            var g = rnd.Next(grades.Length);
            var female = rnd.NextDouble() < .38;
            return new WorkforceRow
            {
                Id = i,
                EmployeeId = (100000 + i).ToString(),
                Name = $"{Pick(names)} {Pick(names)} {Pick(names)}",
                Formation = formations[Math.Min(formations.Length - 1, (int)(Math.Pow(rnd.NextDouble(), 1.8) * formations.Length))],
                Unit = Pick(units),
                JobTitle = Pick(titles),
                Position = rnd.NextDouble() < .09 ? Pick(positions) : null,
                Grade = grades[g], GradeCode = gradeCodes[g], StageNumber = rnd.Next(1, 12), Stage = "مرحلة",
                Education = Pick(education),
                Specialization = Pick(specs),
                Gender = female ? WorkforceStats.Female : WorkforceStats.Male,
                Religion = Pick(religions),
                Nationality = Pick(nationalities),
                Marital = Pick(marital),
                Status = Pick(statuses),
                FirstDepartment = rnd.NextDouble() < .7 ? Pick(first) : null,
                BirthDate = today.AddYears(-age).AddDays(-rnd.Next(365)),
                HireDate = today.AddYears(-service).AddDays(-rnd.Next(365)),
                LastJobTitleDate = today.AddYears(-rnd.Next(0, 12)).AddDays(-rnd.Next(365)),
                LeaveBalance = rnd.Next(0, 240),
                HasPhoto = rnd.NextDouble() < .82,
                HasPhone = rnd.NextDouble() < .91,
                HasEmail = rnd.NextDouble() < .66,
                HasAccount = rnd.NextDouble() < .58,
                HasNationalCard = rnd.NextDouble() < .74,
                HasPassport = rnd.NextDouble() < .41,
                HasMartyr = rnd.NextDouble() < .03,
                Governorate = rnd.NextDouble() < .8 ? Pick(governorates) : null,
                Vehicles = rnd.NextDouble() < .2 ? rnd.Next(1, 3) : 0,
                Courses = rnd.NextDouble() < .45 ? rnd.Next(1, 6) : 0,
                Letters = rnd.NextDouble() < .3 ? rnd.Next(1, 9) : 0,
                Penalties = rnd.NextDouble() < .05 ? rnd.Next(1, 3) : 0
            };
        }).ToList();
    }

    // Home declares InteractiveServer; a static preview renders it in place instead of refusing.
    private sealed class PreviewRenderer(IServiceProvider services) : StaticHtmlRenderer(services, NullLoggerFactory.Instance)
    {
        protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId,
            IComponentActivator componentActivator, IComponentRenderMode renderMode) =>
            componentActivator.CreateInstance(componentType);
    }

    private sealed class PreviewNavigation : NavigationManager
    {
        public PreviewNavigation() => Initialize("http://localhost/", "http://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class AdminAuth : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "preview"))));
    }
}
