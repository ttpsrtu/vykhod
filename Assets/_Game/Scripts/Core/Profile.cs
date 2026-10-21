using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    public class QuestState
    {
        public string id;
        public string status = "available"; // locked | available | active | done_ready | completed
        public List<int> progress = new List<int>();
        public Dictionary<string, object> ToJson() => new Dictionary<string, object> { ["id"] = id, ["status"] = status, ["progress"] = progress.Select(p => (object)(double)p).ToList() };
        public static QuestState FromJson(Dictionary<string, object> d)
        {
            var q = new QuestState { id = J.S(d, "id"), status = J.S(d, "status", "available") };
            var l = J.L(d, "progress"); if (l != null) foreach (var o in l) q.progress.Add((int)J.F(o));
            return q;
        }
    }

    public class CraftJob
    {
        public string recipe; public double finishAt;
    }

    /// <summary>Профиль игрока: уровень, деньги, склад, экипировка, навыки, перки, квесты, убежище.</summary>
    public class PlayerProfile
    {
        public string name = "Боец";
        public int level = 1;
        public long xp = 0;
        public long money = 150000;
        public int perkPoints = 0;
        public Dictionary<string, float> skillXp = new Dictionary<string, float>();
        public List<string> perks = new List<string>();
        public Grid stash = new Grid(10, 40);
        public Loadout loadout = new Loadout();
        public Dictionary<string, QuestState> quests = new Dictionary<string, QuestState>();
        public Dictionary<string, float> traderRep = new Dictionary<string, float>();
        public Dictionary<string, long> traderSold = new Dictionary<string, long>();
        public Dictionary<string, int> hideout = new Dictionary<string, int>();
        public Dictionary<string, int> mastery = new Dictionary<string, int>();   // kills per weapon id
        public HashSet<string> unlocked = new HashSet<string>();
        public HashSet<string> discoveredPois = new HashSet<string>();
        public List<CraftJob> crafts = new List<CraftJob>();
        public List<ItemInstance> insuranceReturns = new List<ItemInstance>();
        public double lastSeen;             // unix time для дохода фермы
        public int extractStreak;
        public int raids, extractions, deaths, kills, bossKills, questsDone;
        public long totalLootValue;
        public float survivalRate => raids > 0 ? (float)extractions / raids : 0;
        public string lastMap = "mirea";
        public string lastMode = "coop";

        // -------------------------------------------------- Уровень
        public static long XpForLevel(int lvl) => lvl <= 1 ? 0 : (long)(900 * Math.Pow(lvl - 1, 1.55));
        public long XpToNext => XpForLevel(level + 1);
        public void AddXp(long amount, string reason = "")
        {
            amount = (long)(amount * Stats.Mul("xpGain"));
            xp += amount;
            while (xp >= XpForLevel(level + 1) && level < 99)
            {
                level++; perkPoints += (level % 5 == 0) ? 2 : 1;
                Events.Raise("levelup", level);
            }
            Events.Raise("xp", amount);
        }

        // -------------------------------------------------- Навыки
        public static int SkillLevelFromXp(float x) { int lv = (int)Math.Floor((-1 + Math.Sqrt(1 + 8 * x / 100.0)) / 2); return Mathf.Clamp(lv, 0, 51); }
        public static float SkillXpForLevel(int lv) => 50f * lv * (lv + 1);
        public int SkillLevel(string id) => skillXp.TryGetValue(id, out var x) ? SkillLevelFromXp(x) : 0;
        public float SkillProgress01(string id)
        {
            float x = skillXp.TryGetValue(id, out var v) ? v : 0; int lv = SkillLevelFromXp(x);
            float a = SkillXpForLevel(lv), b = SkillXpForLevel(lv + 1);
            return b > a ? Mathf.Clamp01((x - a) / (b - a)) : 1;
        }
        public void AddSkillXp(string id, float amount)
        {
            var def = Database.Skill(id); if (def == null) return;
            amount *= Stats.Mul("skillXp");
            if (def.group == "Оружие") amount *= Stats.Mul("weaponSkillXp");
            if (def.group == "Физика") amount *= Stats.Mul("physSkillXp");
            int before = SkillLevel(id);
            skillXp[id] = (skillXp.TryGetValue(id, out var x) ? x : 0) + amount;
            if (SkillLevel(id) > before) { Events.Raise("skillup", id); Stats.Rebuild(this); }
        }

        // -------------------------------------------------- Перки
        public bool CanBuyPerk(PerkDef p)
        {
            if (p == null || perks.Contains(p.id) || perkPoints < p.cost) return false;
            if (p.requires.Count > 0 && !p.requires.Any(r => perks.Contains(r))) return false;
            return true;
        }
        public bool BuyPerk(string id)
        {
            var p = Database.Perk(id); if (!CanBuyPerk(p)) return false;
            perkPoints -= p.cost; perks.Add(id); Stats.Rebuild(this); return true;
        }

        // -------------------------------------------------- Торговцы
        public float Rep(string trader) => traderRep.TryGetValue(trader, out var r) ? r : 0;
        public void AddRep(string trader, float v) { traderRep[trader] = Rep(trader) + v * Stats.Mul("repGain"); }
        public int TraderLevel(string trader) { var t = Database.Trader(trader); return t == null ? 1 : Mathf.Min(4, t.LevelFor(Rep(trader)) + (int)Stats.Get("traderLevelBonus")); }

        // -------------------------------------------------- Убежище
        public int HideoutLevel(string id) => hideout.TryGetValue(id, out var l) ? l : 0;
        public bool RecipeUnlocked(string id)
        {
            foreach (var m in Database.HideoutList) { int lv = HideoutLevel(m.id); for (int i = 0; i < lv && i < m.levels.Count; i++) if (m.levels[i].recipes.Contains(id)) return true; }
            return false;
        }

        // -------------------------------------------------- Мастерство
        public int MasteryLevel(string weaponId) { int k = mastery.TryGetValue(weaponId, out var v) ? v : 0; return k >= 300 ? 5 : k >= 150 ? 4 : k >= 75 ? 3 : k >= 30 ? 2 : k >= 10 ? 1 : 0; }

        // -------------------------------------------------- Склад
        public int StashRows => 40 + (int)Stats.Get("stashBonus") / 10;
        public bool AddToStash(ItemInstance it)
        {
            if (stash.H != StashRows) stash.H = StashRows;
            if (stash.TryAdd(it)) return true;
            stash.Compact();
            return stash.TryAdd(it);
        }
        public int CountAll(string id) => stash.CountOf(id) + loadout.CountOf(id);
        public int TakeAll(string id, int n) { int t = stash.Take(id, n); if (t < n) t += loadout.Take(id, n - t); return t; }
        public List<ItemInstance> FindAll(string pattern)
        {
            var r = new List<ItemInstance>();
            foreach (var i in stash.Items) if (Database.MatchesPattern(i.id, pattern)) r.Add(i);
            foreach (var i in loadout.AllItems()) if (Database.MatchesPattern(i.id, pattern)) r.Add(i);
            return r;
        }

        // -------------------------------------------------- Стартовый набор
        public static PlayerProfile NewGame(string playerName)
        {
            var p = new PlayerProfile { name = playerName };
            p.loadout.eq.Set("primary", MakeWeapon("aks74u", "ammo_5.45x39_std"));
            p.loadout.eq.Set("holster", MakeWeapon("pm", "ammo_9x18_std"));
            p.loadout.eq.Set("melee", new ItemInstance("knife"));
            p.loadout.eq.Set("rig", new ItemInstance("rig_scav"));
            p.loadout.eq.Set("backpack", new ItemInstance("bag_school"));
            p.loadout.eq.Set("armor", new ItemInstance("armor_paca"));
            p.loadout.eq.Set("secure", new ItemInstance("secure_case"));
            p.loadout.TryAdd(new ItemInstance("ammo_5.45x39_std", 60));
            p.loadout.TryAdd(new ItemInstance("ammo_9x18_std", 24));
            p.loadout.TryAdd(new ItemInstance("ai2", 1));
            p.loadout.TryAdd(new ItemInstance("bandage", 2));
            p.loadout.TryAdd(new ItemInstance("water", 1));
            p.AddToStash(MakeWeapon("mp133", "ammo_12ga_std"));
            p.AddToStash(MakeWeapon("tt33", "ammo_7.62x25_std"));
            p.AddToStash(new ItemInstance("ammo_12ga_std", 20));
            p.AddToStash(new ItemInstance("ammo_5.45x39_std", 120));
            p.AddToStash(new ItemInstance("ammo_9x18_std", 60));
            p.AddToStash(new ItemInstance("salewa", 1));
            p.AddToStash(new ItemInstance("tushenka", 2));
            p.AddToStash(new ItemInstance("gren_rgd5", 2));
            p.AddToStash(new ItemInstance("splint", 1));
            p.AddToStash(new ItemInstance("bolts", 2));
            p.hideout["bedroom"] = 1;
            foreach (var t in Database.TraderList) p.traderRep[t.id] = 0;
            p.lastSeen = Now();
            Stats.Rebuild(p);
            return p;
        }

        public static ItemInstance MakeWeapon(string id, string ammo = null, params string[] modIds)
        {
            var w = new ItemInstance(id);
            var def = Database.Weapon(id);
            if (def != null && !def.IsMelee)
            {
                if (ammo == null) { var a = Database.AmmoFor(def.caliber); if (a.Count > 0) ammo = a[0].id; }
                w.ammoId = ammo ?? ""; w.ammoInMag = def.magCap;
            }
            foreach (var m in modIds) { var md = Database.Mod(m); if (md != null && md.Fits(def)) w.mods[md.slot] = m; }
            return w;
        }

        public static double Now() => (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

        // -------------------------------------------------- Сериализация
        public Dictionary<string, object> ToJson()
        {
            return new Dictionary<string, object>
            {
                ["name"] = name, ["level"] = level, ["xp"] = (double)xp, ["money"] = (double)money, ["perkPoints"] = perkPoints,
                ["skillXp"] = skillXp.ToDictionary(k => k.Key, k => (object)(double)k.Value),
                ["perks"] = perks.Select(p => (object)p).ToList(),
                ["stash"] = stash.ToJson(), ["loadout"] = loadout.ToJson(),
                ["quests"] = quests.Values.Select(q => (object)q.ToJson()).ToList(),
                ["traderRep"] = traderRep.ToDictionary(k => k.Key, k => (object)(double)k.Value),
                ["traderSold"] = traderSold.ToDictionary(k => k.Key, k => (object)(double)k.Value),
                ["hideout"] = hideout.ToDictionary(k => k.Key, k => (object)(double)k.Value),
                ["mastery"] = mastery.ToDictionary(k => k.Key, k => (object)(double)k.Value),
                ["unlocked"] = unlocked.Select(u => (object)u).ToList(),
                ["pois"] = discoveredPois.Select(u => (object)u).ToList(),
                ["crafts"] = crafts.Select(c => (object)new Dictionary<string, object> { ["recipe"] = c.recipe, ["finishAt"] = c.finishAt }).ToList(),
                ["insurance"] = insuranceReturns.Select(i => (object)i.ToJson()).ToList(),
                ["lastSeen"] = lastSeen, ["extractStreak"] = extractStreak, ["raids"] = raids, ["extractions"] = extractions, ["deaths"] = deaths,
                ["kills"] = kills, ["bossKills"] = bossKills, ["questsDone"] = questsDone, ["totalLootValue"] = (double)totalLootValue,
                ["lastMap"] = lastMap, ["lastMode"] = lastMode
            };
        }

        public static PlayerProfile FromJson(Dictionary<string, object> d)
        {
            var p = new PlayerProfile();
            if (d == null) return p;
            p.name = J.S(d, "name", "Боец"); p.level = J.I(d, "level", 1); p.xp = (long)J.F(d, "xp"); p.money = (long)J.F(d, "money"); p.perkPoints = J.I(d, "perkPoints");
            p.skillXp = J.FD(d, "skillXp"); p.perks = J.SL(d, "perks");
            p.stash = Grid.FromJson(J.D(d, "stash"), 10, 40); p.loadout = Loadout.FromJson(J.D(d, "loadout"));
            foreach (var q in J.DL(d, "quests")) { var qs = QuestState.FromJson(q); p.quests[qs.id] = qs; }
            p.traderRep = J.FD(d, "traderRep");
            foreach (var kv in J.FD(d, "traderSold")) p.traderSold[kv.Key] = (long)kv.Value;
            p.hideout = J.ID(d, "hideout"); p.mastery = J.ID(d, "mastery");
            p.unlocked = new HashSet<string>(J.SL(d, "unlocked")); p.discoveredPois = new HashSet<string>(J.SL(d, "pois"));
            foreach (var c in J.DL(d, "crafts")) p.crafts.Add(new CraftJob { recipe = J.S(c, "recipe"), finishAt = J.F(c, "finishAt") });
            foreach (var i in J.DL(d, "insurance")) { var it = ItemInstance.FromJson(i); if (it != null) p.insuranceReturns.Add(it); }
            p.lastSeen = J.F(d, "lastSeen"); p.extractStreak = J.I(d, "extractStreak"); p.raids = J.I(d, "raids"); p.extractions = J.I(d, "extractions");
            p.deaths = J.I(d, "deaths"); p.kills = J.I(d, "kills"); p.bossKills = J.I(d, "bossKills"); p.questsDone = J.I(d, "questsDone");
            p.totalLootValue = (long)J.F(d, "totalLootValue"); p.lastMap = J.S(d, "lastMap", "mirea"); p.lastMode = J.S(d, "lastMode", "coop");
            return p;
        }
    }

    public static class SaveSystem
    {
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, "profile.json");
        public static void Save(PlayerProfile p)
        {
            try { File.WriteAllText(Path, Json.Serialize(p.ToJson(), true)); }
            catch (Exception e) { Debug.LogError("Save failed: " + e); }
        }
        public static PlayerProfile Load()
        {
            try
            {
                if (File.Exists(Path)) { var p = PlayerProfile.FromJson(J.D(Json.Parse(File.ReadAllText(Path)))); Stats.Rebuild(p); return p; }
            }
            catch (Exception e) { Debug.LogError("Load failed: " + e); }
            return null;
        }
        public static bool Exists => File.Exists(Path);
        public static void Delete() { try { if (File.Exists(Path)) File.Delete(Path); } catch { } }
    }

    /// <summary>Простая шина событий (строковые события).</summary>
    public static class Events
    {
        static readonly Dictionary<string, List<Action<object>>> subs = new Dictionary<string, List<Action<object>>>();
        public static void On(string ev, Action<object> cb) { if (!subs.TryGetValue(ev, out var l)) subs[ev] = l = new List<Action<object>>(); l.Add(cb); }
        public static void Off(string ev, Action<object> cb) { if (subs.TryGetValue(ev, out var l)) l.Remove(cb); }
        public static void Raise(string ev, object arg = null)
        {
            if (!subs.TryGetValue(ev, out var l)) return;
            foreach (var cb in l.ToArray()) { try { cb(arg); } catch (Exception e) { Debug.LogException(e); } }
        }
        public static void Clear() => subs.Clear();
    }
}
