using System;

public readonly struct Sc_DialogCompletionContract
{
    public E_DialogCompletionEvent Event { get; }
    public string Key { get; }

    private Sc_DialogCompletionContract(
        E_DialogCompletionEvent completionEvent,
        string key)
    {
        Event = completionEvent;
        Key = key;
    }

    public static Sc_DialogCompletionContract From(E_DialogCompletionEvent completionEvent)
    {
        if (!TryCreate(completionEvent, out Sc_DialogCompletionContract contract))
            throw new ArgumentOutOfRangeException(nameof(completionEvent));

        return contract;
    }

    public static bool TryCreate(
        E_DialogCompletionEvent completionEvent,
        out Sc_DialogCompletionContract contract)
    {
        string key;

        switch (completionEvent)
        {
            case E_DialogCompletionEvent.PlayerUsedQ:
                key = Sc_DialogEvents.PlayerUsedQ;
                break;
            case E_DialogCompletionEvent.PlayerUsedE:
                key = Sc_DialogEvents.PlayerUsedE;
                break;
            case E_DialogCompletionEvent.PlayerUsedR:
                key = Sc_DialogEvents.PlayerUsedR;
                break;
            case E_DialogCompletionEvent.PlayerUsedPrimary:
                key = Sc_DialogEvents.PlayerUsedPrimary;
                break;
            case E_DialogCompletionEvent.PlayerUsedSecondary:
                key = Sc_DialogEvents.PlayerUsedSecondary;
                break;
            case E_DialogCompletionEvent.EnemyKilled:
                key = Sc_DialogEvents.EnemyKilled;
                break;
            case E_DialogCompletionEvent.DummyHit:
                key = Sc_DialogEvents.DummyHit;
                break;
            case E_DialogCompletionEvent.PlayerJumped:
                key = Sc_DialogEvents.PlayerJumped;
                break;
            case E_DialogCompletionEvent.PlayerDashed:
                key = Sc_DialogEvents.PlayerDashed;
                break;
            case E_DialogCompletionEvent.WaveCleared:
                key = Sc_DialogEvents.WaveCleared;
                break;
            case E_DialogCompletionEvent.RewardChosen:
                key = Sc_DialogEvents.RewardChosen;
                break;
            default:
                contract = default;
                return false;
        }

        contract = new Sc_DialogCompletionContract(completionEvent, key);
        return true;
    }

    public static bool TryCreate(
        string key,
        out Sc_DialogCompletionContract contract)
    {
        if (string.Equals(key, Sc_DialogEvents.PlayerUsedQ, StringComparison.Ordinal))
            return TryCreate(E_DialogCompletionEvent.PlayerUsedQ, out contract);
        if (string.Equals(key, Sc_DialogEvents.PlayerUsedE, StringComparison.Ordinal))
            return TryCreate(E_DialogCompletionEvent.PlayerUsedE, out contract);
        if (string.Equals(key, Sc_DialogEvents.PlayerUsedR, StringComparison.Ordinal))
            return TryCreate(E_DialogCompletionEvent.PlayerUsedR, out contract);
        if (string.Equals(key, Sc_DialogEvents.PlayerUsedPrimary, StringComparison.Ordinal))
            return TryCreate(E_DialogCompletionEvent.PlayerUsedPrimary, out contract);
        if (string.Equals(key, Sc_DialogEvents.PlayerUsedSecondary, StringComparison.Ordinal))
            return TryCreate(E_DialogCompletionEvent.PlayerUsedSecondary, out contract);
        if (string.Equals(key, Sc_DialogEvents.EnemyKilled, StringComparison.Ordinal))
            return TryCreate(E_DialogCompletionEvent.EnemyKilled, out contract);
        if (string.Equals(key, Sc_DialogEvents.DummyHit, StringComparison.Ordinal))
            return TryCreate(E_DialogCompletionEvent.DummyHit, out contract);
        if (string.Equals(key, Sc_DialogEvents.PlayerJumped, StringComparison.Ordinal))
            return TryCreate(E_DialogCompletionEvent.PlayerJumped, out contract);
        if (string.Equals(key, Sc_DialogEvents.PlayerDashed, StringComparison.Ordinal))
            return TryCreate(E_DialogCompletionEvent.PlayerDashed, out contract);
        if (string.Equals(key, Sc_DialogEvents.WaveCleared, StringComparison.Ordinal))
            return TryCreate(E_DialogCompletionEvent.WaveCleared, out contract);
        if (string.Equals(key, Sc_DialogEvents.RewardChosen, StringComparison.Ordinal))
            return TryCreate(E_DialogCompletionEvent.RewardChosen, out contract);

        contract = default;
        return false;
    }
}
