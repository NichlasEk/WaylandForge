# Stormakt 3020 Assets

`karl-cclv-dark-frigate-v1.png` is the active dark Swedish player-frigate sheet, with matched normal and overheated states. Its painted flame area is cropped during asset packing; runtime draws two pulsing thrust jets only while a movement direction is held. `karl-cclv-swedish-hero-danish-enemies-v3.png` remains the standard-enemy/projectile source and preserves the superseded bright player concept. `stormakt-danish-boss-enemies-v1.png` is the transparent production sheet for Kronens Tiende, fogde sloops and the tax-seal drone. `stormakt-radio-portraits-v1.png` supplies paired neutral/speaking portraits for Ebba Grip, Fogde Rasmus and Kung Christian. `stormakt-stora-balt-environment-v1.png` contains physical bridge and parallax props, while `stormakt-stora-balt-background-v1.png` is the tall scrolling nebula plate. Each `-source.png` sibling preserves the original flat green generation, and the exact image prompts are versioned beside them.

`stormakt3020-logo-v1.png` is the transparent dark-blue, iron and gold three-crowns title emblem used by the level-select screen. Its chroma-key source and exact prompt are preserved alongside it.

`soren-svartkrut-radio-v1.png` supplies matched neutral and speaking portraits for the Skånska skuggor rival transmission. Its magenta-key source and generation brief are preserved beside it.

`soren-corsair-v1.png` supplies neutral, boost and damaged top-down states for Sören's black-iron, copper and forest-green rival ship.

`stormakt-skanska-background-v1.png` is the starless black-forest mining plate for level 2: crystal spruce formations, ironworks, fog and deep red aurora rather than open space.

`stormakt-skanska-props-v1.png` supplies transparent crystal-pine, kiln-moon and mining-hoist foreground sprites for level 2. The retained `-source.png` is the flat magenta generation source used for deterministic local alpha removal.

`glimminge-jarn-v1.png` supplies the intact/damaged Glimminge Järn boss and its detached drill turret. The retained `-source.png` is the flat green generation source used for deterministic local alpha removal.

`skanska-enemies-v1.png` replaces level 2's code placeholders with a transparent mist drone and intact/damaged Danish fogde cargo barges. The retained `-source.png` is the flat magenta generation source used for deterministic local alpha removal.

`glimminge-animations-v1.png` supplies coherent shield-braced, burning and collapsed-wreck states so the boss never falls back to floating debug blocks.

`birgitte-bille-radio-v1.png` introduces Glimminge Järn's commander, Kommandør Birgitte Bille, with matched neutral/speaking radio portraits.

`louhi-radio-v1.png` introduces Silverbergets häxa with matched neutral/speaking radio portraits for her first transmission beyond the defeated Swan. Its chroma-key source and exact reference-guided prompt are retained beside it.

`skanska-signal-beacon-v1.png` replaces the last physical triangle placeholder with intact/damaged mining-beacon sprites.

`glimminge-iron-raven-v1.png` supplies neutral, banking-attack and damaged states for Birgitte's boss escort fighters.

`skanska-projectiles-v1.png` replaces the remaining green cross/loose-pixel, fogde-barge and iron-raven projectile primitives.

`skanska-combat-details-v1.png` supplies Sören's radar decoy/copper shot and Glimminge's crystal spear, leaving only safety telegraphs and HUD markers code-drawn.

`stormakt-oresund-background-v1.png` is the opaque Öresund ringbridge flight corridor. `stormakt-oresund-props-v1.png` supplies a transparent Kronspann, rail machinery and matched left/right armored bridge flaps; its untouched magenta generation is retained as the `-source.png` sibling. Their exact built-in generation prompts are versioned beside the PNGs. Kronspann and rail pieces are non-physical parallax; flap sprites are scaled to rectangles owned by `BridgeSectionState`, so rendering, ship collision and projectile cover all use the same dimensions. Missing assets retain the code-drawn fallback.

`stormakt-oresund-armored-train-v1.png` supplies the vertical crown-prowed locomotive, command wagon, reusable cannon wagon and compact buffer-crash wreck for the Pansartåg section. Runtime composes one locomotive, one command wagon and two cannon wagons from shared state anchors. Direct kills reuse the wreck at wagon scale; the environmental route uses it as the larger final crash. The untouched magenta source and exact built-in generation prompt are retained beside the production alpha PNG.

