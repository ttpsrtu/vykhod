using System;
using System.Collections.Generic;

namespace Vykhod
{
    public class WeaponDef
    {
        public string id, name, cls, clsName, caliber, family, trader, model, modelClass;
        public float rpm, recoilV, recoilH, ergo, moa, weight, meleeDmg;
        public int magCap, price, lvl, w, h, rarity, modelVariant;
        public List<string> modes, slots;

        public WeaponDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); name = J.S(d, "name"); cls = J.S(d, "cls"); clsName = J.S(d, "clsName");
            caliber = J.S(d, "caliber"); family = J.S(d, "family"); trader = J.S(d, "trader"); model = J.S(d, "model");
            modelClass = J.S(d, "modelClass"); modelVariant = J.I(d, "modelVariant", 0);
            rpm = J.F(d, "rpm", 600); recoilV = J.F(d, "recoilV"); recoilH = J.F(d, "recoilH"); ergo = J.F(d, "ergo", 50);
            moa = J.F(d, "moa", 3); weight = J.F(d, "weight", 3); meleeDmg = J.F(d, "meleeDmg");
            magCap = J.I(d, "magCap", 10); price = J.I(d, "price", 1000); lvl = J.I(d, "lvl", 1);
            w = J.I(d, "w", 3); h = J.I(d, "h", 1); rarity = J.I(d, "rarity", 1);
            modes = J.SL(d, "modes"); slots = J.SL(d, "slots");
        }
        public bool IsMelee => cls == "melee";
    }

    public class ModDef
    {
        public string id, name, slot, trader, model;
        public Dictionary<string, float> stats;
        public int price, lvl, w, h, rarity;
        public float weight;
        public List<string> fitCls, fitCal, fitFam, fitWeapons;

        public ModDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); name = J.S(d, "name"); slot = J.S(d, "slot"); trader = J.S(d, "trader"); model = J.S(d, "model");
            stats = J.FD(d, "stats"); price = J.I(d, "price", 1000); lvl = J.I(d, "lvl", 1); w = J.I(d, "w", 1); h = J.I(d, "h", 1);
            rarity = J.I(d, "rarity", 1); weight = J.F(d, "weight", 0.2f);
            var f = J.D(d, "fits");
            fitCls = J.SL(f, "cls"); fitCal = J.SL(f, "cal"); fitFam = J.SL(f, "fam"); fitWeapons = J.SL(f, "weapons");
        }

        public bool Fits(WeaponDef w)
        {
            if (w == null || !w.slots.Contains(slot)) return false;
            if (fitWeapons.Count > 0 && !fitWeapons.Contains(w.id)) return false;
            if (fitCls.Count > 0 && !fitCls.Contains(w.cls)) return false;
            if (fitCal.Count > 0 && !fitCal.Contains(w.caliber)) return false;
            if (fitFam.Count > 0 && !fitFam.Contains(w.family)) return false;
            return true;
        }

        public float Stat(string k) => stats.TryGetValue(k, out var v) ? v : 0;
    }

    public class AmmoDef
    {
        public string id, caliber, name;
        public float dmg, pen, velocity, weight;
        public int pellets, price, stack, rarity;

        public AmmoDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); caliber = J.S(d, "caliber"); name = J.S(d, "name");
            dmg = J.F(d, "dmg", 40); pen = J.F(d, "pen", 10); velocity = J.F(d, "velocity", 400); weight = J.F(d, "weight", 0.01f);
            pellets = J.I(d, "pellets", 1); price = J.I(d, "price", 10); stack = J.I(d, "stack", 60); rarity = J.I(d, "rarity", 1);
        }
    }

    public class ItemDef
    {
        public string id, name, type, trader, desc;
        public int w, h, price, lvl, rarity;
        public float weight;
        public Dictionary<string, object> props;

        public ItemDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); name = J.S(d, "name"); type = J.S(d, "type"); trader = J.S(d, "trader"); desc = J.S(d, "desc");
            w = J.I(d, "w", 1); h = J.I(d, "h", 1); price = J.I(d, "price", 100); lvl = J.I(d, "lvl", 1); rarity = J.I(d, "rarity", 1);
            weight = J.F(d, "weight", 0.5f);
            props = J.D(d, "props") ?? new Dictionary<string, object>();
        }
        public float P(string k, float def = 0) => J.F(props, k, def);
        public string PS(string k, string def = "") => J.S(props, k, def);
        public List<string> PL(string k) => J.SL(props, k);
        public bool Has(string k) => props.ContainsKey(k);
    }

    public class SkillDef
    {
        public string id, name, group, desc;
        public Dictionary<string, float> perLevel;
        public Dictionary<int, string> milestones = new Dictionary<int, string>();
        public int maxLevel;

        public SkillDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); name = J.S(d, "name"); group = J.S(d, "group"); desc = J.S(d, "desc");
            perLevel = J.FD(d, "perLevel"); maxLevel = J.I(d, "maxLevel", 51);
            var m = J.D(d, "milestones");
            if (m != null) foreach (var kv in m) if (int.TryParse(kv.Key, out int lv)) milestones[lv] = kv.Value?.ToString() ?? "";
        }
    }

    public class PerkDef
    {
        public string id, branch, name, desc;
        public int tier, cost;
        public Dictionary<string, float> effects;
        public List<string> requires;

        public PerkDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); branch = J.S(d, "branch"); name = J.S(d, "name"); desc = J.S(d, "desc");
            tier = J.I(d, "tier", 1); cost = J.I(d, "cost", 1); effects = J.FD(d, "effects"); requires = J.SL(d, "requires");
        }
    }

    public class HideoutLevel
    {
        public int money;
        public Dictionary<string, int> items;
        public Dictionary<string, float> bonus;
        public List<string> recipes;
        public string desc;
        public HideoutLevel(Dictionary<string, object> d)
        {
            money = J.I(d, "money"); items = J.ID(d, "items"); bonus = J.FD(d, "bonus"); recipes = J.SL(d, "recipes"); desc = J.S(d, "desc");
        }
    }

    public class HideoutModuleDef
    {
        public string id, name, desc;
        public List<HideoutLevel> levels = new List<HideoutLevel>();
        public HideoutModuleDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); name = J.S(d, "name"); desc = J.S(d, "desc");
            foreach (var l in J.DL(d, "levels")) levels.Add(new HideoutLevel(l));
        }
    }

    public class RecipeDef
    {
        public string id, name;
        public float time;
        public Dictionary<string, int> inputs, output;
        public RecipeDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); name = J.S(d, "name"); time = J.F(d, "time", 600); inputs = J.ID(d, "inputs"); output = J.ID(d, "output");
        }
    }

    public class EnemyDef
    {
        public string id, name, map, guardType;
        public float hp, accuracy, reaction, speed, aggression;
        public int armorClass, lootMin, lootMax, xp, guards;
        public bool boss, sniper, nightOnly;
        public List<string> weapons, loot;

        public EnemyDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); name = J.S(d, "name"); map = J.S(d, "map"); guardType = J.S(d, "guardType", "raider");
            hp = J.F(d, "hp", 100); accuracy = J.F(d, "accuracy", 0.4f); reaction = J.F(d, "reaction", 0.8f);
            speed = J.F(d, "speed", 3.4f); aggression = J.F(d, "aggression", 0.6f); armorClass = J.I(d, "armorClass");
            var lc = J.L(d, "lootCount");
            lootMin = lc != null && lc.Count > 0 ? (int)J.F(lc[0]) : 1; lootMax = lc != null && lc.Count > 1 ? (int)J.F(lc[1]) : 2;
            xp = J.I(d, "xp", 50); guards = J.I(d, "guards"); boss = J.B(d, "boss"); sniper = J.B(d, "sniper"); nightOnly = J.B(d, "nightOnly");
            weapons = J.SL(d, "weapons"); loot = J.SL(d, "loot");
        }
    }

    public class ExtractDef
    {
        public string name, cond;
        public float x, z;
        public ExtractDef(Dictionary<string, object> d)
        {
            name = J.S(d, "name"); cond = J.S(d, "cond");
            var p = J.L(d, "pos"); if (p != null && p.Count >= 2) { x = J.F(p[0]); z = J.F(p[1]); }
        }
    }

    public class PoiDef
    {
        public string name;
        public float x, z;
        public PoiDef(Dictionary<string, object> d)
        {
            name = J.S(d, "name");
            var p = J.L(d, "pos"); if (p != null && p.Count >= 2) { x = J.F(p[0]); z = J.F(p[1]); }
        }
    }

    public class MapDef
    {
        public string id, name, kind, osm, desc;
        public int raidMinutes, enemiesMin, enemiesMax, size;
        public float lootDensity, bossChance;
        public List<ExtractDef> extracts = new List<ExtractDef>();
        public List<PoiDef> pois = new List<PoiDef>();

        public MapDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); name = J.S(d, "name"); kind = J.S(d, "kind"); osm = J.S(d, "osm"); desc = J.S(d, "desc");
            raidMinutes = J.I(d, "raidMinutes", 30); size = J.I(d, "size", 500);
            var e = J.L(d, "enemies"); enemiesMin = e != null && e.Count > 0 ? (int)J.F(e[0]) : 8; enemiesMax = e != null && e.Count > 1 ? (int)J.F(e[1]) : 14;
            lootDensity = J.F(d, "lootDensity", 1); bossChance = J.F(d, "bossChance", 0.3f);
            foreach (var x in J.DL(d, "extracts")) extracts.Add(new ExtractDef(x));
            foreach (var x in J.DL(d, "pois")) pois.Add(new PoiDef(x));
        }
    }

    public class ObjectiveDef
    {
        public string type, item, map, poi, target, skill, cls, family, trader;
        public int count, level, minMods, minLoot, amount;
        public bool headshot, suppressed, grenade, streak;
        public float minDist;

        public ObjectiveDef(Dictionary<string, object> d)
        {
            type = J.S(d, "type"); item = J.S(d, "item"); map = J.S(d, "map"); poi = J.S(d, "poi"); target = J.S(d, "target");
            skill = J.S(d, "skill"); cls = J.S(d, "cls"); family = J.S(d, "family"); trader = J.S(d, "trader");
            count = J.I(d, "count", 1); level = J.I(d, "level"); minMods = J.I(d, "minMods"); minLoot = J.I(d, "minLoot"); amount = J.I(d, "amount");
            headshot = J.B(d, "headshot"); suppressed = J.B(d, "suppressed"); grenade = J.B(d, "grenade"); streak = J.B(d, "streak");
            minDist = J.F(d, "minDist");
        }

        public string Describe()
        {
            string where = string.IsNullOrEmpty(map) || map == "*" ? "" : " (" + (Database.Map(map)?.name ?? map) + ")";
            switch (type)
            {
                case "kill":
                    {
                        string who = string.IsNullOrEmpty(target) ? "врагов" : (Database.Enemy(target)?.name ?? target);
                        string extra = "";
                        if (headshot) extra += ", в голову"; if (suppressed) extra += ", с глушителем"; if (grenade) extra += ", гранатами";
                        if (minDist > 0) extra += $", с {minDist:0}+ м"; if (!string.IsNullOrEmpty(cls)) extra += ", класс " + cls;
                        if (!string.IsNullOrEmpty(family)) extra += ", семейство " + family; if (minMods > 0) extra += $", модов ≥{minMods}";
                        return $"Убить {count} {who}{extra}{where}";
                    }
                case "handover": return $"Передать {count} × {Database.NameOf(item)}";
                case "find": return $"Найти в рейде и вынести {count} × {Database.NameOf(item)}{where}";
                case "visit": return $"Посетить «{poi}»{where}";
                case "extract": return $"Выйти из рейда {count} раз{(streak ? " подряд" : "")}{(minLoot > 0 ? $" с лутом ≥ {minLoot}" : "")}{where}";
                case "skill": return $"Навык «{Database.Skill(skill)?.name ?? skill}» до уровня {level}";
                case "plant": return $"Установить {Database.NameOf(item)} у «{poi}»{where}";
                case "heal_ally": return $"Вылечить союзников {count} раз";
                case "sell": return $"Продать торговцу на {amount}";
                default: return type;
            }
        }

        public int Goal => type == "skill" ? level : type == "sell" ? amount : type == "visit" || type == "plant" ? 1 : count;
    }

    public class RewardDef
    {
        public int xp, money;
        public Dictionary<string, float> rep;
        public Dictionary<string, int> items;
        public List<string> unlock;
        public RewardDef(Dictionary<string, object> d)
        {
            xp = J.I(d, "xp"); money = J.I(d, "money"); rep = J.FD(d, "rep"); items = J.ID(d, "items"); unlock = J.SL(d, "unlock");
        }
    }

    public class QuestDef
    {
        public string id, trader, name, desc, map;
        public List<ObjectiveDef> objectives = new List<ObjectiveDef>();
        public RewardDef rewards;
        public List<string> requires;
        public int minLevel;
        public QuestDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); trader = J.S(d, "trader"); name = J.S(d, "name"); desc = J.S(d, "desc"); map = J.S(d, "map");
            foreach (var o in J.DL(d, "objectives")) objectives.Add(new ObjectiveDef(o));
            rewards = new RewardDef(J.D(d, "rewards") ?? new Dictionary<string, object>());
            requires = J.SL(d, "requires"); minLevel = J.I(d, "minLevel", 1);
        }
    }

    public class TraderDef
    {
        public string id, name, desc;
        public List<string> sells;
        public List<float> levels = new List<float>();
        public TraderDef(Dictionary<string, object> d)
        {
            id = J.S(d, "id"); name = J.S(d, "name"); desc = J.S(d, "desc"); sells = J.SL(d, "sells");
            var l = J.L(d, "levels"); if (l != null) foreach (var x in l) levels.Add(J.F(x));
            if (levels.Count == 0) levels.AddRange(new[] { 0f, 0.15f, 0.35f, 0.6f });
        }
        public int LevelFor(float rep) { int lv = 1; for (int i = 1; i < levels.Count; i++) if (rep >= levels[i]) lv = i + 1; return lv; }
    }

    /// <summary>Унифицированная карточка любого предмета (оружие/мод/патрон/предмет).</summary>
    public class ItemInfo
    {
        public string id, name, kind, type, trader, desc, model;
        public int w, h, price, lvl, rarity, stack;
        public float weight;
        public WeaponDef weapon; public ModDef mod; public AmmoDef ammo; public ItemDef item;
        public bool IsWeapon => weapon != null;
        public bool IsMod => mod != null;
        public bool IsAmmo => ammo != null;
        public bool IsItem => item != null;
    }
}
