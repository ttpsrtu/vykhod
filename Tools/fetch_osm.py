#!/usr/bin/env python
"""
fetch_osm.py - pull OSM data for the game maps via Overpass (mirrors, retries) with a
fallback to the OSM 0.6 /map API, then convert into compact game-ready JSON.

    python -I Tools/fetch_osm.py                 # both maps
    python -I Tools/fetch_osm.py --only mirea
    python -I Tools/fetch_osm.py --no-fetch      # reconvert from Tools/osm_cache/*_raw.json

Output: Assets/StreamingAssets/Maps/<name>.json
Raw Overpass/OSM data is cached in Tools/osm_cache/<name>_raw.json (not shipped).

Game JSON:
{
  "name", "center":{"lat","lon"}, "bbox":{"south","west","north","east"}, "scale_m_per_unit":1,
  "buildings":[{"id","name","levels","tags":{...},"poly":[[x,z],...]}],
  "roads":[{"id","type","name","width","pts":[[x,z],...]}],
  "areas":[{"id","kind":"park|water|grass|forest|parking|other","poly":[[x,z],...]}],
  "barriers":[{"id","type","pts":[[x,z],...]}],
  "lines":[{"id","kind":"railway|waterway","type","name","pts":[[x,z],...]}]   # extra: linear rails/streams
}
x = metres east of center, z = metres north of center (equirectangular).  Polygons do NOT repeat the first point.
"""
import argparse
import json
import math
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

import requests

PROJECT = Path(r"C:\Users\я\Projects\Vykhod")
OUT_DIR = PROJECT / "Assets" / "StreamingAssets" / "Maps"
CACHE_DIR = PROJECT / "Tools" / "osm_cache"

UA = {"User-Agent": "VykhodMapFetcher/1.0 (student game project; python-requests)"}

MIRRORS = [
    "https://overpass.kumi.systems/api/interpreter",
    "https://lz4.overpass-api.de/api/interpreter",
    "https://z.overpass-api.de/api/interpreter",
    "https://overpass.private.coffee/api/interpreter",
    "https://overpass-api.de/api/interpreter",
]
OSM_API = "https://api.openstreetmap.org/api/0.6/map"

MAPS = [
    dict(name="mirea", title="RTU MIREA campus, prospekt Vernadskogo 78, Moscow",
         center=(55.6696, 37.4803), bbox=(55.6665, 37.4745, 55.6728, 37.4862)),   # S, W, N, E
    dict(name="donetsk", title="Donetsk centre, Lenin Square / Artyoma street",
         center=(48.0023, 37.8050), bbox=(47.9975, 37.7980, 48.0070, 37.8120)),
]

QUERY_TMPL = """[out:json][timeout:180];
(
  way[building]({b});
  way[highway]({b});
  way[natural]({b});
  way[landuse]({b});
  way[leisure]({b});
  way[amenity]({b});
  way[barrier]({b});
  way[waterway]({b});
  way[railway]({b});
  relation[building]({b});
);
(._;>;);
out body;"""

ROAD_WIDTH = {
    "motorway": 12, "motorway_link": 12, "trunk": 12, "trunk_link": 12,
    "primary": 12, "primary_link": 12,
    "secondary": 10, "secondary_link": 10,
    "tertiary": 8, "tertiary_link": 8,
    "residential": 6, "unclassified": 6, "living_street": 6,
    "service": 4, "track": 3,
    "footway": 2, "path": 2, "pedestrian": 3, "cycleway": 2, "steps": 2, "bridleway": 2, "corridor": 2,
}
ROAD_DEFAULT = 5.0
BUILDING_TAG_SUBSET = ("building", "name", "amenity", "addr:street", "addr:housenumber", "building:levels", "height", "shop", "office")


def log(*a):
    print(*a, flush=True)


