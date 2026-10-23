namespace API.HR.Node.Services.Localization;

public interface ILanguageService
{
    string CurrentLanguage { get; }
    bool IsKurdish { get; }
    bool IsArabic { get; }
    bool IsEnglish { get; }
    bool IsRtl { get; }
    string Direction { get; }
    
    event Action? OnLanguageChanged;

    void SetLanguage(string languageCode);
    void ToggleLanguage();
    string T(string key);
    string T(string key, string fallback);
}
