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

`StartLevel`, `ResetLevelState`, `StepLevelTimeline` and `DrawLevelScenery` are the stable campaign hooks. Stora Bält, Skånska skuggor, Öresunds järnkrona, Silverkroppen and Fogdens tionde värld are public campaign rows. Silverkroppen retains its separate RTS/dungeon submenu, but the submenu itself is publicly reachable. Snapphanens ed is a directly startable developer row with its own level state; Köpenhamns ring remains the next locked/developer preview row.

## Selection and state ownership

| Concern | Current hook | Skånska requirement |
|---|---|---|
| Selected row | `_levelSelection` in `StepLevelSelect` | Pass row 0 or 1 into `StartLevel`; unfinished later rows retain preview behavior. |
| Active campaign | missing | Add `_levelId`; never infer the running level from `_levelSelection`. |
| Deterministic reset | `Reset()` uses seed `3020` | Rename/generalize to reset the active level and seed level 2 independently. |
| Menu return | `ReturnToCampaignSelect` | A result card waits for Start, then returns to menu music with the next campaign row selected; game over still recalls the current active level. |
| Public unlock | campaign status in `DrawLevelSelect` and `StepLevelSelect` | Level ids 0-4 are reachable without developer mode; level id 3 opens its separate RTS/dungeon submenu. |

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

## Öresund dispatch

Campaign row 3 owns level id `2` and is publicly startable as `STRID`. Reset seed `3303`, `OresundEnemyWaves`, the empty timed `OresundRadioCards` reservation, `StormaktMusicTrack.Oresund`, ringbridge layers, mission title and result card are all separate dispatch branches. The timeline begins the twin-fortress boss at frame 4500 and retains frame 9000 only as a defensive clear failsafe. Start on the completed result card returns to the campaign menu with row 4, Silverkroppen, selected. It must not fall through to Stora Bält or Skånska tables when a future subsystem is absent.

The first deterministic wide/legacy WFEX baseline landed 2026-07-14. `BridgeSectionState` and its direct-destruction, coupling-to-laser and control-house reroute paths landed 2026-07-15. Checkpoint 3 adds generated non-physical ringbridge layers plus two state-owned physical flap passages; ordinary shots stop on the shared flap rectangles while the second passage's laser ignores cover after a 60-frame warning. Checkpoint 4 composes the generated locomotive, command wagon, two cannon wagons and wreck from the same state. Its direct-disarm and explicit `MasterCouplingBroken → TrackDiverted → TrainBufferCrash` paths each repeated ten times in wide and legacy fields. Checkpoint 5 maps `TrainDisarmed`, `TrainCrashed` or a passed train to three explicit Sören targets, queues his and Ebba's existing voiced cards after the train result, and never mutates future boss state. Checkpoint 6 adds independent Helsingör/Helsingborg anchors, one shared 720-health pool, alternating shield/fire turns, warned cross-current beams, a central crown core and a clean collapse/result transition. Both focus solutions repeated ten times in wide and legacy fields, and the row is now public `STRID`.

## RTS dispatch - Silverkroppen

Campaign row 4 owns level id `3` and branches before the shmup simulation/render spine. `StepRts` and `DrawRts` operate on a separate `RtsState`; no ship shots, enemy waves, scrolling sky or shmup boss state are stepped while it is active. Reset uses seed `3404` and reconstructs cursor, camera and landing state deterministically. Developer selection, touchdown, survey placement and camera follow are direct-WFEX verified in both supported resolutions.

EXT3 uses `pointer_driver = "stormakt_rts"`. Legacy `S` steps are always five bytes; pointer-aware `P` steps are always 21 bytes: marker, Saturn buttons, pointer X/Y, pointer buttons and inside flag. The self-describing marker prevents mixed host/core build versions from blocking while waiting for a different packet length. Right drag selects arbitrary friendly units; right click on empty terrain distributes the selected set into a stable three-column formation. Left click mirrors the primary `Fire` action for placement and object activation. A direct extended-protocol capture verifies the selection rectangle. All other stdio cores continue receiving legacy `S` packets.

The economy slice remains inside `RtsState`: stable grid placement, explicit build stage, typed building list, power totals and fixed silver pulses. Steam placement validates Karl's build radius; the crusher validates against `SilverVeinWorldX`, shared by simulation and rendering so the visible vein cannot disagree with placement logic.

Swedish production extends the same state with barracks/animal-hall timers and mutable deterministic unit records. Cursor context selects a production building, groups nearby units by type or gives stable formation-offset move orders. Foot squads and moose caroleans have distinct speeds and silhouettes; no shmup enemy/unit lists are reused.

