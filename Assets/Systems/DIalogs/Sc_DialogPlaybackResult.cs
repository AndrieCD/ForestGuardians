public readonly struct Sc_DialogPlaybackResult
{
    public int RequestId { get; }
    public string Source { get; }
    public E_DialogPlaybackStatus Status { get; }
    public string Reason { get; }

    public bool Succeeded => Status == E_DialogPlaybackStatus.Completed;

    public Sc_DialogPlaybackResult(
        int requestId,
        string source,
        E_DialogPlaybackStatus status,
        string reason)
    {
        RequestId = requestId;
        Source = source;
        Status = status;
        Reason = reason;
    }
}
