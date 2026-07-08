# waylandforge-audiod

Small PipeWire audio daemon experiment for WaylandForge.

Version 0 creates a PipeWire playback node named `EutherAudio Sinklet`, keeps a
small in-memory float ringbuffer, and listens on a Unix socket for test commands.

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

Then inspect the node:

```sh
pw-top
```

The daemon currently emits a short stereo sine click. Real core PCM input is a
later step.
