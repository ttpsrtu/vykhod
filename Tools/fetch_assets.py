#!/usr/bin/env python
"""
fetch_assets.py - download + extract free low-poly packs (CC0 / CC-BY) for Vykhod.

Usage (run from anywhere, with -I so nothing from the download dirs is importable):
    python -I Tools/fetch_assets.py                 # fetch everything that is missing
    python -I Tools/fetch_assets.py --only kenney_weapon-pack sfx_guns
    python -I Tools/fetch_assets.py --force         # re-download + re-extract
    python -I Tools/fetch_assets.py --index-only    # only rebuild asset_index.json

Output: Assets/Resources/Imported/<packname>/...   + LICENSE.txt per pack
        Assets/Resources/Imported/asset_index.json

Downloads land in <scratch>/downloads/<packname>/ (each pack in its own dir).
Only FBX/OBJ(+MTL)/PNG/JPG/WAV/OGG/MP3/TTF/OTF/HDR files are kept; GLB/GLTF/blend/
previews/sample scenes/unitypackages are dropped.  When a pack ships both FBX
and OBJ versions of the same models, only the FBX set is kept (OBJ is dropped)
to keep the Unity import light.

Packs may override the filter with keep_ext={...} (e.g. UI/icon packs keep only
PNG/TTF/OTF/WAV/OGG) and junk_dirs={...} (extra directory names to drop).
"multi" packs that are already present are updated additively: file items whose
target is missing are fetched and appended to LICENSE.txt (no --force needed).
"pbr" packs (textures_cc0) pull 1K-JPG materials from ambientCG (Poly Haven as per-asset
fallback) and keep only <id>/<id>_Color.jpg + <id>_Normal.jpg; they update additively too.
"polyhaven" packs (polyhaven_models) pull realistic CC0 props from Poly Haven: the 1k FBX
(renamed <id>/<id>.fbx) plus its included JPG/PNG textures under <id>/textures/ (EXR
displacement maps dropped). Models without an FBX or above max_mb are skipped; additive too.
"""
import argparse
import datetime
import json
import os
import re
import shutil
import sys
import time
import zipfile
from pathlib import Path

import requests

PROJECT = Path(r"C:\Users\я\Projects\Vykhod")
RESOURCES = PROJECT / "Assets" / "Resources"
IMPORTED = RESOURCES / "Imported"
SCRATCH = Path(r"C:\Users\E0F1~1\AppData\Local\Temp\claude\C--Users--\376933f7-10c0-49de-a7f3-31faf5768a85\scratchpad")
DOWNLOADS = SCRATCH / "downloads"

UA = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36"}

KEEP_EXT = {".fbx", ".obj", ".mtl", ".png", ".jpg", ".jpeg", ".wav", ".ogg", ".mp3", ".ttf", ".otf", ".hdr"}
MODEL_EXT = {".fbx", ".obj"}
AUDIO_EXT = {".wav", ".ogg", ".mp3"}
TEX_EXT = {".png", ".jpg", ".jpeg", ".hdr"}
FONT_EXT = {".ttf", ".otf"}
# filter for 2D UI / icon packs: PNG folders + fonts + UI sounds only (vectors, spritesheets, previews dropped)
UI_KEEP_EXT = {".png", ".ttf", ".otf", ".wav", ".ogg"}
# directory names (case-insensitive) that are junk for the game
JUNK_DIRS = {"preview", "previews", "isometric", "sample", "samples", "extras", "unity", "unitypackage",
             "gltf", "gltf format", "glb", "glb format", "blend", "blend format", "sketchfab", "__macosx",
             "renders", "render", "screenshots", "icons", "spritesheet"}
JUNK_NAMES = re.compile(r"^(preview|sample|screenshot|thumbnail|isometric)", re.I)

