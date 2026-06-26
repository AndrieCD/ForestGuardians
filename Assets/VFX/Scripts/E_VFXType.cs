// E_VFXType.cs
// VFX enum keys use explicit numeric ranges so new entries can be inserted
// without shifting existing ScriptableObject assignments.
//
// Ranges:
//   1000-1999 Generic combat
//   2000-2999 Rajah
//   3000-3999 Mari
//   4000-4999 CuBots
//   5000-5999 Status effects
//   6000-6999 Environment and world events
//   7000-7999 Footsteps and movement
//   8000-8999 UI and screen effects

public enum GenericCombatVFXType
{
    Hit_Guardian_Generic = 1000,
    Hit_CuBot_Generic = 1010,
    Hit_Projectile_Generic = 1020,
    CuBot_Death_Generic = 1030,
    Hit_Panoharra = 1040,
    Hit_Critical = 1050,
}

public enum RajahVFXType
{
    Rajah_Primary_Cast = 2000,
    Rajah_Primary_Hit = 2010,
    Rajah_Secondary_Cast = 2020,
    Rajah_Secondary_Hit = 2030,
    Rajah_Q_Cast = 2040,
    Rajah_Q_Hit = 2050,
    Rajah_Q_Shield = 2060,
    Rajah_E_Cast = 2070,
    Rajah_E_Hit = 2080,
    Rajah_R_Branch1_Cast = 2090,
    Rajah_R_Branch1_Hit = 2100,
    Rajah_R_Branch1_Final = 2110,
    Rajah_R_Branch2_Cast = 2120,
    Rajah_R_Branch2_Hit = 2130,
}

public enum MariVFXType
{
    Mari_Primary_Cast = 3000,
    Mari_Primary_Hit = 3010,
    Mari_Secondary_Cast = 3020,
    Mari_Secondary_Hit = 3030,
    Mari_Q_Cast = 3040,
    Mari_Q_Hit = 3050,
    Mari_E_Cast = 3060,
    Mari_E_Hit = 3070,
    Mari_R_Branch1_Cast = 3080,
    Mari_R_Branch1_Hit = 3090,
    Mari_R_Branch2_Cast = 3100,
    Mari_R_Branch2_Hit = 3110,
}

public enum CuBotVFXType
{
    Hit_CuBot_Generic = 1010,
    CuBot_Death_Generic = 1030,

    CuBot_Attack_Generic = 4000,
    CuBot_Aggro = 4010,
    CuBot_Boss_Death_Generic = 4020,

    CuBot_Chopper_Attack = 4100,
    CuBot_Chopper_Hit = 4110,
    CuBot_Hunter_Attack = 4120,
    CuBot_Hunter_Hit = 4130,
    CuBot_Minny_Attack = 4140,
    CuBot_Minny_Hit = 4150,
    CuBot_Bernie_Attack = 4160,
    CuBot_Bernie_Hit = 4170,
    CuBot_Sawyer_Attack = 4180,
    CuBot_Sawyer_Hit = 4190,
    CuBot_Trapper_Attack = 4200,
    CuBot_Trapper_Hit = 4210,
    CuBot_Drilly_Attack = 4220,
    CuBot_Drilly_Hit = 4230,
    CuBot_Shovy_Attack = 4240,
    CuBot_Shovy_Hit = 4250,
    CuBot_Toxion_Attack = 4260,
    CuBot_Toxion_Hit = 4270,
    CuBot_Luxion_Attack = 4280,
    CuBot_Luxion_Hit = 4290,
}

public enum StatusVFXType
{
    Status_Burn = 5000,
    Status_Poison = 5010,
    Status_Slow = 5020,
    Status_Stun = 5030,
    Status_Shield = 5040,
    Status_Root = 5050,
    Status_Silence = 5060,
}

public enum EnvironmentVFXType
{
    Wave_Start = 6000,
    CuBot_Spawn = 6010,
    CuBot_SpawnPillar = 6020,
    Guardian_Death = 6030,
    Guardian_Levelup = 6040,
    Panoharra_Death = 6050,
    Portal_Enter = 6060,
    Portal_Exit = 6070,
    Almanac_Collected = 6080,
    River_Splash = 6090,
    ScorchedEarth_Burst = 6100,
    Rafflesia_Portal_Idle = 6110,
}

