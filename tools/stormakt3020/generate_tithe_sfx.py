#!/usr/bin/env python3
"""Generate Tithe World's deterministic, noise-free procedural SFX family.

The original Small-SFX renders are kept in ``assets/stormakt3020/sfx/raw`` as
provenance, but they are not used to build the runtime files.  This generator
uses only the Python standard library and deliberately avoids noise sources:
rapid weapon sounds must remain discrete pio/pom events rather than turning
into a continuous saw-like texture when they overlap.
"""
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
CHANNELS = 2
TAU = math.tau


@dataclass(frozen=True)
class Voice:
    start: float
    duration: float
    frequency: float
    end_frequency: float
    amplitude: float
    attack: float = 0.004
    harmonics: tuple[float, ...] = (1.0, 0.18, 0.06)


@dataclass(frozen=True)
class Effect:
    name: str
    duration: float
    voices: tuple[Voice, ...]


def tone(start: float, duration: float, frequency: float, end_frequency: float,
         amplitude: float, attack: float = 0.004,
         harmonics: tuple[float, ...] = (1.0, 0.18, 0.06)) -> Voice:
    return Voice(start, duration, frequency, end_frequency, amplitude, attack, harmonics)


EFFECTS = [
    Effect("tithe-chain-lock-break", 0.36, (
        tone(0.000, 0.20, 260, 155, 0.72), tone(0.055, 0.22, 185, 92, 0.58),
        tone(0.135, 0.18, 540, 310, 0.25, harmonics=(1.0, 0.10)),
    )),
    Effect("tithe-customs-gate", 0.60, (
        tone(0.000, 0.30, 118, 76, 0.70), tone(0.145, 0.26, 92, 61, 0.62),
        tone(0.315, 0.23, 154, 84, 0.48),
    )),
    Effect("tithe-coin-mine-charge", 0.45, (
        tone(0.000, 0.39, 255, 610, 0.56, 0.012, (1.0, 0.08)),
        tone(0.320, 0.10, 820, 690, 0.28, 0.003, (1.0,)),
    )),
    Effect("tithe-coin-mine-break", 0.34, (
        tone(0.000, 0.24, 620, 390, 0.50, harmonics=(1.0, 0.08)),
        tone(0.035, 0.25, 470, 285, 0.45, harmonics=(1.0, 0.08)),
        tone(0.080, 0.22, 330, 205, 0.38, harmonics=(1.0, 0.08)),
    )),
    Effect("tithe-register-switch", 0.54, (
        tone(0.000, 0.18, 210, 135, 0.58), tone(0.120, 0.19, 175, 108, 0.62),
        tone(0.275, 0.22, 132, 72, 0.72),
    )),
    Effect("tithe-seal-wall", 0.55, (
        tone(0.000, 0.27, 142, 82, 0.68), tone(0.115, 0.25, 126, 70, 0.58),
        tone(0.275, 0.24, 104, 55, 0.70),
    )),
    Effect("tithe-upgrade-install", 0.65, (
        tone(0.000, 0.18, 190, 155, 0.45), tone(0.135, 0.20, 245, 205, 0.48),
        tone(0.285, 0.30, 330, 440, 0.48, 0.010, (1.0, 0.08)),
        tone(0.405, 0.20, 495, 495, 0.24, 0.012, (1.0,)),
    )),
    # Nine-frame cooldown: this tail ends before the next shot at 60 fps.
    Effect("tithe-crown-drill", 0.14, (
        tone(0.000, 0.125, 570, 205, 0.58, 0.002, (1.0, 0.12)),
        tone(0.012, 0.105, 285, 145, 0.30, 0.002, (1.0, 0.08)),
    )),
    # Four-frame cooldown: a tiny rounded pio, not a sustained waveform.
    Effect("tithe-volley-director", 0.060, (
        tone(0.000, 0.052, 760, 360, 0.46, 0.0015, (1.0, 0.05)),
        tone(0.006, 0.045, 540, 285, 0.24, 0.0015, (1.0,)),
    )),
    Effect("tithe-magnet-broadside", 0.28, (
        tone(0.000, 0.23, 310, 135, 0.57, 0.006, (1.0, 0.10)),
        tone(0.028, 0.20, 205, 105, 0.43, 0.006, (1.0, 0.08)),
    )),
    Effect("tithe-chain-canister", 0.32, (
        tone(0.000, 0.18, 270, 145, 0.54), tone(0.050, 0.18, 225, 118, 0.48),
        tone(0.105, 0.18, 182, 92, 0.44),
    )),
    Effect("tithe-boss-phase-break", 0.86, (
        tone(0.000, 0.40, 168, 91, 0.68, 0.008), tone(0.170, 0.38, 122, 65, 0.62),
        tone(0.365, 0.41, 88, 43, 0.68),
        tone(0.430, 0.31, 315, 175, 0.20, 0.010, (1.0, 0.06)),
    )),
    Effect("tithe-ledger-shatter", 0.50, (
        tone(0.000, 0.26, 410, 235, 0.48, harmonics=(1.0, 0.10)),
        tone(0.045, 0.31, 305, 165, 0.48), tone(0.125, 0.31, 210, 105, 0.52),
    )),
    # The un-upgraded X route previously leaked the generic harsh Broadside.
    Effect("tithe-standard-broadside", 0.23, (
        tone(0.000, 0.18, 390, 205, 0.50, 0.003, (1.0, 0.08)),
        tone(0.028, 0.17, 285, 150, 0.42, 0.003, (1.0, 0.06)),
    )),
    Effect("tithe-archive-cue", 0.34, (
        tone(0.000, 0.15, 225, 180, 0.40), tone(0.090, 0.18, 315, 270, 0.42),
        tone(0.175, 0.14, 455, 410, 0.28, 0.006, (1.0, 0.06)),
    )),
    Effect("tithe-enemy-pulse", 0.12, (
        tone(0.000, 0.105, 460, 215, 0.50, 0.002, (1.0, 0.07)),
        tone(0.009, 0.085, 260, 145, 0.26, 0.002, (1.0,)),
    )),
    Effect("tithe-structure-break", 0.40, (
        tone(0.000, 0.25, 235, 115, 0.54), tone(0.045, 0.27, 172, 82, 0.50),
        tone(0.120, 0.24, 310, 165, 0.25, harmonics=(1.0, 0.07)),
    )),
    Effect("tithe-hull-hit", 0.26, (
        tone(0.000, 0.21, 155, 78, 0.62),
        tone(0.025, 0.18, 285, 135, 0.27, harmonics=(1.0, 0.08)),
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
            phase += TAU * frequency / SAMPLE_RATE
            attack = min(1.0, offset / max(1.0, voice.attack * SAMPLE_RATE))
            release = (1.0 - progress) ** 2.35
            wave_value = sum(level * math.sin(phase * (index + 1))
                             for index, level in enumerate(voice.harmonics)) / harmonic_scale
            samples[frame] += voice.amplitude * attack * release * wave_value

    # Fixed headroom makes repeated weapons predictable in the runtime mixer.
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
        output.setnchannels(CHANNELS)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", action="append", choices=[effect.name for effect in EFFECTS],
                        help="Generate only the named effect; repeat for more than one.")
    args = parser.parse_args()
    selected = [effect for effect in EFFECTS if not args.only or effect.name in args.only]
    for effect in selected:
        samples = render(effect)
        raw = RAW / f"{effect.name}-procedural.wav"
        runtime = ROOT / f"{effect.name}.wav"
        write_wav(raw, samples)
        write_wav(runtime, samples)
        print(f"Wrote {runtime} ({effect.duration:.3f}s, noise-free procedural)")


if __name__ == "__main__":
    main()
