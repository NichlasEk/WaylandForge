#!/usr/bin/env python3
from __future__ import annotations

import argparse
import struct
from pathlib import Path

from PIL import Image


PRIMARY_SPRITES = [
    ("player", (330, 25, 620, 410), (44, 58)),
    ("enemy_crown", (85, 525, 380, 855), (34, 38)),
    ("enemy_caroline", (465, 525, 760, 840), (34, 34)),
    ("enemy_guard", (900, 520, 1215, 855), (42, 42)),
    ("shot_blue", (70, 920, 265, 1045), (11, 18)),
    ("shot_broadside", (475, 930, 650, 1010), (20, 10)),
    ("shot_cannon", (655, 895, 875, 1015), (18, 9)),
    ("burst", (955, 870, 1210, 1065), (32, 28)),
]

DANISH_SPRITES = [
    ("boss_kronens_tiende", (70, 15, 1015, 555), (124, 74)),
    ("boss_kronens_tiende_damaged", (70, 545, 1015, 1045), (124, 74)),
    ("fogde_sloop", (50, 1025, 365, 1425), (34, 44)),
    ("fogde_sloop_breakaway", (385, 1025, 705, 1425), (36, 44)),
    ("enemy_tax_seal", (720, 1055, 1040, 1395), (36, 36)),
]

RADIO_PORTRAITS = [
    ("portrait_ebba_neutral", (0, 0, 512, 512), (38, 38)),
    ("portrait_ebba_speak", (512, 0, 1024, 512), (38, 38)),
    ("portrait_rasmus_neutral", (0, 512, 512, 1024), (38, 38)),
    ("portrait_rasmus_speak", (512, 512, 1024, 1024), (38, 38)),
    ("portrait_christian_neutral", (0, 1024, 512, 1536), (38, 38)),
    ("portrait_christian_speak", (512, 1024, 1024, 1536), (38, 38)),
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


def append_sprites(
    entries: list[tuple[str, Image.Image]],
    source: Image.Image,
    definitions: list[tuple[str, tuple[int, int, int, int], tuple[int, int]]],
) -> None:
    for name, crop, size in definitions:
        sprite = chroma_alpha(source.crop(crop))
        sprite = trim_alpha(sprite)
        sprite.thumbnail(size, Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def build(input_path: Path, danish_input_path: Path, portrait_input_path: Path, output_path: Path) -> None:
    source = Image.open(input_path)
    danish_source = Image.open(danish_input_path)
    portrait_source = Image.open(portrait_input_path)
    entries: list[tuple[str, Image.Image]] = []
    append_sprites(entries, source, PRIMARY_SPRITES)
    append_sprites(entries, danish_source, DANISH_SPRITES)
    append_sprites(entries, portrait_source, RADIO_PORTRAITS)

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
    parser.add_argument("--input", type=Path, default=Path("assets/stormakt3020/karl-cclv-swedish-hero-danish-enemies-v3.png"))
    parser.add_argument(
        "--danish-input",
        type=Path,
        default=Path("assets/stormakt3020/stormakt-danish-boss-enemies-v1.png"),
    )
    parser.add_argument(
        "--portrait-input",
        type=Path,
        default=Path("assets/stormakt3020/stormakt-radio-portraits-v1.png"),
    )
    parser.add_argument("--output", type=Path, default=Path("assets/stormakt3020/stormakt3020.wfsa"))
    args = parser.parse_args()
    build(args.input, args.danish_input, args.portrait_input, args.output)


if __name__ == "__main__":
    main()
