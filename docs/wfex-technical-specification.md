# WFEX technical specification

Status: documentation of the protocol implemented by the current WaylandForge host and external cores.

WFEX is WaylandForge's small binary framebuffer protocol for process-isolated game and emulator cores. It is not a video codec or a general network streaming protocol. A WFEX stream is a sequence of complete, uncompressed ARGB8888 frame records. Input travels in the opposite direction through one of the companion input formats described below.

The primary implementation references are:

- `src/SystemRegisIII.Host.WaylandForge/ExternalProcessCore.cs`, which starts external processes, transports input, validates WFEX records and presents frames;
- `src/SystemRegisIII.ExternalCore.Stormakt3020/Program.cs`, which implements the pointer-aware stdio producer used by Stormakt 3020;
- `src/SystemRegisIII.ExternalCore.Dummy/Program.cs`, which is the smallest reference producer;
- `src/SystemRegisIII.Core/WfexNegotiation.cs`, `WfexV2Frame.cs` and `WfexSharedMemory.cs`, which define the shared v2 wire and mapped-memory contracts;
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

## WFEX v2 negotiation

WFEX v2 begins with an optional producer-initiated handshake on an interactive stdio or Unix-socket channel. File/FIFO mode remains v1 because it has no bidirectional control channel. A current successful negotiation selects versioned `WFF2` frame records; a v1 policy or fallback continues using ordinary `WFEX` records.

The host policy is configured per external core through `protocol_policy`:

- `v1` sends input immediately and never waits for a hello;
- `prefer-v2` waits up to 2000 ms for a producer hello, then falls back to v1 only when no hello byte was received;
- `require-v2` fails with an actionable timeout when no hello arrives and never falls back.

The host exports the selected policy to a child process as `WAYLANDFORGE_WFEX_POLICY`. A v2-aware producer emits a hello only for `prefer-v2` or `require-v2`; an unchanged v1 producer ignores the variable and continues waiting for its normal first input packet. A partial, malformed or incompatible hello is always an error rather than a v1 fallback.

### Handshake sequence

```text
WaylandForge                         v2-aware producer
     |                                      |
     |<-- 48-byte WFX2 producer hello ------|
     | validate version/capabilities/limits |
     |--- 48-byte WFA2 host accept -------->|
     |                                      |
     |--- S, P, Q or WFIN input ----------->|
     |<-- WFF2 v2 frame record -------------|
```

Both records are exactly 48 bytes and little-endian:

| Offset | Size | Type | Field | Meaning |
|---:|---:|---|---|---|
| 0 | 4 | `uint32` | `magic` | Producer hello `0x32584657` (`WFX2`) or host accept `0x32414657` (`WFA2`). |
| 4 | 2 | `uint16` | `majorVersion` | Must be 2. |
| 6 | 2 | `uint16` | `minorVersion` | Currently 0; the host selects the lower supported minor. |
| 8 | 2 | `uint16` | `recordSize` | Must be 48. |
| 10 | 2 | `uint16` | `reserved` | Must be written as zero and ignored on read. |
| 12 | 8 | `uint64` | `requiredCapabilities` | Producer requirements; zero in the host response. |
| 20 | 8 | `uint64` | `capabilities` | Producer offer or host-selected intersection. |
| 28 | 4 | `uint32` | `maximumWidth` | Offered or selected positive maximum width. |
| 32 | 4 | `uint32` | `maximumHeight` | Offered or selected positive maximum height. |
| 36 | 4 | `uint32` | `maximumPayloadBytes` | Offered or selected positive decompressed payload ceiling. |
| 40 | 4 | `uint32` | `pixelFormats` | Offered or selected pixel-format bits. |
| 44 | 4 | `uint32` | `presentationModes` | Offered or selected presentation-mode bits. |

Capability bit 0 is `RAW_FRAME_RECORDS` and bit 1 is `VERSIONED_FRAME_RECORDS`; both are mandatory for the current v2 baseline. The second bit prevents a Checkpoint 1-era producer that still emits v1 headers after negotiation from being silently misparsed. Capability bit 2 is the optional `SHARED_MEMORY_SLOTS`. Pixel-format bit 0 is `ARGB8888`. Presentation bit 0 is `DETERMINISTIC_LOCKSTEP`; bit 1 is reserved for `LATEST_FRAME` but is not yet selected. Unknown required capability bits fail negotiation. Unknown optional bits are masked out.