`stormakt-oresund-twin-fortress-v1.png` supplies intact and damaged Helsingör/Helsingborg halves for Öresunds Järnkrona. The two halves remain independent render and attack anchors while one shared health pool drives their three boss phases. The untouched magenta source and exact built-in generation prompt are retained beside the production alpha PNG; missing sprites retain the code-drawn fortress fallback.

`stormakt-oresund-guard-control-v1.png` replaces Öresund's last conspicuous combat placeholders with intact/aiming bridge-guard craft and intact/disabled railway control houses. The guard's existing radius remains authoritative for hits; the sprite only changes presentation. The control-house sprite is centered on the same `BridgeSectionState` coordinates used by shot resolution. Its untouched magenta source and exact built-in generation prompt are retained beside the production alpha PNG, with the previous code primitives preserved as fallbacks.

`stormakt-oresund-section-machinery-v1.png` supplies intact/damaged railway service carriages and idle/charged laser relays. The same relay family now represents both the first service-line trap and the double-flap beam source; their beams and hit logic keep the existing state anchors. `stormakt-oresund-crown-core-v1.png` supplies sealed, exposed, damaged and broken states for the Järnkrona's mechanical heart. Both sheets were created with built-in reference-guided image generation; untouched chroma-key sources and exact prompts are retained beside the production alpha PNGs.

`stormakt-oresund-train-coupling-v1.png` replaces the train's last yellow circle with intact/broken crown-jaw master couplings. `stormakt-oresund-boss-effects-v1.png` supplies two alternating hex-lattice crown shields plus staged fortress breach and collapse effects. The shield remains presentation over the existing authoritative protected-half state; the death effects replace only the old orange circles. Both retain their built-in generation prompts and untouched chroma-key sources.

`stormakt-oresund-laser-beams-v1.png` supplies a red warning filament and two alternating white-cyan active beam pulses. The first section, double-flap relay and twin-fortress crossfire all scale these assets between their existing state-owned endpoints. Enemy cannon and guard aim vectors were removed; aiming poses and muzzle glints carry that information without drawing lines to Karl.

`sfx/oresund-guard-shot.wav` and `sfx/oresund-switch-break.wav` began the fixed-seed Stable Audio 3 Small-SFX pass. The nine-effect Öresund set now adds the laser relay, armored flap motor, train rumble/crash, twin-fortress lock and crown-core open/break states. Runtime triggers each from its authoritative state transition rather than from rendering. Full raw generations, prompts, seeds and conversion commands are retained under `sfx/raw/` and in `sfx/oresund-sfx-v1.prompt.md`. Deterministic procedural alternatives use the `-procedural.wav` suffix and are not loaded while the generated masters exist.

`rts-swedish-buildings-v1.png`, `rts-swedish-units-v1.png` and `rts-danish-army-v1.png` supply Silverkroppen's complete first production-art pass. Their packed states cover working steam/crusher machinery, tower fire, Carolean volley/reload, moose charge/carbine, and ready/attack pairs for all five Danish troop families. Equal atlas cells are cropped independently before alpha trimming, so animation effects and neighboring units cannot leak into another sprite.

`rts-silver-miner-v1.png` supplies two empty and two silver-loaded walking phases for the visible crusher-to-Karl economy convoy. The four cells share one reference scale and fixed runtime canvas; delivered crates, rather than a hidden income timer, advance both spendable and salvaged silver.

`rts-toldhus-v1.png` supplies intact, open-gate, burning and wreck states for Møntgrevens mobile Danish tax fortress plus intact/broken royal seal machinery. The boss occupies the enlarged eastern front of Silverkroppen and cannot expose its core until both seal health pools are destroyed.

`rts-forest-props-v1.png` and `rts-danish-frontier-props-v1.png` replace Silverkroppen's triangle forest with sixteen deterministic environment families. Reset builds a stable prop field with a Swedish/neutral crystal-forest palette and a separate Danish customs-front palette. Root-anchored props participate in visual Y sorting; only the large physical families contribute navigation/build blockers, never combat hitboxes.

`rts-forest-floor-v1.png` is the opaque repeating needle/moss/root ground plate. `rts-silver-vein-v1.png` supplies glowing straight, curved, branch and node overlays along the deterministic ore curve. `rts-karl-landing-pad-v1.png` replaces the provisional rectangle beneath the landed command frigate. Large trees, boulders, ore outcrops, crates, carts and barricades now contribute deterministic movement/build blockers; small foliage and ground marks remain passable.

