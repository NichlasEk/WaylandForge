# WFEX v2 Checkpoint 4 - presentation pacing

Checkpoint 4 adds a negotiated `latest-frame` mode without changing the deterministic default. It is available only with WFEX v2 shared memory; v1 and raw-stream sessions stay in lockstep.

## Contract

- The producer hello offers both `DETERMINISTIC_LOCKSTEP` and `LATEST_FRAME`; the host selects exactly one.
- `presentation_mode = "lockstep"` remains the default for every configured external core.
- `latest-frame` requires negotiated shared-memory slots and fails clearly if raw transport is forced.
- The producer simulation advances at 60 Hz independently of the host render cadence.
- Incoming input uses a bounded 256-packet channel with wait/backpressure semantics. Packets are not silently discarded.
- A background host reader validates and releases every published slot, retaining only the newest completed framebuffer for presentation.
- Replacing an unpresented staging frame increments the visible drop counter.

## Diagnostics

The input panel now reports:

- latest simulated frame index;
- latest presented frame index;
- deliberately dropped presentation frames;
- receive-to-present latency in milliseconds;
- the existing received-frame, sequence-error, media-time and avoided-stream-byte counters.

## Verification on 2026-07-18

- Full Release solution build: zero warnings and zero errors.
- Explicit Stormakt Release build: zero warnings and zero errors.
- WFEX conformance: `WFEX CONFORMANCE OK - 68 CASES`.
- Slow-host integration: after a deliberate 250 ms host pause, the dummy producer reached frame 15, the host presented frame 15, and 14 superseded presentations were counted as drops.
- Stormakt slow-host integration reached frame 16, presented frame 16 and counted 15 superseded presentations without stalling simulation.
- Existing raw/shared parity: five runs of 600 Stormakt frames remained hash-identical.
- Median integration time in the final run was 1.689 ms/frame for raw and 1.399 ms/frame for lockstep shared memory.
- Producer-crash cleanup, shared-memory restart, prefer-shared fallback, require-shared failure, v1 fallback, require-v2 timeout and sequence-reconnect tests all passed.

Stormakt remains configured for lockstep. Latest-frame can therefore be exercised deliberately without changing current gameplay or replay timing.