RTS combat owns separate mutable enemy/building health. Waves are keyed to `CombatAge`, which begins only after the first Swedish unit completes. Stable targeting prefers nearby defenders, then steam power, then landed Karl. Powered towers, squad salvos and moose melee share deterministic nearest-target ordering. Powder boars use a visible 45-frame fuse; organ wagons stop at bombardment range. Invincible developer runs suppress RTS damage but retain movement, fuses and attack timing for full-wave frame probes.

## Fogdens tionde värld dispatch

Campaign row 5 owns level id `4`, reset seed `3505` and separate `TitheWorldState`. Its render path branches before the older sky/belt stack and draws Det hängande registret, state-owned chained ships and physical cyan locks. Generic level-0 waves are explicitly suppressed; the level-owned `TitheRadioCards` table prevents Stora Bält dialogue from leaking into the archive.

At archive age 850, live shots and enemies are cleared before `ChoosingUpgrade` stops the combat spine. Left/right chooses one of two deterministic `TithePrimaryModule` values, `Z` or remote-safe `Start/Enter` installs, and `X` toggles the concrete drawback. Kronborren adds a power-7 central bolt at a nine-frame interval; Salvdirektören emits three power-2 spread shots at a four-frame interval. The HUD identifies the installed module. Wide and legacy direct-WFEX runs cover both selections and repeated hashes cover the panel and Salvdirektören result. Campaign persistence, generated archive assets and unique fixed-seed module audio now share the completed Bana 5 state transitions.

The first customs slice extends the same state with `TitheCustomsGate` and `TitheCoinMine` lists. Four fixed-age gates alternate their opening, reserve exactly 60 warning frames, block ordinary player shots outside the opening and damage through the shared `DamageShip` path. Mines use bounded deterministic acceleration toward Karl and a 45-frame fuse. Ordinary fire damages them; a power-5 broadside inside the capture field changes velocity/ownership and lets the mine destroy a generic collector. Neither object is stored in the legacy enemy-shot or ground-target lists.

The register fork is a spatial choice, not a modal menu. At age 2200, the center divider marks revision left and chain hall right; age 2380 records Karl's side in `TitheRegisterRoute`. Revision awards one fixed score bonus and owns three collector spawns. Chain hall creates three additional `TitheChainTarget` records and awards only locks actually destroyed by player shots. The result card reads the stored route and live `FreedShips` count. Three level-specific radio cards use new seed-locked Sören/Ebba clips rather than borrowing old dialogue, while the physical route lock owns a separate register-switch effect.

Ränteverket begins at age 3140 and owns `TitheSealWall` records rather than ground targets. Each wall has seven fixed segments, one deterministic opening and six independent 18-health plates; player shots and collision address the same segment index. At age 3820, the second safe cabinet switches `TitheUpgradeChoice` to broadside. `MagnetBroadside` emits two power-6 cyan pulses and converts nearby non-heavy enemy shots into player shots. `ChainCanister` emits a five-shot power-8 fan with a longer cooldown and no reflection. Both modules have separate packed sprites, HUD labels, projectiles and fixed-seed Stable Audio effects.

The third cabinet opens at age 4650. `SilverCooler` changes heat decay from one to three after 18 idle frames and draws cyan cooling fins. `SeizureArmor` halves normal cooling cadence, draws dark vault plates and consumes one explicit armor charge before `DamageShip` can take a life. `StormaktCampaignSave` persists all three enum selections and the freed-ship count atomically beside dungeon saves. Level 5 loads the kit on normal Start; `Slow+Start` produces the deterministic standard kit. In-memory reset also preserves the current loadout after death.

Campaign row 5 is public `STRID`. `DamageShip` follows the ordinary life-loss path after armor resolution; the former developer-only guard and `DEVSKÖLD` HUD marker are removed. `SeizureArmor` still consumes its one explicit armor charge before an ordinary hit can take a life.

## Sequential campaign return

All completed public missions use the same two-step handoff. Their result card remains on screen until Start, preserving the level's victory music and giving the player a deliberate pause. Start then calls `ReturnToCampaignSelect(levelId + 1)`, switches to menu music and highlights the following campaign row without starting it. Stora Bält selects Skånska skuggor, Skånska selects Öresunds järnkrona, Öresund selects Silverkroppen, the completed temple epilogue selects Fogdens tionde värld, and Rigsregnskabet selects Snapphanens ed. A death is intentionally different: `START ÅTERKALLAR` resets the current level rather than advancing.

The handoff was exercised through real public WFEX input for all five completed campaign missions. Stora Bält returned with row 2 highlighted, Skånska with row 3, Öresund with row 4, the temple epilogue with row 5 and Tionde världen with row 6. Silverkroppen's separate RTS/dungeon submenu also opens in non-developer mode. No handoff auto-started the highlighted mission.

## Snapphanens ed dispatch

