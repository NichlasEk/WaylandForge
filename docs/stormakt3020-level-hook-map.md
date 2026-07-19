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

`StartLevel`, `ResetLevelState`, `StepLevelTimeline` and `DrawLevelScenery` are the stable campaign hooks. Stora Bält, Skånska skuggor, Öresunds järnkrona, Silverkroppen and Fogdens tionde värld are public campaign rows. Silverkroppen retains its separate RTS/dungeon submenu, but the submenu itself is publicly reachable. Snapphanens ed is a directly startable developer row with its own level state; Köpenhamns ring remains publicly locked but opens a ring/landing/Holmen developer submenu.

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

## Köpenhamn dispatch - checkpoint 9 playable

Campaign row 7 owns developer-startable level id `6`, reset seed `3707` and a separate `CopenhagenWorldState`. Checkpoint 1 added the independent render/step dispatch, persistent Bana 5 loadout and previous campaign support, approach and Trekroners lock. Checkpoint 2 replaces the temporary result stop with a menu-free transition into De tolv timmarnas amiralitet. Twelve destructible hour marks accumulate attack patterns; the twelve-stroke volley executes only surviving registered hours, so destroyed marks become deterministic safe gaps. Defeating the clock core reaches a Frederik Null checkpoint card.

Checkpoint 3 replaces that card with a second menu-free transition into full boss Frederik Null. His phase-1 seizure chains can turn campaign support against Karl, his phase-2 register blades own separate health/collision and his phase-3 active repair node can restore armor at the next accounting stamp. Wrong repair nodes retaliate instead of accepting damage.

Checkpoint 4 makes Frederiks escaping customs core become Øresunds Øje without returning to the campaign menu. The eye samples actual ship displacement into deterministic directional votes, exposes its current forecast in the HUD and telegraphs toll walls around a projected future position. Breaking movement habits is the defensive solution. Its second phase closes the core behind three separately destructible orbiting lenses; a completed Sören oath destroys one lens as support but cannot skip the gate. The reopened phase-3 core predicts and fires faster. Defeating it reaches the **Dannebrogsvingar väntar** checkpoint card.

Checkpoint 5 removes that result stop. Christian compiles three Dannebrog cross formations directly through the golden port after the Eye falls. Each formation owns four corner nodes but acts as one collision/attack object. The first destroyed corner becomes a visible safe seam; its index rotates the following formation before the surviving arms charge through the playfield. Missing the destruction window opens a deterministic emergency seam at the price of one hit, preventing a softlock. Clearing all three formations reaches the **Absalon och Elefanten** checkpoint card.

Checkpoint 6 removes that result stop and starts Absalon and Elefanten in the same world. Absalon's seven-node shield wall blocks real player shots outside its initial gap. Elefanten marks and charges that same opening, turning the obvious firing lane into the next movement threat. Both hulls own two destructible turrets; broadside size follows live turret count. Repairs can restore one destroyed turret at half health but never hull or a defeated frigate. Sören opens one extra wall node, freed ships damage turrets and Snapphane allies lengthen repair intervals. The surviving frigate gains an extra solo broadside. Defeating both reaches the **Superfregatten väntar** checkpoint card with both wrecks still visible.

Checkpoint 7 removes that result stop and brings Kong Christian's Superfregatt directly over the two wrecks. Phase 1 owns a drone bay and two broadside sections; each destroyed section permanently removes its attack. Phase 2's four cross nodes telegraph and lock one playfield quadrant at a time, while every destroyed node leaves that quadrant permanently safe. Phase 3 gives a separate memory mantle three cumulative royal orders: cannon shot, impossible rockets and a warned silver beam. Phase 4 opens the crown reactor. Campaign support lengthens drone, aimed-shot and beam intervals but never deals reactor damage for Karl.

Checkpoint 8 makes the reactor break start a short playable Holmen landing instead of a result card. Three physical anchors accept real player-shot damage and return aimed fire until broken. Releasing all three turns the same level directly into a separate `CopenhagenGroundState`: top-down keyboard/controller/pointer movement, directional melee, stagger, potion, four pursuing dock guards and the first silver-heart door. The ground act owns `copenhagen-holmen.json`; normal Bana 7 Start resumes that save across wide/legacy dimensions without touching Silverkroppen's autosave. The heart door is the explicit stop before the next marginal-system slice.

Checkpoint 9 turns that heart into the first authored Codex interaction. **Hjertat är en port** compiles over time, physically unfolds the silver heart and installs a visible active law. **Hjertat är öppet** is interpreted literally: the heart opens without becoming a passage, emits a local damaging fault and returns to the choice. The correct port enters a second room with three guards and a separately destructible bridge cannon whose warned lane and broadside continue while Karl fights. Clearing both threats opens the Rosenborg checkpoint.

