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
- track raw keyboard up/down state and map it to emulator-style actions
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
- close on compositor close

Default keyboard mapping:

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
- INPUT toolbar button: open the input mapper
- Super+Shift in tiled mode: drag the internal tile split to resize in X/Y, drag the Settings title toward an edge to dock left/right/top/bottom
- ESC: mapped input action, not host quit

The reusable UI style skeleton lives in `SystemRegisIII.WaylandForge.Ui`: canvas, rectangles, pointer state, panels, text, row/column helpers, button colors, border thickness, padding, hover/active states, and click state are theme-driven so other WaylandForge apps can use the same low-level controls. Dark is the default theme.

The fake core draws a controllable blob so every mapped button has visible output before a real emulator core is wired in.

## External Core Protocol

`SystemRegisIII.ExternalCore.Dummy` is a deliberately tiny process-isolated core target. The host can start external cores as separate processes and speak either the original stdin/stdout probe protocol or the newer bounded Unix socket protocol.

The legacy stdio probe is:

- host writes: `S` byte followed by little-endian `uint32` Saturn button bits
- core writes: `WFEX` magic, `int32 width`, `int32 height`, `int32 stride`, `uint64 frameIndex`, `int32 byteCount`, reserved `int32`
- core then writes `byteCount` bytes of ARGB8888 pixels

The socket mode is intended for real bringup work. The host creates `socket_path`, starts the process with `WFCORE_SOCKET=<path>`, and exchanges bounded messages:

- host writes `WFIN`: `uint32 magic`, `int32 headerSize`, `uint32 Saturn button bits`, reserved, `uint64 lastFrameIndex`, reserved
- core writes `WFEX`: the same 32-byte frame header and ARGB8888 payload used by the stdio probe
- future audio should use the same socket as bounded PCM chunks, not a growing file

This keeps GPL or other external code out of the WaylandForge process while still letting the host present frames and inject input.

External cores are configured in TOML:

```toml
[external_core]
mode = "stdio" # stdio | wfex_file | wfcore_socket
command = "" # empty uses the built-in dummy external core
args = ""
working_directory = ""
env = ""
wfex_path = "/tmp/waylandforge-opentyrian.wfex"
socket_path = "/tmp/waylandforge-wfcore.sock"
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

The local `/home/nichlas/opentyrian` checkout also has an opt-in `OPENTYRIAN_WFEX_PATH` exporter patch. It writes real OpenTyrian `JE_showVGA()` frames as `WFEX` records to a file or FIFO:

```sh
cd /home/nichlas/opentyrian
SDL_VIDEODRIVER=dummy OPENTYRIAN_WFEX_PATH=/tmp/opentyrian.wfex OPENTYRIAN_WFEX_MAX_FRAMES=2 ./opentyrian
```

That requires the Tyrian 2.1 freeware data files in OpenTyrian's expected data path. Without them, OpenTyrian exits before the first game frame and reports the missing files through stderr.
For this local setup, the downloaded freeware data lives in `/home/nichlas/opentyrian/data`, which is ignored by the OpenTyrian checkout.

To show those exported frames in WaylandForge, switch the external core to file-reader mode:

```toml
[external_core]
mode = "wfcore_socket" # stdio | wfex_file | wfcore_socket
command = "/home/nichlas/WaylandForge/local/opentyrian-wfcore/opentyrian"
args = ""
working_directory = "/home/nichlas/WaylandForge/local/opentyrian-wfcore"
env = "OPENTYRIAN_WFCORE=1;SDL_VIDEODRIVER=dummy"
wfex_path = "/tmp/waylandforge-opentyrian.wfex"
socket_path = "/tmp/waylandforge-wfcore.sock"
```

Then run WaylandForge and press `EXT`. The host starts the local ignored OpenTyrian WF core copy, creates the Unix socket, applies `env`, reads bounded `WFEX` frames, sends `WFIN` input state, and presents the core viewport. To point at another external target, change only this TOML block. The `local/` tree is gitignored so GPL experiment code stays out of the WaylandForge repository until it is intentionally split or licensed as a separate component.

## UI Config

Default UI configuration lives in `config/waylandforge.ui.toml`. Runtime changes are written to `config/waylandforge.ui.local.toml`, which is gitignored. The config currently persists theme, viewport scale, internal window mode, z-order, open state, floating window rectangles, tiled layout, audio volume, and input bindings.

Input mappings live under `[input]`. The `INPUT` toolbar window edits the same values, one action at a time. A binding can also be edited manually as a comma-separated list, for example:

```toml
[input]
start = "enter,space"
a = "z"
b = "x"
x = "a"
```

Those names map to Wayland/Linux key codes today. Future gamepad support should feed the same action names instead of adding emulator-specific hardmapping.

## Audio Daemon Prototype

`tools/waylandforge-audiod` is the first low-level PipeWire audio experiment. It creates a playback node named `EutherAudio Sinklet`, keeps a small F32 stereo ringbuffer, and listens on `/tmp/waylandforge-audio.sock` for a simple `PLAY_TEST` command or bounded `WFAU` PCM chunks.

```sh
cd tools/waylandforge-audiod
make
./waylandforge-audiod
```

In another terminal:

```sh
python -c 'import socket; s=socket.socket(socket.AF_UNIX); s.connect("/tmp/waylandforge-audio.sock"); s.sendall(b"PLAY_TEST\n"); print(s.recv(128).decode(), end="")'
pw-top
```

## Run

```sh
dotnet run --project src/SystemRegisIII.Host.WaylandForge
```

Or start the host together with the PipeWire audio daemon:

```sh
./start-waylandforge.sh
```

Requirements:

- Wayland session with `WAYLAND_DISPLAY` set
- `wayland-client`
- `wayland-protocols`
- `wayland-scanner`
- C compiler available as `cc`
