# WFEX v2 Checkpoint 3 shared memory

Date: 2026-07-18

## Delivered

Checkpoint 3 adds negotiated two-slot shared memory behind capability bit `SHARED_MEMORY_SLOTS`. `frame_transport` supports `raw`, `prefer-shm` and `require-shm`; Stormakt defaults to `prefer-shm`, while the existing native OpenTyrian and Raptor configurations remain raw v1.

The host creates a mode-0600 file-backed mapping and sends its bounded layout and random nonce over the existing control channel. The producer validates, maps and unlinks it before acknowledgement; the host repeats the unlink idempotently. The mapping survives while both endpoints hold it. A dead-PID stale file from a host killed before producer mapping is removed during the next host creation, while live-PID mappings are preserved.

Two fixed slots use explicit `FREE`, `WRITING`, `READY` and `READING` states. Publication and frame sequences are separately checked. Slot reuse is bounded and a timeout cannot skip a publication sequence. The producer publishes only after pixels and metadata are complete; the host releases only after `FrameStore` has copied the immutable mapped span.

Raw v2 still sends a 64-byte header plus the complete payload. Shared mode sends a 16-byte notification and no pixel payload through the pipe. At 400x280 that removes 448,048 control-channel bytes per frame and one full 448,000-byte host staging copy. At 60 Hz the raw output is approximately 26.88 MB/s, versus 960 bytes/s of frame notifications plus input on the shared control channel.

## Evidence

The conformance suite passes 64 cases. Shared-memory coverage includes optional capability selection versus raw masking, exact setup serialization, checked setup arithmetic, mode-0600 permissions and rejection of broader modes, nonce/control validation through producer open, immediate unlink with live mappings, dead-PID stale cleanup, invalid slot rejection, notification and acknowledgement validation, metadata and pixel parity, alternating slots, bounded two-slot backpressure and no sequence loss after timeout.

The host integration harness verifies both bundled v2 producers and lifecycle behavior:

```text
stormakt-shm OK · SHM X2 + CONTROL · V2 · RX 4 · SEQERR 0
dummy-shm OK · SHM X2 + CONTROL · V2 · RX 4 · SEQERR 0
shared-restart OK · fresh mapping
shared-cleanup OK · no linked backing file
producer-crash OK · mapping closed · restart negotiated
prefer-shm fallback OK · raw v2
require-shm failure OK · actionable error
```

A five-pass 400x280 Stormakt comparison alternated raw/shared run order and hashed all 3000 sampled frame pairs identically. Each pass measured 600 warmed lockstep frames:

```text
RAW v2 median   1.850 ms/frame   samples 1.814, 1.816, 1.850, 1.980, 2.024
SHM x2 median   1.449 ms/frame   samples 1.164, 1.404, 1.449, 1.452, 1.476
```

The median improved complete lockstep roundtrip time by about 22 percent while removing the raw pixel stream and host staging copy. Timing varies with system load, so the durable acceptance evidence is exact 3000-frame parity, bounded slot ownership and the structural copy/byte reduction rather than a single percentage.

The diagnostics panel displays selected transport, received frames, sequence errors, media timing and cumulative payload bytes kept out of the raw stream/staging path.

## Next boundary

Shared memory still operates in deterministic request/response lockstep. Checkpoint 4 can add `latest-frame` pacing and drop telemetry using the proven slot states, without changing raw parity mode or silently dropping simulation input.
