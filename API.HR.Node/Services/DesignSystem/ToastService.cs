namespace API.HR.Node.Services.DesignSystem;

public enum ToastType
{
    Success,
    Info,
    Warning,
    Danger
}

public sealed record ToastItem(
    string Id,
    string Message,
    string? Title = null,
    ToastType Type = ToastType.Info,
    int DurationMs = 4000,
    string? ActionLabel = null,
    Action? OnAction = null
);

public interface IToastService
{
    event Action? OnToastsChanged;
    IReadOnlyList<ToastItem> Toasts { get; }
    void Show(string message, string? title = null, ToastType type = ToastType.Info, int durationMs = 4000, string? actionLabel = null, Action? onAction = null);
    void Success(string message, string? title = null, int durationMs = 4000, string? actionLabel = null, Action? onAction = null);
    void Warning(string message, string? title = null, int durationMs = 5000);
    void Danger(string message, string? title = null, int durationMs = 6000);
    void Info(string message, string? title = null, int durationMs = 4000);
    void Dismiss(string id);
}

public sealed class ToastService : IToastService
{
    private readonly List<ToastItem> _toasts = new();
    private readonly object _lock = new();

    public event Action? OnToastsChanged;

    public IReadOnlyList<ToastItem> Toasts
    {
        get
        {
            lock (_lock)
            {
                return _toasts.ToList();
            }
        }
    }

    public void Show(string message, string? title = null, ToastType type = ToastType.Info, int durationMs = 4000, string? actionLabel = null, Action? onAction = null)
    {
        var item = new ToastItem(Guid.NewGuid().ToString("N"), message, title, type, durationMs, actionLabel, onAction);
        lock (_lock)
        {
            _toasts.Add(item);
        }
        OnToastsChanged?.Invoke();

        if (durationMs > 0)
        {
            _ = AutoDismissAsync(item.Id, durationMs);
        }
    }

    public void Success(string message, string? title = null, int durationMs = 4000, string? actionLabel = null, Action? onAction = null) =>
        Show(message, title, ToastType.Success, durationMs, actionLabel, onAction);

    public void Warning(string message, string? title = null, int durationMs = 5000) =>
        Show(message, title, ToastType.Warning, durationMs);

    public void Danger(string message, string? title = null, int durationMs = 6000) =>
        Show(message, title, ToastType.Danger, durationMs);

    public void Info(string message, string? title = null, int durationMs = 4000) =>
        Show(message, title, ToastType.Info, durationMs);

    public void Dismiss(string id)
    {
        lock (_lock)
        {
            _toasts.RemoveAll(t => t.Id == id);
        }
        OnToastsChanged?.Invoke();
    }

    private async Task AutoDismissAsync(string id, int delayMs)
    {
        await Task.Delay(delayMs);
        Dismiss(id);
    }
}
