#!/usr/bin/env python3
"""Generate the Oresund polish SFX family with clean rapid-fire pulses."""
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
    "music ambience speech voices repeated sequence long reverb motor chainsaw buzz rasp "
    "piercing treble harsh laser screech siren distortion white noise"
)


@dataclass(frozen=True)
class Effect:
    name: str
    duration: float
    seed: int
    prompt: str
    lowpass: int = 6200


EFFECTS = [
    Effect("oresund-guard-shot", 0.19, 3030301, "Procedural clean bridge guard pulse", 4300),
    Effect("oresund-rail-pulse", 0.23, 3030302, "Procedural low railway cannon pulse", 3900),
    Effect("oresund-fortress-pulse", 0.25, 3030303, "Procedural restrained fortress pulse", 3600),
    Effect("oresund-crown-pulse", 0.27, 3030304, "Procedural crown core energy pulse", 4400),
    Effect("oresund-soren-strike", 0.30, 3030305, "Procedural copper corsair strike", 4700),
    Effect("oresund-switch-break", 0.48, 3030306, "Procedural railway switch clank", 3600),
    Effect("oresund-laser-relay", 0.78, 3030307,
           "One orbital bridge laser relay charging and snapping off, low cyan coil note and compact brass contact, dry isolated game sound, not piercing", 5200),
    Effect("oresund-flap-motor", 0.95, 3030308,
           "One armored bridge flap moving on two deep hydraulic pistons and locking once, compact dry heavy machine game sound", 5000),
    Effect("oresund-train-rumble", 1.20, 3030309,
           "One short armored railway train pass, low wheel rhythm and heavy chassis over an iron bridge, compact dry game sound", 4600),
    Effect("oresund-train-crash", 1.30, 3030310,
           "One armored railway train striking an iron buffer, deep impact, bent wheels and brief settling debris, dry isolated game sound", 5600),
    Effect("oresund-fortress-lock", 1.05, 3030311,
           "Two orbital iron fortresses locking together once, deep synchronized docking impact and short brass crossbolt, dry boss game sound", 5200),
    Effect("oresund-crown-core-open", 1.00, 3030312,
           "One ancient brass crown reactor opening, heavy claws retract and a low cyan crystal heart wakes, dry boss game sound, no laser", 5600),
    Effect("oresund-crown-core-break", 1.45, 3030313,
           "One ancient brass crown reactor breaking, deep iron rupture, crystal fragments and a fading low electrical field, decisive dry boss sound", 6000),
    Effect("oresund-coupling-break", 0.62, 3030314,
           "One armored railway coupling snapping under tension, compact iron crack and two loose chain links, dry isolated game sound", 5200),
    Effect("oresund-fortress-arrival", 0.95, 3030315,
           "Two immense coastal iron fortresses entering and powering their harbor locks, low twin warning knocks and one brass relay, dry boss game sound", 5000),
    Effect("oresund-fortress-breach", 0.72, 3030316,
           "One section of an orbital iron fortress armor breaching, deep plate buckle and short masonry debris, compact dry isolated game sound", 5400),
]

CHIRPS = {
    "oresund-guard-shot": (740.0, 245.0, 0.05),
    "oresund-rail-pulse": (590.0, 190.0, 0.10),
    "oresund-fortress-pulse": (500.0, 160.0, 0.13),
    "oresund-crown-pulse": (850.0, 275.0, 0.08),
    "oresund-soren-strike": (920.0, 305.0, 0.11),
}


def write_stereo(path: Path, samples: list[float], scale: float = 0.68) -> None:
    peak = max(max(abs(sample) for sample in samples), 1e-9)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(48_000)
        pcm = bytearray()
        for sample in samples:
            value = max(-32768, min(32767, round(sample / peak * scale * 32767)))
            pcm.extend(struct.pack("<hh", value, value))
        output.writeframes(pcm)


