#!/usr/bin/env python3
"""Generate the Skanska skuggor SFX family without buzz-saw weapon loops."""
from __future__ import annotations

import argparse
import math
import os
import struct
import subprocess
import wave
from dataclasses import dataclass
from pathlib import Path

STABLE_ROOT = Path.home() / "ai" / "stable-audio-3"
os.environ.setdefault("XDG_CACHE_HOME", str(STABLE_ROOT / "cache"))
os.environ.setdefault("HF_HOME", str(STABLE_ROOT / "huggingface"))
os.environ.setdefault("HF_HUB_CACHE", str(STABLE_ROOT / "huggingface" / "hub"))
os.environ.setdefault("TRANSFORMERS_CACHE", str(STABLE_ROOT / "huggingface" / "transformers"))
os.environ.setdefault("HF_HUB_DISABLE_XET", "1")

ROOT = Path("assets/stormakt3020/sfx")
RAW = ROOT / "raw"
NEGATIVE = (
    "music ambience speech voices long reverb repeated sequence motor chainsaw buzz rasp "
    "piercing treble harsh laser screech siren distortion white noise"
)


@dataclass(frozen=True)
class Effect:
    name: str
    duration: float
    seed: int
    prompt: str
    lowpass: int = 7000


EFFECTS = [
    Effect("skanska-mist-pulse", 0.20, 3020201, "Procedural soft green signal pulse", 4300),
    Effect("skanska-copper-pulse", 0.18, 3020202, "Procedural clean copper corsair shot", 5000),
    Effect("skanska-convoy-pulse", 0.23, 3020203, "Procedural restrained Danish convoy shot", 4500),
    Effect("skanska-iron-pulse", 0.22, 3020204, "Procedural low iron raven shot", 4000),
    Effect("skanska-drill-pulse", 0.24, 3020205, "Procedural low drill cannon pulse", 3800),
    Effect("skanska-ember-pulse", 0.26, 3020206, "Procedural warm ember pulse", 4600),
    Effect("skanska-signal-break", 0.65, 3020207,
           "One copper green signal beacon breaking, glass rune tube cracks and a short soft electrical field collapses, compact dry isolated game sound", 6500),
    Effect("skanska-soren-arrival", 0.70, 3020208,
           "One small black copper corsair making a very short lateral boost dash, compact engine push and green relay chirp, dry isolated game sound, no motor", 5800),
    Effect("skanska-soren-disengage", 0.85, 3020209,
           "One damaged black copper corsair breaking away, brief armor snap, restrained engine surge and fading green signal click, dry isolated game sound", 6000),
    Effect("skanska-glimminge-warning", 0.90, 3020210,
           "One giant flying iron castle announcing its arrival, two low brass warning knocks and a heavy portcullis lock, compact dry boss game sound", 5600),
    Effect("skanska-raven-deploy", 0.65, 3020211,
           "Two small iron raven escort craft unfolding from a castle hangar, paired metal wing clicks and short low launch pulses, dry isolated game sound", 6200),
    Effect("skanska-glimminge-wall", 0.90, 3020212,
           "One flying iron castle launching a synchronized wall of heavy iron energy bolts, deep restrained mechanical volley, short dry boss game sound, no cannon boom", 5200),
    Effect("skanska-spear-warning", 0.55, 3020213,
           "One crystal spear targeting warning, two soft green copper locator notes and a tiny mineral chime, short dry isolated game sound, not piercing", 5600),
    Effect("skanska-glimminge-phase-break", 1.30, 3020214,
           "One flying iron castle unfolding two enormous mining drill turrets, shield braces release, gears lock and crystal power engages, dry boss game sound", 6500),
    Effect("skanska-glimminge-burning", 0.90, 3020215,
           "One armored flying castle crossing into critical damage, iron plates buckle and a contained furnace flares, compact dry boss game sound", 6200),
    Effect("skanska-glimminge-death", 1.70, 3020216,
           "One enormous flying iron castle collapsing into a connected heavy wreck, towers crack, boilers fail and masonry armor settles, decisive dry boss defeat sound", 6500),
]

