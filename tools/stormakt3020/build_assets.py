#!/usr/bin/env python3
from __future__ import annotations

import argparse
import struct
from pathlib import Path

from PIL import Image


SPRITES = [
    ("player", (25, 35, 315, 390), (44, 54)),
    ("enemy_crown", (85, 525, 380, 855), (34, 38)),
    ("enemy_caroline", (465, 525, 760, 840), (34, 34)),
    ("enemy_guard", (900, 520, 1215, 855), (42, 42)),
    ("shot_blue", (70, 920, 265, 1045), (11, 18)),
    ("shot_broadside", (475, 930, 650, 1010), (20, 10)),
    ("shot_cannon", (655, 895, 875, 1015), (18, 9)),
    ("burst", (955, 870, 1210, 1065), (32, 28)),
]


def chroma_alpha(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            green_bias = g - max(r, b)
            if g > 170 and green_bias > 45:
                pixels[x, y] = (r, g, b, 0)
            elif g > 120 and green_bias > 20:
                pixels[x, y] = (r, g, b, min(a, 90))
    return rgba


def trim_alpha(image: Image.Image) -> Image.Image:
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        return image
    return image.crop(bbox)


def argb_pixels(image: Image.Image) -> bytes:
    data = bytearray()
    raw = image.tobytes()
    for index in range(0, len(raw), 4):
        r, g, b, a = raw[index], raw[index + 1], raw[index + 2], raw[index + 3]
        data.extend(struct.pack("<I", (a << 24) | (r << 16) | (g << 8) | b))
    return bytes(data)


def build(input_path: Path, output_path: Path) -> None:
    source = Image.open(input_path)
    entries: list[tuple[str, Image.Image]] = []
    for name, crop, size in SPRITES:
        sprite = chroma_alpha(source.crop(crop))
        sprite = trim_alpha(sprite)
        sprite.thumbnail(size, Image.Resampling.LANCZOS)
        entries.append((name, sprite))

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("wb") as handle:
        handle.write(b"WFSA")
        handle.write(struct.pack("<II", 1, len(entries)))
        for name, sprite in entries:
            encoded_name = name.encode("ascii")
            if len(encoded_name) > 31:
                raise ValueError(f"sprite name too long: {name}")
            pixels = argb_pixels(sprite)
            handle.write(encoded_name.ljust(32, b"\0"))
            handle.write(struct.pack("<III", sprite.width, sprite.height, len(pixels)))
            handle.write(pixels)


def main() -> None:
    parser = argparse.ArgumentParser(description="Build Stormakt 3020 WFSA sprites from a concept PNG.")
    parser.add_argument("--input", type=Path, default=Path("assets/stormakt3020/karl-cclv-sprite-concept.png"))
    parser.add_argument("--output", type=Path, default=Path("assets/stormakt3020/stormakt3020.wfsa"))
    args = parser.parse_args()
    build(args.input, args.output)


if __name__ == "__main__":
    main()