The selected maximums are the component-wise minimum of the producer offer and host configuration. The mandatory Checkpoint 1 baseline is raw ARGB8888 in deterministic lockstep. The raw stream transport is therefore explicitly confirmed by capability while the already-open stdio or Unix socket determines the control transport.

## WFEX v1 frame record

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
| 12 | 4 | `int32` | `stridePixels` | Row stride expressed in pixels; WFEX v1 requires exactly `width`. |
| 16 | 8 | `uint64` | `frameIndex` | Producer-owned monotonically increasing frame identifier. |
| 24 | 4 | `int32` | `byteCount` | Pixel payload length. The current host requires `width * height * 4`. |
| 28 | 4 | `int32` | `reserved` | Written as zero by current producers and ignored by the host. |

The record length implemented today is therefore:

```text
32 + (width * height * 4) bytes
```

### Effective stride restriction

WFEX v1 formally requires tightly packed rows. Before the v2 safety checkpoint the host accepted a padded stride in the header while still allocating and reading only `width * height` pixels. That half-supported state has been removed: the common v1 parser now rejects `stridePixels != width` before allocation.

Current producers must use tightly packed rows:

```text
stridePixels == width
```

Supporting padded rows in the future requires an explicitly negotiated v2 capability and validation against `stridePixels * height`, while still copying only `width` visible pixels per row.

## WFEX v2 frame record

After a successful v2 handshake, Checkpoint 2 producers emit a 64-byte `WFF2` header followed by the same tightly packed raw ARGB8888 payload used by v1. The pixels therefore remain byte-identical while metadata becomes explicit and extensible.

| Offset | Size | Type | Field | Meaning |
|---:|---:|---|---|---|
| 0 | 4 | `uint32` | `magic` | `0x32464657` (`WFF2`). |
| 4 | 2 | `uint16` | `majorVersion` | Must be 2. |
| 6 | 2 | `uint16` | `minorVersion` | Currently 0. |
| 8 | 2 | `uint16` | `headerSize` | 64 through 4096, divisible by 8. |
| 10 | 2 | `uint16` | `flags` | Bit 0 `FULL_FRAME` is mandatory. |
| 12 | 4 | `uint32` | `payloadCodec` | 1 is raw ARGB8888. |
| 16 | 4 | `int32` | `width` | Positive visible width within negotiated limits. |
| 20 | 4 | `int32` | `height` | Positive visible height within negotiated limits. |
| 24 | 4 | `int32` | `stridePixels` | Currently must equal `width`. |
| 28 | 4 | `int32` | `payloadBytes` | Must equal `width * height * 4`. |
| 32 | 8 | `uint64` | `frameIndex` | Monotonic frame sequence, modulo `uint64`. |
| 40 | 8 | `uint64` | `presentationTimestampNs` | Producer media time in nanoseconds. |
| 48 | 8 | `uint64` | `nominalDurationNs` | Positive nominal frame duration. |
| 56 | 8 | `uint64` | `recordBytes` | Must equal `headerSize + payloadBytes`. |

Bytes from offset 64 through `headerSize - 1` are optional header extensions. A minor-version reader validates the bounded declared size, reads the complete header and skips unknown extension bytes before consuming the payload. Unknown extensions therefore cannot be confused with pixels. The current producers use `headerSize == 64`, logical timestamps `frameIndex * 16,666,667 ns` and a nominal 60 Hz duration of `16,666,667 ns`.

The host validates codec, flags, dimensions, checked payload arithmetic, negotiated limits, declared record length and nominal duration before allocation. It accepts the first frame index of a new connection, then requires each complete record to increment by one. Wrap from `uint64.MaxValue` to zero is valid. Duplicates, gaps and out-of-order indices stop the core with the expected and received values; reconnect/reset starts a new sequence.

## WFEX v2 shared-memory transport

`frame_transport` selects `raw`, `prefer-shm` or `require-shm` independently of `protocol_policy`. Shared memory requires a v2 interactive stdio or Unix-socket connection. `prefer-shm` negotiates raw v2 when the producer lacks the capability or when the host cannot create the region before sending its accept. `require-shm` fails instead. Once a shared setup has been selected and acknowledged, transport failure is fatal rather than silently switching record formats mid-stream.

