#!/usr/bin/env python3
from __future__ import annotations

import argparse
import struct
from pathlib import Path

from PIL import Image, ImageOps


PRIMARY_SPRITES = [
    ("enemy_crown", (85, 525, 380, 855), (34, 38)),
    ("enemy_caroline", (465, 525, 760, 840), (34, 34)),
    ("enemy_guard", (900, 520, 1215, 855), (42, 42)),
    ("shot_blue", (70, 920, 265, 1045), (11, 18)),
    ("shot_broadside", (475, 930, 650, 1010), (20, 10)),
    ("shot_cannon", (655, 895, 875, 1015), (18, 9)),
    ("burst", (955, 870, 1210, 1065), (32, 28)),
]

PLAYER_SPRITES = [
    # Crop before the painted engine flames; runtime draws thrust only while moving.
    ("player", (0, 0, 749, 875), (52, 64)),
    ("player_hot", (749, 0, 1499, 875), (52, 64)),
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

ENVIRONMENT_SPRITES = [
    ("bridge_span_generated", (0, 0, 530, 371), (64, 72)),
    ("bridge_span_damaged_generated", (530, 0, 1060, 371), (64, 72)),
    ("bridge_turret_generated", (0, 371, 530, 742), (34, 36)),
    ("bridge_node_generated", (530, 371, 1060, 742), (28, 28)),
    ("bridge_arch_left_generated", (0, 742, 530, 1113), (100, 62)),
    ("bridge_arch_right_generated", (530, 742, 1060, 1113), (100, 62)),
    ("swedish_wreck_generated", (0, 1113, 530, 1484), (92, 58)),
    ("belt_asteroids_generated", (530, 1113, 1060, 1484), (88, 62)),
]

COMBAT_DETAIL_SPRITES = [
    ("bridge_debris_slab", (0, 0, 500, 350), (54, 34)),
    ("bridge_debris_rail", (500, 0, 1000, 350), (50, 22)),
    ("bridge_debris_machine", (1000, 0, 1499, 350), (36, 30)),
    ("enemy_bridge_cannon", (0, 350, 500, 700), (38, 42)),
    ("boss_broadside_cannon", (500, 350, 1000, 700), (44, 30)),
    ("enemy_bridge_cannon_wreck", (1000, 350, 1499, 700), (38, 42)),
    ("boss_dock_turret", (0, 350, 500, 700), (22, 24)),
    ("boss_dock_turret_wreck", (1000, 350, 1499, 700), (22, 24)),
    ("boss_broadside_cannon_wreck", (1000, 350, 1499, 700), (44, 34)),
    ("enemy_shot_red", (0, 700, 500, 1049), (8, 12)),
    ("enemy_shot_white", (500, 700, 1000, 1049), (8, 12)),
    ("enemy_shot_seal", (1000, 700, 1499, 1049), (10, 10)),
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
        if name == "boss_broadside_cannon_wreck":
            sprite = sprite.transpose(Image.Transpose.ROTATE_270)
        sprite.thumbnail(size, Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_two_frame_portrait(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    half = source.width // 2
    for name, crop in [
        ("portrait_soren_neutral", (0, 0, half, source.height)),
        ("portrait_soren_speak", (half, 0, source.width, source.height)),
    ]:
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        sprite.thumbnail((38, 38), Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_three_frame_corsair(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    third = source.width // 3
    definitions = [
        ("soren_corsair", (0, 0, third, source.height)),
        ("soren_corsair_boost", (third, 0, third * 2, source.height)),
        ("soren_corsair_damaged", (third * 2, 0, source.width, source.height)),
    ]
    for name, crop in definitions:
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        sprite.thumbnail((58, 58), Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_skanska_props(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    third = source.width // 3
    definitions = [
        ("skanska_crystal_pines", (0, 0, third, source.height), (48, 78)),
        ("skanska_kiln_moon", (third, 0, third * 2, source.height), (58, 58)),
        ("skanska_mining_wreck", (third * 2, 0, source.width, source.height), (62, 72)),
    ]
    for name, crop, size in definitions:
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        sprite.thumbnail(size, Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_glimminge(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    first_boundary = 706
    second_boundary = 1400
    definitions = [
        ("glimminge_jarn", (0, 0, first_boundary, source.height), (124, 82)),
        ("glimminge_jarn_damaged", (first_boundary, 0, second_boundary, source.height), (124, 82)),
        ("glimminge_drill_turret", (second_boundary, 0, source.width, source.height), (34, 48)),
    ]
    for name, crop, size in definitions:
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        sprite.thumbnail(size, Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_skanska_enemies(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    first_boundary = 402
    second_boundary = 947
    definitions = [
        ("snapphane_mist_drone", (0, 0, first_boundary, source.height), (34, 40)),
        ("fogde_convoy_barge", (first_boundary, 0, second_boundary, source.height), (40, 48)),
        ("fogde_convoy_barge_damaged", (second_boundary, 0, source.width, source.height), (40, 48)),
    ]
    for name, crop, size in definitions:
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        sprite.thumbnail(size, Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_glimminge_animations(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    first_boundary = 738
    second_boundary = 1280
    definitions = [
        ("glimminge_shield_braced", (0, 0, first_boundary, source.height), (142, 82)),
        ("glimminge_burning", (first_boundary, 0, second_boundary, source.height), (124, 82)),
        ("glimminge_wreck", (second_boundary, 0, source.width, source.height), (124, 72)),
    ]
    for name, crop, size in definitions:
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        sprite.thumbnail(size, Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_birgitte_portrait(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    half = source.width // 2
    for name, crop in [
        ("portrait_birgitte_neutral", (0, 0, half, source.height)),
        ("portrait_birgitte_speak", (half, 0, source.width, source.height)),
    ]:
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        sprite.thumbnail((38, 38), Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_skanska_signal_beacon(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    half = source.width // 2
    for name, crop in [
        ("skanska_signal_beacon", (0, 0, half, source.height)),
        ("skanska_signal_beacon_damaged", (half, 0, source.width, source.height)),
    ]:
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        sprite.thumbnail((34, 40), Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_glimminge_iron_raven(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    first_boundary = 544
    second_boundary = 1262
    definitions = [
        ("glimminge_iron_raven", (0, 0, first_boundary, source.height), (38, 40)),
        ("glimminge_iron_raven_attack", (first_boundary, 0, second_boundary, source.height), (42, 40)),
        ("glimminge_iron_raven_damaged", (second_boundary, 0, source.width, source.height), (38, 40)),
    ]
    for name, crop, size in definitions:
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        sprite.thumbnail(size, Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_skanska_projectiles(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    third = source.width // 3
    definitions = [
        ("skanska_shot_signal", (0, 0, third, source.height), (8, 14)),
        ("fogde_convoy_shot", (third, 0, third * 2, source.height), (8, 14)),
        ("glimminge_shot_iron", (third * 2, 0, source.width, source.height), (8, 14)),
    ]
    for name, crop, size in definitions:
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        sprite.thumbnail(size, Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_skanska_combat_details(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    first_boundary = 725
    second_boundary = 1098
    definitions = [
        ("soren_radar_decoy", (0, 0, first_boundary, source.height), (30, 30)),
        ("soren_copper_shot", (first_boundary, 0, second_boundary, source.height), (8, 14)),
        ("glimminge_crystal_spear", (second_boundary, 0, source.width, source.height), (18, 44)),
    ]
    for name, crop, size in definitions:
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        sprite.thumbnail(size, Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_rts_sheet(
    entries: list[tuple[str, Image.Image]],
    source: Image.Image,
    columns: int,
    definitions: list[tuple[str, int, int, tuple[int, int]]],
) -> None:
    cell_width = source.width // columns
    cell_height = source.height // 2
    reference_scales: dict[str, float] = {}
    groups = {
        "rts_steam_work": "rts_steam_idle", "rts_crusher_work": "rts_crusher_idle",
        "rts_tower_fire": "rts_tower_idle", "rts_carolean_fire": "rts_carolean_ready",
        "rts_carolean_reload": "rts_carolean_ready", "rts_carolean_hit": "rts_carolean_ready",
        "rts_moose_charge": "rts_moose_ready", "rts_moose_fire": "rts_moose_ready",
        "rts_moose_hit": "rts_moose_ready", "rts_toll_attack": "rts_toll_ready",
        "rts_pike_attack": "rts_pike_ready", "rts_mastiff_attack": "rts_mastiff_ready",
        "rts_boar_fuse": "rts_boar_ready", "rts_organ_fire": "rts_organ_ready",
        "rts_toldhus_open": "rts_toldhus_intact", "rts_toldhus_damaged": "rts_toldhus_intact",
        "rts_seal_broken": "rts_seal_intact",
    }
    for name, column, row, size in definitions:
        right = source.width if column == columns - 1 else (column + 1) * cell_width
        bottom = source.height if row == 1 else (row + 1) * cell_height
        crop = (column * cell_width, row * cell_height, right, bottom)
        sprite = trim_alpha(source.crop(crop).convert("RGBA"))
        group = groups.get(name, name)
        if group not in reference_scales:
            reference_scales[group] = min(size[0] / sprite.width, size[1] / sprite.height)
        scale = reference_scales[group]
        sprite = sprite.resize(
            (max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale))),
            Image.Resampling.LANCZOS,
        )
        # Every frame in an animation family must keep an identical runtime
        # canvas. Otherwise smoke/muzzle flashes enlarge the alpha bounds and
        # make the physical building or unit visibly pulse in scale.
        canvas = Image.new("RGBA", size, (0, 0, 0, 0))
        canvas.alpha_composite(sprite, ((size[0] - sprite.width) // 2, size[1] - sprite.height))
        entries.append((name, canvas))


def append_rts_miner(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    names = ["rts_miner_empty_a", "rts_miner_empty_b", "rts_miner_loaded_a", "rts_miner_loaded_b"]
    cell_width = source.width // 4
    target = (24, 30)
    reference_scale: float | None = None
    for column, name in enumerate(names):
        right = source.width if column == 3 else (column + 1) * cell_width
        sprite = trim_alpha(source.crop((column * cell_width, 0, right, source.height)).convert("RGBA"))
        if reference_scale is None:
            reference_scale = min(target[0] / sprite.width, target[1] / sprite.height)
        sprite = sprite.resize(
            (max(1, round(sprite.width * reference_scale)), max(1, round(sprite.height * reference_scale))),
            Image.Resampling.LANCZOS,
        )
        canvas = Image.new("RGBA", target, (0, 0, 0, 0))
        canvas.alpha_composite(sprite, ((target[0] - sprite.width) // 2, target[1] - sprite.height))
        entries.append((name, canvas))


def append_rts_terrain_details(
    entries: list[tuple[str, Image.Image]],
    floor: Image.Image,
    vein: Image.Image,
    landing_pad: Image.Image,
) -> None:
    floor_tile = floor.convert("RGBA").resize((160, 160), Image.Resampling.LANCZOS)
    entries.append(("rts_forest_floor", floor_tile))
    cell_width = vein.width // 4
    for column, name in enumerate(["rts_vein_straight", "rts_vein_curve", "rts_vein_branch", "rts_vein_node"]):
        right = vein.width if column == 3 else (column + 1) * cell_width
        sprite = trim_alpha(vein.crop((column * cell_width, 0, right, vein.height)).convert("RGBA"))
        sprite.thumbnail((28 if column < 3 else 40, 42 if column < 3 else 40), Image.Resampling.LANCZOS)
        entries.append((name, sprite))
    pad = trim_alpha(landing_pad.convert("RGBA"))
    pad.thumbnail((104, 76), Image.Resampling.LANCZOS)
    entries.append(("rts_karl_landing_pad", pad))


def append_rts_frontier_road(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    cell_width = source.width // 2
    for column, name in enumerate(["rts_frontier_road_intact", "rts_frontier_road_churned"]):
        right = source.width if column == 1 else cell_width
        tile = trim_alpha(source.crop((column * cell_width, 0, right, source.height)).convert("RGBA"))
        tile = tile.resize((46, 96), Image.Resampling.LANCZOS)
        entries.append((name, tile))


def append_dungeon_assets(entries: list[tuple[str, Image.Image]], karl: Image.Image, mine: Image.Image) -> None:
    karl_names = ["dungeon_karl_s_idle", "dungeon_karl_s_walk_a", "dungeon_karl_s_walk_b", "dungeon_karl_kneel",
                  "dungeon_karl_n_idle", "dungeon_karl_n_walk", "dungeon_karl_e_idle", "dungeon_karl_e_walk"]
    for index, name in enumerate(karl_names):
        column, row = index % 4, index // 4
        left, top = column * karl.width // 4, row * karl.height // 2
        right, bottom = (column + 1) * karl.width // 4, (row + 1) * karl.height // 2
        sprite = trim_alpha(karl.crop((left, top, right, bottom)).convert("RGBA"))
        sprite.thumbnail((42, 52), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (46, 56), (0, 0, 0, 0))
        canvas.alpha_composite(sprite, ((46 - sprite.width) // 2, 56 - sprite.height))
        entries.append((name, canvas))

    mine_names = ["dungeon_floor_wet", "dungeon_wall_timber", "dungeon_wall_corner", "dungeon_door_dark",
                  "dungeon_rails", "dungeon_chain_lift", "dungeon_supply", "dungeon_descent_pit"]
    targets = [(96, 72), (96, 64), (86, 70), (86, 76), (96, 72), (78, 70), (66, 64), (82, 72)]
    for index, (name, target) in enumerate(zip(mine_names, targets)):
        column, row = index % 4, index // 4
        left, top = column * mine.width // 4, row * mine.height // 2
        right, bottom = (column + 1) * mine.width // 4, (row + 1) * mine.height // 2
        sprite = trim_alpha(mine.crop((left, top, right, bottom)).convert("RGBA"))
        sprite.thumbnail(target, Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_dungeon_loot(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    names = ["loot_rapier", "loot_saber", "loot_axe", "loot_hammer", "loot_spear", "loot_pistol",
             "loot_cuirass", "loot_helmet", "loot_gauntlets", "loot_boots", "loot_ring", "loot_relic"]
    for index, name in enumerate(names):
        column, row = index % 4, index // 4
        left, top = column * source.width // 4, row * source.height // 3
        right, bottom = (column + 1) * source.width // 4, (row + 1) * source.height // 3
        sprite = trim_alpha(source.crop((left, top, right, bottom)).convert("RGBA"))
        sprite.thumbnail((18, 18), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (20, 20), (0, 0, 0, 0))
        canvas.alpha_composite(sprite, ((20 - sprite.width) // 2, (20 - sprite.height) // 2))
        entries.append((name, canvas))


def append_dungeon_ui(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    names = ["ui_health_orb", "ui_power_orb", "ui_inventory_corner", "ui_inventory_divider",
             "ui_item_slot", "ui_backpack_grid", "ui_carolean_silhouette", "ui_stash_crest"]
    targets = [(58, 58), (44, 44), (82, 82), (128, 22), (30, 30), (92, 92), (72, 104), (78, 78)]
    for index, (name, target) in enumerate(zip(names, targets)):
        column, row = index % 4, index // 4
        left, top = column * source.width // 4, row * source.height // 2
        right, bottom = (column + 1) * source.width // 4, (row + 1) * source.height // 2
        sprite = trim_alpha(source.crop((left, top, right, bottom)).convert("RGBA"))
        sprite.thumbnail(target, Image.Resampling.LANCZOS)
        entries.append((name, sprite))


def append_dungeon_combat(entries: list[tuple[str, Image.Image]], karl: Image.Image, enemies: Image.Image,
                          karl_north: Image.Image) -> None:
    karl_names = ["karl_slash_windup", "karl_slash_contact", "karl_slash_follow", "karl_parry",
                  "karl_slash_e_windup", "karl_slash_e_contact", "karl_hammer_impact", "karl_hit"]
    enemy_names = ["dk_stormer_idle", "dk_stormer_walk", "dk_stormer_attack", "dk_stormer_hit",
                   "dk_pikeman_idle", "dk_pikeman_walk", "dk_pikeman_attack", "dk_pikeman_hit"]
    for source, names, target in [(karl, karl_names, (64, 64)), (enemies, enemy_names, (58, 58))]:
        for index, name in enumerate(names):
            column, row = index % 4, index // 4
            left, top = column * source.width // 4, row * source.height // 2
            right, bottom = (column + 1) * source.width // 4, (row + 1) * source.height // 2
            # Scale the fixed source cell, not its alpha bounds. Weapons and
            # slash arcs otherwise make individual poses shrink unpredictably.
            sprite = source.crop((left, top, right, bottom)).convert("RGBA")
            scale = target[1] / sprite.height
            sprite = sprite.resize((max(1, round(sprite.width * scale)), target[1]), Image.Resampling.LANCZOS)
            canvas = Image.new("RGBA", target, (0, 0, 0, 0))
            canvas.alpha_composite(sprite, ((target[0] - sprite.width) // 2, target[1] - sprite.height))
            entries.append((name, canvas))
    north_names = ["karl_slash_n_windup", "karl_slash_n_contact", "karl_slash_n_follow", "karl_parry_n"]
    north = karl_north.crop((0, 115, karl_north.width, 715))
    for index, name in enumerate(north_names):
        left = index * north.width // 4
        right = (index + 1) * north.width // 4
        sprite = north.crop((left, 0, right, north.height)).convert("RGBA")
        scale = 64 / sprite.height
        sprite = sprite.resize((max(1, round(sprite.width * scale)), 64), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
        canvas.alpha_composite(sprite, ((64 - sprite.width) // 2, 0))
        entries.append((name, canvas))


def append_dungeon_gruva2(entries: list[tuple[str, Image.Image]], loot: Image.Image, door: Image.Image) -> None:
    for source, names, target in [
        (loot, ["dungeon_chest_closed", "dungeon_chest_open", "dungeon_silver_vent", "dungeon_silver_mist"], (64, 54)),
        (door, ["dungeon_wood_door", "dungeon_wood_door_damaged", "dungeon_wood_door_broken"], (84, 74)),
    ]:
        columns = len(names)
        for index, name in enumerate(names):
            left = index * source.width // columns
            right = (index + 1) * source.width // columns
            sprite = trim_alpha(source.crop((left, 0, right, source.height)).convert("RGBA"))
            sprite.thumbnail(target, Image.Resampling.LANCZOS)
            canvas = Image.new("RGBA", target, (0, 0, 0, 0))
            canvas.alpha_composite(sprite, ((target[0] - sprite.width) // 2, target[1] - sprite.height))
            entries.append((name, canvas))


def append_dungeon_silver_fogde(entries: list[tuple[str, Image.Image]], source: Image.Image) -> None:
    names = ["dk_silver_fogde_idle", "dk_silver_fogde_walk", "dk_silver_fogde_attack", "dk_silver_fogde_hit"]
    for index, name in enumerate(names):
        left = index * source.width // 4
        right = (index + 1) * source.width // 4
        sprite = trim_alpha(source.crop((left, 0, right, source.height)).convert("RGBA"))
        sprite.thumbnail((78, 72), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (82, 76), (0, 0, 0, 0))
        canvas.alpha_composite(sprite, ((82 - sprite.width) // 2, 76 - sprite.height))
        entries.append((name, canvas))


def mirrored_background(source: Image.Image, width: int, height: int) -> Image.Image:
    plate = source.copy()
    plate.thumbnail((width, height), Image.Resampling.LANCZOS)
    seamless = Image.new("RGBA", (plate.width, plate.height * 2))
    seamless.paste(plate, (0, 0))
    seamless.paste(ImageOps.flip(plate), (0, plate.height))
    return seamless


def build(
    input_path: Path,
    danish_input_path: Path,
    portrait_input_path: Path,
    snapphane_portrait_input_path: Path,
    soren_corsair_input_path: Path,
    player_input_path: Path,
    environment_input_path: Path,
    combat_detail_input_path: Path,
    background_input_path: Path,
    skanska_background_input_path: Path,
    skanska_props_input_path: Path,
    glimminge_input_path: Path,
    skanska_enemies_input_path: Path,
    glimminge_animations_input_path: Path,
    birgitte_portrait_input_path: Path,
    skanska_signal_beacon_input_path: Path,
    glimminge_iron_raven_input_path: Path,
    skanska_projectiles_input_path: Path,
    skanska_combat_details_input_path: Path,
    rts_buildings_input_path: Path,
    rts_units_input_path: Path,
    rts_danish_input_path: Path,
    rts_miner_input_path: Path,
    rts_toldhus_input_path: Path,
    rts_forest_input_path: Path,
    rts_frontier_input_path: Path,
    rts_floor_input_path: Path,
    rts_vein_input_path: Path,
    rts_landing_pad_input_path: Path,
    rts_road_input_path: Path,
    dungeon_karl_input_path: Path,
    dungeon_mine_input_path: Path,
    dungeon_loot_input_path: Path,
    dungeon_ui_input_path: Path,
    dungeon_karl_combat_input_path: Path,
    dungeon_enemies_input_path: Path,
    dungeon_karl_north_input_path: Path,
    dungeon_gruva2_loot_input_path: Path,
    dungeon_door_input_path: Path,
    dungeon_silver_fogde_input_path: Path,
    logo_input_path: Path,
    output_path: Path,
) -> None:
    source = Image.open(input_path)
    danish_source = Image.open(danish_input_path)
    portrait_source = Image.open(portrait_input_path)
    snapphane_portrait_source = Image.open(snapphane_portrait_input_path)
    soren_corsair_source = Image.open(soren_corsair_input_path)
    player_source = Image.open(player_input_path)
    environment_source = Image.open(environment_input_path)
    combat_detail_source = Image.open(combat_detail_input_path)
    background_source = Image.open(background_input_path).convert("RGBA")
    skanska_background_source = Image.open(skanska_background_input_path).convert("RGBA")
    skanska_props_source = Image.open(skanska_props_input_path).convert("RGBA")
    glimminge_source = Image.open(glimminge_input_path).convert("RGBA")
    skanska_enemies_source = Image.open(skanska_enemies_input_path).convert("RGBA")
    glimminge_animations_source = Image.open(glimminge_animations_input_path).convert("RGBA")
    birgitte_portrait_source = Image.open(birgitte_portrait_input_path).convert("RGBA")
    skanska_signal_beacon_source = Image.open(skanska_signal_beacon_input_path).convert("RGBA")
    glimminge_iron_raven_source = Image.open(glimminge_iron_raven_input_path).convert("RGBA")
    skanska_projectiles_source = Image.open(skanska_projectiles_input_path).convert("RGBA")
    skanska_combat_details_source = Image.open(skanska_combat_details_input_path).convert("RGBA")
    rts_buildings_source = Image.open(rts_buildings_input_path).convert("RGBA")
    rts_units_source = Image.open(rts_units_input_path).convert("RGBA")
    rts_danish_source = Image.open(rts_danish_input_path).convert("RGBA")
    rts_miner_source = Image.open(rts_miner_input_path).convert("RGBA")
    rts_toldhus_source = Image.open(rts_toldhus_input_path).convert("RGBA")
    rts_forest_source = Image.open(rts_forest_input_path).convert("RGBA")
    rts_frontier_source = Image.open(rts_frontier_input_path).convert("RGBA")
    rts_floor_source = Image.open(rts_floor_input_path).convert("RGBA")
    rts_vein_source = Image.open(rts_vein_input_path).convert("RGBA")
    rts_landing_pad_source = Image.open(rts_landing_pad_input_path).convert("RGBA")
    rts_road_source = Image.open(rts_road_input_path).convert("RGBA")
    dungeon_karl_source = Image.open(dungeon_karl_input_path).convert("RGBA")
    dungeon_mine_source = Image.open(dungeon_mine_input_path).convert("RGBA")
    dungeon_loot_source = Image.open(dungeon_loot_input_path).convert("RGBA")
    dungeon_ui_source = Image.open(dungeon_ui_input_path).convert("RGBA")
    dungeon_karl_combat_source = Image.open(dungeon_karl_combat_input_path).convert("RGBA")
    dungeon_enemies_source = Image.open(dungeon_enemies_input_path).convert("RGBA")
    dungeon_karl_north_source = Image.open(dungeon_karl_north_input_path).convert("RGBA")
    dungeon_gruva2_loot_source = Image.open(dungeon_gruva2_loot_input_path).convert("RGBA")
    dungeon_door_source = Image.open(dungeon_door_input_path).convert("RGBA")
    dungeon_silver_fogde_source = Image.open(dungeon_silver_fogde_input_path).convert("RGBA")
    logo_source = trim_alpha(Image.open(logo_input_path).convert("RGBA"))
    entries: list[tuple[str, Image.Image]] = []
    append_sprites(entries, source, PRIMARY_SPRITES)
    append_sprites(entries, danish_source, DANISH_SPRITES)
    append_sprites(entries, portrait_source, RADIO_PORTRAITS)
    append_two_frame_portrait(entries, snapphane_portrait_source)
    append_three_frame_corsair(entries, soren_corsair_source)
    append_skanska_props(entries, skanska_props_source)
    append_glimminge(entries, glimminge_source)
    append_skanska_enemies(entries, skanska_enemies_source)
    append_glimminge_animations(entries, glimminge_animations_source)
    append_birgitte_portrait(entries, birgitte_portrait_source)
    append_skanska_signal_beacon(entries, skanska_signal_beacon_source)
    append_glimminge_iron_raven(entries, glimminge_iron_raven_source)
    append_skanska_projectiles(entries, skanska_projectiles_source)
    append_skanska_combat_details(entries, skanska_combat_details_source)
    append_rts_sheet(entries, rts_buildings_source, 4, [
        ("rts_steam_idle", 0, 0, (46, 46)),
        ("rts_steam_work", 1, 0, (46, 46)),
        ("rts_crusher_idle", 2, 0, (48, 40)),
        ("rts_crusher_work", 3, 0, (48, 40)),
        ("rts_barracks", 0, 1, (48, 42)),
        ("rts_animal_hall", 1, 1, (54, 44)),
        ("rts_tower_idle", 2, 1, (44, 42)),
        ("rts_tower_fire", 3, 1, (44, 42)),
    ])
    append_rts_sheet(entries, rts_units_source, 4, [
        ("rts_carolean_ready", 0, 0, (42, 30)),
        ("rts_carolean_fire", 1, 0, (42, 30)),
        ("rts_moose_ready", 2, 0, (48, 34)),
        ("rts_moose_charge", 3, 0, (48, 34)),
        ("rts_carolean_reload", 0, 1, (42, 30)),
        ("rts_carolean_hit", 1, 1, (42, 30)),
        ("rts_moose_fire", 2, 1, (48, 34)),
        ("rts_moose_hit", 3, 1, (48, 34)),
    ])
    append_rts_sheet(entries, rts_danish_source, 5, [
        ("rts_toll_ready", 0, 0, (32, 28)),
        ("rts_toll_attack", 0, 1, (32, 28)),
        ("rts_pike_ready", 1, 0, (38, 30)),
        ("rts_pike_attack", 1, 1, (38, 30)),
        ("rts_mastiff_ready", 2, 0, (32, 24)),
        ("rts_mastiff_attack", 2, 1, (32, 24)),
        ("rts_boar_ready", 3, 0, (38, 26)),
        ("rts_boar_fuse", 3, 1, (38, 26)),
        ("rts_organ_ready", 4, 0, (52, 34)),
        ("rts_organ_fire", 4, 1, (52, 34)),
    ])
    append_rts_miner(entries, rts_miner_source)
    append_rts_sheet(entries, rts_toldhus_source, 3, [
        ("rts_toldhus_intact", 0, 0, (148, 104)),
        ("rts_toldhus_open", 1, 0, (148, 104)),
        ("rts_toldhus_damaged", 2, 0, (148, 104)),
        ("rts_seal_intact", 0, 1, (32, 52)),
        ("rts_seal_broken", 1, 1, (32, 52)),
        ("rts_toldhus_wreck", 2, 1, (148, 72)),
    ])
    append_rts_sheet(entries, rts_forest_source, 4, [
        ("rts_spruce_tall", 0, 0, (34, 54)),
        ("rts_spruce_bent", 1, 0, (42, 48)),
        ("rts_pine_dead", 2, 0, (32, 52)),
        ("rts_spruce_crystal", 3, 0, (38, 54)),
        ("rts_moss_boulders", 0, 1, (36, 28)),
        ("rts_silver_outcrop", 1, 1, (36, 32)),
        ("rts_forest_shrub", 2, 1, (34, 25)),
        ("rts_forest_stump", 3, 1, (28, 24)),
    ])
    append_rts_sheet(entries, rts_frontier_source, 4, [
        ("rts_dk_barricade", 0, 0, (40, 28)),
        ("rts_dk_lantern", 1, 0, (24, 34)),
        ("rts_dk_tripod", 2, 0, (28, 38)),
        ("rts_dk_signpost", 3, 0, (24, 38)),
        ("rts_dk_crates", 0, 1, (34, 30)),
        ("rts_dk_minecart", 1, 1, (38, 28)),
        ("rts_dk_scorched", 2, 1, (34, 28)),
        ("rts_dk_wagon_rut", 3, 1, (42, 28)),
    ])
    append_rts_terrain_details(entries, rts_floor_source, rts_vein_source, rts_landing_pad_source)
    append_rts_frontier_road(entries, rts_road_source)
    append_dungeon_assets(entries, dungeon_karl_source, dungeon_mine_source)
    append_dungeon_loot(entries, dungeon_loot_source)
    append_dungeon_ui(entries, dungeon_ui_source)
    append_dungeon_combat(entries, dungeon_karl_combat_source, dungeon_enemies_source, dungeon_karl_north_source)
    append_dungeon_gruva2(entries, dungeon_gruva2_loot_source, dungeon_door_source)
    append_dungeon_silver_fogde(entries, dungeon_silver_fogde_source)
    append_sprites(entries, player_source, PLAYER_SPRITES)
    append_sprites(entries, environment_source, ENVIRONMENT_SPRITES)
    append_sprites(entries, combat_detail_source, COMBAT_DETAIL_SPRITES)
    entries.append(("stora_balt_background", mirrored_background(background_source, 320, 700)))
    entries.append(("stora_balt_background_wide", mirrored_background(background_source, 400, 875)))
    entries.append(("skanska_background", mirrored_background(skanska_background_source, 320, 700)))
    entries.append(("skanska_background_wide", mirrored_background(skanska_background_source, 400, 875)))
    logo_wide = logo_source.copy()
    logo_wide.thumbnail((210, 105), Image.Resampling.LANCZOS)
    entries.append(("stormakt_logo_wide", logo_wide))
    logo_legacy = logo_source.copy()
    logo_legacy.thumbnail((150, 75), Image.Resampling.LANCZOS)
    entries.append(("stormakt_logo_legacy", logo_legacy))

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
    parser.add_argument(
        "--snapphane-portrait-input",
        type=Path,
        default=Path("assets/stormakt3020/soren-svartkrut-radio-v1.png"),
    )
    parser.add_argument(
        "--soren-corsair-input",
        type=Path,
        default=Path("assets/stormakt3020/soren-corsair-v1.png"),
    )
    parser.add_argument(
        "--environment-input",
        type=Path,
        default=Path("assets/stormakt3020/stormakt-stora-balt-environment-v1.png"),
    )
    parser.add_argument(
        "--player-input",
        type=Path,
        default=Path("assets/stormakt3020/karl-cclv-dark-frigate-v1.png"),
    )
    parser.add_argument(
        "--background-input",
        type=Path,
        default=Path("assets/stormakt3020/stormakt-stora-balt-background-v1.png"),
    )
    parser.add_argument(
        "--skanska-background-input",
        type=Path,
        default=Path("assets/stormakt3020/stormakt-skanska-background-v1.png"),
    )
    parser.add_argument(
        "--skanska-props-input",
        type=Path,
        default=Path("assets/stormakt3020/stormakt-skanska-props-v1.png"),
    )
    parser.add_argument(
        "--glimminge-input",
        type=Path,
        default=Path("assets/stormakt3020/glimminge-jarn-v1.png"),
    )
    parser.add_argument(
        "--skanska-enemies-input",
        type=Path,
        default=Path("assets/stormakt3020/skanska-enemies-v1.png"),
    )
    parser.add_argument(
        "--glimminge-animations-input",
        type=Path,
        default=Path("assets/stormakt3020/glimminge-animations-v1.png"),
    )
    parser.add_argument(
        "--birgitte-portrait-input",
        type=Path,
        default=Path("assets/stormakt3020/birgitte-bille-radio-v1.png"),
    )
    parser.add_argument(
        "--skanska-signal-beacon-input",
        type=Path,
        default=Path("assets/stormakt3020/skanska-signal-beacon-v1.png"),
    )
    parser.add_argument(
        "--glimminge-iron-raven-input",
        type=Path,
        default=Path("assets/stormakt3020/glimminge-iron-raven-v1.png"),
    )
    parser.add_argument(
        "--skanska-projectiles-input",
        type=Path,
        default=Path("assets/stormakt3020/skanska-projectiles-v1.png"),
    )
    parser.add_argument(
        "--skanska-combat-details-input",
        type=Path,
        default=Path("assets/stormakt3020/skanska-combat-details-v1.png"),
    )
    parser.add_argument(
        "--combat-detail-input",
        type=Path,
        default=Path("assets/stormakt3020/stormakt-bridge-cannons-projectiles-v1.png"),
    )
    parser.add_argument("--rts-buildings-input", type=Path, default=Path("assets/stormakt3020/rts-swedish-buildings-v1.png"))
    parser.add_argument("--rts-units-input", type=Path, default=Path("assets/stormakt3020/rts-swedish-units-v1.png"))
    parser.add_argument("--rts-danish-input", type=Path, default=Path("assets/stormakt3020/rts-danish-army-v1.png"))
    parser.add_argument("--rts-miner-input", type=Path, default=Path("assets/stormakt3020/rts-silver-miner-v1.png"))
    parser.add_argument("--rts-toldhus-input", type=Path, default=Path("assets/stormakt3020/rts-toldhus-v1.png"))
    parser.add_argument("--rts-forest-input", type=Path, default=Path("assets/stormakt3020/rts-forest-props-v1.png"))
    parser.add_argument("--rts-frontier-input", type=Path, default=Path("assets/stormakt3020/rts-danish-frontier-props-v1.png"))
    parser.add_argument("--rts-floor-input", type=Path, default=Path("assets/stormakt3020/rts-forest-floor-v1.png"))
    parser.add_argument("--rts-vein-input", type=Path, default=Path("assets/stormakt3020/rts-silver-vein-v1.png"))
    parser.add_argument("--rts-landing-pad-input", type=Path, default=Path("assets/stormakt3020/rts-karl-landing-pad-v1.png"))
    parser.add_argument("--rts-road-input", type=Path, default=Path("assets/stormakt3020/rts-danish-frontier-road-v1.png"))
    parser.add_argument("--dungeon-karl-input", type=Path, default=Path("assets/stormakt3020/dungeon-karl-v1.png"))
    parser.add_argument("--dungeon-mine-input", type=Path, default=Path("assets/stormakt3020/dungeon-gruva1-environment-v1.png"))
    parser.add_argument("--dungeon-loot-input", type=Path, default=Path("assets/stormakt3020/dungeon-loot-v1.png"))
    parser.add_argument("--dungeon-ui-input", type=Path, default=Path("assets/stormakt3020/dungeon-ui-chrome-v1.png"))
    parser.add_argument("--dungeon-karl-combat-input", type=Path, default=Path("assets/stormakt3020/dungeon-karl-combat-v1.png"))
    parser.add_argument("--dungeon-enemies-input", type=Path, default=Path("assets/stormakt3020/dungeon-danish-enemies-v1.png"))
    parser.add_argument("--dungeon-karl-north-input", type=Path, default=Path("assets/stormakt3020/dungeon-karl-north-combat-v1.png"))
    parser.add_argument("--dungeon-gruva2-loot-input", type=Path, default=Path("assets/stormakt3020/dungeon-gruva2-loot-v1.png"))
    parser.add_argument("--dungeon-door-input", type=Path, default=Path("assets/stormakt3020/dungeon-breakable-door-v1.png"))
    parser.add_argument("--dungeon-silver-fogde-input", type=Path, default=Path("assets/stormakt3020/dungeon-silver-fogde-v1.png"))
    parser.add_argument(
        "--logo-input",
        type=Path,
        default=Path("assets/stormakt3020/stormakt3020-logo-v1.png"),
    )
    parser.add_argument("--output", type=Path, default=Path("assets/stormakt3020/stormakt3020.wfsa"))
    args = parser.parse_args()
    build(
        args.input,
        args.danish_input,
        args.portrait_input,
        args.snapphane_portrait_input,
        args.soren_corsair_input,
        args.player_input,
        args.environment_input,
        args.combat_detail_input,
        args.background_input,
        args.skanska_background_input,
        args.skanska_props_input,
        args.glimminge_input,
        args.skanska_enemies_input,
        args.glimminge_animations_input,
        args.birgitte_portrait_input,
        args.skanska_signal_beacon_input,
        args.glimminge_iron_raven_input,
        args.skanska_projectiles_input,
        args.skanska_combat_details_input,
        args.rts_buildings_input,
        args.rts_units_input,
        args.rts_danish_input,
        args.rts_miner_input,
        args.rts_toldhus_input,
        args.rts_forest_input,
        args.rts_frontier_input,
        args.rts_floor_input,
        args.rts_vein_input,
        args.rts_landing_pad_input,
        args.rts_road_input,
        args.dungeon_karl_input,
        args.dungeon_mine_input,
        args.dungeon_loot_input,
        args.dungeon_ui_input,
        args.dungeon_karl_combat_input,
        args.dungeon_enemies_input,
        args.dungeon_karl_north_input,
        args.dungeon_gruva2_loot_input,
        args.dungeon_door_input,
        args.dungeon_silver_fogde_input,
        args.logo_input,
        args.output,
    )


if __name__ == "__main__":
    main()
