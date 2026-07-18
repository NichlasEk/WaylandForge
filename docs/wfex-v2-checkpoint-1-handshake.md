# WFEX v2 Checkpoint 1 handshake

Date: 2026-07-18

## Delivered

Checkpoint 1 adds a producer-initiated, fixed 48-byte WFEX v2 handshake without changing the existing v1 frame record. The host supports three per-core policies:

- `v1` for known legacy producers with no startup delay;
- `prefer-v2` for bounded negotiation with byte-zero v1 fallback;
- `require-v2` for deployments where fallback would hide a configuration error.

The hello and accept records negotiate major/minor version, required and optional capabilities, maximum frame dimensions and payload, ARGB8888, deterministic lockstep and raw stream frame transport. Unknown mandatory capabilities, wrong record sizes, wrong magic, unsupported major versions, partial hellos and incompatible mandatory baselines produce explicit failures.

Stormakt and the reference Dummy producer emit a hello only when the host policy environment requests it. Their ordinary command loops and v1 direct-capture behavior are unchanged when the variable is absent. OpenTyrian and Raptor remain explicitly configured as v1; Stormakt uses `prefer-v2`.

The diagnostics panel displays negotiated protocol and effective limits. Before negotiation it displays the configured policy and `PENDING`; a legacy fallback is visibly marked `FALLBACK`.

## Compatibility and failure evidence

The conformance executable now covers 23 cases, including handshake roundtrips, limit intersection, unsupported major version, wrong record size, unknown mandatory capabilities and a cancellable anonymous-pipe timeout that remains usable afterward.

Direct producer integration produced identical first-frame SHA-256 prefixes on v1 and v2 paths:

```text
Dummy 320x224 v1       11d011b21d8b5400
Dummy 320x224 v2       11d011b21d8b5400
Stormakt 400x280 v1    33acabfeca9e5e73
Stormakt 400x280 v2    33acabfeca9e5e73
Stormakt 320x224 v2    d2f5879a6320b46b
```

An integration harness exercised `ExternalProcessCore` itself:

```text
stormakt-v2 OK · V2.0 RAW LOCKSTEP · 400X280
probe-v1-fallback OK · V1 RAW LOCKSTEP · FALLBACK · 320X224
probe-require-v2 OK · actionable timeout
```

The unchanged ProcessProbe executable is the v1 fallback fixture: it neither references the negotiation library nor reacts to `WAYLANDFORGE_WFEX_POLICY`.

## Deliberate boundary

Checkpoint 1 confirms that both sides agree on v2 and its mandatory baseline, but framebuffer records still use the exact 32-byte v1 header. This isolates negotiation and fallback bugs from frame-format changes. Checkpoint 2 introduces versioned records, timestamps, declared header size, sequence validation and reconnect diagnostics behind the established policy boundary.