# ----------------------------------------------------------------------------------------------
# Fetching
# ----------------------------------------------------------------------------------------------
def fetch_overpass(bbox):
    s, w, n, e = bbox
    q = QUERY_TMPL.format(b=f"{s},{w},{n},{e}")
    delays = [0, 8, 20]
    for attempt, delay in enumerate(delays):
        if delay:
            log(f"  retry round {attempt + 1} after {delay}s")
            time.sleep(delay)
        for m in MIRRORS:
            t = time.time()
            try:
                r = requests.post(m, data={"data": q}, headers=UA, timeout=180)
                dt = time.time() - t
                if r.status_code == 200:
                    try:
                        data = r.json()
                    except ValueError:
                        log(f"  {m}: 200 but not JSON ({dt:.0f}s)")
                        continue
                    if "elements" in data:
                        if data.get("remark") and "runtime error" in data["remark"]:
                            log(f"  {m}: remark {data['remark']}")
                            continue
                        log(f"  {m}: OK {len(data['elements'])} elements in {dt:.0f}s")
                        return data, m
                    log(f"  {m}: 200 but no elements key")
                else:
                    log(f"  {m}: HTTP {r.status_code} ({dt:.0f}s) {r.text[:60]!r}")
            except Exception as ex:
                log(f"  {m}: {type(ex).__name__}: {str(ex)[:80]}")
    return None, None


def fetch_osm_api(bbox):
    """Fallback: whole bbox via /api/0.6/map (XML) -> Overpass-like element list."""
    s, w, n, e = bbox
    area = (n - s) * (e - w)
    if area >= 0.25:
        raise RuntimeError("bbox too large for /map")
    for attempt in range(3):
        t = time.time()
        try:
            r = requests.get(OSM_API, params={"bbox": f"{w},{s},{e},{n}"}, headers=UA, timeout=300)
            if r.status_code == 200:
                log(f"  osm api: OK {len(r.content) // 1024} KB in {time.time() - t:.0f}s")
                return xml_to_elements(r.content), OSM_API
            log(f"  osm api: HTTP {r.status_code} {r.text[:100]!r}")
        except Exception as ex:
            log(f"  osm api: {type(ex).__name__}: {str(ex)[:80]}")
        time.sleep(10 * (attempt + 1))
    raise RuntimeError("OSM API fallback failed")


def xml_to_elements(xml_bytes):
    root = ET.fromstring(xml_bytes)
    elements = []
    for el in root:
        if el.tag not in ("node", "way", "relation"):
            continue
        d = {"type": el.tag, "id": int(el.get("id"))}
        if el.tag == "node":
            d["lat"] = float(el.get("lat"))
            d["lon"] = float(el.get("lon"))
        tags = {t.get("k"): t.get("v") for t in el.findall("tag")}
        if tags:
            d["tags"] = tags
        if el.tag == "way":
            d["nodes"] = [int(nd.get("ref")) for nd in el.findall("nd")]
        if el.tag == "relation":
            d["members"] = [{"type": mm.get("type"), "ref": int(mm.get("ref")), "role": mm.get("role", "")}
                            for mm in el.findall("member")]
        elements.append(d)
    return {"elements": elements, "source": "osm-api-0.6-map"}


# ----------------------------------------------------------------------------------------------
# Conversion
# ----------------------------------------------------------------------------------------------
def projector(center):
    lat0, lon0 = center
    kx = math.cos(math.radians(lat0)) * 111320.0
    kz = 110574.0

    def proj(lat, lon):
        return [round((lon - lon0) * kx, 2), round((lat - lat0) * kz, 2)]
    return proj


def parse_levels(tags):
    v = tags.get("building:levels")
    if v:
        try:
            lv = int(float(v.replace(",", ".").split(";")[0]))
            if lv > 0:
                return min(lv, 80)
        except ValueError:
            pass
    h = tags.get("height") or tags.get("building:height")
    if h:
        try:
            hv = float(h.lower().replace("m", "").replace(",", ".").strip().split(" ")[0])
            if hv > 0:
                return max(1, min(80, int(round(hv / 3.0))))
        except ValueError:
            pass
    return 2


def area_kind(tags):
    nat, lu, le, am, ww = tags.get("natural"), tags.get("landuse"), tags.get("leisure"), tags.get("amenity"), tags.get("waterway")
    if nat == "water" or lu in ("reservoir", "basin", "pond") or ww in ("riverbank", "dock") or le == "swimming_pool":
        return "water"
    if le in ("park", "garden", "nature_reserve", "playground", "dog_park") or lu == "recreation_ground":
        return "park"
    if nat in ("wood", "scrub", "tree_row") or lu in ("forest", "orchard"):
        return "forest"
    if nat in ("grassland", "heath", "meadow") or lu in ("grass", "meadow", "village_green", "greenfield", "cemetery", "flowerbed", "farmland") or le in ("pitch", "golf_course", "sports_centre", "stadium", "track"):
        return "grass"
    if am == "parking" or lu == "garages":
        return "parking"
    return "other"


