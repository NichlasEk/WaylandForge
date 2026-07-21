#!/usr/bin/env python3
"""Generate Copenhagen/Codex's deterministic noise-free procedural SFX."""
from __future__ import annotations

import argparse
import math
import struct
import wave
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2] / "assets" / "stormakt3020" / "sfx"
RAW = ROOT / "raw"
RATE = 48_000


@dataclass(frozen=True)
class Voice:
    start: float
    duration: float
    f0: float
    f1: float
    level: float
    attack: float = 0.004
    harmonics: tuple[float, ...] = (1.0, 0.15, 0.04)


@dataclass(frozen=True)
class Effect:
    name: str
    duration: float
    voices: tuple[Voice, ...]


def v(start: float, duration: float, f0: float, f1: float, level: float,
      attack: float = 0.004, harmonics: tuple[float, ...] = (1.0, 0.15, 0.04)) -> Voice:
    return Voice(start, duration, f0, f1, level, attack, harmonics)


EFFECTS = [
    Effect("copenhagen-ring-lock", .50, (v(0,.25,170,95,.60),v(.10,.25,135,72,.55),v(.23,.22,100,52,.62))),
    Effect("copenhagen-clock-strike", .46, (v(0,.40,330,325,.52,.006,(1,.08)),v(.04,.34,165,120,.28))),
    Effect("copenhagen-frederik-chain", .34, (v(0,.21,285,135,.55),v(.05,.23,190,82,.50))),
    Effect("copenhagen-ledger-blade", .18, (v(0,.16,620,240,.48,.002,(1,.06)),v(.02,.13,310,145,.24))),
    Effect("copenhagen-eye-forecast", .42, (v(0,.34,245,510,.45,.012,(1,.06)),v(.28,.10,720,610,.25,.003,(1,)))),
    Effect("copenhagen-cross-charge", .24, (v(0,.20,360,155,.50,.003,(1,.07)),v(.03,.17,220,105,.33))),
    Effect("copenhagen-shield-wall", .42, (v(0,.19,205,120,.44),v(.09,.20,170,95,.46),v(.19,.18,132,70,.50))),
    Effect("copenhagen-super-compile", .66, (v(0,.22,145,95,.48),v(.15,.25,205,145,.45),v(.34,.26,310,410,.38,.010,(1,.06)))),
    Effect("copenhagen-anchor-break", .40, (v(0,.25,230,105,.56),v(.06,.28,155,65,.52))),
    Effect("copenhagen-ground-gate", .56, (v(0,.31,118,66,.62),v(.16,.31,92,48,.58))),
    Effect("copenhagen-heart-compile", .56, (v(0,.18,125,115,.42),v(.16,.29,230,340,.40,.010,(1,.06)),v(.32,.18,455,455,.22,.010,(1,)))),
    Effect("copenhagen-material-rewrite", .28, (v(0,.22,285,430,.44,.007,(1,.06)),v(.08,.17,470,315,.25,.004,(1,)))),
    Effect("copenhagen-legend-moose", .76, (v(0,.22,95,58,.54),v(.14,.22,105,62,.50),v(.29,.23,88,50,.56),v(.45,.24,145,75,.40))),
    Effect("copenhagen-legend-impact", .12, (v(0,.105,120,55,.62,.002),v(.01,.085,260,120,.22,.002))),
    Effect("copenhagen-saga-order", .36, (v(0,.17,260,155,.45),v(.08,.18,205,112,.48),v(.17,.15,155,78,.52))),
    Effect("copenhagen-saga-horse", .50, (v(0,.20,115,70,.48),v(.13,.20,128,75,.46),v(.27,.19,105,58,.52))),
    Effect("copenhagen-pen-scratch", .13, (v(0,.115,690,330,.40,.002,(1,.04)),v(.01,.09,420,235,.21,.002,(1,)))),
    Effect("copenhagen-edit-stamp", .36, (v(0,.23,180,82,.58),v(.06,.24,295,135,.30))),
    Effect("copenhagen-wrath-claim", .56, (v(0,.30,145,78,.58),v(.12,.30,110,58,.55),v(.27,.22,245,130,.28))),
    Effect("copenhagen-word-reclaim", .35, (v(0,.24,310,165,.48),v(.08,.22,205,95,.47))),
    Effect("copenhagen-circuit-open", .50, (v(0,.24,130,190,.42,.010,(1,.06)),v(.16,.25,220,330,.40,.010,(1,.05)),v(.31,.15,440,440,.22,.008,(1,)))),
    Effect("copenhagen-royal-armor-break", .48, (v(0,.29,195,82,.60),v(.07,.31,135,55,.55),v(.18,.23,320,150,.24))),
    Effect("copenhagen-codex-recognize", .72, (v(0,.25,110,150,.38,.012,(1,.06)),v(.18,.28,190,285,.40,.012,(1,.05)),v(.39,.25,380,510,.32,.014,(1,)))),
    Effect("copenhagen-naval-volley", .17, (v(0,.145,430,185,.50,.002,(1,.07)),v(.02,.12,270,125,.30,.002,(1,)))),
    Effect("copenhagen-hull-hit", .25, (v(0,.20,135,62,.62),v(.025,.17,250,110,.26))),
    Effect("copenhagen-structure-break", .40, (v(0,.25,205,88,.58),v(.06,.27,145,58,.52),v(.14,.20,310,145,.22))),
    Effect("copenhagen-signal-cue", .30, (v(0,.14,220,265,.40),v(.08,.16,310,370,.38),v(.17,.10,470,430,.22,.006,(1,)))),
    Effect("copenhagen-sword-slash", .15, (v(0,.13,560,260,.44,.002,(1,.05)),v(.015,.10,310,165,.22,.002,(1,)))),
    Effect("copenhagen-sword-hit", .16, (v(0,.14,285,130,.48,.002,(1,.08)),v(.018,.11,155,72,.30,.002))),
    Effect("copenhagen-silver-wave", .28, (v(0,.24,240,480,.42,.008,(1,.05)),v(.07,.17,390,260,.22,.005,(1,)))),
    Effect("copenhagen-silver-shatter", .34, (v(0,.22,430,225,.45,.003,(1,.08)),v(.04,.24,285,135,.40),v(.10,.18,175,78,.35))),
]


