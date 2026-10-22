using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Текущий построенный мир рейда. Детерминирован по (карта, сид) — у всех клиентов одинаковый.</summary>
    public static class World
    {
        public static bool IsReady;
        public static Transform Root;
        public static MapDef Map;
        public static int Seed;
        public static System.Random Rng;
        public static readonly List<LootContainer> Containers = new List<LootContainer>();
        public static readonly List<LockedDoor> Doors = new List<LockedDoor>();
        public static readonly List<ExtractZone> Extracts = new List<ExtractZone>();
        public static readonly List<PoiMarker> Pois = new List<PoiMarker>();
        public static readonly List<Vector3> PlayerSpawns = new List<Vector3>();
        public static readonly List<Vector3> EnemySpawns = new List<Vector3>();
        public static readonly List<Vector3> SniperSpots = new List<Vector3>();
        public static readonly List<Vector2> RoadPoints = new List<Vector2>();   // точки дорог (XZ) — для логичной расстановки укрытий
        public static Vector3 BossSpawn;
        public static bool Night;
        public static bool ForceDay;   // для автотестов/скриншотов
        public static bool Underground;
        public static float Half => Map != null ? Map.size * 0.5f : 250f;
        public static Light Sun;
        static readonly List<Light> lamps = new List<Light>();

        public static void Clear()
        {
            IsReady = false;
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = null; Map = null;
            Containers.Clear(); Doors.Clear(); Extracts.Clear(); Pois.Clear(); PlayerSpawns.Clear(); EnemySpawns.Clear(); SniperSpots.Clear(); RoadPoints.Clear(); lamps.Clear();
            Underground = false;
        }

        public static void Build(MapDef map, int seed, bool isServer)
        {
            Clear();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Map = map; Seed = seed; Rng = new System.Random(seed);
            Night = !ForceDay && new System.Random(seed).NextDouble() < 0.15;
            Root = new GameObject("World_" + map.id).transform;
            Underground = map.kind == "metro";
            SetupLighting();
            switch (map.kind)
            {
                case "osm": OsmMapBuilder.Build(map); break;
                case "industrial": ProceduralMaps.BuildIndustrial(); break;
                case "suburb": ProceduralMaps.BuildSuburb(); break;
                case "mall": ProceduralMaps.BuildMall(); break;
                case "metro": ProceduralMaps.BuildMetro(); break;
                default: ProceduralMaps.BuildIndustrial(); break;
            }
            Physics.SyncTransforms();
            MapDetails.Decorate(map.kind == "metro" ? 0.6f : map.kind == "mall" ? 0.5f : 1.1f, !Underground);
            Bounds();
            PlaceExtracts();
            PlacePois();
            if (PlayerSpawns.Count == 0) DefaultSpawns();
            // спавн не должен совпадать с выходом — сдвигаем к центру карты
            for (int i = 0; i < PlayerSpawns.Count; i++)
                foreach (var e in Extracts)
                    if (Vector3.Distance(PlayerSpawns[i], e.transform.position) < 18f) { var dir = (-PlayerSpawns[i]); dir.y = 0; PlayerSpawns[i] = Snap(PlayerSpawns[i] + dir.normalized * 30f); }
            if (EnemySpawns.Count == 0) RandomEnemySpawns(60);
            if (BossSpawn == Vector3.zero && Map.pois.Count > 0) BossSpawn = new Vector3(Map.pois[0].x, 0, Map.pois[0].z);
            Physics.SyncTransforms();
            if (isServer) BakeNavMesh();
            StaticBatchingUtility.Combine(Root.gameObject);
            IsReady = true;
            Debug.Log($"[World] {map.name}: построено за {sw.ElapsedMilliseconds} мс, контейнеров {Containers.Count}, дверей {Doors.Count}, спавнов {PlayerSpawns.Count}/{EnemySpawns.Count}");
        }

        static void SetupLighting()
        {
            var lgo = new GameObject("Sun"); lgo.transform.SetParent(Root, false);
            Sun = lgo.AddComponent<Light>(); Sun.type = LightType.Directional;
            Sun.shadows = Game.Settings.quality > 0 ? LightShadows.Soft : LightShadows.None;
            Sun.shadowStrength = 0.7f;
            if (Underground)
            {
                Sun.intensity = 0.05f; Sun.color = new Color(0.6f, 0.65f, 0.8f); lgo.transform.rotation = Quaternion.Euler(80, 0, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.08f, 0.08f, 0.1f);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.03f; RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.03f);
                Camera.main?.gameObject.SetActive(true);
            }
            else if (Night)
            {
                // «синий час»: ночь читаемая и цветная, а не чёрная
                Sun.intensity = 0.7f; Sun.color = new Color(0.65f, 0.75f, 1f); lgo.transform.rotation = Quaternion.Euler(35, 120, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.4f, 0.45f, 0.8f); RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.5f); RenderSettings.ambientGroundColor = new Color(0.15f, 0.15f, 0.22f);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 120; RenderSettings.fogEndDistance = 600; RenderSettings.fogColor = new Color(0.08f, 0.1f, 0.25f);
            }
            else
            {
                Sun.intensity = 1.35f; Sun.color = new Color(1f, 0.97f, 0.9f); lgo.transform.rotation = Quaternion.Euler(48, 35, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.65f, 0.8f, 1f); RenderSettings.ambientEquatorColor = new Color(0.6f, 0.62f, 0.7f); RenderSettings.ambientGroundColor = new Color(0.35f, 0.4f, 0.3f);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 200; RenderSettings.fogEndDistance = 900; RenderSettings.fogColor = new Color(0.62f, 0.8f, 1f);
            }
            // небо: HDRI-панорама (создаётся редакторным скриптом в Resources/Materials/Sky), ночью приглушаем
            var sky = Underground ? null : Resources.Load<Material>("Materials/Sky");
            RenderSettings.skybox = sky;
            if (sky != null)
            {
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", Night ? 0.12f : 1.0f);
                if (sky.HasProperty("_Tint")) sky.SetColor("_Tint", Night ? new Color(0.35f, 0.4f, 0.7f) : new Color(0.5f, 0.5f, 0.5f));
                if (sky.HasProperty("_Rotation")) sky.SetFloat("_Rotation", 215f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox; RenderSettings.ambientIntensity = Night ? 0.6f : 1.1f;
                DynamicGI.UpdateEnvironment();
            }
        }

        public static Color SkyColor => Underground ? new Color(0.01f, 0.01f, 0.015f) : Night ? new Color(0.1f, 0.12f, 0.32f) : new Color(0.42f, 0.74f, 1f);

        /// <summary>Точечный свет (ограниченно, ради производительности).</summary>
        public static void Lamp(Vector3 pos, Color c, float range = 12f, float intensity = 1.5f)
        {
            if (lamps.Count > 48 && !Underground) return;
            if (lamps.Count > 90) return;
            var go = new GameObject("lamp"); go.transform.SetParent(Root, false); go.transform.position = pos;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = c; l.shadows = LightShadows.None;
            lamps.Add(l);
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere); bulb.transform.SetParent(go.transform, false); bulb.transform.localScale = Vector3.one * 0.25f;
            bulb.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Emissive(c, 2f); Object.Destroy(bulb.GetComponent<Collider>());
        }

        static void Bounds()
        {
            float h = Half + 5f; var c = new Color(0.1f, 0.1f, 0.1f);
            var b = new GameObject("Bounds").transform; b.SetParent(Root, false);
            foreach (var (p, s) in new[] { (new Vector3(h, 10, 0), new Vector3(1, 40, h * 2)), (new Vector3(-h, 10, 0), new Vector3(1, 40, h * 2)), (new Vector3(0, 10, h), new Vector3(h * 2, 40, 1)), (new Vector3(0, 10, -h), new Vector3(h * 2, 40, 1)) })
            { var w = MeshKit.Box(b, p, s, c); w.GetComponent<Renderer>().enabled = !Underground ? false : true; }
        }

        static void PlaceExtracts()
        {
            foreach (var e in Map.extracts)
            {
                var pos = Snap(new Vector3(e.x, 0, e.z));
                var go = new GameObject("Extract_" + e.name); go.transform.SetParent(Root, false); go.transform.position = pos;
                var z = go.AddComponent<ExtractZone>(); z.Def = e;
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.transform.SetParent(go.transform, false);
                ring.transform.localScale = new Vector3(10, 0.05f, 10); ring.transform.localPosition = Vector3.up * 0.06f;
                ring.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Emissive(string.IsNullOrEmpty(e.cond) ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.7f, 0.2f), 0.8f);
                Object.Destroy(ring.GetComponent<Collider>());
                var beam = MeshKit.Box(go.transform, new Vector3(0, 20, 0), new Vector3(0.12f, 40, 0.12f), new Color(0.3f, 1f, 0.4f), false, false);
                beam.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Emissive(new Color(0.3f, 1f, 0.4f), 2.5f);
                ring.transform.localScale = new Vector3(10, 0.02f, 10); ring.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Mat(new Color(0.2f, 0.9f, 0.3f, 0.35f), 0.5f);
                Extracts.Add(z);
            }
        }

        static void PlacePois()
        {
            foreach (var p in Map.pois)
            {
                var go = new GameObject("POI_" + p.name); go.transform.SetParent(Root, false); go.transform.position = Snap(new Vector3(p.x, 0, p.z));
                var m = go.AddComponent<PoiMarker>(); m.Name = p.name; Pois.Add(m);
            }
        }

        static void DefaultSpawns()
        {
            float h = Half - 15;
            foreach (var p in new[] { new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(0, 0, -h), new Vector3(-h, 0, 0) })
                PlayerSpawns.Add(Snap(p));
        }

        public static void RandomEnemySpawns(int n)
        {
            for (int i = 0; i < n * 4 && EnemySpawns.Count < n; i++)
            {
                var p = new Vector3((float)(Rng.NextDouble() * 2 - 1) * (Half - 20), 0, (float)(Rng.NextDouble() * 2 - 1) * (Half - 20));
                if (PlayerSpawns.Any(s => Vector3.Distance(s, p) < 45)) continue;
                if (Physics.CheckSphere(p + Vector3.up * 1f, 0.6f)) continue;
                EnemySpawns.Add(Snap(p));
            }
        }

        /// <summary>Опустить точку на землю/пол.</summary>
        /// <summary>Опустить точку на землю/пол: берём самую верхнюю поверхность, кроме потолков помещений и «крышки» мира.</summary>
        public static Vector3 Snap(Vector3 p)
        {
            var hits = Physics.RaycastAll(new Vector3(p.x, 150, p.z), Vector3.down, 400f);
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));
            foreach (var h in hits)
            {
                string n = h.collider.gameObject.name;
                if (n.StartsWith("ceil") || n.StartsWith("cap") || h.collider.isTrigger) continue;
                return h.point + Vector3.up * 0.05f;
            }
            return new Vector3(p.x, 0.05f, p.z);
        }

        public static float GroundY(Vector3 p) => Snap(p).y;

        public static Vector3 PickPlayerSpawn(string squad)
        {
            if (PlayerSpawns.Count == 0) return Vector3.up;
            int idx = Mathf.Abs(squad.GetHashCode() + Seed) % PlayerSpawns.Count;
            var p = PlayerSpawns[idx];
            var r = Random.insideUnitCircle * 3f;
            return p + new Vector3(r.x, 1.2f, r.y);
        }

        public static LootContainer AddContainer(Vector3 pos, Quaternion rot, string kind, Transform parent = null)
        {
            var go = new GameObject("Loot_" + kind);
            go.transform.SetParent(parent ?? Root, false); go.transform.position = pos; go.transform.rotation = rot;
            var lc = go.AddComponent<LootContainer>(); lc.Id = Containers.Count; lc.Kind = kind;
            lc.Title = LootContainer.Titles.TryGetValue(kind, out var t) ? t : kind;
            Containers.Add(lc);
            Color col = kind == "safe" ? new Color(0.25f, 0.25f, 0.3f) : kind == "medbag" ? new Color(0.9f, 0.3f, 0.3f) : kind == "weaponbox" ? new Color(0.3f, 0.4f, 0.25f) : kind == "jacket" ? new Color(0.4f, 0.3f, 0.5f) : kind == "body" ? new Color(0.5f, 0.45f, 0.4f) : new Color(0.6f, 0.45f, 0.25f);
            // реалистичные модели Poly Haven по типу контейнера; запасной вариант — паки Kenney, потом примитив
            string ph = kind == "crate" ? "wooden_crate_01" : kind == "locker" ? "painted_wooden_cabinet" : kind == "cabinet" ? "painted_wooden_cabinet" : kind == "desk" ? "metal_office_desk" : kind == "shelf" ? "steel_frame_shelves_01"
                      : kind == "toolbox" ? "metal_toolbox" : kind == "weaponbox" ? "ammo_box" : kind == "server" ? "worn_metal_rack" : kind == "register" ? "cardboard_box_01" : kind == "fridge" ? "painted_wooden_cabinet" : kind == "bag" || kind == "medbag" ? "cardboard_box_01" : kind == "jacket" ? "trashbag" : kind == "safe" ? "utility_box_01" : null;
            string hint = kind == "safe" ? "safe" : kind == "locker" || kind == "cabinet" ? "locker" : kind == "desk" ? "table" : kind == "shelf" ? "shelf" : kind == "fridge" ? "fridge" : kind == "medbag" || kind == "bag" ? "bag" : kind == "body" ? "character" : kind == "server" ? "server" : "crate";
            float size = kind == "locker" || kind == "cabinet" || kind == "server" ? 1.9f : kind == "desk" ? 1.4f : kind == "shelf" ? 1.8f : kind == "body" ? 1.7f : kind == "medbag" || kind == "jacket" || kind == "bag" ? 0.5f : kind == "toolbox" || kind == "weaponbox" ? 0.6f : 0.9f;
            GameObject vis = null;
            if (kind == "body")
            {   // труп: персонаж Quaternius в позе поражения
                string[] whos = { "Casual_Male", "Worker_Male", "Casual2_Male", "Soldier_Male", "Casual_Female" };
                var cp = AssetCatalog.FindModel(whos[Mathf.Abs(Containers.Count * 7 + Seed) % whos.Length], "quaternius_characters");
                if (cp != null)
                {
                    vis = AssetCatalog.Spawn(cp, go.transform, 1.8f, false);
                    var mb = AssetCatalog.MeshBounds(vis); vis.transform.position += Vector3.up * (go.transform.position.y - mb.min.y);
                    var an = vis.GetComponent<Animator>() ?? vis.AddComponent<Animator>(); var ctrl = Resources.Load<RuntimeAnimatorController>("Anim/Soldier");
                    if (ctrl != null) { an.runtimeAnimatorController = ctrl; an.applyRootMotion = false; an.SetBool("Dead", true); an.Update(0f); }
                    var cc = go.AddComponent<CapsuleCollider>(); cc.direction = 2; cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0, 0.3f, 0.3f);
                }
            }
            else if (ph != null) { var phPath = AssetCatalog.FindModel(ph, "polyhaven"); if (phPath != null) { vis = AssetCatalog.Spawn(phPath, go.transform, size, true); if (vis != null) { var mb = AssetCatalog.MeshBounds(vis); vis.transform.position += Vector3.up * (go.transform.position.y - mb.min.y); } } }
            if (vis == null) vis = AssetCatalog.SpawnOrPlaceholder(hint, go.transform, size, col, PrimitiveType.Cube, null, true);
            if (vis.name.StartsWith("ph_")) { vis.transform.localPosition = Vector3.up * size * 0.5f; if (kind == "body") { vis.transform.localScale = new Vector3(0.5f, 0.3f, 1.7f); vis.transform.localPosition = Vector3.up * 0.15f; } }
            var col2 = go.GetComponent<Collider>(); if (col2 == null) { var bc = go.AddComponent<BoxCollider>(); bc.size = Vector3.one * Mathf.Max(0.8f, size); bc.center = Vector3.up * size * 0.5f; }
            foreach (var c in vis.GetComponentsInChildren<Collider>()) c.enabled = true;
            return lc;
        }

        public static LockedDoor AddDoor(Vector3 pos, float yaw, float width, string keyId, string room, Transform parent = null)
        {
            var go = new GameObject("Door_" + room); go.transform.SetParent(parent ?? Root, false);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var d = go.AddComponent<LockedDoor>(); d.Id = Doors.Count; d.KeyId = keyId; d.RoomName = room; Doors.Add(d);
            MeshKit.Box(go.transform, new Vector3(0, 1.3f, 0), new Vector3(width, 2.6f, 0.15f), string.IsNullOrEmpty(keyId) ? new Color(0.45f, 0.3f, 0.15f) : new Color(0.6f, 0.15f, 0.15f), true, false);
            return d;
        }

        public static LootContainer Container(int id) => id >= 0 && id < Containers.Count ? Containers[id] : null;
        public static LockedDoor Door(int id) => id >= 0 && id < Doors.Count ? Doors[id] : null;

        static void BakeNavMesh()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var s = Root.gameObject.AddComponent<NavMeshSurface>();
            s.collectObjects = CollectObjects.All;
            s.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            s.overrideVoxelSize = true; s.voxelSize = 0.33f;
            s.overrideTileSize = true; s.tileSize = 128;
            s.layerMask = ~0;
            s.BuildNavMesh();
            Debug.Log($"[World] NavMesh за {sw.ElapsedMilliseconds} мс");
        }

        public static void StartServerSystems()
        {
            if (Root == null) return;
            if (Root.GetComponent<EnemySpawner>() == null) Root.gameObject.AddComponent<EnemySpawner>();
        }
    }
}
