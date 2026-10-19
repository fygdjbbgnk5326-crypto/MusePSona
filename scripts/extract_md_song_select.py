# -*- coding: utf-8 -*-
"""导出官机选曲页静态物件，供 Unity 选曲页本机加载。

只读已安装 Muse Dash 的选曲页、导航栏与通用封面 bundle，输出目录位于
``StreamingAssets/MdLooks/sprites/common``，受 MdLooks/.gitignore 保护，
不会把官机素材提交到仓库。缺少输出时游玩端会回退到程序化外观。
"""

import argparse
import glob
import io
import json
import os

import UnityPy
from PIL import Image, ImageDraw, ImageFont

import _paths


ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_OUT = os.path.join(
    ROOT, "PlayClient", "Unity", "Assets", "StreamingAssets", "MdLooks",
    "sprites", "common",
)
# 输出键 -> pnlstage Sprite 名。选 Sprite 而非 Texture2D，保留官机的裁切区域。
SONG_SELECT_SPRITES = {
    "ui_song_disc": "disc",
    "ui_song_ray": "RayFx",
    "ui_stage_info": "StageInfoBase",
    "ui_album_title": "PackInfoBase",
    "ui_difficulty_button": "DifficultyButton",
    "ui_difficulty_easy": "IconEasy",
    "ui_difficulty_hard": "IconHard",
    "ui_difficulty_master": "IconMaster",
    "ui_difficulty_hidden": "IconHide",
    "ui_difficulty_empty": "IconEmpty",
    "ui_difficulty_base": "StageAchievement",
    "ui_album_shadow": "AlbumCoverShadow",
    "ui_album_switch": "BtnSwitchAlbum",
    "ui_store_button": "StoreButton",
    "ui_all_music": "IconAllMusic",
    "ui_star": "Star",
    "ui_chapter_button": "ChapterButton",
    "ui_chapter_base": "ChapterBase",
    "ui_search_icon": "Magnifier",
    "ui_difficulty_title": "DifficultyTagTittle",
    "ui_like_button": "BtnLikeOff",
    "ui_achievement_button": "StageAchievement",
    "ui_achievement_icon": "StageAchievementIcon",
    "ui_stage_info_middle": "StageInfoBase2",
    "ui_stage_info_back": "StageInfoBase3",
    "ui_stage_info_bottom": "StageInfoBase",
    "ui_stage_info_bottom_reverse": "StageInfoBaseReverse",
    "ui_copy_button": "icon_copy",
}

NAVIGATION_SPRITES = {
    "ui_top_base": "PcSprTopBase",
    "ui_option_button": "PcSprButton",
    "ui_level_star_bg": "PcSprStarGift",
    "ui_level_slider": "NsSprStarSliderBg",
}

UI_SYSTEM_COMMON_SPRITES = {
    "ui_option_icon": "IconOption",
}

SHARE_IMAGE_SPRITES = {
    "ui_back_button": "PcBackButton",
    "ui_key_q": "PcButtonQ",
    "ui_key_e": "PcButtonE",
}

COVER_COMMON_SPRITES = {
    "ui_random_song_cover": "random_song_cover",
}

def find_bundle(bundle_dir, pattern):
    matches = sorted(glob.glob(os.path.join(bundle_dir, pattern)))
    if not matches:
        raise FileNotFoundError("未找到 %s：%s" % (pattern, bundle_dir))
    return matches[0]


def read_manifest(path):
    if not os.path.isfile(path):
        return {}
    try:
        with open(path, "r", encoding="utf-8") as source:
            data = json.load(source)
        return data if isinstance(data, dict) else {}
    except (OSError, ValueError):
        return {}


def read_sprites(bundle, aliases):
    env = UnityPy.load(bundle)
    wanted = set(aliases.values())
    sprites = {}
    for obj in env.objects:
        if obj.type.name != "Sprite":
            continue
        try:
            sprite = obj.read()
        except Exception:
            continue
        if sprite.m_Name in wanted and sprite.m_Name not in sprites:
            sprites[sprite.m_Name] = sprite
    return sprites


def read_normal_font_data(bundle_dir):
    bundle = find_bundle(bundle_dir, "fonts_assets_all_*.bundle")
    env = UnityPy.load(bundle)
    for asset_path, obj in env.container.items():
        if (obj.type.name != "Font"
                or asset_path.replace("\\", "/").lower()
                != "assets/static resources/fonts/normal.ttf"):
            continue
        font = obj.read()
        data = bytes(getattr(font, "m_FontData", b""))
        if data:
            return data
    raise FileNotFoundError("官机字体包中未找到 Fonts/Normal.ttf")


