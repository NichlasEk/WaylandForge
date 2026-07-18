# WayControlProtocol technical specification

Status: WCP 1.0 foundation and generic WaylandForge host mapping implemented

## Purpose

WayControlProtocol, abbreviated WCP, is WaylandForge's own boundary for gamepads, joysticks and similar controllers. It separates operating-system device access from controller normalization, user profiles and game actions.

WCP does not depend on SDL, GLFW, hidapi, libudev or a third-party game-controller database. The first backend talks directly to Linux evdev through the stable kernel userspace ABI and four libc system calls. The managed WCP contracts themselves are platform-neutral so later backends can be added without changing games or the WaylandForge input mapper.

WCP and WFEX solve different problems:

- WCP discovers controllers and produces normalized control events.
- WaylandForge maps those controls into host shortcuts and game actions.
- WFEX/WFIN transports the resulting game action state to an isolated core.
- A core never needs to know whether an action came from a keyboard, USB controller, Bluetooth controller or replay file.

## Architecture

```text
Linux evdev       future raw-HID       replay/test
     |                  |                  |
     +--------- IWayControlBackend --------+
                        |
                  WayControlHub
                        |
          device identity + WCP events
                        |
          profile, dead zone and mapper
                  (next checkpoint)
                        |
        ForgeInput / host shortcuts / WFIN
```

Backends own discovery and native handles. `WayControlHub` polls any number of backends and merges their events without making a game depend on a native device API. A backend reports connection, disconnection, button, axis and synchronization-loss events. Backend failure must not terminate another backend or the active game core.

## Linux and Bluetooth boundary

Linux exposes a paired Bluetooth gamepad as an evdev input device, normally under `/dev/input/event*`, just like a USB gamepad. WCP therefore reads gameplay input without calling BlueZ or linking a Bluetooth library. The descriptor's kernel bus id distinguishes USB (`0x0003`) from Bluetooth (`0x0005`).

Pairing, trust management and battery/vendor extensions are separate concerns. A future pairing module may speak to the operating system's Bluetooth service, but it is not part of the input event path and may not become a requirement for ordinary WCP operation. A controller paired outside WaylandForge must work immediately when its readable evdev node appears.

Users need read access to the relevant `/dev/input/event*` nodes. WCP must report permission failures clearly; it must not run the complete WaylandForge host as root.

## Canonical controls

WCP 1.0 names controller position rather than platform branding:

- face buttons: `South`, `East`, `West`, `North`;
- shoulders and digital trigger buttons;
- `Select`, `Start`, `Guide`, left-stick click and right-stick click;
- four digital D-pad directions;
- left and right X/Y axes;
- left and right analog triggers.

Buttons have value 0 or 1. Stick and trigger axes are normalized to signed 16-bit range `-32768..32767` in a 32-bit event field. The evdev backend applies the kernel-provided flat/dead zone before normalization. Profile-level dead zones and response curves belong to the mapper rather than the device backend.

Hat axes are converted to ordered release/press button events. A direction change from left to right releases left before pressing right. `SYN_DROPPED` becomes `SyncLost`; the mapper must discard held state for that device until a full snapshot is available.

## Device identity

Each device descriptor contains:

- stable WCP id;
- human-readable kernel name;
- bus, vendor, product and version ids;
- backend name;
- diagnostic native path.

The evdev backend derives a 128-bit printable id from a SHA-256 digest of kernel bus/id fields, unique string, physical path and name. The native event path is deliberately not the identity because its event number can change after reconnect or reboot.

Profiles should first match exact stable id, then bus/vendor/product, then a conservative default layout. Device names are display and fallback hints, not trusted unique identifiers.

## WCP packet header

WCP has an in-process contract and a versioned binary form for replay, process isolation and future remote controller modules. Every binary packet begins with this 32-byte little-endian header:

| Offset | Size | Field | Meaning |
| ---: | ---: | --- | --- |
| 0 | 4 | `magic` | `0x31504357`, bytes `WCP1` |
| 4 | 2 | `major` | incompatible protocol generation |
| 6 | 2 | `minor` | compatible feature revision |
| 8 | 2 | `packetType` | hello, welcome, device, input, snapshot or error |
| 10 | 2 | `flags` | type-specific flags |
| 12 | 4 | `packetSize` | header plus payload, minimum 32 |
| 16 | 8 | `sequence` | monotonic packet/event sequence |
| 24 | 8 | `timestampUs` | monotonic microseconds |

