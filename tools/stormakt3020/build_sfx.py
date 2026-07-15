#!/usr/bin/env python3
from __future__ import annotations

import math
import random
import struct
import wave
from pathlib import Path


RATE = 48_000
OUTPUT = Path("assets/stormakt3020/sfx")


def envelope(t: float, duration: float, attack: float = 0.005, release: float = 0.12) -> float:
    attack_gain = min(1.0, t / max(attack, 1e-6))
    release_gain = min(1.0, (duration - t) / max(release, 1e-6))
    return max(0.0, min(attack_gain, release_gain))


def normalize(stereo: list[tuple[float, float]], peak: float = 0.86) -> list[tuple[float, float]]:
    maximum = max(max(abs(left), abs(right)) for left, right in stereo) or 1.0
    gain = peak / maximum
    return [(left * gain, right * gain) for left, right in stereo]


def write_wav(name: str, stereo: list[tuple[float, float]]) -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUTPUT / name), "wb") as handle:
        handle.setnchannels(2)
        handle.setsampwidth(2)
        handle.setframerate(RATE)
        payload = bytearray()
        for left, right in normalize(stereo):
            payload.extend(struct.pack("<hh", round(left * 32767), round(right * 32767)))
        handle.writeframes(payload)


def twin_cannon() -> list[tuple[float, float]]:
    duration = 0.13
    result = []
    rng = random.Random(302001)
    for index in range(round(duration * RATE)):
        t = index / RATE
        left = right = 0.0
        for delay, pan in ((0.0, -0.55), (0.032, 0.55)):
            local = t - delay
            if local < 0.0 or local >= 0.09:
                continue
            phase = 2.0 * math.pi * (1350.0 * local - 5200.0 * local * local)
            pulse = (0.65 * math.sin(phase) + 0.35 * rng.uniform(-1.0, 1.0)) * math.exp(-34.0 * local)
            left += pulse * (1.0 - max(0.0, pan))
            right += pulse * (1.0 + min(0.0, pan))
        result.append((left, right))
    return result


def broadside() -> list[tuple[float, float]]:
    duration = 0.48
    result = []
    rng = random.Random(302002)
    low_noise = 0.0
    for index in range(round(duration * RATE)):
        t = index / RATE
        raw = rng.uniform(-1.0, 1.0)
        low_noise += 0.055 * (raw - low_noise)
        boom = math.sin(2.0 * math.pi * (92.0 * t - 54.0 * t * t)) * math.exp(-7.5 * t)
        crack = raw * math.exp(-30.0 * t)
        smoke = low_noise * math.exp(-4.5 * t)
        metal = math.sin(2.0 * math.pi * 410.0 * t) * math.exp(-13.0 * t)
        sample = (0.68 * boom) + (0.32 * crack) + (0.35 * smoke) + (0.12 * metal)
        result.append((sample, sample * 0.93))
    return result


def enemy_explosion() -> list[tuple[float, float]]:
    duration = 0.68
    result = []
    rng = random.Random(302003)
    low_left = low_right = 0.0
    for index in range(round(duration * RATE)):
        t = index / RATE
        left_noise = rng.uniform(-1.0, 1.0)
        right_noise = rng.uniform(-1.0, 1.0)
        cutoff = 0.12 - (0.09 * t / duration)
        low_left += cutoff * (left_noise - low_left)
        low_right += cutoff * (right_noise - low_right)
        body = math.sin(2.0 * math.pi * (118.0 * t - 65.0 * t * t)) * math.exp(-5.2 * t)
        debris = (math.sin(2.0 * math.pi * 733.0 * t) + math.sin(2.0 * math.pi * 997.0 * t)) * math.exp(-9.0 * t)
        gain = envelope(t, duration, attack=0.001, release=0.24)
        result.append(((0.48 * low_left + 0.48 * body + 0.08 * debris) * gain,
                       (0.48 * low_right + 0.48 * body - 0.08 * debris) * gain))
    return result


def hull_hit() -> list[tuple[float, float]]:
    duration = 0.34
    result = []
    rng = random.Random(302004)
    for index in range(round(duration * RATE)):
        t = index / RATE
        impact = rng.uniform(-1.0, 1.0) * math.exp(-55.0 * t)
        ring = (math.sin(2.0 * math.pi * 530.0 * t) +
                0.7 * math.sin(2.0 * math.pi * 817.0 * t) +
                0.45 * math.sin(2.0 * math.pi * 1193.0 * t)) * math.exp(-10.5 * t)
        result.append((0.45 * impact + 0.42 * ring, 0.45 * impact - 0.38 * ring))
    return result


def deploy_chime() -> list[tuple[float, float]]:
    duration = 0.62
    notes = (293.66, 349.23, 440.0, 587.33)
    result = []
    for index in range(round(duration * RATE)):
        t = index / RATE
        sample = 0.0
        for note_index, frequency in enumerate(notes):
            local = t - (note_index * 0.095)
            if local < 0.0:
                continue
            sample += math.sin(2.0 * math.pi * frequency * local) * math.exp(-5.5 * local) * 0.34
            sample += math.sin(2.0 * math.pi * frequency * 2.01 * local) * math.exp(-9.0 * local) * 0.08
        pan = math.sin(t * math.pi / duration) * 0.18
        result.append((sample * (1.0 - pan), sample * (1.0 + pan)))
    return result


def oresund_guard_shot() -> list[tuple[float, float]]:
    duration = 0.24
    result = []
    rng = random.Random(330_301)
    for index in range(round(duration * RATE)):
        t = index / RATE
        sweep = 2.0 * math.pi * (2_100.0 * t - 5_800.0 * t * t)
        brass = math.sin(sweep) * math.exp(-22.0 * t)
        relay = math.sin(2.0 * math.pi * 680.0 * t) * math.exp(-13.0 * t)
        crack = rng.uniform(-1.0, 1.0) * math.exp(-48.0 * t)
        sample = 0.54 * brass + 0.24 * relay + 0.22 * crack
        result.append((sample * 0.86, sample))
    return result


def oresund_switch_break() -> list[tuple[float, float]]:
    duration = 0.46
    result = []
    rng = random.Random(330_302)
    for index in range(round(duration * RATE)):
        t = index / RATE
        clank = 0.0
        for delay, frequency in ((0.0, 310.0), (0.075, 227.0), (0.14, 166.0)):
            local = t - delay
            if local >= 0.0:
                clank += math.sin(2.0 * math.pi * frequency * local) * math.exp(-16.0 * local)
        arc = rng.uniform(-1.0, 1.0) * math.exp(-9.0 * t) * (0.35 + 0.65 * abs(math.sin(2.0 * math.pi * 31.0 * t)))
        servo = math.sin(2.0 * math.pi * (920.0 * t - 340.0 * t * t)) * math.exp(-7.0 * t)
        result.append((0.48 * clank + 0.18 * arc + 0.16 * servo,
                       0.44 * clank - 0.18 * arc + 0.16 * servo))
    return result


def main() -> None:
    write_wav("twin-cannon.wav", twin_cannon())
    write_wav("broadside.wav", broadside())
    write_wav("enemy-explosion.wav", enemy_explosion())
    write_wav("hull-hit.wav", hull_hit())
    write_wav("deploy-chime.wav", deploy_chime())
    write_wav("oresund-guard-shot-procedural.wav", oresund_guard_shot())
    write_wav("oresund-switch-break-procedural.wav", oresund_switch_break())
    print(f"Wrote 5 core and 2 Öresund fallback effects to {OUTPUT}")


if __name__ == "__main__":
    main()