```toml
[external_core3]
protocol_policy = "prefer-v2"
frame_transport = "prefer-shm"
shared_memory_directory = "" # empty selects /dev/shm or the runtime temp directory
```

The raw v2 control channel remains open for input, lifecycle, setup and 16-byte frame notifications. Pixel payloads use a two-slot file-backed mapping. At 400x280 this reduces producer-to-host control output from 448,064 bytes per frame to 16 bytes and removes the host's intermediate framebuffer copy: `FrameStore` copies directly from the immutable reading slot.

### Setup record

After the 48-byte host accept, the host writes a 48-byte `WFS2` setup header followed by a UTF-8 path:

| Offset | Size | Type | Field | Meaning |
|---:|---:|---|---|---|
| 0 | 4 | `uint32` | `magic` | `0x32534657` (`WFS2`). |
| 4 | 2 | `uint16` | `headerSize` | Must be 48. |
| 6 | 2 | `uint16` | `slotCount` | Must be 2. |
| 8 | 4 | `uint32` | `controlSize` | Must be 64. |
| 12 | 4 | `uint32` | `slotHeaderSize` | Must be 64. |
| 16 | 4 | `uint32` | `slotStrideBytes` | `64 + maximumPayloadBytes`. |
| 20 | 4 | `uint32` | `maximumPayloadBytes` | Must not exceed negotiated payload limits. |
| 24 | 8 | `uint64` | `regionBytes` | `64 + slotCount * slotStrideBytes`. |
| 32 | 4 | `uint32` | `pathBytes` | 1 through 1024. |
| 36 | 4 | `uint32` | `reserved` | Zero on write, ignored on read. |
| 40 | 8 | `uint64` | `nonce` | Random session identity repeated in the mapping. |

The host creates the backing file with mode `0600` in `/dev/shm`, falling back to the runtime temporary directory when necessary. A configured `shared_memory_directory` may override it. The producer requires the random WaylandForge filename prefix, rejects symbolic links, checks the exact file and region size plus absence of group/other permissions, then validates the control block and nonce.

After validating and mapping, the producer unlinks the pathname and sends the fixed eight-byte `WFSA` acknowledgement. The host also attempts the unlink idempotently. Both mappings remain valid through their open handles. If the host dies in the narrow interval before the producer maps, the next host creation removes dead-PID WaylandForge regions; live-PID mappings are never swept.

### Region and slot layout

The 64-byte control block contains `WFSM`, layout version 1, both fixed structure sizes, payload/region sizes and the nonce. It is followed by two equal slots:

```text
64-byte control
64-byte slot 0 header + maximum payload
64-byte slot 1 header + maximum payload
```

Each slot header stores state, publication sequence, frame index, media timestamp, nominal duration, width, height, stride and payload size. Slot states are:

```text
FREE -> WRITING -> READY -> READING -> FREE
```

The producer waits for its alternating slot to become `FREE`, marks it `WRITING`, copies pixels and metadata, publishes `READY` after a memory barrier and sends a `WFR2` notification. The notification is 16 bytes: magic, size, slot index and 64-bit publication sequence. The host verifies state and sequence, marks `READING`, validates metadata as a normal v2 raw frame, presents directly from the mapped pixel span and finally releases the slot to `FREE`.

Both frame index and slot publication sequence are checked. Two occupied slots apply bounded backpressure; a timeout does not consume a publication sequence. A reconnect creates a new random region and resets both sequence epochs.

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

WFEX v1 has no pixel-format negotiation. A v2 handshake confirms ARGB8888, which remains the only supported format.

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

### Controller-aware `Q` step packet

Stormakt 3020 also accepts a 29-byte `Q` packet. It preserves the merged action bitfield while identifying controller-owned actions and carrying WCP's normalized left stick without reducing it to four digital buttons:

| Offset | Size | Type | Field |
|---:|---:|---|---|
| 0 | 1 | byte | ASCII `Q` (`0x51`) |
| 1 | 4 | `uint32` | Merged keyboard/controller action bitfield |
| 5 | 4 | `uint32` | Actions currently owned by WCP controllers |
| 9 | 2 | `int16` | Normalized left-stick X, `-32768..32767` |
| 11 | 2 | `int16` | Normalized left-stick Y, `-32768..32767` |
| 13 | 4 | `int32` | Pointer X in core framebuffer coordinates |
| 17 | 4 | `int32` | Pointer Y in core framebuffer coordinates |
| 21 | 4 | `uint32` | Pointer button bitfield |
| 25 | 4 | `uint32` | Pointer-inside flag |

