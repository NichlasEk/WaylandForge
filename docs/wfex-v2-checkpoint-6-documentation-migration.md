# WFEX v2 Checkpoint 6 - documentation and migration

Checkpoint 6 closes the compatibility and adoption work around the implemented v2 protocol. The later explicit Checkpoint 5 follow-up added optional PACKRLE without changing these compatibility rules.

## Delivered

- The technical specification contains the final v1, v2 handshake, `WFF2`, shared-region, notification, input and pacing layouts.
- A compiled producer example supports v1, v2 raw and v2 shared memory from one small source file.
- The conformance executable loads a file-based malformed-stream corpus and reports the failing filename, expected result and actual result.
- The integration guide documents feature detection, policy/transport combinations, fallback rules, troubleshooting and staged migration.
- Dummy and Stormakt retain v1 while supporting v2 raw/shared/latest. ProcessProbe intentionally remains unchanged as the real v1 fallback fixture.

## Compatibility rule

No existing producer must upgrade. A new feature is used only after the v2 handshake selects it, and reconnect is required before changing record or transport format. Raw ARGB8888 lockstep remains the mandatory v2 baseline.

## Verification

The release gate is:

```sh
dotnet build WaylandForge.slnx -c Release
dotnet build src/SystemRegisIII.ExternalCore.Stormakt3020/SystemRegisIII.ExternalCore.Stormakt3020.csproj -c Release
dotnet run --project src/SystemRegisIII.WfexConformance/SystemRegisIII.WfexConformance.csproj -c Release --no-build
```

The host integration suite additionally covers v1 fallback, v2 raw, shared lockstep, latest-frame slow-host behavior, raw/shared framebuffer parity, duplicate sequences, reconnect and producer crash cleanup.

Verification on 2026-07-18 produced:

- full solution and explicit Stormakt Release builds with zero warnings and zero errors;
- after the PACKRLE follow-up, `WFEX CONFORMANCE OK - 90 CASES - CORPUS 10` from both the bundled and explicit corpus paths;
- the producer example running as a real child process at 64x48 in v1, v2 raw and v2 shared modes;
- five 600-frame Stormakt raw/shared comparisons with identical framebuffer hashes;
- passing latest-frame, v1 fallback, required-v2 failure, shared-memory cleanup, crash and reconnect checks.