public enum FootstepVFXType
{
    Footstep_Grass = 7000,
    Footstep_Water = 7010,
    Footstep_Generic = 7020,
    Footstep_Mud = 7030,
    Footstep_Stone = 7040,
    Footstep_Metal = 7050,
    Guardian_Jump = 7100,
    Guardian_Land = 7110,
}

public enum UIVFXType
{
    RewardCard_Shimmer = 8000,
    LevelUp_ScreenFlash = 8010,
    Defeat_Vignette = 8020,
    Victory_Confetti = 8030,
    Burn_Vignette = 8040,
    Poison_Vignette = 8050,
    Slow_Vignette = 8060,
    Stun_Vignette = 8070,
    Guardian_LowHP_Vignette = 8080,
}

public enum VFXType
{
    Hit_Guardian_Generic = GenericCombatVFXType.Hit_Guardian_Generic,
    Hit_CuBot_Generic = GenericCombatVFXType.Hit_CuBot_Generic,
    Hit_Projectile_Generic = GenericCombatVFXType.Hit_Projectile_Generic,
    CuBot_Death_Generic = GenericCombatVFXType.CuBot_Death_Generic,
    Hit_Panoharra = GenericCombatVFXType.Hit_Panoharra,
    Hit_Critical = GenericCombatVFXType.Hit_Critical,

    Rajah_Primary_Cast = RajahVFXType.Rajah_Primary_Cast,
    Rajah_Primary_Hit = RajahVFXType.Rajah_Primary_Hit,
    Rajah_Secondary_Cast = RajahVFXType.Rajah_Secondary_Cast,
    Rajah_Secondary_Hit = RajahVFXType.Rajah_Secondary_Hit,
    Rajah_Q_Cast = RajahVFXType.Rajah_Q_Cast,
    Rajah_Q_Hit = RajahVFXType.Rajah_Q_Hit,
    Rajah_Q_Shield = RajahVFXType.Rajah_Q_Shield,
    Rajah_E_Cast = RajahVFXType.Rajah_E_Cast,
    Rajah_E_Hit = RajahVFXType.Rajah_E_Hit,
    Rajah_R_Branch1_Cast = RajahVFXType.Rajah_R_Branch1_Cast,
    Rajah_R_Branch1_Hit = RajahVFXType.Rajah_R_Branch1_Hit,
    Rajah_R_Branch1_Final = RajahVFXType.Rajah_R_Branch1_Final,
    Rajah_R_Branch2_Cast = RajahVFXType.Rajah_R_Branch2_Cast,
    Rajah_R_Branch2_Hit = RajahVFXType.Rajah_R_Branch2_Hit,

    Mari_Primary_Cast = MariVFXType.Mari_Primary_Cast,
    Mari_Primary_Hit = MariVFXType.Mari_Primary_Hit,
    Mari_Secondary_Cast = MariVFXType.Mari_Secondary_Cast,
    Mari_Secondary_Hit = MariVFXType.Mari_Secondary_Hit,
    Mari_Q_Cast = MariVFXType.Mari_Q_Cast,
    Mari_Q_Hit = MariVFXType.Mari_Q_Hit,
    Mari_E_Cast = MariVFXType.Mari_E_Cast,
    Mari_E_Hit = MariVFXType.Mari_E_Hit,
    Mari_R_Branch1_Cast = MariVFXType.Mari_R_Branch1_Cast,
    Mari_R_Branch1_Hit = MariVFXType.Mari_R_Branch1_Hit,
    Mari_R_Branch2_Cast = MariVFXType.Mari_R_Branch2_Cast,
    Mari_R_Branch2_Hit = MariVFXType.Mari_R_Branch2_Hit,

