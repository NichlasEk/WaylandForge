#!/usr/bin/env python3
from __future__ import annotations

import math
import struct
import wave
from pathlib import Path


RATE = 48_000
DURATION = 20
OUTPUT = Path("assets/stormakt3020/music/codex-argentum-clock-loop-v1.wav")


def envelope(position: float, decay: float) -> float:
    return math.exp(-position * decay) if position >= 0.0 else 0.0


def build() -> None:
    frames = RATE * DURATION
    payload = bytearray(frames * 4)
    royal_notes = (293.665, 349.228, 440.000, 415.305, 293.665, 277.183, 220.000, 207.652)
    noise = 0x3020_0255

    for frame in range(frames):
        t = frame / RATE
        beat = int(t) % len(royal_notes)
        beat_t = t - math.floor(t)
        note = royal_notes[beat]

        drone = 0.035 * math.sin(math.tau * 73.416 * t)
        drone += 0.018 * math.sin(math.tau * 110.000 * t + 0.7)
        remembered = 0.026 * envelope(beat_t, 2.8) * math.sin(math.tau * note * t)
        remembered += 0.012 * envelope(beat_t, 3.7) * math.sin(math.tau * note * 2.01 * t)

        tick_t = t - round(t * 2.0) / 2.0
        tick = 0.0
        if tick_t >= 0.0:
            tick = 0.075 * envelope(tick_t, 38.0) * (
                math.sin(math.tau * 1320.0 * tick_t) +
                0.45 * math.sin(math.tau * 1979.0 * tick_t)
            )

        noise = (1664525 * noise + 1013904223) & 0xFFFFFFFF
        hiss = (((noise >> 9) / float(1 << 23)) * 2.0 - 1.0) * 0.004
        gate = 0.55 + 0.45 * math.sin(math.tau * 0.05 * t - math.pi / 2)
        sample = max(-0.92, min(0.92, (drone + remembered + tick + hiss) * gate))
        left = int(sample * 32767)
        right = int((sample * 0.94 + 0.006 * math.sin(math.tau * 146.832 * t)) * 32767)
        struct.pack_into("<hh", payload, frame * 4, left, right)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUTPUT), "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(payload)
    print(f"Wrote {OUTPUT}: {DURATION}s cold Codex clock loop")


if __name__ == "__main__":
    build()
