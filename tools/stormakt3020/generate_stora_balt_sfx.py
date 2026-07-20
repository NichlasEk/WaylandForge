#!/usr/bin/env python3
"""Generate the fixed-seed Stable Audio 3 SFX family for Stora Balt."""
from __future__ import annotations

import argparse
import os
import subprocess
from dataclasses import dataclass
from pathlib import Path

STABLE_ROOT = Path.home() / "ai" / "stable-audio-3"
os.environ.setdefault("XDG_CACHE_HOME", str(STABLE_ROOT / "cache"))
os.environ.setdefault("HF_HOME", str(STABLE_ROOT / "huggingface"))
os.environ.setdefault("HF_HUB_CACHE", str(STABLE_ROOT / "huggingface" / "hub"))
os.environ.setdefault("TRANSFORMERS_CACHE", str(STABLE_ROOT / "huggingface" / "transformers"))
os.environ.setdefault("HF_HUB_DISABLE_XET", "1")

import torchaudio
from stable_audio_3 import StableAudioModel


ROOT = Path("assets/stormakt3020/sfx")
RAW = ROOT / "raw"
NEGATIVE = (
    "music ambience speech voices long reverb repeated sequence piercing treble "
    "harsh laser screech siren distortion white noise"
)


@dataclass(frozen=True)
class Effect:
    name: str
    duration: float
    seed: int
    prompt: str
    lowpass: int = 7500


EFFECTS = [
    Effect("stora-balt-player-piow-a", 0.34, 3020101,
           "One tiny friendly retro spaceship pulse cannon, rounded soft piow, quick falling pitch, warm analog arcade tone, clean short dry game sound", 6200),
    Effect("stora-balt-player-piow-b", 0.34, 3020102,
           "One tiny friendly retro spaceship pulse cannon, rounded soft piow at a slightly higher pitch, quick falling tone, clean short dry arcade game sound", 6500),
    Effect("stora-balt-enemy-piow", 0.42, 3020103,
           "One compact Danish royal drone pulse shot, low rounded pew, muted brass relay click and short descending analog tone, dry retro game sound", 5600),
    Effect("stora-balt-turret-piow", 0.55, 3020104,
           "One heavy bridge turret energy pulse, brief mechanical latch then a rounded low piow projectile, restrained recoil, short dry retro science fiction game sound", 5200),
    Effect("stora-balt-bridge-crack", 0.75, 3020105,
           "One damaged iron gravity bridge cracking, stressed girders snap and a few hot copper sparks scatter, compact dry isolated game sound", 7200),
    Effect("stora-balt-bridge-collapse", 1.35, 3020106,
           "One gravity bridge span collapsing into space, heavy iron deck rupture, rails tear and machinery drops away, deep compact impact with short debris tail, dry isolated game sound", 6800),
    Effect("stora-balt-boss-warning", 0.80, 3020107,
           "One royal fogde warship warning signal, two restrained low brass electronic pulses and a mechanical lock, ominous but soft, dry isolated boss game sound", 5600),
    Effect("stora-balt-boss-salvo", 0.95, 3020108,
           "One armored red white warship firing a synchronized energy broadside, several rounded low pulse shots with heavy breech recoil, compact dry boss game sound", 6000),
    Effect("stora-balt-boss-phase-break", 1.45, 3020109,
           "One royal warship armor phase breaking open, red white plates rupture, chains recoil and a hot seal core unlocks, deep mechanical science fiction boss game sound", 7000),
    Effect("stora-balt-boss-death", 1.90, 3020110,
           "One enormous royal tax warship core finally rupturing, layered deep hull breaks, brass seal shatters and engine power collapses, decisive short dry boss defeat sound", 6800),
]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", action="append", choices=[effect.name for effect in EFFECTS])
    args = parser.parse_args()
    selected = [effect for effect in EFFECTS if not args.only or effect.name in args.only]
    RAW.mkdir(parents=True, exist_ok=True)
    ROOT.mkdir(parents=True, exist_ok=True)
    print("Loading Stable Audio 3 Small-SFX once...")
    model = StableAudioModel.from_pretrained("small-sfx", device="cuda", model_half=True)
    for effect in selected:
        raw = RAW / f"{effect.name}-stable-audio3.wav"
        runtime = ROOT / f"{effect.name}.wav"
        print(f"Generating {effect.name} seed={effect.seed} duration={effect.duration:.2f}s")
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