def stitch_rings(segments):
    """Join open way segments (lists of node ids) into closed rings. Returns list of node-id rings."""
    segs = [list(s) for s in segments if len(s) >= 2]
    rings = []
    while segs:
        cur = segs.pop(0)
        changed = True
        while changed and cur[0] != cur[-1]:
            changed = False
            for i, s in enumerate(segs):
                if s[0] == cur[-1]:
                    cur += s[1:]
                elif s[-1] == cur[-1]:
                    cur += list(reversed(s))[1:]
                elif s[-1] == cur[0]:
                    cur = s[:-1] + cur
                elif s[0] == cur[0]:
                    cur = list(reversed(s))[:-1] + cur
                else:
                    continue
                segs.pop(i)
                changed = True
                break
        rings.append(cur)
    return rings


def to_poly(ids, nodes, proj):
    if len(ids) >= 2 and ids[0] == ids[-1]:
        ids = ids[:-1]
    pts = []
    for i in ids:
        n = nodes.get(i)
        if n is None:
            continue
        p = proj(n[0], n[1])
        if not pts or p != pts[-1]:
            pts.append(p)
    return pts


def convert(raw, mapdef):
    proj = projector(mapdef["center"])
    nodes, ways, rels = {}, {}, []
    for el in raw["elements"]:
        t = el["type"]
        if t == "node":
            nodes[el["id"]] = (el["lat"], el["lon"])
        elif t == "way":
            ways[el["id"]] = el
        elif t == "relation":
            rels.append(el)

    buildings, roads, areas, barriers, lines = [], [], [], [], []
    used_as_outer = set()

    # building multipolygon relations -> take outer rings
    for rel in rels:
        tags = rel.get("tags", {})
        if "building" not in tags:
            continue
        outer_segs = []
        for m in rel.get("members", []):
            if m["type"] == "way" and m.get("role", "outer") in ("outer", ""):
                w = ways.get(m["ref"])
                if w:
                    outer_segs.append(w["nodes"])
                    used_as_outer.add(m["ref"])
        for k, ring in enumerate(stitch_rings(outer_segs)):
            poly = to_poly(ring, nodes, proj)
            if len(poly) >= 3:
                buildings.append(make_building(f"r{rel['id']}" + (f"_{k}" if k else ""), tags, poly))

    for wid, w in ways.items():
        tags = w.get("tags", {})
        if not tags:
            continue
        ids = w["nodes"]
        closed = len(ids) >= 4 and ids[0] == ids[-1]
        if "building" in tags and tags["building"] != "no":
            if wid in used_as_outer:
                continue
            poly = to_poly(ids, nodes, proj)
            if len(poly) >= 3:
                buildings.append(make_building(str(wid), tags, poly))
            continue
        if "highway" in tags:
            if tags.get("area") == "yes" and closed:
                poly = to_poly(ids, nodes, proj)
                if len(poly) >= 3:
                    areas.append({"id": str(wid), "kind": "other", "poly": poly, "tags": {"highway": tags["highway"]}})
                continue
            pts = to_poly(ids, nodes, proj) if not closed else [proj(*nodes[i]) for i in ids if i in nodes]
            if len(pts) >= 2:
                hw = tags["highway"]
                road = {"id": str(wid), "type": hw, "name": tags.get("name", ""),
                        "width": float(ROAD_WIDTH.get(hw, ROAD_DEFAULT)), "pts": pts}
                if "lanes" in tags:
                    road["lanes"] = tags["lanes"]
                if tags.get("oneway") == "yes":
                    road["oneway"] = True
                if tags.get("bridge") == "yes":
                    road["bridge"] = True
                if tags.get("tunnel") == "yes":
                    road["tunnel"] = True
                roads.append(road)
            continue
        if "barrier" in tags:
            pts = [proj(*nodes[i]) for i in ids if i in nodes]
            if len(pts) >= 2:
                barriers.append({"id": str(wid), "type": tags["barrier"], "pts": pts})
            continue
        if "railway" in tags and not closed:
            pts = [proj(*nodes[i]) for i in ids if i in nodes]
            if len(pts) >= 2:
                lines.append({"id": str(wid), "kind": "railway", "type": tags["railway"], "name": tags.get("name", ""), "pts": pts})
            continue
        if "waterway" in tags and not closed:
            pts = [proj(*nodes[i]) for i in ids if i in nodes]
            if len(pts) >= 2:
                lines.append({"id": str(wid), "kind": "waterway", "type": tags["waterway"], "name": tags.get("name", ""), "pts": pts})
            continue
        if closed and any(k in tags for k in ("natural", "landuse", "leisure", "amenity", "waterway", "railway")):
            poly = to_poly(ids, nodes, proj)
            if len(poly) >= 3:
                sub = {k: tags[k] for k in ("natural", "landuse", "leisure", "amenity", "name") if k in tags}
                areas.append({"id": str(wid), "kind": area_kind(tags), "poly": poly, "tags": sub})
            continue
        if not closed and "natural" in tags:  # tree rows, cliffs etc
            pts = [proj(*nodes[i]) for i in ids if i in nodes]
            if len(pts) >= 2:
                barriers.append({"id": str(wid), "type": "natural:" + tags["natural"], "pts": pts})

    s, w_, n, e = mapdef["bbox"]
    out = {
        "name": mapdef["name"],
        "title": mapdef["title"],
        "center": {"lat": mapdef["center"][0], "lon": mapdef["center"][1]},
        "bbox": {"south": s, "west": w_, "north": n, "east": e},
        "size_m": {"x": round((e - w_) * math.cos(math.radians(mapdef["center"][0])) * 111320.0, 1),
                   "z": round((n - s) * 110574.0, 1)},
        "scale_m_per_unit": 1,
        "buildings": buildings, "roads": roads, "areas": areas, "barriers": barriers, "lines": lines,
    }
    return out


