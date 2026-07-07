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
- blit the core framebuffer through a dedicated emulator viewport
- track host frame timing and show FPS/frame milliseconds
- keep host update and UI rendering as separate steps
- track keyboard state for emulator-style buttons
- track Wayland pointer position/buttons
- expose clickable scale toggles in the custom UI
- draw buttons through a reusable custom UI/style layer
- draw panels/text through the same reusable UI layer
- provide dark-first theme variants and simple row/column layout helpers
- expose toolbar controls for pause/run, reset, and single-step
- close on ESC or compositor close

Current keyboard mapping:

- arrows: d-pad
- enter: start
- Z/X/C: A/B/C
- A/S/D: X/Y/Z
- 1/2/3: viewport scale mode fit/integer/stretch
- T: cycle UI theme
- mouse: hover/click the scale toggles
- mouse: use the toolbar buttons for pause/run, reset, and step
- ESC: quit

The reusable UI style skeleton lives in `SystemRegisIII.WaylandForge.Ui`: canvas, rectangles, pointer state, panels, text, row/column helpers, button colors, border thickness, padding, hover/active states, and click state are theme-driven so other WaylandForge apps can use the same low-level controls. Dark is the default theme.

The fake core draws a controllable blob so every mapped button has visible output before a real emulator core is wired in.

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
