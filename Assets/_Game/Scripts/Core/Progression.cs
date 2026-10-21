using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Торговля: ассортимент по уровню лояльности, цены с учётом навыков/перков.</summary>
    public static class TraderSystem
    {
        static PlayerProfile P => Game.Profile;

        public static List<ItemInfo> Stock(string traderId)
        {
            var t = Database.Trader(traderId); if (t == null) return new List<ItemInfo>();
            int lv = P.TraderLevel(traderId);
            bool black = Stats.Has("blackMarket");
            var r = new List<ItemInfo>();
            foreach (var info in Database.AllInfos())
            {
                bool byTrader = info.trader == traderId;
                bool byCat = info.IsAmmo && t.sells.Contains("ammo");
                if (!byTrader && !byCat) continue;
                int need = info.IsAmmo ? info.rarity : info.lvl;
                if (need > lv && !(black && need <= lv + 1) && !P.unlocked.Contains(info.id)) continue;
                r.Add(info);
            }
            return r.OrderBy(i => i.kind).ThenBy(i => i.lvl).ThenBy(i => i.price).ToList();
        }

        public static long BuyPrice(ItemInfo info, string traderId)
        {
            float m = Stats.Mul("tradePrice");
            if (info.IsAmmo) m *= Stats.Mul("ammoPrice");
            if (info.IsMod) m *= Stats.Mul("modPrice");
            if (info.type == "med") m *= Stats.Mul("medPrice");
            int lv = P.TraderLevel(traderId);
            m *= 1f - 0.03f * (lv - 1);
            return (long)Mathf.Max(1, info.price * m);
        }

        public static long SellPrice(ItemInstance it)
        {
            float m = 0.45f * Stats.Mul("sellPrice");
            if (it.Info != null && it.Info.type == "valuable") m += 0.15f;
            return (long)Mathf.Max(1, it.Value * m);
        }

        public static bool Buy(ItemInfo info, string traderId, int count = 1)
        {
            long price = BuyPrice(info, traderId) * count;
            if (P.money < price) { Game.Notify("Не хватает денег"); return false; }
            ItemInstance it = info.IsWeapon ? PlayerProfile.MakeWeapon(info.id) : new ItemInstance(info.id, info.stack > 1 ? count : 1);
            if (info.stack <= 1 && count > 1) { for (int i = 1; i < count; i++) if (!P.AddToStash(new ItemInstance(info.id))) { Game.Notify("Склад полон"); break; } }
            if (!P.AddToStash(it)) { Game.Notify("Склад полон"); return false; }
            P.money -= price;
            P.AddSkillXp("charisma", 2 + count);
            Events.Raise("profile");
            return true;
        }

        public static bool Sell(ItemInstance it, string traderId)
        {
            long price = SellPrice(it);
            if (!P.stash.Remove(it) && !P.loadout.RemoveItem(it)) return false;
            P.money += price;
            P.AddSkillXp("charisma", 3);
            QuestSystem.OnSold(traderId, price);
            Events.Raise("profile");
            return true;
        }

        public static long RepairCost(ItemInstance it)
        {
            if (it.durability < 0) return 0;
            float max = it.IsWeapon ? 100 : it.Info?.item?.P("durability", 100) ?? 100;
            float missing = Mathf.Max(0, max - it.durability);
            return (long)(missing / max * (it.Info?.price ?? 1000) * 0.4f * Stats.Mul("repairCost"));
        }

        public static bool Repair(ItemInstance it)
        {
            long c = RepairCost(it); if (c <= 0) return false;
            if (P.money < c) { Game.Notify("Не хватает денег"); return false; }
            P.money -= c;
            it.durability = it.IsWeapon ? 100 : it.Info.item.P("durability", 100);
            P.AddSkillXp("maintenance", 25);
            Events.Raise("profile");
            return true;
        }

        public static long InsurancePrice(ItemInstance it) => (long)(it.Value * 0.12f * Stats.Mul("insurancePrice"));
        public static bool Insure(ItemInstance it)
        {
            if (it.insured) return false; long c = InsurancePrice(it);
            if (P.money < c) { Game.Notify("Не хватает денег"); return false; }
            P.money -= c; it.insured = true; Events.Raise("profile"); return true;
        }
    }

    /// <summary>Убежище: улучшения модулей, крафт, пассивный доход.</summary>
    public static class HideoutSystem
    {
        static PlayerProfile P => Game.Profile;

        public static HideoutLevel NextLevel(HideoutModuleDef m)
        {
            int lv = P.HideoutLevel(m.id);
            return lv < m.levels.Count ? m.levels[lv] : null;
        }

        public static string MissingFor(HideoutModuleDef m)
        {
            var nl = NextLevel(m); if (nl == null) return "Максимум";
            var sb = new System.Text.StringBuilder();
            float costMul = Stats.Mul("hideoutCost") * (Stats.Has("intellect_m30") ? 0.8f : 1f);
            long money = (long)(nl.money * costMul);
            if (P.money < money) sb.Append($"деньги {P.money}/{money}; ");
            if (m.id != "generator" && P.HideoutLevel("generator") < 1 && m.id != "bedroom" && P.HideoutLevel(m.id) >= 1) sb.Append("нужен генератор; ");
            foreach (var kv in nl.items)
            {
                int need = Mathf.Max(1, Mathf.RoundToInt(kv.Value * costMul)); int have = P.CountAll(kv.Key);
                if (have < need) sb.Append($"{Database.NameOf(kv.Key)} {have}/{need}; ");
            }
            return sb.ToString();
        }

        public static bool CanUpgrade(HideoutModuleDef m) => NextLevel(m) != null && MissingFor(m).Length == 0;

        public static bool Upgrade(HideoutModuleDef m)
        {
            if (!CanUpgrade(m)) return false;
            var nl = NextLevel(m);
            float costMul = Stats.Mul("hideoutCost") * (Stats.Has("intellect_m30") ? 0.8f : 1f);
            P.money -= (long)(nl.money * costMul);
            foreach (var kv in nl.items) P.TakeAll(kv.Key, Mathf.Max(1, Mathf.RoundToInt(kv.Value * costMul)));
            P.hideout[m.id] = P.HideoutLevel(m.id) + 1;
            P.AddSkillXp("intellect", 60); P.AddSkillXp("crafting", 30);
            P.AddXp(500 * P.hideout[m.id], "hideout");
            Stats.Rebuild(P); Events.Raise("profile");
            Game.Notify($"{m.name}: уровень {P.hideout[m.id]}");
            return true;
        }

        public static List<RecipeDef> AvailableRecipes()
        {
            var r = Database.Recipes.Values.Where(x => P.RecipeUnlocked(x.id)).ToList();
            if (Stats.Has("legendaryCraft") || Stats.Has("crafting_m50")) r.AddRange(Database.Recipes.Values.Where(x => x.id == "craft_gun_rare" && !r.Contains(x)));
            return r;
        }

        public static string MissingForRecipe(RecipeDef r)
        {
            var sb = new System.Text.StringBuilder();
            float cm = Stats.Mul("craftCost");
            foreach (var kv in r.inputs)
            {
                int need = Mathf.Max(1, Mathf.RoundToInt(kv.Value * cm)); int have = P.CountAll(kv.Key);
                if (have < need) sb.Append($"{Database.NameOf(kv.Key)} {have}/{need}; ");
            }
            return sb.ToString();
        }

        public static bool StartCraft(RecipeDef r)
        {
            if (MissingForRecipe(r).Length > 0) return false;
            if (P.crafts.Count >= 3 + P.HideoutLevel("generator")) { Game.Notify("Все станки заняты"); return false; }
            float cm = Stats.Mul("craftCost");
            foreach (var kv in r.inputs) P.TakeAll(kv.Key, Mathf.Max(1, Mathf.RoundToInt(kv.Value * cm)));
            float t = r.time * Mathf.Max(0.2f, 1f - Stats.Get("craftSpeed"));
            P.crafts.Add(new CraftJob { recipe = r.id, finishAt = PlayerProfile.Now() + t });
            P.AddSkillXp("crafting", 20); P.AddSkillXp("intellect", 10);
            Events.Raise("profile");
            return true;
        }

        /// <summary>Забрать готовые крафты. Возвращает число собранных.</summary>
        public static int Collect()
        {
            int n = 0; double now = PlayerProfile.Now();
            foreach (var c in P.crafts.ToList())
            {
                if (c.finishAt > now) continue;
                var r = Database.Recipe(c.recipe); P.crafts.Remove(c); if (r == null) continue;
                foreach (var kv in r.output)
                {
                    int cnt = kv.Value;
                    if (Stats.Has("intellect_m50") && Random.value < 0.25f) cnt++;
                    if (Stats.Has("crafting_m40") && kv.Key.StartsWith("ammo_")) cnt *= 2;
                    var info = Database.Info(kv.Key); if (info == null) continue;
                    if (info.IsWeapon) P.AddToStash(PlayerProfile.MakeWeapon(kv.Key));
                    else if (info.stack > 1) P.AddToStash(new ItemInstance(kv.Key, cnt));
                    else for (int i = 0; i < cnt; i++) P.AddToStash(new ItemInstance(kv.Key));
                }
                n++;
            }
            if (n > 0) Events.Raise("profile");
            return n;
        }

        /// <summary>Пассивный доход фермы и страховка за время отсутствия.</summary>
        public static void TickOffline()
        {
            double now = PlayerProfile.Now();
            double hours = Mathf.Clamp((float)((now - P.lastSeen) / 3600.0), 0, 48);
            float income = Stats.Get("income");
            if (income > 0 && hours > 0.01)
            {
                long gain = (long)(income * hours);
                P.money += gain; if (gain > 0) Game.Notify($"Ферма принесла {gain} ₽");
            }
            if (P.insuranceReturns.Count > 0)
            {
                foreach (var it in P.insuranceReturns.ToList()) { it.insured = false; if (P.AddToStash(it)) P.insuranceReturns.Remove(it); }
                Game.Notify("Страховка вернула снаряжение на склад");
            }
            if (P.HideoutLevel("medstation") > 0) { /* лечение после рейда — здоровье восстанавливается в убежище всегда */ }
            P.lastSeen = now;
        }
    }
}
