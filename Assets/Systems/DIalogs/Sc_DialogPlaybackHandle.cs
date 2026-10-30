using System;
using System.Threading.Tasks;

public sealed class Sc_DialogPlaybackHandle
{
    private readonly TaskCompletionSource<Sc_DialogPlaybackResult> _completion
        = new TaskCompletionSource<Sc_DialogPlaybackResult>();

    private Func<bool> _cancelAction;
    private Func<bool> _skipAction;

    public int RequestId { get; }
    public string Source { get; }
    public E_DialogPlaybackStatus Status { get; private set; }
    public bool IsCompleted => _completion.Task.IsCompleted;
    public Sc_DialogPlaybackResult Result { get; private set; }
    public Task<Sc_DialogPlaybackResult> Completion => _completion.Task;

    public event Action<Sc_DialogPlaybackResult> Completed;

    internal Sc_DialogPlaybackHandle(
        int requestId,
        string source,
        Func<bool> cancelAction,
        Func<bool> skipAction)
    {
        RequestId = requestId;
        Source = source;
        Status = E_DialogPlaybackStatus.Pending;
        _cancelAction = cancelAction;
        _skipAction = skipAction;
    }

    public bool Cancel()
    {
        return !IsCompleted && _cancelAction != null && _cancelAction();
    }

    public bool SkipCurrentDialog()
    {
        return !IsCompleted && _skipAction != null && _skipAction();
    }

    internal void MarkPlaying()
    {
        if (!IsCompleted)
            Status = E_DialogPlaybackStatus.Playing;
    }

    internal void Complete(E_DialogPlaybackStatus status, string reason)
    {
        if (IsCompleted)
            return;

        Status = status;
        Result = new Sc_DialogPlaybackResult(RequestId, Source, status, reason);
        _completion.TrySetResult(Result);
        Completed?.Invoke(Result);

        _cancelAction = null;
        _skipAction = null;
    }
}