# ----------------------------------------------------------------------------------------------
# Pack catalogue
# ----------------------------------------------------------------------------------------------
# kind: "kenney"  -> slug; download URL parsed from https://kenney.nl/assets/<slug>
#       "zip"     -> direct zip URL
#       "files"   -> list of (url, relative_target) single files (no zip)
PACKS = [
    # ---- weapons (ESSENTIAL) ----
    dict(name="kenney_weapon-pack", kind="zip", license="CC0 1.0",
         url="https://opengameart.org/sites/default/files/weaponPack_assets.zip",
         source="https://opengameart.org/content/weapon-pack (Kenney 'Weapon Pack', retired from kenney.nl; OGA mirror, CC0)",
         note="30 real-ish low-poly weapons (pistols, rifles, shotgun, smg, knife, grenades...) as OBJ+MTL. Kenney removed this pack from kenney.nl; OGA listing page is access-restricted but the file is served."),
    dict(name="quaternius_guns", kind="zip", license="CC0 1.0",
         url="https://opengameart.org/sites/default/files/ultimate_gun_pack_by_quaternius.zip",
         source="https://opengameart.org/content/low-poly-guns-pack (Quaternius 'Ultimate Gun Pack', CC0)",
         note="40 low-poly gun models, FBX/OBJ."),
    dict(name="kenney_blaster-kit", kind="kenney", slug="blaster-kit", license="CC0 1.0", optional=True),
    # ---- city / environment ----
    dict(name="kenney_city-kit-commercial", kind="kenney", slug="city-kit-commercial", license="CC0 1.0"),
    dict(name="kenney_city-kit-suburban", kind="kenney", slug="city-kit-suburban", license="CC0 1.0"),
    dict(name="kenney_city-kit-industrial", kind="kenney", slug="city-kit-industrial", license="CC0 1.0"),
    dict(name="kenney_city-kit-roads", kind="kenney", slug="city-kit-roads", license="CC0 1.0"),
    dict(name="kenney_survival-kit", kind="kenney", slug="survival-kit", license="CC0 1.0"),
    dict(name="kenney_furniture-kit", kind="kenney", slug="furniture-kit", license="CC0 1.0"),
    dict(name="kenney_nature-kit", kind="kenney", slug="nature-kit", license="CC0 1.0"),
    dict(name="kenney_prototype-kit", kind="kenney", slug="prototype-kit", license="CC0 1.0"),
    dict(name="kenney_modular-buildings", kind="kenney", slug="modular-buildings", license="CC0 1.0"),
    dict(name="kenney_building-kit", kind="kenney", slug="building-kit", license="CC0 1.0"),
    dict(name="kenney_car-kit", kind="kenney", slug="car-kit", license="CC0 1.0"),
    dict(name="kenney_food-kit", kind="kenney", slug="food-kit", license="CC0 1.0", optional=True),
    # ---- characters ----
    dict(name="kenney_mini-characters", kind="kenney", slug="mini-characters", license="CC0 1.0"),
    dict(name="kenney_blocky-characters", kind="kenney", slug="blocky-characters", license="CC0 1.0"),
    dict(name="quaternius_characters", kind="zip", license="CC0 1.0",
         url="https://opengameart.org/sites/default/files/ultimate_animated_character_pack_by_quaternius.zip",
         source="https://opengameart.org/content/animated-characters-pack (Quaternius 'Ultimate Animated Character Pack', CC0; also https://quaternius.com/packs/ultimatedanimatedcharacter.html)",
         note="50+ animated low-poly humanoid characters with animations, FBX."),
    # ---- textures ----
    dict(name="kenney_prototype-textures", kind="kenney", slug="prototype-textures", license="CC0 1.0"),
    # ---- audio ----
    dict(name="kenney_impact-sounds", kind="kenney", slug="impact-sounds", license="CC0 1.0"),
    dict(name="kenney_interface-sounds", kind="kenney", slug="interface-sounds", license="CC0 1.0"),
    dict(name="kenney_ui-audio", kind="kenney", slug="ui-audio", license="CC0 1.0"),
    dict(name="kenney_digital-audio", kind="kenney", slug="digital-audio", license="CC0 1.0", optional=True),
    dict(name="sfx_guns", kind="multi", license="CC0 1.0",
         source="OpenGameArt CC0 gun sounds (see LICENSE.txt for per-file sources)",
         items=[
             dict(kind="zip", url="https://opengameart.org/sites/default/files/sounds.zip", sub="gunshots",
                  source="https://opengameart.org/content/gunshot-sounds (Tabasco, CC0) - CZ-52 pistol, Mosin Nagant, SKS, shotgun"),
             dict(kind="file", url="https://opengameart.org/sites/default/files/gunreload1.wav", sub="reload", target="reload_pistol.wav",
                  source="https://opengameart.org/content/gun-reload-sounds (SpringySpringo, CC0)"),
             dict(kind="file", url="https://opengameart.org/sites/default/files/assaultriflereload1_0.wav", sub="reload", target="reload_rifle.wav",
                  source="https://opengameart.org/content/gun-reload-sounds (SpringySpringo, CC0)"),
             dict(kind="file", url="https://opengameart.org/sites/default/files/shotguncock_0.wav", sub="reload", target="shotgun_cock.wav",
                  source="https://opengameart.org/content/gun-reload-sounds (SpringySpringo, CC0)"),
             dict(kind="file", url="https://opengameart.org/sites/default/files/clipload1.wav", sub="reload", target="clip_load1.wav",
                  source="https://opengameart.org/content/gun-reload-sound-effects (BMacZero, CC0)"),
             dict(kind="file", url="https://opengameart.org/sites/default/files/clipload2.wav", sub="reload", target="clip_load2.wav",
                  source="https://opengameart.org/content/gun-reload-sound-effects (BMacZero, CC0)"),
             dict(kind="file", url="https://opengameart.org/sites/default/files/singlebullet1.wav", sub="reload", target="single_bullet_load.wav",
                  source="https://opengameart.org/content/gun-reload-sound-effects (BMacZero, CC0)"),
             dict(kind="file", url="https://opengameart.org/sites/default/files/reload.wav", sub="reload", target="reload_handgun_full.wav",
                  source="https://opengameart.org/content/handgun-reload-sound-effect (zer0_sol, CC0)"),
         ]),
    # ---- UI: icons + widgets (2D, PNG only) ----
    dict(name="kenney_game-icons", kind="kenney", slug="game-icons", license="CC0 1.0",
         keep_ext=UI_KEEP_EXT,
         note="105 flat game icons (white/black on transparent) as PNG/<Color>/<1x|2x>/<name>.png"),
    dict(name="kenney_game-icons-expansion", kind="kenney", slug="game-icons-expansion", license="CC0 1.0", optional=True,
         keep_ext=UI_KEEP_EXT, junk_dirs={"game icons (base)"},
         note="81 extra icons + 17 colored; the bundled copy of the base pack ('Game icons (base)/') is dropped - see kenney_game-icons"),
    dict(name="kenney_ui-pack", kind="kenney", slug="ui-pack", license="CC0 1.0",
         keep_ext=UI_KEEP_EXT,
         note="UI Pack 2.x: buttons/panels/sliders/checkboxes as PNG/<Blue|Green|Grey|Red|Yellow|Extra>/<Default|Double>/*.png, plus Font/*.ttf and Sounds/*.ogg"),
    dict(name="kenney_ui-pack-sci-fi", kind="kenney", slug="ui-pack-sci-fi", license="CC0 1.0", optional=True,
         keep_ext=UI_KEEP_EXT,
         note="UI Pack - Sci-Fi (zip is named kenney_ui-pack-space-expansion.zip): same layout as kenney_ui-pack"),
    dict(name="kenney_ui-pack-adventure", kind="kenney", slug="ui-pack-adventure", license="CC0 1.0", optional=True,
         keep_ext=UI_KEEP_EXT,
         note="UI Pack - Adventure: wooden/parchment widgets as PNG/<Default|Double>/*.png"),
    # ---- font ----
    dict(name="fonts", kind="multi", license="SIL Open Font License 1.1",
         source="Google Fonts (OFL): https://github.com/google/fonts/tree/main/ofl/rubik (Rubik), "
                "https://github.com/google/fonts/tree/main/ofl/russoone (Russo One, Cyrillic display), "
                "https://github.com/google/fonts/tree/main/ofl/montserrat (Montserrat)",
         items=[
             dict(kind="file", url="https://raw.githubusercontent.com/google/fonts/main/ofl/rubik/static/Rubik-Regular.ttf", target="Rubik-Regular.ttf",
                  fallback="https://raw.githubusercontent.com/google/fonts/main/ofl/rubik/Rubik%5Bwght%5D.ttf", fallback_target="Rubik-Variable.ttf",
                  source="https://github.com/google/fonts/tree/main/ofl/rubik"),
             dict(kind="file", url="https://raw.githubusercontent.com/google/fonts/main/ofl/rubik/static/Rubik-Bold.ttf", target="Rubik-Bold.ttf",
                  optional=True, source="https://github.com/google/fonts/tree/main/ofl/rubik"),
             dict(kind="file", url="https://raw.githubusercontent.com/google/fonts/main/ofl/rubik/OFL.txt", target="OFL_Rubik.txt",
                  source="https://github.com/google/fonts/tree/main/ofl/rubik"),
             # Russo One - heavy display face with Cyrillic, good for titles / HUD numbers
             dict(kind="file", url="https://raw.githubusercontent.com/google/fonts/main/ofl/russoone/RussoOne-Regular.ttf", target="RussoOne-Regular.ttf",
                  source="https://github.com/google/fonts/tree/main/ofl/russoone (Russo One, OFL 1.1, Cyrillic+Latin)"),
             dict(kind="file", url="https://raw.githubusercontent.com/google/fonts/main/ofl/russoone/OFL.txt", target="OFL_RussoOne.txt",
                  source="https://github.com/google/fonts/tree/main/ofl/russoone"),
             # Montserrat ExtraBold - only if a static TTF is published (google/fonts ships the variable font only -> skipped)
             dict(kind="file", url="https://raw.githubusercontent.com/google/fonts/main/ofl/montserrat/static/Montserrat-ExtraBold.ttf",
                  target="Montserrat-ExtraBold.ttf", optional=True,
                  source="https://github.com/google/fonts/tree/main/ofl/montserrat (Montserrat, OFL 1.1, Cyrillic+Latin)"),
             dict(kind="file", url="https://raw.githubusercontent.com/google/fonts/main/ofl/montserrat/OFL.txt", target="OFL_Montserrat.txt",
                  optional=True, requires="Montserrat-ExtraBold.ttf",
                  source="https://github.com/google/fonts/tree/main/ofl/montserrat"),
         ]),
    # ---- sky ----
    dict(name="sky", kind="multi", license="CC0 1.0",
         source="https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky (Poly Haven, CC0)",
         items=[
             dict(kind="file", url="https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/kloofendal_48d_partly_cloudy_puresky_2k.hdr",
                  target="kloofendal_48d_partly_cloudy_puresky_2k.hdr",
                  source="https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky"),
         ]),
    # ---- PBR textures (ambientCG 1K-JPG, CC0; Poly Haven 1k jpg as per-asset fallback) ----
    # Each item: key = what the game looks up by substring, id = ambientCG asset id (folder + file prefix),
    # ph = Poly Haven asset id used only when the ambientCG download fails. Only Color + NormalGL are kept:
    #   Imported/textures_cc0/<id>/<id>_Color.jpg  and  <id>_Normal.jpg
    dict(name="textures_cc0", kind="pbr", license="CC0 1.0",
         source="https://ambientcg.com (CC0 1.0; per-asset pages https://ambientcg.com/view?id=<id>); "
                "fallback https://polyhaven.com (CC0 1.0)",
         keep_ext={".jpg"},
         note="1K JPG PBR materials, only <id>_Color.jpg (albedo) and <id>_Normal.jpg (OpenGL normal) per asset; "
              "Roughness/Displacement/AO/metal/DX-normal maps and .blend/.usdc/.mtlx were dropped to save space.",
         items=[
             dict(key="grass", id="Grass004", ph="leafy_grass"),            # neat lawn
             dict(key="grass", id="Grass001", ph="sparse_grass"),           # wilder, darker, dense
             dict(key="ground_dirt", id="Ground048", ph="dirt_floor"),      # brown soil
             dict(key="asphalt", id="Asphalt015", ph="asphalt_02"),         # plain road
             dict(key="asphalt", id="Asphalt026C", ph="worn_asphalt"),      # cracked
             dict(key="concrete", id="Concrete034", ph="concrete_floor_02"),  # smooth light
             dict(key="concrete", id="Concrete044C", ph="dirty_concrete"),    # damaged
             dict(key="bricks", id="Bricks059", ph="brick_wall_001"),       # red
             dict(key="bricks", id="Bricks061", ph="brick_wall_02"),        # grey
             dict(key="plaster", id="PaintedPlaster012", ph="painted_plaster_wall"),  # old painted wall
             dict(key="roofingtiles", id="RoofingTiles007", ph="clay_roof_tiles"),    # orange clay
             dict(key="roofingtiles", id="RoofingTiles013A", ph="grey_roof_tiles"),   # dark
             dict(key="corrugated", id="CorrugatedSteel009", ph="corrugated_iron"),   # dark industrial
             dict(key="woodplanks", id="Planks037A", ph="brown_planks_03"),           # worn floor planks
             dict(key="tiles", id="Tiles141", ph="dirty_tiles"),            # beige bathroom floor
             dict(key="tiles", id="Tiles012", ph="floor_tiles_02"),         # black/white checker
             dict(key="pavingstones", id="PavingStones070", ph="cobblestone_floor_01"),  # old cobble
             dict(key="gravel", id="Gravel022", ph="gravel"),
             dict(key="facade", id="Facade018A", ph="rectangular_facade_tiles"),  # brick office
             dict(key="facade", id="Facade019A", ph="concrete_tile_facade"),      # concrete office
             dict(key="facade", id="Facade001", ph="rectangular_facade_tiles_02"),  # glass
             dict(key="rock", id="Rock030", ph="gray_rocks"),
             dict(key="sand", id="Ground033", ph="coast_sand_01"),          # light beach sand
             dict(key="carpet", id="Carpet001", ph="dirty_carpet"),
             dict(key="fabric", id="Fabric030", ph="denim_fabric"),
             dict(key="marble", id="Marble012", ph="marble_01"),
             dict(key="rust", id="Rust009", ph="rusty_metal"),
         ]),
    # ---- realistic props (Poly Haven models, CC0) ----
    # Each item: id = Poly Haven asset id (https://polyhaven.com/a/<id>), desc = what it is (for the report/LICENSE).
    # Fetched via https://api.polyhaven.com/files/<id> -> fbx -> <res> -> fbx -> {url, include{textures/...}}:
    #   Imported/polyhaven_models/<id>/<id>.fbx  +  <id>/textures/<name>_<res>.(jpg|png)
    dict(name="polyhaven_models", kind="polyhaven", license="CC0 1.0",
         source="https://polyhaven.com/models (Poly Haven, CC0 1.0; per-asset pages https://polyhaven.com/a/<id>)",
         keep_ext={".fbx", ".jpg", ".png"}, res="1k", max_mb=40,
         note="Realistic scanned/PBR props for the urban-industrial map; 1k FBX + included 1k JPG/PNG textures only "
              "(EXR displacement maps and higher resolutions were dropped).",
         items=[
             dict(id="concrete_road_barrier", desc="concrete jersey barrier"),
             dict(id="concrete_road_barrier_02", desc="concrete road barrier, worn"),
             dict(id="Barrel_01", desc="explosive metal barrel"),
             dict(id="barrel_03", desc="rusty oil drum"),
             dict(id="metal_jerrycan", desc="metal jerrycan"),
             dict(id="wooden_crate_01", desc="wooden crate"),
             dict(id="plastic_crate_01", desc="plastic crate"),
             dict(id="cardboard_box_01", desc="cardboard box"),
             dict(id="old_tyre", desc="old car tyre"),
             dict(id="metal_trash_can", desc="metal trash can"),
             dict(id="trashbag", desc="black trash bag"),
             dict(id="modular_street_seating", desc="modular street bench"),
             dict(id="street_lamp_01", desc="street lamp post"),
             dict(id="fire_hydrant", desc="fire hydrant"),
             dict(id="portable_generator", desc="portable generator"),
             dict(id="steel_frame_shelves_01", desc="steel frame shelves"),
             dict(id="worn_metal_rack", desc="worn metal rack"),
             dict(id="Sofa_01", desc="fabric sofa"),
             dict(id="ArmChair_01", desc="armchair"),
             dict(id="plastic_monobloc_chair_01", desc="plastic monobloc chair"),
             dict(id="metal_office_desk", desc="metal office desk"),
             dict(id="WoodenTable_02", desc="wooden table"),
             dict(id="painted_wooden_cabinet", desc="painted wooden cabinet"),
             dict(id="old_bed_frame", desc="old metal bed frame"),
             dict(id="wooden_bookshelf_worn", desc="worn wooden bookshelf"),
             dict(id="Television_01", desc="old CRT television"),
             dict(id="potted_plant_01", desc="potted plant"),
             dict(id="modular_chainlink_fence", desc="chainlink fence module"),
             dict(id="ladder_sectioned_01", desc="aluminium sectioned ladder"),
             dict(id="metal_toolbox", desc="metal toolbox"),
             dict(id="korean_fire_extinguisher_01", desc="fire extinguisher"),
             dict(id="utility_box_01", desc="wall utility box"),
             dict(id="propane_tank", desc="propane gas tank"),
             dict(id="hand_truck", desc="hand truck dolly"),
             dict(id="ammo_box", desc="ammo box"),
             dict(id="covered_car", desc="car under tarp cover"),
         ]),
]


