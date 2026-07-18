# WFEX producer integration and troubleshooting

This guide is the shortest path from a software-rendered framebuffer to a WaylandForge external core. The normative byte layouts and limits are in [WFEX technical specification](wfex-technical-specification.md). A compilable implementation is in [`examples/SystemRegisIII.WfexProducerExample`](../examples/SystemRegisIII.WfexProducerExample/Program.cs).

## Choose the smallest compatible mode

| Producer | Host policy | Frame transport | Presentation | Use when |
|---|---|---|---|---|
| v1 | `v1` | `raw` | `lockstep` | Existing producer or file/FIFO exporter. |
| v2 raw | `require-v2` | `raw` | `lockstep` | Testing negotiation and versioned records without shared memory. |
| v2 shared | `require-v2` | `require-shm` | `lockstep` | Normal low-copy local process integration. |
| v2 shared latest | `require-v2` | `require-shm` | `latest-frame` | Simulation must continue when host presentation is slow. |

Use `prefer-v2` and `prefer-shm` during migration. They retain v1/raw fallback. Use `require-*` in conformance tests so an accidental fallback cannot look like success.

```toml
[external_core]
mode = "stdio"
command = "dotnet"
args = "/absolute/path/to/producer.dll v2-shm"
working_directory = "/absolute/path/to/project"
protocol_policy = "require-v2"
frame_transport = "require-shm"
presentation_mode = "lockstep"
shared_memory_directory = ""
max_frame_width = 1920
max_frame_height = 1080
max_frame_bytes = 8294400
```

## Feature detection

1. Read `WAYLANDFORGE_WFEX_POLICY` before consuming the first input packet.
2. With `v1`, do not emit a handshake; wait for normal input and return v1 `WFEX` records.
3. With `prefer-v2` or `require-v2`, emit the 48-byte `WFX2` hello and read the complete 48-byte `WFA2` response.
4. Use only capabilities, limits, pixel format and presentation mode selected by the response.
5. Emit `WFF2` raw records unless `SHARED_MEMORY_SLOTS` was selected.
6. If shared memory was selected, read `WFS2`, validate/map the region, send `WFSA`, then publish slots plus `WFR2` notifications.

A partial or malformed handshake is fatal. It is never safe to reinterpret handshake bytes as a v1 frame. Once a session selects shared memory, it must not switch to raw records without reconnecting.

## Build and exercise the example

```sh
dotnet build examples/SystemRegisIII.WfexProducerExample -c Release
dotnet run --project src/SystemRegisIII.WfexConformance -c Release
```

The example exposes `v1`, `v2-raw` and `v2-shm` modes. Configure the matching host policy from the table above, select the external core in WaylandForge and check the WFEX diagnostics panel. The example writes all diagnostics through exceptions/stderr; stdout remains binary-only.

## Troubleshooting

| Symptom | Likely cause | Check or fix |
|---|---|---|
| `Invalid WFEX ... magic` | Text/logging on stdout or wrong mode. | Send logs only to stderr; verify producer and host both expect handshake or both expect v1. |
| v2 hello timeout | Producer ignored `WAYLANDFORGE_WFEX_POLICY` or waited for input first. | Emit `WFX2` before reading `S`, `P`, `Q` or `WFIN`. |
| truncated handshake/record | A single read/write was assumed complete. | Loop until every fixed header and payload byte has transferred. |
| unsupported baseline | Missing raw/versioned capability, ARGB8888 or lockstep offer. | Always offer the mandatory baseline even when shared/latest is supported. |
| shared-memory setup rejected | Wrong size/nonce, broad permissions, symlink or stale path. | Use the exact `WFS2` values and a private `0600` regular file. Do not invent a pathname. |
| shared slot timeout | Host stopped consuming or a slot was not released. | Inspect process state and `SEQERR`; restart the session rather than changing transport mid-stream. |
| latest-frame rejected | Raw or v1 transport was selected. | Select v2 plus `require-shm`/`prefer-shm`, or return to lockstep. |
| duplicate/out-of-order sequence | Frame index was reused or publication order changed. | Increment only after successful publication and reset sequence state on reconnect. |
| dimensions/payload rejected | Overflow, padded stride or limit mismatch. | Require `stride == width`, checked `width * height * 4`, and limits no larger than the host response. |
| core exits and does not relaunch | Host intentionally blocks crash loops. | Read `LastError`/stderr, correct the producer, then press restart. |

## Migration status

| Bundled producer | v1 | v2 raw | v2 shared | latest-frame | Role |
|---|:---:|:---:|:---:|:---:|---|
| Dummy | yes | yes | yes | yes | Small reference and smoke-test core. |
| Stormakt 3020 | yes | yes | yes | yes | Real 400x280 integration; lockstep remains default. |
| ProcessProbe | yes | no | no | no | Deliberate unchanged v1 fallback fixture. |
| ProducerExample | yes | yes | yes | no | Minimal third-party starting point. |

There is no flag day: leave a working producer on v1, add the v2 handshake, verify raw v2, then enable shared memory. Enable latest-frame only after deterministic lockstep parity is established.

## Malformed corpus

The conformance executable checks the bundled text corpus on every run. Each `.wfexcase` contains a protocol version, expected validation result and exact record bytes as hexadecimal. Run another directory without changing the repository:

```sh
dotnet run --project src/SystemRegisIII.WfexConformance -c Release -- --corpus /path/to/corpus
```

Failures identify the case filename plus expected and actual result. This makes a producer's captured bad record suitable for a small regression case without embedding executable code.
