# Forest Guardians — PROJECT_CONTEXT.md

This document is the primary onboarding reference for AI assistants working on Forest Guardians.
Repository-wide engineering behavior is defined in **AGENTS.md**.
Update this file whenever architecture, standards, or project scope changes significantly.

---

## Project Identity

| Field | Value |
|---|---|
| Project Name | Forest Guardians |
| Developer | Henia's Roses (Andrie, Crishia, Leo, Angel) |
| Lead Developer | Andrie |
| Academic Program | BSIT — Animation and Game Development |
| Project Type | Undergraduate Capstone + Thesis |
| Engine | Unity 6 (6000.0.x) |
| Render Pipeline | Built-in Render Pipeline |
| Language | C# |
| Platform | PC (Windows) |
| Input | Unity New Input System |
| Genre | Single-player 3D Action-Defense / Hack-and-Slash |
| Theme | Philippine Biodiversity Conservation — SDG 15 |

---

## Project Objectives

Forest Guardians has two equally important deliverables:

1. **Unity Game** — a playable, polished prototype
2. **Undergraduate Thesis** — formal academic documentation

Implementation and documentation must remain consistent.
If implementation conflicts with documented design, identify the inconsistency before proposing changes.

---

## Current Milestone

The project is in **active development**.

### Completed and stable
- Core component architecture (Mb_CharacterBase, Mb_StatBlock, Mb_HealthComponent, Mb_AbilityController, Mb_Movement, Mb_GuardianBase, Mb_PauseManager)
- Stat and modifier system (Sc_Stat, Sc_StatEffect, Sc_Modifier, ModifierSource)
- Wave and enemy system (Mb_WaveManager, MB_CuBotBase, Mb_CuBotController, object pooling)
- Augment system (Sc_AugmentBase, Mb_AugmentManager, AugmentFactory)
- Ability system (Sc_BaseAbility, SO_Ability, Sc_AbilityScalingEntry)
- Rajah Bagwis — all abilities implemented (Primary, Secondary, Q, E, Passive, R Branch 1 & 2)
- Reward and progression system (Mb_RewardsManager, Mb_RewardsPanelUI)
- Animation system (Mb_GuardianAnimator)
- Dialog system (Mb_DialogManager, Mb_DialogUI, SO_Dialog, SO_DialogSequence)
- Tutorial stage (Mb_TutorialManager, Mb_TargetDummy)
- Wildlife and Almanac system (Mb_WildlifeDiscoveryManager, Mb_AlmanacManager, SO_WildlifeEntry)
- Audio system (Mb_AudioManager, SO_AudioLibrary)
- VFX system (Mb_VFXManager, SO_VFXLibrary, Mb_VFXInstance)
- UI systems (HUD, healthbars, ability slots, minimap, floating text, wave progress)

### In progress or experimental
- Second guardian character (Mari) — architecture ready, implementation pending
- Guardian leveling UI
- Stage 2 and Stage 3 design

### Not yet started
- Multiplayer (out of scope)

---

## Scene Flow

- **Bootstrap** — hosts GameManager, UIManager, SceneLoader, Mb_AudioManager, Mb_AlmanacManager (all DontDestroyOnLoad)
- **ApplicationGUI** — main menu canvas
- **Stage1** — primary gameplay scene; hosts Mb_StageManager, Mb_WaveManager, Mb_RewardsManager, Mb_AugmentManager, Mb_VictoryManager, Mb_DefeatManager, HUD Canvas, CuBot pool

**Always enter Play Mode from the Bootstrap scene in the Editor.**
Launching Stage1 directly will produce null reference errors (UIManager, GameManager not initialized).

---

## System Ownership Map

### Player Systems
| System | Script(s) |
|---|---|
| Character root | Mb_CharacterBase → Mb_GuardianBase → Mb_PlayerController |
| Stats | Mb_StatBlock |
| Health / shields | Mb_HealthComponent |
| Movement | Mb_Movement |
| Input | Mb_PlayerController (Unity New Input System, Action Map: "Player") |
| Abilities | Mb_AbilityController, Sc_BaseAbility subclasses |
| Animation | Mb_GuardianAnimator |
| Augments | Sc_AugmentBase subclasses, managed by Mb_AugmentManager |

### Enemy Systems
| System | Script(s) |
|---|---|
| Enemy root | Mb_CharacterBase → MB_CuBotBase → Mb_CuBotController subclasses |
| AI / movement | Mb_CuBotController (NavMeshAgent) |
| Enemy abilities | Sc_BaseAbility subclasses (Sc_ChopperMeleeAttack, Sc_HunterRangeAttack, etc.) |
| Spawning / pooling | Mb_WaveManager |
| Targeting | Mb_CuBotController (Panoharra default, player on aggro) |

### Stage / Progression Systems
| System | Script(s) |
|---|---|
| Stage lifecycle | Mb_StageManager (OnStageStart, OnStageEnd) |
| Wave flow | Mb_WaveManager (Preparation → Combat → Resolution) |
| Rewards | Mb_RewardsManager, Mb_RewardsPanelUI |
| Augment management | Mb_AugmentManager |
| Victory | Mb_VictoryManager |
| Defeat | Mb_DefeatManager |
| Level progression | Mb_CharacterBase.LevelUp(), Mb_StatBlock.SetLevel() |

### UI Systems
| System | Script(s) |
|---|---|
| Game state / cursor | GameManager |
| HUD root | UIManager, Sc_SceneUIBinder |
| Healthbars | Mb_GuardianHealthbarUI, Mb_CuBotHealthBar |
| Ability panel | Mb_AbilitiesPanelUI, Mb_AbilitySlotUI |
| Reticle | Mb_ReticleUI |
| Top bar (wave) | Mb_TopBarUI |
| Rewards panel | Mb_RewardsPanelUI |
| Floating text | Mb_FloatingTextPool, Mb_PlayerFloatingTextManager |
| Minimap | Mb_MinimapUI, Mb_MinimapIconRegistrar |
| Almanac UI | Mb_AlmanacUI, Mb_AlmanacDiamondCard |