def log(*a):
    print(*a, flush=True)


# ----------------------------------------------------------------------------------------------
# HTTP helpers
# ----------------------------------------------------------------------------------------------
def http_get(url, timeout=120, tries=3, stream=False):
    last = None
    for i in range(tries):
        try:
            r = requests.get(url, headers=UA, timeout=timeout, stream=stream, allow_redirects=True)
            if r.status_code == 200:
                return r
            last = f"HTTP {r.status_code}"
            if r.status_code in (403, 404):
                break
        except Exception as e:  # noqa
            last = repr(e)
        time.sleep(3 * (i + 1))
    raise RuntimeError(f"GET {url} failed: {last}")


def download(url, dest: Path, force=False):
    dest.parent.mkdir(parents=True, exist_ok=True)
    if dest.exists() and dest.stat().st_size > 0 and not force:
        log(f"    cached  {dest.name} ({dest.stat().st_size // 1024} KB)")
        return dest
    log(f"    GET {url}")
    r = http_get(url, timeout=300, stream=True)
    tmp = dest.with_suffix(dest.suffix + ".part")
    n = 0
    with open(tmp, "wb") as f:
        for chunk in r.iter_content(1 << 16):
            f.write(chunk)
            n += len(chunk)
    tmp.replace(dest)
    log(f"    saved   {dest.name} ({n // 1024} KB)")
    return dest


