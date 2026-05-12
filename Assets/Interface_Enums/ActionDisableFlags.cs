[System.Flags]
public enum ActionDisableFlags
{
    None = 0,

    // Movement Layer
    Movement = 1 << 0,
    Rotation = 1 << 1,
    Jump = 1 << 2,
    Dash = 1 << 3,

    // Combat Layer
    PrimaryAttack = 1 << 4,
    SecondaryAttack = 1 << 5,
    AbilityQ = 1 << 6,
    AbilityE = 1 << 7,
    AbilityR = 1 << 8,

    // Meta
    AllAbilities = AbilityQ | AbilityE | AbilityR,
    AllAttacks = PrimaryAttack | SecondaryAttack,

    // Presets
    Stun = Movement | Rotation | Jump | Dash | AllAttacks | AllAbilities,
    Root = Movement,
    Silence = AllAbilities,
    Disarm = AllAttacks
}