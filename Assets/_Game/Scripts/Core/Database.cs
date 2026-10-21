using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Все игровые данные из Resources/Data/*.json. Загружается один раз.</summary>
    public static class Database
    {
        public static bool Loaded { get; private set; }
        public static readonly Dictionary<string, WeaponDef> Weapons = new Dictionary<string, WeaponDef>();
        public static readonly Dictionary<string, ModDef> Mods = new Dictionary<string, ModDef>();
        public static readonly Dictionary<string, AmmoDef> Ammo = new Dictionary<string, AmmoDef>();
        public static readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>();
        public static readonly Dictionary<string, SkillDef> Skills = new Dictionary<string, SkillDef>();
        public static readonly Dictionary<string, PerkDef> Perks = new Dictionary<string, PerkDef>();
        public static readonly List<KeyValuePair<string, string>> Branches = new List<KeyValuePair<string, string>>();
        public static readonly Dictionary<string, HideoutModuleDef> Hideout = new Dictionary<string, HideoutModuleDef>();
        public static readonly Dictionary<string, RecipeDef> Recipes = new Dictionary<string, RecipeDef>();
        public static readonly Dictionary<string, EnemyDef> Enemies = new Dictionary<string, EnemyDef>();
        public static readonly Dictionary<string, MapDef> Maps = new Dictionary<string, MapDef>();
        public static readonly Dictionary<string, QuestDef> Quests = new Dictionary<string, QuestDef>();
        public static readonly Dictionary<string, TraderDef> Traders = new Dictionary<string, TraderDef>();
        static readonly Dictionary<string, ItemInfo> infoCache = new Dictionary<string, ItemInfo>();

        public static List<WeaponDef> WeaponList => Weapons.Values.ToList();
        public static List<ModDef> ModList => Mods.Values.ToList();
        public static List<MapDef> MapList => Maps.Values.ToList();
        public static List<QuestDef> QuestList => Quests.Values.ToList();
        public static List<TraderDef> TraderList => Traders.Values.ToList();
        public static List<SkillDef> SkillList => Skills.Values.ToList();
        public static List<PerkDef> PerkList => Perks.Values.ToList();
        public static List<HideoutModuleDef> HideoutList => Hideout.Values.ToList();

        static List<Dictionary<string, object>> LoadList(string file)
        {
            var ta = Resources.Load<TextAsset>("Data/" + file);
            if (ta == null) { Debug.LogError("Нет файла данных Data/" + file + ".json. Запустите Tools/gen_data.py"); return new List<Dictionary<string, object>>(); }
            return J.DL(Json.Parse(ta.text));
        }

        static Dictionary<string, object> LoadDict(string file)
        {
            var ta = Resources.Load<TextAsset>("Data/" + file);
            if (ta == null) { Debug.LogError("Нет файла данных Data/" + file + ".json"); return new Dictionary<string, object>(); }
            return J.D(Json.Parse(ta.text)) ?? new Dictionary<string, object>();
        }

        public static void Load()
        {
            if (Loaded) return;
            foreach (var d in LoadList("weapons")) { var w = new WeaponDef(d); Weapons[w.id] = w; }
            foreach (var d in LoadList("mods")) { var m = new ModDef(d); Mods[m.id] = m; }
            foreach (var d in LoadList("ammo")) { var a = new AmmoDef(d); Ammo[a.id] = a; }
            foreach (var d in LoadList("items")) { var it = new ItemDef(d); Items[it.id] = it; }
            var sk = LoadDict("skills");
            foreach (var d in J.DL(sk, "skills")) { var s = new SkillDef(d); Skills[s.id] = s; }
            foreach (var d in J.DL(sk, "perks")) { var p = new PerkDef(d); Perks[p.id] = p; }
            foreach (var d in J.DL(sk, "branches")) Branches.Add(new KeyValuePair<string, string>(J.S(d, "id"), J.S(d, "name")));
            var hd = LoadDict("hideout");
            foreach (var d in J.DL(hd, "modules")) { var m = new HideoutModuleDef(d); Hideout[m.id] = m; }
            foreach (var d in J.DL(hd, "recipes")) { var r = new RecipeDef(d); Recipes[r.id] = r; }
            foreach (var d in LoadList("enemies")) { var e = new EnemyDef(d); Enemies[e.id] = e; }
            foreach (var d in LoadList("maps")) { var m = new MapDef(d); Maps[m.id] = m; }
            foreach (var d in LoadList("quests")) { var q = new QuestDef(d); Quests[q.id] = q; }
            foreach (var d in LoadList("traders")) { var t = new TraderDef(d); Traders[t.id] = t; }
            Loaded = true;
            Debug.Log($"[Database] оружие {Weapons.Count}, моды {Mods.Count}, патроны {Ammo.Count}, предметы {Items.Count}, квесты {Quests.Count}, карты {Maps.Count}");
        }

        public static WeaponDef Weapon(string id) => id != null && Weapons.TryGetValue(id, out var w) ? w : null;
        public static ModDef Mod(string id) => id != null && Mods.TryGetValue(id, out var m) ? m : null;
        public static AmmoDef AmmoDef(string id) => id != null && Ammo.TryGetValue(id, out var a) ? a : null;
        public static ItemDef Item(string id) => id != null && Items.TryGetValue(id, out var i) ? i : null;
        public static SkillDef Skill(string id) => id != null && Skills.TryGetValue(id, out var s) ? s : null;
        public static PerkDef Perk(string id) => id != null && Perks.TryGetValue(id, out var p) ? p : null;
        public static EnemyDef Enemy(string id) => id != null && Enemies.TryGetValue(id, out var e) ? e : null;
        public static MapDef Map(string id) => id != null && Maps.TryGetValue(id, out var m) ? m : null;
        public static QuestDef Quest(string id) => id != null && Quests.TryGetValue(id, out var q) ? q : null;
        public static TraderDef Trader(string id) => id != null && Traders.TryGetValue(id, out var t) ? t : null;
        public static HideoutModuleDef Module(string id) => id != null && Hideout.TryGetValue(id, out var m) ? m : null;
        public static RecipeDef Recipe(string id) => id != null && Recipes.TryGetValue(id, out var r) ? r : null;

        public static string NameOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            if (id.StartsWith("*")) return "любой " + id.Substring(1);
            var i = Info(id); return i != null ? i.name : id;
        }

        /// <summary>Единая карточка по любому id.</summary>
        public static ItemInfo Info(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (infoCache.TryGetValue(id, out var c)) return c;
            ItemInfo r = null;
            if (Weapons.TryGetValue(id, out var w)) r = new ItemInfo { id = id, name = w.name, kind = "weapon", type = w.cls, trader = w.trader, desc = w.clsName + ", " + w.caliber, model = w.model, w = w.w, h = w.h, price = w.price, lvl = w.lvl, rarity = w.rarity, weight = w.weight, stack = 1, weapon = w };
            else if (Mods.TryGetValue(id, out var m)) r = new ItemInfo { id = id, name = m.name, kind = "mod", type = m.slot, trader = m.trader, desc = "Модуль: " + m.slot, model = m.model, w = m.w, h = m.h, price = m.price, lvl = m.lvl, rarity = m.rarity, weight = m.weight, stack = 1, mod = m };
            else if (Ammo.TryGetValue(id, out var a)) r = new ItemInfo { id = id, name = a.name, kind = "ammo", type = "ammo", trader = "mech", desc = $"Урон {a.dmg:0} · Пробитие {a.pen:0} · {a.velocity:0} м/с", model = "ammo", w = 1, h = 1, price = a.price, lvl = 1, rarity = a.rarity, weight = a.weight, stack = a.stack, ammo = a };
            else if (Items.TryGetValue(id, out var it)) r = new ItemInfo { id = id, name = it.name, kind = "item", type = it.type, trader = it.trader, desc = it.desc, model = it.type, w = it.w, h = it.h, price = it.price, lvl = it.lvl, rarity = it.rarity, weight = it.weight, stack = it.type == "barter" || it.type == "food" ? 5 : 1, item = it };
            if (r != null) infoCache[id] = r;
            return r;
        }

        public static IEnumerable<ItemInfo> AllInfos()
        {
            foreach (var k in Weapons.Keys) yield return Info(k);
            foreach (var k in Mods.Keys) yield return Info(k);
            foreach (var k in Ammo.Keys) yield return Info(k);
            foreach (var k in Items.Keys) yield return Info(k);
        }

        /// <summary>Подходит ли id под шаблон цели квеста ("*pistol", "*med", "*valuable", точный id).</summary>
        public static bool MatchesPattern(string id, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            if (!pattern.StartsWith("*")) return id == pattern;
            var info = Info(id); if (info == null) return false;
            string p = pattern.Substring(1);
            return info.type == p || info.kind == p;
        }

        public static List<AmmoDef> AmmoFor(string caliber) => Ammo.Values.Where(a => a.caliber == caliber).ToList();
        public static List<ModDef> ModsFor(WeaponDef w, string slot) => Mods.Values.Where(m => m.slot == slot && m.Fits(w)).ToList();
    }
}
