#!/usr/bin/env python3
from __future__ import annotations

import wave
from dataclasses import dataclass
from pathlib import Path


RATE = 48_000
CHANNELS = 2
SAMPLE_WIDTH = 2


@dataclass(frozen=True)
class LoopEdit:
    source: Path
    output: Path
    bpm: int
    beats_per_bar: int
    bars: int

    @property
    def frames(self) -> int:
        seconds = self.bars * self.beats_per_bar * 60.0 / self.bpm
        return round(seconds * RATE)


EDITS = [
    LoopEdit(
        source=Path("assets/stormakt3020/music/kronans-sista-salva-v1.wav"),
        output=Path("assets/stormakt3020/music/kronans-sista-salva-loop-v2.wav"),
        bpm=84,
        beats_per_bar=4,
        bars=14,
    ),
]


def build(edit: LoopEdit) -> None:
    with wave.open(str(edit.source), "rb") as source:
        if source.getframerate() != RATE or source.getnchannels() != CHANNELS or source.getsampwidth() != SAMPLE_WIDTH:
            raise ValueError(f"{edit.source} must be 48 kHz stereo PCM16 WAV")
        if source.getnframes() < edit.frames:
            raise ValueError(f"{edit.source} is shorter than the requested loop")
        payload = source.readframes(edit.frames)

    edit.output.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(edit.output), "wb") as output:
        output.setnchannels(CHANNELS)
        output.setsampwidth(SAMPLE_WIDTH)
        output.setframerate(RATE)
        output.writeframes(payload)
    print(f"Wrote {edit.output}: {edit.frames / RATE:.3f}s ({edit.bars} bars at {edit.bpm} BPM)")


def main() -> None:
    for edit in EDITS:
        build(edit)


if __name__ == "__main__":
    main()
