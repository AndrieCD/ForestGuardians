using System;

/// <summary>
/// Stable runtime identity for one applied modifier instance.
/// Handles are not serialized and are only valid for the runtime session in
/// which their modifier was created.
/// </summary>
public readonly struct Sc_ModifierHandle : IEquatable<Sc_ModifierHandle>
{
    public readonly long RuntimeId;

    public bool IsValid => RuntimeId > 0;

    internal Sc_ModifierHandle(long runtimeId)
    {
        RuntimeId = runtimeId;
    }

    public bool Equals(Sc_ModifierHandle other) => RuntimeId == other.RuntimeId;

    public override bool Equals(object obj)
    {
        return obj is Sc_ModifierHandle other && Equals(other);
    }

    public override int GetHashCode() => RuntimeId.GetHashCode();

    public static bool operator ==(Sc_ModifierHandle left, Sc_ModifierHandle right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Sc_ModifierHandle left, Sc_ModifierHandle right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return IsValid ? $"ModifierHandle({RuntimeId})" : "InvalidModifierHandle";
    }
}
