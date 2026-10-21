using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    public class KillInfo
    {
        public string enemyId = "", map = "", weaponId = "", cls = "", family = "";
        public bool headshot, suppressed, grenade, boss, player;
        public float dist; public int mods;
    }

    /// <summary>Логика квестов: доступность, прогресс по событиям, выдача наград.</summary>
    public static class QuestSystem
    {
        static PlayerProfile P => Game.Profile;

        public static QuestState State(string id)
        {
            if (P == null) return null;
            if (!P.quests.TryGetValue(id, out var s)) { s = new QuestState { id = id }; P.quests[id] = s; }
            var def = Database.Quest(id);
            if (def != null) while (s.progress.Count < def.objectives.Count) s.progress.Add(0);
            return s;
        }

        /// <summary>Пересчитать locked/available по требованиям.</summary>
        public static void Refresh()
        {
            if (P == null) return;
            foreach (var q in Database.QuestList)
            {
                var s = State(q.id);
                if (s.status == "active" || s.status == "completed" || s.status == "done_ready") continue;
                bool ok = P.level >= q.minLevel && q.requires.All(r => State(r).status == "completed");
                s.status = ok ? "available" : "locked";
            }
        }

        public static List<QuestDef> ForTrader(string trader, string status) =>
            Database.QuestList.Where(q => q.trader == trader && State(q.id).status == status).OrderBy(q => q.minLevel).ToList();
        public static List<QuestDef> Active() => Database.QuestList.Where(q => State(q.id).status == "active" || State(q.id).status == "done_ready").ToList();

        public static void Accept(string id)
        {
            var s = State(id); if (s == null || s.status != "available") return;
            s.status = "active"; Events.Raise("quest", id);
        }

        public static bool ObjectiveDone(QuestDef q, int i)
        {
            var o = q.objectives[i]; var s = State(q.id);
            switch (o.type)
            {
                case "handover": return P.FindAll(o.item).Sum(x => x.count) >= o.count;
                case "skill": return P.SkillLevel(o.skill) >= o.level;
                case "sell": return (P.traderSold.TryGetValue(o.trader, out var v) ? v : 0) >= o.amount;
                default: return s.progress[i] >= o.Goal;
            }
        }

        public static int ObjectiveProgress(QuestDef q, int i)
        {
            var o = q.objectives[i]; var s = State(q.id);
            switch (o.type)
            {
                case "handover": return Mathf.Min(o.count, P.FindAll(o.item).Sum(x => x.count));
                case "skill": return Mathf.Min(o.level, P.SkillLevel(o.skill));
                case "sell": return (int)Mathf.Min(o.amount, P.traderSold.TryGetValue(o.trader, out var v) ? v : 0);
                default: return Mathf.Min(o.Goal, s.progress[i]);
            }
        }

        public static bool ReadyToComplete(QuestDef q)
        {
            var s = State(q.id); if (s.status != "active" && s.status != "done_ready") return false;
            for (int i = 0; i < q.objectives.Count; i++) if (!ObjectiveDone(q, i)) return false;
            return true;
        }

        public static bool Complete(string id)
        {
            var q = Database.Quest(id); if (q == null || !ReadyToComplete(q)) return false;
            var s = State(id);
            // забираем предметы по handover и found
            foreach (var o in q.objectives)
            {
                if (o.type == "handover" || o.type == "find")
                {
                    int need = o.count;
                    foreach (var it in P.FindAll(o.item).ToList())
                    {
                        if (need <= 0) break;
                        int t = Mathf.Min(it.count, need); it.count -= t; need -= t;
                        if (it.count <= 0) { if (!P.stash.Remove(it)) P.loadout.RemoveItem(it); }
                    }
                }
            }
            s.status = "completed"; P.questsDone++;
            var r = q.rewards;
            P.AddXp((long)(r.xp * Stats.Mul("questXp")), "quest");
            P.money += r.money;
            foreach (var kv in r.rep) P.AddRep(kv.Key, kv.Value);
            foreach (var kv in r.items)
            {
                var info = Database.Info(kv.Key); if (info == null) continue;
                if (info.IsWeapon) P.AddToStash(PlayerProfile.MakeWeapon(kv.Key));
                else if (info.stack > 1) P.AddToStash(new ItemInstance(kv.Key, kv.Value));
                else for (int i = 0; i < kv.Value; i++) P.AddToStash(new ItemInstance(kv.Key));
            }
            foreach (var u in r.unlock) P.unlocked.Add(u);
            P.AddSkillXp("charisma", 40);
            Refresh();
            Events.Raise("questDone", id);
            Game.Notify($"Квест выполнен: {q.name}  (+{r.xp} XP, +{r.money} ₽)");
            return true;
        }

        static void Bump(QuestDef q, int i, int n = 1)
        {
            var s = State(q.id); s.progress[i] += n;
            if (ReadyToComplete(q) && s.status == "active") { s.status = "done_ready"; Game.Notify($"Квест готов к сдаче: {q.name}"); }
            Events.Raise("questProgress", q.id);
        }

        // ------------------------------------------------ события из рейда
        public static void OnKill(KillInfo k)
        {
            if (P == null) return;
            foreach (var q in Active())
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i]; if (o.type != "kill" || ObjectiveDone(q, i)) continue;
                    if (!string.IsNullOrEmpty(o.target) && o.target != k.enemyId) continue;
                    if (!string.IsNullOrEmpty(o.map) && o.map != "*" && o.map != k.map) continue;
                    if (o.headshot && !k.headshot) continue;
                    if (o.suppressed && !k.suppressed) continue;
                    if (o.grenade && !k.grenade) continue;
                    if (o.minDist > 0 && k.dist < o.minDist) continue;
                    if (!string.IsNullOrEmpty(o.cls) && o.cls != k.cls) continue;
                    if (!string.IsNullOrEmpty(o.family) && o.family != k.family) continue;
                    if (o.minMods > 0 && k.mods < o.minMods) continue;
                    Bump(q, i);
                }
        }

        public static void OnVisit(string map, string poi)
        {
            if (P == null) return;
            P.discoveredPois.Add(map + ":" + poi);
            foreach (var q in Active())
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i];
                    if (o.type == "visit" && o.map == map && o.poi == poi && !ObjectiveDone(q, i)) Bump(q, i);
                }
        }

        public static void OnPlant(string map, string poi, string item)
        {
            foreach (var q in Active())
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i];
                    if (o.type == "plant" && o.map == map && o.poi == poi && o.item == item && !ObjectiveDone(q, i)) Bump(q, i);
                }
        }

        /// <summary>Какой предмет сейчас нужно установить у этой точки (для подсказки на взаимодействии).</summary>
        public static string PlantNeededAt(string map, string poi)
        {
            foreach (var q in Active())
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i];
                    if (o.type == "plant" && o.map == map && o.poi == poi && !ObjectiveDone(q, i)) return o.item;
                }
            return null;
        }

        public static void OnExtract(string map, int lootValue, bool survived)
        {
            if (P == null) return;
            foreach (var q in Active())
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i]; if (o.type != "extract" || ObjectiveDone(q, i)) continue;
                    if (!string.IsNullOrEmpty(o.map) && o.map != "*" && o.map != map) continue;
                    if (o.minLoot > 0 && lootValue < o.minLoot) continue;
                    if (o.streak) { State(q.id).progress[i] = Mathf.Max(State(q.id).progress[i], P.extractStreak); if (ReadyToComplete(q)) Bump(q, i, 0); Events.Raise("questProgress", q.id); }
                    else Bump(q, i);
                }
        }

        public static void OnFoundExtracted(IEnumerable<ItemInstance> items)
        {
            if (P == null) return;
            var list = items.Where(i => i.found).ToList();
            foreach (var q in Active())
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i]; if (o.type != "find" || ObjectiveDone(q, i)) continue;
                    int n = list.Where(x => Database.MatchesPattern(x.id, o.item)).Sum(x => x.count);
                    if (n > 0) Bump(q, i, n);
                }
        }

        public static void OnHealAlly()
        {
            foreach (var q in Active())
                for (int i = 0; i < q.objectives.Count; i++) if (q.objectives[i].type == "heal_ally" && !ObjectiveDone(q, i)) Bump(q, i);
        }

        public static void OnSold(string trader, long amount)
        {
            if (P == null) return;
            P.traderSold[trader] = (P.traderSold.TryGetValue(trader, out var v) ? v : 0) + amount;
            foreach (var q in Active()) if (ReadyToComplete(q) && State(q.id).status == "active") State(q.id).status = "done_ready";
        }

        public static void OnSkillChanged()
        {
            foreach (var q in Active()) if (ReadyToComplete(q) && State(q.id).status == "active") { State(q.id).status = "done_ready"; Game.Notify($"Квест готов к сдаче: {q.name}"); }
        }

        /// <summary>Квестовые точки на карте для подсказок в рейде.</summary>
        public static List<string> PoisNeeded(string map)
        {
            var r = new List<string>();
            foreach (var q in Active())
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i];
                    if ((o.type == "visit" || o.type == "plant") && o.map == map && !ObjectiveDone(q, i)) r.Add(o.poi);
                }
            return r;
        }
    }
}