`stormakt-bridge-cannons-projectiles-v1.png` adds three bridge-collapse pieces, intact/destroyed Danish bridge cannon states, a detachable boss broadside cannon and red/white/gold enemy projectile families. Runtime uses the wreck pieces during the existing 45-frame collapse and mirrors the broadside module for the boss's right side.

Prompt summary:

- Player ship: Karl CCLV, ornate Swedish blue/yellow and brass steampunk spaceship.
- Hero motifs: three crowns, deep royal blue panels, yellow-gold trim, brass fittings, steam vents.
- Enemies: a fictional futuristic Danish royal navy in Dannebrog red/white, dark iron and restrained gold trim.
- Use: 16-bit top-down shmup sprite sheet concept.

Runtime note:

The EXT3 core can load `stormakt3020.wfsa`, a small raw sprite pack generated from both production sheets. If the pack or a named sprite is missing, the core falls back to code-drawn sprites.

Rebuild the pack after editing/replacing the concept sheet:

```sh
python tools/stormakt3020/build_assets.py
```

The builder packs named sprites including normal/hot player states, environment parts, combat-detail assets and separate 320- and 400-pixel-wide scrolling backgrounds. Smaller derived cannon/wreck sizes polish the boss attachments without duplicating source art. Each background is followed by its vertical mirror, making both wrap boundaries exact instead of exposing a hard seam. Active radio cards alternate their neutral and speaking frames every eight simulation frames. All former code-drawn backgrounds, faces, player ship, bridge pieces, cannons and projectiles remain missing-asset fallbacks. The builder trims alpha and downsamples with high-quality filtering; gameplay keeps separate deterministic hitboxes.

To rebuild the transparent generated sheet from its chroma-key source:

```sh
python ~/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py \
  --input assets/stormakt3020/stormakt-danish-boss-enemies-v1-source.png \
  --out assets/stormakt3020/stormakt-danish-boss-enemies-v1.png \
  --auto-key border --soft-matte --transparent-threshold 12 \
  --opaque-threshold 220 --despill
```

The radio portrait sheet uses the same command with `stormakt-radio-portraits-v1-source.png` as input and `stormakt-radio-portraits-v1.png` as output.

The Stora Bält environment sheet uses the same command with `stormakt-stora-balt-environment-v1-source.png` as input and `stormakt-stora-balt-environment-v1.png` as output.

To rebuild from another compatible sheet without changing the active default:

```sh
python tools/stormakt3020/build_assets.py --input path/to/sheet.png
```

`WFSA` is intentionally tiny: magic/version/count, then named ARGB8888 sprites. Runtime does not need PNG decoding.

The Silverkroppen dungeon extension adds versioned transparent sheets for Karl on foot, Gruva I terrain, twelve inventory items and reusable black-iron/Swedish-brass UI chrome. `dungeon-loot-v1.png` supplies the first six weapon families plus armor, ring and relic icons. `dungeon-ui-chrome-v1.png` supplies health/power orbs, slot/grid frames, Carolean equipment silhouette and stash crest. Their built-in image-generation prompts and chroma-key sources are preserved beside the runtime PNGs.

`dungeon-temple-act1-v2.png` replaces the first temple floor's dominant cross medallion with a quieter seamless field of small black basalt and iron slabs. The lower contrast keeps Karl, enemies, loot and telegraphs readable; the two gates, health tincture and Lemminkäinen shadow poses remain derived unchanged from the v1 source. The v1 sheet remains versioned for comparison and rollback.

`dungeon-temple-props-v1.png` furnishes the first sigil court with black-iron guardian statues, a low sacrificial altar and closed/open Tuonela-swan sarcophagi. They replace the reused mine clutter in the eastern court and have matching physical navigation blockers. The chroma-key source and exact built-in image-generation prompt are preserved beside the runtime sheet.

`dungeon-tuonela-swan-v1.png` introduces Svanen i Tuonela as a coherent black-iron threshold boss set: dormant funerary form, awakened idle, wing-sweep telegraph and neck strike. The third-sigil checkpoint initially uses the dormant and awakened states for its non-combat revelation; the attack states are packed for the following boss slice. Its chroma-key source and exact reference-guided built-in generation prompt are preserved beside it.

`dungeon-louhi-phase1-v1.png` supplies Louhi's idle, staff-casting and black-water teleport poses plus the silver altar used in her first boss phase. It is reference-guided from Louhi's established radio portrait, the Tuonela Swan sheet and the temple prop sheet; the untouched chroma-key source and exact built-in prompt are versioned beside the production alpha PNG.