The first Holmen graphics pass packs eight new environment sprites and eight new guard poses while reusing Silverkroppen's Karl combat/movement and temple floor. The environment sheet uses measured crop gutters so the furnace and debris remain separate. Guard movement, telegraph, contact, hit, fall and dead frames are state-driven; `FacingLeft` updates only while alive, preventing a corpse from flipping when Karl crosses it. Bana 7 now opens a six-row Copenhagen submenu in developer mode: new ring, direct landing, saved Holmen arsenal, then three visibly locked future depths. Both 400x280 and 320x224 keep all rows and footer inside the panel.

`WAYLANDFORGE_STORMAKT_COPENHAGEN_LANDING_TEST=1` and `WAYLANDFORGE_STORMAKT_COPENHAGEN_GROUND_TEST=1` extend the seven existing Copenhagen fixtures. Complete 760-frame landing traces repeat at `15cca66447daee8a` in wide and `a10ac2aeaf0db652` in legacy. The complete correct-marginal/arsenal trace repeats at `e13d637c571123ac` in wide and `b0744829f39ad627` in legacy; the literal fault repeats at `aa2ecf7097994603` and `90e6ac28ba8406ae`. A 1200-frame correct-marginal trace produces `21c5d51318874a8f` through WFEX v2 raw, PACKRLE and shared memory. Schema 1 remains accepted as a first-room save; an isolated schema-2 save resumes through the submenu inside the second room with the active law, health, position, potion, enemies and partial cannon health restored.

The intended complete level remains a two-act final: a level-owned shmup boss chain through Köpenhamns ring followed by a direct, menu-free landing into a separate top-down hack-and-slash state under the city. The ground act may reuse proven dungeon movement/combat helpers, but it must use its own save namespace and may not mutate Silverkroppen's `DungeonState` or autosave. Full design: `docs/stormakt3020-level-07-kopenhamns-ring-codex-argentum.md`.

Checkpoint 2 moves module ownership out of `TitheWorldState` into `StormaktLoadout`. Player firing, cooling cadence, seizure-armor charge, ship overlays and module HUD all read the same active object in Bana 5 and 6; death reset clones its live charge instead of silently rebuilding a fresh kit. Normal Bana 6 start reads the campaign file and also transfers `FreedShips` into `SnapphaneWorldState`. `Slow+Start` deliberately chooses standard weapons without modifying the real save.

`StormaktCampaignSave` schema 2 appends `SnapphaneRoute`, `SnapphaneAllies` and `SorenOathComplete`. Schema 1 remains accepted with zero/default Snapphane fields. Atomic writes still use a sibling temporary file and replacement, but fresh starts and the `WAYLANDFORGE_STORMAKT_LOADOUT_TEST=0..7` matrix suppress all campaign writes. Isolated-state WFEX tests prove eight distinct armed frames, exact schema-1/schema-2 kit mapping, byte-identical saves after test/fresh runs and a real schema-2 write after the first Bana 5 upgrade.

Checkpoint 3 expands `SnapphaneWorldState` with beacons, scent mines, hunters and hunter projectiles while keeping them out of generic Bana 1 lists. Three deterministic beacon waves each contain one uneven green true signal and two red-white counterfeits. False beacons transition through off, signal, revealed and destroyed states and release a real hunter when exposed. Physical layer-2 wrecks share visible position with collision and shot damage; a heavy hit gives them a seeded lateral impulse before their compact burst/recycle transition.

Magnet broadside turns a scent mine without destroying it; all other loadouts can break the shell directly. Hunter shots home on a turned mine before Karl. A lured impact explodes against nearby false beacons, physical wrecks and other hunters, increments the visible `VILSELD` outcome and never draws a tracking vector. `WAYLANDFORGE_STORMAKT_EMERGENCE_TEST=1` builds one isolated two-hunter/turned-mine proof: the first hunter's shot detonates the mine and kills the second hunter deterministically.

The project-bound sheet is `snapphane-beacons-wrecks-v1-source.png` -> alpha-cleaned `snapphane-beacons-wrecks-v1.png`, with the exact built-in image-generation prompt in its sibling Markdown file. `build_assets.py` packs twelve gameplay sprites: four beacon states, three mine states, hunter skiff, three physical wreck types and wreck burst. The later graphics pass adds opaque `snapphane-wreck-sea-background-v1.png` plus the eight-object `snapphane-wreck-sea-props-v1-source.png` -> alpha-cleaned runtime sheet. Separate mirrored 320/400 background entries scroll behind eight translucent, non-colliding parallax props; physical wrecks, beacons, mines and ships remain the fully opaque state-owned layer. Mines now receive distinct red hostile or pulsing cyan turned rings, while active rescue buoys receive yellow/green proximity rings. Code fallback remains for every gameplay family and for the full background path.