def kenney_zip_url(slug):
    page = f"https://kenney.nl/assets/{slug}"
    r = http_get(page, timeout=60)
    html = r.text
    m = re.findall(r'(https?://kenney\.nl/media/pages/assets/[^"\s]+?\.zip)', html)
    if not m:
        m = re.findall(r'href="([^"]+\.zip)"', html)
        m = [x if x.startswith("http") else "https://kenney.nl" + x for x in m]
    if not m:
        raise RuntimeError(f"no zip link on {page}")
    lic = "CC0 1.0" if "CC0" in html else ("CC-BY (check page)" if "CC-BY" in html or "CC BY" in html else "unknown (check page)")
    return m[0], page, lic


# ----------------------------------------------------------------------------------------------
# Extraction with filtering
# ----------------------------------------------------------------------------------------------
def _is_junk_path(parts, junk_dirs=JUNK_DIRS):
    for p in parts[:-1]:
        if p.lower() in junk_dirs:
            return True
    name = parts[-1]
    if JUNK_NAMES.match(name) and Path(name).suffix.lower() in TEX_EXT:
        return True
    return False


def plan_members(zf: zipfile.ZipFile, keep_ext=None, junk_dirs=None):
    """Return list of (member, relative_target_parts) that we want to keep.

    keep_ext  - per-pack set of extensions to keep (default KEEP_EXT)
    junk_dirs - extra directory names (lower-case) to drop, on top of JUNK_DIRS
    """
    keep_ext = KEEP_EXT if keep_ext is None else keep_ext
    junk = JUNK_DIRS | {d.lower() for d in (junk_dirs or ())}
    infos = [i for i in zf.infolist() if not i.is_dir()]
    names = [i.filename.replace("\\", "/") for i in infos]
    # strip common single root dir
    roots = {n.split("/")[0] for n in names if "/" in n}
    top_files = [n for n in names if "/" not in n]
    strip_root = None
    if len(roots) == 1 and not top_files:
        strip_root = next(iter(roots))
    keep = []
    have_fbx = any(n.lower().endswith(".fbx") for n in names)
    for info, n in zip(infos, names):
        parts = n.split("/")
        if strip_root and parts[0] == strip_root:
            parts = parts[1:]
        if not parts or any(p in ("..", "") for p in parts):
            continue
        ext = Path(parts[-1]).suffix.lower()
        lower_name = parts[-1].lower()
        if ext not in keep_ext:
            # keep the pack's own license/readme text (unless it sits in a dropped sub-folder)
            if ext == ".txt" and ("licen" in lower_name or "readme" in lower_name or "credit" in lower_name) \
                    and not _is_junk_path(parts, junk):
                keep.append((info, ["_pack_" + parts[-1]]))
            continue
        if _is_junk_path(parts, junk):
            continue
        # when FBX exists, drop the OBJ/MTL duplicates (same models, double import otherwise)
        if have_fbx and ext in (".obj", ".mtl"):
            continue
        # textures that only live inside OBJ/GLTF folders are dropped with them
        if have_fbx and ext in TEX_EXT and any(p.lower().startswith("obj") or p.lower().startswith("gltf") for p in parts[:-1]):
            continue
        keep.append((info, parts))
    return keep


