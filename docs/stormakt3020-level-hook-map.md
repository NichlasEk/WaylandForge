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

`StartLevel`, `ResetLevelState`, `StepLevelTimeline` and `DrawLevelScenery` are the stable campaign hooks. Stora Bält and Skånska skuggor are public `STRID` rows; Öresunds järnkrona is the active developer-frontier and keeps preview behavior until its first playable slice lands.

## Selection and state ownership

| Concern | Current hook | Skånska requirement |
|---|---|---|
| Selected row | `_levelSelection` in `StepLevelSelect` | Pass row 0 or 1 into `StartLevel`; unfinished later rows retain preview behavior. |
| Active campaign | missing | Add `_levelId`; never infer the running level from `_levelSelection`. |
| Deterministic reset | `Reset()` uses seed `3020` | Rename/generalize to reset the active level and seed level 2 independently. |
| Menu return | `StepLevelPreview` | Keep preview return; level clear/game over restart the same active level. |
| Public unlock | campaign status in `DrawLevelSelect` and `StepLevelSelect` | Level 2 is startable without developer mode; level 3 remains `DEV`. |

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
| Atmospheric fallback | `DrawNebula` | Red aurora and green-black fog fallback when the asset pack is missing. Ordinary stars are suppressed on level 2. |
| Parallax props | `DrawBeltRuins` | Dispatch to `DrawSkanskaScenery`: crystal pines, kiln moons and mining wrecks. |
| Physical props | `DrawGroundTargets` | Signal beacons and mining turrets stay below projectiles/player. |
| Rival/background pass | new | Sören's wordless first pass is non-colliding and drawn behind projectiles. |
| Hazards | `DrawEnemyShots` / `DrawAnchorHazards` | Copper rounds and mine telegraphs remain fully bright through interference. |
| Actors | `DrawEnemies`, `DrawShip` | Black/copper/green must remain distinct from Danish red/white and Swedish blue/gold. |
| Presentation | HUD/title/radio/result/pause | Interference goes before radio/result/pause and may not obscure pause or dangerous shots. |

Boss dialogue is event-driven rather than timeline-driven: `ActivateBossRadio` is called by phase transitions and owns a priority card/age independent of `_missionFrame`. Dynamic boss radio overrides scheduled cards while active and is cleared by level reset.

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
- Level 2 selects `StormaktMusicTrack.Skanska`: a 21-bar 92 BPM loop derived from the generated source at an exact bar boundary, using the shared runtime crossfade.
- The repo-local casting spine is `radio/skanska-cast.json` -> synthetic role reference -> local line render -> `build_radio_voices.py` deterministic runtime filter. Existing lines use Dots MF; Birgitte's versioned line overrides use VoxCPM2 reference cloning because the Dots worker stalled during this slice. Voice IDs are wired through `StormaktVoice`, `LoadVoices` and level/event-specific radio cards.
- New SFX should enter the existing bounded mixer queue. Do not open a second audio stream.

## Skånska build checkpoints

1. **Hook spine (landed 2026-07-11):** `_levelId`, start/reset/dispatch seams, level 2 enters a deterministic 60-second skeleton with its own seed, title, palette, scenery fallback, wave table and result. Level 1 remains on its existing dispatch path.
2. **Snapphane identity (landed 2026-07-11):** generated Sören portrait, first radio, three-state corsair and silent background pass are wired with code fallbacks.
3. **World (polished 2026-07-11):** the generated starless Skåne background, transparent props, intact/damaged signal beacons, reveal-only mist drones and intact/damaged red/white fogde barges are packed and frame-verified with code fallbacks.
4. **Rival duel (landed 2026-07-11):** Sören owns separate non-boss state with boost dashes, aimed copper salvos, two non-physical radar decoys, health/time interruption and a damaged escape that never awards a kill.
5. **Glimminge Järn (polished 2026-07-11):** 720-health separate boss state with damage-blocking shield-braced, damaged, burning and connected-wreck sprites; complete content-aware crops; center-locked sprite crossfades and eased movement transitions; Birgitte Bille event radio; animated iron-raven escorts; iron-wall phase, drill-turret/crystal-spear phase, low-health escort/ember escalation and a level-specific result card.
6. **Public battle status (landed 2026-07-14):** the completed gameplay spine is exposed as `STRID` in normal mode. Remaining detail work stays polish rather than blocking campaign progression; Öresunds järnkrona becomes the next `DEV` row.

For every checkpoint: build, `git diff --check`, capture at least one direct WFEX frame, verify deterministic selection, commit and push narrowly.

## RTS dispatch - Silverkroppen

Campaign row 4 owns level id `3` and branches before the shmup simulation/render spine. `StepRts` and `DrawRts` operate on a separate `RtsState`; no ship shots, enemy waves, scrolling sky or shmup boss state are stepped while it is active. Reset uses seed `3404` and reconstructs cursor, camera and landing state deterministically. Developer selection, touchdown, survey placement and camera follow are direct-WFEX verified in both supported resolutions.

EXT3 uses `pointer_driver = "stormakt_rts"`. Legacy `S` steps are always five bytes; pointer-aware `P` steps are always 21 bytes: marker, Saturn buttons, pointer X/Y, pointer buttons and inside flag. The self-describing marker prevents mixed host/core build versions from blocking while waiting for a different packet length. Right drag selects arbitrary friendly units; right click on empty terrain distributes the selected set into a stable three-column formation. Left click mirrors the primary `Fire` action for placement and object activation. A direct extended-protocol capture verifies the selection rectangle. All other stdio cores continue receiving legacy `S` packets.

The economy slice remains inside `RtsState`: stable grid placement, explicit build stage, typed building list, power totals and fixed silver pulses. Steam placement validates Karl's build radius; the crusher validates against `SilverVeinWorldX`, shared by simulation and rendering so the visible vein cannot disagree with placement logic.

Swedish production extends the same state with barracks/animal-hall timers and mutable deterministic unit records. Cursor context selects a production building, groups nearby units by type or gives stable formation-offset move orders. Foot squads and moose caroleans have distinct speeds and silhouettes; no shmup enemy/unit lists are reused.

RTS combat owns separate mutable enemy/building health. Waves are keyed to `CombatAge`, which begins only after the first Swedish unit completes. Stable targeting prefers nearby defenders, then steam power, then landed Karl. Powered towers, squad salvos and moose melee share deterministic nearest-target ordering. Powder boars use a visible 45-frame fuse; organ wagons stop at bombardment range. Invincible developer runs suppress RTS damage but retain movement, fuses and attack timing for full-wave frame probes.