The two non-physical state-wreck layers now select deterministic reduced sprites from the same eight-prop sheet instead of exposing their triangle/cross/line fallback geometry over the generated plate. Layer 0 uses 24-pixel, 82-alpha silhouettes; layer 1 uses 42-pixel, 148-alpha silhouettes. The physical layer retains its separate full-opacity collision sprites. Repeated normal-flight 400x280 hashes at ages 100, 430, 760 and 1180 are `9f8a00e1becfcdc3`, `c74fed9e3c96d67d`, `3d9521df77b69262`, `1a67470f6e7c148f`; 320x224 gives `48538bd90801e5d2`, `df4ab40e0de34b5c`, `01598ecc6decf873`, `9431178e2b5e6b73`.

Checkpoint 4 begins the duel at vrakhav age 3300 and clears the earlier beacon encounter before `SorenDuelState` becomes authoritative. Fas 1 owns the real corsair, non-colliding afterimages and aimed copper salvos. Fas 2 adds alternating physical chain hooks that ordinary fire can break and Kedjekarteschen damages harder. Hooks and Sören are addressed before the generic player-shot cleanup, while afterimages never enter collision state. Sören loses honor rather than health and therefore cannot enter an enemy death or explosion path.

At zero honor the duel changes to an oath phase in the same state: projectiles and hooks are cleared, Sören eases into a position alongside Karl and `SorenOathComplete` is set before the result transition. Sören's challenge, oath and Ebbas confirmation use the existing priority radio queue; stage clear waits until the queue is empty. The cards are text-only until the dedicated fixed-seed voice pass and deliberately do not borrow older clips.

The sheet `snapphane-soren-duel-v1-source.png` -> `snapphane-soren-duel-v1.png` supplies four corsair states, an afterimage, chain hook, copper salvo and oath pulse. Its sibling prompt records the exact built-in image-generation request. Repeated 400x280 WFEX runs matched at ages 3325, 3470, 4050, 4210, 4860, 5350, result and menu; 320x224 covers the same duel, oath and handoff states.

Checkpoint 5 replaces the temporary post-oath clear with `RouteStarted`, a 90-frame spatial commitment window and two independent route timelines. No modal input handler is introduced: ordinary ship movement across a real center dead zone selects `Kaparleden` or `Krutrannan`. Selection, completed rescues and route completion call the existing atomic schema-2 writer with the incoming Bana 5 fleet count intact.

Kaparleden owns three `SnapphaneRescueShip` records. Proximity advances an individual 150-frame channel and leaving the radius decays only that ship's progress; a timeout never invents missing allies. `IsSnapphaneRescueChanneling` doubles the installed primary module's cooldown and prevents broadside creation while leaving movement, heat and ordinary projectiles alive. Each completed channel increments and immediately persists `SnapphaneAllies`. The route now dispatches its own deterministic hunter pressure and scent mines rather than presenting an empty channel: two out of every three hunter locks track Sören, one continues to track Karl, and any turned scent mine overrides either target. Phase-3 Sören guards the active rescue, alternates visible dash/ready states and fires twin homing copper rounds at the nearest surviving hunter. The concise HUD states `SÖREN TÄCKER` during this division of labor and changes to `VÄND MINA LOCKAR JÄGARELD` while a usable turned lure exists.

`WAYLANDFORGE_STORMAKT_SNAPPHANE_RESCUE_TEST=1` creates a campaign-write-suppressed three-buoy fixture after a fresh developer start. Repeated 400x280 traces match at rescue ages 90, 180, 300, 450 and 620 (`392921d1c963dbc3`, `1e5b1ff689261a2f`, `f4241d109d7f32c4`, `bff6e4bd3ec886b7`, `f060725c96e7eda5`) and complete all three rescues at `1183fc121da72fd9`. The equivalent 320x224 hashes are `30f5d5382e111b17`, `f78a1eccb9a146c2`, `07fad3ace59bd6cb`, `8fe8d64f1b815f49`, `85eb72af0c8b4bcb` and final `1929accd60bc6cf2`.

Krutrännan owns four `SnapphaneWreckPress` records whose simulated gap is shared by the rendered pair of physical press jaws. Three route-tagged false beacons drive the displayed bonus count, while normal mine, hunter-shot and player-shot systems remain reusable. Two elite hunters carry 74 health, their own packed sprite and denser two-shot salvos. The route awards no allies and calculates its final fixed-plus-beacon bonus only at completion.

