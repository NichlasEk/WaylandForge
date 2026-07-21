#!/usr/bin/env python3
"""Generate the deterministic hybrid SFX family for Stora Balt.

The rapid player shots are synthesized from clean sine chirps so overlapping
shots remain individual ``piow`` pulses. Longer mechanical effects use the
fixed-seed Stable Audio model.
"""
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
    Effect("stora-balt-player-piow-a", 0.18, 3020101,
           "Procedural clean falling sine pulse", 4800),
    Effect("stora-balt-player-piow-b", 0.18, 3020102,
           "Procedural clean falling sine pulse, higher variant", 5200),
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

PLAYER_CHIRPS = {
    "stora-balt-player-piow-a": (1120.0, 390.0),
    "stora-balt-player-piow-b": (1320.0, 465.0),
}


def synthesize_player_piow(path: Path, duration: float, start_hz: float, end_hz: float) -> None:
    sample_rate = 48_000
    frames = round(duration * sample_rate)
    phase = 0.0
    samples: list[float] = []
    for index in range(frames):
        t = index / sample_rate
        progress = index / max(1, frames - 1)
        frequency = end_hz + (start_hz - end_hz) * math.exp(-4.2 * progress)
        phase += math.tau * frequency / sample_rate
        attack = min(1.0, t / 0.004)
        release = max(0.0, 1.0 - progress) ** 2.35
        envelope = attack * release
        tone = math.sin(phase) * 0.88 + math.sin(phase * 2.0 + 0.18) * 0.09
        samples.append(math.tanh(tone * 1.08) * envelope)

    peak = max(abs(sample) for sample in samples)
    scale = 0.68 / max(peak, 1e-9)
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
    RAW.mkdir(parents=True, exist_ok=True)
    ROOT.mkdir(parents=True, exist_ok=True)
    stable_effects = [effect for effect in selected if effect.name not in PLAYER_CHIRPS]
    model = None
    torchaudio = None
    if stable_effects:
        import torchaudio as torchaudio_module
        from stable_audio_3 import StableAudioModel

        torchaudio = torchaudio_module
        print("Loading Stable Audio 3 Small-SFX once...")
        model = StableAudioModel.from_pretrained("small-sfx", device="cuda", model_half=True)
    for effect in selected:
        procedural = effect.name in PLAYER_CHIRPS
        raw = RAW / f"{effect.name}-{'procedural' if procedural else 'stable-audio3'}.wav"
        runtime = ROOT / f"{effect.name}.wav"
        if procedural:
            print(f"Synthesizing {effect.name} duration={effect.duration:.2f}s")
            synthesize_player_piow(raw, effect.duration, *PLAYER_CHIRPS[effect.name])
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
