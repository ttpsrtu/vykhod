using System.Collections.Generic;
using UnityEngine;

namespace Vykhod
{
    /// <summary>
    /// Слой деталей и укрытий для любой карты: бетонные блоки, мешки с песком, штабеля ящиков, бочки, разбитые машины,
    /// контейнеры, остановки, киоски, заборы, кусты и камни. Рядом с укрытиями иногда лежит лут.
    /// </summary>
    public static class MapDetails
    {
        static System.Random R => World.Rng;
        static Transform T;
        static float Rnd(float a, float b) => Mathf.Lerp(a, b, (float)R.NextDouble());
        static bool Chance(double p) => R.NextDouble() < p;
        static bool Free(Vector3 p, float r) => !Physics.CheckSphere(p + Vector3.up * (r + 0.35f), r);

        static Material concrete, sandbag, metal, wood, bricks;

        public static void Decorate(float density, bool outdoor)
        {
            T = new GameObject("Details").transform; T.SetParent(World.Root, false);
            concrete = AssetCatalog.TexMat("concrete", 1.5f, null, new Color(0.6f, 0.6f, 0.6f), 1);
            sandbag = AssetCatalog.TexMat("fabric", 0.6f, new Color(0.85f, 0.75f, 0.5f), new Color(0.6f, 0.55f, 0.35f));
            metal = AssetCatalog.TexMat("metal", 1.5f, null, new Color(0.4f, 0.42f, 0.45f));
            wood = AssetCatalog.TexMat("woodplanks", 1f, null, new Color(0.5f, 0.38f, 0.22f));
            bricks = AssetCatalog.TexMat("bricks", 1.5f, null, new Color(0.6f, 0.35f, 0.3f));
            float half = World.Half;
            int clusters = Mathf.RoundToInt(half * half * 4f / 2200f * density);
            int placed = 0;
            for (int i = 0; i < clusters * 3 && placed < clusters; i++)
            {
                var p = new Vector3(Rnd(-half + 8, half - 8), 0, Rnd(-half + 8, half - 8));
                p = World.Snap(p);
                if (World.Map.kind == "mall" && Mathf.Abs(p.x) < 95 && Mathf.Abs(p.z) < 65) continue; // внутри ТЦ своя расстановка
                if (World.Underground && !Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, 1.5f)) continue;
                if (!Free(p, 2.5f)) continue;
                bool nearPlayerSpawn = false; foreach (var s in World.PlayerSpawns) if (Vector3.Distance(s, p) < 8) { nearPlayerSpawn = true; break; }
                if (nearPlayerSpawn) continue;
                float yaw = R.Next(0, 360);
                int kind = R.Next(0, outdoor ? 14 : 8);
                switch (kind)
                {
                    case 0: Jersey(p, yaw); break;
                    case 1: Sandbags(p, yaw); break;
                    case 2: CrateStack(p, yaw); break;
                    case 3: Barrels(p); break;
                    case 4: LowWall(p, yaw); break;
                    case 5: Dumpster(p, yaw); break;
                    case 6: Pallets(p, yaw); break;
                    case 7: FenceLine(p, yaw); break;
                    case 8: if (outdoor) OsmMapBuilder.Car(T, p, yaw, R); break;
                    case 9: Container(p, yaw); break;
                    case 10: BusStop(p, yaw); break;
                    case 11: Kiosk(p, yaw); break;
                    case 12: Bushes(p); break;
                    case 13: Rocks(p); break;
                }
                placed++;
            }
            Debug.Log($"[Details] укрытий и деталей: {placed}");
        }

        static void Loot(Vector3 p, string kind) { if (Chance(0.35)) World.AddContainer(p, Quaternion.Euler(0, R.Next(0, 360), 0), kind, T); }

