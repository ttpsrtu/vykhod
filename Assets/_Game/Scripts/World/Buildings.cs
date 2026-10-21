using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Здания: полый первый этаж с дверями и лутом, верхние этажи сплошные, запертые комнаты по ключам.</summary>
    public static class Buildings
    {
        public const float FloorH = 3.2f;
        static readonly Color[] palette = {
            new Color(0.85f, 0.8f, 0.7f), new Color(0.75f, 0.72f, 0.68f), new Color(0.62f, 0.55f, 0.5f), new Color(0.8f, 0.65f, 0.5f),
            new Color(0.7f, 0.75f, 0.8f), new Color(0.55f, 0.6f, 0.62f), new Color(0.88f, 0.86f, 0.8f), new Color(0.65f, 0.4f, 0.35f), new Color(0.5f, 0.55f, 0.45f)
        };

        public static Color ColorFor(int hash) => palette[Mathf.Abs(hash) % palette.Length];

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

        /// <summary>Случайная точка внутри полигона на расстоянии ≥ inset от края.</summary>
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

        /// <summary>
        /// Строит здание. enterable — полый первый этаж с дверью; lockedKey — внутри запертая комната.
        /// </summary>
        public static GameObject Make(List<Vector2> polyIn, int levels, string name, Color wall, bool enterable, string kind = "generic",
                                      string lockedKey = null, string lockedRoom = null, float floorH = FloorH, System.Random rng = null)
        {
            rng = rng ?? World.Rng;
            var poly = Clean(polyIn);
            if (poly.Count < 3) return null;
            float area = Mathf.Abs(MeshKit.SignedArea(poly));
            if (area < 4) return null;
            levels = Mathf.Max(1, levels);
            var root = new GameObject("B_" + name); root.transform.SetParent(World.Root, false);
            var t = root.transform;
            enterable = enterable && area > 35;
            float totalH = levels * floorH;
            Color roofC = wall * 0.55f; roofC.a = 1;
            if (!enterable)
            {
                MeshKit.Slab(t, poly, 0, totalH, wall, true, "solid");
                return root;
            }
            // пол
            MeshKit.Slab(t, poly, -0.05f, 0.15f, new Color(0.35f, 0.33f, 0.3f), true, "floor");
            // стены первого этажа с дверными проёмами
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
                    MeshKit.Wall(t, a, mid - dir * doorW * 0.5f, floorH, thick, wall);
                    MeshKit.Wall(t, mid + dir * doorW * 0.5f, b, floorH, thick, wall);
                    MeshKit.Box(t, new Vector3(mid.x, floorH - 0.3f, mid.y), new Vector3(thick, 0.6f, doorW + 0.2f), wall, true, true, Quaternion.Euler(0, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0)); // перемычка
                }
                else MeshKit.Wall(t, a, b, floorH, thick, wall);
                // окна-полосы на верхних этажах (декор, без коллайдера)
                if (levels > 1 && edgeLen[i] > 4)
                {
                    var dir = (b - a).normalized; var nrm = new Vector2(-dir.y, dir.x) * 0.02f;
                    bool ccw = MeshKit.SignedArea(poly) > 0; if (!ccw) nrm = -nrm;
                    for (int f = 1; f < Mathf.Min(levels, 12); f++)
                    {
                        var mid = (a + b) * 0.5f - nrm * 10;
                        MeshKit.Box(t, new Vector3(mid.x, f * floorH + floorH * 0.5f, mid.y), new Vector3(0.05f, floorH * 0.4f, edgeLen[i] * 0.85f), new Color(0.25f, 0.35f, 0.45f), false, true, Quaternion.Euler(0, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0));
                    }
                }
            }
            // перекрытие + верхние этажи сплошным блоком, крыша с парапетом
            MeshKit.Slab(t, poly, floorH, levels > 1 ? totalH - floorH : 0.3f, levels > 1 ? wall : roofC, true, "upper");
            if (levels > 1) { MeshKit.Slab(t, poly, totalH, 0.2f, roofC, true, "roof"); for (int i = 0; i < n; i++) MeshKit.Wall(t, poly[i], poly[(i + 1) % n], 1.0f, 0.25f, roofC, totalH + 0.2f); }
            // колонны в больших помещениях
            if (area > 300)
            {
                int cols = Mathf.Min(12, (int)(area / 150));
                for (int i = 0; i < cols; i++) if (RandomInside(poly, rng, 3f, out var cp)) MeshKit.Box(t, new Vector3(cp.x, floorH * 0.5f, cp.y), new Vector3(0.6f, floorH, 0.6f), wall * 0.8f);
            }
            // лут внутри
            var loot = kindLoot.TryGetValue(kind, out var kl) ? kl : kindLoot["generic"];
            int count = Mathf.Clamp(Mathf.RoundToInt(area / 70f * (World.Map?.lootDensity ?? 1f)) + 1, 1, 8);
            for (int i = 0; i < count; i++)
                if (RandomInside(poly, rng, 1.2f, out var lp))
                    World.AddContainer(new Vector3(lp.x, 0.1f, lp.y), Quaternion.Euler(0, rng.Next(0, 4) * 90, 0), loot[rng.Next(loot.Length)], t);
            // мебель-декор
            int furn = Mathf.Clamp((int)(area / 90), 0, 6);
            for (int i = 0; i < furn; i++)
                if (RandomInside(poly, rng, 1.5f, out var fp))
                {
                    var f = AssetCatalog.SpawnOrPlaceholder(rng.NextDouble() < 0.5 ? "table" : "chair", t, 1.2f, new Color(0.5f, 0.4f, 0.3f), PrimitiveType.Cube, "furniture", true);
                    f.transform.position = new Vector3(fp.x, 0.1f, fp.y); f.transform.rotation = Quaternion.Euler(0, rng.Next(0, 360), 0);
                    if (f.name.StartsWith("ph_")) { f.transform.localScale = new Vector3(1.4f, 0.8f, 0.8f); f.transform.position += Vector3.up * 0.4f; }
                    f.isStatic = true;
                }
            // лампы в темноте
            if ((World.Night || World.Underground) && area > 60 && RandomInside(poly, rng, 2f, out var lamp)) World.Lamp(new Vector3(lamp.x, floorH - 0.4f, lamp.y), new Color(1f, 0.9f, 0.7f), 14f, 1.2f);
            // запертая комната
            if (!string.IsNullOrEmpty(lockedKey) && area > 120 && RandomInside(poly, rng, 5f, out var lc))
                LockedRoom(t, new Vector3(lc.x, 0.1f, lc.y), lockedKey, lockedRoom ?? "комната", floorH, rng, kind == "bank" ? "safe" : kind == "police" ? "weaponbox" : "safe");
            return root;
        }

        /// <summary>Запертая комната 6×6 с ценным лутом.</summary>
        public static void LockedRoom(Transform parent, Vector3 center, string keyId, string title, float h, System.Random rng, string mainKind = "safe")
        {
            float s = 3f; var c = new Vector2(center.x, center.z);
            var wallC = new Color(0.5f, 0.3f, 0.3f);
            var a = c + new Vector2(-s, -s); var b = c + new Vector2(s, -s); var d = c + new Vector2(s, s); var e = c + new Vector2(-s, s);
            MeshKit.Wall(parent, b, d, h, 0.25f, wallC); MeshKit.Wall(parent, d, e, h, 0.25f, wallC); MeshKit.Wall(parent, e, a, h, 0.25f, wallC);
            // передняя стена с дверью
            MeshKit.Wall(parent, a, c + new Vector2(-1f, -s), h, 0.25f, wallC); MeshKit.Wall(parent, c + new Vector2(1f, -s), b, h, 0.25f, wallC);
            MeshKit.Box(parent, new Vector3(c.x, h - 0.3f, c.y - s), new Vector3(2.2f, 0.6f, 0.25f), wallC);
            World.AddDoor(new Vector3(c.x, center.y, c.y - s), 0, 2f, keyId, title, parent);
            World.AddContainer(new Vector3(c.x - 1.5f, center.y, c.y + 1.5f), Quaternion.identity, mainKind, parent);
            World.AddContainer(new Vector3(c.x + 1.5f, center.y, c.y + 1.5f), Quaternion.identity, mainKind == "safe" ? "desk" : "crate", parent);
            World.AddContainer(new Vector3(c.x + 1.5f, center.y, c.y - 1f), Quaternion.Euler(0, 90, 0), rng.NextDouble() < 0.5 ? "weaponbox" : "cabinet", parent);
            World.Lamp(new Vector3(c.x, center.y + h - 0.5f, c.y), new Color(1f, 0.6f, 0.5f), 8f, 1.5f);
        }

        /// <summary>Прямоугольник как полигон.</summary>
        public static List<Vector2> Rect(float cx, float cz, float w, float d, float yawDeg = 0)
        {
            var r = new List<Vector2>();
            float s = Mathf.Sin(yawDeg * Mathf.Deg2Rad), c = Mathf.Cos(yawDeg * Mathf.Deg2Rad);
            foreach (var (x, z) in new[] { (-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2) })
                r.Add(new Vector2(cx + x * c - z * s, cz + x * s + z * c));
            return r;
        }

        /// <summary>Комната с проёмами (для ТЦ/метро): стены вокруг прямоугольника; doors — стороны 0=юг,1=восток,2=север,3=запад.</summary>
        public static void Room(Transform parent, float cx, float cz, float w, float d, float h, Color wall, IEnumerable<int> doors, float doorW = 2.4f, float yBase = 0, bool ceiling = false, Color? ceilColor = null)
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
            if (ceiling) MeshKit.Slab(parent, p, yBase + h, 0.3f, ceilColor ?? wall * 0.6f, true, "ceil");
        }

        /// <summary>Коридор вдоль ломаной шириной w: две стены + потолок.</summary>
        public static void Corridor(Transform parent, List<Vector2> pts, float w, float h, Color wall, bool ceiling, float yBase = 0)
        {
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1]; var dir = (b - a).normalized; var nrm = new Vector2(-dir.y, dir.x) * w * 0.5f;
                MeshKit.Wall(parent, a + nrm, b + nrm, h, 0.3f, wall, yBase);
                MeshKit.Wall(parent, a - nrm, b - nrm, h, 0.3f, wall, yBase);
                if (ceiling) MeshKit.Slab(parent, new List<Vector2> { a + nrm * 1.1f, b + nrm * 1.1f, b - nrm * 1.1f, a - nrm * 1.1f }, yBase + h, 0.3f, wall * 0.5f, true, "ceil");
            }
        }

        /// <summary>Пандус (наклонная плита) от a (низ) к b (верх).</summary>
        public static void Ramp(Transform parent, Vector3 a, Vector3 b, float width, Color c)
        {
            var mid = (a + b) * 0.5f; var dir = b - a; float len = dir.magnitude;
            var rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z).normalized, Vector3.up) * Quaternion.Euler(-Mathf.Atan2(dir.y, new Vector3(dir.x, 0, dir.z).magnitude) * Mathf.Rad2Deg, 0, 0);
            var go = MeshKit.Box(parent, mid, new Vector3(width, 0.3f, len), c, true, true, rot);
            go.name = "ramp";
            // перила
            MeshKit.Box(parent, mid + rot * new Vector3(width / 2, 0.6f, 0), new Vector3(0.1f, 1.0f, len), c * 0.7f, true, true, rot);
            MeshKit.Box(parent, mid + rot * new Vector3(-width / 2, 0.6f, 0), new Vector3(0.1f, 1.0f, len), c * 0.7f, true, true, rot);
        }
    }
}
