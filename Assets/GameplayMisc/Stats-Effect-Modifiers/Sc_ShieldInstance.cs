public class Sc_ShieldInstance
{
    public float MaxValue;
    public float CurrentValue;
    public float Duration;
    public float TimeRemaining;

    public Sc_ShieldInstance(float value, float duration)
    {
        MaxValue = value;
        CurrentValue = value;
        Duration = duration;
        TimeRemaining = duration;
    }
}