`dungeon-louhi-iron-bird-v1.png` supplies Louhi's Järnfågel awakening, idle, wing-sweep and diving-strike poses for the second boss phase. Its crown, silver hair and black-iron body preserve Louhi's identity while the hooked beak and angular wings keep it distinct from Tuonela's swan. The untouched chroma-key source and exact reference-guided prompt are versioned beside the production alpha PNG.

`dungeon-karl-hammer-move-v1.png` and `dungeon-karl-hammer-attack-v1.png` replace Karl's sword silhouette whenever Gruvhammaren is equipped. The first sheet supplies idle and two-step movement from south, north and east; the second supplies direction-specific windup, grounded contact and weighted recovery. West reuses the east row by horizontal flip. Chroma-key sources and exact reference-guided prompts are versioned beside both production alpha PNGs.

`dungeon-louhi-ore-mother-v1.png` supplies Louhi's final altar-bound Malmmoder form: awakening, shielded, ore-vein casting and exposed-heart vulnerability. Its broad rooted base remains stationary while four interactive altar hearts are rendered by gameplay. The chroma-key source and exact reference-guided prompt are versioned beside the production alpha PNG.

`dungeon-temple-seal-fog-v1.png` replaces the old cyan/gold cross-line room dividers with four softly animated green-and-blood-red fog plumes. Runtime overlaps the alpha sprites into a continuous but porous magical seal; collision and progression remain unchanged.

`dungeon-ore-mother-ring-v1.png` is the unique permanent reward from Louhi's final form: black meteoric iron, old Swedish silver, three crowns and a green-red living ore rune. Its pickup begins the collapsing-temple escape through the deep lift, mounted moose run and launch of Karl CCLV.

`dungeon-karl-moose-escape-v1.png` gives the temple escape its own Karl-specific mounted ready and charge poses instead of reusing the helmeted RTS Carolean. `dungeon-moose-dismounted-v1.png` supplies the empty saddle after Karl jumps down and walks aboard Karl CCLV.

`dungeon-karl-moose-gallop-v2.png` replaces the single sliding charge pose with a three-frame landing, push-off and airborne stride. The packer identifies the three alpha-connected riders instead of cutting mathematical thirds, preventing legs from adjacent poses becoming loose debris. The ship remains parked while the moose visibly approaches and stops; the riderless dismount shot retains that exact screen anchor.

## Resolution

Stormakt defaults to a 400x280 logical framebuffer. Assets retain their native gameplay pixel size, providing 25 percent more field in both directions and making ships smaller relative to the world. WaylandForge scales the WFEX frame to its viewport. Set `WAYLANDFORGE_STORMAKT_LEGACY_320=1` to A/B test the original 320x224 field; the pack contains a correctly sized background for both modes.

## Music concept

`stormakt-over-oresund-v1.wav` is a 60-second instrumental ACE-Step concept for the main combat theme: a somber, grandiose D-minor march at 96 BPM with low strings, brass, field drums, timpani, organ and restrained harpsichord. The exact local generation request is preserved in `music-request.json`.

Audition it locally with:

```sh
pw-play assets/stormakt3020/stormakt-over-oresund-v1.wav
```

The EXT3 core starts the track automatically and streams bounded 48 kHz stereo F32LE chunks over WaylandForge's existing `WFAU` audio socket. It keeps roughly half a second buffered, handles partially accepted packets without replaying samples, retries harmlessly while the audio daemon is unavailable, crossfades the final half-second into the opening, and clears queued PCM when the core starts or stops.

Set `WAYLANDFORGE_STORMAKT_MUSIC=0` to disable music, `WAYLANDFORGE_STORMAKT_MUSIC_PATH` to audition another compatible 48 kHz stereo PCM16 WAV, or `WAYLANDFORGE_AUDIO_SOCKET` to use another audio-daemon socket.

Additional scored roles live under `music/`:

- `marsch-mot-kopenhamn-v1.wav`: somber menu and launch procession.
- `music/tre-kronors-jarnmarsch-v1.wav`: a faster, brass-led grand campaign menu march.
- `music/tre-kronors-jarnmarsch-loop-v1.wav`: the active 16-bar, 88 BPM menu loop derived from the new march.
- `music/skanska-skuggor-v1.wav`: dark nyckelharpa-led level theme for Skånska skuggor.
- `music/skanska-skuggor-loop-v1.wav`: the active 21-bar, 92 BPM level loop with the generated silent tail removed.
- `music/oresund-i-brand-v1.wav`: the active prototype score for Öresunds järnkrona until its dedicated 6/8 railway march lands.
- `music/silverkroppen-faltmarsch-v1.wav`: the preserved 60-second local generation for the RTS mission.
- `music/silverkroppen-faltmarsch-loop-v1.wav`: the active 16-bar, 84 BPM Silverkroppen field loop.
- `music/lemminkainen-gruva1-v1.wav`: active 64-second E-minor Gruva I exploration score; loaded when Silverkroppen mutates from RTS to dungeon play.
- `music/lemminkainen-djupgruva-v1.wav`: reserved 64-second D-minor Djupgruvan score.
- `music/lemminkainen-forbannad-gruva-v1.wav`: reserved 64-second F-sharp-minor cursed-mine score.
- `music/lemminkainen-tempel-v1.wav`: reserved 64-second C-sharp-minor temple score.
- `music/lemminkainen-flykt-v1.wav`: active 48-second D-minor, 116 BPM collapse-and-lift escape score, generated locally with fixed seed `30201661`.
- `music/lemminkainen-lattnad-v1.wav`: active 48-second G-major/E-minor, 92 BPM mounted escape and spacecraft departure score, generated locally with fixed seed `30201662`.
- `oresund-i-brand-v1.wav`: faster normal-combat loop.
- `kronans-sista-salva-v1.wav`: monumental boss loop.

`kronans-sista-salva-v1.wav` is the preserved 60-second generation; its final 12.09 seconds are effectively silent. `kronans-sista-salva-loop-v2.wav` is the active non-destructive edit: 40.000 seconds, exactly 14 four-beat bars at 84 BPM, cut before the generated fade. Rebuild it with `python tools/stormakt3020/build_music_loops.py`.

The loop edit is preloaded with the combat score. Kronens Tiende requests a 0.5-second in-stream crossfade at arrival, and restarting the stage requests the same crossfade back to the combat loop. Track-transition and loop positions advance only by audio frames accepted by the daemon. If loop-v2 is absent, runtime falls back to the preserved v1 generation.

`music/generation-manifest.json` records accepted task IDs, seeds, models, prompt files, and rejected attempts. The Lemminkäinen suite deliberately bypasses ACE-Step's LM caption rewrite (`thinking=false`) after the first worker attempt transformed the restrained mine request into a heroic orchestral fanfare. The complete local workflow is documented in `docs/stormakt3020-audio-toolchain.md`.

## Sound effects

The original five deterministic 48 kHz stereo effects are joined by nine Silverkroppen effects: construction, Carolean volley, moose charge, tower fire, raid horn, powder fuse/explosion, organ volley and unit-ready chime. Rebuild them with:

```sh
python tools/stormakt3020/build_sfx.py
python tools/stormakt3020/build_rts_sfx.py
```

The external core triggers them from actual gameplay events and mixes up to 32 voices into the music stream with headroom before sending 2048-frame `WFAU` packets.

## Radio voices

The campaign radio uses a two-stage local casting pipeline: VoxCPM2 creates fully synthetic reference performances for Ebba, Sören, Rasmus and Christian, then the selected local backend renders versioned Swedish or Danish dialogue from each reference WAV and exact transcript. Silverkroppen adds six event-driven Swedish/Danish calls for the silver landing, steam power, first royal claim, powder warning, organ wagon and moose readiness. The three older Matcha English files remain archived but are no longer loaded by runtime. Requests, raw output, job IDs, hashes and approval state are preserved under `radio/`; no known or third-party voice is cloned and none of these voices are final casting until listened to and approved.

Rebuild the 48 kHz stereo radio-filtered runtime files from the raw WAV files with:

```sh
python tools/stormakt3020/build_radio_voices.py
```

Create or refresh the synthetic casting references and Dots dialogue with:

```sh
python tools/stormakt3020/render_radio_cast.py references
python tools/stormakt3020/render_radio_cast.py lines
python tools/stormakt3020/build_radio_voices.py
```

Use repeated `--line LINE_ID --force` arguments to re-render selected dialogue without touching the rest of the cast.

The game keeps voice playback separate from ordinary effects and ducks music by about 6 dB while a radio line is active. Missing voice assets never suppress the deterministic subtitle card.