def extract_difficulty_glyphs(bundle_dir, out_dir):
    """把官机 Teko Bold 数字预渲染进固定 80×80 文本槽。

    官机 TxtValue 使用 80×80 / MiddleCenter。固定画布让数字与星形共用稳定的
    中心锚点，避免紧边界纹理在非整数窗口缩放时因宽度不同产生半像素漂移。
    """
    supersample = 4
    slot_size = 80
    font = ImageFont.truetype(
        io.BytesIO(read_normal_font_data(bundle_dir)), 24 * supersample)
    manifest_path = os.path.join(out_dir, "manifest.json")
    manifest = read_manifest(manifest_path)
    values = [str(value) for value in range(1, 21)] + ["?"]
    for value in values:
        scratch = Image.new("L", (slot_size * supersample,) * 2, 0)
        draw = ImageDraw.Draw(scratch)
        bbox = draw.textbbox((0, 0), value, font=font)
        draw.text((16 - bbox[0], 16 - bbox[1]), value, font=font, fill=255)
        ink_box = scratch.getbbox()
        ink = scratch.crop(ink_box) if ink_box else Image.new("L", (1, 1), 0)

        # 以实际墨迹重心为准放进固定槽；4 倍采样后再缩小，保留亚像素精度。
        pixels = ink.load()
        weight = sum(pixels[x, y] for y in range(ink.height) for x in range(ink.width))
        if weight:
            center_x = sum(
                (x + 0.5) * pixels[x, y]
                for y in range(ink.height) for x in range(ink.width)) / weight
            center_y = sum(
                (y + 0.5) * pixels[x, y]
                for y in range(ink.height) for x in range(ink.width)) / weight
        else:
            center_x, center_y = ink.width * 0.5, ink.height * 0.5

        large_size = slot_size * supersample
        alpha = Image.new("L", (large_size, large_size), 0)
        alpha.paste(ink, (
            round(large_size * 0.5 - center_x),
            round(large_size * 0.5 - center_y),
        ))
        alpha = alpha.resize((slot_size, slot_size), Image.Resampling.LANCZOS)
        image = Image.new("RGBA", (slot_size, slot_size), (255, 255, 255, 0))
        image.putalpha(alpha)
        suffix = "unknown" if value == "?" else value
        key = "ui_difficulty_value_" + suffix
        filename = key + ".png"
        image.save(os.path.join(out_dir, filename))
        manifest[key] = {
            "file": filename,
            "w": slot_size,
            "h": slot_size,
            "src": "fonts/Normal.ttf:" + value,
        }
    with open(manifest_path, "w", encoding="utf-8") as target:
        json.dump(manifest, target, ensure_ascii=False, indent=2)
        target.write("\n")
    return len(values)


def extract(bundle_dir, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    groups = (
        ("pnlstage", SONG_SELECT_SPRITES,
         find_bundle(bundle_dir, "pnlstage_assets_all_*.bundle")),
        ("pnlhome&navigation", NAVIGATION_SPRITES,
         find_bundle(bundle_dir, "pnlhome&navigation_assets_all_*.bundle")),
        ("uisystemcommon", UI_SYSTEM_COMMON_SPRITES,
         find_bundle(bundle_dir, "uisystemcommon_assets_all_*.bundle")),
        ("shareimage", SHARE_IMAGE_SPRITES,
         find_bundle(bundle_dir, "shareimage_assets_all_*.bundle")),
        ("covercommon", COVER_COMMON_SPRITES,
         find_bundle(bundle_dir, "covercommon_assets_all_*.bundle")),
    )

    manifest_path = os.path.join(out_dir, "manifest.json")
    manifest = read_manifest(manifest_path)
    exported = []
    missing = []
    for source_group, aliases, bundle in groups:
        sprites = read_sprites(bundle, aliases)
        for key, source_name in aliases.items():
            sprite = sprites.get(source_name)
            if sprite is None:
                missing.append(source_name)
                continue
            image = sprite.image.convert("RGBA")
            filename = key + ".png"
            image.save(os.path.join(out_dir, filename))
            manifest[key] = {
                "file": filename,
                "w": image.width,
                "h": image.height,
                "src": source_group + "/" + source_name,
            }
            exported.append(key)

    with open(manifest_path, "w", encoding="utf-8") as target:
        json.dump(manifest, target, ensure_ascii=False, indent=2)
        target.write("\n")
    return exported, missing


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-dir", default=_paths.bundle_dir(), help="官机 bundle 目录")
    parser.add_argument("--out", default=DEFAULT_OUT, help="输出目录")
    args = parser.parse_args()
    exported, missing = extract(args.bundle_dir, args.out)
    glyph_count = extract_difficulty_glyphs(args.bundle_dir, args.out)
    print("官机选曲物件：%d 张 → %s" % (len(exported), args.out))
    print("官机难度数字：%d 张 → %s" % (glyph_count, args.out))
    if missing:
        print("未找到：" + ", ".join(missing))


if __name__ == "__main__":
    main()
