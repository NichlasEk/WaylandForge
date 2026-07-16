#!/usr/bin/env python3
"""Generate the fixed-seed Stable Audio 3 SFX family for Tithe World.

Run with the Stable Audio 3 virtual environment so the model is loaded once:

  /home/nichlas/ai/stable-audio-3/.venv/bin/python \
    tools/stormakt3020/generate_tithe_sfx.py
"""
from __future__ import annotations

import argparse
import os
import subprocess
from dataclasses import dataclass
from pathlib import Path

# Match the isolated Stable Audio installation even when this script is invoked
# directly rather than through its launcher shell.
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
NEGATIVE = "music ambience speech voices footsteps long reverb repeated sequence"


@dataclass(frozen=True)
class Effect:
    name: str
    duration: float
    seed: int
    prompt: str
    raw_revision: str = ""


EFFECTS = [
    Effect("tithe-chain-lock-break", 1.8, 3020501,
           "Single royal iron chain lock snapping open, taut heavy chain breaks, brass seal cracks, short cyan electrical release, dry isolated retro science fiction game sound effect"),
    Effect("tithe-customs-gate", 2.2, 3020502,
           "Single colossal suspended customs gate engaging, iron teeth slide on rails, two brass signal relays click, red wax seal press locks with a deep mechanical clank, dry isolated retro science fiction game sound effect"),
    Effect("tithe-coin-mine-charge", 1.5, 3020503,
           "Single magnetic coin mine arming, spinning silver coins accelerate into a rising metallic electrical whine, brief warning ping at the end, dry isolated retro science fiction game sound effect"),
    Effect("tithe-coin-mine-break", 1.4, 3020504,
           "Single magnetic coin mine breaking apart, compact brass pop, silver coins scatter and ring, short cyan energy collapse, dry isolated retro science fiction game sound effect"),
    Effect("tithe-register-switch", 2.0, 3020505,
           "Single giant archive railway register switch changing route, heavy iron points move, ledger gears ratchet, chain lever locks with one decisive brass clank, dry isolated retro science fiction game sound effect"),
    Effect("tithe-seal-wall", 2.1, 3020506,
           "Single enormous royal sealing press builds an iron wall, synchronized pistons stamp red wax seals, chain drive tightens, deep final lock, dry isolated retro science fiction game sound effect"),
    Effect("tithe-upgrade-install", 2.0, 3020507,
           "Single confiscated starship weapon module installed, brass clamps close in sequence, iron couplings engage, restrained cyan power-up chime, dry isolated retro science fiction game sound effect"),
    Effect("tithe-crown-drill", 1.0, 3020508,
           "Single compact crown drill cannon shot, fast rotating brass bore spins up and launches one dense piercing cyan bolt, hard mechanical recoil, dry isolated retro science fiction game sound effect"),
    Effect("tithe-volley-director", 0.55, 3020514,
           "Single subdued three-gun starship volley, three tiny brass clockwork breeches click almost together, compact soft electromagnetic pops, restrained mechanical recoil, very short dry tail, quiet isolated retro science fiction game sound effect, no cannon boom no sharp crack",
           "v2"),
    Effect("tithe-magnet-broadside", 0.85, 3020515,
           "Single compact magnetic starship broadside, paired cyan coils make one muted low mechanical thump and a short soft field flutter, restrained brass relay click, narrow dry tail, quiet isolated retro science fiction game sound effect, no laser blast no resonant sweep",
           "v2"),
    Effect("tithe-chain-canister", 1.4, 3020511,
           "Single chain canister starship broadside, heavy iron breech blast launches several linked chain fragments, sharp brass recoil and rattling metal tail, dry isolated retro science fiction game sound effect"),
    Effect("tithe-boss-phase-break", 2.4, 3020512,
           "Single enormous flying tax archive armor transition, two iron revision plates rupture, ledger drums halt, chains recoil and a cold cyan accounting core unlocks, dry isolated boss game sound effect"),
    Effect("tithe-ledger-shatter", 1.7, 3020513,
           "Single armored mechanical ledger roll shattering, parchment drum tears, brass axle snaps, iron account seals scatter with a short electrical crack, dry isolated boss game sound effect"),
]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", action="append", choices=[effect.name for effect in EFFECTS],
                        help="Generate only the named effect; repeat for more than one.")
    args = parser.parse_args()
    selected = [effect for effect in EFFECTS if not args.only or effect.name in args.only]
    RAW.mkdir(parents=True, exist_ok=True)
    ROOT.mkdir(parents=True, exist_ok=True)
    print("Loading Stable Audio 3 Small-SFX once...")
    model = StableAudioModel.from_pretrained("small-sfx", device="cuda", model_half=True)
    for effect in selected:
        revision = f"-{effect.raw_revision}" if effect.raw_revision else ""
        raw = RAW / f"{effect.name}{revision}-stable-audio3.wav"
        runtime = ROOT / f"{effect.name}.wav"
        print(f"Generating {effect.name} seed={effect.seed} duration={effect.duration:.1f}s")
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
        fade_start = max(0.0, effect.duration - 0.15)
        subprocess.run([
            "ffmpeg", "-y", "-loglevel", "error", "-i", str(raw),
            "-t", f"{effect.duration:.2f}",
            "-af", f"aresample=48000,afade=t=out:st={fade_start:.2f}:d=0.15",
            "-ar", "48000", "-ac", "2", "-c:a", "pcm_s16le", str(runtime),
        ], check=True)
        print(f"Wrote {runtime}")


if __name__ == "__main__":
    main()
