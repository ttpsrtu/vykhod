using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Карта по реальным данным OpenStreetMap (StreamingAssets/Maps/*.json из Tools/fetch_osm.py).</summary>
    public static class OsmMapBuilder
    {
        class B { public string name, kind; public int levels; public List<Vector2> poly; public Dictionary<string, string> tags; public Vector2 c; public float area; }

        static readonly Dictionary<string, Color> roadColors = new Dictionary<string, Color>
        {
            ["primary"] = new Color(0.22f, 0.22f, 0.23f), ["secondary"] = new Color(0.25f, 0.25f, 0.26f), ["tertiary"] = new Color(0.28f, 0.28f, 0.29f),
            ["residential"] = new Color(0.3f, 0.3f, 0.31f), ["service"] = new Color(0.36f, 0.36f, 0.36f), ["footway"] = new Color(0.55f, 0.52f, 0.47f),
            ["path"] = new Color(0.5f, 0.45f, 0.35f), ["pedestrian"] = new Color(0.58f, 0.55f, 0.5f), ["steps"] = new Color(0.5f, 0.5f, 0.5f)
        };

        public static void Build(MapDef map)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Maps", map.osm + ".json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("[OSM] нет файла " + path + " — строим процедурный город. Запустите Tools/fetch_osm.py");
                ProceduralMaps.BuildCityFallback(map);
                return;
            }
            var d = J.D(Json.Parse(File.ReadAllText(path)));
            var rng = World.Rng; var root = World.Root;
            float half = World.Half;
            if (map.id == "mirea") MeshKit.Ground(root, map.size + 300, "grass", 3f, null); else MeshKit.Ground(root, map.size + 300, "paving", 2f, null);

            // --- площади: парки, вода, парковки
            int trees = 0;
            foreach (var a in J.DL(d, "areas"))
            {
                var poly = Poly(a); if (poly.Count < 3) continue;
                string kind = J.S(a, "kind");
                Material am = kind == "water" ? AssetCatalog.Mat(new Color(0.2f, 0.45f, 0.7f), 0.8f)
                    : kind == "park" || kind == "grass" ? AssetCatalog.TexMat("grass", 3f, null, new Color(0.3f, 0.55f, 0.25f), 1)
                    : kind == "forest" ? AssetCatalog.TexMat("grass", 3f, new Color(0.75f, 0.85f, 0.7f), new Color(0.2f, 0.4f, 0.2f), 0)
                    : kind == "parking" ? AssetCatalog.TexMat("asphalt", 4f, null, new Color(0.35f, 0.35f, 0.36f), 1)
                    : AssetCatalog.TexMat("gravel", 2f, null, new Color(0.5f, 0.5f, 0.45f));
                MeshKit.Slab(root, poly, 0.0f, 0.03f, am, false, "area_" + kind);
                if ((kind == "park" || kind == "forest" || kind == "grass") && trees < 900)
                {
                    int n = Mathf.Clamp((int)(Mathf.Abs(MeshKit.SignedArea(poly)) / (kind == "forest" ? 120 : 350)), 1, 120);
                    for (int i = 0; i < n && trees < 900; i++) if (Buildings.RandomInside(poly, rng, 1f, out var tp) && Mathf.Abs(tp.x) < half && Mathf.Abs(tp.y) < half) { MeshKit.Tree(root, new Vector3(tp.x, 0, tp.y), rng, kind == "forest" && rng.NextDouble() < 0.5 ? "pine" : null); trees++; }
                }
            }

            // --- дороги
            var roadPts = new List<Vector2>();
            var roadsRoot = new GameObject("Roads").transform; roadsRoot.SetParent(root, false);
            foreach (var r in J.DL(d, "roads"))
            {
                var pts = Line(r); if (pts.Count < 2) continue;
                string type = J.S(r, "type"); float w = J.F(r, "width", 5);
                Color c = roadColors.TryGetValue(type, out var cc) ? cc : new Color(0.3f, 0.3f, 0.3f);
                bool foot = type == "footway" || type == "path" || type == "pedestrian" || type == "steps";
                var rm = foot ? AssetCatalog.TexMat("paving", 1.5f, null, c) : AssetCatalog.TexMat("asphalt", 5f, null, c, type == "primary" || type == "secondary" ? 0 : 1);
                MeshKit.Strip(roadsRoot, pts, w, foot ? 0.05f : 0.04f, rm, "road_" + type);
                if (w >= 5) foreach (var p in pts) if (Mathf.Abs(p.x) < half - 5 && Mathf.Abs(p.y) < half - 5) roadPts.Add(p);
                if (w >= 4) for (int i = 0; i + 1 < pts.Count; i++) { float len = (pts[i + 1] - pts[i]).magnitude; int n = Mathf.Max(1, (int)(len / 8)); for (int k = 0; k <= n; k++) { var q = Vector2.Lerp(pts[i], pts[i + 1], (float)k / n); if (Mathf.Abs(q.x) < half - 6 && Mathf.Abs(q.y) < half - 6) World.RoadPoints.Add(q); } }
                // машины вдоль дорог
                if (w >= 6) for (int i = 0; i + 1 < pts.Count; i++)
                    {
                        float len = (pts[i + 1] - pts[i]).magnitude; int cars = (int)(len / 45f);
                        for (int k = 0; k < cars; k++) if (rng.NextDouble() < 0.5)
                            {
                                var t = (float)rng.NextDouble(); var p = Vector2.Lerp(pts[i], pts[i + 1], t); var dir = (pts[i + 1] - pts[i]).normalized; var nrm = new Vector2(-dir.y, dir.x);
                                p += nrm * (w * 0.5f - 1.3f) * (rng.NextDouble() < 0.5 ? 1 : -1);
                                if (Mathf.Abs(p.x) > half - 3 || Mathf.Abs(p.y) > half - 3) continue;
                                Car(root, new Vector3(p.x, 0, p.y), Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, rng);
                            }
                    }
            }

            // --- озеленение дворов и пустырей: деревья не ближе 7 м к дорогам и не внутри зданий (здания строятся ниже и сами вычистят место)
            var bpolys = J.DL(d, "buildings").Select(Poly).Where(bp => bp.Count >= 3).ToList();
            for (int i = 0; i < 4000 && trees < 1500; i++)
            {
                var p = new Vector2((float)rng.NextDouble() * 2 * half - half, (float)rng.NextDouble() * 2 * half - half);
                if (map.id != "mirea" && rng.NextDouble() < 0.5) continue;
                bool nearRoad = false; foreach (var rp in World.RoadPoints) if ((rp - p).sqrMagnitude < 49) { nearRoad = true; break; }
                if (nearRoad) continue;
                bool inBuilding = false; foreach (var bp in bpolys) if (MeshKit.PointInPoly(p, bp)) { inBuilding = true; break; }
                if (inBuilding) continue;
                MeshKit.Tree(root, new Vector3(p.x, 0, p.y), rng, rng.NextDouble() < 0.3 ? "pine" : null); trees++;
            }
            // --- здания
            var bl = new List<B>();
            foreach (var b in J.DL(d, "buildings"))
            {
                var poly = Poly(b); if (poly.Count < 3) continue;
                var c = MeshKit.Centroid(poly);
                if (Mathf.Abs(c.x) > half + 10 || Mathf.Abs(c.y) > half + 10) continue;
                var tags = J.SD(b, "tags");
                bl.Add(new B { name = J.S(b, "name", J.S(b, "id")), levels = Mathf.Clamp(J.I(b, "levels", 2), 1, 25), poly = poly, tags = tags, c = c, kind = Buildings.KindFromTags(tags), area = Mathf.Abs(MeshKit.SignedArea(poly)) });
            }
            // ключи этой карты → крупнейшие здания у точек интереса
            var keys = Database.Items.Values.Where(i => i.type == "key" && i.PS("map") == map.id).ToList();
            var lockedFor = new Dictionary<B, ItemDef>();
            for (int i = 0; i < keys.Count; i++)
            {
                var poi = map.pois.Count > 0 ? map.pois[Mathf.Min(i + 1, map.pois.Count - 1)] : null;
                var pp = poi != null ? new Vector2(poi.x, poi.z) : Vector2.zero;
                var best = bl.Where(x => x.area > 150 && !lockedFor.ContainsKey(x)).OrderBy(x => (x.c - pp).magnitude).FirstOrDefault();
                if (best != null) lockedFor[best] = keys[i];
            }
            int built = 0;
            foreach (var b in bl)
            {
                bool enterable = b.area > 35 && (b.area < 6000 || rng.NextDouble() < 0.7);
                lockedFor.TryGetValue(b, out var key);
                string lockedName = key != null ? key.name.Replace("Ключ от ", "") : null;
                var go = Buildings.Make(b.poly, b.levels, b.name, Buildings.ColorFor(b.name.GetHashCode()), enterable, b.kind, key?.id, lockedName, Buildings.FloorH, rng);
                if (go != null) built++;
                if (b.levels >= 4 && b.area > 200 && World.SniperSpots.Count < 12) World.SniperSpots.Add(new Vector3(b.c.x, b.levels * Buildings.FloorH + 0.5f, b.c.y));
                // мусорные баки/тела у зданий
                if (rng.NextDouble() < 0.25)
                {
                    var e = b.poly[rng.Next(b.poly.Count)]; var dirOut = (e - b.c).normalized; var p = e + dirOut * 2.5f;
                    if (Mathf.Abs(p.x) < half - 2 && Mathf.Abs(p.y) < half - 2 && !Physics.CheckSphere(new Vector3(p.x, 0.5f, p.y), 0.6f))
                        World.AddContainer(new Vector3(p.x, 0, p.y), Quaternion.Euler(0, rng.Next(0, 360), 0), rng.NextDouble() < 0.6 ? "crate" : "body");
                }
            }

            // --- заборы
            foreach (var br in J.DL(d, "barriers"))
            {
                var pts = Line(br); string type = J.S(br, "type");
                float h = type == "wall" ? 2.2f : type == "hedge" ? 1.2f : 1.5f;
                for (int i = 0; i + 1 < pts.Count; i++)
                    if (Mathf.Abs(pts[i].x) < half && Mathf.Abs(pts[i].y) < half)
                        MeshKit.Wall(root, pts[i], pts[i + 1], h, 0.15f, type == "hedge" ? new Color(0.2f, 0.4f, 0.2f) : new Color(0.4f, 0.4f, 0.4f));
            }

            // --- спавны игроков: точки дорог у края, 6 секторов по углу
            var groups = new List<Vector2>[6];
            for (int i = 0; i < 6; i++) groups[i] = new List<Vector2>();
            foreach (var p in roadPts) if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) > half * 0.72f) groups[(int)((Mathf.Atan2(p.y, p.x) + Mathf.PI) / (2 * Mathf.PI) * 6) % 6].Add(p);
            foreach (var g in groups) if (g.Count > 0) { var p = g[rng.Next(g.Count)]; World.PlayerSpawns.Add(World.Snap(new Vector3(p.x, 0, p.y))); }
            // --- спавны врагов на дорогах
            var shuffled = roadPts.OrderBy(_ => rng.Next()).Take(90).ToList();
            foreach (var p in shuffled) if (!World.PlayerSpawns.Any(s => Vector2.Distance(new Vector2(s.x, s.z), p) < 50)) World.EnemySpawns.Add(World.Snap(new Vector3(p.x, 0, p.y)));
            // босс — у первой точки интереса
            if (map.pois.Count > 0) World.BossSpawn = World.Snap(new Vector3(map.pois[0].x, 0, map.pois[0].z));
            // уличные фонари ночью
            if (World.Night) foreach (var p in roadPts.OrderBy(_ => rng.Next()).Take(40)) World.Lamp(new Vector3(p.x, 5f, p.y), new Color(1f, 0.85f, 0.6f), 18f, 1.6f);
            Debug.Log($"[OSM] {map.name}: зданий {built}, дорожных точек {roadPts.Count}, деревьев {trees}");
        }

        static List<Vector2> Poly(Dictionary<string, object> o)
        {
            var r = new List<Vector2>();
            var l = J.L(o, "poly"); if (l == null) return r;
            foreach (var p in l) if (p is List<object> xy && xy.Count >= 2) r.Add(new Vector2(J.F(xy[0]), J.F(xy[1])));
            return r;
        }
        static List<Vector2> Line(Dictionary<string, object> o)
        {
            var r = new List<Vector2>();
            var l = J.L(o, "pts"); if (l == null) return r;
            foreach (var p in l) if (p is List<object> xy && xy.Count >= 2) r.Add(new Vector2(J.F(xy[0]), J.F(xy[1])));
            return r;
        }

        public static void Car(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            string[] cars = { "sedan", "sedan-sports", "suv", "suv-luxury", "hatchback-sports", "taxi", "van", "delivery", "truck", "police", "ambulance" };
            var path = AssetCatalog.FindModel(cars[rng.Next(cars.Length)], "car-kit");
            GameObject go = path != null ? AssetCatalog.Spawn(path, parent, rng.NextDouble() < 0.2 ? 6.5f : 4.6f, true) : null;
            if (go == null)
            {
                go = new GameObject("car"); go.transform.SetParent(parent, false);
                var c = new Color((float)rng.NextDouble() * 0.6f + 0.2f, (float)rng.NextDouble() * 0.6f + 0.2f, (float)rng.NextDouble() * 0.6f + 0.2f);
                MeshKit.Box(go.transform, new Vector3(0, 0.55f, 0), new Vector3(1.8f, 0.7f, 4.4f), c);
                MeshKit.Box(go.transform, new Vector3(0, 1.15f, -0.2f), new Vector3(1.6f, 0.55f, 2.2f), c * 0.7f);
            }
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.isStatic = true;
            if (rng.NextDouble() < 0.3) World.AddContainer(pos + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, -2.6f), Quaternion.Euler(0, yaw, 0), "crate", go.transform).Title = "Багажник";
        }
    }
}
