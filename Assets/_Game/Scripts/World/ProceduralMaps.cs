using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Процедурные карты: завод, посёлок, торговый центр, подземка, запасной город.</summary>
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

        static void Fence(float half, float h, Color c, float gap = 14f)
        {
            // периметр с проёмами посередине каждой стороны и по углам (для выходов)
            float s = half;
            foreach (var (a, b) in new[] { (new Vector2(-s, -s), new Vector2(s, -s)), (new Vector2(s, -s), new Vector2(s, s)), (new Vector2(s, s), new Vector2(-s, s)), (new Vector2(-s, s), new Vector2(-s, -s)) })
            {
                var dir = (b - a).normalized; float len = (b - a).magnitude;
                MeshKit.Wall(T, a + dir * gap, a + dir * (len * 0.5f - gap * 0.5f), h, 0.2f, c);
                MeshKit.Wall(T, a + dir * (len * 0.5f + gap * 0.5f), b - dir * gap, h, 0.2f, c);
            }
        }

        static void Crate(Vector3 p, string kind = "crate") => World.AddContainer(p, Quaternion.Euler(0, R.Next(0, 360), 0), kind);

        static void Shipping(Vector3 p, float yaw)
        {
            var c = new Color(Rnd(0.2f, 0.9f), Rnd(0.2f, 0.7f), Rnd(0.2f, 0.7f));
            MeshKit.Box(T, p + Vector3.up * 1.3f, new Vector3(2.5f, 2.6f, 12f), c, true, true, Quaternion.Euler(0, yaw, 0));
            if (Chance(0.4)) Crate(p + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, 7f));
        }

        // ================================================================ ЗАВОД
        public static void BuildIndustrial()
        {
            float half = World.Half;
            MeshKit.Ground(T, World.Map.size + 200, new Color(0.38f, 0.38f, 0.37f), 5f);
            Fence(half - 4, 2.6f, new Color(0.45f, 0.45f, 0.45f));
            var road = new Color(0.25f, 0.25f, 0.26f);
            for (int i = -1; i <= 1; i++) { MeshKit.Strip(T, new List<Vector2> { new Vector2(-half, i * 150), new Vector2(half, i * 150) }, 8, 0.04f, road); MeshKit.Strip(T, new List<Vector2> { new Vector2(i * 150, -half), new Vector2(i * 150, half) }, 8, 0.04f, road); }
            // ж/д
            MeshKit.Strip(T, new List<Vector2> { new Vector2(-half, -230), new Vector2(half, -230) }, 4, 0.05f, new Color(0.3f, 0.25f, 0.2f));
            for (float x = -half; x < half; x += 1.2f) MeshKit.Box(T, new Vector3(x, 0.08f, -230), new Vector3(0.3f, 0.12f, 3f), new Color(0.35f, 0.25f, 0.15f), false);
            for (int i = 0; i < 5; i++) { MeshKit.Box(T, new Vector3(-180 + i * 16, 1.8f, -230), new Vector3(14, 3, 3), new Color(0.4f, 0.25f, 0.2f)); if (Chance(0.5)) Crate(new Vector3(-180 + i * 16, 3.3f, -230), "crate"); }
            var steel = new Color(0.55f, 0.57f, 0.6f);
            // сборочный цех (0,0): большой высокий зал с колоннами и верстаками
            Buildings.Make(Buildings.Rect(0, 0, 70, 36), 1, "Сборочный цех", new Color(0.6f, 0.62f, 0.65f), true, "industrial", null, null, 9f);
            for (int i = 0; i < 6; i++) Crate(new Vector3(-28 + i * 11, 0.1f, Rnd(-12, 12)), Chance(0.5) ? "toolbox" : "crate");
            for (int i = 0; i < 3; i++) MeshKit.Box(T, new Vector3(-20 + i * 20, 1.2f, 0), new Vector3(6, 2.4f, 2.5f), new Color(0.3f, 0.5f, 0.35f)); // станки
            // склад готовой продукции (-150,100): стеллажи
            Buildings.Make(Buildings.Rect(-150, 100, 44, 26), 1, "Склад", new Color(0.55f, 0.5f, 0.45f), true, "industrial", "key_factory_tools", "инструменталка", 7f);
            for (int r = 0; r < 3; r++) for (int i = 0; i < 4; i++) { MeshKit.Box(T, new Vector3(-165 + i * 10, 2f, 90 + r * 10), new Vector3(6, 4, 1.2f), steel); World.AddContainer(new Vector3(-165 + i * 10, 0.1f, 91.2f + r * 10), Quaternion.identity, "shelf"); }
            // котельная (150,100) с трубой
            Buildings.Make(Buildings.Rect(150, 100, 24, 18), 2, "Котельная", new Color(0.5f, 0.35f, 0.3f), true, "industrial", null, null, 4f);
            var chimney = GameObject.CreatePrimitive(PrimitiveType.Cylinder); chimney.transform.SetParent(T, false); chimney.transform.position = new Vector3(165, 20, 110); chimney.transform.localScale = new Vector3(3, 20, 3); chimney.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Mat(new Color(0.4f, 0.3f, 0.28f)); chimney.isStatic = true;
            // гаражи (-150,-120)
            for (int i = 0; i < 8; i++) { Buildings.Make(Buildings.Rect(-185 + i * 10, -120, 9, 7), 1, "Гараж" + i, new Color(0.45f, 0.45f, 0.5f), true, "garage", i == 3 ? null : null, null, 3f); }
            // администрация (0,80) — 3 этажа, кабинет директора заперт
            Buildings.Make(Buildings.Rect(0, 80, 30, 14), 3, "Администрация", new Color(0.8f, 0.75f, 0.65f), true, "office", "key_factory_director", "кабинет директора");
            World.SniperSpots.Add(new Vector3(0, 3 * Buildings.FloorH + 0.5f, 80));
            // резервуары и трубы
            for (int i = 0; i < 4; i++)
            {
                var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder); tank.transform.SetParent(T, false); tank.transform.position = new Vector3(120 + i * 18, 6, -100); tank.transform.localScale = new Vector3(12, 6, 12);
                tank.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Mat(new Color(0.7f, 0.7f, 0.68f)); tank.isStatic = true;
            }
            MeshKit.Box(T, new Vector3(100, 3.5f, -60), new Vector3(0.6f, 0.6f, 120), steel); for (int i = 0; i < 6; i++) MeshKit.Box(T, new Vector3(100, 1.6f, -110 + i * 20), new Vector3(0.4f, 3.2f, 0.4f), steel);
            // контейнеры, краны, бочки
            for (int i = 0; i < 14; i++) Shipping(new Vector3(Rnd(-220, -60), 0, Rnd(-200, -150)), Chance(0.5) ? 0 : 90);
            MeshKit.Box(T, new Vector3(-140, 14, -175), new Vector3(60, 1.5f, 1.5f), new Color(0.85f, 0.6f, 0.1f)); MeshKit.Box(T, new Vector3(-168, 7, -175), new Vector3(1.2f, 14, 1.2f), new Color(0.85f, 0.6f, 0.1f)); MeshKit.Box(T, new Vector3(-112, 7, -175), new Vector3(1.2f, 14, 1.2f), new Color(0.85f, 0.6f, 0.1f));
            for (int i = 0; i < 25; i++) { var p = new Vector3(Rnd(-half + 10, half - 10), 0, Rnd(-half + 10, half - 10)); if (!Physics.CheckSphere(p + Vector3.up, 1.5f)) { var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); b.transform.SetParent(T, false); b.transform.position = p + Vector3.up * 0.45f; b.transform.localScale = new Vector3(0.7f, 0.45f, 0.7f); b.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Mat(new Color(0.3f, 0.4f, 0.6f)); b.isStatic = true; } }
            for (int i = 0; i < 18; i++) { var p = new Vector3(Rnd(-half + 10, half - 10), 0, Rnd(-half + 10, half - 10)); if (!Physics.CheckSphere(p + Vector3.up, 1.2f)) Crate(p, Chance(0.3) ? "body" : Chance(0.5) ? "crate" : "toolbox"); }
            for (int i = 0; i < 6; i++) OsmMapBuilder.Car(T, new Vector3(Rnd(-120, 120), 0, -150 + Rnd(-3, 3)), 90, R);
            // проходная (230,-230)
            Buildings.Make(Buildings.Rect(225, -215, 10, 8), 1, "Проходная", new Color(0.7f, 0.7f, 0.7f), true, "office");
            if (World.Night) for (int i = 0; i < 16; i++) World.Lamp(new Vector3(Rnd(-half, half), 6, Rnd(-half, half)), new Color(1f, 0.8f, 0.5f), 20, 1.8f);
            Spawns(half - 15);
            World.BossSpawn = new Vector3(0, 0.1f, 0);
        }

        // ================================================================ ПОСЁЛОК
        public static void BuildSuburb()
        {
            float half = World.Half;
            MeshKit.Ground(T, World.Map.size + 200, new Color(0.32f, 0.52f, 0.25f), 6f);
            var road = new Color(0.3f, 0.3f, 0.3f);
            MeshKit.Strip(T, new List<Vector2> { new Vector2(-half, half), new Vector2(-200, 150), new Vector2(0, 0), new Vector2(half, -half) }, 7, 0.04f, road); // лесная → шоссе
            for (int i = 0; i < 4; i++) MeshKit.Strip(T, new List<Vector2> { new Vector2(-80 + i * 90, -half + 60), new Vector2(-80 + i * 90, 160) }, 5, 0.04f, new Color(0.45f, 0.4f, 0.32f));
            for (int i = 0; i < 4; i++) MeshKit.Strip(T, new List<Vector2> { new Vector2(-120, -300 + i * 110), new Vector2(230, -300 + i * 110) }, 5, 0.04f, new Color(0.45f, 0.4f, 0.32f));
            // коттеджи на участках
            int n = 0;
            for (int gx = 0; gx < 4; gx++) for (int gz = 0; gz < 4; gz++) for (int k = 0; k < 2; k++)
                    {
                        float cx = -80 + gx * 90 + (k == 0 ? -25 : 25) + Rnd(-5, 5), cz = -300 + gz * 110 + 50 + Rnd(-10, 10);
                        if (Mathf.Abs(cx - 150) < 30 && Mathf.Abs(cz + 150) < 30) continue;
                        float w = Rnd(9, 13), d = Rnd(8, 11); int lv = Chance(0.4) ? 2 : 1;
                        Buildings.Make(Buildings.Rect(cx, cz, w, d, Rnd(-10, 10)), lv, "Дом" + n++, Buildings.ColorFor(n * 31), true, "residential");
                        // забор участка с проёмом
                        float fx = 20, fz = 22; var c = new Vector2(cx, cz);
                        MeshKit.Wall(T, c + new Vector2(-fx, -fz), c + new Vector2(-2, -fz), 1.3f, 0.1f, new Color(0.5f, 0.4f, 0.3f)); MeshKit.Wall(T, c + new Vector2(2, -fz), c + new Vector2(fx, -fz), 1.3f, 0.1f, new Color(0.5f, 0.4f, 0.3f));
                        MeshKit.Wall(T, c + new Vector2(fx, -fz), c + new Vector2(fx, fz), 1.3f, 0.1f, new Color(0.5f, 0.4f, 0.3f)); MeshKit.Wall(T, c + new Vector2(fx, fz), c + new Vector2(-fx, fz), 1.3f, 0.1f, new Color(0.5f, 0.4f, 0.3f)); MeshKit.Wall(T, c + new Vector2(-fx, fz), c + new Vector2(-fx, -fz), 1.3f, 0.1f, new Color(0.5f, 0.4f, 0.3f));
                        if (Chance(0.6)) { Buildings.Make(Buildings.Rect(cx + 12, cz + 10, 6, 5), 1, "Гараж", new Color(0.5f, 0.5f, 0.55f), true, "garage", null, null, 2.8f); }
                        if (Chance(0.5)) MeshKit.Tree(T, new Vector3(cx - 12, 0, cz + Rnd(-10, 10)), R);
                        if (Chance(0.3)) OsmMapBuilder.Car(T, new Vector3(cx + Rnd(-8, 8), 0, cz - 17), Rnd(0, 360), R);
                    }
            // магазин (0,0)
            Buildings.Make(Buildings.Rect(0, 0, 22, 12), 1, "Магазин", new Color(0.8f, 0.5f, 0.3f), true, "shop", null, null, 3.6f);
            // коттедж богача (150,-150), 2 этажа, запертая комната
            Buildings.Make(Buildings.Rect(150, -150, 22, 16), 2, "Коттедж", new Color(0.9f, 0.88f, 0.8f), true, "residential", "key_suburb_cottage", "кабинет");
            MeshKit.Wall(T, new Vector2(120, -180), new Vector2(180, -180), 2.2f, 0.2f, new Color(0.3f, 0.3f, 0.3f)); MeshKit.Wall(T, new Vector2(180, -180), new Vector2(180, -120), 2.2f, 0.2f, new Color(0.3f, 0.3f, 0.3f));
            // запертый гараж (-120,-200)
            Buildings.Make(Buildings.Rect(-120, -200, 14, 9), 1, "Большой гараж", new Color(0.45f, 0.45f, 0.5f), true, "garage", "key_suburb_garage", "бокс", 3.5f);
            // церковь (-150,100)
            Buildings.Make(Buildings.Rect(-150, 100, 16, 24), 3, "Церковь", new Color(0.95f, 0.95f, 0.9f), true, "theatre");
            World.SniperSpots.Add(new Vector3(-150, 3 * Buildings.FloorH + 0.5f, 100));
            // лесничество (-300,300)
            Buildings.Make(Buildings.Rect(-300, 300, 14, 10), 1, "Лесничество", new Color(0.5f, 0.35f, 0.2f), true, "industrial");
            // озеро (300,250)
            var lake = new List<Vector2>(); for (int i = 0; i < 24; i++) { float a = i / 24f * Mathf.PI * 2; lake.Add(new Vector2(300 + Mathf.Cos(a) * Rnd(55, 70), 250 + Mathf.Sin(a) * Rnd(45, 60))); }
            MeshKit.Slab(T, lake, 0.0f, 0.02f, new Color(0.2f, 0.45f, 0.65f), false, "lake");
            // лес: северо-запад и полоса вдоль края
            int trees = 0;
            for (int i = 0; i < 700 && trees < 320; i++)
            {
                var p = new Vector3(Rnd(-half + 5, half - 5), 0, Rnd(-half + 5, half - 5));
                bool forest = (p.x < -130 && p.z > 60) || Mathf.Abs(p.x) > half - 60 || Mathf.Abs(p.z) > half - 60;
                if (!forest || Physics.CheckSphere(p + Vector3.up, 2.5f)) continue;
                MeshKit.Tree(T, p, R); trees++;
            }
            // вышки
            foreach (var p in new[] { new Vector3(-250, 0, -250), new Vector3(300, 0, 100) })
            {
                MeshKit.Box(T, p + new Vector3(0, 7.2f, 0), new Vector3(4, 0.3f, 4), new Color(0.4f, 0.3f, 0.2f));
                for (int i = 0; i < 4; i++) MeshKit.Box(T, p + new Vector3(i < 2 ? -1.8f : 1.8f, 3.6f, i % 2 == 0 ? -1.8f : 1.8f), new Vector3(0.3f, 7.2f, 0.3f), new Color(0.4f, 0.3f, 0.2f));
                Buildings.Ramp(T, p + new Vector3(2, 0, -12), p + new Vector3(2, 7.3f, -2), 1.5f, new Color(0.4f, 0.3f, 0.2f));
                World.SniperSpots.Add(p + Vector3.up * 7.6f);
                Crate(p + new Vector3(0, 7.4f, 0), "weaponbox");
            }
            for (int i = 0; i < 15; i++) { var p = new Vector3(Rnd(-half + 10, half - 10), 0, Rnd(-half + 10, half - 10)); if (!Physics.CheckSphere(p + Vector3.up, 1.2f)) Crate(p, Chance(0.4) ? "body" : "bag"); }
            Spawns(half - 15);
            World.BossSpawn = new Vector3(-300, 0.1f, 300);
        }

        // ================================================================ ТОРГОВЫЙ ЦЕНТР
        public static void BuildMall()
        {
            float half = World.Half;
            MeshKit.Ground(T, World.Map.size + 200, new Color(0.4f, 0.4f, 0.4f), 5f);
            float W = 180, D = 120, fh = 4.5f; int floors = 3;
            var wall = new Color(0.85f, 0.85f, 0.88f); var floorC = new Color(0.75f, 0.72f, 0.68f); var shopC = new Color(0.6f, 0.6f, 0.7f);
            var outer = Buildings.Rect(0, 0, W, D);
            // внешние стены с главным входом (юг) и служебными выходами
            for (int f = 0; f < floors; f++)
            {
                float y = f * fh;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 a = outer[i], b = outer[(i + 1) % 4];
                    if (f == 0 && (i == 0 || i == 2)) { var dir = (b - a).normalized; var mid = (a + b) * 0.5f; MeshKit.Wall(T, a, mid - dir * 4, fh, 0.4f, wall, y); MeshKit.Wall(T, mid + dir * 4, b, fh, 0.4f, wall, y); }
                    else MeshKit.Wall(T, a, b, fh, 0.4f, wall, y);
                }
                // перекрытие: 4 полосы вокруг атриума (центр открыт)
                if (f > 0)
                {
                    float ax = 30, az = 20;
                    MeshKit.Slab(T, new List<Vector2> { new Vector2(-W / 2, -D / 2), new Vector2(W / 2, -D / 2), new Vector2(W / 2, -az), new Vector2(-W / 2, -az) }, y - 0.3f, 0.3f, floorC, true, "floorS");
                    MeshKit.Slab(T, new List<Vector2> { new Vector2(-W / 2, az), new Vector2(W / 2, az), new Vector2(W / 2, D / 2), new Vector2(-W / 2, D / 2) }, y - 0.3f, 0.3f, floorC, true, "floorN");
                    MeshKit.Slab(T, new List<Vector2> { new Vector2(-W / 2, -az), new Vector2(-ax, -az), new Vector2(-ax, az), new Vector2(-W / 2, az) }, y - 0.3f, 0.3f, floorC, true, "floorW");
                    MeshKit.Slab(T, new List<Vector2> { new Vector2(ax, -az), new Vector2(W / 2, -az), new Vector2(W / 2, az), new Vector2(ax, az) }, y - 0.3f, 0.3f, floorC, true, "floorE");
                    // перила атриума
                    MeshKit.Wall(T, new Vector2(-ax, -az), new Vector2(ax, -az), 1.1f, 0.1f, shopC, y); MeshKit.Wall(T, new Vector2(-ax, az), new Vector2(ax, az), 1.1f, 0.1f, shopC, y);
                    MeshKit.Wall(T, new Vector2(-ax, -az), new Vector2(-ax, az), 1.1f, 0.1f, shopC, y); MeshKit.Wall(T, new Vector2(ax, -az), new Vector2(ax, az), 1.1f, 0.1f, shopC, y);
                }
                // магазины вдоль северной и южной стен
                for (int s = 0; s < 8; s++)
                {
                    float cx = -W / 2 + 12 + s * 22; if (f == 0 && s >= 3 && s <= 4) continue; // проход у входа
                    Buildings.Room(T, cx, -D / 2 + 9, 18, 16, fh - 0.3f, shopC, new[] { 2 }, 3f, y);
                    Buildings.Room(T, cx, D / 2 - 9, 18, 16, fh - 0.3f, shopC, new[] { 0 }, 3f, y);
                    foreach (var cz in new[] { -D / 2 + 9, D / 2 - 9 })
                    {
                        string kind = Chance(0.5) ? "shelf" : Chance(0.5) ? "register" : "cabinet";
                        World.AddContainer(new Vector3(cx + Rnd(-6, 6), y + 0.1f, cz + Rnd(-5, 5)), Quaternion.identity, kind);
                        if (Chance(0.5)) World.AddContainer(new Vector3(cx + Rnd(-6, 6), y + 0.1f, cz + Rnd(-5, 5)), Quaternion.identity, "shelf");
                    }
                }
                // пандусы между этажами (запад и восток)
                if (f < floors) { Buildings.Ramp(T, new Vector3(-W / 2 + 6, y + 0.05f, -D / 2 + 30), new Vector3(-W / 2 + 6, y + fh, -D / 2 + 30 + 26), 3f, floorC); Buildings.Ramp(T, new Vector3(W / 2 - 6, y + 0.05f, D / 2 - 30), new Vector3(W / 2 - 6, y + fh, D / 2 - 30 - 26), 3f, floorC); }
                // потолочные панели (эмиссия) и лампы
                for (int i = 0; i < 10; i++) { var pnl = MeshKit.Box(T, new Vector3(-W / 2 + 10 + i * 18, y + fh - 0.2f, 0), new Vector3(6, 0.1f, 2), Color.white, false); pnl.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Emissive(new Color(1f, 0.97f, 0.9f), 1.2f); }
                World.Lamp(new Vector3(-50, y + fh - 0.6f, 0), new Color(1f, 0.95f, 0.85f), 30, 1.5f); World.Lamp(new Vector3(50, y + fh - 0.6f, 0), new Color(1f, 0.95f, 0.85f), 30, 1.5f);
            }
            // крыша (полная) + парапет
            MeshKit.Slab(T, outer, floors * fh, 0.4f, new Color(0.5f, 0.5f, 0.52f), true, "roof");
            for (int i = 0; i < 4; i++) MeshKit.Wall(T, outer[i], outer[(i + 1) % 4], 1.2f, 0.3f, wall, floors * fh + 0.4f);
            // атриум: стеклянная крыша отсутствует — открыт (свет сверху), дно атриума — фудкорт (0,60 → у нас центр)
            // фудкорт на 1 этаже в центре
            for (int i = 0; i < 10; i++) World.AddContainer(new Vector3(Rnd(-25, 25), 0.1f, Rnd(-15, 15)), Quaternion.Euler(0, R.Next(0, 360), 0), Chance(0.5) ? "fridge" : "desk");
            // ювелирный (-60,-40) заперт, электроника (60,-40), супермаркет (-80,60) на 1 этаже — в западном/восточном крыле
            Buildings.Room(T, -60, -40, 20, 14, fh - 0.3f, new Color(0.7f, 0.6f, 0.3f), new int[0]); World.AddDoor(new Vector3(-60, 0, -33), 0, 2.4f, "key_mall_jewelry", "ювелирный");
            MeshKit.Wall(T, new Vector2(-70, -33), new Vector2(-61.2f, -33), fh - 0.3f, 0.3f, new Color(0.7f, 0.6f, 0.3f)); MeshKit.Wall(T, new Vector2(-58.8f, -33), new Vector2(-50, -33), fh - 0.3f, 0.3f, new Color(0.7f, 0.6f, 0.3f));
            World.AddContainer(new Vector3(-64, 0.1f, -42), Quaternion.identity, "safe"); World.AddContainer(new Vector3(-56, 0.1f, -42), Quaternion.identity, "safe"); World.AddContainer(new Vector3(-60, 0.1f, -36), Quaternion.identity, "register");
            Buildings.Room(T, 60, -40, 20, 14, fh - 0.3f, new Color(0.3f, 0.4f, 0.6f), new[] { 2 }); for (int i = 0; i < 4; i++) World.AddContainer(new Vector3(52 + i * 5, 0.1f, -44), Quaternion.identity, Chance(0.5) ? "server" : "shelf");
            Buildings.Room(T, -70, 30, 30, 24, fh - 0.3f, new Color(0.5f, 0.7f, 0.5f), new[] { 1 }); for (int r = 0; r < 3; r++) for (int i = 0; i < 3; i++) { MeshKit.Box(T, new Vector3(-80 + i * 10, 1, 22 + r * 8), new Vector3(6, 2, 0.8f), new Color(0.6f, 0.6f, 0.6f)); World.AddContainer(new Vector3(-80 + i * 10, 0.1f, 23 + r * 8), Quaternion.identity, Chance(0.6) ? "shelf" : "fridge"); }
            // кинотеатр (0,-80 → южная стена 2 этаж не влезает; делаем на 3 этаже в центре-юге)
            Buildings.Room(T, 0, -40, 40, 24, fh - 0.3f, new Color(0.25f, 0.1f, 0.12f), new[] { 2 }, 3f, 2 * fh); for (int i = 0; i < 5; i++) MeshKit.Box(T, new Vector3(-12 + i * 6, 2 * fh + 0.5f, -45), new Vector3(4, 1, 1.5f), new Color(0.5f, 0.1f, 0.1f));
            World.AddContainer(new Vector3(0, 2 * fh + 0.1f, -48), Quaternion.identity, "jacket"); World.AddContainer(new Vector3(10, 2 * fh + 0.1f, -36), Quaternion.identity, "bag");
            // охрана (заперто) на 2 этаже восток
            Buildings.Room(T, 70, 0, 14, 12, fh - 0.3f, new Color(0.4f, 0.4f, 0.45f), new int[0], 2.4f, fh); World.AddDoor(new Vector3(63, fh, 0), 90, 2.4f, "key_mall_security", "охрана");
            MeshKit.Wall(T, new Vector2(63, -6), new Vector2(63, -1.2f), fh - 0.3f, 0.3f, new Color(0.4f, 0.4f, 0.45f), fh); MeshKit.Wall(T, new Vector2(63, 1.2f), new Vector2(63, 6), fh - 0.3f, 0.3f, new Color(0.4f, 0.4f, 0.45f), fh);
            World.AddContainer(new Vector3(72, fh + 0.1f, 3), Quaternion.identity, "weaponbox"); World.AddContainer(new Vector3(72, fh + 0.1f, -3), Quaternion.identity, "locker"); World.AddContainer(new Vector3(68, fh + 0.1f, 0), Quaternion.identity, "server");
            // парковка снаружи
            for (int i = 0; i < 18; i++) OsmMapBuilder.Car(T, new Vector3(-100 + i * 11, 0, -95 + (i % 2) * 8), 0, R);
            for (int i = 0; i < 6; i++) OsmMapBuilder.Car(T, new Vector3(-110, 0, 70 + i * 7), 90, R);
            for (int i = 0; i < 12; i++) { var p = new Vector3(Rnd(-half + 8, half - 8), 0, Rnd(-half + 8, half - 8)); if (Mathf.Abs(p.x) < W / 2 + 3 && Mathf.Abs(p.z) < D / 2 + 3) continue; if (!Physics.CheckSphere(p + Vector3.up, 1.2f)) Crate(p, Chance(0.4) ? "body" : "crate"); }
            // спавны: снаружи по углам
            Spawns(half - 10);
            World.BossSpawn = new Vector3(0, 0.1f, 0);
            for (int i = 0; i < 40; i++) { var p = new Vector3(Rnd(-W / 2 + 10, W / 2 - 10), (R.Next(0, floors)) * fh + 0.2f, Rnd(-D / 2 + 10, D / 2 - 10)); World.EnemySpawns.Add(p); }
        }

        // ================================================================ ПОДЗЕМКА
        public static void BuildMetro()
        {
            float half = World.Half; float h = 5f;
            MeshKit.Ground(T, World.Map.size + 50, new Color(0.18f, 0.18f, 0.2f), 3f);
            var wall = new Color(0.3f, 0.32f, 0.36f); var tile = new Color(0.55f, 0.6f, 0.62f);
            // общая «крыша» мира
            MeshKit.Slab(T, Buildings.Rect(0, 0, World.Map.size + 50, World.Map.size + 50), h + 3f, 1f, new Color(0.1f, 0.1f, 0.11f), false, "cap");
            // центральный зал (0,0) 60x30 с платформами и путями
            Buildings.Room(T, 0, 0, 64, 32, h, tile, new[] { 0, 1, 2, 3 }, 8f, 0, true, new Color(0.25f, 0.25f, 0.28f));
            MeshKit.Box(T, new Vector3(0, -0.6f, 0), new Vector3(64, 0.2f, 8), new Color(0.12f, 0.12f, 0.12f)); // путевой лоток ниже платформ
            MeshKit.Box(T, new Vector3(0, 0.4f, 10), new Vector3(64, 0.8f, 12), tile); MeshKit.Box(T, new Vector3(0, 0.4f, -10), new Vector3(64, 0.8f, 12), tile);
            for (int i = 0; i < 6; i++) MeshKit.Box(T, new Vector3(-25 + i * 10, h * 0.5f, 0), new Vector3(1.2f, h, 1.2f), tile * 0.8f);
            for (int i = 0; i < 8; i++) World.AddContainer(new Vector3(Rnd(-28, 28), 0.9f, Chance(0.5) ? 10 : -10), Quaternion.identity, Chance(0.5) ? "bag" : Chance(0.5) ? "body" : "crate");
            // диспетчерская заперта в углу зала
            Buildings.LockedRoom(T, new Vector3(26, 0.9f, 11), "key_metro_control", "диспетчерская", h - 1f, R, "server");
            // тоннели: запад-восток через зал (депо на востоке, дрезина на западе), север к станции «Северная»
            var tunnelC = new Color(0.2f, 0.2f, 0.22f);
            Buildings.Corridor(T, new List<Vector2> { new Vector2(32, 0), new Vector2(95, 0), new Vector2(120, 40) }, 8, h, tunnelC, true);
            Buildings.Corridor(T, new List<Vector2> { new Vector2(-32, 0), new Vector2(-120, 0), new Vector2(-180, 60), new Vector2(-180, 100) }, 8, h, tunnelC, true);
            Buildings.Corridor(T, new List<Vector2> { new Vector2(0, -16), new Vector2(0, -60), new Vector2(-40, -120), new Vector2(0, -170) }, 7, h, tunnelC, true);
            Buildings.Corridor(T, new List<Vector2> { new Vector2(0, 16), new Vector2(0, 60), new Vector2(60, 90), new Vector2(180, 100) }, 6, h, tunnelC, true);
            Buildings.Corridor(T, new List<Vector2> { new Vector2(-32, -4), new Vector2(-80, -40), new Vector2(-120, -60) }, 5, h - 1, tunnelC, true);
            // станция «Северная» (0,-190)
            Buildings.Room(T, 0, -190, 40, 24, h, tile, new[] { 2 }, 7f, 0, true);
            for (int i = 0; i < 5; i++) World.AddContainer(new Vector3(Rnd(-15, 15), 0.1f, Rnd(-200, -182)), Quaternion.identity, Chance(0.5) ? "locker" : "crate");
            // депо (120,60): зал 50x30 с поездом
            Buildings.Room(T, 120, 60, 50, 30, h + 2, wall, new[] { 0 }, 8f, 0, true);
            MeshKit.Box(T, new Vector3(120, 1.9f, 60), new Vector3(22, 3.4f, 3.2f), new Color(0.2f, 0.35f, 0.5f));
            for (int i = 0; i < 6; i++) World.AddContainer(new Vector3(Rnd(100, 140), 0.1f, Rnd(48, 72)), Quaternion.identity, Chance(0.5) ? "toolbox" : "weaponbox");
            // вентшахта (180,100) — комнатка с выходом
            Buildings.Room(T, 180, 100, 10, 10, h, wall, new[] { 3 }, 4f, 0, true);
            // дрезина (-180,100)
            Buildings.Room(T, -180, 100, 12, 10, h, wall, new[] { 0 }, 4f, 0, true); MeshKit.Box(T, new Vector3(-180, 0.7f, 102), new Vector3(3, 1.2f, 2), new Color(0.5f, 0.3f, 0.2f));
            // бункер (-120,-60): сетка комнат + оружейная заперта
            Buildings.Room(T, -120, -60, 40, 30, h - 1, new Color(0.35f, 0.4f, 0.35f), new[] { 1 }, 4f, 0, true);
            for (int i = 0; i < 3; i++) { Buildings.Room(T, -132 + i * 12, -68, 10, 10, h - 1.2f, new Color(0.35f, 0.4f, 0.35f), new[] { 2 }, 2.2f); World.AddContainer(new Vector3(-132 + i * 12, 0.1f, -71), Quaternion.identity, i == 1 ? "medbag" : "locker"); }
            Buildings.LockedRoom(T, new Vector3(-80, 0.1f, 80), "key_metro_armory", "оружейная", h - 1f, R, "weaponbox");
            World.AddContainer(new Vector3(-128, 0.1f, -52), Quaternion.identity, "safe"); World.AddContainer(new Vector3(-110, 0.1f, -52), Quaternion.identity, "server");
            // освещение
            foreach (var p in new[] { new Vector3(-20, h - 0.5f, 10), new Vector3(20, h - 0.5f, -10), new Vector3(0, h - 0.5f, -190), new Vector3(120, h + 1, 60), new Vector3(-120, h - 1.5f, -60), new Vector3(60, h - 0.5f, 0), new Vector3(-80, h - 0.5f, 0), new Vector3(0, h - 0.5f, -100), new Vector3(60, h - 0.5f, 90), new Vector3(180, h - 0.5f, 100), new Vector3(-180, h - 0.5f, 100), new Vector3(-180, h - 0.5f, 60), new Vector3(100, h - 0.5f, 20) })
                World.Lamp(p, Chance(0.3) ? new Color(1f, 0.4f, 0.3f) : new Color(0.9f, 0.9f, 1f), 16, 1.4f);
            // спавны игроков: станции и концы тоннелей
            foreach (var p in new[] { new Vector3(15, 0, -196), new Vector3(120, 0, 95), new Vector3(-180, 0, 70), new Vector3(135, 0, 70), new Vector3(-135, 0, -70), new Vector3(-100, 0, -5) }) World.PlayerSpawns.Add(World.Snap(p));
            foreach (var p in new[] { new Vector3(0, 0, 0), new Vector3(0, 0, -60), new Vector3(60, 0, 0), new Vector3(-60, 0, 0), new Vector3(0, 0, -150), new Vector3(110, 0, 50), new Vector3(-120, 0, -55), new Vector3(60, 0, 90), new Vector3(-80, 0, -40), new Vector3(-150, 0, 40), new Vector3(100, 0, 20), new Vector3(20, 0, 60) }) { World.EnemySpawns.Add(World.Snap(p)); World.EnemySpawns.Add(World.Snap(p + new Vector3(3, 0, 2))); }
            World.BossSpawn = World.Snap(new Vector3(120, 0, 60));
        }

        // ================================================================ ЗАПАСНОЙ ГОРОД (если нет OSM-файла)
        public static void BuildCityFallback(MapDef map)
        {
            float half = World.Half;
            MeshKit.Ground(T, map.size + 200, new Color(0.45f, 0.45f, 0.42f), 6f);
            var road = new Color(0.25f, 0.25f, 0.26f);
            int blocks = Mathf.Max(3, (int)(map.size / 120));
            float step = map.size / blocks;
            for (int i = 0; i <= blocks; i++) { float c = -half + i * step; MeshKit.Strip(T, new List<Vector2> { new Vector2(-half, c), new Vector2(half, c) }, 10, 0.04f, road); MeshKit.Strip(T, new List<Vector2> { new Vector2(c, -half), new Vector2(c, half) }, 10, 0.04f, road); }
            var keys = Database.Items.Values.Where(i => i.type == "key" && i.PS("map") == map.id).ToList(); int k = 0, n = 0;
            for (int bx = 0; bx < blocks; bx++) for (int bz = 0; bz < blocks; bz++)
                {
                    float cx = -half + (bx + 0.5f) * step, cz = -half + (bz + 0.5f) * step;
                    int per = R.Next(1, 4);
                    for (int i = 0; i < per; i++)
                    {
                        float w = Rnd(14, step * 0.4f), d = Rnd(12, step * 0.4f);
                        float ox = Rnd(-step * 0.25f, step * 0.25f), oz = Rnd(-step * 0.25f, step * 0.25f);
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
