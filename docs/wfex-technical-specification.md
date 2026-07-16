# WFEX technical specification

Status: documentation of the protocol implemented by the current WaylandForge host and external cores.

WFEX is WaylandForge's small binary framebuffer protocol for process-isolated game and emulator cores. It is not a video codec or a general network streaming protocol. A WFEX stream is a sequence of complete, uncompressed ARGB8888 frame records. Input travels in the opposite direction through one of the companion input formats described below.

The primary implementation references are:

- `src/SystemRegisIII.Host.WaylandForge/ExternalProcessCore.cs`, which starts external processes, transports input, validates WFEX records and presents frames;
- `src/SystemRegisIII.ExternalCore.Stormakt3020/Program.cs`, which implements the pointer-aware stdio producer used by Stormakt 3020;
- `src/SystemRegisIII.ExternalCore.Dummy/Program.cs`, which is the smallest reference producer;
- `src/SystemRegisIII.Host.WaylandForge/FrameStore.cs`, which copies a received frame into the host's packed framebuffer.

## Design goals

WFEX deliberately keeps the integration boundary narrow:

- an external core remains in its own process and address space;
- the core does not need to link WaylandForge, Wayland, SDL, OpenGL or Vulkan;
- the host receives the exact final framebuffer and can hash or capture it deterministically;
- implementations can be written in any language capable of binary stream I/O;
- code with a different license can remain outside the WaylandForge process;
- the same frame record works over standard output, a file/FIFO or a Unix socket.

The protocol currently prioritizes local bring-up, determinism and simple debugging over bandwidth efficiency.

## Byte order and integer representation

All multibyte values are little-endian. Signed fields use ordinary two's-complement `int32`. Boolean values in input packets are encoded as `uint32`, where zero is false and one is true.

The frame magic is defined numerically as `0x58454657`. When serialized little-endian, its bytes are:

```text
57 46 45 58    W F E X
```

The socket input magic is `0x4e494657`, serialized as:

```text
57 46 49 4e    W F I N
```

## WFEX frame record

Each record consists of a fixed 32-byte header followed immediately by one complete pixel payload.

```text
+-----------------------+
| 32-byte WFEX header   |
+-----------------------+
| ARGB8888 pixel data   |
| width * height * 4 B  |
+-----------------------+
```

### Header layout

| Offset | Size | Type | Field | Current meaning |
|---:|---:|---|---|---|
| 0 | 4 | `uint32` | `magic` | Must be `0x58454657` (`WFEX`). |
| 4 | 4 | `int32` | `width` | Visible width in pixels; must be positive. |
| 8 | 4 | `int32` | `height` | Visible height in pixels; must be positive. |
| 12 | 4 | `int32` | `stridePixels` | Row stride expressed in pixels; must be at least `width`. |
| 16 | 8 | `uint64` | `frameIndex` | Producer-owned monotonically increasing frame identifier. |
| 24 | 4 | `int32` | `byteCount` | Pixel payload length. The current host requires `width * height * 4`. |
| 28 | 4 | `int32` | `reserved` | Written as zero by current producers and ignored by the host. |

The record length implemented today is therefore:

```text
32 + (width * height * 4) bytes
```

### Effective stride restriction

The header has a stride field and the host accepts `stridePixels >= width`, but the current host also requires `byteCount == width * height * 4` and allocates exactly `width * height` pixels. A padded payload using `stridePixels * height` is consequently not supported end to end.

Current producers must use tightly packed rows:

```text
stridePixels == width
```

Supporting padded rows in the future requires the host to validate and allocate against `stridePixels * height`, while still copying only `width` visible pixels per row.

## Pixel format

Each pixel is represented logically as a 32-bit integer in `0xAARRGGBB` form:

```text
bits 31..24  alpha
bits 23..16  red
bits 15..8   green
bits 7..0    blue
```

Examples:

```text
0xffff0000  opaque red
0xff00ff00  opaque green
0xff0000ff  opaque blue
0xff061018  opaque dark blue-black
```

Because integers are serialized little-endian, the bytes of `0xAARRGGBB` appear on the wire as `BB GG RR AA`. A producer should construct 32-bit ARGB values and serialize them little-endian rather than assuming a platform-native byte layout on a big-endian system.

The current protocol has no pixel-format negotiation. ARGB8888 is mandatory.

## Frame sizes and bandwidth

The header overhead is negligible compared with the raw framebuffer.

| Resolution | Pixel payload | Complete record | Approximate payload at 60 Hz |
|---|---:|---:|---:|
| 320 x 224 | 286,720 B | 286,752 B | 17.2 MB/s |
| 400 x 280 | 448,000 B | 448,032 B | 26.9 MB/s |

These rates are reasonable through local pipes and Unix sockets. They are not intended for an internet-facing transport.

## Stdio control protocol

In `stdio` mode, WaylandForge starts the core with redirected standard input, standard output and standard error.

- stdin carries a step command and current input;
- stdout carries only WFEX records;
- stderr carries diagnostic text and is collected separately by the host.

