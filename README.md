# WaylandForge

Low-level Wayland host experiments for SystemRegisIII.

M0 is intentionally narrow:

- connect to the Wayland display
- bind `wl_compositor`, `wl_shm`, `wl_seat`, and `xdg_wm_base`
- create `wl_surface`, `xdg_surface`, and `xdg_toplevel`
- allocate a `wl_shm` ARGB8888 framebuffer
- render a moving gradient from managed code
- repaint from Wayland frame callbacks
- close on ESC or compositor close

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
