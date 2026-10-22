using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Процедурные карты из готовых моделей Kenney и текстурированной геометрии: завод, посёлок, торговый центр, подземка, запасной город.</summary>
    public static class ProceduralMaps
    {
        static System.Random R => World.Rng;
        static Transform T => World.Root;
        static float Rnd(float a, float b) => Mathf.Lerp(a, b, (float)R.NextDouble());
        static bool Chance(double p) => R.NextDouble() < p;

        static void Spawns(float h)
        {
            foreach (var p in new[] { new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(0, 0, -h), new Vector3(0, 0, h) })
                World.PlayerSpawns.Add(World.Snap(p));
        }

        static void Fence(float half, float h, Material m, float gap = 14f)
        {
            float s = half;
            foreach (var (a, b) in new[] { (new Vector2(-s, -s), new Vector2(s, -s)), (new Vector2(s, -s), new Vector2(s, s)), (new Vector2(s, s), new Vector2(-s, s)), (new Vector2(-s, s), new Vector2(-s, -s)) })
            {
                var dir = (b - a).normalized; float len = (b - a).magnitude;
                MeshKit.Wall(T, a + dir * gap, a + dir * (len * 0.5f - gap * 0.5f), h, 0.2f, m);
                MeshKit.Wall(T, a + dir * (len * 0.5f + gap * 0.5f), b - dir * gap, h, 0.2f, m);
            }
        }

        static void Crate(Vector3 p, string kind = "crate") => World.AddContainer(p, Quaternion.Euler(0, R.Next(0, 360), 0), kind);

        /// <summary>Дорога-лента + регистрация точек для слоя укрытий.</summary>
        static void Road(List<Vector2> pts, float w, Material m)
        {
            MeshKit.Strip(T, pts, w, 0.04f, m);
            for (int i = 0; i + 1 < pts.Count; i++) { float len = (pts[i + 1] - pts[i]).magnitude; int n = Mathf.Max(1, (int)(len / 8)); for (int k = 0; k <= n; k++) World.RoadPoints.Add(Vector2.Lerp(pts[i], pts[i + 1], (float)k / n)); }
        }
        static bool Free(Vector3 p, float r) => !Physics.CheckSphere(p + Vector3.up * (r + 0.35f), r);

        /// <summary>Готовая модель из пака (или ничего, если пака нет). Возвращает bounds-размер по XZ для расстановки лута рядом.</summary>
        static GameObject Model(string hint, string pack, Vector3 p, float yaw, float size)
        {
            var go = AssetCatalog.Prop(hint, pack, T, p, yaw, size, true);
            return go;
        }

        static void Container(Vector3 p, float yaw)
        {
            if (Model("shipping-container", "industrial", p, yaw, 8.5f) == null)
            {
                var c = new Color(Rnd(0.2f, 0.9f), Rnd(0.2f, 0.7f), Rnd(0.2f, 0.7f));
                MeshKit.Box(T, p + Vector3.up * 1.3f, new Vector3(2.5f, 2.6f, 12f), AssetCatalog.TexMat("metal", 2f, c, c), true, true, Quaternion.Euler(0, yaw, 0));
            }
            if (Chance(0.4)) Crate(p + Quaternion.Euler(0, yaw, 0) * new Vector3(2.2f, 0, Rnd(-5, 5)));
        }

        // ================================================================ ЗАВОД
        public static void BuildIndustrial()
        {
            float half = World.Half;
            MeshKit.Ground(T, World.Map.size + 200, "asphalt", 6f, new Color(0.55f, 0.55f, 0.58f));
            Fence(half - 4, 2.6f, AssetCatalog.TexMat("metal", 2.5f, null, new Color(0.45f, 0.45f, 0.45f)));
            var road = AssetCatalog.TexMat("asphalt", 5f, new Color(0.85f, 0.85f, 0.85f), new Color(0.25f, 0.25f, 0.26f), 1);
            for (int i = -1; i <= 1; i++) { Road(new List<Vector2> { new Vector2(-half, i * 150), new Vector2(half, i * 150) }, 8, road); Road(new List<Vector2> { new Vector2(i * 150, -half), new Vector2(i * 150, half) }, 8, road); }
            // ж/д
            MeshKit.Strip(T, new List<Vector2> { new Vector2(-half, -230), new Vector2(half, -230) }, 4, 0.05f, AssetCatalog.TexMat("gravel", 2f, null, new Color(0.3f, 0.25f, 0.2f)));
            for (float x = -half; x < half; x += 0.7f) MeshKit.Box(T, new Vector3(x, 0.07f, -230), new Vector3(0.25f, 0.12f, 2.6f), new Color(0.35f, 0.25f, 0.15f), false);
            foreach (var z in new[] { -230.75f, -229.25f }) MeshKit.Box(T, new Vector3(0, 0.16f, z), new Vector3(half * 2, 0.08f, 0.08f), Buildings.Metal, false);
            for (int i = 0; i < 5; i++) { MeshKit.Box(T, new Vector3(-180 + i * 16, 1.8f, -230), new Vector3(14, 3, 3), AssetCatalog.TexMat("rust", 3f, null, new Color(0.4f, 0.25f, 0.2f))); if (Chance(0.5)) Crate(new Vector3(-180 + i * 16, 3.3f, -230)); }
            // цеха и склады (полые, с лутом) + промышленные здания Kenney по периметру
            Buildings.Make(Buildings.Rect(0, 0, 70, 36), 1, "Сборочный цех", new Color(0.6f, 0.62f, 0.65f), true, "industrial", null, null, 9f);
            for (int i = 0; i < 6; i++) Crate(new Vector3(-28 + i * 11, 0.1f, Rnd(-12, 12)), Chance(0.5) ? "toolbox" : "crate");
            for (int i = 0; i < 3; i++) if (Model("workbench", "survival", new Vector3(-20 + i * 20, 0.1f, 0), 90, 2.5f) == null) MeshKit.Box(T, new Vector3(-20 + i * 20, 1.2f, 0), new Vector3(6, 2.4f, 2.5f), Buildings.Metal);
            Buildings.Make(Buildings.Rect(-150, 100, 44, 26), 1, "Склад", new Color(0.55f, 0.5f, 0.45f), true, "industrial", "key_factory_tools", "инструменталка", 7f);
            for (int r = 0; r < 3; r++) for (int i = 0; i < 4; i++) { MeshKit.Box(T, new Vector3(-165 + i * 10, 2f, 90 + r * 10), new Vector3(6, 4, 1.2f), Buildings.Metal); World.AddContainer(new Vector3(-165 + i * 10, 0.1f, 91.2f + r * 10), Quaternion.identity, "shelf"); }
            Buildings.Make(Buildings.Rect(150, 100, 24, 18), 2, "Котельная", new Color(0.5f, 0.35f, 0.3f), true, "industrial", null, null, 4f);
            if (Model("chimney-large", "industrial", new Vector3(165, 0, 110), 0, 28f) == null) { var ch = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ch.transform.SetParent(T, false); ch.transform.position = new Vector3(165, 20, 110); ch.transform.localScale = new Vector3(3, 20, 3); ch.GetComponent<Renderer>().sharedMaterial = Buildings.Bricks; ch.isStatic = true; }
            for (int i = 0; i < 8; i++) Buildings.Make(Buildings.Rect(-185 + i * 10, -120, 9, 7), 1, "Гараж" + i, new Color(0.45f, 0.45f, 0.5f), true, "garage", null, null, 3f);
            Buildings.Make(Buildings.Rect(0, 80, 30, 14), 3, "Администрация", new Color(0.8f, 0.75f, 0.65f), true, "office", "key_factory_director", "кабинет директора");
            World.SniperSpots.Add(new Vector3(0, 3 * Buildings.FloorH + 0.5f, 80));
            // склады и цеха (гофрометалл, роллетные ворота, ленты окон) вдоль дорог
            int wi = 0;
            foreach (var (x, z) in new[] { (-200f, 60f), (-200f, 0f), (-90f, 170f), (90f, 170f), (200f, 40f), (200f, -40f), (60f, -170f), (-60f, -170f), (200f, 180f), (-200f, 200f) })
            {
                float w = Rnd(22, 36), d = Rnd(14, 24); int lv = Chance(0.3) ? 2 : 1; float fh = lv == 1 ? Rnd(5, 7) : 4f;
                Buildings.Make(Buildings.Rect(x, z, w, d, R.Next(0, 2) * 90), lv, "Склад" + wi++, new Color(0.6f, 0.62f, 0.65f), true, "industrial", null, null, fh);
                if (Chance(0.7)) Crate(new Vector3(x + Rnd(-8, 8), 0.1f, z + d * 0.5f + 3), Chance(0.5) ? "crate" : "toolbox");
                if (Chance(0.5)) AssetCatalog.Prop("portable_generator", "polyhaven", T, new Vector3(x + w * 0.5f + 2, 0, z + Rnd(-4, 4)), R.Next(0, 360), 1.2f);
            }
            var tankM = AssetCatalog.TexMat("rust", 2.5f, new Color(0.85f, 0.85f, 0.85f), new Color(0.6f, 0.55f, 0.5f));
            for (int i = 0; i < 4; i++) { var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder); tank.transform.SetParent(T, false); tank.transform.position = new Vector3(120 + i * 18, 5, -100); tank.transform.localScale = new Vector3(11, 5, 11); tank.GetComponent<Renderer>().sharedMaterial = tankM; tank.isStatic = true; var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere); cap.transform.SetParent(T, false); cap.transform.position = new Vector3(120 + i * 18, 10, -100); cap.transform.localScale = new Vector3(11, 3, 11); cap.GetComponent<Renderer>().sharedMaterial = tankM; cap.isStatic = true; }
            for (int i = 0; i < 6; i++) AssetCatalog.Prop("propane_tank", "polyhaven", T, new Vector3(100 + i * 5, 0, -118), R.Next(0, 360), 1.3f);
            // водонапорная башня
            { var leg = Buildings.Metal; foreach (var (lx, lz) in new[] { (-3f, -3f), (3f, -3f), (3f, 3f), (-3f, 3f) }) MeshKit.Box(T, new Vector3(-60 + lx, 9, 180 + lz), new Vector3(0.5f, 18, 0.5f), leg); var wt = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wt.transform.SetParent(T, false); wt.transform.position = new Vector3(-60, 21, 180); wt.transform.localScale = new Vector3(9, 3.5f, 9); wt.GetComponent<Renderer>().sharedMaterial = tankM; wt.isStatic = true; }
            MeshKit.Box(T, new Vector3(100, 3.5f, -60), new Vector3(0.6f, 0.6f, 120), Buildings.Metal); for (int i = 0; i < 6; i++) MeshKit.Box(T, new Vector3(100, 1.6f, -110 + i * 20), new Vector3(0.4f, 3.2f, 0.4f), Buildings.Metal);
            for (int i = 0; i < 14; i++) Container(new Vector3(Rnd(-220, -60), 0, Rnd(-200, -150)), Chance(0.5) ? 0 : 90);
            var crane = AssetCatalog.TexMat("metal", 2f, new Color(0.9f, 0.6f, 0.1f), new Color(0.85f, 0.6f, 0.1f));
            MeshKit.Box(T, new Vector3(-140, 14, -175), new Vector3(60, 1.5f, 1.5f), crane); MeshKit.Box(T, new Vector3(-168, 7, -175), new Vector3(1.2f, 14, 1.2f), crane); MeshKit.Box(T, new Vector3(-112, 7, -175), new Vector3(1.2f, 14, 1.2f), crane);
            for (int i = 0; i < 30; i++) { var p = new Vector3(Rnd(-half + 10, half - 10), 0, Rnd(-half + 10, half - 10)); if (Free(p, 1.5f)) { if (Model(Chance(0.5) ? "barrel" : "box", "survival", p, R.Next(0, 360), 1.0f) == null) { var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); b.transform.SetParent(T, false); b.transform.position = p + Vector3.up * 0.45f; b.transform.localScale = new Vector3(0.7f, 0.45f, 0.7f); b.GetComponent<Renderer>().sharedMaterial = Buildings.Metal; b.isStatic = true; } } }
            for (int i = 0; i < 18; i++) { var p = new Vector3(Rnd(-half + 10, half - 10), 0, Rnd(-half + 10, half - 10)); if (Free(p, 1.2f)) Crate(p, Chance(0.3) ? "body" : Chance(0.5) ? "crate" : "toolbox"); }
            for (int i = 0; i < 6; i++) OsmMapBuilder.Car(T, new Vector3(Rnd(-120, 120), 0, -150 + Rnd(-3, 3)), 90, R);
            Buildings.Make(Buildings.Rect(225, -215, 10, 8), 1, "Проходная", new Color(0.7f, 0.7f, 0.7f), true, "office");
            if (World.Night) for (int i = 0; i < 16; i++) World.Lamp(new Vector3(Rnd(-half, half), 6, Rnd(-half, half)), new Color(1f, 0.8f, 0.5f), 20, 1.8f);
            Spawns(half - 15);
            World.BossSpawn = new Vector3(0, 0.1f, 0);
        }

        // ================================================================ ПОСЁЛОК
        public static void BuildSuburb()
        {
            float half = World.Half;
            MeshKit.Ground(T, World.Map.size + 200, "grass", 3f, null);
            var road = AssetCatalog.TexMat("asphalt", 5f, null, new Color(0.3f, 0.3f, 0.3f));
            var dirt = AssetCatalog.TexMat("ground", 3f, null, new Color(0.45f, 0.4f, 0.32f));
            Road(new List<Vector2> { new Vector2(-half, half), new Vector2(-200, 150), new Vector2(0, 0), new Vector2(half, -half) }, 7, road);
            for (int i = 0; i < 4; i++) Road(new List<Vector2> { new Vector2(-80 + i * 90, -half + 60), new Vector2(-80 + i * 90, 160) }, 5, dirt);
            for (int i = 0; i < 4; i++) Road(new List<Vector2> { new Vector2(-120, -300 + i * 110), new Vector2(230, -300 + i * 110) }, 5, dirt);
            var fenceM = AssetCatalog.TexMat("woodplanks", 1.5f, null, new Color(0.5f, 0.4f, 0.3f));
            int n = 0;
            for (int gx = 0; gx < 4; gx++) for (int gz = 0; gz < 4; gz++) for (int k = 0; k < 2; k++)
                    {
                        float cx = -80 + gx * 90 + (k == 0 ? -25 : 25) + Rnd(-5, 5), cz = -300 + gz * 110 + 50 + Rnd(-10, 10);
                        if (Mathf.Abs(cx - 150) < 30 && Mathf.Abs(cz + 150) < 30) continue;
                        n++;
                        // дома: кирпич/штукатурка, окна с рамами, двускатная крыша, полый первый этаж с лутом
                        Buildings.Make(Buildings.Rect(cx, cz, Rnd(10, 14), Rnd(8, 11), Rnd(-8, 8)), Chance(0.45) ? 2 : 1, "Дом" + n, Buildings.ColorFor(n * 31), true, "residential");
                        if (Chance(0.5)) Crate(new Vector3(cx + Rnd(-6, 6), 0.1f, cz - 9), Chance(0.5) ? "bag" : "cabinet");
                        if (Chance(0.4)) AssetCatalog.Prop(Chance(0.5) ? "plastic_monobloc_chair_01" : "WoodenTable_02", "polyhaven", T, new Vector3(cx + Rnd(-7, 7), 0, cz + 8), R.Next(0, 360), 1.0f);
                        float fx = 20, fz = 22; var c = new Vector2(cx, cz);
                        MeshKit.Wall(T, c + new Vector2(-fx, -fz), c + new Vector2(-2, -fz), 1.3f, 0.1f, fenceM); MeshKit.Wall(T, c + new Vector2(2, -fz), c + new Vector2(fx, -fz), 1.3f, 0.1f, fenceM);
                        MeshKit.Wall(T, c + new Vector2(fx, -fz), c + new Vector2(fx, fz), 1.3f, 0.1f, fenceM); MeshKit.Wall(T, c + new Vector2(fx, fz), c + new Vector2(-fx, fz), 1.3f, 0.1f, fenceM); MeshKit.Wall(T, c + new Vector2(-fx, fz), c + new Vector2(-fx, -fz), 1.3f, 0.1f, fenceM);
                        if (Chance(0.6)) Buildings.Make(Buildings.Rect(cx + 13, cz + 11, 6, 5), 1, "Гараж", new Color(0.5f, 0.5f, 0.55f), true, "garage", null, null, 2.8f);
                        if (Chance(0.6)) MeshKit.Tree(T, new Vector3(cx - 14, 0, cz + Rnd(-10, 10)), R);
                        if (Chance(0.4)) OsmMapBuilder.Car(T, new Vector3(cx + Rnd(-8, 8), 0, cz - 17), Rnd(0, 360), R);
                        if (Chance(0.4)) Model("planter", "suburban", new Vector3(cx + Rnd(-10, 10), 0, cz + 14), 0, 1.5f);
                    }
            Buildings.Make(Buildings.Rect(0, 0, 22, 12), 1, "Магазин", new Color(0.8f, 0.5f, 0.3f), true, "shop", null, null, 3.6f);
            Buildings.Make(Buildings.Rect(150, -150, 22, 16), 2, "Коттедж", new Color(0.9f, 0.88f, 0.8f), true, "residential", "key_suburb_cottage", "кабинет");
            MeshKit.Wall(T, new Vector2(120, -180), new Vector2(180, -180), 2.2f, 0.2f, Buildings.Bricks); MeshKit.Wall(T, new Vector2(180, -180), new Vector2(180, -120), 2.2f, 0.2f, Buildings.Bricks);
            Buildings.Make(Buildings.Rect(-120, -200, 14, 9), 1, "Большой гараж", new Color(0.45f, 0.45f, 0.5f), true, "garage", "key_suburb_garage", "бокс", 3.5f);
            Buildings.Make(Buildings.Rect(-150, 100, 16, 24), 3, "Церковь", new Color(0.95f, 0.95f, 0.9f), true, "theatre");
            World.SniperSpots.Add(new Vector3(-150, 3 * Buildings.FloorH + 0.5f, 100));
            Buildings.Make(Buildings.Rect(-300, 300, 14, 10), 1, "Лесничество", new Color(0.5f, 0.35f, 0.2f), true, "industrial");
            var lake = new List<Vector2>(); for (int i = 0; i < 24; i++) { float a = i / 24f * Mathf.PI * 2; lake.Add(new Vector2(300 + Mathf.Cos(a) * Rnd(55, 70), 250 + Mathf.Sin(a) * Rnd(45, 60))); }
            MeshKit.Slab(T, lake, 0.0f, 0.02f, AssetCatalog.Mat(new Color(0.2f, 0.45f, 0.7f), 0.9f), false, "lake");
            var sand = new List<Vector2>(); for (int i = 0; i < 24; i++) { float a = i / 24f * Mathf.PI * 2; sand.Add(new Vector2(300 + Mathf.Cos(a) * 78, 250 + Mathf.Sin(a) * 68)); }
            MeshKit.Slab(T, sand, -0.01f, 0.02f, AssetCatalog.TexMat("sand", 2f, null, new Color(0.8f, 0.75f, 0.55f)), false, "beach");
            int trees = 0;
            for (int i = 0; i < 700 && trees < 320; i++)
            {
                var p = new Vector3(Rnd(-half + 5, half - 5), 0, Rnd(-half + 5, half - 5));
                bool forest = (p.x < -130 && p.z > 60) || Mathf.Abs(p.x) > half - 60 || Mathf.Abs(p.z) > half - 60;
                if (!forest || !Free(p, 2.5f)) continue;
                MeshKit.Tree(T, p, R); trees++;
            }
            for (int i = 0; i < 40; i++) { var p = new Vector3(Rnd(-half + 5, half - 5), 0, Rnd(-half + 5, half - 5)); if (Free(p, 2f)) Model(Chance(0.5) ? "rock-a" : "rock-b", "survival", p, R.Next(0, 360), Rnd(1.5f, 3f)); }
            foreach (var p in new[] { new Vector3(-250, 0, -250), new Vector3(300, 0, 100) })
            {
                var wood = AssetCatalog.TexMat("woodplanks", 1.5f, null, new Color(0.4f, 0.3f, 0.2f));
                MeshKit.Box(T, p + new Vector3(0, 7.2f, 0), new Vector3(4, 0.3f, 4), wood);
                for (int i = 0; i < 4; i++) MeshKit.Box(T, p + new Vector3(i < 2 ? -1.8f : 1.8f, 3.6f, i % 2 == 0 ? -1.8f : 1.8f), new Vector3(0.3f, 7.2f, 0.3f), wood);
                Buildings.Ramp(T, p + new Vector3(2, 0, -12), p + new Vector3(2, 7.3f, -2), 1.5f, wood);
                World.SniperSpots.Add(p + Vector3.up * 7.6f);
                Crate(p + new Vector3(0, 7.4f, 0), "weaponbox");
            }
            for (int i = 0; i < 15; i++) { var p = new Vector3(Rnd(-half + 10, half - 10), 0, Rnd(-half + 10, half - 10)); if (Free(p, 1.2f)) Crate(p, Chance(0.4) ? "body" : "bag"); }
            Spawns(half - 15);
            World.BossSpawn = new Vector3(-300, 0.1f, 300);
        }

        // ================================================================ ТОРГОВЫЙ ЦЕНТР
        public static void BuildMall()
        {
            float half = World.Half;
            MeshKit.Ground(T, World.Map.size + 200, "asphalt", 6f, new Color(0.5f, 0.5f, 0.52f));
            float W = 180, D = 120, fh = 4.5f; int floors = 3;
            var wall = AssetCatalog.TexMat("plaster", 3f, null, new Color(0.85f, 0.85f, 0.88f)); var floorM = AssetCatalog.TexMat("tiles", 1.5f, null, new Color(0.75f, 0.72f, 0.68f)); var shopM = AssetCatalog.TexMat("plaster", 2f, new Color(0.8f, 0.85f, 1f), new Color(0.6f, 0.6f, 0.7f), 1);
            var outer = Buildings.Rect(0, 0, W, D);
            var facade = AssetCatalog.TexMat("facade", 7f, null, new Color(0.85f, 0.85f, 0.88f), 2);
            for (int f = 0; f < floors; f++)
            {
                float y = f * fh;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 a = outer[i], b = outer[(i + 1) % 4];
                    if (f == 0 && (i == 0 || i == 2)) { var dir = (b - a).normalized; var mid = (a + b) * 0.5f; MeshKit.Wall(T, a, mid - dir * 4, fh, 0.4f, facade, y); MeshKit.Wall(T, mid + dir * 4, b, fh, 0.4f, facade, y); }
                    else MeshKit.Wall(T, a, b, fh, 0.4f, facade, y);
                }
                if (f > 0)
                {
                    float ax = 30, az = 20;
                    MeshKit.Slab(T, new List<Vector2> { new Vector2(-W / 2, -D / 2), new Vector2(W / 2, -D / 2), new Vector2(W / 2, -az), new Vector2(-W / 2, -az) }, y - 0.3f, 0.3f, floorM, true, "floorS");
                    MeshKit.Slab(T, new List<Vector2> { new Vector2(-W / 2, az), new Vector2(W / 2, az), new Vector2(W / 2, D / 2), new Vector2(-W / 2, D / 2) }, y - 0.3f, 0.3f, floorM, true, "floorN");
                    MeshKit.Slab(T, new List<Vector2> { new Vector2(-W / 2, -az), new Vector2(-ax, -az), new Vector2(-ax, az), new Vector2(-W / 2, az) }, y - 0.3f, 0.3f, floorM, true, "floorW");
                    MeshKit.Slab(T, new List<Vector2> { new Vector2(ax, -az), new Vector2(W / 2, -az), new Vector2(W / 2, az), new Vector2(ax, az) }, y - 0.3f, 0.3f, floorM, true, "floorE");
                    MeshKit.Wall(T, new Vector2(-ax, -az), new Vector2(ax, -az), 1.1f, 0.1f, Buildings.Metal, y); MeshKit.Wall(T, new Vector2(-ax, az), new Vector2(ax, az), 1.1f, 0.1f, Buildings.Metal, y);
                    MeshKit.Wall(T, new Vector2(-ax, -az), new Vector2(-ax, az), 1.1f, 0.1f, Buildings.Metal, y); MeshKit.Wall(T, new Vector2(ax, -az), new Vector2(ax, az), 1.1f, 0.1f, Buildings.Metal, y);
                }
                else MeshKit.Slab(T, outer, -0.05f, 0.1f, floorM, true, "floor0");
                for (int s = 0; s < 8; s++)
                {
                    float cx = -W / 2 + 12 + s * 22; if (f == 0 && s >= 3 && s <= 4) continue;
                    Buildings.Room(T, cx, -D / 2 + 9, 18, 16, fh - 0.3f, shopM, new[] { 2 }, 3f, y);
                    Buildings.Room(T, cx, D / 2 - 9, 18, 16, fh - 0.3f, shopM, new[] { 0 }, 3f, y);
                    foreach (var cz in new[] { -D / 2 + 9, D / 2 - 9 })
                    {
                        World.AddContainer(new Vector3(cx + Rnd(-6, 6), y + 0.1f, cz + Rnd(-5, 5)), Quaternion.identity, Chance(0.5) ? "shelf" : Chance(0.5) ? "register" : "cabinet");
                        if (Chance(0.5)) World.AddContainer(new Vector3(cx + Rnd(-6, 6), y + 0.1f, cz + Rnd(-5, 5)), Quaternion.identity, "shelf");
                        Model(Chance(0.5) ? "table" : "bookcase", "furniture", new Vector3(cx + Rnd(-6, 6), y + 0.1f, cz + Rnd(-5, 5)), R.Next(0, 4) * 90, 1.6f);
                    }
                }
                if (f < floors) { Buildings.Ramp(T, new Vector3(-W / 2 + 6, y + 0.05f, -D / 2 + 30), new Vector3(-W / 2 + 6, y + fh, -D / 2 + 30 + 26), 3f, floorM); Buildings.Ramp(T, new Vector3(W / 2 - 6, y + 0.05f, D / 2 - 30), new Vector3(W / 2 - 6, y + fh, D / 2 - 30 - 26), 3f, floorM); }
                for (int i = 0; i < 10; i++) { var pnl = MeshKit.Box(T, new Vector3(-W / 2 + 10 + i * 18, y + fh - 0.2f, 0), new Vector3(6, 0.1f, 2), Color.white, false); pnl.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Emissive(new Color(1f, 0.97f, 0.9f), 1.2f); }
                World.Lamp(new Vector3(-50, y + fh - 0.6f, 0), new Color(1f, 0.95f, 0.85f), 30, 1.5f); World.Lamp(new Vector3(50, y + fh - 0.6f, 0), new Color(1f, 0.95f, 0.85f), 30, 1.5f);
            }
            MeshKit.Slab(T, outer, floors * fh, 0.4f, Buildings.Concrete, true, "roof");
            for (int i = 0; i < 4; i++) MeshKit.Wall(T, outer[i], outer[(i + 1) % 4], 1.2f, 0.3f, Buildings.Concrete, floors * fh + 0.4f);
            for (int i = 0; i < 10; i++) { World.AddContainer(new Vector3(Rnd(-25, 25), 0.1f, Rnd(-15, 15)), Quaternion.Euler(0, R.Next(0, 360), 0), Chance(0.5) ? "fridge" : "desk"); Model(Chance(0.5) ? "table" : "chair", "furniture", new Vector3(Rnd(-25, 25), 0.1f, Rnd(-15, 15)), R.Next(0, 360), 1.3f); }
            var gold = AssetCatalog.TexMat("marble", 2f, new Color(1f, 0.9f, 0.6f), new Color(0.7f, 0.6f, 0.3f));
            Buildings.Room(T, -60, -40, 20, 14, fh - 0.3f, gold, new int[0]); World.AddDoor(new Vector3(-60, 0, -33), 0, 2.4f, "key_mall_jewelry", "ювелирный");
            MeshKit.Wall(T, new Vector2(-70, -33), new Vector2(-61.2f, -33), fh - 0.3f, 0.3f, gold); MeshKit.Wall(T, new Vector2(-58.8f, -33), new Vector2(-50, -33), fh - 0.3f, 0.3f, gold);
            World.AddContainer(new Vector3(-64, 0.1f, -42), Quaternion.identity, "safe"); World.AddContainer(new Vector3(-56, 0.1f, -42), Quaternion.identity, "safe"); World.AddContainer(new Vector3(-60, 0.1f, -36), Quaternion.identity, "register");
            Buildings.Room(T, 60, -40, 20, 14, fh - 0.3f, shopM, new[] { 2 }); for (int i = 0; i < 4; i++) World.AddContainer(new Vector3(52 + i * 5, 0.1f, -44), Quaternion.identity, Chance(0.5) ? "server" : "shelf");
            Buildings.Room(T, -70, 30, 30, 24, fh - 0.3f, shopM, new[] { 1 }); for (int r = 0; r < 3; r++) for (int i = 0; i < 3; i++) { MeshKit.Box(T, new Vector3(-80 + i * 10, 1, 22 + r * 8), new Vector3(6, 2, 0.8f), Buildings.Metal); World.AddContainer(new Vector3(-80 + i * 10, 0.1f, 23 + r * 8), Quaternion.identity, Chance(0.6) ? "shelf" : "fridge"); }
            var cinema = AssetCatalog.TexMat("carpet", 2f, new Color(0.5f, 0.2f, 0.25f), new Color(0.25f, 0.1f, 0.12f));
            Buildings.Room(T, 0, -40, 40, 24, fh - 0.3f, cinema, new[] { 2 }, 3f, 2 * fh); for (int i = 0; i < 5; i++) MeshKit.Box(T, new Vector3(-12 + i * 6, 2 * fh + 0.5f, -45), new Vector3(4, 1, 1.5f), AssetCatalog.TexMat("fabric", 1f, new Color(0.8f, 0.2f, 0.2f), new Color(0.5f, 0.1f, 0.1f)));
            World.AddContainer(new Vector3(0, 2 * fh + 0.1f, -48), Quaternion.identity, "jacket"); World.AddContainer(new Vector3(10, 2 * fh + 0.1f, -36), Quaternion.identity, "bag");
            Buildings.Room(T, 70, 0, 14, 12, fh - 0.3f, Buildings.Concrete, new int[0], 2.4f, fh); World.AddDoor(new Vector3(63, fh, 0), 90, 2.4f, "key_mall_security", "охрана");
            MeshKit.Wall(T, new Vector2(63, -6), new Vector2(63, -1.2f), fh - 0.3f, 0.3f, Buildings.Concrete, fh); MeshKit.Wall(T, new Vector2(63, 1.2f), new Vector2(63, 6), fh - 0.3f, 0.3f, Buildings.Concrete, fh);
            World.AddContainer(new Vector3(72, fh + 0.1f, 3), Quaternion.identity, "weaponbox"); World.AddContainer(new Vector3(72, fh + 0.1f, -3), Quaternion.identity, "locker"); World.AddContainer(new Vector3(68, fh + 0.1f, 0), Quaternion.identity, "server");
            for (int i = 0; i < 18; i++) OsmMapBuilder.Car(T, new Vector3(-100 + i * 11, 0, -95 + (i % 2) * 8), 0, R);
            for (int i = 0; i < 6; i++) OsmMapBuilder.Car(T, new Vector3(-110, 0, 70 + i * 7), 90, R);
            for (int i = 0; i < 12; i++) { var p = new Vector3(Rnd(-half + 8, half - 8), 0, Rnd(-half + 8, half - 8)); if (Mathf.Abs(p.x) < W / 2 + 3 && Mathf.Abs(p.z) < D / 2 + 3) continue; if (Free(p, 1.2f)) Crate(p, Chance(0.4) ? "body" : "crate"); }
            for (int i = 0; i < 10; i++) MeshKit.Tree(T, new Vector3(Rnd(-half + 8, half - 8), 0, D / 2 + Rnd(10, 25)), R);
            Spawns(half - 10);
            World.BossSpawn = new Vector3(0, 0.1f, 0);
            for (int i = 0; i < 40; i++) World.EnemySpawns.Add(new Vector3(Rnd(-W / 2 + 10, W / 2 - 10), (R.Next(0, floors)) * fh + 0.2f, Rnd(-D / 2 + 10, D / 2 - 10)));
        }

        // ================================================================ ПОДЗЕМКА
        public static void BuildMetro()
        {
            float half = World.Half; float h = 5f;
            MeshKit.Ground(T, World.Map.size + 50, "concrete", 2.5f, new Color(0.6f, 0.6f, 0.62f));
            var wall = AssetCatalog.TexMat("concrete", 2.5f, new Color(0.7f, 0.72f, 0.78f), new Color(0.3f, 0.32f, 0.36f), 1); var tile = AssetCatalog.TexMat("tiles", 1f, null, new Color(0.55f, 0.6f, 0.62f), 1);
            var tunnelM = AssetCatalog.TexMat("bricks", 2f, new Color(0.6f, 0.6f, 0.65f), new Color(0.2f, 0.2f, 0.22f), 1);
            MeshKit.Slab(T, Buildings.Rect(0, 0, World.Map.size + 50, World.Map.size + 50), h + 3f, 1f, AssetCatalog.Mat(new Color(0.1f, 0.1f, 0.11f)), false, "cap");
            Buildings.Room(T, 0, 0, 64, 32, h, tile, new[] { 0, 1, 2, 3 }, 8f, 0, true, Buildings.Concrete);
            MeshKit.Box(T, new Vector3(0, -0.6f, 0), new Vector3(64, 0.2f, 8), AssetCatalog.TexMat("gravel", 2f, null, new Color(0.12f, 0.12f, 0.12f)));
            var platform = AssetCatalog.TexMat("marble", 2f, null, new Color(0.55f, 0.6f, 0.62f));
            MeshKit.Box(T, new Vector3(0, 0.4f, 10), new Vector3(64, 0.8f, 12), platform); MeshKit.Box(T, new Vector3(0, 0.4f, -10), new Vector3(64, 0.8f, 12), platform);
            for (int i = 0; i < 6; i++) MeshKit.Box(T, new Vector3(-25 + i * 10, h * 0.5f, 0), new Vector3(1.2f, h, 1.2f), platform);
            for (int i = 0; i < 8; i++) World.AddContainer(new Vector3(Rnd(-28, 28), 0.9f, Chance(0.5) ? 10 : -10), Quaternion.identity, Chance(0.5) ? "bag" : Chance(0.5) ? "body" : "crate");
            Buildings.LockedRoom(T, new Vector3(26, 0.9f, 11), "key_metro_control", "диспетчерская", h - 1f, R, "server");
            Buildings.Corridor(T, new List<Vector2> { new Vector2(32, 0), new Vector2(95, 0), new Vector2(120, 40) }, 8, h, tunnelM, true);
            Buildings.Corridor(T, new List<Vector2> { new Vector2(-32, 0), new Vector2(-120, 0), new Vector2(-180, 60), new Vector2(-180, 100) }, 8, h, tunnelM, true);
            Buildings.Corridor(T, new List<Vector2> { new Vector2(0, -16), new Vector2(0, -60), new Vector2(-40, -120), new Vector2(0, -170) }, 7, h, tunnelM, true);
            Buildings.Corridor(T, new List<Vector2> { new Vector2(0, 16), new Vector2(0, 60), new Vector2(60, 90), new Vector2(180, 100) }, 6, h, tunnelM, true);
            Buildings.Corridor(T, new List<Vector2> { new Vector2(-32, -4), new Vector2(-80, -40), new Vector2(-120, -60) }, 5, h - 1, tunnelM, true);
            Buildings.Room(T, 0, -190, 40, 24, h, tile, new[] { 2 }, 7f, 0, true, Buildings.Concrete);
            for (int i = 0; i < 5; i++) World.AddContainer(new Vector3(Rnd(-15, 15), 0.1f, Rnd(-200, -182)), Quaternion.identity, Chance(0.5) ? "locker" : "crate");
            Buildings.Room(T, 120, 60, 50, 30, h + 2, wall, new[] { 0 }, 8f, 0, true, Buildings.Concrete);
            MeshKit.Box(T, new Vector3(120, 1.9f, 60), new Vector3(22, 3.4f, 3.2f), AssetCatalog.TexMat("metal", 2f, new Color(0.3f, 0.5f, 0.8f), new Color(0.2f, 0.35f, 0.5f)));
            for (int i = 0; i < 6; i++) World.AddContainer(new Vector3(Rnd(100, 140), 0.1f, Rnd(48, 72)), Quaternion.identity, Chance(0.5) ? "toolbox" : "weaponbox");
            for (int i = 0; i < 6; i++) Model(Chance(0.5) ? "barrel" : "box", "survival", new Vector3(Rnd(100, 140), 0.1f, Rnd(48, 72)), R.Next(0, 360), 1.0f);
            Buildings.Room(T, 180, 100, 10, 10, h, wall, new[] { 3 }, 4f, 0, true, Buildings.Concrete);
            Buildings.Room(T, -180, 100, 12, 10, h, wall, new[] { 0 }, 4f, 0, true, Buildings.Concrete); MeshKit.Box(T, new Vector3(-180, 0.7f, 102), new Vector3(3, 1.2f, 2), AssetCatalog.TexMat("rust", 1f, null, new Color(0.5f, 0.3f, 0.2f)));
            var bunker = AssetCatalog.TexMat("concrete", 2f, new Color(0.7f, 0.8f, 0.7f), new Color(0.35f, 0.4f, 0.35f), 0);
            Buildings.Room(T, -120, -60, 40, 30, h - 1, bunker, new[] { 1 }, 4f, 0, true, Buildings.Concrete);
            for (int i = 0; i < 3; i++) { Buildings.Room(T, -132 + i * 12, -68, 10, 10, h - 1.2f, bunker, new[] { 2 }, 2.2f); World.AddContainer(new Vector3(-132 + i * 12, 0.1f, -71), Quaternion.identity, i == 1 ? "medbag" : "locker"); Model("bedroll", "survival", new Vector3(-134 + i * 12, 0.1f, -66), 0, 2f); }
            Buildings.LockedRoom(T, new Vector3(-80, 0.1f, 80), "key_metro_armory", "оружейная", h - 1f, R, "weaponbox");
            World.AddContainer(new Vector3(-128, 0.1f, -52), Quaternion.identity, "safe"); World.AddContainer(new Vector3(-110, 0.1f, -52), Quaternion.identity, "server");
            foreach (var p in new[] { new Vector3(-20, h - 0.5f, 10), new Vector3(20, h - 0.5f, -10), new Vector3(0, h - 0.5f, -190), new Vector3(120, h + 1, 60), new Vector3(-120, h - 1.5f, -60), new Vector3(60, h - 0.5f, 0), new Vector3(-80, h - 0.5f, 0), new Vector3(0, h - 0.5f, -100), new Vector3(60, h - 0.5f, 90), new Vector3(180, h - 0.5f, 100), new Vector3(-180, h - 0.5f, 100), new Vector3(-180, h - 0.5f, 60), new Vector3(100, h - 0.5f, 20) })
                World.Lamp(p, Chance(0.3) ? new Color(1f, 0.4f, 0.3f) : new Color(0.9f, 0.9f, 1f), 16, 1.4f);
            foreach (var p in new[] { new Vector3(15, 0, -196), new Vector3(120, 0, 95), new Vector3(-180, 0, 70), new Vector3(135, 0, 70), new Vector3(-135, 0, -70), new Vector3(-100, 0, -5) }) World.PlayerSpawns.Add(World.Snap(p));
            foreach (var p in new[] { new Vector3(0, 0, 0), new Vector3(0, 0, -60), new Vector3(60, 0, 0), new Vector3(-60, 0, 0), new Vector3(0, 0, -150), new Vector3(110, 0, 50), new Vector3(-120, 0, -55), new Vector3(60, 0, 90), new Vector3(-80, 0, -40), new Vector3(-150, 0, 40), new Vector3(100, 0, 20), new Vector3(20, 0, 60) }) { World.EnemySpawns.Add(World.Snap(p)); World.EnemySpawns.Add(World.Snap(p + new Vector3(3, 0, 2))); }
            World.BossSpawn = World.Snap(new Vector3(120, 0, 60));
        }

        // ================================================================ ЗАПАСНОЙ ГОРОД (если нет OSM-файла)
        public static void BuildCityFallback(MapDef map)
        {
            float half = World.Half;
            MeshKit.Ground(T, map.size + 200, "paving", 2f, null);
            var road = AssetCatalog.TexMat("asphalt", 5f, null, new Color(0.25f, 0.25f, 0.26f));
            int blocks = Mathf.Max(3, (int)(map.size / 120)); float step = map.size / blocks;
            for (int i = 0; i <= blocks; i++) { float c = -half + i * step; Road(new List<Vector2> { new Vector2(-half, c), new Vector2(half, c) }, 10, road); Road(new List<Vector2> { new Vector2(c, -half), new Vector2(c, half) }, 10, road); }
            var keys = Database.Items.Values.Where(i => i.type == "key" && i.PS("map") == map.id).ToList(); int k = 0, n = 0;
            string[] comm = { "building-a", "building-b", "building-c", "building-d", "building-e", "building-f", "building-g", "building-h", "building-skyscraper-a", "building-skyscraper-b" };
            for (int bx = 0; bx < blocks; bx++) for (int bz = 0; bz < blocks; bz++)
                {
                    float cx = -half + (bx + 0.5f) * step, cz = -half + (bz + 0.5f) * step;
                    int per = R.Next(1, 4);
                    for (int i = 0; i < per; i++)
                    {
                        float ox = Rnd(-step * 0.25f, step * 0.25f), oz = Rnd(-step * 0.25f, step * 0.25f);
                        if (Chance(0.5) && Model(comm[R.Next(comm.Length)], "commercial", new Vector3(cx + ox, 0, cz + oz), R.Next(0, 4) * 90, Rnd(18, 40)) != null) { Crate(new Vector3(cx + ox + 12, 0.1f, cz + oz), Chance(0.5) ? "crate" : "jacket"); continue; }
                        float w = Rnd(14, step * 0.4f), d = Rnd(12, step * 0.4f);
                        string key = k < keys.Count && Chance(0.25) ? keys[k++].id : null;
                        Buildings.Make(Buildings.Rect(cx + ox, cz + oz, w, d), R.Next(1, 7), "Дом" + n++, Buildings.ColorFor(n * 17), true, Chance(0.3) ? "shop" : Chance(0.5) ? "residential" : "office", key, key != null ? Database.NameOf(key).Replace("Ключ от ", "") : null);
                    }
                    if (Chance(0.3)) MeshKit.Tree(T, new Vector3(cx + Rnd(-20, 20), 0, cz + Rnd(-20, 20)), R);
                }
            for (int i = 0; i < 20; i++) OsmMapBuilder.Car(T, new Vector3(Rnd(-half + 10, half - 10), 0, -half + R.Next(0, blocks + 1) * step + 4), 90, R);
            Spawns(half - 15);
        }
    }
}