        /// <summary>Ряд бетонных блоков-разделителей.</summary>
        static void Jersey(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); int n = R.Next(2, 5);
            for (int i = 0; i < n; i++) MeshKit.Box(T, p + rot * new Vector3((i - (n - 1) / 2f) * 2.1f, 0.45f, 0), new Vector3(2f, 0.9f, 0.6f), concrete, true, true, rot);
            Loot(p + rot * new Vector3(0, 0, 1.2f), "bag");
        }

        /// <summary>Мешки с песком Г-образно, высота по грудь.</summary>
        static void Sandbags(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            for (int row = 0; row < 3; row++) for (int i = 0; i < 4; i++)
                {
                    MeshKit.Box(T, p + rot * new Vector3(-2f + i * 1.0f + (row % 2) * 0.5f, 0.2f + row * 0.4f, 0), new Vector3(1.0f, 0.4f, 0.55f), sandbag, true, true, rot);
                    if (i < 3) MeshKit.Box(T, p + rot * new Vector3(2.0f, 0.2f + row * 0.4f, 0.5f + i * 1.0f + (row % 2) * 0.5f), new Vector3(0.55f, 0.4f, 1.0f), sandbag, true, true, rot);
                }
            Loot(p + rot * new Vector3(0, 0, -1.2f), "weaponbox");
        }

        static void CrateStack(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); int n = R.Next(2, 5);
            for (int i = 0; i < n; i++)
            {
                var pos = p + rot * new Vector3(Rnd(-1.5f, 1.5f), 0, Rnd(-1.5f, 1.5f));
                if (AssetCatalog.Prop(Chance(0.5) ? "box-large" : "box", "survival", T, pos, R.Next(0, 360), 1.3f) == null) MeshKit.Box(T, pos + Vector3.up * 0.6f, new Vector3(1.2f, 1.2f, 1.2f), wood, true, true, Quaternion.Euler(0, R.Next(0, 360), 0));
            }
            if (AssetCatalog.Prop("box-large", "survival", T, p + Vector3.up * 1.3f, R.Next(0, 360), 1.3f) == null) MeshKit.Box(T, p + Vector3.up * 1.8f, new Vector3(1.2f, 1.2f, 1.2f), wood);
            Loot(p + rot * new Vector3(2.2f, 0, 0), "crate");
        }

        static void Barrels(Vector3 p)
        {
            int n = R.Next(3, 6);
            for (int i = 0; i < n; i++)
            {
                var pos = p + new Vector3(Rnd(-1.5f, 1.5f), 0, Rnd(-1.5f, 1.5f));
                if (AssetCatalog.Prop(Chance(0.7) ? "barrel" : "barrel-open", "survival", T, pos, R.Next(0, 360), 1.0f) == null)
                { var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); b.transform.SetParent(T, false); b.transform.position = pos + Vector3.up * 0.45f; b.transform.localScale = new Vector3(0.7f, 0.45f, 0.7f); b.GetComponent<Renderer>().sharedMaterial = AssetCatalog.TexMat("rust", 1f, null, new Color(0.4f, 0.3f, 0.25f)); b.isStatic = true; }
            }
            Loot(p + new Vector3(2f, 0, 0), "toolbox");
        }

        static void LowWall(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); float len = Rnd(4, 9);
            MeshKit.Box(T, p + Vector3.up * 0.6f, new Vector3(len, 1.2f, 0.3f), Chance(0.5) ? bricks : concrete, true, true, rot);
            if (Chance(0.5)) MeshKit.Box(T, p + rot * new Vector3(len / 2, 0.6f, len * 0.25f), new Vector3(0.3f, 1.2f, len * 0.5f), bricks, true, true, rot);
            Loot(p + rot * new Vector3(0, 0, 1f), "jacket");
        }

        static void Dumpster(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            MeshKit.Box(T, p + Vector3.up * 0.7f, new Vector3(2.2f, 1.4f, 1.2f), AssetCatalog.TexMat("metal", 1.5f, new Color(0.3f, 0.5f, 0.3f), new Color(0.2f, 0.4f, 0.25f)), true, true, rot);
            MeshKit.Box(T, p + Vector3.up * 1.45f, new Vector3(2.3f, 0.1f, 1.3f), metal, true, true, rot);
            World.AddContainer(p + rot * new Vector3(0, 0, 1.0f), rot, "crate", T).Title = "Мусорный бак";
        }

        static void Pallets(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); int n = R.Next(2, 6);
            for (int i = 0; i < n; i++) MeshKit.Box(T, p + rot * new Vector3(0, 0.08f + i * 0.16f, 0), new Vector3(1.2f, 0.14f, 1.0f), wood, true, true, rot);
            if (Chance(0.5)) { for (int i = 0; i < 3; i++) MeshKit.Box(T, p + rot * new Vector3(1.6f, 0.08f + i * 0.16f, 0.3f), new Vector3(1.2f, 0.14f, 1.0f), wood, true, true, rot); }
            Loot(p + rot * new Vector3(-1.5f, 0, 0), "crate");
        }

        static void FenceLine(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0); float len = Rnd(6, 14);
            int segs = Mathf.RoundToInt(len / 2.2f); bool placed = false;
            for (int i = 0; i < segs; i++) { var seg = AssetCatalog.Prop(Chance(0.8) ? "fence" : "fence-fortified", "survival", T, p + rot * new Vector3((i - (segs - 1) / 2f) * 2.2f, 0, 0), yaw, 2.3f); placed |= seg != null; }
            if (!placed)
            {
                MeshKit.Box(T, p + Vector3.up * 1.0f, new Vector3(len, 2.0f, 0.1f), AssetCatalog.TexMat("metal", 1f, new Color(0.9f, 0.9f, 0.9f), new Color(0.5f, 0.5f, 0.5f)), true, true, rot);
                for (float x = -len / 2; x <= len / 2; x += 2f) MeshKit.Box(T, p + rot * new Vector3(x, 1.05f, 0), new Vector3(0.12f, 2.1f, 0.12f), metal, true, true, rot);
            }
        }

        static void Container(Vector3 p, float yaw)
        {
            if (AssetCatalog.Prop("shipping-container", "industrial", T, p, yaw, 8.5f) == null)
            {
                var c = new Color(Rnd(0.2f, 0.9f), Rnd(0.2f, 0.7f), Rnd(0.2f, 0.7f));
                MeshKit.Box(T, p + Vector3.up * 1.3f, new Vector3(2.5f, 2.6f, 12f), AssetCatalog.TexMat("metal", 2f, c, c), true, true, Quaternion.Euler(0, yaw, 0));
            }
            Loot(p + Quaternion.Euler(0, yaw, 0) * new Vector3(2.2f, 0, Rnd(-5, 5)), "crate");
        }

        /// <summary>Остановка: навес на стойках и скамейка — укрытие и точка лута.</summary>
        static void BusStop(Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            MeshKit.Box(T, p + rot * new Vector3(0, 2.6f, 0), new Vector3(5f, 0.15f, 2.2f), metal, true, true, rot);
            foreach (var x in new[] { -2.3f, 2.3f }) MeshKit.Box(T, p + rot * new Vector3(x, 1.3f, -1.0f), new Vector3(0.12f, 2.6f, 0.12f), metal, true, true, rot);
            MeshKit.Box(T, p + rot * new Vector3(0, 1.3f, -1.05f), new Vector3(5f, 2.6f, 0.08f), AssetCatalog.Mat(new Color(0.6f, 0.8f, 0.9f, 0.5f), 0.9f), true, true, rot);
            MeshKit.Box(T, p + rot * new Vector3(0, 0.45f, -0.4f), new Vector3(3.5f, 0.1f, 0.5f), wood, true, true, rot);
            World.AddContainer(p + rot * new Vector3(2.0f, 0, 0.6f), rot, "jacket", T);
        }

        /// <summary>Маленький киоск — заходим внутрь, там касса и полка.</summary>
        static void Kiosk(Vector3 p, float yaw)
        {
            Buildings.Make(Buildings.Rect(p.x, p.z, 4.5f, 3.5f, yaw), 1, "Киоск" + p.GetHashCode(), new Color(0.9f, 0.6f, 0.3f), true, "shop", null, null, 2.8f);
        }

        static void Bushes(Vector3 p)
        {
            int n = R.Next(3, 7);
            for (int i = 0; i < n; i++)
            {
                var pos = p + new Vector3(Rnd(-3, 3), 0, Rnd(-3, 3));
                if (AssetCatalog.Prop("bush", "nature", T, pos, R.Next(0, 360), Rnd(1.2f, 2.2f), true) == null)
                { var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.transform.SetParent(T, false); s.transform.position = pos + Vector3.up * 0.7f; s.transform.localScale = new Vector3(1.8f, 1.4f, 1.8f); s.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Mat(new Color(0.2f, 0.5f, 0.2f)); s.isStatic = true; }
            }
        }

        static void Rocks(Vector3 p)
        {
            int n = R.Next(2, 5);
            for (int i = 0; i < n; i++)
            {
                var pos = p + new Vector3(Rnd(-2.5f, 2.5f), 0, Rnd(-2.5f, 2.5f));
                if (AssetCatalog.Prop(Chance(0.5) ? "rock-a" : "rock-c", "survival|nature", T, pos, R.Next(0, 360), Rnd(1.2f, 2.6f), true) == null)
                { var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.transform.SetParent(T, false); s.transform.position = pos + Vector3.up * 0.5f; s.transform.localScale = new Vector3(2f, 1.2f, 1.6f); s.GetComponent<Renderer>().sharedMaterial = AssetCatalog.TexMat("rock", 1.5f, null, new Color(0.5f, 0.5f, 0.5f)); s.isStatic = true; }
            }
        }
    }
}
