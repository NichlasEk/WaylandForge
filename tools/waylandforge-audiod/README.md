# waylandforge-audiod

Small PipeWire audio daemon experiment for WaylandForge.

Version 0 creates a PipeWire playback node named `EutherAudio Sinklet`, keeps a
small in-memory float ringbuffer, and listens on a Unix socket for test commands
or bounded PCM chunks.

## Build

```sh
make
```

## Run

```sh
./waylandforge-audiod
```

In another terminal:

```sh
printf 'PLAY_TEST\n' | socat - UNIX-CONNECT:/tmp/waylandforge-audio.sock
```

Or send a real `WFAU` PCM packet:

```sh
python - <<'PY'
import math, socket, struct

rate = 48000
frames = rate // 4
samples = bytearray()
for i in range(frames):
    fade = 1.0 - (i / frames)
    sample = math.sin(2.0 * math.pi * 440.0 * i / rate) * 0.18 * fade
    samples += struct.pack('<ff', sample, sample)

header = struct.pack('<4sHHIHHII', b'WFAU', 1, 1, rate, 2, 0, frames, len(samples))
with socket.socket(socket.AF_UNIX) as s:
    s.connect('/tmp/waylandforge-audio.sock')
    s.sendall(header + samples)
    print(s.recv(128).decode(), end='')
PY
```

Then inspect the node:

```sh
pw-top
```

## WFAU v1

The binary protocol is intentionally tiny and bounded. All integer fields are
little-endian:

| Offset | Type | Meaning |
| --- | --- | --- |
| 0 | char[4] | `WFAU` |
| 4 | uint16 | version, currently `1` |
| 6 | uint16 | format, currently `1` = F32LE |
| 8 | uint32 | sample rate, currently `48000` |
| 12 | uint16 | channels, currently `2` |
| 14 | uint16 | reserved, send `0` |
| 16 | uint32 | frame count, max `8192` |
| 20 | uint32 | payload byte count |

Payload is interleaved stereo `float32`: left, right, left, right. The daemon
replies with `OK WFAU frames=N accepted=N dropped=N` or an `ERR WFAU ...` line.