def synthesize_chirp(path: Path, effect: Effect) -> None:
    start_hz, end_hz, harmonic = CHIRPS[effect.name]
    frames = round(effect.duration * 48_000)
    phase = 0.0
    samples = []
    for index in range(frames):
        progress = index / max(1, frames - 1)
        frequency = end_hz + (start_hz - end_hz) * math.exp(-4.2 * progress)
        phase += math.tau * frequency / 48_000
        attack = min(1.0, index / (48_000 * 0.005))
        envelope = attack * max(0.0, 1.0 - progress) ** 2.35
        tone = math.sin(phase) + harmonic * math.sin(phase * 2.0 + 0.18)
        samples.append(math.tanh(tone * 1.04) * envelope)
    write_stereo(path, samples)


def synthesize_switch(path: Path, effect: Effect) -> None:
    frames = round(effect.duration * 48_000)
    samples = []
    for index in range(frames):
        t = index / 48_000
        sample = 0.0
        for delay, frequency, strength in ((0.0, 285.0, 1.0), (0.075, 210.0, 0.72), (0.145, 148.0, 0.55)):
            local = t - delay
            if local >= 0:
                sample += math.sin(math.tau * frequency * local) * math.exp(-15.0 * local) * strength
        samples.append(sample)
    write_stereo(path, samples, 0.62)


def synthesize_rumble(path: Path, effect: Effect) -> None:
    frames = round(effect.duration * 48_000)
    samples = []
    for index in range(frames):
        t = index / 48_000
        progress = index / max(1, frames - 1)
        envelope = min(1.0, t / 0.06) * min(1.0, (1.0 - progress) / 0.10)
        chassis = math.sin(math.tau * 62.0 * t) * 0.24
        wheels = 0.0
        for delay in (0.08, 0.22, 0.42, 0.56, 0.76, 0.90):
            local = t - delay
            if local >= 0:
                wheels += math.sin(math.tau * 185.0 * local) * math.exp(-30.0 * local) * 0.34
        samples.append((chassis + wheels) * envelope)
    write_stereo(path, samples, 0.58)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", action="append", choices=[effect.name for effect in EFFECTS])
    args = parser.parse_args()
    selected = [effect for effect in EFFECTS if not args.only or effect.name in args.only]
    ROOT.mkdir(parents=True, exist_ok=True)
    RAW.mkdir(parents=True, exist_ok=True)

    procedural_names = set(CHIRPS) | {"oresund-switch-break", "oresund-train-rumble"}
    stable_effects = [effect for effect in selected if effect.name not in procedural_names]
    model = None
    torchaudio = None
    if stable_effects:
        import torchaudio as torchaudio_module
        from stable_audio_3 import StableAudioModel

        torchaudio = torchaudio_module
        print("Loading Stable Audio 3 Small-SFX once...")
        model = StableAudioModel.from_pretrained("small-sfx", device="cuda", model_half=True)

    for effect in selected:
        procedural = effect.name in procedural_names
        raw = RAW / f"{effect.name}-{'procedural' if procedural else 'stable-audio3'}.wav"
        runtime = ROOT / f"{effect.name}.wav"
        if effect.name in CHIRPS:
            print(f"Synthesizing {effect.name} duration={effect.duration:.2f}s")
            synthesize_chirp(raw, effect)
        elif effect.name == "oresund-switch-break":
            print(f"Synthesizing {effect.name} duration={effect.duration:.2f}s")
            synthesize_switch(raw, effect)
        elif effect.name == "oresund-train-rumble":
            print(f"Synthesizing {effect.name} duration={effect.duration:.2f}s")
            synthesize_rumble(raw, effect)
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
        fade_duration = min(0.14, effect.duration / 3)
        fade_start = effect.duration - fade_duration
        subprocess.run([
            "ffmpeg", "-y", "-loglevel", "error", "-i", str(raw),
            "-t", f"{effect.duration:.2f}",
            "-af", f"aresample=48000,lowpass=f={effect.lowpass},volume=-1dB,"
                   f"alimiter=limit=0.89:level=false,afade=t=out:st={fade_start:.2f}:d={fade_duration:.2f}",
            "-ar", "48000", "-ac", "2", "-c:a", "pcm_s16le", str(runtime),
        ], check=True)
        print(f"Wrote {runtime}")


if __name__ == "__main__":
    main()
