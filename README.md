# WaylandForge

Low-level Wayland host experiments for SystemRegisIII.

M1 is still intentionally narrow:

- connect to the Wayland display
- bind `wl_compositor`, `wl_shm`, `wl_seat`, and `xdg_wm_base`
- create `wl_surface`, `xdg_surface`, and `xdg_toplevel`
- allocate double-buffered `wl_shm` ARGB8888 framebuffers
- resize the shm framebuffers from `xdg_toplevel.configure`
- repaint only from Wayland frame callbacks / buffer release
- render a small custom software UI from managed code
- run a fake Saturn core that produces a 320x224 ARGB8888 framebuffer
- run an opt-in external process core over a tiny stdin/stdout ARGB8888 frame protocol
- blit the core framebuffer through a dedicated emulator viewport
- track host frame timing and show FPS/frame milliseconds
- keep host update and UI rendering as separate steps
- track keyboard state for emulator-style buttons
- track Wayland pointer position/buttons
- expose clickable scale toggles in the custom UI
- draw buttons through a reusable custom UI/style layer
- draw panels/text through the same reusable UI layer
- provide dark-first theme variants and simple row/column layout helpers
- keep persistent widget state by `UiId`
- expose collapsible UI sections and a tiny style-inspector section
- expose text boxes with focus, cursor, numeric/password options, and basic editing
- expose clipped scroll areas with wheel input and a scrollbar
- expose a native WaylandForge file picker for ROM/file selection
- expose toolbar controls for pause/run, reset, single-step, ROM picker, and settings
- expose an EXT toolbar toggle that switches between the in-process fake core and the dummy external core process
- expose external-core status, restart, exit state, and stderr tail in the debug panel
- persist UI defaults/state through a small repo-local TOML configuration layer
- close on ESC or compositor close

Current keyboard mapping:

- arrows: d-pad
- enter: start
- Z/X/C: A/B/C
- A/S/D: X/Y/Z
- 1/2/3: viewport scale mode fit/integer/stretch
- T: cycle UI theme
- text boxes: click to focus, type, backspace, enter to submit
- mouse wheel: scroll clipped panels
- ROM toolbar button: open the file picker
- mouse: hover/click the scale toggles
- mouse: use the toolbar buttons for pause/run, reset, step, ROM, and settings
- EXT toolbar button: toggle the external process dummy core
- ROM toolbar button: toggles the ROM picker open/closed
- Super+Shift in tiled mode: drag the internal tile split to resize in X/Y, drag the Settings title toward an edge to dock left/right/top/bottom
- ESC: quit

The reusable UI style skeleton lives in `SystemRegisIII.WaylandForge.Ui`: canvas, rectangles, pointer state, panels, text, row/column helpers, button colors, border thickness, padding, hover/active states, and click state are theme-driven so other WaylandForge apps can use the same low-level controls. Dark is the default theme.

The fake core draws a controllable blob so every mapped button has visible output before a real emulator core is wired in.

## External Core Protocol

`SystemRegisIII.ExternalCore.Dummy` is a deliberately tiny process-isolated core target. The host starts it as a separate `dotnet` process and speaks over stdin/stdout:

- host writes: `S` byte followed by little-endian `uint32` Saturn button bits
- core writes: `WFEX` magic, `int32 width`, `int32 height`, `int32 stride`, `uint64 frameIndex`, `int32 byteCount`, reserved `int32`
- core then writes `byteCount` bytes of ARGB8888 pixels

This keeps GPL or other external code out of the WaylandForge process while still letting the host present frames and inject input.

External cores are configured in TOML:

```toml
[external_core]
command = "" # empty uses the built-in dummy external core
args = ""
working_directory = ""
```

When `EXT` is enabled, the debug panel shows process status, the selected command, last host-side fault, a restart button, and the latest stderr lines.

For a first OpenTyrian lifecycle probe, build OpenTyrian without networking and point the external core at the process probe:

```sh
cd /home/nichlas/opentyrian
make WITH_NETWORK=false
```

```toml
[external_core]
command = "dotnet"
args = "src/SystemRegisIII.ExternalCore.ProcessProbe/bin/Debug/net8.0/SystemRegisIII.ExternalCore.ProcessProbe.dll --target /usr/bin/env --cwd /home/nichlas/opentyrian -- SDL_VIDEODRIVER=dummy /home/nichlas/opentyrian/opentyrian"
working_directory = "/home/nichlas/WaylandForge"
```

The probe does not capture OpenTyrian video yet. It starts the target as a separate process, relays its stdout/stderr to WaylandForge, and renders a simple status framebuffer over the same `WFEX` protocol. The `SDL_VIDEODRIVER=dummy` example avoids opening a separate SDL window during lifecycle testing.

## UI Config

Default UI configuration lives in `config/waylandforge.ui.toml`. Runtime changes are written to `config/waylandforge.ui.local.toml`, which is gitignored. The config currently persists theme, viewport scale, internal window mode, z-order, open state, floating window rectangles, and the tiled Settings split and dock side.

## Run

```sh
dotnet run --project src/SystemRegisIII.Host.WaylandForge
```

Requirements:

- Wayland session with `WAYLAND_DISPLAY` set
- `wayland-client`
- `wayland-protocols`
- `wayland-scanner`
- C compiler available as `cc`