Campaign row 6 owns level id `5`, reset seed `3606` and a separate `SnapphaneWorldState`. The first checkpoint branches out of the shared shooter spine immediately after player movement, weapon input and `StepShots`; generic enemies, ground encounters, bosses and old level timelines are never stepped. Both scheduled radio selectors explicitly choose an empty Bana 6 table, so Stora Bält dialogue cannot leak into the new level while its real script is still being produced.

`DrawSnapphaneWorld` is a separate render stack with three state-owned wreck depths, the physical silhouette of Sören's uneven green double-blink and its own mission title. The temporary one-minute checkpoint result remains in combat music until Start, then opens the campaign menu with row 7 selected. Seven-row menu spacing is verified in 400x280 and 320x224 without overlapping the footer. Repeated wide WFEX traces matched at the title, result and campaign handoff frames; a separate legacy trace covered the same three states.

Checkpoint 2 moves module ownership out of `TitheWorldState` into `StormaktLoadout`. Player firing, cooling cadence, seizure-armor charge, ship overlays and module HUD all read the same active object in Bana 5 and 6; death reset clones its live charge instead of silently rebuilding a fresh kit. Normal Bana 6 start reads the campaign file and also transfers `FreedShips` into `SnapphaneWorldState`. `Slow+Start` deliberately chooses standard weapons without modifying the real save.

`StormaktCampaignSave` schema 2 appends `SnapphaneRoute`, `SnapphaneAllies` and `SorenOathComplete`. Schema 1 remains accepted with zero/default Snapphane fields. Atomic writes still use a sibling temporary file and replacement, but fresh starts and the `WAYLANDFORGE_STORMAKT_LOADOUT_TEST=0..7` matrix suppress all campaign writes. Isolated-state WFEX tests prove eight distinct armed frames, exact schema-1/schema-2 kit mapping, byte-identical saves after test/fresh runs and a real schema-2 write after the first Bana 5 upgrade.

Checkpoint 3 expands `SnapphaneWorldState` with beacons, scent mines, hunters and hunter projectiles while keeping them out of generic Bana 1 lists. Three deterministic beacon waves each contain one uneven green true signal and two red-white counterfeits. False beacons transition through off, signal, revealed and destroyed states and release a real hunter when exposed. Physical layer-2 wrecks share visible position with collision and shot damage; a heavy hit gives them a seeded lateral impulse before their compact burst/recycle transition.

Magnet broadside turns a scent mine without destroying it; all other loadouts can break the shell directly. Hunter shots home on a turned mine before Karl. A lured impact explodes against nearby false beacons, physical wrecks and other hunters, increments the visible `VILSELD` outcome and never draws a tracking vector. `WAYLANDFORGE_STORMAKT_EMERGENCE_TEST=1` builds one isolated two-hunter/turned-mine proof: the first hunter's shot detonates the mine and kills the second hunter deterministically.

The project-bound sheet is `snapphane-beacons-wrecks-v1-source.png` -> alpha-cleaned `snapphane-beacons-wrecks-v1.png`, with the exact built-in image-generation prompt in its sibling Markdown file. `build_assets.py` packs twelve `snapphane_` sprites: four beacon states, three mine states, hunter skiff, three wreck types and wreck burst. Code fallback remains for every family. Repeated 400x280 WFEX traces match at ages 430, 760, 1180, 1700, 2500 and 4190; 320x224 covers the same ages plus the result/menu handoff.

Checkpoint 4 begins the duel at vrakhav age 3300 and clears the earlier beacon encounter before `SorenDuelState` becomes authoritative. Fas 1 owns the real corsair, non-colliding afterimages and aimed copper salvos. Fas 2 adds alternating physical chain hooks that ordinary fire can break and Kedjekarteschen damages harder. Hooks and Sören are addressed before the generic player-shot cleanup, while afterimages never enter collision state. Sören loses honor rather than health and therefore cannot enter an enemy death or explosion path.

At zero honor the duel changes to an oath phase in the same state: projectiles and hooks are cleared, Sören eases into a position alongside Karl and `SorenOathComplete` is set before the result transition. Sören's challenge, oath and Ebbas confirmation use the existing priority radio queue; stage clear waits until the queue is empty. The cards are text-only until the dedicated fixed-seed voice pass and deliberately do not borrow older clips.

The sheet `snapphane-soren-duel-v1-source.png` -> `snapphane-soren-duel-v1.png` supplies four corsair states, an afterimage, chain hook, copper salvo and oath pulse. Its sibling prompt records the exact built-in image-generation request. Repeated 400x280 WFEX runs matched at ages 3325, 3470, 4050, 4210, 4860, 5350, result and menu; 320x224 covers the same duel, oath and handoff states.
