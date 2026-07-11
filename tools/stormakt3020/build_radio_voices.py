#!/usr/bin/env python3
from __future__ import annotations

import math
import random
import struct
import wave
import zlib
from pathlib import Path


RATE = 48_000
RAW = Path("assets/stormakt3020/radio/raw")
OUTPUT = Path("assets/stormakt3020/radio/voices")
LEGACY_SEEDS = {
    "ebba-grip-en-raw.wav": 302_100,
    "rasmus-gyldentold-en-raw.wav": 302_101,
    "kung-christian-en-raw.wav": 302_102,
}


def read_mono(path: Path) -> tuple[int, list[float]]:
    with wave.open(str(path), "rb") as handle:
        channels = handle.getnchannels()
        width = handle.getsampwidth()
        rate = handle.getframerate()
        frames = handle.readframes(handle.getnframes())
    if width != 2 or channels not in (1, 2):
        raise ValueError(f"{path} must be mono/stereo PCM16 WAV")
    values = struct.unpack(f"<{len(frames) // 2}h", frames)
    if channels == 1:
        return rate, [value / 32768.0 for value in values]
    return rate, [((values[index] + values[index + 1]) * 0.5) / 32768.0 for index in range(0, len(values), 2)]


def resample(samples: list[float], source_rate: int) -> list[float]:
    if source_rate == RATE:
        return samples
    count = max(1, round(len(samples) * RATE / source_rate))
    result: list[float] = []
    scale = source_rate / RATE
    for index in range(count):
        source = min(index * scale, len(samples) - 1)
        left = int(source)
        right = min(left + 1, len(samples) - 1)
        fraction = source - left
        result.append(samples[left] * (1.0 - fraction) + samples[right] * fraction)
    return result


def radio_filter(samples: list[float], seed: int) -> list[float]:
    low_alpha = 1.0 - math.exp(-2.0 * math.pi * 4_500.0 / RATE)
    high_alpha = math.exp(-2.0 * math.pi * 180.0 / RATE)
    low = previous_input = previous_high = 0.0
    filtered: list[float] = []
    for sample in samples:
        low += low_alpha * (sample - low)
        high = high_alpha * (previous_high + low - previous_input)
        previous_input = low
        previous_high = high
        filtered.append(high)

    peak = max((abs(sample) for sample in filtered), default=1.0) or 1.0
    pre_gain = 0.82 / peak
    rng = random.Random(seed)
    result: list[float] = []
    fade = round(0.012 * RATE)
    for index, sample in enumerate(filtered):
        driven = sample * pre_gain * 1.8
        compressed = math.tanh(driven) / math.tanh(1.8)
        noise = rng.uniform(-0.0032, 0.0032)
        edge = min(1.0, index / fade, (len(filtered) - 1 - index) / fade)
        result.append((compressed + noise) * max(0.0, edge) * 0.78)
    return result


def write_stereo(path: Path, samples: list[float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as handle:
        handle.setnchannels(2)
        handle.setsampwidth(2)
        handle.setframerate(RATE)
        payload = bytearray()
        for sample in samples:
            value = round(max(-0.98, min(0.98, sample)) * 32767)
            payload.extend(struct.pack("<hh", value, value))
        handle.writeframes(payload)


def main() -> None:
    sources = sorted(RAW.glob("*-raw.wav"))
    for source in sources:
        output_name = source.name.replace("-raw.wav", "-radio.wav")
        rate, samples = read_mono(source)
        seed = LEGACY_SEEDS.get(source.name, 400_000 + zlib.crc32(source.name.encode("utf-8")) % 100_000)
        processed = radio_filter(resample(samples, rate), seed)
        write_stereo(OUTPUT / output_name, processed)
        print(f"Wrote {output_name}: {len(processed) / RATE:.2f}s")


if __name__ == "__main__":
    main()
