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
        source=Path("assets/stormakt3020/music/silverkroppen-faltmarsch-v1.wav"),
        output=Path("assets/stormakt3020/music/silverkroppen-faltmarsch-loop-v1.wav"),
        bpm=84,
        beats_per_bar=4,
        bars=16,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/skanska-skuggor-v1.wav"),
        output=Path("assets/stormakt3020/music/skanska-skuggor-loop-v1.wav"),
        bpm=92,
        beats_per_bar=4,
        bars=21,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/tre-kronors-jarnmarsch-v1.wav"),
        output=Path("assets/stormakt3020/music/tre-kronors-jarnmarsch-loop-v1.wav"),
        bpm=88,
        beats_per_bar=4,
        bars=16,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/kronans-sista-salva-v1.wav"),
        output=Path("assets/stormakt3020/music/kronans-sista-salva-loop-v2.wav"),
        bpm=84,
        beats_per_bar=4,
        bars=14,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/snapphanens-jakt-v1.wav"),
        output=Path("assets/stormakt3020/music/snapphanens-jakt-loop-v1.wav"),
        bpm=120,
        beats_per_bar=4,
        bars=22,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/rode-hunde-drev-v1.wav"),
        output=Path("assets/stormakt3020/music/rode-hunde-drev-loop-v1.wav"),
        bpm=120,
        beats_per_bar=4,
        bars=22,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/snapphanens-ed-seger-v1.wav"),
        output=Path("assets/stormakt3020/music/snapphanens-ed-seger-loop-v1.wav"),
        bpm=120,
        beats_per_bar=4,
        bars=8,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/kopenhamns-ring-v1.wav"),
        output=Path("assets/stormakt3020/music/kopenhamns-ring-loop-v1.wav"),
        bpm=120,
        beats_per_bar=4,
        bars=22,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/frederik-null-v1.wav"),
        output=Path("assets/stormakt3020/music/frederik-null-loop-v1.wav"),
        bpm=126,
        beats_per_bar=4,
        bars=24,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/oresunds-oje-v1.wav"),
        output=Path("assets/stormakt3020/music/oresunds-oje-loop-v1.wav"),
        bpm=112,
        beats_per_bar=4,
        bars=20,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/kungliga-armadan-v1.wav"),
        output=Path("assets/stormakt3020/music/kungliga-armadan-loop-v1.wav"),
        bpm=124,
        beats_per_bar=4,
        bars=23,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/christians-superfregatt-v1.wav"),
        output=Path("assets/stormakt3020/music/christians-superfregatt-loop-v1.wav"),
        bpm=132,
        beats_per_bar=4,
        bars=22,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/holmen-under-staden-v1.wav"),
        output=Path("assets/stormakt3020/music/holmen-under-staden-loop-v1.wav"),
        bpm=96,
        beats_per_bar=4,
        bars=16,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/argentum-legender-v1.wav"),
        output=Path("assets/stormakt3020/music/argentum-legender-loop-v1.wav"),
        bpm=108,
        beats_per_bar=4,
        bars=20,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/konung-christians-vrede-v1.wav"),
        output=Path("assets/stormakt3020/music/konung-christians-vrede-loop-v1.wav"),
        bpm=128,
        beats_per_bar=4,
        bars=24,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/kopenhamn-silvergryning-v1.wav"),
        output=Path("assets/stormakt3020/music/kopenhamn-silvergryning-loop-v1.wav"),
        bpm=96,
        beats_per_bar=4,
        bars=8,
    ),
    LoopEdit(
        source=Path("assets/stormakt3020/music/kopenhamn-landning-v1.wav"),
        output=Path("assets/stormakt3020/music/kopenhamn-landning-loop-v1.wav"),
        bpm=80,
        beats_per_bar=4,
        bars=8,
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