The WaylandForge host selects `Q` only for the Stormakt pointer driver. Other stdio cores retain their existing `S` packets, and Stormakt continues accepting old `S` and `P` recordings unchanged.

## Stdio lockstep behavior

Stdio mode is request/response lockstep:

```text
WaylandForge                         external core
     |                                   |
     |--- S or P input packet ---------->|
     |                                   | simulate one step
     |                                   | render one frame
     |<-- 32-byte v1 or 64-byte v2 header|
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

Stormakt 3020 currently uses `stdio` with the pointer driver `stormakt_rts`. Its loop performs the following work for every `Q` packet (and retains old `S`/`P` decoding):

1. Read the marker and then the remaining 28 bytes exactly.
2. Decode keyboard/controller actions, analog stick and pointer state.
3. Call `StormaktGame.Step(buttons, pointer, controller)` once.
4. Call `StormaktGame.Render(frame, frameIndex)` once.
5. Populate the 32-byte v1 or 64-byte v2 WFEX header selected by negotiation.
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

All v1 stdio, file/FIFO and Unix-socket readers use the same `WfexFrameHeader` parser. Negotiated v2 streams use `WfexV2FrameHeader`; both paths validate before framebuffer allocation:

- the `WFEX` magic;
- positive width and height;
- configurable maximum width and height, defaulting to 8192 by 8192;
- `stridePixels == width`;
- checked `width * height * sizeof(uint)` arithmetic;
- `byteCount == width * height * sizeof(uint)` and a configurable payload ceiling, defaulting to 256 MiB;
- successful reading of the complete payload.

The v2 path additionally validates its declared header/record size, payload codec, full-frame flag, nominal duration and frame sequence. Diagnostics expose received v2 records, sequence errors, last media timestamp and nominal duration.

The limits are configured independently under each `external_core` section with `max_frame_width`, `max_frame_height` and `max_frame_bytes`. The shared record reader handles fragmented reads, rejects a truncated synchronous record and gives non-blocking file-like streams a bounded no-data retry window. A transport whose underlying `Read` blocks must still supply its own timeout, as the Unix-socket path does.

In synchronous stdio and socket reads, a short stream is an error. File/FIFO mode may retain and present the last complete frame while waiting for more data.

On an invalid header, invalid dimensions or transport exception, the host records the message as `LastError`, adds it to the external core's stderr/status tail, stops the core and propagates the error to the host UI. If the process exits, automatic relaunch is blocked until the user explicitly requests restart.

These checks prevent malformed headers from causing arithmetic wraparound or an unbounded framebuffer allocation. WFEX remains a trusted local protocol: it does not authenticate a producer, resynchronize a damaged byte stream or impose a universal execution deadline on a producer blocked inside simulation.

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

- pixel format other than ARGB8888;
- compression or delta-frame encoding;
- dirty rectangles or partial frame updates;
- checksum or corruption recovery;
- stream resynchronization after an invalid or dropped byte;
- audio payload;
- network authentication, encryption or congestion control;
- endianness negotiation.

The v1 reserved header word creates room for a limited compatible extension, but substantial changes use the explicit versioned v2 header rather than silently changing the existing 32-byte contract.

## Security and deployment boundary

WFEX should presently be treated as a trusted local IPC format. Frame allocations are bounded before allocation and sockets use transport read timeouts, but a producer can still stall its own stdio or blocking-FIFO exchange or continuously force process restarts. The Unix socket must remain local and access-controlled.

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
10. select `S`, `P`, Stormakt's host-side `Q`, or WFIN according to the configured transport and input requirements.

A v2 producer additionally sends its hello before waiting for ordinary input, honors the host-selected capability intersection, emits `WFF2` records for raw v2, and enters shared-memory setup only when `SHARED_MEMORY_SLOTS` was selected. It must never send both a raw frame record and a shared notification for the same negotiated session.

Following those rules is enough to make a software-rendered external core appear as a normal WaylandForge viewport without sharing a graphics context or loading the core into the host process.
