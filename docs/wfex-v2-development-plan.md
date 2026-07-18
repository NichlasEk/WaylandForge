# WFEX v2 development plan

Status: planned, not started

Scope: WaylandForge host and process-isolated desktop cores

Compatibility rule: existing WFEX v1 producers must continue to work unchanged

## Purpose

WFEX v2 should make local external cores safer, cheaper to present and easier to evolve without weakening the two properties that make WFEX useful today: a very small integration boundary and deterministic full-frame captures.

The primary performance target is local process isolation. Stormakt 3020 currently emits a complete tightly packed ARGB8888 image for every logical frame. At 400x280 this is 448,000 bytes per frame, or about 26.9 MB/s at 60 frames per second, before counting copies between producer, kernel, host and presentation buffer. A shared-memory frame path can remove most of those copies while the existing raw stream remains available for compatibility and tests.

This work is deliberately independent of Stormakt gameplay. The browser/WebAssembly port is also outside the protocol path: a browser host should call the shared simulation and renderer directly rather than serialize frames through WFEX.

## Goals

- Preserve byte-for-byte WFEX v1 support.
- Reject malformed dimensions and payload sizes before allocating memory.
- Negotiate protocol version, transport and optional capabilities explicitly.
- Add a low-copy local shared-memory transport.
- Decouple simulation input from presentation pacing when requested.
- Preserve a deterministic lockstep mode with exact framebuffer hashes.
- Make frame timing, format and dropped-frame behavior observable.
- Introduce every feature behind an independently testable checkpoint.

## Non-goals

- Replacing WebRTC, browser media delivery or remote-play protocols.
- Carrying authenticated or encrypted traffic over the internet.
- Mixing audio into the framebuffer stream.
- Requiring compression for small local software-rendered games.
- Removing the current stdio, FIFO or Unix-socket v1 paths.
- Making dirty rectangles the default. Scrolling games often change most pixels, so partial-frame bookkeeping can cost more than it saves.

## Compatibility model

The current 32-byte `WFEX` header remains WFEX v1 and never changes meaning. Its reserved word must not be repurposed into an extension that an old reader could misinterpret.

A v2 connection begins with a distinct, fixed-size handshake record before any frame records. The handshake contains its own magic, protocol major/minor version, record/header sizes and capability bitsets. A host that has not negotiated v2 continues parsing ordinary `WFEX` records exactly as it does today. Configuration may explicitly require v1, prefer v2 with v1 fallback, or require v2.

Major versions change wire compatibility. Minor versions may add capabilities or record types that are ignored unless both sides advertise them. Unknown mandatory capabilities fail negotiation with a precise error; unknown optional capabilities are masked out.

## Proposed v2 capabilities

### Required baseline

- Little-endian integer encoding.
- ARGB8888 full-frame records.
- Explicit maximum width, height, stride and payload.
- Monotonic frame index.
- Presentation timestamp and nominal frame duration.
- Declared record size so future readers can skip understood extensions safely.

### Optional capabilities

- Shared-memory frame slots.
- Two or three frame slots selected during negotiation.
- `latest-frame` presentation mode, where stale completed frames may be dropped.
- Deterministic lockstep mode, where one input step yields one retained frame.
- Additional pixel formats only after a real core requires them.
- Compressed full-frame payloads for socket/FIFO transport.
- Checksums for recorded files and diagnostic streams.

Dirty rectangles and delta frames remain experimental capability bits until measurements show a benefit. They must always have a full-frame recovery path and may not become a prerequisite for a compliant v2 core.

## Shared-memory design direction

The control channel remains stdio or a local Unix socket. Negotiation creates or passes an access-controlled shared-memory region containing a small control block followed by two or three equal frame slots.

Each slot contains immutable metadata for one published frame plus its pixel storage. The producer writes only to a free slot, completes the pixels, publishes metadata with release ordering and then signals the host through the control channel or an event primitive. The host acquires a completed slot, presents it, and returns it to the free state. Producer and host must never write the same slot concurrently.

The initial implementation should favor correctness over clever lock-free behavior. A small explicit slot state machine and sequence numbers are preferable to implicit ownership. On producer death, timeout or invalid sequence, the host discards the region and falls back or restarts according to the existing external-core policy.

The negotiated maximum frame dimensions determine slot size once. A producer may submit smaller visible dimensions within that allocation, but may never grow beyond it without renegotiating.

## Presentation modes

### Deterministic lockstep

The host sends one input state and waits for the corresponding frame index. No completed frame is skipped. This remains the default for parity tests, WFEX capture tools and debugging.

### Latest frame wins

The simulation may continue while the host presents the newest completed slot and releases older completed slots. Input packets carry the last observed simulation and presentation indices so telemetry can distinguish simulation delay from display delay.

This mode needs bounded queues and an explicit drop counter. It may drop presentation work, never simulation input silently. The host must keep displaying the last valid frame if the producer temporarily has no new frame.

## Development checkpoints

### Checkpoint 0 - Baseline and safety limits