def extract_zip(zip_path: Path, out_dir: Path, subdir=None, keep_ext=None, junk_dirs=None):
    with zipfile.ZipFile(zip_path) as zf:
        bad = zf.testzip()
        if bad:
            raise RuntimeError(f"corrupt zip member {bad} in {zip_path}")
        keep = plan_members(zf, keep_ext=keep_ext, junk_dirs=junk_dirs)
        n = 0
        for info, parts in keep:
            target = out_dir
            if subdir:
                target = target / subdir
            target = target.joinpath(*parts)
            # zip-slip guard
            if os.path.commonpath([str(out_dir.resolve()), str(target.resolve())]) != str(out_dir.resolve()):
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            with zf.open(info) as src, open(target, "wb") as dst:
                shutil.copyfileobj(src, dst)
            n += 1
        return n


def dir_size(p: Path):
    return sum(f.stat().st_size for f in p.rglob("*") if f.is_file())


def write_license(pack_dir: Path, pack, download_urls, extra_lines=()):
    pack_dir.mkdir(parents=True, exist_ok=True)
    lines = [
        f"Pack: {pack['name']}",
        f"License: {pack.get('license', 'unknown')}",
        f"Source: {pack.get('source', '')}",
    ]
    for u in download_urls:
        lines.append(f"Download: {u}")
    if pack.get("note"):
        lines.append(f"Note: {pack['note']}")
    lines += list(extra_lines)
    lines.append(f"Fetched: {datetime.date.today().isoformat()} by Tools/fetch_assets.py")
    kept = "/".join(sorted(e.lstrip(".").upper() for e in pack.get("keep_ext", KEEP_EXT)))
    lines.append(f"Only {kept} files were kept; previews, vectors, spritesheets, GLTF/GLB, .blend and sample scenes were removed.")
    (pack_dir / "LICENSE.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")


def append_license(pack_dir: Path, pack, extra_lines):
    """Additive update of an existing LICENSE.txt: refresh the Source line, append the new items."""
    lf = pack_dir / "LICENSE.txt"
    lines = lf.read_text(encoding="utf-8", errors="replace").splitlines() if lf.exists() else []
    lines = [f"Source: {pack.get('source', '')}" if l.startswith("Source:") else l for l in lines]
    lines.append(f"Added {datetime.date.today().isoformat()} by Tools/fetch_assets.py:")
    lines += list(extra_lines)
    lf.write_text("\n".join(lines) + "\n", encoding="utf-8")