The implemented header codec rejects bad magic, major version zero and packet sizes smaller than the header. Payload codecs and transport negotiation land only after the in-process Linux path is exercised with real controllers; their layouts must then be added here before implementation.

Defined packet types are `Hello`, `Welcome`, `DeviceConnected`, `DeviceDisconnected`, `Input`, `Snapshot` and `Error`. Unknown packet types are skipped only when their declared size is within the negotiated maximum. No packet may cause an unbounded allocation.

## Current Linux backend

`LinuxEvdevBackend` currently:

- scans `/dev/input/event*` without libudev;
- filters out devices that do not expose joystick/gamepad buttons;
- opens nodes read-only, close-on-exec and non-blocking;
- reads Linux `input_event` batches directly;
- obtains axis limits and kernel flat values with `EVIOCGABS`;
- normalizes standard face, shoulder, menu, D-pad, stick and trigger controls;
- reports USB/Bluetooth bus and kernel identity metadata through sysfs;
- rescans for hotplug once per second;
- closes handles on unplug and disposal;
- never owns Bluetooth pairing or kernel driver behavior.

`SystemRegisIII.WayControlProbe` is the first consumer. It prints discovered devices and live normalized events, providing a hardware check before the WaylandForge mapper is modified.

## Module rules

1. Games consume actions, never evdev codes or branded button names.
2. Backends emit canonical physical controls, never game-specific actions.
3. Mapping profiles remain data and may be changed without rebuilding a backend.
4. Every device keeps independent held state and player assignment.
5. Hotplug must not clear keyboard input or another controller's state.
6. Disconnect and sync loss release every held control for that device.
7. Controller enumeration order must never be used as persistent identity.
8. Native errors are surfaced through diagnostics without crashing the active core.
9. Rumble, LEDs, gyro, touchpads and battery status are negotiated extensions, not assumptions.
10. Replay events use the same canonical controls and timestamps as live backends.

## Development checkpoints

### Checkpoint 1 - Protocol and direct evdev foundation

- Platform-neutral WCP device/event/backend contracts.
- Multi-backend hub.
- Versioned 32-byte binary header codec.
- Direct Linux evdev discovery, hotplug and normalized input.
- Standalone live probe.
- No external packages or controller libraries.

### Checkpoint 2 - WaylandForge mapper (generic path implemented)

- WCP is an input source beside the existing keyboard path.
- Device states merge without clearing keyboard or other controller state; disconnect and sync loss clear the affected device.
- D-pad and left stick feed directions through a conservative fixed threshold.
- The generic Saturn-style layout maps face buttons to A/B/X/Y and shoulders/triggers to C/Z.
- The existing input mapper has separate `KEY` and `WCP CONTROL` columns. Either binding can be captured or cleared without changing the other.
- Controller mappings persist in `[controller]` and `[controller.<core>]` TOML sections and inherit from the host defaults in the same way as keyboard profiles.
- Axis capture converts a deliberate stick movement into a mappable directional control such as `LeftStickLeft`.
- The input/debug windows show WCP status and the first connected controller.
- Device-specific presets, configurable axis thresholds and hardware validation remain.
- Every current keyboard mapping and deterministic WFEX input path remains intact.

### Checkpoint 3 - Real controller matrix

- Exercise at least one USB and one Bluetooth controller.
- Capture raw control maps for Xbox-style, PlayStation-style and one third-party pad when hardware is available.
- Add vendor/product profile data only where canonical Linux mappings differ.
- Verify reconnect, sleep/wake, simultaneous keyboard plus pad and two-controller isolation.
- Document the exact non-root input permission setup used by WaylandForge.

### Checkpoint 4 - Replay and process transport

- Finalize hello/welcome and payload layouts.
- Serialize descriptors, events and complete state snapshots.
- Add bounded local Unix-socket and recorded replay modules.
- Verify recorded WCP input reproduces the same Stormakt WFEX frame hashes.

### Checkpoint 5 - Optional controller features

- Output/rumble backend contract.
- Battery and connection metadata.
- Gyro, accelerometer and touch surface capability records.
- Pairing UI only if direct operating-system integration is still desirable.

Each optional family must be capability-driven. An ordinary digital/analog gamepad remains fully usable without implementing any optional feature.

## Immediate next step

Run the probe and WaylandForge with real hardware, correct any raw mapping differences exposed by the attached controllers, then add per-core controller profiles and configurable thresholds. Protocol transport, rumble and pairing remain outside the first gameplay path.
