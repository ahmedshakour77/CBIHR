# دمج نظام التصميم API.HR.Node في المشروع

الحزمة فيها كل شي تحتاجه لتطبيق نظام التصميم على مشروع Blazor (ASP.NET Core):

```
wwwroot/css/hr-tokens.css      كل التوكنات: الألوان بالثيمين، الـ 11 باليت، المسافات، الزوايا، الظلال، الحركة، الطبقات
wwwroot/css/hr-components.css  الأزرار، الشارات، الحقول، التبويبات، الملف التعريفي، الجدول، التفضيلات، التنقل بأنواعه الثلاثة
wwwroot/js/hr-prefs.js         تفضيلات المظهر (بديل theme.js ومتوافق وياه)
wwwroot/img/cbi-logo.png       الشعار
design/                        tokens.json (المصدر)، توثيق كل مكوّن، ومعاينات HTML تفتحها بالمتصفح مباشرة
```

## 1. انسخ الملفات

انسخ مجلد `wwwroot` فوق `wwwroot` مالت المشروع، وحط مجلد `design` بجذر المشروع (مو داخل wwwroot) حتى يبقى مرجعاً للفريق.

## 2. اربطها في `App.razor` (أو `_Host.cshtml` / `_Layout.cshtml`)

داخل `<head>`، وبهذا الترتيب بالضبط:

```html
<html lang="ar" dir="rtl">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />

    <!-- أولاً: يطبّق التفضيلات قبل أول رسم حتى ما تومض الصفحة باللون الافتراضي -->
    <script src="js/hr-prefs.js"></script>

    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link rel="stylesheet" href="css/hr-tokens.css" />
    <link rel="stylesheet" href="css/hr-components.css" />
    <link rel="stylesheet" href="css/hr-ui.css" />          <!-- ملفك الحالي، إن بقي -->
    ...
</head>
<body class="hr-root">
```

> **ترتيب `hr-ui.css` و `hr-components.css`.** عشرون اسم صنف أساسي موجود بالملفين
> (`hr-btn`, `hr-card`, `hr-input`, `hr-table`, `hr-badge`, `hr-avatar`, `hr-tabs`,
> `hr-pager`, `hr-rail`, `hr-main`, `hr-scrim`, `hr-check`, `hr-crumbs`, `hr-dot`,
> `hr-num`, `hr-skip`, `hr-sr`, `hr-swatch`, `hr-tab`, `hr-timeline`).
> إذا ملفك الحالي (`hr-ui.css`) لسه هو اللي تبني عليه الصفحات — وهو الحال بمشروع
> API.HR.Node: ٣٩٢ استعمال لـ `.hr-card` و١٤٤ لـ `.hr-btn-sm` مقابل ١٤ استعمال من
> النظام الجديد — خلّيه **آخر** ملف. لو حطّيته قبل `hr-components.css` راح تفوز قواعد
> `.hr-btn` و`.hr-card` و`.hr-input` الأساسية مال الملف الجديد على قواعدك، وكل زر صغير
> يرجع بالحجم الكامل بدون أي خطأ ظاهر.
> بعد ما تنقل مكوّناً لصيغة BEM، احذف قاعدته من `hr-ui.css` بنفس التغيير بدل الاعتماد
> على الترتيب.

- `viewport-fit=cover` ضروري حتى يحترم الشريط العائم الحافة السفلية بالآيفون.
- `hr-prefs.js` يحمّل خط الموظف المختار فقط من Google Fonts، فلا تضيف رابط الخطوط يدوياً.
- احذف `<script src="js/theme.js">`. الملف الجديد يعرّف `window.hrTheme.toggle()` و `get()` بنفس الأسماء، فـ `CommandPalette.razor` يبقى يشتغل بدون تعديل.

## 3. نظّف `hr-ui.css` الحالي

أسماء التوكنات بالحزمة هي نفس أسماء ملفك (`--b-25`…`--b-1000`، `--text`، `--text-2`، `--text-muted`، `--line`، `--line-control`، `--s-*`، `--r-*`، `--shadow-*`، `--t-*`، `--ease`). لذلك:

1. احذف من `hr-ui.css` كتل تعريف المتغيرات (`:root { --… }` و `[data-theme="dark"] { --… }`). صار مصدرها `hr-tokens.css`.
   **هاي الخطوة أهم شي بالدمج كله.** إذا انحذف سطر الربط بس وبقت الكتل، راح يفوز
   `hr-ui.css` لأنه يجي بعدين بالترتيب، ويصير: تعديل `tokens.json` ما يغيّر ولا شي
   بالشاشة، والثيم الغامق يبقى بنفسجياً مكتوباً باليد فما يوصله تبديل اللون.
   بمشروع API.HR.Node هاي الخطوة صارت بتاريخ ٢٠٢٦-٠٩-٢٧؛ ٦١ توكن كانت مكرّرة بالملفين.
2. خلي قواعد المكوّنات بـ `hr-ui.css` مثل ما هي، وانقلها تدريجياً إلى أصناف `hr-*`.
3. ابحث بالمشروع عن أي لون مكتوب مباشرة (`#`، `rgb(`) وبدّله بالتوكن المقابل. أي لون مكتوب باليد ما راح يتغير ويا الثيم أو اللون المختار.
4. تأكد إن كل اسم توكن مستعمل بالـ `.razor` موجود فعلاً بالـ CSS. `var(--اسم-غير-معرّف)`
   ما يطلع أي خطأ: القاعدة كلها تنلغي بصمت، فالخلفية تصير شفافة والحدود تختفي.

**التوليد والتحقق.** `hr-tokens.css` ملف مولّد؛ لا تعدّله:

```bash
node design/build-tokens.cjs          # يولّد hr-tokens.css من tokens.json
node design/build-tokens.cjs --check  # يفشل إذا الملف قديم — حطه بالـ CI
node design/check-tokens.cjs          # يتحقق من التباين بكل الباليتات والثيمين
```

توكنات جديدة ما كانت بملفك: `ground`، `surface`، `surface-2`، `accent`، `accent-hover`، `on-accent`، `accent-text`، `accent-soft`، `focus`، وتوكنات `control-h*` و `nav-w*` و `topbar-h*` و `dock-h` و `z-*`.

## 4. احفظ التفضيلات بحساب الموظف

التفضيلات تنحفظ محلياً تلقائياً، بس لازم تنحفظ بالسيرفر حتى تتبع الموظف على كل أجهزته.

**النموذج والـ API:**

```csharp
public sealed record UserPreferencesDto(
    string Mode = "system",        // light | dark | system
    string Brand = "purple",       // purple | indigo | blue | teal | green | olive | gold | copper | crimson | rose | slate
    string Font = "plex",          // plex | cairo | tajawal | noto | alexandria
    string FontSize = "md",        // sm | md | lg | xl
    string Density = "comfortable" // compact | comfortable | spacious
);

// Minimal API
app.MapGet("/api/me/preferences", async (ClaimsPrincipal user, IPreferencesStore store) =>
    Results.Ok(await store.GetAsync(user.GetEmployeeId()) ?? new UserPreferencesDto()))
   .RequireAuthorization();

app.MapPut("/api/me/preferences", async (ClaimsPrincipal user, UserPreferencesDto dto, IPreferencesStore store) =>
{
    await store.SaveAsync(user.GetEmployeeId(), dto);   // تحقق من القيم مقابل القوائم أعلاه قبل الحفظ
    return Results.NoContent();
}).RequireAuthorization();
```

**خدمة Blazor تربط JS بالسيرفر:**

```csharp
public sealed class PreferencesService(IJSRuntime js, HttpClient http) : IAsyncDisposable
{
    private DotNetObjectReference<PreferencesService>? _ref;

    // استدعها مرة بعد تسجيل الدخول (OnAfterRenderAsync firstRender بالـ MainLayout)
    public async Task InitAsync()
    {
        var server = await http.GetFromJsonAsync<UserPreferencesDto>("api/me/preferences");
        await js.InvokeVoidAsync("hrPrefs.load", server);            // نسخة السيرفر تغلب النسخة المحلية
        _ref = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("hrPrefs.bindDotNet", _ref);     // أي تغيير لاحق ينحفظ بالسيرفر
    }

    public ValueTask SetAsync(object partial) => js.InvokeVoidAsync("hrPrefs.set", partial);

    [JSInvokable] public Task Save(UserPreferencesDto p) => http.PutAsJsonAsync("api/me/preferences", p);

    public ValueTask DisposeAsync() { _ref?.Dispose(); return ValueTask.CompletedTask; }
}
```