# ----------------------------------------------------------------------------------------------
# Per-pack fetch
# ----------------------------------------------------------------------------------------------
def _file_item_present(it, pack_dir: Path):
    tgt_dir = pack_dir / it["sub"] if it.get("sub") else pack_dir
    if (tgt_dir / it["target"]).exists():
        return True
    return bool(it.get("fallback_target")) and (tgt_dir / it["fallback_target"]).exists()


def fetch_file_item(it, pack_dir: Path, dl_dir: Path, force, urls, extra):
    """Download one single-file item (with fallback / optional / requires handling). Returns 1 if a file landed."""
    tgt_dir = pack_dir / it["sub"] if it.get("sub") else pack_dir
    tgt_dir.mkdir(parents=True, exist_ok=True)
    if it.get("requires") and not (tgt_dir / it["requires"]).exists():
        log(f"    skip {it['target']} (requires {it['requires']}, which is absent)")
        return 0
    try:
        f = download(it["url"], dl_dir / it["target"], force)
        shutil.copyfile(f, tgt_dir / it["target"])
        urls.append(it["url"])
        extra.append(f"  {it['target']}  <- {it['source']}")
    except Exception as e:
        if it.get("fallback"):
            log(f"    primary failed ({e}); trying fallback")
            f = download(it["fallback"], dl_dir / it["fallback_target"], force)
            shutil.copyfile(f, tgt_dir / it["fallback_target"])
            urls.append(it["fallback"])
            extra.append(f"  {it['fallback_target']}  <- {it['source']}")
        elif it.get("optional"):
            log(f"    optional item failed: {e}")
            return 0
        else:
            raise
    return 1


# ---- PBR textures (ambientCG with Poly Haven fallback) ----
PBR_MAPS = {"Color": "_Color.jpg", "NormalGL": "_Normal.jpg"}   # zip-member suffix -> target suffix


def _pbr_present(it, pack_dir: Path):
    for aid in (it["id"], it.get("ph")):
        if aid and (pack_dir / aid / f"{aid}_Color.jpg").exists():
            return True
    return False


def fetch_pbr_ambientcg(aid, pack_dir: Path, dl_dir: Path, force):
    """Download <aid>_1K-JPG.zip from ambientCG, keep only Color + NormalGL as <aid>/<aid>_Color.jpg, <aid>_Normal.jpg."""
    url = f"https://ambientcg.com/get?file={aid}_1K-JPG.zip"
    zp = download(url, dl_dir / f"{aid}_1K-JPG.zip", force)
    out = pack_dir / aid
    got = {}
    with zipfile.ZipFile(zp) as zf:
        if zf.testzip():
            raise RuntimeError(f"corrupt zip {zp.name}")
        for info in zf.infolist():
            if info.is_dir():
                continue
            base = Path(info.filename.replace("\\", "/")).name
            for suffix, tgt_suffix in PBR_MAPS.items():
                if base.lower().endswith(f"_{suffix}.jpg".lower()) and suffix not in got:
                    out.mkdir(parents=True, exist_ok=True)
                    tgt = out / f"{aid}{tgt_suffix}"
                    with zf.open(info) as src, open(tgt, "wb") as dst:
                        shutil.copyfileobj(src, dst)
                    got[suffix] = tgt
    if "Color" not in got:
        shutil.rmtree(out, ignore_errors=True)
        raise RuntimeError(f"{aid}: no *_Color.jpg in {zp.name}")
    if "NormalGL" not in got:
        log(f"    warning: {aid} has no NormalGL map")
    return url, len(got)


def fetch_pbr_polyhaven(ph, pack_dir: Path, dl_dir: Path, force):
    """Fallback: Poly Haven 1k jpg Diffuse + nor_gl -> <ph>/<ph>_Color.jpg, <ph>_Normal.jpg."""
    api = f"https://api.polyhaven.com/files/{ph}"
    files = http_get(api, timeout=60).json()

    def url_of(*cats):
        for c in cats:
            node = files.get(c) or {}
            u = ((node.get("1k") or {}).get("jpg") or {}).get("url")
            if u:
                return u
        return None

    color = url_of("Diffuse", "diffuse", "Color")
    normal = url_of("nor_gl")
    if not color:
        raise RuntimeError(f"polyhaven {ph}: no 1k jpg Diffuse map")
    out = pack_dir / ph
    out.mkdir(parents=True, exist_ok=True)
    n = 0
    for u, tgt in ((color, f"{ph}_Color.jpg"), (normal, f"{ph}_Normal.jpg")):
        if not u:
            continue
        f = download(u, dl_dir / tgt, force)
        shutil.copyfile(f, out / tgt)
        n += 1
    return api, n


def fetch_pbr_item(it, pack_dir: Path, dl_dir: Path, force, urls, extra):
    """One PBR material: ambientCG first, Poly Haven fallback. Returns number of jpg files that landed."""
    aid = it["id"]
    try:
        url, n = fetch_pbr_ambientcg(aid, pack_dir, dl_dir, force)
        urls.append(url)
        extra.append(f"  {aid}/  key={it['key']}  https://ambientcg.com/view?id={aid}  (ambientCG, CC0 1.0)")
        return n
    except Exception as e:
        if not it.get("ph"):
            raise
        log(f"    ambientCG {aid} failed ({e}); falling back to Poly Haven {it['ph']}")
        ph = it["ph"]
        api, n = fetch_pbr_polyhaven(ph, pack_dir, dl_dir, force)
        urls.append(api)
        extra.append(f"  {ph}/  key={it['key']}  https://polyhaven.com/a/{ph}  (Poly Haven, CC0 1.0; fallback for ambientCG {aid})")
        return n


# ---- Poly Haven models (FBX + included textures) ----
PH_TEX_EXT = {".jpg", ".jpeg", ".png"}     # include members kept; .exr displacement maps are dropped
_PH_NAMES = None                           # id -> display name, from /assets?t=models (fetched once)


