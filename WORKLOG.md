# Forest Guardians Worklog

This file is a persistent handoff note for future development sessions. Keep it concise and update it after meaningful implementation work.

## Current repository snapshot

- Project: Forest Guardians
- Unity: `6000.3.9f1`
- Current branch: `gameplay-additions`
- Current HEAD: `1a80073` — `Add elite CuBots and Forest Fruit rewards`
- Remote: `https://github.com/AndrieCD/ForestGuardians.git`
- Current Unity context: `Stage3`, active object `GuardianSpawner`
- Build Settings entry point: `Assets/Scenes/Bootstrap.unity`
- No automated Unity tests are currently documented.

## Preserved game-version snapshots

These local annotated tags now preserve the three development generations:

| Version label | Tag | Commit | Existing branch/reference | Meaning |
|---|---|---|---|---|
| Demo Defense | `demo-defense-2026-07-21` | `5fb1884` | `origin/main` | Late-July defense/demo snapshot with Luxion boss phases and Stage 3 wave spawning. |
| Compliance CAP2 | `compliance-cap2-2026-07-23` | `1ec569e` | `main`, `origin/For-Compliance-CAP2` | Post-defense compliance snapshot, including Luxion animation/restoration and related Stage 3/cutscene updates. |
| Gameplay Additions | `gameplay-additions-2026-08-05` | `1a80073` | `gameplay-additions` | August feature-development snapshot with elite CuBots and Forest Fruit/Blessing systems. |

The existing branches were left untouched. Tags are local and have not been pushed to GitHub.

## Most recent confirmed work

### 2026-08-05 — Add elite CuBots and Forest Fruit rewards

The latest commit added or changed:

- `Mb_EliteCuBot`, which can apply elite stat multipliers, visual glow, and aura effects.
- Elite aura support for Commander and Guardian variants.
- Wave configuration for enabling an elite CuBot and selecting its aura.
- Wave spawning logic that selects one eligible elite spawn per configured wave.
- Forest Fruit collectible and spawner systems.
- Forest Blessing collectible and spawner systems.
- Forest Fruit stat reward data and reward selection support.
- `ModifierSource.ForestFruit` and `ModifierSource.ForestBlessing`.
- Reward-panel integration for Forest Fruit choices.
- Wave/reward synchronization so the wave manager waits for reward-panel completion.
- Audio manager updates.
- Cutscene player updates.
- CuBot healthbar updates for elite-related state.

The commit changed 19 tracked files, adding approximately 1,386 lines and removing 11 lines.

### Elite CuBot behavior and diagnostics

- Eligible elites remain Chopper, Hunter, Minny, Sawyer, Shovy, and Trapper.
- One elite is selected per configured wave.
- Elite per-stat multipliers are applied during CuBot base-stat reconstruction before normal stage and level scaling.
- Elite aura targets only CuBots tracked by the current wave manager and works through walls.
- Pool reuse rebuilds the CuBot as a fresh instance and clears elite state, aura modifiers, scale, and healthbar state.
- Missing pooled CuBots produce an error and the wave continues.
- Elite death uses the existing death event to produce one random blessing.
- Diagnostics report missing elite dependencies, invalid elite configuration, missing pools, and missing spawn points.

Manual verification checklist:

1. Configure one eligible elite wave and confirm exactly one elite spawns.
2. Confirm excluded types never become elite.
3. Compare normal and elite stats after stage and level scaling.
4. Confirm aura affects only active CuBots in the current wave and works through walls.
5. Kill the elite and confirm its aura disappears immediately.
6. Reuse the same pooled CuBot and confirm it returns to normal stats, scale, and healthbar state.
7. Confirm exactly one random blessing appears after an elite death.
8. Reduce the relevant pool size and confirm the missing-spawn error is logged while the wave continues.

## August 5 work that still needs scene/content integration

The code exists, but the checked-in Unity content does not appear fully wired:

- `Stage3.unity` has no `Mb_ForestFruitSpawner` component or `Mb_ForestBlessingSpawner` component.
- No Stage3 scene references were found for Forest Fruit, Forest Blessing, or their spawner objects.
- Stage3 wave assets contain ordinary reward values, but no `CanSpawnElite` or `EliteAura` fields.
- No Stage3 wave is configured with the new `ForestFruit` reward type.
- No dedicated fruit or blessing prefab was found by the feature reference search; the runtime currently falls back to colored primitive spheres.
- The new spawners still require scene references and spawn-point lists.
- `Mb_WaveManager` has an `EliteConfig` Inspector configuration, but the Stage3 scene was not changed in the August 5 commit, so designer-tuned elite settings were not persisted there.

Likely remaining implementation pass:

1. Decide which stage(s) should use Forest Fruits and Forest Blessings.
2. Add spawner GameObjects/components to those scenes.
3. Assign `Mb_RewardsManager`, collectible prefabs, and spawn-point transforms.
4. Create or assign final fruit/blessing visuals instead of placeholder spheres.
5. Configure selected Stage3 wave assets with `CanSpawnElite`, `EliteAura`, and `WaveReward: ForestFruit` where intended.
6. Tune and verify `Mb_WaveManager.EliteConfig` in the scene.
7. Playtest elite spawn, elite aura effects, elite death blessing drops, natural blessing spawns, fruit reward selection, stage cleanup, and reward-panel timing.

### Previous recent work

- `2026-07-23` — Luxion-related asset/Stage 3 updates and finalization work.
- `2026-07-21` — Added Luxion boss phases and Stage 3 wave spawning.
- `2026-07-21` — Imported Luxion model/material/texture assets.
- `2026-07-21` — Added automatic stage music resolution and wildlife discovery feedback.
- `2026-07-20` — Refined Mari's Psychic Slam shockwave and related gameplay flow.
- `2026-07-15` — Added Rajah balance buffs, small polish, and a prologue/Mari E/cheat-system update.

## Current working-tree state

At the time this worklog was created, the repository reported:

- Modified: `ProjectSettings/ProjectSettings.asset`
- Untracked: `.codex/`
- Untracked: three Forest Blessing `.meta` files:
  - `Mb_ForestBlessingCollectible.cs.meta`
  - `Mb_ForestBlessingSpawner.cs.meta`
  - `Sc_ForestBlessingData.cs.meta`

These changes were not created by the worklog task and should be reviewed before any cleanup or commit.

## Important project rules

- Enter Play Mode through `Bootstrap`.
- Use targeted searches in the relevant `Assets` subsystem.
- ScriptableObjects store configuration only; MonoBehaviours own runtime state.
- Use `Mb_StatBlock.AddModifier()` and `RemoveModifier()` for stat changes.
- Use `GetValue()` for stat access; do not use `.Value`.
- Do not use `FindObjectOfType`.
- Unsubscribe from events in `OnDisable()` or `OnUnequip()`.
- Reset pooled CuBot stats from ScriptableObject base values.
- Preserve existing architecture and avoid unrequested mechanics, polish, or refactors.

## Known documentation mismatches

- `AGENTS.md` names Unity `6000.3.9f1`; `PROJECT_CONTEXT.md` still says `6000.0.x`.
- Project documentation describes Built-in Render Pipeline, while URP packages are present in the package manifest. `GraphicsSettings.asset` currently has no active custom render pipeline.
- `PROJECT_CONTEXT.md` describes Stage1 as the primary test stage, while current development context is Stage3.
- `PROJECT_CONTEXT.md` describes Mari as pending even though Mari source files are present.

## Session handoff template

### Active task

### Assumptions

### Affected systems/files

### Implementation status

### Inspector or prefab setup

### Testing performed

### Known issues / next step

