// I_Damageable.cs
public interface I_Damageable
{
    void TakeDamage(float amount, DamageType type = DamageType.Physical);
    bool IsDead { get; }
}

public enum DamageType { Physical, Ability, DoT, Environmental, True }