def _ph_model_present(it, pack_dir: Path):
    return (pack_dir / it["id"] / f"{it['id']}.fbx").exists()


def _ph_name(aid):
    global _PH_NAMES
    if _PH_NAMES is None:
        try:
            _PH_NAMES = {k: v.get("name", k) for k, v in http_get("https://api.polyhaven.com/assets?t=models", timeout=60).json().items()}
        except Exception as e:  # noqa
            log(f"    warning: could not fetch Poly Haven model list ({e})")
            _PH_NAMES = {}
    return _PH_NAMES.get(aid, aid)


def fetch_ph_model(it, pack_dir: Path, dl_dir: Path, force, urls, extra, res="1k", max_mb=40):
    """One Poly Haven model: <id>/<id>.fbx + <id>/textures/*.jpg|png (layout as the API's 'include' says).

    Returns the number of files that landed. Raises when the asset has no FBX or exceeds max_mb.
    """
    aid = it["id"]
    api = f"https://api.polyhaven.com/files/{aid}"
    files = http_get(api, timeout=60).json()
    fbx_res = files.get("fbx") or {}
    node = None
    for r in (res, "2k", "1k"):
        node = (fbx_res.get(r) or {}).get("fbx")
        if node and node.get("url"):
            res = r
            break
    if not node or not node.get("url"):
        raise RuntimeError(f"{aid}: no FBX variant (have: {sorted(files.keys())})")
    include = node.get("include") or {}
    tex = []
    for rel, meta in include.items():
        rel = rel.replace("\\", "/")
        parts = rel.split("/")
        if Path(parts[-1]).suffix.lower() not in PH_TEX_EXT:
            continue
        if any(p in ("..", "", ".") for p in parts) or rel.startswith("/") or ":" in rel:
            continue
        if not meta.get("url"):
            continue
        tex.append((parts, meta))
    total = node.get("size", 0) + sum(m.get("size", 0) for _, m in tex)
    if total > max_mb * 1024 * 1024:
        raise RuntimeError(f"{aid}: {total / 1048576:.1f} MB exceeds max_mb={max_mb}")
    if not tex:
        log(f"    warning: {aid} has no jpg/png textures in its FBX include list")

    out = pack_dir / aid
    dl = dl_dir / aid
    try:
        f = download(node["url"], dl / f"{aid}.fbx", force)
        out.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(f, out / f"{aid}.fbx")
        n = 1
        for parts, meta in tex:
            f = download(meta["url"], dl.joinpath(*parts), force)
            tgt = out.joinpath(*parts)
            tgt.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(f, tgt)
            n += 1
    except Exception:
        shutil.rmtree(out, ignore_errors=True)   # never leave a half-downloaded model behind
        raise
    urls.append(node["url"])
    extra.append(f"  {aid}/  {_ph_name(aid)} ({it.get('desc', '')})  https://polyhaven.com/a/{aid}  (Poly Haven, CC0 1.0; {res} FBX + {len(tex)} textures, {total / 1048576:.1f} MB)")
    return n


def fetch_ph_items(items, pack, pack_dir: Path, dl_dir: Path, force, urls, extra):
    """Fetch a list of Poly Haven model items; returns (files_landed, failed_ids)."""
    n, failed = 0, []
    for it in items:
        log(f"  - {it['id']} ({it.get('desc', '')})")
        try:
            n += fetch_ph_model(it, pack_dir, dl_dir, force, urls, extra, res=pack.get("res", "1k"), max_mb=pack.get("max_mb", 40))
        except Exception as e:
            failed.append(it["id"])
            log(f"    {it['id']} failed: {e}")
    return n, failed


