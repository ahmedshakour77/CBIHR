using System;

namespace API.HR.Node.Services.Feedback;

public interface IFeedbackModalState
{
    bool IsOpen { get; }
    event Action? OnChanged;
    void Open();
    void Close();
}

public class FeedbackModalState : IFeedbackModalState
{
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    public event Action? OnChanged;

    public void Open()
    {
        _isOpen = true;
        OnChanged?.Invoke();
    }

    public void Close()
    {
        _isOpen = false;
        OnChanged?.Invoke();
    }
}
