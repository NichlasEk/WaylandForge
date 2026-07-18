# WFEX v2 Checkpoint 5 - compressed stream fallback

Checkpoint 5 adds one optional, lossless full-frame codec for v2 stream records. Raw ARGB8888 remains mandatory. Shared memory remains the preferred local transport; PACKRLE is useful when frames must cross a pipe or Unix socket.

## Codec choice

The probe sampled 120 real 400x280 Stormakt frames across a 600-frame run:

| Candidate | Wire size | Median encode | Median decode | Decision |
|---|---:|---:|---:|---|
| Deflate level 1 | 30.9% | 6.43 ms | 1.75 ms | Rejected: strong ratio, excessive latency. |
| Simple 16-bit run RLE | 77.4% | - | - | Rejected: insufficient reduction. |
| PACKRLE literal/run blocks | 54.4% | 0.484 ms | 0.313 ms | Selected. |

PACKRLE groups non-repeated pixels into bounded literal blocks and represents runs of three or more pixels with one token and one pixel. It needs no external library, uses reusable producer/host buffers, and has a small provable encoded upper bound.

## Negotiation and policy

- Capability bit 3 is `PACKED_RLE_FRAME_RECORDS`.
- `frame_codec` is `raw`, `prefer-packrle` or `require-packrle`.
- PACKRLE requires v2 and a streamed frame transport.
- Shared memory always carries raw mapped pixels.
- `prefer-shm` plus `prefer-packrle` chooses shared memory first and PACKRLE if shared setup is unavailable.
- `require-packrle` fails if the producer lacks bit 3 or shared memory was selected.

## Safety

The host validates dimensions and the decoded byte ceiling before allocating. It then bounds encoded bytes by `decodedBytes + 2 * ceil(pixelCount / 32768)`. Decode rejects truncated tokens, oversized counts, missing repeat values, decoded underflow and any stream that cannot produce exactly one full frame. Corrupt frames are stopped before presentation.

## Verification on 2026-07-18

- Three 600-frame raw/PACKRLE Stormakt comparisons produced identical framebuffer hashes.
- Median full host exchange was 1.957 ms/frame raw and 2.704 ms/frame PACKRLE in the final measured run.
- Stormakt used 54.4% of raw wire bytes; the deliberately noisy Dummy frame used 87.6%.
- Required-codec failure and corrupt-token rejection produced actionable errors.
- Shared-memory, latest-frame, v1 fallback, crash cleanup and reconnect regressions remained green.

The result is intentionally opt-in. It traded about 0.75 ms of local end-to-end frame time for roughly 45.6% fewer Stormakt stream bytes in the final run, while shared memory remains both faster and lower-copy on the same machine.
