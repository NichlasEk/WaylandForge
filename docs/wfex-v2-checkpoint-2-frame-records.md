# WFEX v2 Checkpoint 2 frame records

Date: 2026-07-18

## Delivered

Checkpoint 2 replaces the post-handshake v1 header with a versioned 64-byte `WFF2` record header. The handshake now requires `VERSIONED_FRAME_RECORDS` in addition to `RAW_FRAME_RECORDS`, so a producer or host from the transitional Checkpoint 1 implementation fails capability negotiation instead of misparsing the other version. The payload remains tightly packed raw ARGB8888, so protocol metadata can evolve independently of rendering and pixel parity.

The record declares header size, complete record size, codec, full-frame flags, visible dimensions, stride, payload bytes, frame index, presentation timestamp and nominal duration. Headers may grow from 64 to 4096 bytes in eight-byte increments. The host drains unknown bounded extension bytes before reading pixels.

Dummy and Stormakt choose v1 or v2 records from the negotiated session returned by the shared producer handshake. Their v2 timestamps are deterministic logical media time at a nominal 60 Hz. Direct v1 execution without a policy environment remains unchanged.

The host validates every v2 field before allocation, commits sequence state only after the complete payload arrives and accepts unsigned wrap from `ulong.MaxValue` to zero. Duplicate, skipped and out-of-order indices are fatal and report expected versus received. Restart creates a clean sequence epoch.

Diagnostics add record version, received-record count, sequence-error count, last media timestamp and nominal duration.

## Evidence

The conformance suite now passes 44 cases. New cases cover v2 roundtrip and fragmentation, bounded optional header extensions, magic/version/header size, codec, flags, dimensions, limits, stride, payload arithmetic, record length, duration, first/next/duplicate sequence behavior and unsigned wrap.

Four-frame direct producer comparisons show identical v1 and v2 pixel hashes at every index. The final sampled frames were:

```text
Dummy 320x224 frame 3             373ffaea1c27770e
Stormakt 400x280 frame 3          33acabfeca9e5e73
Stormakt legacy 320x224 frame 3   d2f5879a6320b46b
```

The `ExternalProcessCore` integration harness verifies:

```text
stormakt-v2 OK · V2.0 RAW LOCKSTEP · 400X280
probe-v1-fallback OK · V1 RAW LOCKSTEP · FALLBACK · 320X224
probe-require-v2 OK · actionable timeout
duplicate-sequence OK · actionable error
sequence-reconnect OK · tracker reset
```

The solution and explicit Stormakt Release target build with zero warnings and zero errors.

## Next boundary

Checkpoint 2 still transports every pixel through the pipe or socket and copies it into host presentation storage. Checkpoint 3 can now negotiate shared-memory slots without changing version discovery, raw parity records or sequence semantics at the control boundary.
