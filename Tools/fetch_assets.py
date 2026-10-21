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
Only FBX/OBJ(+MTL)/PNG/JPG/WAV/OGG/MP3/TTF/HDR files are kept; GLB/GLTF/blend/
previews/sample scenes/unitypackages are dropped.  When a pack ships both FBX
and OBJ versions of the same models, only the FBX set is kept (OBJ is dropped)
to keep the Unity import light.
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

KEEP_EXT = {".fbx", ".obj", ".mtl", ".png", ".jpg", ".jpeg", ".wav", ".ogg", ".mp3", ".ttf", ".hdr"}
MODEL_EXT = {".fbx", ".obj"}
AUDIO_EXT = {".wav", ".ogg", ".mp3"}
TEX_EXT = {".png", ".jpg", ".jpeg", ".hdr"}
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
    # ---- font ----
    dict(name="fonts", kind="multi", license="SIL Open Font License 1.1",
         source="https://github.com/google/fonts/tree/main/ofl/rubik (Rubik, Cyrillic+Latin)",
         items=[
             dict(kind="file", url="https://raw.githubusercontent.com/google/fonts/main/ofl/rubik/static/Rubik-Regular.ttf", target="Rubik-Regular.ttf",
                  fallback="https://raw.githubusercontent.com/google/fonts/main/ofl/rubik/Rubik%5Bwght%5D.ttf", fallback_target="Rubik-Variable.ttf",
                  source="https://github.com/google/fonts/tree/main/ofl/rubik"),
             dict(kind="file", url="https://raw.githubusercontent.com/google/fonts/main/ofl/rubik/static/Rubik-Bold.ttf", target="Rubik-Bold.ttf",
                  optional=True, source="https://github.com/google/fonts/tree/main/ofl/rubik"),
             dict(kind="file", url="https://raw.githubusercontent.com/google/fonts/main/ofl/rubik/OFL.txt", target="OFL_Rubik.txt",
                  source="https://github.com/google/fonts/tree/main/ofl/rubik"),
         ]),
    # ---- sky ----
    dict(name="sky", kind="multi", license="CC0 1.0",
         source="https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky (Poly Haven, CC0)",
         items=[
             dict(kind="file", url="https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/kloofendal_48d_partly_cloudy_puresky_2k.hdr",
                  target="kloofendal_48d_partly_cloudy_puresky_2k.hdr",
                  source="https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky"),
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
def _is_junk_path(parts):
    for p in parts[:-1]:
        if p.lower() in JUNK_DIRS:
            return True
    name = parts[-1]
    if JUNK_NAMES.match(name) and Path(name).suffix.lower() in TEX_EXT:
        return True
    return False


def plan_members(zf: zipfile.ZipFile):
    """Return list of (member, relative_target_parts) that we want to keep."""
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
        if ext not in KEEP_EXT:
            # keep the pack's own license/readme text
            if ext == ".txt" and ("licen" in lower_name or "readme" in lower_name or "credit" in lower_name):
                keep.append((info, ["_pack_" + parts[-1]]))
            continue
        if _is_junk_path(parts):
            continue
        # when FBX exists, drop the OBJ/MTL duplicates (same models, double import otherwise)
        if have_fbx and ext in (".obj", ".mtl"):
            continue
        # textures that only live inside OBJ/GLTF folders are dropped with them
        if have_fbx and ext in TEX_EXT and any(p.lower().startswith("obj") or p.lower().startswith("gltf") for p in parts[:-1]):
            continue
        keep.append((info, parts))
    return keep


def extract_zip(zip_path: Path, out_dir: Path, subdir=None):
    with zipfile.ZipFile(zip_path) as zf:
        bad = zf.testzip()
        if bad:
            raise RuntimeError(f"corrupt zip member {bad} in {zip_path}")
        keep = plan_members(zf)
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
    lines.append("Only FBX/OBJ/PNG/WAV/OGG/MP3/TTF/HDR files were kept; previews, GLTF/GLB, .blend and sample scenes were removed.")
    (pack_dir / "LICENSE.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")


# ----------------------------------------------------------------------------------------------
# Per-pack fetch
# ----------------------------------------------------------------------------------------------
def fetch_pack(pack, force=False):
    name = pack["name"]
    pack_dir = IMPORTED / name
    dl_dir = DOWNLOADS / name
    if pack_dir.exists() and (pack_dir / "LICENSE.txt").exists() and not force:
        log(f"[{name}] already present ({dir_size(pack_dir) // 1024} KB) - skip (use --force)")
        return "skipped"
    if pack_dir.exists() and force:
        shutil.rmtree(pack_dir)
    dl_dir.mkdir(parents=True, exist_ok=True)

    kind = pack["kind"]
    urls = []
    extra = []
    if kind == "kenney":
        url, page, lic = kenney_zip_url(pack["slug"])
        pack["source"] = page
        if "check page" in lic:
            pack["license"] = lic
        zp = download(url, dl_dir / Path(url).name, force)
        n = extract_zip(zp, pack_dir)
        urls.append(url)
    elif kind == "zip":
        url = pack["url"]
        zp = download(url, dl_dir / Path(url).name, force)
        n = extract_zip(zp, pack_dir)
        urls.append(url)
    elif kind == "multi":
        n = 0
        for it in pack["items"]:
            try:
                if it["kind"] == "zip":
                    zp = download(it["url"], dl_dir / Path(it["url"]).name.replace("%20", "_"), force)
                    n += extract_zip(zp, pack_dir, subdir=it.get("sub"))
                    urls.append(it["url"])
                    extra.append(f"  {it.get('sub','')}/  <- {it['source']}")
                else:
                    tgt_dir = pack_dir / it["sub"] if it.get("sub") else pack_dir
                    tgt_dir.mkdir(parents=True, exist_ok=True)
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
                            continue
                        else:
                            raise
                    n += 1
            except Exception as e:
                if it.get("optional"):
                    log(f"    optional item failed: {e}")
                else:
                    raise
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
            elif ext == ".ttf":
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
