using API.HR.Node.Services.DesignSystem;
using API.HR.Node.Services.Localization;
using Xunit;

namespace API.HR.Node.Tests;

public class DesignSystemTests
{
    [Fact]
    public void ToastService_ShouldAddAndDismissToast()
    {
        var service = new ToastService();
        var eventFired = false;
        service.OnToastsChanged += () => eventFired = true;

        service.Success("Test success message", "Title");

        Assert.True(eventFired);
        Assert.Single(service.Toasts);
        var toast = service.Toasts[0];
        Assert.Equal("Test success message", toast.Message);
        Assert.Equal("Title", toast.Title);
        Assert.Equal(ToastType.Success, toast.Type);

        service.Dismiss(toast.Id);
        Assert.Empty(service.Toasts);
    }

    [Fact]
    public void UserPreferenceService_ShouldUpdateAndNotify()
    {
        var service = new UserPreferenceService();
        var notified = false;
        service.OnPreferenceChanged += () => notified = true;

        service.SetDensity(GridDensity.Compact);
        Assert.True(notified);
        Assert.Equal(GridDensity.Compact, service.Current.Density);

        notified = false;
        service.SetAppearance(AppearanceMode.Dark);
        Assert.True(notified);
        Assert.Equal(AppearanceMode.Dark, service.Current.Appearance);

        service.SetReducedMotion(true);
        Assert.True(service.Current.ReducedMotion);
    }

    [Fact]
    public void LanguageService_ShouldSupportTriLingualAndDirection()
    {
        var lang = new LanguageService();

        // Default is Arabic
        Assert.Equal(LanguageService.Arabic, lang.CurrentLanguage);
        Assert.True(lang.IsArabic);
        Assert.False(lang.IsEnglish);
        Assert.True(lang.IsRtl);
        Assert.Equal("rtl", lang.Direction);
        Assert.Equal("الموظف ٣٦٠°   ", lang.T("app.title"));

        // Switch to Kurdish
        lang.SetLanguage(LanguageService.Kurdish);
        Assert.True(lang.IsKurdish);
        Assert.True(lang.IsRtl);
        Assert.Equal("rtl", lang.Direction);
        Assert.Equal("سیستەمی سەرچاوە مرۆییەکان", lang.T("app.title"));

        // Switch to English
        lang.SetLanguage(LanguageService.English);
        if (LanguageService.EnglishEnabled)
        {
            Assert.True(lang.IsEnglish);
            Assert.False(lang.IsRtl);
            Assert.Equal("ltr", lang.Direction);
            Assert.Equal("HR Enterprise System", lang.T("app.title"));

            // Toggle Language cycle: en -> ar
            lang.ToggleLanguage();
            Assert.True(lang.IsArabic);
        }
        else
        {
            // English is withdrawn until the UI is translated: a request for
            // it resolves to Arabic, never to an LTR layout around Arabic text.
            Assert.False(lang.IsEnglish);
            Assert.True(lang.IsArabic);
            Assert.True(lang.IsRtl);
            Assert.Equal("rtl", lang.Direction);
        }
    }
}
