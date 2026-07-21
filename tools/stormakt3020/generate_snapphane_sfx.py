#!/usr/bin/env python3
"""Generate Bana 6's deterministic, noise-free procedural sound family."""
from __future__ import annotations

import argparse
import math
import struct
import wave
from dataclasses import dataclass
from pathlib import Path


ROOT = Path("assets/stormakt3020/sfx")
RAW = ROOT / "raw"
SAMPLE_RATE = 48_000


@dataclass(frozen=True)
class Voice:
    start: float
    duration: float
    frequency: float
    end_frequency: float
    amplitude: float
    attack: float = 0.004
    harmonics: tuple[float, ...] = (1.0, 0.16, 0.05)


@dataclass(frozen=True)
class Effect:
    name: str
    duration: float
    voices: tuple[Voice, ...]


def tone(start: float, duration: float, frequency: float, end_frequency: float,
         amplitude: float, attack: float = 0.004,
         harmonics: tuple[float, ...] = (1.0, 0.16, 0.05)) -> Voice:
    return Voice(start, duration, frequency, end_frequency, amplitude, attack, harmonics)


EFFECTS = [
    Effect("snapphane-signal-cue", 0.34, (
        tone(0.000, 0.17, 205, 255, 0.42), tone(0.085, 0.19, 305, 365, 0.38),
        tone(0.180, 0.13, 455, 410, 0.24, harmonics=(1.0, 0.05)),
    )),
    Effect("snapphane-beacon-break", 0.30, (
        tone(0.000, 0.20, 510, 255, 0.48, harmonics=(1.0, 0.08)),
        tone(0.035, 0.22, 285, 145, 0.42),
    )),
    Effect("snapphane-mine-turn", 0.24, (
        tone(0.000, 0.21, 245, 520, 0.46, 0.008, (1.0, 0.06)),
        tone(0.055, 0.15, 360, 285, 0.25, harmonics=(1.0,)),
    )),
    Effect("snapphane-mine-burst", 0.32, (
        tone(0.000, 0.23, 215, 92, 0.58), tone(0.045, 0.23, 335, 155, 0.32),
    )),
    Effect("snapphane-hunter-shot", 0.105, (
        tone(0.000, 0.092, 610, 275, 0.48, 0.002, (1.0, 0.06)),
        tone(0.008, 0.075, 330, 175, 0.24, 0.002, (1.0,)),
    )),
    Effect("snapphane-hunter-break", 0.28, (
        tone(0.000, 0.20, 315, 145, 0.48), tone(0.040, 0.20, 205, 98, 0.45),
    )),
    Effect("snapphane-wreck-break", 0.38, (
        tone(0.000, 0.25, 185, 76, 0.58), tone(0.055, 0.27, 265, 112, 0.31),
        tone(0.120, 0.20, 390, 210, 0.18, harmonics=(1.0, 0.05)),
    )),
    Effect("snapphane-copper-volley", 0.16, (
        tone(0.000, 0.14, 485, 220, 0.46, 0.002, (1.0, 0.07)),
        tone(0.018, 0.12, 340, 165, 0.30, 0.002, (1.0,)),
    )),
    Effect("snapphane-chain-hook", 0.26, (
        tone(0.000, 0.18, 270, 135, 0.52), tone(0.045, 0.17, 185, 92, 0.42),
    )),
    Effect("snapphane-rescue-release", 0.44, (
        tone(0.000, 0.22, 175, 115, 0.50), tone(0.105, 0.27, 275, 385, 0.38, 0.008, (1.0, 0.06)),
    )),
    Effect("snapphane-red-hounds-volley", 0.18, (
        tone(0.000, 0.15, 390, 170, 0.52, 0.002, (1.0, 0.08)),
        tone(0.025, 0.13, 255, 120, 0.34, 0.002, (1.0,)),
    )),
    Effect("snapphane-mega-charge", 0.62, (
        tone(0.000, 0.55, 115, 360, 0.48, 0.018, (1.0, 0.08)),
        tone(0.260, 0.28, 420, 560, 0.26, 0.012, (1.0,)),
    )),
    Effect("snapphane-chain-deploy", 0.48, (
        tone(0.000, 0.25, 205, 105, 0.54), tone(0.100, 0.28, 165, 78, 0.52),
        tone(0.230, 0.19, 295, 155, 0.25),
    )),
    Effect("snapphane-chain-break", 0.32, (
        tone(0.000, 0.21, 330, 155, 0.52), tone(0.045, 0.22, 205, 92, 0.48),
    )),
    Effect("snapphane-fleet-break", 0.48, (
        tone(0.000, 0.29, 175, 72, 0.60), tone(0.075, 0.32, 125, 52, 0.55),
        tone(0.160, 0.25, 280, 125, 0.25),
    )),
    Effect("snapphane-hull-hit", 0.26, (
        tone(0.000, 0.21, 145, 68, 0.62), tone(0.025, 0.18, 260, 120, 0.27),
    )),
]


def render(effect: Effect) -> list[float]:
    frame_count = round(effect.duration * SAMPLE_RATE)
    samples = [0.0] * frame_count
    for voice in effect.voices:
        start_frame = round(voice.start * SAMPLE_RATE)
        voice_frames = max(1, round(voice.duration * SAMPLE_RATE))
        phase = 0.0
        harmonic_scale = sum(abs(level) for level in voice.harmonics)
        for offset in range(voice_frames):
            frame = start_frame + offset
            if frame >= frame_count:
                break
            progress = offset / max(1, voice_frames - 1)
            frequency = voice.frequency + (voice.end_frequency - voice.frequency) * progress
            phase += math.tau * frequency / SAMPLE_RATE
            attack = min(1.0, offset / max(1.0, voice.attack * SAMPLE_RATE))
            release = (1.0 - progress) ** 2.35
            value = sum(level * math.sin(phase * (index + 1))
                        for index, level in enumerate(voice.harmonics)) / harmonic_scale
            samples[frame] += voice.amplitude * attack * release * value
    peak = max((abs(sample) for sample in samples), default=1.0)
    scale = 0.58 / max(peak, 0.58)
    return [sample * scale for sample in samples]


def write_wav(path: Path, samples: list[float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = bytearray()
    for sample in samples:
        value = round(max(-1.0, min(1.0, sample)) * 32767)
        pcm.extend(struct.pack("<hh", value, value))
    with wave.open(str(path), "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", action="append", choices=[effect.name for effect in EFFECTS])
    args = parser.parse_args()
    selected = [effect for effect in EFFECTS if not args.only or effect.name in args.only]
    for effect in selected:
        samples = render(effect)
        write_wav(RAW / f"{effect.name}-procedural.wav", samples)
        write_wav(ROOT / f"{effect.name}.wav", samples)
        print(f"Wrote {effect.name} ({effect.duration:.3f}s, noise-free procedural)")


if __name__ == "__main__":
    main()