def render(effect: Effect) -> list[float]:
    samples = [0.0] * round(effect.duration * RATE)
    for voice in effect.voices:
        first = round(voice.start * RATE)
        count = max(1, round(voice.duration * RATE))
        phase = 0.0
        harmonic_total = sum(abs(level) for level in voice.harmonics)
        for offset in range(count):
            index = first + offset
            if index >= len(samples): break
            t = offset / max(1, count - 1)
            frequency = voice.f0 + (voice.f1 - voice.f0) * t
            phase += math.tau * frequency / RATE
            attack = min(1.0, offset / max(1.0, voice.attack * RATE))
            release = (1.0 - t) ** 2.35
            signal = sum(level * math.sin(phase * (n + 1)) for n, level in enumerate(voice.harmonics)) / harmonic_total
            samples[index] += voice.level * attack * release * signal
    peak = max((abs(sample) for sample in samples), default=1.0)
    scale = .58 / max(.58, peak)
    return [sample * scale for sample in samples]


def write(path: Path, samples: list[float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = bytearray()
    for sample in samples:
        value = round(max(-1.0, min(1.0, sample)) * 32767)
        pcm.extend(struct.pack("<hh", value, value))
    with wave.open(str(path), "wb") as output:
        output.setnchannels(2); output.setsampwidth(2); output.setframerate(RATE); output.writeframes(pcm)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", action="append", choices=[effect.name for effect in EFFECTS])
    args = parser.parse_args()
    for effect in EFFECTS:
        if args.only and effect.name not in args.only: continue
        samples = render(effect)
        write(RAW / f"{effect.name}-procedural.wav", samples)
        write(ROOT / f"{effect.name}.wav", samples)
        print(f"Wrote {effect.name} ({effect.duration:.3f}s, noise-free procedural)")


if __name__ == "__main__": main()