1. Record current v1 throughput, allocations, copy count and frame latency for the dummy core and Stormakt at 320x224 and 400x280.
2. Add checked arithmetic for `width * height * sizeof(uint)`.
3. Add configurable hard maximum dimensions, stride and payload size before allocation.
4. Unify validation shared by stdio, FIFO and socket readers.
5. Decide whether padded v1 stride is formally rejected or implemented end to end; do not retain the current half-supported state.
6. Add malformed-header, overflow, truncated-payload and stalled-producer tests.

Acceptance: all existing v1 cores and deterministic hashes remain unchanged, invalid inputs fail without large allocation or host hang, and the baseline report is committed.

### Checkpoint 1 - Version and capability handshake

1. Specify the exact handshake and capability bit layout in the technical specification.
2. Implement `v1`, `prefer-v2` and `require-v2` host policies.
3. Add producer and host negotiation state machines with bounded timeouts.
4. Negotiate limits, pixel format, presentation mode and transport.
5. Expose the negotiated protocol and capabilities in the WaylandForge diagnostics UI.

Acceptance: an unchanged v1 producer works under `prefer-v2`; a v2 producer negotiates raw ARGB full frames; mismatched mandatory capabilities fail with an actionable error.

### Checkpoint 2 - Versioned raw-frame records

1. Add frame timestamp, nominal duration, visible dimensions, stride, payload codec and flags.
2. Support unknown optional record extensions through declared record size.
3. Add sequence validation, clean reconnect behavior and diagnostic counters.
4. Keep raw ARGB8888 as the mandatory codec and parity reference.

Acceptance: raw v2 output matches v1 framebuffer hashes at every sampled frame and survives reconnect, partial read/write and deliberate record fragmentation tests.

### Checkpoint 3 - Shared-memory transport

1. Implement negotiated region creation and permission-safe handle transfer.
2. Implement two-slot ownership first, then optionally measure three slots.
3. Add release/acquire publication rules and sequence checks.
4. Add cleanup for normal shutdown, producer crash, host crash and stale socket paths.
5. Retain the control channel for input, lifecycle and error messages.

Acceptance: Stormakt and the dummy core run through shared memory with identical deterministic hashes, no torn frames, bounded memory, correct crash cleanup and materially fewer copied framebuffer bytes than raw v1.

### Checkpoint 4 - Presentation pacing

1. Implement selectable lockstep and latest-frame modes.
2. Add bounded completed-frame handling and explicit drop counters.
3. Track simulation index, presented index, producer time, host receive time and presentation time.
4. Display latency, stalls and dropped frames in diagnostics.

Acceptance: lockstep remains bit-deterministic; an intentionally slow host stays responsive in latest-frame mode; no input packet is silently discarded.

### Checkpoint 5 - Optional compressed stream fallback

1. Measure raw socket cost before selecting a codec.
2. Prototype one fast lossless codec for full ARGB frames.
3. Negotiate codec and maximum decompressed size.
4. Reject corrupt or oversized payloads before presentation.
5. Compare CPU time, latency and byte reduction against raw records.

Acceptance: compression is shipped only if measurements show a useful win for a real non-shared-memory transport. Raw ARGB remains mandatory and selectable.

### Checkpoint 6 - Documentation and migration

1. Update `wfex-technical-specification.md` with the final binary layouts.
2. Add minimal producer examples for v1, v2 raw and v2 shared memory.
3. Add a protocol conformance test tool and malformed-stream corpus.
4. Document feature detection, fallback and troubleshooting.
5. Migrate cores one at a time; never require a flag day.

Acceptance: a third-party producer can implement the protocol from the specification, the conformance tool reports useful failures, and all bundled v1/v2 cores pass CI.

## Test matrix

Every protocol checkpoint should cover:

- Linux stdio, FIFO and Unix-socket v1 transports.
- v2 raw socket and shared memory when those capabilities land.
- 320x224 and 400x280 Stormakt frames plus a synthetic maximum-size frame.
- Short reads, short writes, fragmented headers and delayed payloads.
- Invalid magic, negative and overflowing dimensions, excessive stride and payload mismatch.
- Producer exit during header, payload and published shared-memory slot.
- Host exit with a live producer and subsequent clean restart.
- Frame-index wrap policy and out-of-order or duplicated sequence numbers.
- Exact deterministic framebuffer hashes in lockstep mode.
- Slow-host behavior and visible drop telemetry in latest-frame mode.

## Performance evidence required

Each optimization checkpoint records the same measurements so results stay comparable:

- framebuffer bytes produced and copied per second;
- allocations and allocated bytes per frame;
- producer render time;
- transport time;
- host presentation latency;
- CPU usage for producer and host;
- frames simulated, received, presented and dropped;
- p50, p95 and worst observed latency over a fixed run.

Shared memory is considered successful only if it reduces framebuffer copying without increasing frame-time instability or weakening crash isolation. Compression is considered successful only if its byte reduction outweighs codec CPU and latency on a measured target.

## Recommended implementation order

Start with Checkpoint 0 and commit it independently. Then land handshake plus raw v2 before shared memory so negotiation and error handling can be tested without debugging two new mechanisms at once. Shared memory is the first expected performance feature. Presentation decoupling follows after frame ownership is proven. Compression remains optional and measurement-driven.

Until this plan is explicitly resumed, WFEX v1 remains the production protocol and Stormakt development continues against the existing deterministic path.
