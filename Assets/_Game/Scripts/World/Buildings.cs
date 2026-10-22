using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Здания: полый первый этаж с дверями и лутом, верхние этажи сплошные, настоящие фасады/крыши из CC0-текстур, запертые комнаты.</summary>
    public static class Buildings
    {
        public const float FloorH = 3.2f;
        static readonly Color[] palette = {
            new Color(0.98f, 0.86f, 0.55f), new Color(0.55f, 0.85f, 0.95f), new Color(0.98f, 0.6f, 0.45f), new Color(0.7f, 0.9f, 0.6f),
            new Color(0.9f, 0.7f, 0.95f), new Color(0.98f, 0.95f, 0.85f), new Color(0.6f, 0.75f, 1f), new Color(1f, 0.55f, 0.6f), new Color(0.5f, 0.9f, 0.8f), new Color(0.96f, 0.8f, 0.4f)
        };

        public static Color ColorFor(int hash) => palette[Mathf.Abs(hash) % palette.Length];

        /// <summary>Материал стен по типу здания. Фасады с окнами — для жилых/офисных, кирпич/металл — для промышленных.</summary>
        public static Material WallMat(string kind, Color tint, int hash)
        {
            var t = Color.Lerp(Color.white, tint, 0.35f);
            switch (kind)
            {
                case "industrial": case "garage": return AssetCatalog.TexMat("metal", 2.5f, Color.Lerp(Color.white, tint, 0.2f), tint, hash);
                case "theatre": case "bank": return AssetCatalog.TexMat("plaster", 3f, t, tint, hash);
                case "shop": return AssetCatalog.TexMat("facade", 6.4f, t, tint, hash + 1);
                case "residential": return AssetCatalog.TexMat(Mathf.Abs(hash) % 3 == 0 ? "bricks" : "facade", 6.4f, t, tint, hash);
                default: return AssetCatalog.TexMat("facade", 6.4f, t, tint, hash);
            }
        }
        public static Material RoofMat(int levels, int hash) => levels <= 2 ? AssetCatalog.TexMat("roofingtiles", 2.5f, null, new Color(0.5f, 0.3f, 0.25f), hash) : AssetCatalog.TexMat("concrete", 3f, new Color(0.8f, 0.8f, 0.8f), new Color(0.45f, 0.45f, 0.45f), hash);
        public static Material FloorMat(string kind, int hash) => kind == "industrial" || kind == "garage" ? AssetCatalog.TexMat("concrete", 2f, null, new Color(0.4f, 0.4f, 0.4f), hash) : kind == "residential" ? AssetCatalog.TexMat("woodplanks", 1.5f, null, new Color(0.5f, 0.35f, 0.2f), hash) : AssetCatalog.TexMat("tiles", 1.2f, null, new Color(0.6f, 0.6f, 0.6f), hash);
        public static Material Concrete => AssetCatalog.TexMat("concrete", 2.5f, null, new Color(0.5f, 0.5f, 0.5f));
        public static Material Bricks => AssetCatalog.TexMat("bricks", 2f, null, new Color(0.6f, 0.3f, 0.25f));
        public static Material Plaster => AssetCatalog.TexMat("plaster", 3f, null, new Color(0.85f, 0.85f, 0.8f));
        public static Material Tiles => AssetCatalog.TexMat("tiles", 1.2f, null, new Color(0.7f, 0.7f, 0.7f));
        public static Material Metal => AssetCatalog.TexMat("metal", 2f, null, new Color(0.55f, 0.57f, 0.6f));
        public static Material Asphalt => AssetCatalog.TexMat("asphalt", 4f, null, new Color(0.28f, 0.28f, 0.29f));

        public static string KindFromTags(Dictionary<string, string> tags)
        {
            if (tags == null) return "generic";
            string b = tags.TryGetValue("building", out var bb) ? bb : "", a = tags.TryGetValue("amenity", out var aa) ? aa : "";
            if (a == "bank") return "bank"; if (a == "pharmacy" || a == "hospital" || a == "clinic") return "medical";
            if (a == "cafe" || a == "restaurant" || a == "fast_food" || a == "bar") return "food";
            if (a == "university" || a == "college" || a == "school" || b == "university" || b == "school") return "edu";
            if (a == "theatre" || a == "cinema") return "theatre"; if (a == "police") return "police";
            if (b == "retail" || b == "commercial" || b == "supermarket" || a == "marketplace" || tags.ContainsKey("shop")) return "shop";
            if (b == "office") return "office"; if (b == "industrial" || b == "warehouse") return "industrial"; if (b == "garage" || b == "garages") return "garage";
            if (b == "apartments" || b == "residential" || b == "house" || b == "dormitory" || b == "detached") return "residential";
            return "generic";
        }

        static readonly Dictionary<string, string[]> kindLoot = new Dictionary<string, string[]>
        {
            ["bank"] = new[] { "safe", "desk", "register", "cabinet" }, ["medical"] = new[] { "medbag", "medbag", "cabinet", "desk" },
            ["food"] = new[] { "fridge", "register", "shelf", "desk" }, ["edu"] = new[] { "desk", "locker", "cabinet", "server", "bag" },
            ["theatre"] = new[] { "jacket", "cabinet", "desk", "safe" }, ["police"] = new[] { "weaponbox", "locker", "safe", "desk" },
            ["shop"] = new[] { "shelf", "shelf", "register", "crate" }, ["office"] = new[] { "desk", "desk", "server", "cabinet", "safe" },
            ["industrial"] = new[] { "crate", "toolbox", "crate", "locker" }, ["garage"] = new[] { "toolbox", "crate", "barrel" },
            ["residential"] = new[] { "cabinet", "jacket", "fridge", "bag", "desk" }, ["generic"] = new[] { "crate", "cabinet", "desk", "jacket" },
        };

        static List<Vector2> Clean(List<Vector2> poly)
        {
            var r = new List<Vector2>();
            foreach (var p in poly) if (r.Count == 0 || (r[r.Count - 1] - p).sqrMagnitude > 0.01f) r.Add(p);
            if (r.Count > 1 && (r[0] - r[r.Count - 1]).sqrMagnitude < 0.01f) r.RemoveAt(r.Count - 1);
            return r;
        }

        public static bool RandomInside(List<Vector2> poly, System.Random rng, float inset, out Vector2 p)
        {
            float minX = poly.Min(v => v.x), maxX = poly.Max(v => v.x), minZ = poly.Min(v => v.y), maxZ = poly.Max(v => v.y);
            for (int i = 0; i < 40; i++)
            {
                p = new Vector2(Mathf.Lerp(minX, maxX, (float)rng.NextDouble()), Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble()));
                if (!MeshKit.PointInPoly(p, poly)) continue;
                bool ok = true;
                for (int k = 0; k < poly.Count; k++) { if (DistToSeg(p, poly[k], poly[(k + 1) % poly.Count]) < inset) { ok = false; break; } }
                if (ok) return true;
            }
            p = MeshKit.Centroid(poly); return MeshKit.PointInPoly(p, poly);
        }

        public static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (a + ab * t - p).magnitude;
        }

        /// <summary>Строит здание. enterable — полый первый этаж с дверью; lockedKey — внутри запертая комната.</summary>
        public static GameObject Make(List<Vector2> polyIn, int levels, string name, Color wall, bool enterable, string kind = "generic",
                                      string lockedKey = null, string lockedRoom = null, float floorH = FloorH, System.Random rng = null)
        {
            rng = rng ?? World.Rng;
            var poly = Clean(polyIn);
            if (poly.Count < 3) return null;
            float area = Mathf.Abs(MeshKit.SignedArea(poly));
            if (area < 4) return null;
            levels = Mathf.Max(1, levels);
            int hash = name.GetHashCode();
            var root = new GameObject("B_" + name); root.transform.SetParent(World.Root, false);
            var t = root.transform;
            enterable = enterable && area > 35;
            float totalH = levels * floorH;
            var wallM = WallMat(kind, wall, hash); var roofM = RoofMat(levels, hash); var floorM = FloorMat(kind, hash);
            if (!enterable)
            {
                MeshKit.Slab(t, poly, 0, totalH, wallM, true, "solid");
                MeshKit.Slab(t, poly, totalH, 0.25f, roofM, true, "roof");
                return root;
            }
            MeshKit.Slab(t, poly, -0.05f, 0.15f, floorM, true, "floor");
            int n = poly.Count;
            var edgeLen = new float[n]; for (int i = 0; i < n; i++) edgeLen[i] = (poly[(i + 1) % n] - poly[i]).magnitude;
            var doorEdges = new HashSet<int>();
            var order = Enumerable.Range(0, n).OrderByDescending(i => edgeLen[i]).ToList();
            doorEdges.Add(order[0]);
            if (n > 3 && edgeLen.Sum() > 70 && order.Count > 1 && edgeLen[order[1]] > 6) doorEdges.Add(order[1]);
            if (n > 3 && edgeLen.Sum() > 160 && order.Count > 2 && edgeLen[order[2]] > 6) doorEdges.Add(order[2]);
            const float doorW = 2.0f, thick = 0.3f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                if (doorEdges.Contains(i) && edgeLen[i] > doorW + 1.5f)
                {
                    var dir = (b - a).normalized; var mid = (a + b) * 0.5f;
                    MeshKit.Wall(t, a, mid - dir * doorW * 0.5f, floorH, thick, wallM);
                    MeshKit.Wall(t, mid + dir * doorW * 0.5f, b, floorH, thick, wallM);
                    MeshKit.Box(t, new Vector3(mid.x, floorH - 0.3f, mid.y), new Vector3(thick, 0.6f, doorW + 0.2f), wallM, true, true, Quaternion.Euler(0, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0));
                }
                else MeshKit.Wall(t, a, b, floorH, thick, wallM);
            }
            // перекрытие и верхние этажи сплошным блоком с фасадом, крыша с парапетом (или скатная для малоэтажных домов)
            bool pitched = (kind == "residential" || kind == "garage" || kind == "food") && levels <= 2 && n == 4;
            MeshKit.Slab(t, poly, floorH, levels > 1 ? totalH - floorH : 0.3f, levels > 1 ? wallM : roofM, true, "upper");
            if (pitched) PitchedRoof(t, poly, totalH + (levels > 1 ? 0 : 0.3f), roofM, rng);
            else if (levels > 1) { MeshKit.Slab(t, poly, totalH, 0.2f, roofM, true, "roof"); for (int i = 0; i < n; i++) MeshKit.Wall(t, poly[i], poly[(i + 1) % n], 1.0f, 0.25f, Concrete, totalH + 0.2f); }
            // окна и двери: для кирпичных/оштукатуренных/металлических стен (фасадные текстуры уже содержат окна)
            bool texturedFacade = kind == "office" || kind == "shop" || kind == "generic" || kind == "edu" || kind == "medical" || kind == "police";
            if (!texturedFacade) Windows(t, poly, levels, floorH, kind, doorEdges, rng);
            // колонны
            if (area > 300)
            {
                int cols = Mathf.Min(12, (int)(area / 150));
                for (int i = 0; i < cols; i++) if (RandomInside(poly, rng, 3f, out var cp)) MeshKit.Box(t, new Vector3(cp.x, floorH * 0.5f, cp.y), new Vector3(0.6f, floorH, 0.6f), Concrete);
            }
            // лут внутри
            var loot = kindLoot.TryGetValue(kind, out var kl) ? kl : kindLoot["generic"];
            int count = Mathf.Clamp(Mathf.RoundToInt(area / 70f * (World.Map?.lootDensity ?? 1f)) + 1, 1, 8);
            for (int i = 0; i < count; i++)
                if (RandomInside(poly, rng, 1.2f, out var lp))
                    World.AddContainer(new Vector3(lp.x, 0.1f, lp.y), Quaternion.Euler(0, rng.Next(0, 4) * 90, 0), loot[rng.Next(loot.Length)], t);
            // мебель-декор из паков
            int furn = Mathf.Clamp((int)(area / 80), 1, 7);
            string[] furnHints = kind == "industrial" || kind == "garage" ? new[] { "barrel", "box", "workbench", "resource-planks" } : kind == "food" ? new[] { "table", "chair", "kitchen" } : new[] { "table", "chair", "bookcase", "sofa", "lamp", "plant" };
            for (int i = 0; i < furn; i++)
                if (RandomInside(poly, rng, 1.5f, out var fp))
                {
                    var hint = furnHints[rng.Next(furnHints.Length)];
                    var f = AssetCatalog.Prop(hint, kind == "industrial" || kind == "garage" ? "survival" : "furniture", t, new Vector3(fp.x, 0.1f, fp.y), rng.Next(0, 360), hint == "barrel" || hint == "chair" || hint == "lamp" || hint == "plant" ? 1.0f : 1.6f, true);
                    if (f == null) { var ph = MeshKit.Box(t, new Vector3(fp.x, 0.5f, fp.y), new Vector3(1.4f, 0.8f, 0.8f), new Color(0.5f, 0.4f, 0.3f)); ph.transform.rotation = Quaternion.Euler(0, rng.Next(0, 360), 0); }
                }
            if ((World.Night || World.Underground) && area > 60 && RandomInside(poly, rng, 2f, out var lamp)) World.Lamp(new Vector3(lamp.x, floorH - 0.4f, lamp.y), new Color(1f, 0.9f, 0.7f), 14f, 1.2f);
            if (!string.IsNullOrEmpty(lockedKey) && area > 120 && RandomInside(poly, rng, 5f, out var lc))
                LockedRoom(t, new Vector3(lc.x, 0.1f, lc.y), lockedKey, lockedRoom ?? "комната", floorH, rng, kind == "bank" ? "safe" : kind == "police" ? "weaponbox" : "safe");
            return root;
        }

        static Material glassM, frameM, doorM;
        /// <summary>Окна с рамами и двери на стенах; ночью окна слегка светятся.</summary>
        static void Windows(Transform t, List<Vector2> poly, int levels, float floorH, string kind, HashSet<int> doorEdges, System.Random rng)
        {
            if (glassM == null) { glassM = AssetCatalog.Mat(new Color(0.12f, 0.18f, 0.26f), 0.92f); frameM = AssetCatalog.Mat(new Color(0.92f, 0.92f, 0.9f), 0.3f); doorM = AssetCatalog.TexMat("woodplanks", 1f, new Color(0.55f, 0.4f, 0.3f), new Color(0.4f, 0.28f, 0.18f)); }
            var nightGlass = (World.Night || World.Underground) ? AssetCatalog.Emissive(new Color(1f, 0.85f, 0.55f), 0.9f) : null;
            int n = poly.Count; bool ccw = MeshKit.SignedArea(poly) > 0;
            bool industrial = kind == "industrial" || kind == "garage";
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n]; float len = (b - a).magnitude; if (len < 3.2f) continue;
                var dir = (b - a).normalized; var outward = new Vector2(dir.y, -dir.x) * (ccw ? 1 : -1);
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; var rot = Quaternion.Euler(0, yaw, 0);
                float ww = industrial ? 2.2f : 1.3f, wh = industrial ? 0.9f : 1.5f, spacing = industrial ? 4.5f : 3.4f;
                int count = Mathf.Max(1, (int)(len / spacing));
                for (int f = 0; f < levels; f++)
                {
                    float y = f * floorH + (industrial ? floorH - 1.2f : 1.55f);
                    for (int k = 0; k < count; k++)
                    {
                        float along = (k + 0.5f) / count * len;
                        if (f == 0 && doorEdges.Contains(i) && Mathf.Abs(along - len * 0.5f) < 2.2f) continue; // не ставим окно в дверной проём
                        if (f == 0 && !industrial && rng.NextDouble() < 0.15) continue;
                        var c = a + dir * along + outward * 0.17f; var wp = new Vector3(c.x, y, c.y);
                        MeshKit.Box(t, wp, new Vector3(0.05f, wh, ww), nightGlass != null && rng.NextDouble() < 0.45 ? nightGlass : glassM, false, true, rot);
                        MeshKit.Box(t, wp + new Vector3(outward.x, 0, outward.y) * -0.02f, new Vector3(0.04f, wh + 0.16f, ww + 0.16f), frameM, false, true, rot);
                        MeshKit.Box(t, wp + new Vector3(outward.x, 0, outward.y) * 0.04f, new Vector3(0.1f, 0.08f, ww + 0.3f), frameM, false, true, rot); // подоконник
                    }
                }
                if (industrial && !doorEdges.Contains(i) && len > 6 && rng.NextDouble() < 0.6)
                {   // роллетные ворота
                    var c = a + dir * (len * 0.5f) + outward * 0.17f;
                    MeshKit.Box(t, new Vector3(c.x, 1.4f, c.y), new Vector3(0.06f, 2.8f, 3.4f), AssetCatalog.TexMat("metal", 1f, new Color(0.6f, 0.6f, 0.65f), new Color(0.45f, 0.45f, 0.5f), 1), false, true, rot);
                }
            }
            // двери в проёмах
            foreach (var i in doorEdges)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n]; if ((b - a).magnitude < 3.5f) continue;
                var dir = (b - a).normalized; var mid = (a + b) * 0.5f; var outward = new Vector2(dir.y, -dir.x) * (ccw ? 1 : -1);
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; var rot = Quaternion.Euler(0, yaw, 0);
                var d1 = mid + dir * 0.95f - outward * 0.05f; // открытая створка сбоку проёма
                MeshKit.Box(t, new Vector3(d1.x, 1.1f, d1.y), new Vector3(0.06f, 2.2f, 0.9f), doorM, false, true, rot * Quaternion.Euler(0, 75, 0));
            }
        }

        /// <summary>Двускатная крыша для прямоугольного дома.</summary>
        static void PitchedRoof(Transform t, List<Vector2> poly, float y, Material roofM, System.Random rng)
        {
            var e0 = poly[1] - poly[0]; var e1 = poly[2] - poly[1];
            bool longFirst = e0.magnitude >= e1.magnitude;
            var along = (longFirst ? e0 : e1).normalized; var across = (longFirst ? e1 : e0);
            float L = (longFirst ? e0 : e1).magnitude + 0.8f, W = across.magnitude; across.Normalize();
            var c = MeshKit.Centroid(poly);
            float pitch = 28f; float halfW = W * 0.5f + 0.4f; float slope = halfW / Mathf.Cos(pitch * Mathf.Deg2Rad); float rise = halfW * Mathf.Tan(pitch * Mathf.Deg2Rad);
            float yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
            foreach (var s in new[] { 1f, -1f })
            {
                var mid2 = c + across * s * halfW * 0.5f; var pos = new Vector3(mid2.x, y + rise * 0.5f + 0.05f, mid2.y);
                var rot = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, 0, s * pitch);
                MeshKit.Box(t, pos, new Vector3(slope, 0.18f, L), roofM, true, true, rot);
            }
            // фронтоны
            var gableM = AssetCatalog.TexMat("plaster", 2f, null, new Color(0.85f, 0.8f, 0.7f));
            foreach (var s in new[] { 1f, -1f })
            {
                var end = c + along * s * (L - 0.8f) * 0.5f;
                for (int k = 0; k < 6; k++) { float frac = (k + 0.5f) / 6f; float w = W * (1 - frac); MeshKit.Box(t, new Vector3(end.x, y + rise * frac + 0.02f, end.y), new Vector3(0.22f, rise / 6f + 0.02f, w), gableM, true, true, Quaternion.Euler(0, yaw, 0)); }
            }
            MeshKit.Box(t, new Vector3(c.x, y + rise + 0.08f, c.y), new Vector3(0.3f, 0.2f, L), roofM, false, true, Quaternion.Euler(0, yaw, 0));
            if (rng.NextDouble() < 0.6) { var ch = c + across * 0.25f * W + along * 0.2f * L; MeshKit.Box(t, new Vector3(ch.x, y + rise * 0.8f + 0.6f, ch.y), new Vector3(0.6f, 1.6f, 0.6f), Bricks); }
        }

        /// <summary>Запертая комната 6×6 с ценным лутом.</summary>
        public static void LockedRoom(Transform parent, Vector3 center, string keyId, string title, float h, System.Random rng, string mainKind = "safe")
        {
            float s = 3f; var c = new Vector2(center.x, center.z); var wallM = Bricks;
            var a = c + new Vector2(-s, -s); var b = c + new Vector2(s, -s); var d = c + new Vector2(s, s); var e = c + new Vector2(-s, s);
            MeshKit.Wall(parent, b, d, h, 0.25f, wallM, center.y); MeshKit.Wall(parent, d, e, h, 0.25f, wallM, center.y); MeshKit.Wall(parent, e, a, h, 0.25f, wallM, center.y);
            MeshKit.Wall(parent, a, c + new Vector2(-1f, -s), h, 0.25f, wallM, center.y); MeshKit.Wall(parent, c + new Vector2(1f, -s), b, h, 0.25f, wallM, center.y);
            MeshKit.Box(parent, new Vector3(c.x, center.y + h - 0.3f, c.y - s), new Vector3(2.2f, 0.6f, 0.25f), wallM);
            World.AddDoor(new Vector3(c.x, center.y, c.y - s), 0, 2f, keyId, title, parent);
            World.AddContainer(new Vector3(c.x - 1.5f, center.y, c.y + 1.5f), Quaternion.identity, mainKind, parent);
            World.AddContainer(new Vector3(c.x + 1.5f, center.y, c.y + 1.5f), Quaternion.identity, mainKind == "safe" ? "desk" : "crate", parent);
            World.AddContainer(new Vector3(c.x + 1.5f, center.y, c.y - 1f), Quaternion.Euler(0, 90, 0), rng.NextDouble() < 0.5 ? "weaponbox" : "cabinet", parent);
            World.Lamp(new Vector3(c.x, center.y + h - 0.5f, c.y), new Color(1f, 0.6f, 0.5f), 8f, 1.5f);
        }

        public static List<Vector2> Rect(float cx, float cz, float w, float d, float yawDeg = 0)
        {
            var r = new List<Vector2>();
            float s = Mathf.Sin(yawDeg * Mathf.Deg2Rad), c = Mathf.Cos(yawDeg * Mathf.Deg2Rad);
            foreach (var (x, z) in new[] { (-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2) })
                r.Add(new Vector2(cx + x * c - z * s, cz + x * s + z * c));
            return r;
        }

        public static void Room(Transform parent, float cx, float cz, float w, float d, float h, Color wall, IEnumerable<int> doors, float doorW = 2.4f, float yBase = 0, bool ceiling = false, Color? ceilColor = null)
            => Room(parent, cx, cz, w, d, h, AssetCatalog.Mat(wall), doors, doorW, yBase, ceiling, ceilColor.HasValue ? AssetCatalog.Mat(ceilColor.Value) : null);

        /// <summary>Комната с проёмами; doors — стороны 0=юг,1=восток,2=север,3=запад.</summary>
        public static void Room(Transform parent, float cx, float cz, float w, float d, float h, Material wall, IEnumerable<int> doors, float doorW = 2.4f, float yBase = 0, bool ceiling = false, Material ceilMat = null)
        {
            var p = Rect(cx, cz, w, d);
            var ds = new HashSet<int>(doors ?? new int[0]);
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % 4];
                if (ds.Contains(i))
                {
                    var dir = (b - a).normalized; var mid = (a + b) * 0.5f;
                    MeshKit.Wall(parent, a, mid - dir * doorW * 0.5f, h, 0.3f, wall, yBase);
                    MeshKit.Wall(parent, mid + dir * doorW * 0.5f, b, h, 0.3f, wall, yBase);
                    MeshKit.Box(parent, new Vector3(mid.x, yBase + h - 0.3f, mid.y), new Vector3(0.3f, 0.6f, doorW + 0.3f), wall, true, true, Quaternion.Euler(0, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0));
                }
                else MeshKit.Wall(parent, a, b, h, 0.3f, wall, yBase);
            }
            if (ceiling) MeshKit.Slab(parent, p, yBase + h, 0.3f, ceilMat ?? Concrete, true, "ceil");
        }

        public static void Corridor(Transform parent, List<Vector2> pts, float w, float h, Color wall, bool ceiling, float yBase = 0)
            => Corridor(parent, pts, w, h, AssetCatalog.Mat(wall), ceiling, yBase);

        public static void Corridor(Transform parent, List<Vector2> pts, float w, float h, Material wall, bool ceiling, float yBase = 0)
        {
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1]; var dir = (b - a).normalized; var nrm = new Vector2(-dir.y, dir.x) * w * 0.5f;
                MeshKit.Wall(parent, a + nrm, b + nrm, h, 0.3f, wall, yBase);
                MeshKit.Wall(parent, a - nrm, b - nrm, h, 0.3f, wall, yBase);
                if (ceiling) MeshKit.Slab(parent, new List<Vector2> { a + nrm * 1.1f, b + nrm * 1.1f, b - nrm * 1.1f, a - nrm * 1.1f }, yBase + h, 0.3f, Concrete, true, "ceil");
            }
        }

        public static void Ramp(Transform parent, Vector3 a, Vector3 b, float width, Color c) => Ramp(parent, a, b, width, AssetCatalog.Mat(c));
        public static void Ramp(Transform parent, Vector3 a, Vector3 b, float width, Material m)
        {
            var mid = (a + b) * 0.5f; var dir = b - a; float len = dir.magnitude;
            var rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z).normalized, Vector3.up) * Quaternion.Euler(-Mathf.Atan2(dir.y, new Vector3(dir.x, 0, dir.z).magnitude) * Mathf.Rad2Deg, 0, 0);
            var go = MeshKit.Box(parent, mid, new Vector3(width, 0.3f, len), m, true, true, rot); go.name = "ramp";
            MeshKit.Box(parent, mid + rot * new Vector3(width / 2, 0.6f, 0), new Vector3(0.1f, 1.0f, len), Metal, true, true, rot);
            MeshKit.Box(parent, mid + rot * new Vector3(-width / 2, 0.6f, 0), new Vector3(0.1f, 1.0f, len), Metal, true, true, rot);
        }
    }
}