    CuBot_Attack_Generic = CuBotVFXType.CuBot_Attack_Generic,
    CuBot_Aggro = CuBotVFXType.CuBot_Aggro,
    CuBot_Boss_Death_Generic = CuBotVFXType.CuBot_Boss_Death_Generic,
    CuBot_Chopper_Attack = CuBotVFXType.CuBot_Chopper_Attack,
    CuBot_Chopper_Hit = CuBotVFXType.CuBot_Chopper_Hit,
    CuBot_Hunter_Attack = CuBotVFXType.CuBot_Hunter_Attack,
    CuBot_Hunter_Hit = CuBotVFXType.CuBot_Hunter_Hit,
    CuBot_Minny_Attack = CuBotVFXType.CuBot_Minny_Attack,
    CuBot_Minny_Hit = CuBotVFXType.CuBot_Minny_Hit,
    CuBot_Bernie_Attack = CuBotVFXType.CuBot_Bernie_Attack,
    CuBot_Bernie_Hit = CuBotVFXType.CuBot_Bernie_Hit,
    CuBot_Sawyer_Attack = CuBotVFXType.CuBot_Sawyer_Attack,
    CuBot_Sawyer_Hit = CuBotVFXType.CuBot_Sawyer_Hit,
    CuBot_Trapper_Attack = CuBotVFXType.CuBot_Trapper_Attack,
    CuBot_Trapper_Hit = CuBotVFXType.CuBot_Trapper_Hit,
    CuBot_Drilly_Attack = CuBotVFXType.CuBot_Drilly_Attack,
    CuBot_Drilly_Hit = CuBotVFXType.CuBot_Drilly_Hit,
    CuBot_Shovy_Attack = CuBotVFXType.CuBot_Shovy_Attack,
    CuBot_Shovy_Hit = CuBotVFXType.CuBot_Shovy_Hit,
    CuBot_Toxion_Attack = CuBotVFXType.CuBot_Toxion_Attack,
    CuBot_Toxion_Hit = CuBotVFXType.CuBot_Toxion_Hit,
    CuBot_Luxion_Attack = CuBotVFXType.CuBot_Luxion_Attack,
    CuBot_Luxion_Hit = CuBotVFXType.CuBot_Luxion_Hit,

    Status_Burn = StatusVFXType.Status_Burn,
    Status_Poison = StatusVFXType.Status_Poison,
    Status_Slow = StatusVFXType.Status_Slow,
    Status_Stun = StatusVFXType.Status_Stun,
    Status_Shield = StatusVFXType.Status_Shield,
    Status_Root = StatusVFXType.Status_Root,
    Status_Silence = StatusVFXType.Status_Silence,

    Wave_Start = EnvironmentVFXType.Wave_Start,
    CuBot_Spawn = EnvironmentVFXType.CuBot_Spawn,
    CuBot_SpawnPillar = EnvironmentVFXType.CuBot_SpawnPillar,
    Guardian_Death = EnvironmentVFXType.Guardian_Death,
    Guardian_Levelup = EnvironmentVFXType.Guardian_Levelup,
    Panoharra_Death = EnvironmentVFXType.Panoharra_Death,
    Portal_Enter = EnvironmentVFXType.Portal_Enter,
    Portal_Exit = EnvironmentVFXType.Portal_Exit,
    Almanac_Collected = EnvironmentVFXType.Almanac_Collected,
    River_Splash = EnvironmentVFXType.River_Splash,
    ScorchedEarth_Burst = EnvironmentVFXType.ScorchedEarth_Burst,
    Rafflesia_Portal_Idle = EnvironmentVFXType.Rafflesia_Portal_Idle,

    Footstep_Grass = FootstepVFXType.Footstep_Grass,
    Footstep_Water = FootstepVFXType.Footstep_Water,
    Footstep_Generic = FootstepVFXType.Footstep_Generic,
    Footstep_Mud = FootstepVFXType.Footstep_Mud,
    Footstep_Stone = FootstepVFXType.Footstep_Stone,
    Footstep_Metal = FootstepVFXType.Footstep_Metal,
    Guardian_Jump = FootstepVFXType.Guardian_Jump,
    Guardian_Land = FootstepVFXType.Guardian_Land,

    RewardCard_Shimmer = UIVFXType.RewardCard_Shimmer,
    LevelUp_ScreenFlash = UIVFXType.LevelUp_ScreenFlash,
    Defeat_Vignette = UIVFXType.Defeat_Vignette,
    Victory_Confetti = UIVFXType.Victory_Confetti,
    Burn_Vignette = UIVFXType.Burn_Vignette,
    Poison_Vignette = UIVFXType.Poison_Vignette,
    Slow_Vignette = UIVFXType.Slow_Vignette,
    Stun_Vignette = UIVFXType.Stun_Vignette,
    Guardian_LowHP_Vignette = UIVFXType.Guardian_LowHP_Vignette,
}