### Wildlife / Almanac
| System | Script(s) |
|---|---|
| In-stage discovery | Mb_WildlifeDiscoveryManager, Mb_WildlifeAnimal, Mb_WildlifeSpawnPoint |
| Hotbar | Mb_WildlifeHotbarUI, Mb_WildlifeHotbarSlot |
| Persistent records | Mb_AlmanacManager, Sc_AlmanacSaveData |

### Audio / VFX
| System | Script(s) |
|---|---|
| Audio | Mb_AudioManager, SO_AudioLibrary |
| VFX | Mb_VFXManager, SO_VFXLibrary, Mb_VFXInstance |

### Dialog / Tutorial
| System | Script(s) |
|---|---|
| Dialog playback | Mb_DialogManager, Mb_DialogUI |
| Wave dialog | Mb_WaveDialogBinder |
| Stage dialog | Mb_StageDialogBinder |
| Tutorial | Mb_TutorialManager |

---

## Coding Conventions

### Naming prefixes
| Prefix | Type |
|---|---|
| `Mb_` | MonoBehaviour |
| `Sc_` | Plain C# class |
| `SO_` | ScriptableObject |
| `I_` | Interface |
| `E_` | Enum file |

### Field naming
| Scope | Convention | Example |
|---|---|---|
| Public | PascalCase | `MaxHealth`, `AttackPower` |
| Private SerializeField | PascalCase with '_' prefix | `_ProjectileOrigin |
| Private | camelCase with `_` prefix | `_currentHealth` |
| Protected | PascalCase with `_` prefix | `_MoveSpeed` |
| Constants | ALL_CAPS | `MAX_WAVES` |

### Critical API rules
- Stat value accessor: **`GetValue()`** — never `.Value`
- Cooldown formula (Q/E/R): `baseCooldown / (1 + Haste / 100)`
- Cooldown formula (Primary/Secondary): `1 / AttackSpeed`
- Events named with `On` prefix: `OnWaveStart`, `OnHealthChanged`
- Static events preferred for cross-system communication
- `FindObjectOfType` forbidden — use Inspector references or singletons

---

## Architecture Rules

- **ScriptableObjects** hold configuration data only — no runtime state
- **MonoBehaviours** own runtime logic and lifetime
- **Managers coordinate** — they do not own gameplay logic
- **Composition over inheritance** — distinct responsibilities in separate components
- **No enterprise patterns** — no service locators, no IoC containers
- **Pool safety** — CuBot stats always reset from SO base values on `Reset()`; never accumulate across pool cycles
- **Modifier ownership** — all stat changes go through `Mb_StatBlock.AddModifier()` / `RemoveModifier()`; never write to `Sc_Stat.Effects` directly
- **Event cleanup** — always unsubscribe in `OnDisable()` or `OnUnequip()` to prevent ghost listeners

---

## Development Expectations

### Implementation style
- **Default: implement directly.** Provide complete, copy-pastable, compilable code.
- Never truncate code with `// ... rest of code` or similar. Always write the full implementation, unless directly asked or change is very minimal (1 line).
- If output is very long, split into clearly labeled sequential parts — but complete each part fully.

### Before coding
- State the task in one sentence
- List assumptions
- List affected systems
- Recommend one approach (not multiple options unless explicitly asked)

### After coding
- Inspector setup steps
- Testing notes

### Design challenges
- Flag significant technical debt or architectural violations before implementing
- Do not redesign or refactor beyond what was asked
- Do not add fields, components, or behaviors that were not requested (no feature creep)

### Explanations
- Concise by default
- Detailed only when the topic is genuinely complex or when asked

### Clarification
- Ask only when truly blocked
- Ask one question at a time
- Make reasonable assumptions and state them rather than asking about minor details

---

## Unity Setup Details

- **Unity version:** Unity 6 (6000.0.x), Built-in Render Pipeline
- **Input:** Unity New Input System, Action Map name: `"Player"`
- **Input Action asset location:** assign via Inspector on Mb_PlayerController / Mb_PauseManager
- **Bootstrap scene:** must be the first scene in Build Settings and the entry point for Play Mode
- **CuBot pool:** child GameObjects under a `CuBotPool` GameObject in the Stage scene, pre-populated in the Inspector
- **Spawn points:** `Transform` list on `Mb_WaveManager` in the Inspector

---

## Testing Workflow

- **Primary test scene:** Stage1 (entered via Bootstrap)
- **Ability / combat testing:** use `Mb_TargetDummy` — it auto-revives and fires standard kill events
- **No automated Unity tests** currently
- **Known issue areas to avoid treating as new regressions:**
  - Dialog system timing (LinePause, PlaySequenceAndWait pattern)
  - Wave 4 tutorial dummy (timer-based end via ForceEndCurrentWave)
  - Victory sequencing (HoldFinalResolution flag in Mb_WaveManager)

---

## Communication Style

Preferred response structure for implementation requests:
Preferred response structure for design/architecture questions:
- Direct recommendation first
- Reasoning second
- Tradeoffs only if relevant

---

## Repository Maintenance

Update `PROJECT_CONTEXT.md` when any of the following change:
- Architecture or engineering standards
- System ownership
- Scene structure
- Coding conventions
- Project scope or milestone status

Keep `AGENTS.md` relatively stable — it governs general behavior.
Keep `PROJECT_CONTEXT.md` current — it governs project-specific behavior.