def make_building(bid, tags, poly):
    b = {"id": bid, "levels": parse_levels(tags),
         "tags": {k: tags[k] for k in BUILDING_TAG_SUBSET if k in tags}, "poly": poly}
    if tags.get("name"):
        b["name"] = tags["name"]
    return b


# ----------------------------------------------------------------------------------------------
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--no-fetch", action="store_true", help="reconvert from cache only")
    args = ap.parse_args()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    summary = []
    for mapdef in MAPS:
        if args.only and mapdef["name"] not in args.only:
            continue
        name = mapdef["name"]
        cache = CACHE_DIR / f"{name}_raw.json"
        log(f"== {name} bbox={mapdef['bbox']} ==")
        raw, src = None, None
        if args.no_fetch:
            if not cache.exists():
                log(f"  no cache at {cache}")
                summary.append((name, None))
                continue
            raw = json.loads(cache.read_text(encoding="utf-8"))
            src = "cache"
        else:
            raw, src = fetch_overpass(mapdef["bbox"])
            if raw is None:
                log("  all Overpass mirrors failed -> OSM API /map fallback")
                try:
                    raw, src = fetch_osm_api(mapdef["bbox"])
                except Exception as ex:
                    log(f"  fallback failed: {ex}")
                    if cache.exists():
                        log("  using cached raw data")
                        raw = json.loads(cache.read_text(encoding="utf-8"))
                        src = "cache"
            if raw is not None and src != "cache":
                cache.write_text(json.dumps(raw), encoding="utf-8")
        if raw is None:
            log(f"  {name}: FAILED, no data")
            summary.append((name, None))
            continue
        game = convert(raw, mapdef)
        game["source"] = src
        out = OUT_DIR / f"{name}.json"
        out.write_text(json.dumps(game, separators=(",", ":"), ensure_ascii=False), encoding="utf-8")
        counts = {k: len(game[k]) for k in ("buildings", "roads", "areas", "barriers", "lines")}
        log(f"  {name}: {counts}  ({out.stat().st_size // 1024} KB) -> {out}")
        summary.append((name, counts))
    log("\n==== SUMMARY ====")
    for name, c in summary:
        log(f"{name}: {c}")
    if any(c is None for _, c in summary):
        sys.exit(1)


if __name__ == "__main__":
    main()
