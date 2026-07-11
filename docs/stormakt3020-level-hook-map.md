# Stormakt 3020 level hook map

Status: living implementation map. Update this file whenever a level hook changes.

Purpose: keep new campaigns deterministic and make iteration possible without rediscovering the monolithic core. Method names are the stable references; line numbers are intentionally omitted because `Program.cs` moves rapidly.

## Runtime spine

```text
level select
  -> StartLevel(levelId)
  -> ResetLevelState(levelId)
  -> Step()
       input and player weapons
       StepRadio()
       StepShots() / StepEnemyShots()
       StepEnemies() / StepGroundTargets()
       StepLevelTimeline()
       StepBoss()
       StepStars()
  -> Render()
       DrawSky() / DrawNebula() / DrawStars()
       DrawLevelScenery()
       ground / boss / shots / enemies / player
       HUD / boss HUD / title / radio / result / pause
```

`StartLevel`, `ResetLevelState`, `StepLevelTimeline` and `DrawLevelScenery` are the target hooks. At the start of Skånska skuggor they are not yet explicit: selection only starts level 0 and most gameplay methods are hard-wired to Stora Bält. The first refactor must introduce these seams without changing level 1 behavior.

## Selection and state ownership

| Concern | Current hook | Skånska requirement |
|---|---|---|
| Selected row | `_levelSelection` in `StepLevelSelect` | Pass row 0 or 1 into `StartLevel`; unfinished later rows retain preview behavior. |
| Active campaign | missing | Add `_levelId`; never infer the running level from `_levelSelection`. |
| Deterministic reset | `Reset()` uses seed `3020` | Rename/generalize to reset the active level and seed level 2 independently. |
| Menu return | `StepLevelPreview` | Keep preview return; level clear/game over restart the same active level. |
| Dev unlock | `WAYLANDFORGE_STORMAKT_DEVELOPER_MODE` | Level 2 is startable in dev mode from the first playable slice. |

Invariant: selecting Skånska skuggor may never run Stora Bält with a changed title. `_levelId` owns every level-specific timeline, asset and result choice.

## Simulation hooks

| Order | Existing method | Reuse/generalize |
|---:|---|---|
| 1 | `StepRadio` | Select a level-specific `RadioCard` table. Radio timing remains presentation-only. |
| 2 | player movement/fire in `Step` | Reuse unchanged. Signal interference must never alter input or hitboxes. |
| 3 | `StepShots` / `StepEnemyShots` | Reuse. Extend enemy-shot kinds for copper salvos and mine links. |
| 4 | `StepEnemies` | Reuse generic movement/collision; introduce explicit enemy kinds for mist drone, convoy, snapphane and decoy. |
| 5 | `StepGroundTargets` | Reuse for signal beacons and mine turrets after ground-target type expansion. |
| 6 | `StepBoss` | Keep Kronens Tiende isolated to level 1. Add a separate Sören rival state before sharing any helper. |
| 7 | `SpawnEnemies` | Dispatch by `_levelId`; level 2 owns its wave table. |
| 8 | `SpawnGroundEncounters` | Dispatch by `_levelId`; level 2 spawns beacons/mining structures, not bridges. |
| 9 | `SpawnBoss` | Dispatch by `_levelId`; first Skåne slice ends before Glimminge Järn. |
| 10 | `StepStars` | Reuse with level-specific seed and optional palette only. |

## Render stack and safety

| Layer | Current hook | Level 2 plan |
|---|---|---|
| Far plate | `DrawSky` | `skanska_background[_wide]`, vertically mirrored/looped in WFSA. |
| Atmospheric fallback | `DrawNebula` | Red aurora and green-black fog fallback when the asset pack is missing. |
| Parallax props | `DrawBeltRuins` | Dispatch to `DrawSkanskaScenery`: crystal pines, kiln moons and mining wrecks. |
| Physical props | `DrawGroundTargets` | Signal beacons and mining turrets stay below projectiles/player. |
| Rival/background pass | new | Sören's wordless first pass is non-colliding and drawn behind projectiles. |
| Hazards | `DrawEnemyShots` / `DrawAnchorHazards` | Copper rounds and mine telegraphs remain fully bright through interference. |
| Actors | `DrawEnemies`, `DrawShip` | Black/copper/green must remain distinct from Danish red/white and Swedish blue/gold. |
| Presentation | HUD/title/radio/result/pause | Interference goes before radio/result/pause and may not obscure pause or dangerous shots. |

## Asset pipeline hooks

1. Generated chroma-key sources live beside transparent production PNGs under `assets/stormakt3020/`.
2. Exact generation prompts live in sibling `.prompt.md` files.
3. `tools/stormakt3020/build_assets.py` crops and downsamples named sprites into `stormakt3020.wfsa`.
4. `SpritePack.LoadDefault` loads WFSA; every generated sprite needs a readable code-drawn fallback.
5. Level 2 names use the prefixes `skanska_`, `snapphane_`, `soren_` and `glimminge_`.

First asset batch:

- `soren_svartkrut_neutral`, `soren_svartkrut_speak`
- `soren_corsair`, `soren_corsair_boost`, `soren_corsair_damaged`
- `snapphane_mist_drone`, `snapphane_afterimage`
- `skanska_background`, `skanska_background_wide`
- `skanska_crystal_pines`, `skanska_kiln_moon`, `skanska_mining_wreck`

## Audio hooks

- `StormaktMusicLoop` already preloads and crossfades menu/combat/boss tracks.
- Add level-specific combat selection without replacing the `StormaktMusicTrack` transition mechanism.
- `StormaktVoice` and `LoadVoices` need a Sören entry; placeholder English voice is acceptable.
- New SFX should enter the existing bounded mixer queue. Do not open a second audio stream.

## Skånska build checkpoints

1. **Hook spine (landed 2026-07-11):** `_levelId`, start/reset/dispatch seams, level 2 enters a deterministic 60-second skeleton with its own seed, title, palette, scenery fallback, wave table and result. Level 1 remains on its existing dispatch path.
2. **Snapphane identity:** generated Sören ship and portrait, first radio, silent background pass.
3. **World:** generated Skåne background/props, fog drones, convoy and signal beacon.
4. **Rival duel:** Sören dash, copper salvo, decoys and deterministic interruption.
5. **Glimminge Järn:** separate boss state, two phases, heavy death and level-specific result.

For every checkpoint: build, `git diff --check`, capture at least one direct WFEX frame, verify deterministic selection, commit and push narrowly.
