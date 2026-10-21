using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>Убежище: склад и экипировка, торговцы, квесты, навыки, перки, модули, верстак, запуск рейда.</summary>
    public class HideoutUI
    {
        Canvas canvas; RectTransform content, tabs; Text topText; string tab = "stash"; string trader = "mech"; ItemInstance benchWeapon; string selMap = "mirea"; bool pvp;
        InputField ipField; float tickT;
        static PlayerProfile P => Game.Profile;

        public void Show()
        {
            if (canvas == null) Build();
            canvas.gameObject.SetActive(true);
            selMap = P.lastMap; pvp = P.lastMode == "pvp";
            Rebuild();
        }
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); }
        public void Tick() { tickT += Time.deltaTime; if (tickT > 1f) { tickT = 0; if (tab == "hideout" || tab == "stash") RefreshTop(); } }

        void Build()
        {
            canvas = UIKit.NewCanvas("Hideout", 30);
            UIKit.Panel(canvas.transform, new Color(0.06f, 0.07f, 0.08f, 1));
            var top = UIKit.Box(canvas.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -60), new Vector2(0, 0), UIKit.Panel2, "top");
            topText = UIKit.Label(top, "", 18, TextAnchor.MiddleLeft); UIKit.Stretch(topText.rectTransform, 16, 0, 420, 0);
            var tb = UIKit.Row(top, 8, 40); UIKit.Place(tb, new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(400, 40));
            UIKit.Btn(tb, "Сохранить", () => { SaveSystem.Save(P); Game.Notify("Сохранено"); }, 130, 36);
            UIKit.Btn(tb, "В главное меню", () => Game.Manager.ToMainMenu(), 160, 36);
            tabs = UIKit.Column(canvas.transform, 6, 10); tabs.anchorMin = new Vector2(0, 0); tabs.anchorMax = new Vector2(0, 1); tabs.pivot = new Vector2(0, 1); tabs.offsetMin = new Vector2(0, 0); tabs.offsetMax = new Vector2(230, -60);
            foreach (var (id, name) in new[] { ("stash", "Склад и экипировка"), ("traders", "Торговцы"), ("quests", "Задания"), ("skills", "Навыки"), ("perks", "Перки"), ("hideout", "Убежище и крафт"), ("bench", "Верстак"), ("raid", "В РЕЙД") })
            {
                string tid = id; UIKit.Btn(tabs, name, () => { tab = tid; Rebuild(); }, 0, 44, id == "raid" ? UIKit.Accent * 0.9f : UIKit.BtnC);
            }
            var host = UIKit.Rect(canvas.transform, "contentHost"); host.anchorMin = new Vector2(0, 0); host.anchorMax = new Vector2(1, 1); host.offsetMin = new Vector2(236, 8); host.offsetMax = new Vector2(-8, -66);
            content = UIKit.Scroll(host, out var sr); UIKit.Stretch(sr.GetComponent<RectTransform>());
            Events.On("profile", _ => { if (canvas.gameObject.activeSelf) RefreshTop(); });
        }

        void RefreshTop()
        {
            if (P == null) return;
            long need = PlayerProfile.XpForLevel(P.level + 1), prev = PlayerProfile.XpForLevel(P.level);
            topText.text = $"<b>{P.name}</b>   Уровень {P.level}  <color=#aaaaaa>({P.xp - prev}/{need - prev} XP)</color>   <color=#ffb347>{P.money:N0} ₽</color>   Очков перков: {P.perkPoints}   Склад: {P.stash.FreeCells} своб. ячеек   Рейдов {P.raids} · выходов {P.extractions}";
        }

        public void Rebuild()
        {
            if (content == null) return;
            RefreshTop(); UIKit.Clear(content);
            switch (tab)
            {
                case "stash": BuildStash(); break;
                case "traders": BuildTraders(); break;
                case "quests": BuildQuests(); break;
                case "skills": BuildSkills(); break;
                case "perks": BuildPerks(); break;
                case "hideout": BuildHideout(); break;
                case "bench": BuildBench(); break;
                case "raid": BuildRaid(); break;
            }
        }

        // ================================================================ СКЛАД
        void BuildStash()
        {
            var lo = P.loadout;
            UIKit.Header(content, "Экипировка в рейд");
            foreach (var slot in Equipment.SlotNames)
            {
                var it = lo.eq.Get(slot);
                var row = UIKit.Row(content, 6, 34); UIKit.Cell(row, Equipment.SlotTitles[slot], 130, 15, TextAnchor.MiddleLeft, UIKit.Muted);
                UIKit.Cell(row, it != null ? UIKit.ItemLine(it) : "<color=#555555>— пусто —</color>", 0, 16);
                if (it != null) UIKit.Btn(row, "На склад", () => { if (P.AddToStash(it)) { lo.eq.Set(slot, null); Changed(); } else Game.Notify("Склад полон"); }, 100, 30, null, 14);
                if (it != null && it.durability >= 0) UIKit.Btn(row, $"Ремонт {TraderSystem.RepairCost(it)}₽", () => { if (TraderSystem.Repair(it)) Changed(); }, 140, 30, null, 13);
                if (it != null && !it.insured && (it.IsWeapon || it.Type == "armor" || it.Type == "helmet" || it.Type == "rig" || it.Type == "backpack")) UIKit.Btn(row, $"Страховка {TraderSystem.InsurancePrice(it)}₽", () => { if (TraderSystem.Insure(it)) Changed(); }, 160, 30, null, 13);
            }
            foreach (var g in lo.Grids())
            {
                UIKit.Label(content, $"<b>{g.Key}</b>  <color=#aaaaaa>{g.Value.W}×{g.Value.H}, свободно {g.Value.FreeCells}</color>", 17, TextAnchor.MiddleLeft, UIKit.Accent, false, 28);
                foreach (var it in g.Value.Items.ToList())
                {
                    var row = UIKit.Row(content, 4, 30); UIKit.Cell(row, UIKit.ItemLine(it), 0, 15);
                    UIKit.Btn(row, "На склад", () => { if (P.AddToStash(it)) { g.Value.Remove(it); Changed(); } else Game.Notify("Склад полон"); }, 100, 28, null, 14);
                }
            }
            UIKit.Label(content, $"Вес снаряжения: {lo.Weight:0.0} кг   Ценность: {lo.Value:N0} ₽", 15, TextAnchor.MiddleLeft, UIKit.Muted, false, 26);
            UIKit.Header(content, $"Склад ({P.stash.Items.Count} предметов, ценность {P.stash.Value:N0} ₽)");
            var filterRow = UIKit.Row(content, 6, 36);
            foreach (var (k, n) in new[] { ("", "Все"), ("weapon", "Оружие"), ("mod", "Моды"), ("ammo", "Патроны"), ("med", "Медицина"), ("food", "Еда"), ("armor", "Броня"), ("valuable", "Ценности"), ("barter", "Ресурсы"), ("key", "Ключи") })
            { string kk = k; UIKit.Btn(filterRow, n, () => { stashFilter = kk; Rebuild(); }, 0, 32, stashFilter == k ? UIKit.Accent * 0.8f : UIKit.BtnC, 14); }
            foreach (var it in P.stash.Items.OrderBy(i => i.Info?.kind).ThenBy(i => i.Info?.type).ThenBy(i => i.Name).ToList())
            {
                var info = it.Info; if (info == null) continue;
                if (stashFilter != "" && info.kind != stashFilter && info.type != stashFilter) continue;
                var row = UIKit.Row(content, 4, 30); UIKit.Cell(row, UIKit.ItemLine(it), 0, 15);
                UIKit.Cell(row, $"{it.Weight:0.0}кг · {it.W}×{it.H}", 90, 12, TextAnchor.MiddleRight, UIKit.Muted);
                bool equippable = info.IsWeapon || info.type == "armor" || info.type == "helmet" || info.type == "rig" || info.type == "backpack" || (info.type == "container" && info.item.P("secure") > 0) || (info.type == "tool" && info.item.P("night") > 0);
                if (equippable) UIKit.Btn(row, "Надеть", () => EquipFromStash(it), 80, 28, UIKit.Good * 0.6f, 14);
                UIKit.Btn(row, "Взять с собой", () => { var one = it.Stackable && it.count > 1 ? new ItemInstance(it.id, Mathf.Min(it.count, it.MaxStack)) : it; if (lo.TryAdd(one)) { if (one == it) P.stash.Remove(it); else it.count -= one.count; if (it.count <= 0) P.stash.Remove(it); Changed(); } else Game.Notify("Нет места в карманах/разгрузке/рюкзаке"); }, 130, 28, null, 14);
                if (it.IsWeapon) UIKit.Btn(row, "Верстак", () => { benchWeapon = it; tab = "bench"; Rebuild(); }, 90, 28, null, 14);
                if (it.durability >= 0 && TraderSystem.RepairCost(it) > 0) UIKit.Btn(row, $"Ремонт {TraderSystem.RepairCost(it)}₽", () => { if (TraderSystem.Repair(it)) Changed(); }, 130, 28, null, 13);
                UIKit.Btn(row, "Удалить", () => { P.stash.Remove(it); Changed(); }, 80, 28, UIKit.Bad * 0.5f, 13);
            }
        }
        string stashFilter = "";

        void EquipFromStash(ItemInstance it)
        {
            var lo = P.loadout; var info = it.Info; string slot;
            if (info.IsWeapon) slot = info.weapon.cls == "pistol" ? "holster" : info.weapon.cls == "melee" ? "melee" : lo.eq.Get("primary") == null ? "primary" : "secondary";
            else if (info.type == "container") slot = "secure"; else if (info.type == "tool") slot = "headgear"; else slot = info.type;
            if (!Equipment.SlotAccepts(slot, it)) return;
            var old = lo.eq.Get(slot);
            P.stash.Remove(it); lo.eq.Set(slot, it);
            if (old != null) P.AddToStash(old);
            Changed();
        }

        void Changed() { Stats.Rebuild(P); SaveSystem.Save(P); Rebuild(); }

        // ================================================================ ТОРГОВЦЫ
        void BuildTraders()
        {
            var row = UIKit.Row(content, 6, 44);
            foreach (var t in Database.TraderList) { string id = t.id; UIKit.Btn(row, $"{t.name} (ур.{P.TraderLevel(id)})", () => { trader = id; Rebuild(); }, 0, 40, trader == id ? UIKit.Accent * 0.8f : UIKit.BtnC, 14); }
            var td = Database.Trader(trader); if (td == null) return;
            UIKit.Label(content, $"<b>{td.name}</b> — {td.desc}\nРепутация {P.Rep(trader):0.00}  ·  уровень {P.TraderLevel(trader)}/4  ·  скидка навыков {(-Stats.Get("tradePrice") * 100):0}%", 16, TextAnchor.UpperLeft, UIKit.TextC, false, 50);
            // квесты торговца
            var avail = QuestSystem.ForTrader(trader, "available"); var active = QuestSystem.ForTrader(trader, "active").Concat(QuestSystem.ForTrader(trader, "done_ready")).ToList();
            if (avail.Count + active.Count > 0) UIKit.Header(content, "Задания");
            foreach (var q in active)
            {
                var qr = UIKit.Row(content, 6, 34); UIKit.Cell(qr, $"<b>{q.name}</b>  <color=#aaaaaa>{string.Join("; ", q.objectives.Select((o, i) => o.Describe() + $" {QuestSystem.ObjectiveProgress(q, i)}/{o.Goal}"))}</color>", 0, 14);
                if (QuestSystem.ReadyToComplete(q)) UIKit.Btn(qr, "Сдать", () => { QuestSystem.Complete(q.id); Changed(); }, 90, 30, UIKit.Good * 0.7f, 14); else UIKit.Cell(qr, "в процессе", 100, 13, TextAnchor.MiddleRight, UIKit.Muted);
            }
            foreach (var q in avail)
            {
                var qr = UIKit.Row(content, 6, 34); UIKit.Cell(qr, $"<b>{q.name}</b> (ур.{q.minLevel})  <color=#aaaaaa>{q.desc}</color>", 0, 14);
                UIKit.Cell(qr, $"+{q.rewards.xp}XP +{q.rewards.money}₽", 140, 13, TextAnchor.MiddleRight, UIKit.Accent);
                UIKit.Btn(qr, "Взять", () => { QuestSystem.Accept(q.id); Rebuild(); }, 80, 30, null, 14);
            }
            UIKit.Header(content, "Ассортимент");
            var fr = UIKit.Row(content, 6, 34);
            foreach (var (k, n) in new[] { ("", "Все"), ("weapon", "Оружие"), ("mod", "Моды"), ("ammo", "Патроны"), ("med", "Медицина"), ("food", "Еда"), ("armor", "Броня/шлемы"), ("backpack", "Рюкзаки/разгрузки"), ("barter", "Ресурсы"), ("throwable", "Гранаты") })
            { string kk = k; UIKit.Btn(fr, n, () => { shopFilter = kk; Rebuild(); }, 0, 30, shopFilter == k ? UIKit.Accent * 0.8f : UIKit.BtnC, 13); }
            foreach (var info in TraderSystem.Stock(trader))
            {
                if (shopFilter != "" && info.kind != shopFilter && info.type != shopFilter && !(shopFilter == "armor" && info.type == "helmet") && !(shopFilter == "backpack" && info.type == "rig")) continue;
                var r = UIKit.Row(content, 4, 30); long price = TraderSystem.BuyPrice(info, trader);
                UIKit.Cell(r, $"<color={UIKit.RarityColor(info.rarity)}>{info.name}</color>  <color=#aaaaaa>{info.desc}</color>", 0, 14);
                UIKit.Cell(r, $"{price:N0} ₽{(info.stack > 1 ? $" ×{info.stack}" : "")}", 120, 14, TextAnchor.MiddleRight, UIKit.Accent);
                UIKit.Btn(r, "Купить", () => { if (TraderSystem.Buy(info, trader, info.stack > 1 ? info.stack : 1)) Changed(); }, 90, 28, UIKit.Good * 0.6f, 14);
                if (info.stack > 1) UIKit.Btn(r, "×5", () => { if (TraderSystem.Buy(info, trader, info.stack * 5)) Changed(); }, 50, 28, null, 14);
            }
            UIKit.Header(content, "Продать со склада");
            foreach (var it in P.stash.Items.OrderByDescending(i => TraderSystem.SellPrice(i)).ToList())
            {
                var r = UIKit.Row(content, 4, 30); UIKit.Cell(r, UIKit.ItemLine(it), 0, 14);
                UIKit.Cell(r, $"{TraderSystem.SellPrice(it):N0} ₽", 100, 14, TextAnchor.MiddleRight, UIKit.Accent);
                UIKit.Btn(r, "Продать", () => { if (TraderSystem.Sell(it, trader)) Changed(); }, 90, 28, UIKit.Bad * 0.5f, 14);
            }
        }
        string shopFilter = "";

        // ================================================================ КВЕСТЫ
        void BuildQuests()
        {
            UIKit.Header(content, "Активные задания");
            var act = QuestSystem.Active();
            if (act.Count == 0) UIKit.Label(content, "Нет активных заданий. Возьмите их у торговцев.", 16, TextAnchor.MiddleLeft, UIKit.Muted, false, 30);
            foreach (var q in act)
            {
                UIKit.Label(content, $"<b>{q.name}</b>  <color=#aaaaaa>{Database.Trader(q.trader)?.name}{(string.IsNullOrEmpty(q.map) ? "" : " · " + Database.Map(q.map)?.name)}</color>\n{q.desc}", 15, TextAnchor.UpperLeft, UIKit.TextC, false, 44);
                for (int i = 0; i < q.objectives.Count; i++) { var o = q.objectives[i]; bool d = QuestSystem.ObjectiveDone(q, i); UIKit.Label(content, $"   {(d ? "<color=#7ee07e>✓</color>" : "•")} {o.Describe()}  {QuestSystem.ObjectiveProgress(q, i)}/{o.Goal}", 14, TextAnchor.MiddleLeft, d ? UIKit.Good : UIKit.TextC, false, 20); }
                var rw = q.rewards; UIKit.Label(content, $"   Награда: {rw.xp} XP, {rw.money} ₽{(rw.items.Count > 0 ? ", " + string.Join(", ", rw.items.Select(k => Database.NameOf(k.Key) + (k.Value > 1 ? " ×" + k.Value : ""))) : "")}", 13, TextAnchor.MiddleLeft, UIKit.Accent, false, 20);
                if (QuestSystem.ReadyToComplete(q)) UIKit.Btn(content, "Сдать задание", () => { QuestSystem.Complete(q.id); Changed(); }, 0, 32, UIKit.Good * 0.7f, 14);
            }
            UIKit.Header(content, $"Выполнено: {Database.QuestList.Count(q => QuestSystem.State(q.id).status == "completed")} из {Database.Quests.Count}");
        }

        // ================================================================ НАВЫКИ
        void BuildSkills()
        {
            UIKit.Label(content, "Навыки растут от действий: бег, стрельба, лечение, торговля, крафт. Каждые 10 уровней — особый бонус.", 15, TextAnchor.UpperLeft, UIKit.Muted, false, 30);
            foreach (var grp in Database.SkillList.GroupBy(s => s.group))
            {
                UIKit.Header(content, grp.Key);
                foreach (var s in grp)
                {
                    int lv = P.SkillLevel(s.id);
                    var r = UIKit.Row(content, 8, 30); UIKit.Cell(r, $"<b>{s.name}</b>", 220, 15); UIKit.Cell(r, $"ур. {lv}", 60, 15, TextAnchor.MiddleRight, UIKit.Accent);
                    var bar = UIKit.Bar(r, UIKit.Accent, 12, 200); bar.fillAmount = P.SkillProgress01(s.id);
                    int next = ((lv / 10) + 1) * 10; string ms = s.milestones.TryGetValue(next, out var m) ? $"{next}: {m}" : "";
                    UIKit.Cell(r, $"<color=#aaaaaa>{s.desc}</color>  <color=#7ee07e>{ms}</color>", 0, 12);
                }
            }
        }

        // ================================================================ ПЕРКИ
        void BuildPerks()
        {
            UIKit.Label(content, $"Очков перков: <b>{P.perkPoints}</b>. Очко за каждый уровень, два за каждый пятый. Следующий ряд открывается после любого перка предыдущего.", 15, TextAnchor.UpperLeft, UIKit.Muted, false, 30);
            foreach (var br in Database.Branches)
            {
                UIKit.Header(content, br.Value);
                foreach (var p in Database.PerkList.Where(x => x.branch == br.Key).OrderBy(x => x.tier))
                {
                    bool have = P.perks.Contains(p.id); bool can = P.CanBuyPerk(p);
                    var r = UIKit.Row(content, 6, 30);
                    UIKit.Cell(r, $"<color={(have ? "#7ee07e" : can ? "#ffffff" : "#777777")}>{new string('★', p.tier)} <b>{p.name}</b></color>  <color=#aaaaaa>{p.desc}</color>", 0, 14);
                    if (have) UIKit.Cell(r, "изучено", 100, 13, TextAnchor.MiddleRight, UIKit.Good);
                    else UIKit.Btn(r, $"Изучить ({p.cost})", () => { if (P.BuyPerk(p.id)) Changed(); }, 120, 28, can ? UIKit.Accent * 0.8f : UIKit.BtnC, 13);
                }
            }
        }

        // ================================================================ УБЕЖИЩЕ
        void BuildHideout()
        {
            UIKit.Header(content, "Модули убежища");
            foreach (var m in Database.HideoutList)
            {
                int lv = P.HideoutLevel(m.id); var nl = HideoutSystem.NextLevel(m);
                var r = UIKit.Row(content, 6, 36);
                UIKit.Cell(r, $"<b>{m.name}</b> ур.{lv}/{m.levels.Count}  <color=#aaaaaa>{m.desc}</color>", 0, 14);
                if (nl == null) UIKit.Cell(r, "макс.", 80, 13, TextAnchor.MiddleRight, UIKit.Good);
                else
                {
                    string miss = HideoutSystem.MissingFor(m);
                    UIKit.Cell(r, miss.Length == 0 ? $"{nl.money:N0} ₽ + {string.Join(", ", nl.items.Select(k => Database.NameOf(k.Key) + "×" + k.Value))}" : $"<color=#ff8080>не хватает: {miss}</color>", 520, 12, TextAnchor.MiddleRight);
                    UIKit.Btn(r, "Улучшить", () => { if (HideoutSystem.Upgrade(m)) Changed(); }, 100, 30, miss.Length == 0 ? UIKit.Good * 0.7f : UIKit.BtnC, 14);
                }
            }
            UIKit.Header(content, "Крафт");
            int got = HideoutSystem.Collect(); if (got > 0) { Game.Notify($"Забрано крафтов: {got}"); }
            foreach (var c in P.crafts) { var rd = Database.Recipe(c.recipe); double left = c.finishAt - PlayerProfile.Now(); UIKit.Label(content, $"⚙ {rd?.name}: {(left > 0 ? $"{(int)(left / 60)} мин {(int)(left % 60)} с" : "готово")}", 15, TextAnchor.MiddleLeft, UIKit.Accent, false, 24); }
            foreach (var rd in HideoutSystem.AvailableRecipes())
            {
                string miss = HideoutSystem.MissingForRecipe(rd);
                var r = UIKit.Row(content, 6, 32);
                UIKit.Cell(r, $"<b>{rd.name}</b>  <color=#aaaaaa>{string.Join(", ", rd.inputs.Select(k => Database.NameOf(k.Key) + "×" + k.Value))} → {string.Join(", ", rd.output.Select(k => Database.NameOf(k.Key) + "×" + k.Value))} · {rd.time / 60:0} мин</color>", 0, 13);
                if (miss.Length > 0) UIKit.Cell(r, $"<color=#ff8080>{miss}</color>", 300, 12, TextAnchor.MiddleRight);
                UIKit.Btn(r, "Создать", () => { if (HideoutSystem.StartCraft(rd)) Changed(); }, 90, 28, miss.Length == 0 ? UIKit.Good * 0.7f : UIKit.BtnC, 14);
            }
            if (HideoutSystem.AvailableRecipes().Count == 0) UIKit.Label(content, "Рецепты открываются улучшением верстака, медпункта, кухни и сборщика воды.", 15, TextAnchor.MiddleLeft, UIKit.Muted, false, 30);
        }

        // ================================================================ ВЕРСТАК
        void BuildBench()
        {
            UIKit.Header(content, "Верстак: модификация оружия");
            var weapons = P.stash.Items.Where(i => i.IsWeapon).Concat(P.loadout.eq.slots.Values.Where(i => i.IsWeapon)).ToList();
            var wr = UIKit.Row(content, 6, 36);
            foreach (var w in weapons.Take(12)) { var ww = w; UIKit.Btn(wr, w.Name, () => { benchWeapon = ww; Rebuild(); }, 0, 32, benchWeapon == w ? UIKit.Accent * 0.8f : UIKit.BtnC, 13); }
            if (weapons.Count > 12) { var wr2 = UIKit.Row(content, 6, 36); foreach (var w in weapons.Skip(12).Take(12)) { var ww = w; UIKit.Btn(wr2, w.Name, () => { benchWeapon = ww; Rebuild(); }, 0, 32, benchWeapon == w ? UIKit.Accent * 0.8f : UIKit.BtnC, 13); } }
            if (benchWeapon == null || !benchWeapon.IsWeapon) { UIKit.Label(content, "Выберите оружие.", 16, TextAnchor.MiddleLeft, UIKit.Muted, false, 30); return; }
            var def = benchWeapon.Info.weapon; var wc = WeaponStats.Compute(benchWeapon);
            UIKit.Label(content, $"<b>{def.name}</b> — {def.clsName}, {def.caliber}, семейство {def.family}\n" +
                $"Отдача ↕{wc.recoilV:0} ↔{wc.recoilH:0}   Эргономика {wc.ergo:0}   Точность {wc.moa:0.00} MOA   Темп {wc.rpm:0}   Магазин {wc.magCap}   Перезарядка {wc.reloadTime:0.0}с   Прицел {wc.adsTime:0.00}с   Зум {wc.zoom:0.0}x   Вес {wc.weight:0.0}кг   Прочность {benchWeapon.durability:0}%{(wc.suppressed ? "   ГЛУШИТЕЛЬ" : "")}   Мастерство {P.MasteryLevel(def.id)}/5", 15, TextAnchor.UpperLeft, UIKit.TextC, false, 60);
            // патроны
            var ammoRow = UIKit.Row(content, 6, 34); UIKit.Cell(ammoRow, "Патроны в магазине:", 170, 15, TextAnchor.MiddleLeft, UIKit.Muted);
            foreach (var a in Database.AmmoFor(def.caliber)) { var aa = a; UIKit.Btn(ammoRow, $"{a.name.Replace(def.caliber + " ", "")} ({P.CountAll(a.id)})", () => { benchWeapon.ammoId = aa.id; benchWeapon.ammoInMag = Mathf.Min(benchWeapon.ammoInMag, wc.magCap); Changed(); }, 0, 30, benchWeapon.ammoId == a.id ? UIKit.Accent * 0.8f : UIKit.BtnC, 13); }
            UIKit.Btn(content, $"Зарядить магазин со склада ({benchWeapon.ammoInMag}/{wc.magCap})", () => { int need = wc.magCap - benchWeapon.ammoInMag; benchWeapon.ammoInMag += P.TakeAll(benchWeapon.ammoId, need); Changed(); }, 0, 32);
            foreach (var slot in def.slots)
            {
                benchWeapon.mods.TryGetValue(slot, out var curId); var cur = Database.Mod(curId);
                UIKit.Label(content, $"<b>Слот: {SlotName(slot)}</b>   сейчас: <color=#ffb347>{(cur != null ? cur.name : "пусто")}</color>", 15, TextAnchor.MiddleLeft, UIKit.TextC, false, 26);
                if (cur != null) UIKit.Btn(content, "Снять на склад", () => { if (P.AddToStash(new ItemInstance(cur.id))) { benchWeapon.mods.Remove(slot); Changed(); } else Game.Notify("Склад полон"); }, 0, 28, null, 13);
                var inStash = P.stash.Items.Where(i => i.IsMod && i.Info.mod.slot == slot && i.Info.mod.Fits(def) && i.id != curId).ToList();
                foreach (var m in inStash)
                {
                    var r = UIKit.Row(content, 6, 28); UIKit.Cell(r, $"   {m.Name}  <color=#aaaaaa>{StatsText(m.Info.mod)}</color>", 0, 13);
                    UIKit.Btn(r, "Установить", () => { if (cur != null && !P.AddToStash(new ItemInstance(cur.id))) { Game.Notify("Склад полон"); return; } P.stash.Remove(m); benchWeapon.mods[slot] = m.id; P.AddSkillXp("intellect", 4); Changed(); }, 110, 26, UIKit.Good * 0.6f, 13);
                }
                // у торговцев
                foreach (var t in Database.TraderList)
                    foreach (var info in TraderSystem.Stock(t.id).Where(i => i.IsMod && i.mod.slot == slot && i.mod.Fits(def) && i.id != curId && !inStash.Any(x => x.id == i.id)))
                    {
                        var r = UIKit.Row(content, 6, 26); long price = TraderSystem.BuyPrice(info, t.id);
                        UIKit.Cell(r, $"   <color=#aaaaaa>{t.name}:</color> {info.name}  <color=#aaaaaa>{StatsText(info.mod)}</color>", 0, 12);
                        UIKit.Btn(r, $"Купить и поставить {price:N0}₽", () => { if (P.money < price) { Game.Notify("Не хватает денег"); return; } if (cur != null && !P.AddToStash(new ItemInstance(cur.id))) { Game.Notify("Склад полон"); return; } P.money -= price; benchWeapon.mods[slot] = info.id; P.AddSkillXp("charisma", 2); Changed(); }, 230, 24, null, 12);
                    }
            }
        }

        static string SlotName(string s) => s == "muzzle" ? "Дульное устройство" : s == "sight" || s == "sight_p" ? "Прицел" : s == "grip" ? "Рукоятка" : s == "stock" ? "Приклад" : s == "mag" ? "Магазин" : s == "handguard" ? "Цевьё" : s == "light" ? "Фонарь/ЛЦУ" : s == "barrel" ? "Ствол" : s == "bipod" ? "Сошки" : s;
        static string StatsText(ModDef m) => string.Join(" ", m.stats.Select(kv => kv.Key == "recoil" ? $"отдача {kv.Value * 100:+0;-0}%" : kv.Key == "ergo" ? $"эрг {kv.Value:+0;-0}" : kv.Key == "moa" ? $"точн {kv.Value * 100:+0;-0}%" : kv.Key == "capacity" ? $"ёмк {kv.Value:+0;-0}" : kv.Key == "zoom" ? $"зум {kv.Value}x" : kv.Key == "suppressed" ? "глушитель" : kv.Key == "reload" ? $"перезар {kv.Value * 100:+0;-0}%" : kv.Key == "velocity" ? $"скор {kv.Value * 100:+0;-0}%" : kv.Key == "laser" ? "ЛЦУ" : kv.Key == "light" ? "фонарь" : kv.Key == "night" ? "ночной" : kv.Key == "weight" ? $"вес {kv.Value:+0.0;-0.0}" : ""));

        // ================================================================ РЕЙД
        void BuildRaid()
        {
            UIKit.Header(content, "Выбор карты");
            foreach (var m in Database.MapList)
            {
                var r = UIKit.Row(content, 6, 60);
                UIKit.Btn(r, m.name, () => { selMap = m.id; Rebuild(); }, 220, 52, selMap == m.id ? UIKit.Accent * 0.8f : UIKit.BtnC, 16);
                UIKit.Cell(r, $"{m.desc}\n<color=#aaaaaa>{m.raidMinutes} мин · врагов {m.enemiesMin}-{m.enemiesMax} · выходов {m.extracts.Count} · босс {m.bossChance * 100:0}%{(m.kind == "osm" ? " · по данным OpenStreetMap" : "")}</color>", 0, 13);
            }
            UIKit.Header(content, "Режим");
            var mr = UIKit.Row(content, 6, 44);
            UIKit.Btn(mr, "Кооператив (все в одном отряде, без дружественного огня)", () => { pvp = false; Rebuild(); }, 0, 40, !pvp ? UIKit.Accent * 0.8f : UIKit.BtnC, 14);
            UIKit.Btn(mr, "PvP: все против всех (+ боты)", () => { pvp = true; Rebuild(); }, 0, 40, pvp ? UIKit.Accent * 0.8f : UIKit.BtnC, 14);
            var lo = P.loadout; var hp = UIKit.Label(content, $"Снаряжение: {lo.eq.Get("primary")?.Name ?? "—"} / {lo.eq.Get("holster")?.Name ?? "—"} · броня {lo.eq.Get("armor")?.Name ?? "—"} · рюкзак {lo.eq.Get("backpack")?.Name ?? "—"} · вес {lo.Weight:0.0} кг", 15, TextAnchor.MiddleLeft, UIKit.Muted, false, 28);
            if (lo.eq.Get("primary") == null && lo.eq.Get("holster") == null) UIKit.Label(content, "<color=#ff8080>Вы идёте без оружия!</color>", 16, TextAnchor.MiddleLeft, UIKit.Bad, false, 26);
            UIKit.Btn(content, $"НАЧАТЬ РЕЙД (хост) — {Database.Map(selMap)?.name}", () => Game.Manager.StartRaidHost(selMap, pvp ? "pvp" : "coop"), 0, 56, UIKit.Accent);
            UIKit.Label(content, $"Вы хост: друзья подключаются к вашему IP <b>{NetGame.LocalIPv4()}</b> (порт 7777). В интернете нужен белый IP с пробросом порта или Radmin VPN / Hamachi / ZeroTier. Подключаться можно в любой момент рейда.", 14, TextAnchor.UpperLeft, UIKit.Muted, false, 44);
            UIKit.Header(content, "Подключиться к другу");
            var jr = UIKit.Row(content, 6, 42); ipField = UIKit.Field(jr, "IP хоста (например 26.10.20.30 или 192.168.1.5:7777)", Game.Settings.lastIp, 0, 40);
            UIKit.Btn(jr, "Подключиться", () => { string ip = ipField.text.Trim(); ushort port = 7777; if (ip.Contains(":")) { var ps = ip.Split(':'); ip = ps[0]; ushort.TryParse(ps[1], out port); } Game.Manager.JoinRaid(ip, port); }, 180, 40, UIKit.Good * 0.7f);
        }
    }
}
