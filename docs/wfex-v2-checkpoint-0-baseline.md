# WFEX v2 Checkpoint 0 baseline

Date: 2026-07-18

Host: Intel Xeon E5-2697 v3, x86_64, Linux 7.1.3, .NET SDK 10.0.110

## Result

Checkpoint 0 establishes a safe WFEX v1 compatibility floor before adding a v2 handshake. The host now has one common header parser and record reader for stdio, file/FIFO and Unix-socket transports. Dimensions, tightly packed stride, checked payload arithmetic and payload size are validated before allocation.

Default limits are 8192 by 8192 pixels and 256 MiB payload per frame. Each external core may override them with `max_frame_width`, `max_frame_height` and `max_frame_bytes`. Padded v1 stride is formally rejected; all bundled producers already emit `stridePixels == width`.

The conformance executable covers 15 cases: valid and fragmented records, short headers, bad magic, zero or negative dimensions, configured dimension and payload limits, padded stride, payload mismatch, arithmetic overflow, reserved-field compatibility, truncated payload, bounded no-data polling and throwing parser behavior.

## Microbenchmark

Command:

```sh
dotnet run --project src/SystemRegisIII.WfexConformance/SystemRegisIII.WfexConformance.csproj -c Release -- --benchmark
```

Observed result:

```text
WFEX CONFORMANCE OK · 15 CASES
HEADER PARSE · 80.9 NS · 0 B ALLOC
COPY 320X224 · 12.82 GB/S · 0.022 MS/FRAME
COPY 400X280 · 17.00 GB/S · 0.026 MS/FRAME
```

The parser reuses existing buffers and allocates no bytes per parsed header. Once a resolution is established, the host also reuses its frame arrays. In the v1 path a complete frame still crosses the producer-to-kernel and kernel-to-host boundary and is copied once more into `FrameStore`; that is the copy cost v2 shared memory is intended to reduce.

## End-to-end raw stdio baseline

The Release producers were warmed for 60 frames and then measured over 1200 lockstep exchanges. Each measurement writes one five-byte `S` input packet, reads one 32-byte WFEX header and drains its complete pixel payload.

```text
Dummy 320x224       474.6 roundtrips/s   2.107 ms/frame   136.1 MB/s wire
Stormakt 320x224   1286.4 roundtrips/s   0.777 ms/frame   368.9 MB/s wire
Stormakt 400x280   1000.9 roundtrips/s   0.999 ms/frame   448.5 MB/s wire
```

These are unthrottled producer-render-plus-pipe roundtrips, not display latency. At 60 Hz, raw pixels account for approximately 17.2 MB/s at 320x224 and 26.9 MB/s at 400x280, plus the small frame header and input packet. The dummy's trigonometric full-frame renderer is deliberately heavier than Stormakt and should not be interpreted as transport-only cost.

The measurements show that parsing and a single in-memory copy are small compared with the frame budget, while raw transport still moves every pixel through several buffers. Consequently the first performance-oriented v2 target remains negotiated shared-memory frame slots, not parser optimization or mandatory compression.

## Compatibility evidence

The existing Stormakt deterministic capture hashes remained unchanged after the WCP input extension immediately preceding this checkpoint. Checkpoint 0 changes only host-side validation and rejects no bundled producer. A full Release solution build and all 15 conformance cases complete with zero warnings and zero errors.