CHIRPS = {
    "skanska-mist-pulse": (760.0, 250.0, 0.04),
    "skanska-copper-pulse": (980.0, 340.0, 0.10),
    "skanska-convoy-pulse": (700.0, 230.0, 0.08),
    "skanska-iron-pulse": (610.0, 205.0, 0.13),
    "skanska-drill-pulse": (520.0, 175.0, 0.11),
    "skanska-ember-pulse": (840.0, 280.0, 0.07),
}


def synthesize_chirp(path: Path, duration: float, start_hz: float, end_hz: float, harmonic: float) -> None:
    sample_rate = 48_000
    frames = round(duration * sample_rate)
    phase = 0.0
    samples: list[float] = []
    for index in range(frames):
        t = index / sample_rate
        progress = index / max(1, frames - 1)
        frequency = end_hz + (start_hz - end_hz) * math.exp(-4.0 * progress)
        phase += math.tau * frequency / sample_rate
        envelope = min(1.0, t / 0.005) * max(0.0, 1.0 - progress) ** 2.25
        tone = math.sin(phase) + math.sin(phase * 2.0 + 0.2) * harmonic
        samples.append(math.tanh(tone * 1.06) * envelope)
    scale = 0.68 / max(max(abs(sample) for sample in samples), 1e-9)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(sample_rate)
        pcm = bytearray()
        for sample in samples:
            value = max(-32768, min(32767, round(sample * scale * 32767)))
            pcm.extend(struct.pack("<hh", value, value))
        output.writeframes(pcm)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", action="append", choices=[effect.name for effect in EFFECTS])
    args = parser.parse_args()
    selected = [effect for effect in EFFECTS if not args.only or effect.name in args.only]
    ROOT.mkdir(parents=True, exist_ok=True)
    RAW.mkdir(parents=True, exist_ok=True)

    stable_effects = [effect for effect in selected if effect.name not in CHIRPS]
    model = None
    torchaudio = None
    if stable_effects:
        import torchaudio as torchaudio_module
        from stable_audio_3 import StableAudioModel

        torchaudio = torchaudio_module
        print("Loading Stable Audio 3 Small-SFX once...")
        model = StableAudioModel.from_pretrained("small-sfx", device="cuda", model_half=True)

    for effect in selected:
        procedural = effect.name in CHIRPS
        raw = RAW / f"{effect.name}-{'procedural' if procedural else 'stable-audio3'}.wav"
        runtime = ROOT / f"{effect.name}.wav"
        if procedural:
            print(f"Synthesizing {effect.name} duration={effect.duration:.2f}s")
            synthesize_chirp(raw, effect.duration, *CHIRPS[effect.name])
        else:
            print(f"Generating {effect.name} seed={effect.seed} duration={effect.duration:.2f}s")
            assert model is not None and torchaudio is not None
            audio = model.generate(
                prompt=effect.prompt,
                negative_prompt=NEGATIVE,
                duration=effect.duration,
                steps=8,
                cfg_scale=1.0,
                seed=effect.seed,
                batch_size=1,
                sample_size=model.model_config["sample_size"],
            )
            torchaudio.save(raw, audio[0].cpu(), model.model.sample_rate)
        fade_start = max(0.0, effect.duration - 0.12)
        subprocess.run([
            "ffmpeg", "-y", "-loglevel", "error", "-i", str(raw),
            "-t", f"{effect.duration:.2f}",
            "-af", f"aresample=48000,lowpass=f={effect.lowpass},volume=-1dB,"
                   f"alimiter=limit=0.89:level=false,afade=t=out:st={fade_start:.2f}:d=0.12",
            "-ar", "48000", "-ac", "2", "-c:a", "pcm_s16le", str(runtime),
        ], check=True)
        print(f"Wrote {runtime}")


if __name__ == "__main__":
    main()