Logging or any other text on stdout corrupts frame alignment and causes an invalid-magic failure. External cores must log exclusively to stderr.

### Basic `S` step packet

The original command is five bytes:

| Offset | Size | Type | Field |
|---:|---:|---|---|
| 0 | 1 | byte | ASCII `S` (`0x53`) |
| 1 | 4 | `uint32` | Saturn-compatible button bitfield |

The button bits currently shared through `SystemRegisIII.Core.SaturnButtons` are:

| Bit | Action |
|---:|---|
| 0 | Escape |
| 1 | Up |
| 2 | Down |
| 3 | Left |
| 4 | Right |
| 5 | Start |
| 6 | A |
| 7 | B |
| 8 | C |
| 9 | X |
| 10 | Y |
| 11 | Z |
| 19 | Developer save |
| 20 | Developer load |

The physical keyboard or controller bindings belong to the WaylandForge input profile. The external core receives actions, not host key names.

### Pointer-aware `P` step packet

Stormakt 3020 uses a self-describing 21-byte extension so the same process can support shooter, RTS and dungeon input:

| Offset | Size | Type | Field |
|---:|---:|---|---|
| 0 | 1 | byte | ASCII `P` (`0x50`) |
| 1 | 4 | `uint32` | Button bitfield |
| 5 | 4 | `int32` | Pointer X in core framebuffer coordinates |
| 9 | 4 | `int32` | Pointer Y in core framebuffer coordinates |
| 13 | 4 | `uint32` | Pointer button bitfield |
| 17 | 4 | `uint32` | Pointer-inside flag |

The leading marker lets a core determine the exact packet length before reading the rest. A legacy `S` reader consumes five bytes; a pointer-aware reader consumes either five or 21 bytes according to the marker.

WaylandForge maps pointer coordinates from the scaled viewport back into the core's native framebuffer before writing this packet.

## Stdio lockstep behavior

Stdio mode is request/response lockstep:

```text
WaylandForge                         external core
     |                                   |
     |--- S or P input packet ---------->|
     |                                   | simulate one step
     |                                   | render one frame
     |<-- 32-byte WFEX header -----------|
     |<-- complete pixel payload --------|
     |                                   |
     |--- next input packet ------------>|
```

The host blocks until it has read the complete header and payload. The core waits for another command before stepping again. This gives useful properties:

- a frame corresponds directly to one input sample;
- the simulation cannot silently run ahead of presentation;
- pipe backpressure bounds memory use without a frame queue;
- recorded input streams can reproduce exact frame indices and hashes;
- a slow core reduces the achieved frame rate instead of accumulating latency.

The corresponding limitation is that a core stalled in simulation or rendering stalls the host's external-core step as well.

## Stormakt 3020 exchange

Stormakt 3020 currently uses `stdio` with the pointer driver `stormakt_rts`. Its loop performs the following work for every `P` packet:

1. Read the marker and then the remaining 20 bytes exactly.
2. Decode buttons and pointer state.
3. Call `StormaktGame.Step(buttons, pointer)` once.
4. Call `StormaktGame.Render(frame, frameIndex)` once.
5. Populate the 32-byte WFEX header.
6. Write the complete header and framebuffer to stdout.
7. Flush stdout and increment `frameIndex`.

The framebuffer is normally 400 x 280. Setting `WAYLANDFORGE_STORMAKT_LEGACY_320=1` selects 320 x 224 before the process enters its command loop.

This boundary is one reason Stormakt's simulation is portable: gameplay produces a plain pixel array and does not call Wayland, SDL or a GPU API.

## Unix socket mode and WFIN

`wfcore_socket` is the richer transport used by external emulator bring-up. WaylandForge creates a Unix socket, publishes its path through `WFCORE_SOCKET`, starts the process and accepts the core's connection.

Frames remain ordinary WFEX records. Input uses the fixed 48-byte WFIN header implemented by the current host:

| Offset | Size | Type | Field | Meaning |
|---:|---:|---|---|---|
| 0 | 4 | `uint32` | `magic` | `0x4e494657` (`WFIN`). |
| 4 | 4 | `int32` | `headerSize` | Currently 48. |
| 8 | 4 | `uint32` | `buttons` | Saturn-compatible action bits. |
| 12 | 4 | `uint32` | `rawKeyCode` | Host raw key code, or zero. |
| 16 | 8 | `uint64` | `lastFrameIndex` | Last WFEX frame accepted by the host. |
| 24 | 4 | `uint32` | `rawKeySerial` | Serial associated with the raw key event. |
| 28 | 4 | `uint32` | `rawKeyPressed` | One for press, zero for release/no event. |
| 32 | 4 | `int32` | `pointerX` | Pointer X in core coordinates. |
| 36 | 4 | `int32` | `pointerY` | Pointer Y in core coordinates. |
| 40 | 4 | `uint32` | `pointerButtons` | Pointer button bitfield. |
| 44 | 4 | `uint32` | `pointerInside` | One when the pointer is inside the core viewport. |

