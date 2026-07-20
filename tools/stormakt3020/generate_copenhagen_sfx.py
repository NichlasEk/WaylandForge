#!/usr/bin/env python3
"""Generate the fixed-seed Stable Audio 3 SFX family for Copenhagen Ring.

Run with the isolated Stable Audio 3 virtual environment so the model is
loaded once:

  /home/nichlas/ai/stable-audio-3/.venv/bin/python \
    tools/stormakt3020/generate_copenhagen_sfx.py
"""
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
os.environ.setdefault("HF_HUB_OFFLINE", "1")
os.environ.setdefault("TRANSFORMERS_OFFLINE", "1")

import torchaudio
from stable_audio_3 import StableAudioModel


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
ROOT = REPOSITORY_ROOT / "assets" / "stormakt3020" / "sfx"
RAW = ROOT / "raw"
NEGATIVE = "music ambience speech voices long reverb repeated sequence laser beam generic sci-fi blaster"


@dataclass(frozen=True)
class Effect:
    name: str
    duration: float
    seed: int
    prompt: str


EFFECTS = [
    Effect("copenhagen-ring-lock", 1.8, 3020701,
           "Single colossal Danish crown lock engaging in outer space, three iron crown teeth rotate and clamp, baroque brass gears, heavy final seal impact, short dry game sound effect"),
    Effect("copenhagen-clock-strike", 1.5, 3020702,
           "Single haunted naval clock striking one registered hour, large bronze bell hit, clockwork escapement snap, restrained cannon mechanism tail, short dry game sound effect"),
    Effect("copenhagen-frederik-chain", 1.7, 3020703,
           "Single royal confiscation chain fired and pulled taut, heavy iron links whip forward, brass ledger lock bites shut, mechanical strain, short dry game sound effect"),
    Effect("copenhagen-ledger-blade", 1.1, 3020704,
           "Single enormous armored bookkeeping blade rushing past, sharp steel sweep, parchment register flutter, brass axle recoil, compact dry boss game sound effect"),
    Effect("copenhagen-eye-forecast", 1.6, 3020705,
           "Single mechanical Oresund observatory eye calculating a movement forecast, glass iris aperture clicks, optical gyroscope accelerates, cold warning tone resolves into a relay snap, dry game sound effect"),
    Effect("copenhagen-cross-charge", 1.5, 3020706,
           "Single red and white Danish mechanical cross formation charging through space, four armored drone engines surge together, canvas and brass vibration, forceful pass-by, dry game sound effect"),
    Effect("copenhagen-shield-wall", 1.4, 3020707,
           "Single seven-node naval shield wall deploying, seven compact iron shutters lock in sequence, taut chain and low electrical field thump, dry game sound effect"),
    Effect("copenhagen-super-compile", 2.0, 3020708,
           "Single impossible royal superfregate memory weapon compiling, old cannon breech, rocket rack and silver lamp engage in three clockwork stages, ominous final relay, dry boss game sound effect"),
    Effect("copenhagen-anchor-break", 1.4, 3020709,
           "Single gigantic burning dock anchor chain breaking during a starship landing, iron shackle snaps, chain whips away, stone and sparks scatter, compact dry game sound effect"),
    Effect("copenhagen-ground-gate", 1.8, 3020710,
           "Single underground Copenhagen stone and silver gate opening, ancient masonry shifts, heavy iron latch withdraws, cold silver mechanism settles, dry dungeon game sound effect"),
    Effect("copenhagen-heart-compile", 1.9, 3020711,
           "Single mechanical silver heart interpreting a written law, metallic heartbeat, tiny typewriter relays, liquid silver unfolds into a portal with a soft final chime, dry game sound effect"),
    Effect("copenhagen-material-rewrite", 1.4, 3020712,
           "Single programmable silver tool rewritten between shield and sword, molten metal folds and hardens, runic relay clicks, compact crystalline finish, dry game sound effect"),
    Effect("copenhagen-legend-moose", 3.2, 3020713,
           "A short violent charge of seven spectral Scandinavian war moose across a stone hall, heavy rapid hoofbeats, antler clatter, old Carolean plate armor rattling, one deep animal bellow, cold silver magic tail, physical and organic, no gunshot, dry game sound effect"),
    Effect("copenhagen-legend-impact", 0.38, 3020723,
           "Single heavy armored moose hoof striking a stone floor and an iron enemy, compact low thud, antler and Carolean plate clink, extremely short dry physical game impact, no magic blast"),
    Effect("copenhagen-saga-order", 1.5, 3020714,
           "Single absurd baroque saga king issuing a physical royal order, three crooked crowns stamp together, parchment mechanism slams, lion ornaments growl mechanically, dry boss game sound effect, no speech"),
    Effect("copenhagen-saga-horse", 2.2, 3020715,
           "Single mechanical wooden war horse fused with a royal ship charging across stone, pounding hooves, timber hull creak, brass tack and tiny sail snap, comic but dangerous, dry boss game sound effect"),
    Effect("copenhagen-pen-scratch", 1.0, 3020716,
           "Single enormous hostile quill writing a correction into metal parchment, vicious nib scratch, ink mechanism chatter, brief steel flourish, compact dry game sound effect"),
    Effect("copenhagen-edit-stamp", 1.4, 3020717,
           "Single royal marginal correction executed, giant printing press stamp, punctuation type blocks scatter, black ink crack and brass recoil, dry boss game sound effect"),
    Effect("copenhagen-wrath-claim", 1.8, 3020718,
           "Single terrifying royal legal claim made physical, crown halo locks, guilt chains tighten, three deep brass seals strike, cold silver pressure wave, dry final boss game sound effect"),
    Effect("copenhagen-word-reclaim", 1.3, 3020719,
           "Single stolen armored word reclaimed from a king, metal letters tear free, chain snaps, silver fragments rush back into parchment, compact dry game sound effect"),
    Effect("copenhagen-circuit-open", 2.0, 3020720,
           "Single ancient silver claim circuit opening on a stone floor, concentric metal rings rotate, eight contacts engage, low current rises into one clean lock tone, dry final boss game sound effect"),
    Effect("copenhagen-royal-armor-break", 1.6, 3020721,
           "Single layer of immense baroque royal armor breaking, black iron plate fractures, gold crown fittings scatter, heavy chain recoil and cold silver core release, dry boss game sound effect"),
    Effect("copenhagen-codex-recognize", 2.4, 3020722,
           "Single forbidden mechanical codex recognizing its two-hundred-fifty-fifth silver instance, thick book unlocks, 254 tiny relays answer in a wave, cold half-second clock pulse, final incomplete typewriter mark, dry game sound effect"),
]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", action="append", choices=[effect.name for effect in EFFECTS],
                        help="Generate only the named effect; repeat for more than one.")
    parser.add_argument("--runtime-only", action="store_true",
                        help="Rebuild 48 kHz runtime files from existing raw generations.")
    args = parser.parse_args()
    selected = [effect for effect in EFFECTS if not args.only or effect.name in args.only]
    RAW.mkdir(parents=True, exist_ok=True)
    ROOT.mkdir(parents=True, exist_ok=True)
    model = None
    if not args.runtime_only:
        print("Loading Stable Audio 3 Small-SFX once in offline mode...")
        model = StableAudioModel.from_pretrained("small-sfx", device="cuda", model_half=True)
    for effect in selected:
        raw = RAW / f"{effect.name}-stable-audio3.wav"
        runtime = ROOT / f"{effect.name}.wav"
        if not args.runtime_only:
            assert model is not None
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
        elif not raw.exists():
            raise FileNotFoundError(f"Missing raw generation for {effect.name}: {raw}")
        fade_start = max(0.0, effect.duration - 0.12)
        subprocess.run([
            "ffmpeg", "-y", "-loglevel", "error", "-i", str(raw),
            "-t", f"{effect.duration:.2f}",
            "-af", f"loudnorm=I=-10:TP=-1:LRA=7,aresample=48000,afade=t=out:st={fade_start:.2f}:d=0.12",
            "-ar", "48000", "-ac", "2", "-c:a", "pcm_s16le", str(runtime),
        ], check=True)
        print(f"Wrote {runtime}")


if __name__ == "__main__":
    main()
