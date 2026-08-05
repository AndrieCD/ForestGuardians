public enum ModifierSource
{
    Ability,        // From an active ability (e.g. Royal Plumage buff)
    Augment,        // From an augment selection between waves
    Environmental,  // From a stage zone or hazard
    StageScaling,   // From stage difficulty scaling (Stage 2 and Stage 3 base stat multipliers)
    WaveScaling,    // From wave difficulty scaling on CuBots
    Almanac,        // From the Almanac's entry stat modifiers
    StatusEffect,   // Conditional debuffs (burn, slow, etc.)
    ForestFruit,    // Permanent stat buffs collected during the current stage run
    ForestBlessing  // Temporary combat power-ups collected during the current stage run
}
