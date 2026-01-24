// I_Damageable.cs
public interface I_Damageable
{
    void TakeDamage(float amount);
    bool IsDead { get; }
}