صفحة الإعدادات تستدعي `SetAsync(new { brand = "teal" })` عند كل تغيير، بدون زر "تطبيق".

## 5. المكوّنات في Blazor

المكوّنات الموجودة (`DsButton`، `DsStatusBadge`…) تبقى، بس تكتب أصناف `hr-*`:

```razor
@* DsButton.razor *@
<button type="@Type" class="hr-btn hr-btn--@Variant @(Small ? "hr-btn--sm" : null) @(IconOnly ? "hr-btn--icon" : null)"
        disabled="@Disabled" aria-label="@AriaLabel" @onclick="OnClick">
    @ChildContent
</button>
@code {
    [Parameter] public string Variant { get; set; } = "secondary"; // primary | secondary | ghost | danger
    [Parameter] public bool Small { get; set; }
    [Parameter] public bool IconOnly { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string? AriaLabel { get; set; }
    [Parameter] public string Type { get; set; } = "button";
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

```razor
@* DsStatusBadge.razor — الحالة دائماً نقطة + كلمة *@
<span class="hr-badge @(Tone is null ? null : $"hr-badge--{Tone}")">@Label</span>
@code {
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter] public string? Tone { get; set; } // ok | warn | danger | null للمحايد
}
```

**رابط التنقل:** `NavLink` بـ Blazor يضيف صنف `active` بس، والنظام يعتمد `aria-current="page"`. سوّي مكوّناً صغيراً:

```razor
@* HrNavLink.razor *@
@inject NavigationManager Nav
@implements IDisposable
<a class="hr-nav__item" href="@Href" data-tip="@Label" aria-current="@(IsCurrent ? "page" : null)">
    @Icon<span class="hr-nav__label">@Label</span>
    @if (Count > 0) { <span class="hr-nav__count" aria-label="@($"{Count} جديد")">@Count</span> }
</a>
@code {
    [Parameter, EditorRequired] public string Href { get; set; } = "";
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter] public RenderFragment? Icon { get; set; }
    [Parameter] public int Count { get; set; }
    [Parameter] public bool Exact { get; set; }
    private bool IsCurrent
    {
        get
        {
            var path = "/" + Nav.ToBaseRelativePath(Nav.Uri).Split('?', '#')[0];
            return Exact ? path == Href : path.StartsWith(Href, StringComparison.OrdinalIgnoreCase);
        }
    }
    protected override void OnInitialized() => Nav.LocationChanged += Changed;
    private void Changed(object? s, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);
    public void Dispose() => Nav.LocationChanged -= Changed;
}
```

هيكل HTML لكل مكوّن (أي عنصر داخل أي عنصر، وأي صنف) موجود كما هو بملفات `design/previews/*.html`: افتح المعاينة، وانسخ الهيكل إلى ملف `.razor`، وبدّل البيانات الثابتة بـ `@` و `foreach`. قواعد كل مكوّن ومواصفاته بملف `design/<المكوّن>.md` المقابل.

## 6. تحقق قبل الدمج

- افتح `design/previews/SideNav.html` بالمتصفح وجرّب الأزرار الثلاثة (سطح المكتب، لوحي، هاتف).
- بالتطبيق: غيّر اللون والخط والحجم من صفحة الإعدادات، وحدّث الصفحة. لازم ما تومض باللون البنفسجي قبل ما يطلع اختيارك.
- افحص كل شاشة على عرض 390 و 768 و 1280px، بالوضعين، وبحجم الخط `xl`.
- شغّل Lighthouse أو axe على صفحة الملف التعريفي وصفحة الجدول: لازم ما يطلع أي خطأ تباين.

## 7. تحديث النظام لاحقاً

المصدر هو `design/tokens.json` ونظام التصميم على Claude. أي تعديل يصير هناك، بعدها تنزّل حزمة جديدة وتبدّل `hr-tokens.css` و `hr-components.css` بس. لا تعدّل هذين الملفين يدوياً بالمشروع، حتى ما يضيع تعديلك بالتحديث الجاي.