The sheet `snapphane-route-v1-source.png` -> `snapphane-route-v1.png` supplies two allied ships, intact/broken rescue buoy, two press jaws, elite hunter and release pulse; the exact built-in generation prompt is stored beside it. Repeated 400x280 traces matched at route choice, commitment, channel/press combat, result and Bana 7 menu for both routes. Separate 320x224 traces cover the same states without HUD overlap. Isolated saves prove `SnapphaneRoute=1` with the actually rescued count and `SnapphaneRoute=2` with zero free allies.

Checkpoint 6 starts only after either route has completed and waits 180 frames before `RedHoundsBossState` becomes authoritative. Completed Kaparleden rescues first accelerate out of their route positions, lose their spent buoy overlay and are hard-cleared when boss state starts; persisted `SnapphaneAllies` remains unchanged and supplies the separate support formation. Sporet, Biddet and Koblet keep separate current/target positions, cannon health and packed intact/damaged sprites while sharing 1050 boss health. Fleet intervals are now 82/98 frames in phases 1/2 instead of 96/118. Phase 2 eases the same anchors into a V instead of teleporting them; its two 110-health physical chain records fragment and fade independently, with doubled damage from Kedjekartesch and Kronborr. Phase 3 opens Koblet's hunting mask, exposes three 54-health scent vents and blocks all shared-core damage until every vent is broken. While any vent remains, 68-frame fleet salvos, 88-frame flank fans and the 270-frame warned beam/five-shot cycle overlap. Breaking the final vent starts deterministic `DesperationAge`: 58-frame fleet salvos, 44-frame aimed five-shot fans, 86-frame six-shot curtains with one rotating safe lane, alternating 72-frame side fans and a sinusoidally sweeping formation. Death is staged across the three anchors before the queued victory radio, result pause and Bana 7 handoff.

Sören's support target is derived from current visible state: healthiest live cannon, healthiest live chain, then the exposed core. He may reduce the core only to 20 health and therefore cannot steal Karl's final shot. The 0–3 persisted rescue allies target cannons only. `WAYLANDFORGE_STORMAKT_RED_HOUNDS_TEST=1` retains the full save-suppressed boss fixture. `WAYLANDFORGE_STORMAKT_RED_HOUNDS_FINAL_TEST=1` starts directly at the exposed-core Sista bettet with two allies and campaign writes suppressed. Its repeated 400x280 hashes at ages 100/430/760/1180 are `2e6e7a023ce42621`, `7c2aca334c738504`, `3370e9da77886cba`, `36944898e29fe50f`; 320x224 gives `714ce5a36c831024`, `f65656786a5ead8b`, `f42e473e90c07ad7`, `95436497ec64e36d`.

The sheet `snapphane-red-hounds-v1-source.png` -> `snapphane-red-hounds-v1.png` supplies three intact ships, three damaged ships, chain link and closed/open mask. Its sibling Markdown file records the exact built-in image-generation prompt and the crop treatment used by `build_assets.py`; every packed sprite retains a code fallback. Developer mode adds a damage guard in `DamageShip` and a visible `DEVSKÖLD` HUD label for rapid testing in levels 5 and 6. Copenhagen's separate ground-health path applies the same guard. Both guards are false in public play and in all other levels.

Agnete Rød commands Sporet, Bodil Rød commands Biddet and Dagmar Rød commands Koblet. Their intro, chain, mask and mega-weapon cards are short phase-paced entries in the existing priority queue; all-three cards use a dedicated group portrait and no old voice clip. `red-hound-admiral-triplets-v1-source.png` -> alpha-cleaned `red-hound-admiral-triplets-v1.png` packs neutral and shouting portraits for all three plus neutral/shouting group portraits. The exact built-in generation prompt and style references are stored in the sibling Markdown file. Three fully synthetic seed-locked VoxCPM2 references feed seven Danish `dots.tts-soar` lines. Ebba and Sören reuse their established synthetic reference identity and role seed for ten additional Swedish SOAR lines spanning duel challenge, hooks, oath, route selection, both routes, chain, mask and victory. Their raw masters, request records, hashes and deterministic stereo radio renders live under `radio/`; `StormaktVoice` and `LoadVoices` bind all 17 files to the phase-owned cards, leaving no text-only Bana 6 radio. Final casting remains subject to listening approval.

Level reset selects `StormaktMusicTrack.Snapphane`, whose 44-second 120 BPM D-minor classical-techno loop remains active through wreck sea, duel, oath and route play. `BeginRedHoundsBoss` crossfades to the matching 44-second `RedHoundsBoss` hunt fugue, and `BeginRedHoundsDeath` ducks the explosion before crossfading to the 16-second F-major/D-minor-shadow `SnapphaneVictory` loop retained by the result card. All three tracks have preserved fixed-seed ACE-Step masters, exact whole-bar runtime edits, fallback tracks and startup diagnostics.
