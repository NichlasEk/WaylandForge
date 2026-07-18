# WFEX malformed-stream corpus

Each `.wfexcase` file is an intentionally malformed complete record encoded as hexadecimal. `version` selects the v1 or v2 parser and `expect` names the validation result. `TruncatedPayload` means that the header is valid but fewer payload bytes follow than the header declares.

The conformance executable loads this directory on every normal run. An external corpus can be checked with:

```sh
dotnet run --project src/SystemRegisIII.WfexConformance -- --corpus /path/to/corpus
```

Keep cases small, deterministic and non-secret. A failure reports the filename, expected result and actual parser result.
