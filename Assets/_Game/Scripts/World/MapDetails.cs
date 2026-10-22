using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>
    /// Слой деталей и укрытий: реалистичные пропсы Poly Haven (бетонные блоки, бочки, ящики, лавки, фонари, баки, заборы)
    /// расставляются по логике — вдоль дорог и у стен зданий, группами; в чистом поле реже.
    /// </summary>
    public static class MapDetails
    {
        static System.Random R => World.Rng;
        static Transform T;
        static float Rnd(float a, float b) => Mathf.Lerp(a, b, (float)R.NextDouble());
        static bool Chance(double p) => R.NextDouble() < p;
        static bool Free(Vector3 p, float r) => !Physics.CheckSphere(p + Vector3.up * (r + 0.35f), r);
        static Material concrete, sandbag, metal, wood, bricks;

        static bool PH(string id, Vector3 p, float yaw, float size, bool collider = true) => AssetCatalog.Prop(id, "polyhaven", T, p, yaw, size, collider) != null;

        public static void Decorate(float density, bool outdoor)
        {
            T = new GameObject("Details").transform; T.SetParent(World.Root, false);
            concrete = AssetCatalog.TexMat("concrete", 1.5f, null, new Color(0.6f, 0.6f, 0.6f), 1);
            sandbag = AssetCatalog.TexMat("fabric", 0.6f, new Color(0.85f, 0.75f, 0.5f), new Color(0.6f, 0.55f, 0.35f));
            metal = AssetCatalog.TexMat("metal", 1.5f, null, new Color(0.4f, 0.42f, 0.45f));
            wood = AssetCatalog.TexMat("woodplanks", 1f, null, new Color(0.5f, 0.38f, 0.22f));
            bricks = AssetCatalog.TexMat("bricks", 1.5f, null, new Color(0.6f, 0.35f, 0.3f));
            float half = World.Half;
            int clusters = Mathf.RoundToInt(half * half * 4f / 1500f * density);
            int placed = 0; bool roads = World.RoadPoints.Count > 20;
            for (int i = 0; i < clusters * 4 && placed < clusters; i++)
            {
                Vector3 p; float yaw;
                if (roads && Chance(0.75))
                {
                    // у дороги: точка на обочине, ориентация вдоль дороги
                    int idx = R.Next(World.RoadPoints.Count); var rp = World.RoadPoints[idx]; var rq = World.RoadPoints[Mathf.Min(idx + 1, World.RoadPoints.Count - 1)];
                    var dir = (rq - rp); if (dir.sqrMagnitude < 0.01f) dir = Vector2.up; dir.Normalize(); var nrm = new Vector2(-dir.y, dir.x);
                    float side = Chance(0.5) ? 1 : -1; var pos = rp + nrm * side * Rnd(4.5f, 9f);
                    p = new Vector3(pos.x, 0, pos.y); yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                }
                else { p = new Vector3(Rnd(-half + 8, half - 8), 0, Rnd(-half + 8, half - 8)); yaw = R.Next(0, 360); }
                if (Mathf.Abs(p.x) > half - 6 || Mathf.Abs(p.z) > half - 6) continue;
                p = World.Snap(p);
                if (World.Map.kind == "mall" && Mathf.Abs(p.x) < 95 && Mathf.Abs(p.z) < 65) continue;
                if (!Free(p, 2.2f)) continue;
                bool nearSpawn = false; foreach (var s in World.PlayerSpawns) if (Vector3.Distance(s, p) < 8) { nearSpawn = true; break; }
                if (nearSpawn) continue;
                bool green = World.Map.kind == "suburb" || World.Map.kind == "osm";
                int kind = R.Next(0, outdoor ? (green ? 19 : 16) : 8);
                switch (kind)
                {
                    case 15: TreeLine(p, yaw); break;
                    case 16: Rocks(p); break;
                    case 17: GrassPatch(p); break;
                    case 18: GrassPatch(p); break;
                    case 0: Jersey(p, yaw); break;
                    case 1: Sandbags(p, yaw); break;
                    case 2: CrateStack(p, yaw); break;
                    case 3: Barrels(p, yaw); break;
                    case 4: LowWall(p, yaw); break;
                    case 5: Dumpster(p, yaw); break;
                    case 6: Shelves(p, yaw); break;
                    case 7: FenceLine(p, yaw); break;
                    case 8: if (Chance(0.6)) OsmMapBuilder.Car(T, p, yaw + Rnd(-15, 15), R); else CoveredCar(p, yaw); break;
                    case 9: Container(p, yaw); break;
                    case 10: BusStop(p, yaw); break;
                    case 11: Kiosk(p, yaw); break;
                    case 12: StreetLamp(p, yaw); break;
                    case 13: Bushes(p); break;
                    case 14: Tyres(p, yaw); break;
                }
                placed++;
            }
            Debug.Log($"[Details] укрытий и деталей: {placed} (у дорог: {roads})");
        }

        static void Loot(Vector3 p, string kind) { if (Chance(0.35)) World.AddContainer(p, Quaternion.Euler(0, R.Next(0, 360), 0), kind, T); }

        static void Jersey(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); int n = R.Next(2, 5);
            for (int i = 0; i < n; i++)
            {
                var pos = p + rot * new Vector3((i - (n - 1) / 2f) * 2.4f, 0, 0);
                if (!PH(Chance(0.5) ? "concrete_road_barrier_02" : "concrete_road_barrier", pos, yaw + 90, 2.3f)) MeshKit.Box(T, pos + Vector3.up * 0.45f, new Vector3(2f, 0.9f, 0.6f), concrete, true, true, rot);
            }
            Loot(p + rot * new Vector3(0, 0, 1.3f), "bag");
        }

        static void Sandbags(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            for (int row = 0; row < 3; row++) for (int i = 0; i < 4; i++)
                {
                    MeshKit.Box(T, p + rot * new Vector3(-2f + i * 1.0f + (row % 2) * 0.5f, 0.2f + row * 0.4f, 0), new Vector3(1.0f, 0.4f, 0.55f), sandbag, true, true, rot * Quaternion.Euler(0, Rnd(-6, 6), 0));
                    if (i < 3) MeshKit.Box(T, p + rot * new Vector3(2.0f, 0.2f + row * 0.4f, 0.5f + i * 1.0f + (row % 2) * 0.5f), new Vector3(0.55f, 0.4f, 1.0f), sandbag, true, true, rot);
                }
            Loot(p + rot * new Vector3(0, 0, -1.2f), "weaponbox");
        }

        static void CrateStack(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); int n = R.Next(2, 5);
            string[] ids = { "wooden_crate_01", "wooden_crate_01", "plastic_crate_01", "cardboard_box_01" };
            for (int i = 0; i < n; i++)
            {
                var pos = p + rot * new Vector3(Rnd(-1.4f, 1.4f), 0, Rnd(-1.2f, 1.2f));
                if (!PH(ids[R.Next(ids.Length)], pos, R.Next(0, 360), Rnd(0.8f, 1.2f))) MeshKit.Box(T, pos + Vector3.up * 0.5f, Vector3.one, wood, true, true, Quaternion.Euler(0, R.Next(0, 360), 0));
            }
            if (!PH("wooden_crate_01", p + Vector3.up * 1.0f, R.Next(0, 360), 1.0f)) MeshKit.Box(T, p + Vector3.up * 1.5f, Vector3.one, wood);
            Loot(p + rot * new Vector3(2.2f, 0, 0), "crate");
        }

        static void Barrels(Vector3 p, float yaw)
        {
            int n = R.Next(3, 6);
            for (int i = 0; i < n; i++)
            {
                var pos = p + new Vector3(Rnd(-1.4f, 1.4f), 0, Rnd(-1.4f, 1.4f));
                if (!PH(Chance(0.6) ? "barrel_03" : "Barrel_01", pos, R.Next(0, 360), 0.9f))
                { var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); b.transform.SetParent(T, false); b.transform.position = pos + Vector3.up * 0.45f; b.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f); b.GetComponent<Renderer>().sharedMaterial = AssetCatalog.TexMat("rust", 1f, null, new Color(0.4f, 0.3f, 0.25f)); b.isStatic = true; }
            }
            if (Chance(0.5)) PH("metal_jerrycan", p + new Vector3(1.8f, 0, 0.5f), R.Next(0, 360), 0.5f);
            if (Chance(0.4)) PH("propane_tank", p + new Vector3(-1.8f, 0, -0.5f), R.Next(0, 360), 1.2f);
            Loot(p + new Vector3(2.2f, 0, 0), "toolbox");
        }

        static void LowWall(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); float len = Rnd(4, 9);
            MeshKit.Box(T, p + Vector3.up * 0.6f, new Vector3(len, 1.2f, 0.3f), Chance(0.5) ? bricks : concrete, true, true, rot);
            if (Chance(0.5)) MeshKit.Box(T, p + rot * new Vector3(len / 2, 0.6f, len * 0.25f), new Vector3(0.3f, 1.2f, len * 0.5f), bricks, true, true, rot);
            if (Chance(0.5)) PH("utility_box_01", p + rot * new Vector3(-len / 2 + 0.6f, 0.8f, -0.25f), yaw, 0.6f, false);
            Loot(p + rot * new Vector3(0, 0, 1f), "jacket");
        }

        static void Dumpster(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            MeshKit.Box(T, p + Vector3.up * 0.7f, new Vector3(2.2f, 1.4f, 1.2f), AssetCatalog.TexMat("metal", 1.5f, new Color(0.3f, 0.5f, 0.3f), new Color(0.2f, 0.4f, 0.25f)), true, true, rot);
            MeshKit.Box(T, p + Vector3.up * 1.45f, new Vector3(2.3f, 0.1f, 1.3f), metal, true, true, rot);
            for (int i = 0; i < R.Next(1, 4); i++) PH(Chance(0.5) ? "trashbag" : "metal_trash_can", p + rot * new Vector3(Rnd(-1.6f, 1.6f), 0, 1.2f + Rnd(0, 0.8f)), R.Next(0, 360), 0.8f);
            World.AddContainer(p + rot * new Vector3(0, 0, 1.0f), rot, "crate", T).Title = "Мусорный бак";
        }

        static void Shelves(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            if (!PH(Chance(0.5) ? "steel_frame_shelves_01" : "worn_metal_rack", p, yaw, 1.9f)) { for (int i = 0; i < 3; i++) MeshKit.Box(T, p + rot * new Vector3(0, 0.3f + i * 0.6f, 0), new Vector3(1.6f, 0.05f, 0.6f), metal, true, true, rot); }
            if (Chance(0.6)) PH("metal_toolbox", p + rot * new Vector3(1.6f, 0, 0), R.Next(0, 360), 0.5f);
            if (Chance(0.5)) PH("ladder_sectioned_01", p + rot * new Vector3(-1.8f, 0, 0.2f), yaw + 90, 2.5f);
            Loot(p + rot * new Vector3(0, 0, 1.2f), "toolbox");
        }

        static void FenceLine(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); int segs = R.Next(3, 7); bool placed = false;
            for (int i = 0; i < segs; i++) placed |= PH("modular_chainlink_fence", p + rot * new Vector3((i - (segs - 1) / 2f) * 2.0f, 0, 0), yaw, 2.0f);
            if (!placed)
            {
                float len = segs * 2f;
                MeshKit.Box(T, p + Vector3.up * 1.0f, new Vector3(len, 2.0f, 0.06f), AssetCatalog.TexMat("metal", 1f, new Color(0.9f, 0.9f, 0.9f), new Color(0.5f, 0.5f, 0.5f)), true, true, rot);
                for (float x = -len / 2; x <= len / 2; x += 2f) MeshKit.Box(T, p + rot * new Vector3(x, 1.05f, 0), new Vector3(0.1f, 2.1f, 0.1f), metal, true, true, rot);
            }
        }

        static void CoveredCar(Vector3 p, float yaw) { if (!PH("covered_car", p, yaw, 4.6f)) OsmMapBuilder.Car(T, p, yaw, R); Loot(p + Quaternion.Euler(0, yaw, 0) * new Vector3(2.2f, 0, 0), "jacket"); }

        static void Container(Vector3 p, float yaw)
        {
            if (AssetCatalog.Prop("shipping-container", "industrial", T, p, yaw, 8.5f) == null)
            {
                var c = new Color(Rnd(0.2f, 0.9f), Rnd(0.2f, 0.7f), Rnd(0.2f, 0.7f));
                MeshKit.Box(T, p + Vector3.up * 1.3f, new Vector3(2.5f, 2.6f, 8.5f), AssetCatalog.TexMat("metal", 2f, c, c), true, true, Quaternion.Euler(0, yaw, 0));
            }
            Loot(p + Quaternion.Euler(0, yaw, 0) * new Vector3(2.2f, 0, Rnd(-3, 3)), "crate");
        }

        static void BusStop(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            MeshKit.Box(T, p + rot * new Vector3(0, 2.6f, 0), new Vector3(5f, 0.15f, 2.2f), metal, true, true, rot);
            foreach (var x in new[] { -2.3f, 2.3f }) MeshKit.Box(T, p + rot * new Vector3(x, 1.3f, -1.0f), new Vector3(0.12f, 2.6f, 0.12f), metal, true, true, rot);
            MeshKit.Box(T, p + rot * new Vector3(0, 1.3f, -1.05f), new Vector3(5f, 2.6f, 0.08f), AssetCatalog.Mat(new Color(0.6f, 0.8f, 0.9f, 0.5f), 0.9f), true, true, rot);
            if (!PH("modular_street_seating", p + rot * new Vector3(0, 0, -0.4f), yaw, 2.2f)) MeshKit.Box(T, p + rot * new Vector3(0, 0.45f, -0.4f), new Vector3(3.5f, 0.1f, 0.5f), wood, true, true, rot);
            PH("metal_trash_can", p + rot * new Vector3(2.9f, 0, -0.3f), yaw, 0.9f);
            World.AddContainer(p + rot * new Vector3(2.0f, 0, 0.6f), rot, "jacket", T);
        }

        static void Kiosk(Vector3 p, float yaw)
        {
            Buildings.Make(Buildings.Rect(p.x, p.z, 4.5f, 3.5f, yaw), 1, "Киоск" + p.GetHashCode(), new Color(0.9f, 0.6f, 0.3f), true, "shop", null, null, 2.8f);
        }

        static void StreetLamp(Vector3 p, float yaw)
        {
            if (!PH("street_lamp_01", p, yaw, 7f)) { MeshKit.Box(T, p + Vector3.up * 3f, new Vector3(0.15f, 6f, 0.15f), metal); MeshKit.Box(T, p + new Vector3(0.6f, 5.9f, 0), new Vector3(1.3f, 0.12f, 0.12f), metal); }
            if (World.Night) World.Lamp(p + new Vector3(0.9f, 5.7f, 0), new Color(1f, 0.85f, 0.6f), 18f, 1.8f);
            if (Chance(0.5)) PH("fire_hydrant", p + new Vector3(1.5f, 0, 1.0f), R.Next(0, 360), 0.8f);
        }

        static bool Nat(string id, Vector3 p, float size, bool collider = false) => AssetCatalog.Prop(id, "quaternius_nature", T, p, R.Next(0, 360), size, collider) != null;

        /// <summary>Ряд деревьев вдоль дороги (аллея) — читаемые ориентиры и укрытие от снайперов.</summary>
        static void TreeLine(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); int n = R.Next(3, 7); string kind = Chance(0.4) ? "pine" : null;
            for (int i = 0; i < n; i++)
            {
                var pos = World.Snap(p + rot * new Vector3((i - (n - 1) / 2f) * Rnd(5.5f, 7f), 0, Rnd(-0.8f, 0.8f)));
                if (Free(pos, 1.5f)) MeshKit.Tree(T, pos, R, kind);
            }
            if (Chance(0.5)) GrassPatch(p + rot * new Vector3(0, 0, 2.5f));
        }

        /// <summary>Валуны и камни — укрытие в чистом поле.</summary>
        static void Rocks(Vector3 p)
        {
            int n = R.Next(2, 5);
            string[] ids = { "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3" };
            for (int i = 0; i < n; i++)
            {
                var pos = p + new Vector3(Rnd(-2.5f, 2.5f), 0, Rnd(-2.5f, 2.5f));
                if (!Nat(ids[R.Next(ids.Length)], pos, Rnd(1.6f, 3.2f), true)) AssetCatalog.Prop(Chance(0.5) ? "rock-a" : "rock-b", "survival", T, pos, R.Next(0, 360), Rnd(1.5f, 3f), true);
            }
            for (int i = 0; i < 4; i++) Nat("Pebble_Round_" + R.Next(1, 6), p + new Vector3(Rnd(-3.5f, 3.5f), 0, Rnd(-3.5f, 3.5f)), Rnd(0.3f, 0.6f));
            if (Chance(0.5)) GrassPatch(p + new Vector3(Rnd(-2, 2), 0, Rnd(-2, 2)));
        }

        /// <summary>Пятно травы, клевера, папоротника и цветов (без коллайдеров).</summary>
        static void GrassPatch(Vector3 p)
        {
            int n = R.Next(7, 14);
            string[] ids = { "Grass_Common_Tall", "Grass_Common_Short", "Grass_Wispy_Tall", "Grass_Wispy_Short", "Clover_1", "Clover_2", "Fern_1", "Flower_3_Group", "Flower_4_Group", "Plant_1", "Plant_7" };
            for (int i = 0; i < n; i++)
            {
                var pos = World.Snap(p + new Vector3(Rnd(-3.5f, 3.5f), 0, Rnd(-3.5f, 3.5f)));
                if (Physics.CheckSphere(pos + Vector3.up * 0.5f, 0.3f)) continue;
                Nat(ids[R.Next(ids.Length)], pos, Rnd(0.6f, 1.3f));
            }
        }

        static void Bushes(Vector3 p)
        {
            int n = R.Next(3, 7);
            for (int i = 0; i < n; i++)
            {
                var pos = p + new Vector3(Rnd(-3, 3), 0, Rnd(-3, 3));
                if (Nat(Chance(0.7) ? "Bush_Common" : "Bush_Common_Flowers", pos, Rnd(1.4f, 2.4f), true)) continue;
                var bush = AssetCatalog.Prop("bush", "nature", T, pos, R.Next(0, 360), Rnd(1.2f, 2.2f), true);
                if (bush != null) { foreach (var r in bush.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) m.color = Color.Lerp(m.color, new Color(0.12f, 0.3f, 0.1f), 0.75f); }
                else
                { var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.transform.SetParent(T, false); s.transform.position = pos + Vector3.up * 0.7f; s.transform.localScale = new Vector3(1.8f, 1.4f, 1.8f); s.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Mat(new Color(0.2f, 0.5f, 0.2f)); s.isStatic = true; }
            }
        }

        static void Tyres(Vector3 p, float yaw)
        {
            int n = R.Next(3, 8);
            for (int i = 0; i < n; i++) { var pos = p + new Vector3(Rnd(-1.5f, 1.5f), i < 3 ? 0 : 0.25f, Rnd(-1.5f, 1.5f)); if (!PH("old_tyre", pos, R.Next(0, 360), 0.7f)) { var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); c.transform.SetParent(T, false); c.transform.position = pos + Vector3.up * 0.12f; c.transform.localScale = new Vector3(0.7f, 0.12f, 0.7f); c.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Mat(new Color(0.08f, 0.08f, 0.08f)); c.isStatic = true; } }
            Loot(p + new Vector3(2f, 0, 0), "bag");
        }
    }
}