Unlike stdio lockstep, socket mode can reuse the last valid frame if no new socket data is available during a host render pass. The initial frame is required within the host's startup timeout.

`lastFrameIndex` gives a socket core enough information to reason about which output the host has accepted, although the current protocol does not define acknowledgements, retransmission or a formal pacing algorithm.

## WFEX file/FIFO mode

In `wfex_file` mode, the external process writes consecutive WFEX records to the configured path. The host opens that path as a read stream and attempts to consume complete records.

This mode is useful when an existing program can export its framebuffer at a convenient presentation point but cannot yet implement bidirectional integration. The OpenTyrian bring-up path used this approach around its screen-present function.

If no new complete frame is available after at least one valid frame has been received, WaylandForge presents the previous frame again. For seekable streams, an incomplete read restores the prior stream position so the same partial record can be retried.

Input is not carried by the WFEX file itself. A producer that needs interactive input should normally use stdio or `wfcore_socket`.

## Host validation and failure behavior

For each record, `ExternalProcessCore` currently validates:

- the `WFEX` magic;
- positive width and height;
- `stridePixels >= width`;
- `byteCount == width * height * sizeof(uint)`;
- successful reading of the complete payload.

In synchronous stdio and socket reads, a short stream is an error. File/FIFO mode may retain and present the last complete frame while waiting for more data.

On an invalid header, invalid dimensions or transport exception, the host records the message as `LastError`, adds it to the external core's stderr/status tail, stops the core and propagates the error to the host UI. If the process exits, automatic relaunch is blocked until the user explicitly requests restart.

The current checks are sufficient for trusted local cores but are not a hardened parser for untrusted data. In particular, a future hardened version should impose explicit maximum dimensions and payload sizes before allocating memory.

## Deterministic capture and testing

WFEX is useful as a test artifact because it exposes the exact software-rendered output. A deterministic test can:

1. start a core with a known configuration and seed;
2. send a recorded sequence of `S` or `P` packets;
3. read complete WFEX records;
4. select known `frameIndex` values;
5. hash the pixel payloads;
6. compare repeated runs, resolutions or implementations.

Stormakt uses this pattern to check that level dispatch, timed encounters and rendering produce identical frames for identical input. The frame header itself should normally be excluded or normalized if a test wants to compare only pixels.

## Minimal producer pseudocode

```text
frameIndex = 0

while read_step_packet(input):
    buttons, pointer = decode_input_packet()
    simulation.step(buttons, pointer)
    pixels = simulation.render_argb8888()

    write_u32_le(output, 0x58454657)       # WFEX
    write_i32_le(output, width)
    write_i32_le(output, height)
    write_i32_le(output, width)            # packed stride
    write_u64_le(output, frameIndex)
    write_i32_le(output, width * height * 4)
    write_i32_le(output, 0)                # reserved
    write_argb8888_le(output, pixels)
    flush(output)

    frameIndex += 1
```

A correct implementation must handle partial reads and writes. A single stream call is not guaranteed to transfer the requested number of bytes, especially for sockets and FIFOs.

## Current non-goals and limitations

WFEX currently has no:

- protocol version or capability negotiation;
- pixel format other than ARGB8888;
- compression or delta-frame encoding;
- timestamps, durations or refresh-rate declaration;
- dirty rectangles or partial frame updates;
- checksum or corruption recovery;
- stream resynchronization after an invalid or dropped byte;
- formal maximum width, height or allocation limit;
- audio payload;
- network authentication, encryption or congestion control;
- endianness negotiation.

The reserved header word creates room for a limited compatible extension, but substantial changes should introduce an explicit versioned header rather than silently changing the existing 32-byte contract.

## Security and deployment boundary

WFEX should presently be treated as a trusted local IPC format. A malformed producer can request large allocations, block while withholding payload bytes or continuously force process restarts. The Unix socket must remain local and access-controlled.

For remote play, browser delivery or internet transport, a separate host-facing layer should provide validation, pacing, authentication and an appropriate compressed media or application protocol. Stormakt's planned WebAssembly host would bypass WFEX serialization entirely and call the shared simulation/rendering core directly, while retaining WFEX for desktop compatibility and deterministic parity tests.

## Compatibility rules for new cores

A new producer compatible with the current host should:

1. emit only binary WFEX data on its selected frame transport;
2. send logs to stderr, never stdout in stdio mode;
3. serialize every multibyte value little-endian;
4. use positive, reasonably bounded dimensions;
5. use `stridePixels == width`;
6. emit exactly `width * height * 4` payload bytes;
7. encode pixels as `0xAARRGGBB` integers;
8. increment `frameIndex` monotonically;
9. loop on partial reads and writes;
10. select `S`, `P` or WFIN according to the configured transport and pointer requirements.

Following those rules is enough to make a software-rendered external core appear as a normal WaylandForge viewport without sharing a graphics context or loading the core into the host process.