def fetch_pack(pack, force=False):
    name = pack["name"]
    pack_dir = IMPORTED / name
    dl_dir = DOWNLOADS / name
    kind = pack["kind"]
    if pack_dir.exists() and (pack_dir / "LICENSE.txt").exists() and not force:
        if kind == "pbr":
            missing = [it for it in pack["items"] if not _pbr_present(it, pack_dir)]
            if missing:
                dl_dir.mkdir(parents=True, exist_ok=True)
                urls, extra, n = [], [], 0
                for it in missing:
                    try:
                        n += fetch_pbr_item(it, pack_dir, dl_dir, force, urls, extra)
                    except Exception as e:
                        log(f"    {it['id']} failed: {e}")
                if n:
                    append_license(pack_dir, pack, extra)
                    log(f"[{name}] updated: +{n} files, {dir_size(pack_dir) // 1024} KB -> {pack_dir}")
                    return "updated"
        if kind == "polyhaven":
            missing = [it for it in pack["items"] if not _ph_model_present(it, pack_dir)]
            if missing:
                dl_dir.mkdir(parents=True, exist_ok=True)
                urls, extra = [], []
                n, failed = fetch_ph_items(missing, pack, pack_dir, dl_dir, force, urls, extra)
                if failed:
                    extra.append(f"  FAILED (not downloaded): {', '.join(failed)}")
                if n:
                    append_license(pack_dir, pack, extra)
                    log(f"[{name}] updated: +{n} files, {dir_size(pack_dir) // 1024} KB -> {pack_dir}")
                    return "updated"
        if kind == "multi":
            # additive update: fetch only file items whose target is missing (keeps Unity .meta of existing files)
            missing = [it for it in pack["items"] if it["kind"] == "file" and not _file_item_present(it, pack_dir)]
            if missing:
                dl_dir.mkdir(parents=True, exist_ok=True)
                urls, extra, n = [], [], 0
                for it in missing:
                    try:
                        n += fetch_file_item(it, pack_dir, dl_dir, force, urls, extra)
                    except Exception as e:
                        if it.get("optional"):
                            log(f"    optional item failed: {e}")
                        else:
                            raise
                if n:
                    append_license(pack_dir, pack, extra)
                    log(f"[{name}] updated: +{n} files, {dir_size(pack_dir) // 1024} KB -> {pack_dir}")
                    return "updated"
        log(f"[{name}] already present ({dir_size(pack_dir) // 1024} KB) - skip (use --force)")
        return "skipped"
    if pack_dir.exists() and force:
        shutil.rmtree(pack_dir)
    dl_dir.mkdir(parents=True, exist_ok=True)

    urls = []
    extra = []
    filt = dict(keep_ext=pack.get("keep_ext"), junk_dirs=pack.get("junk_dirs"))
    if kind == "kenney":
        url, page, lic = kenney_zip_url(pack["slug"])
        pack["source"] = page
        if "check page" in lic:
            pack["license"] = lic
        zp = download(url, dl_dir / Path(url).name, force)
        n = extract_zip(zp, pack_dir, **filt)
        urls.append(url)
    elif kind == "zip":
        url = pack["url"]
        zp = download(url, dl_dir / Path(url).name, force)
        n = extract_zip(zp, pack_dir, **filt)
        urls.append(url)
    elif kind == "multi":
        n = 0
        for it in pack["items"]:
            try:
                if it["kind"] == "zip":
                    zp = download(it["url"], dl_dir / Path(it["url"]).name.replace("%20", "_"), force)
                    n += extract_zip(zp, pack_dir, subdir=it.get("sub"), **filt)
                    urls.append(it["url"])
                    extra.append(f"  {it.get('sub','')}/  <- {it['source']}")
                else:
                    n += fetch_file_item(it, pack_dir, dl_dir, force, urls, extra)
            except Exception as e:
                if it.get("optional"):
                    log(f"    optional item failed: {e}")
                else:
                    raise
    elif kind == "pbr":
        n, failed = 0, []
        for it in pack["items"]:
            log(f"  - {it['key']}: {it['id']}")
            try:
                n += fetch_pbr_item(it, pack_dir, dl_dir, force, urls, extra)
            except Exception as e:
                failed.append(it["id"])
                log(f"    {it['id']} failed: {e}")
        if n == 0:
            raise RuntimeError("no textures downloaded (ambientCG and Poly Haven both unreachable?)")
        if failed:
            extra.append(f"  FAILED (not downloaded): {', '.join(failed)}")
    elif kind == "polyhaven":
        n, failed = fetch_ph_items(pack["items"], pack, pack_dir, dl_dir, force, urls, extra)
        if n == 0:
            raise RuntimeError("no models downloaded (Poly Haven unreachable?)")
        if failed:
            extra.append(f"  FAILED (not downloaded): {', '.join(failed)}")
    else:
        raise RuntimeError(f"unknown kind {kind}")
    write_license(pack_dir, pack, urls, extra)
    log(f"[{name}] OK: {n} files, {dir_size(pack_dir) // 1024} KB -> {pack_dir}")
    return "ok"


# ----------------------------------------------------------------------------------------------
# asset_index.json
# ----------------------------------------------------------------------------------------------
def read_license_meta(pack_dir: Path):
    meta = {"license": "unknown", "source": ""}
    lf = pack_dir / "LICENSE.txt"
    if lf.exists():
        for line in lf.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.startswith("License:"):
                meta["license"] = line.split(":", 1)[1].strip()
            elif line.startswith("Source:"):
                meta["source"] = line.split(":", 1)[1].strip()
    return meta


def build_index():
    index = {"generated": datetime.date.today().isoformat(),
             "root": "Assets/Resources",
             "note": "paths are relative to Assets/Resources, without extension; use Resources.Load<T>(path)",
             "packs": []}
    for pack_dir in sorted(p for p in IMPORTED.iterdir() if p.is_dir()):
        meta = read_license_meta(pack_dir)
        models, audio, textures, fonts, other = [], [], [], [], []
        for f in sorted(pack_dir.rglob("*")):
            if not f.is_file() or f.suffix.lower() == ".meta":
                continue
            rel = f.relative_to(RESOURCES).with_suffix("").as_posix()
            ext = f.suffix.lower()
            if ext in MODEL_EXT:
                models.append(rel)
            elif ext in AUDIO_EXT:
                audio.append(rel)
            elif ext in TEX_EXT:
                textures.append(rel)
            elif ext in FONT_EXT:
                fonts.append(rel)
        entry = {"pack": pack_dir.name, "license": meta["license"], "source": meta["source"],
                 "models": models, "audio": audio, "textures": textures}
        if fonts:
            entry["fonts"] = fonts
        index["packs"].append(entry)
    out = IMPORTED / "asset_index.json"
    out.write_text(json.dumps(index, indent=1, ensure_ascii=False), encoding="utf-8")
    log(f"asset_index.json: {len(index['packs'])} packs, "
        f"{sum(len(p['models']) for p in index['packs'])} models, "
        f"{sum(len(p['audio']) for p in index['packs'])} audio, "
        f"{sum(len(p['textures']) for p in index['packs'])} textures -> {out}")
    return index


# ----------------------------------------------------------------------------------------------
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*", help="pack names to fetch")
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--index-only", action="store_true")
    args = ap.parse_args()

    IMPORTED.mkdir(parents=True, exist_ok=True)
    if not args.index_only:
        results = {}
        for pack in PACKS:
            if args.only and pack["name"] not in args.only:
                continue
            log(f"== {pack['name']} ==")
            try:
                results[pack["name"]] = fetch_pack(pack, args.force)
            except Exception as e:
                results[pack["name"]] = f"FAILED: {e}"
                log(f"[{pack['name']}] FAILED: {e}")
        log("\n==== SUMMARY ====")
        for k, v in results.items():
            d = IMPORTED / k
            size = dir_size(d) // 1024 if d.exists() else 0
            log(f"{k:32s} {v:12s} {size:8d} KB")
    build_index()


if __name__ == "__main__":
    main()
