using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Генерация содержимого контейнеров и трофеев с врагов.</summary>
    public static class LootGen
    {
        static readonly Dictionary<string, string[]> kindCats = new Dictionary<string, string[]>
        {
            ["crate"] = new[] { "barter", "ammo", "mod", "food" }, ["locker"] = new[] { "armor", "helmet", "rig", "barter", "food", "backpack" },
            ["safe"] = new[] { "valuable", "valuable", "key", "intel" }, ["medbag"] = new[] { "med", "med", "food" },
            ["weaponbox"] = new[] { "weapon", "ammo", "ammo", "mod", "throwable" }, ["jacket"] = new[] { "key", "valuable", "barter", "food" },
            ["desk"] = new[] { "intel", "valuable", "barter", "food", "key" }, ["body"] = new[] { "food", "barter", "ammo", "med" },
            ["fridge"] = new[] { "food", "food", "food" }, ["toolbox"] = new[] { "barter", "barter", "tool", "mod" },
            ["shelf"] = new[] { "food", "barter", "valuable" }, ["bag"] = new[] { "food", "med", "barter", "ammo" },
            ["server"] = new[] { "valuable", "valuable", "intel", "barter" }, ["register"] = new[] { "valuable", "barter" }, ["cabinet"] = new[] { "barter", "food", "med", "mod" },
        };

        static List<ItemInfo> cache;
        static List<ItemInfo> All() => cache ?? (cache = Database.AllInfos().ToList());

        static ItemInfo Pick(System.Random rng, string cat, float quality, string map)
        {
            var pool = All().Where(i =>
            {
                if (cat == "weapon") return i.IsWeapon && i.weapon.cls != "melee";
                if (cat == "mod") return i.IsMod;
                if (cat == "ammo") return i.IsAmmo;
                if (cat == "key") return i.type == "key" && (string.IsNullOrEmpty(map) || i.item.PS("map") == map);
                return i.IsItem && i.type == cat;
            }).ToList();
            if (pool.Count == 0) return null;
            float total = 0; var w = new float[pool.Count];
            for (int k = 0; k < pool.Count; k++) { float r = pool[k].rarity; w[k] = 1f / (r * r) + quality * 0.25f / r; total += w[k]; }
            double x = rng.NextDouble() * total;
            for (int k = 0; k < pool.Count; k++) { x -= w[k]; if (x <= 0) return pool[k]; }
            return pool[pool.Count - 1];
        }

        public static ItemInstance Make(ItemInfo info, System.Random rng)
        {
            if (info == null) return null;
            if (info.IsWeapon)
            {
                var w = PlayerProfile.MakeWeapon(info.id);
                w.durability = 40 + rng.Next(0, 60); w.ammoInMag = rng.Next(0, Database.Weapon(info.id).magCap + 1);
                foreach (var slot in Database.Weapon(info.id).slots)
                    if (rng.NextDouble() < 0.18) { var ms = Database.ModsFor(Database.Weapon(info.id), slot); if (ms.Count > 0) w.mods[slot] = ms[rng.Next(ms.Count)].id; }
                return w;
            }
            if (info.IsAmmo) return new ItemInstance(info.id, rng.Next(10, Mathf.Max(11, info.stack)));
            var it = new ItemInstance(info.id);
            if (info.stack > 1) it.count = rng.Next(1, 3);
            if (it.durability > 0) it.durability *= 0.4f + 0.6f * (float)rng.NextDouble();
            if (it.uses > 1) it.uses = rng.Next(1, it.uses + 1);
            return it;
        }

        public static List<ItemInstance> Generate(LootContainer c, int seed)
        {
            var rng = new System.Random(seed ^ (c.Id * 7919 + 13));
            var cats = kindCats.TryGetValue(c.Kind, out var cc) ? cc : kindCats["crate"];
            float density = World.Map?.lootDensity ?? 1f;
            float quality = Stats.Get("lootQuality");
            int n = Mathf.RoundToInt((c.Kind == "safe" || c.Kind == "weaponbox" ? 2 : 1) + (float)rng.NextDouble() * 3f * density);
            if (rng.NextDouble() < 0.15 + Stats.Get("lootCount")) n++;
            if (Stats.Has("search_m30") && rng.NextDouble() < 0.1) n++;
            if (Stats.Has("search_m50") && rng.NextDouble() < 0.3) n++;
            var r = new List<ItemInstance>();
            for (int i = 0; i < n; i++)
            {
                var info = Pick(rng, cats[rng.Next(cats.Length)], quality, World.Map?.id);
                var it = Make(info, rng); if (it != null) r.Add(it);
            }
            if (rng.NextDouble() < 0.05 * density) { var k = Pick(rng, "key", quality, World.Map?.id); var it = Make(k, rng); if (it != null) r.Add(it); }
            return r;
        }

        public static List<ItemInstance> EnemyLoot(EnemyDef e, int seed)
        {
            var rng = new System.Random(seed);
            var r = new List<ItemInstance>();
            int n = rng.Next(e.lootMin, e.lootMax + 1);
            if (e.boss && Stats.Has("bossLoot")) n++;
            for (int i = 0; i < n; i++)
            {
                string cat = e.loot.Count > 0 ? e.loot[rng.Next(e.loot.Count)] : "barter";
                var info = Pick(rng, cat, e.boss ? 3f : 0.5f, World.Map?.id);
                var it = Make(info, rng); if (it != null) r.Add(it);
            }
            if (e.weapons.Count > 0 && rng.NextDouble() < 0.7)
            {
                var w = PlayerProfile.MakeWeapon(e.weapons[rng.Next(e.weapons.Count)]);
                if (w != null) { w.durability = 20 + rng.Next(0, 50); r.Add(w); }
            }
            return r;
        }
    }
}
