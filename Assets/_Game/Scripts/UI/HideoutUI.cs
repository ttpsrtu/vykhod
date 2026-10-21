using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>Лобби: вкладки Лобби / Снаряжение / Магазин / Задания / Прокачка / Убежище, большая кнопка «В рейд», действия — в попапах.</summary>
    public class HideoutUI
    {
        Canvas canvas; RectTransform content, tabsHost, moneyChip, levelChip, pointsChip, bottomBar; Image xpBar; Button raidBtn;
        int tab; string trader = "mech"; string stashFilter = ""; string shopFilter = ""; string perkBranch = "assault"; string selMap = "mirea"; bool pvp;
        static readonly string[] TabNames = { "Лобби", "Снаряжение", "Магазин", "Задания", "Прокачка", "Убежище" };
        static readonly string[] TabIcons = { "map", "gear", "shop", "quest", "skills", "home" };
        static PlayerProfile P => Game.Profile;
        float tickT;

        public void Show() { if (canvas == null) Build(); canvas.gameObject.SetActive(true); selMap = P.lastMap; pvp = P.lastMode == "pvp"; tab = 0; Rebuild(); }
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); UIKit.CloseAllPopups(); }
        public void SetTab(int i) { tab = Mathf.Clamp(i, 0, TabNames.Length - 1); Rebuild(); }
        public void Tick() { tickT += Time.deltaTime; if (tickT > 1f) { tickT = 0; RefreshTop(); if (tab == 5) { int n = HideoutSystem.Collect(); if (n > 0) { Game.Notify($"Готово крафтов: {n}"); Rebuild(); } } } }

        void Build()
        {
            canvas = UIKit.NewCanvas("Lobby", 30);
            UIKit.Background(canvas.transform);
            // левая панель навигации (как в Fortnite / Rocket League): логотип, крупные иконки разделов, внизу настройки и выход
            var rail = UIKit.Box(canvas.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(150, 0), new Color(0, 0, 0.1f, 0.55f), "rail", 0);
            var logo = UIKit.Label(rail, "ВЫХОД", 30, TextAnchor.MiddleCenter, UIKit.Yellow, true, 0, true); UIKit.Place(logo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(150, 48));
            tabsHost = UIKit.Rect(rail, "tabsHost"); tabsHost.anchorMin = new Vector2(0, 0); tabsHost.anchorMax = new Vector2(1, 1); tabsHost.offsetMin = new Vector2(10, 140); tabsHost.offsetMax = new Vector2(-10, -80);
            var railBottom = UIKit.Row(rail, 10, 56, 0, "railBottom", TextAnchor.MiddleCenter); UIKit.Place(railBottom, new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(130, 56));
            var sb = UIKit.BigButton(railBottom, "", new Color(1, 1, 1, 0.12f), MainMenuUI.SettingsPopup, 60, 56, 14, Color.white); UIKit.Place(UIKit.Icon(sb.transform, "settings", Color.white, 30).rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            var eb = UIKit.BigButton(railBottom, "", UIKit.Red * new Color(1, 1, 1, 0.75f), () => Game.Manager.ToMainMenu(), 60, 56, 14, Color.white); UIKit.Place(UIKit.Icon(eb.transform, "exit", Color.white, 30).rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            // чипы профиля справа сверху
            var chips = UIKit.Row(canvas.transform, 8, 52, 0, "chips", TextAnchor.MiddleRight); UIKit.Place(chips, new Vector2(1, 1), new Vector2(-20, -16), new Vector2(640, 52));
            pointsChip = UIKit.Chip(chips, "", UIKit.Pink, "skills", 16, 48, 160);
            moneyChip = UIKit.Chip(chips, "0 ₽", UIKit.Yellow * new Color(1, 1, 1, 0.92f), "money", 18, 48, 215); moneyChip.Find("chipText").GetComponent<Text>().color = UIKit.TextDark;
            levelChip = UIKit.Chip(chips, "ур. 1", UIKit.Cyan * new Color(1, 1, 1, 0.92f), "level", 17, 48, 250); levelChip.Find("chipText").GetComponent<Text>().color = UIKit.TextDark;
            // нижняя панель
            bottomBar = UIKit.Box(canvas.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(150, 0), new Vector2(0, 104), new Color(0, 0, 0.08f, 0.45f), "bottom", 0);
            var xprt = UIKit.Rect(canvas.transform, "xp"); xprt.anchorMin = new Vector2(0, 0); xprt.anchorMax = new Vector2(1, 0); xprt.offsetMin = new Vector2(150, 104); xprt.offsetMax = new Vector2(0, 110); xpBar = UIKit.Bar(xprt, UIKit.Cyan, 6); UIKit.Stretch(xpBar.transform.parent.GetComponent<RectTransform>());
            // контент
            var host = UIKit.Rect(canvas.transform, "contentHost"); host.anchorMin = new Vector2(0, 0); host.anchorMax = new Vector2(1, 1); host.offsetMin = new Vector2(172, 122); host.offsetMax = new Vector2(-20, -84);
            content = host;
            Events.On("profile", _ => { if (canvas != null && canvas.gameObject.activeSelf) RefreshTop(); });
        }

        void RefreshTop()
        {
            if (P == null || moneyChip == null) return;
            UIKit.SetChip(moneyChip, $"{P.money:N0} ₽"); UIKit.SetChip(levelChip, $"{P.name} · ур. {P.level}");
            UIKit.SetChip(pointsChip, P.perkPoints > 0 ? $"{P.perkPoints} очк. перков" : ""); pointsChip.gameObject.SetActive(P.perkPoints > 0);
            long need = PlayerProfile.XpForLevel(P.level + 1), prev = PlayerProfile.XpForLevel(P.level);
            xpBar.fillAmount = need > prev ? Mathf.Clamp01((float)(P.xp - prev) / (need - prev)) : 1;
        }

        void BuildBottom()
        {
            UIKit.Clear(bottomBar);
            var modes = UIKit.Row(bottomBar, 10, 60, 0, "modes"); UIKit.Place(modes, new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(760, 60));
            UIKit.BigButton(modes, "Кооператив", !pvp ? UIKit.Green : new Color(1, 1, 1, 0.12f), () => { pvp = false; P.lastMode = "coop"; BuildBottom(); }, 240, 56, 20, !pvp ? UIKit.TextDark : Color.white, "coop");
            UIKit.BigButton(modes, "Все против всех", pvp ? UIKit.Red : new Color(1, 1, 1, 0.12f), () => { pvp = true; P.lastMode = "pvp"; BuildBottom(); }, 280, 56, 20, Color.white, "pvp");
            UIKit.BigButton(modes, "По IP", UIKit.Cyan * new Color(1, 1, 1, 0.85f), JoinPopup, 150, 56, 20, UIKit.TextDark, "join");
            var map = Database.Map(selMap);
            raidBtn = UIKit.BigButton(bottomBar, $"В рейд  ·  {map?.name}", UIKit.Yellow, () => Game.Manager.StartRaidHost(selMap, pvp ? "pvp" : "coop"), 560, 76, 32, UIKit.TextDark, "play");
            UIKit.Place(raidBtn.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(560, 76));
        }

        public void Rebuild()
        {
            if (content == null) return;
            RefreshTop(); UIKit.Clear(content); UIKit.Clear(tabsHost); BuildBottom();
            var rail = UIKit.Column(tabsHost, 10, 0); UIKit.Stretch(rail);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int idx = i; bool sel = i == tab;
                var card = UIKit.Card(rail, sel ? UIKit.Yellow : new Color(1, 1, 1, 0.1f), 18, "tab_" + i);
                var le = card.gameObject.AddComponent<LayoutElement>(); le.minHeight = 92; le.preferredHeight = 92; le.flexibleHeight = 0;
                var b = card.gameObject.AddComponent<Button>(); b.targetGraphic = card; var cols = b.colors; cols.highlightedColor = new Color(1.15f, 1.15f, 1.15f); b.colors = cols;
                b.onClick.AddListener(() => { Fx.Click(); tab = idx; Rebuild(); }); card.gameObject.AddComponent<HoverScale>();
                var ic = UIKit.Icon(card.transform, TabIcons[i], sel ? UIKit.TextDark : Color.white, 40); UIKit.Place(ic.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(40, 40));
                var t = UIKit.Label(card.transform, TabNames[i].ToUpperInvariant(), 13, TextAnchor.MiddleCenter, sel ? UIKit.TextDark : Color.white, true, 0, !sel); t.rectTransform.anchorMin = new Vector2(0, 0); t.rectTransform.anchorMax = new Vector2(1, 0); t.rectTransform.offsetMin = new Vector2(2, 8); t.rectTransform.offsetMax = new Vector2(-2, 36);
                if (i == 3 && QuestSystem.Active().Any(q => QuestSystem.ReadyToComplete(q))) { var dot = UIKit.Img(card.transform, UIKit.Green); dot.sprite = UIKit.Circle(); UIKit.Place(dot.rectTransform, new Vector2(1, 1), new Vector2(-8, -8), new Vector2(14, 14)); }
                if (i == 4 && P.perkPoints > 0) { var dot = UIKit.Img(card.transform, UIKit.Pink); dot.sprite = UIKit.Circle(); UIKit.Place(dot.rectTransform, new Vector2(1, 1), new Vector2(-8, -8), new Vector2(14, 14)); }
            }
            switch (tab)
            {
                case 0: BuildLobby(); break;
                case 1: BuildGear(); break;
                case 2: BuildShop(); break;
                case 3: BuildQuests(); break;
                case 4: BuildProgress(); break;
                case 5: BuildHideout(); break;
            }
        }

        static readonly Dictionary<string, Color> mapColors = new Dictionary<string, Color> { ["mirea"] = UIKit.Blue, ["donetsk"] = UIKit.Orange, ["factory"] = UIKit.Red, ["suburb"] = UIKit.Green, ["mall"] = UIKit.Pink, ["metro"] = UIKit.Purple };

        // ================================================================ ЛОББИ
        void BuildLobby()
        {
            var map = Database.Map(selMap);
            // большая карточка выбранной карты
            var big = UIKit.Card(content, Color.Lerp(mapColors[selMap], UIKit.Dark, 0.3f), 28, "bigmap"); big.rectTransform.anchorMin = new Vector2(0, 0); big.rectTransform.anchorMax = new Vector2(0.62f, 1); big.rectTransform.offsetMin = Vector2.zero; big.rectTransform.offsetMax = new Vector2(-12, 0);
            var glow = UIKit.Img(big.transform, new Color(1, 1, 1, 0.25f)); glow.sprite = UIKit.Glow(); UIKit.Place(glow.rectTransform, new Vector2(0.8f, 0.7f), Vector2.zero, new Vector2(900, 900));
            var bc = UIKit.Column(big.transform, 10, 34); UIKit.Stretch(bc);
            UIKit.Label(bc, map.name.ToUpperInvariant(), 64, TextAnchor.MiddleLeft, Color.white, true, 80, true);
            UIKit.Label(bc, map.desc, 22, TextAnchor.UpperLeft, UIKit.TextC, false, 90);
            var chips = UIKit.Row(bc, 10, 44);
            UIKit.Chip(chips, $"{map.raidMinutes} мин", new Color(0, 0, 0, 0.35f), "clock"); UIKit.Chip(chips, $"врагов {map.enemiesMin}–{map.enemiesMax}", new Color(0, 0, 0, 0.35f), "enemy");
            UIKit.Chip(chips, $"выходов {map.extracts.Count}", new Color(0, 0, 0, 0.35f), "map"); UIKit.Chip(chips, $"босс {map.bossChance * 100:0}%", new Color(0, 0, 0, 0.35f), "skull");
            if (map.kind == "osm") UIKit.Chip(chips, "реальная карта OSM", new Color(0, 0, 0, 0.35f), "map");
            UIKit.Label(bc, "Выходы: " + string.Join(" · ", map.extracts.Select(e => e.name)), 18, TextAnchor.UpperLeft, UIKit.Muted, false, 50);
            UIKit.Label(bc, "Точки: " + string.Join(" · ", map.pois.Select(p => p.name)), 18, TextAnchor.UpperLeft, UIKit.Muted, false, 40);
            // снаряжение кратко
            var lo = P.loadout;
            var gearRow = UIKit.Row(bc, 10, 150);
            foreach (var slot in new[] { "primary", "holster", "armor", "helmet", "backpack", "rig" })
            {
                var it = lo.eq.Get(slot);
                if (it != null) { var tl = UIKit.Tile(gearRow, it.Info, it, () => { tab = 1; Rebuild(); }).gameObject.AddComponent<LayoutElement>(); tl.minWidth = 120; tl.preferredWidth = 120; }
                else { var empty = UIKit.Card(gearRow, new Color(0, 0, 0, 0.3f), 18); var le = empty.gameObject.AddComponent<LayoutElement>(); le.minWidth = 120; le.preferredWidth = 120; var t = UIKit.Label(empty.transform, Equipment.SlotTitles[slot], 14, TextAnchor.MiddleCenter, UIKit.Muted); UIKit.Stretch(t.rectTransform); }
            }
            UIKit.Label(bc, $"<color=#ffd400>Отряд:</color> вы хост, друзья подключаются к IP <b>{NetGame.LocalIPv4()}</b> (порт 7777) в любой момент рейда. Кооператив — один отряд без дружественного огня; «все против всех» — каждый сам за себя, плюс боты.", 16, TextAnchor.UpperLeft, UIKit.TextC, false, 50);
            // правая колонка: выбор карты
            var right = UIKit.Rect(content, "right"); right.anchorMin = new Vector2(0.62f, 0); right.anchorMax = new Vector2(1, 1); right.offsetMin = new Vector2(12, 0); right.offsetMax = Vector2.zero;
            var rc = UIKit.Column(right, 12, 0); UIKit.Stretch(rc);
            UIKit.Header(rc, "Выбор карты", 28);
            var grid = UIKit.Grid(rc, new Vector2(330, 120), 12, 0);
            foreach (var m in Database.MapList)
            {
                var mm = m; bool sel = m.id == selMap;
                var card = UIKit.Card(grid, Color.Lerp(mapColors[m.id], UIKit.Dark, sel ? 0.1f : 0.5f), 20);
                var b = card.gameObject.AddComponent<Button>(); b.targetGraphic = card; b.onClick.AddListener(() => { Fx.Click(); selMap = mm.id; P.lastMap = mm.id; Rebuild(); });
                card.gameObject.AddComponent<HoverScale>();
                var t = UIKit.Label(card.transform, m.name.ToUpperInvariant(), 24, TextAnchor.MiddleLeft, Color.white, true, 0, true); UIKit.Stretch(t.rectTransform, 18, 10, 10, 50);
                var s = UIKit.Label(card.transform, $"{m.raidMinutes} мин · {m.enemiesMin}–{m.enemiesMax} врагов · {m.extracts.Count} выходов", 15, TextAnchor.MiddleLeft, UIKit.TextC, false); UIKit.Stretch(s.rectTransform, 18, 60, 10, 10);
                if (sel) { var fr = UIKit.Img(card.transform, UIKit.Yellow, "frame"); fr.sprite = UIKit.Rounded(20); fr.type = Image.Type.Sliced; UIKit.Stretch(fr.rectTransform, -3, -3, -3, -3); fr.transform.SetAsFirstSibling(); var inner = UIKit.Img(fr.transform, Color.Lerp(mapColors[m.id], UIKit.Dark, 0.1f)); inner.sprite = UIKit.Rounded(20); inner.type = Image.Type.Sliced; UIKit.Stretch(inner.rectTransform, 3, 3, 3, 3); }
            }
        }

        void JoinPopup()
        {
            UIKit.Popup("Подключиться по IP", 700, 300, out var c, UIKit.Cyan);
            var f = UIKit.Field(c, "IP хоста", Game.Settings.lastIp, 0, 56);
            UIKit.BigButton(c, "Подключиться", UIKit.Cyan, () => { string ip = f.text.Trim(); ushort port = 7777; if (ip.Contains(":")) { var ps = ip.Split(':'); ip = ps[0]; ushort.TryParse(ps[1], out port); } UIKit.CloseAllPopups(); Game.Manager.JoinRaid(ip, port); }, 0, 68, 28, UIKit.TextDark);
        }

        // ================================================================ СНАРЯЖЕНИЕ
        void BuildGear()
        {
            var lo = P.loadout;
            var left = UIKit.Rect(content, "left"); left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0.42f, 1); left.offsetMin = Vector2.zero; left.offsetMax = new Vector2(-10, 0);
            var lc = UIKit.Scroll(left, out var lsr); UIKit.Stretch(lsr.GetComponent<RectTransform>());
            UIKit.Header(lc, "Экипировка", 26);
            var eqGrid = UIKit.Grid(lc, new Vector2(150, 170), 10, 0);
            foreach (var slot in Equipment.SlotNames)
            {
                var it = lo.eq.Get(slot); string s = slot;
                if (it != null) UIKit.Tile(eqGrid, it.Info, it, () => ItemPopup(it, "equipped:" + s), null, false, Equipment.SlotTitles[slot]);
                else
                {
                    var empty = UIKit.Card(eqGrid, new Color(1, 1, 1, 0.06f), 18); var t = UIKit.Label(empty.transform, Equipment.SlotTitles[slot], 16, TextAnchor.MiddleCenter, UIKit.Muted, true); UIKit.Stretch(t.rectTransform, 6, 6, 6, 6);
                    var b = empty.gameObject.AddComponent<Button>(); b.targetGraphic = empty; b.onClick.AddListener(() => { stashFilter = SlotFilter(s); Rebuild(); });
                }
            }
            foreach (var g in lo.Grids())
            {
                UIKit.Label(lc, $"{g.Key}  <color=#b9c3e6>{g.Value.W}×{g.Value.H}, свободно {g.Value.FreeCells}</color>", 20, TextAnchor.MiddleLeft, UIKit.Yellow, true, 32, true);
                var gg = UIKit.Grid(lc, new Vector2(120, 136), 8, 0);
                foreach (var it in g.Value.Items.ToList()) { var ii = it; var grid = g.Value; UIKit.Tile(gg, it.Info, it, () => ItemPopup(ii, "carry", grid)); }
                if (g.Value.Items.Count == 0) UIKit.Label(lc, "пусто", 16, TextAnchor.MiddleLeft, UIKit.Muted, false, 24);
            }
            UIKit.Label(lc, $"Вес {lo.Weight:0.0} кг · Ценность {lo.Value:N0} ₽", 18, TextAnchor.MiddleLeft, UIKit.Muted, true, 30);
            // склад
            var right = UIKit.Rect(content, "right"); right.anchorMin = new Vector2(0.42f, 0); right.anchorMax = new Vector2(1, 1); right.offsetMin = new Vector2(10, 0); right.offsetMax = Vector2.zero;
            var rcol = UIKit.Column(right, 8, 0); UIKit.Stretch(rcol);
            var hdr = UIKit.Row(rcol, 10, 44); UIKit.Header(hdr, $"Склад · {P.stash.Items.Count} предм. · {P.stash.Value:N0} ₽", 26).GetComponent<LayoutElement>().flexibleWidth = 1;
            var filt = UIKit.Row(rcol, 6, 40);
            foreach (var (k, n) in new[] { ("", "Все"), ("weapon", "Оружие"), ("mod", "Моды"), ("ammo", "Патроны"), ("med", "Медицина"), ("food", "Еда"), ("armor", "Броня"), ("valuable", "Ценное"), ("barter", "Ресурсы"), ("key", "Ключи") })
            { string kk = k; UIKit.BigButton(filt, n, stashFilter == k ? UIKit.Yellow : new Color(1, 1, 1, 0.12f), () => { stashFilter = kk; Rebuild(); }, 0, 36, 13, stashFilter == k ? UIKit.TextDark : Color.white); }
            var sc = UIKit.Scroll(rcol, out var ssr, true, new Vector2(132, 150)); ssr.GetComponent<LayoutElement>()?.ToString(); var le = ssr.gameObject.AddComponent<LayoutElement>(); le.flexibleHeight = 1;
            foreach (var it in P.stash.Items.OrderBy(i => i.Info?.kind).ThenBy(i => i.Info?.type).ThenByDescending(i => i.Info?.rarity).ThenBy(i => i.Name).ToList())
            {
                var info = it.Info; if (info == null) continue;
                if (stashFilter != "" && info.kind != stashFilter && info.type != stashFilter && !(stashFilter == "armor" && (info.type == "helmet" || info.type == "rig" || info.type == "backpack"))) continue;
                var ii = it; UIKit.Tile(sc, info, it, () => ItemPopup(ii, "stash"));
            }
        }

        static string SlotFilter(string slot) => slot == "primary" || slot == "secondary" || slot == "holster" || slot == "melee" ? "weapon" : slot == "helmet" || slot == "armor" || slot == "rig" || slot == "backpack" ? "armor" : "";

        /// <summary>Единое окно предмета: описание + действия по контексту (stash / carry / equipped:slot).</summary>
        void ItemPopup(ItemInstance it, string ctx, Grid fromGrid = null)
        {
            var info = it.Info; if (info == null) return;
            UIKit.Popup(info.name, 820, 560, out var c, UIKit.RarityColor(info.rarity));
            var top = UIKit.Row(c, 16, 110);
            var ic = UIKit.Icon(top, UIKit.IconHintFor(info), Color.white, 100, info.name.Substring(0, 1)); var il = ic.gameObject.AddComponent<LayoutElement>(); il.minWidth = 100; il.minHeight = 100;
            UIKit.Cell(top, UIKit.Describe(it), 0, 17);
            var acts = UIKit.Grid(c, new Vector2(248, 56), 10, 0);
            var lo = P.loadout;
            bool equippable = info.IsWeapon || info.type == "armor" || info.type == "helmet" || info.type == "rig" || info.type == "backpack" || (info.type == "container" && info.item.P("secure") > 0) || (info.type == "tool" && info.item.P("night") > 0);
            if (ctx.StartsWith("equipped:"))
            {
                string slot = ctx.Substring(9);
                UIKit.BigButton(acts, "Снять на склад", UIKit.Blue, () => { if (P.AddToStash(it)) { lo.eq.Set(slot, null); Done(); } else Game.Notify("Склад полон"); }, 0, 52, 18);
            }
            else
            {
                if (equippable) UIKit.BigButton(acts, "Надеть", UIKit.Green, () => { Equip(it, fromGrid); Done(); }, 0, 52, 18, UIKit.TextDark);
                if (ctx == "stash") UIKit.BigButton(acts, "Взять с собой", UIKit.Cyan, () => { var one = it.Stackable && it.count > 1 ? new ItemInstance(it.id, Mathf.Min(it.count, it.MaxStack)) : it; if (lo.TryAdd(one)) { if (one == it) P.stash.Remove(it); else { it.count -= one.count; if (it.count <= 0) P.stash.Remove(it); } Done(); } else Game.Notify("Нет места в карманах/разгрузке/рюкзаке"); }, 0, 52, 18, UIKit.TextDark);
                if (ctx == "carry") UIKit.BigButton(acts, "На склад", UIKit.Blue, () => { if (P.AddToStash(it)) { fromGrid?.Remove(it); Done(); } else Game.Notify("Склад полон"); }, 0, 52, 18);
            }
            if (it.IsWeapon) UIKit.BigButton(acts, "Модификация", UIKit.Purple, () => { UIKit.CloseAllPopups(); ModPopup(it); }, 0, 52, 18);
            if (it.durability >= 0 && TraderSystem.RepairCost(it) > 0) UIKit.BigButton(acts, $"Ремонт {TraderSystem.RepairCost(it):N0} ₽", UIKit.Orange, () => { if (TraderSystem.Repair(it)) Done(); }, 0, 52, 17, UIKit.TextDark);
            if (!it.insured && (it.IsWeapon || info.type == "armor" || info.type == "helmet" || info.type == "rig" || info.type == "backpack")) UIKit.BigButton(acts, $"Страховка {TraderSystem.InsurancePrice(it):N0} ₽", UIKit.Cyan * new Color(1, 1, 1, 0.8f), () => { if (TraderSystem.Insure(it)) Done(); }, 0, 52, 17, UIKit.TextDark);
            if (ctx == "stash") UIKit.BigButton(acts, $"Продать {TraderSystem.SellPrice(it):N0} ₽", UIKit.Yellow, () => { if (TraderSystem.Sell(it, BestTraderFor(info))) Done(); }, 0, 52, 17, UIKit.TextDark);
            if (ctx == "stash") UIKit.BigButton(acts, "Выбросить", UIKit.Red * new Color(1, 1, 1, 0.8f), () => { P.stash.Remove(it); Done(); }, 0, 52, 18);
        }

        static string BestTraderFor(ItemInfo info) => !string.IsNullOrEmpty(info.trader) && Database.Trader(info.trader) != null ? info.trader : "fence";

        void Equip(ItemInstance it, Grid fromGrid)
        {
            var lo = P.loadout; var info = it.Info; string slot;
            if (info.IsWeapon) slot = info.weapon.cls == "pistol" ? "holster" : info.weapon.cls == "melee" ? "melee" : lo.eq.Get("primary") == null ? "primary" : "secondary";
            else if (info.type == "container") slot = "secure"; else if (info.type == "tool") slot = "headgear"; else slot = info.type;
            if (!Equipment.SlotAccepts(slot, it)) return;
            var old = lo.eq.Get(slot);
            if (fromGrid != null) fromGrid.Remove(it); else P.stash.Remove(it);
            lo.eq.Set(slot, it);
            if (old != null) P.AddToStash(old);
        }

        void Done() { UIKit.CloseAllPopups(); Stats.Rebuild(P); SaveSystem.Save(P); Rebuild(); }

        void ModPopup(ItemInstance w)
        {
            var def = w.Info.weapon; var wc = WeaponStats.Compute(w);
            UIKit.Popup("Модификация: " + def.name, 1100, 760, out var c, UIKit.Purple);
            UIKit.Label(c, $"Отдача ↕{wc.recoilV:0} ↔{wc.recoilH:0} · Эргономика {wc.ergo:0} · Точность {wc.moa:0.00} MOA · Темп {wc.rpm:0} · Магазин {w.ammoInMag}/{wc.magCap} · Зум {wc.zoom:0.0}x · Вес {wc.weight:0.0} кг{(wc.suppressed ? " · ГЛУШИТЕЛЬ" : "")}", 17, TextAnchor.MiddleLeft, UIKit.TextC, false, 30);
            var ammoRow = UIKit.Row(c, 8, 42); UIKit.Cell(ammoRow, "Патроны:", 110, 18, TextAnchor.MiddleLeft, UIKit.Muted);
            foreach (var a in Database.AmmoFor(def.caliber)) { var aa = a; UIKit.BigButton(ammoRow, $"{a.name.Replace(def.caliber + " ", "")} ({P.CountAll(a.id)})", w.ammoId == a.id ? UIKit.Yellow : new Color(1, 1, 1, 0.12f), () => { w.ammoId = aa.id; w.ammoInMag = Mathf.Min(w.ammoInMag, wc.magCap); SaveSystem.Save(P); UIKit.CloseAllPopups(); ModPopup(w); }, 0, 38, 15, w.ammoId == a.id ? UIKit.TextDark : Color.white); }
            UIKit.BigButton(ammoRow, "Зарядить", UIKit.Green, () => { w.ammoInMag += P.TakeAll(w.ammoId, wc.magCap - w.ammoInMag); SaveSystem.Save(P); UIKit.CloseAllPopups(); ModPopup(w); }, 140, 38, 15, UIKit.TextDark);
            var sc = UIKit.Scroll(c, out var sr); var le = sr.gameObject.AddComponent<LayoutElement>(); le.flexibleHeight = 1;
            foreach (var slot in def.slots)
            {
                w.mods.TryGetValue(slot, out var curId); var cur = Database.Mod(curId); string s = slot;
                var hdr = UIKit.Row(sc, 8, 40); UIKit.Cell(hdr, $"<b>{SlotName(slot)}</b>: <color=#ffd400>{(cur != null ? cur.name : "пусто")}</color>", 0, 18);
                if (cur != null) UIKit.BigButton(hdr, "Снять", UIKit.Blue, () => { if (P.AddToStash(new ItemInstance(cur.id))) { w.mods.Remove(s); SaveSystem.Save(P); UIKit.CloseAllPopups(); ModPopup(w); } else Game.Notify("Склад полон"); }, 110, 34, 15);
                var inStash = P.stash.Items.Where(i => i.IsMod && i.Info.mod.slot == slot && i.Info.mod.Fits(def) && i.id != curId).ToList();
                foreach (var m in inStash)
                {
                    var mm = m; var r = UIKit.Row(sc, 8, 34); UIKit.Cell(r, $"   {m.Name}  <color=#b9c3e6>{StatsText(m.Info.mod)}</color>", 0, 15);
                    UIKit.BigButton(r, "Установить", UIKit.Green, () => { if (cur != null && !P.AddToStash(new ItemInstance(cur.id))) { Game.Notify("Склад полон"); return; } P.stash.Remove(mm); w.mods[s] = mm.id; P.AddSkillXp("intellect", 4); SaveSystem.Save(P); UIKit.CloseAllPopups(); ModPopup(w); }, 140, 30, 14, UIKit.TextDark);
                }
                foreach (var t in Database.TraderList)
                    foreach (var info in TraderSystem.Stock(t.id).Where(i => i.IsMod && i.mod.slot == slot && i.mod.Fits(def) && i.id != curId && !inStash.Any(x => x.id == i.id)))
                    {
                        var ii = info; long price = TraderSystem.BuyPrice(info, t.id);
                        var r = UIKit.Row(sc, 8, 32); UIKit.Cell(r, $"   <color=#b9c3e6>{t.name}:</color> {info.name}  <color=#b9c3e6>{StatsText(info.mod)}</color>", 0, 14);
                        UIKit.BigButton(r, $"Купить и поставить {price:N0} ₽", UIKit.Yellow, () => { if (P.money < price) { Game.Notify("Не хватает денег"); return; } if (cur != null && !P.AddToStash(new ItemInstance(cur.id))) { Game.Notify("Склад полон"); return; } P.money -= price; w.mods[s] = ii.id; SaveSystem.Save(P); UIKit.CloseAllPopups(); ModPopup(w); }, 260, 28, 13, UIKit.TextDark);
                    }
            }
        }

        static string SlotName(string s) => s == "muzzle" ? "Дульное устройство" : s == "sight" || s == "sight_p" ? "Прицел" : s == "grip" ? "Рукоятка" : s == "stock" ? "Приклад" : s == "mag" ? "Магазин" : s == "handguard" ? "Цевьё" : s == "light" ? "Фонарь/ЛЦУ" : s == "barrel" ? "Ствол" : s == "bipod" ? "Сошки" : s;
        static string StatsText(ModDef m) => string.Join(" ", m.stats.Select(kv => kv.Key == "recoil" ? $"отдача {kv.Value * 100:+0;-0}%" : kv.Key == "ergo" ? $"эрг {kv.Value:+0;-0}" : kv.Key == "moa" ? $"точн {kv.Value * 100:+0;-0}%" : kv.Key == "capacity" ? $"ёмк {kv.Value:+0;-0}" : kv.Key == "zoom" ? $"зум {kv.Value}x" : kv.Key == "suppressed" ? "глушитель" : kv.Key == "reload" ? $"перезар {kv.Value * 100:+0;-0}%" : kv.Key == "velocity" ? $"скор {kv.Value * 100:+0;-0}%" : kv.Key == "laser" ? "ЛЦУ" : kv.Key == "light" ? "фонарь" : kv.Key == "night" ? "ночной" : kv.Key == "weight" ? $"вес {kv.Value:+0.0;-0.0}" : ""));

        // ================================================================ МАГАЗИН
        void BuildShop()
        {
            var col = UIKit.Column(content, 10, 0); UIKit.Stretch(col);
            var traders = UIKit.Row(col, 10, 92);
            foreach (var t in Database.TraderList)
            {
                var tt = t; bool sel = trader == t.id; int lv = P.TraderLevel(t.id);
                var card = UIKit.Card(traders, sel ? UIKit.Yellow : new Color(1, 1, 1, 0.1f), 18); var le = card.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1;
                var b = card.gameObject.AddComponent<Button>(); b.targetGraphic = card; b.onClick.AddListener(() => { Fx.Click(); trader = tt.id; Rebuild(); }); card.gameObject.AddComponent<HoverScale>();
                var av = UIKit.Img(card.transform, UIKit.RarityColor(lv + 1)); av.sprite = UIKit.Circle(); UIKit.Place(av.rectTransform, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(60, 60));
                var init = UIKit.Label(av.transform, t.name.Substring(0, 1), 30, TextAnchor.MiddleCenter, Color.white, true, 0, true); UIKit.Stretch(init.rectTransform);
                var n = UIKit.Label(card.transform, t.name, 20, TextAnchor.MiddleLeft, sel ? UIKit.TextDark : Color.white, true); UIKit.Stretch(n.rectTransform, 84, 10, 8, 44);
                var s = UIKit.Label(card.transform, $"уровень {lv}/4 · {t.sells.Count} категорий", 14, TextAnchor.MiddleLeft, sel ? UIKit.TextDark : UIKit.Muted, false); UIKit.Stretch(s.rectTransform, 84, 44, 8, 10);
            }
            var td = Database.Trader(trader);
            UIKit.Label(col, $"<b>{td.name}</b> — {td.desc}  <color=#b9c3e6>Репутация {P.Rep(trader):0.00}, скидка {(1 - 1f / Mathf.Max(0.01f, Stats.Mul("tradePrice"))) * 100:0}%</color>", 18, TextAnchor.MiddleLeft, UIKit.TextC, false, 30);
            var filt = UIKit.Row(col, 6, 40);
            foreach (var (k, n) in new[] { ("", "Все"), ("weapon", "Оружие"), ("mod", "Моды"), ("ammo", "Патроны"), ("med", "Медицина"), ("food", "Еда"), ("armor", "Броня"), ("backpack", "Рюкзаки"), ("barter", "Ресурсы"), ("throwable", "Гранаты"), ("sell", "ПРОДАТЬ") })
            { string kk = k; UIKit.BigButton(filt, n, shopFilter == k ? (k == "sell" ? UIKit.Green : UIKit.Yellow) : new Color(1, 1, 1, 0.12f), () => { shopFilter = kk; Rebuild(); }, 0, 36, 13, shopFilter == k ? UIKit.TextDark : Color.white); }
            var sc = UIKit.Scroll(col, out var sr, true, new Vector2(150, 176)); var sle = sr.gameObject.AddComponent<LayoutElement>(); sle.flexibleHeight = 1;
            if (shopFilter == "sell")
            {
                foreach (var it in P.stash.Items.OrderByDescending(i => TraderSystem.SellPrice(i)).ToList()) { var ii = it; UIKit.Tile(sc, it.Info, it, () => SellPopup(ii), null, false, $"{TraderSystem.SellPrice(it):N0} ₽"); }
                return;
            }
            foreach (var info in TraderSystem.Stock(trader))
            {
                if (shopFilter != "" && info.kind != shopFilter && info.type != shopFilter && !(shopFilter == "armor" && info.type == "helmet") && !(shopFilter == "backpack" && info.type == "rig")) continue;
                var ii = info; long price = TraderSystem.BuyPrice(info, trader);
                UIKit.Tile(sc, info, null, () => BuyPopup(ii), info.stack > 1 ? "×" + info.stack : null, false, $"{price:N0} ₽");
            }
        }

        void BuyPopup(ItemInfo info)
        {
            long price = TraderSystem.BuyPrice(info, trader);
            UIKit.Popup(info.name, 760, 420, out var c, UIKit.RarityColor(info.rarity));
            var top = UIKit.Row(c, 16, 100); var ic = UIKit.Icon(top, UIKit.IconHintFor(info), Color.white, 90, info.name.Substring(0, 1)); var il = ic.gameObject.AddComponent<LayoutElement>(); il.minWidth = 90;
            UIKit.Cell(top, $"<color={UIKit.RarityHex(info.rarity)}>{UIKit.RarityName(info.rarity)}</color> · {info.desc}\nЦена {price:N0} ₽{(info.stack > 1 ? $" за {info.stack} шт." : "")} · у вас {P.money:N0} ₽ · вес {info.weight:0.0} кг · размер {info.w}×{info.h}", 0, 17);
            var row = UIKit.Row(c, 10, 64);
            UIKit.BigButton(row, $"Купить ×1 — {price:N0} ₽", UIKit.Yellow, () => { if (TraderSystem.Buy(info, trader, info.stack > 1 ? info.stack : 1)) Done(); }, 0, 60, 20, UIKit.TextDark);
            UIKit.BigButton(row, $"×5 — {price * 5:N0} ₽", UIKit.Orange, () => { if (TraderSystem.Buy(info, trader, (info.stack > 1 ? info.stack : 1) * 5)) Done(); }, 220, 60, 20, UIKit.TextDark);
        }

        void SellPopup(ItemInstance it)
        {
            long price = TraderSystem.SellPrice(it);
            UIKit.Popup("Продать: " + it.Name, 760, 400, out var c, UIKit.Green);
            UIKit.Label(c, UIKit.Describe(it), 17, TextAnchor.UpperLeft, UIKit.TextC, false, 120);
            UIKit.BigButton(c, $"Продать за {price:N0} ₽", UIKit.Green, () => { if (TraderSystem.Sell(it, trader)) Done(); }, 0, 64, 24, UIKit.TextDark);
        }

        // ================================================================ ЗАДАНИЯ
        void BuildQuests()
        {
            var left = UIKit.Rect(content, "left"); left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0.5f, 1); left.offsetMin = Vector2.zero; left.offsetMax = new Vector2(-10, 0);
            var lc = UIKit.Scroll(left, out var lsr); UIKit.Stretch(lsr.GetComponent<RectTransform>());
            UIKit.Header(lc, "Активные задания", 26);
            var act = QuestSystem.Active();
            if (act.Count == 0) UIKit.Label(lc, "Пока нет. Возьмите задания справа — за них дают опыт, деньги и уникальное снаряжение.", 18, TextAnchor.UpperLeft, UIKit.Muted, false, 50);
            foreach (var q in act) QuestCard(lc, q, true);
            var right = UIKit.Rect(content, "right"); right.anchorMin = new Vector2(0.5f, 0); right.anchorMax = new Vector2(1, 1); right.offsetMin = new Vector2(10, 0); right.offsetMax = Vector2.zero;
            var rc = UIKit.Scroll(right, out var rsr); UIKit.Stretch(rsr.GetComponent<RectTransform>());
            UIKit.Header(rc, "Доступные", 26);
            foreach (var t in Database.TraderList) foreach (var q in QuestSystem.ForTrader(t.id, "available")) QuestCard(rc, q, false);
            int done = Database.QuestList.Count(q => QuestSystem.State(q.id).status == "completed");
            UIKit.Label(rc, $"Выполнено {done} из {Database.Quests.Count}", 16, TextAnchor.MiddleLeft, UIKit.Muted, true, 30);
        }

        void QuestCard(Transform parent, QuestDef q, bool active)
        {
            var card = UIKit.Card(parent, UIKit.Panel, 20); var col = UIKit.Column(card.transform, 6, 16, true); UIKit.Stretch(col);
            var fit = card.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hdr = UIKit.Row(col, 8, 36); UIKit.Cell(hdr, $"<b>{q.name}</b>", 0, 21, TextAnchor.MiddleLeft, UIKit.Yellow); UIKit.Cell(hdr, $"{Database.Trader(q.trader)?.name} · ур. {q.minLevel}{(string.IsNullOrEmpty(q.map) ? "" : " · " + Database.Map(q.map)?.name)}", 320, 14, TextAnchor.MiddleRight, UIKit.Muted);
            UIKit.Label(col, q.desc, 16, TextAnchor.UpperLeft, UIKit.TextC, false);
            for (int i = 0; i < q.objectives.Count; i++)
            {
                var o = q.objectives[i]; bool d = active && QuestSystem.ObjectiveDone(q, i); int prog = active ? QuestSystem.ObjectiveProgress(q, i) : 0;
                var r = UIKit.Row(col, 8, 24); UIKit.Cell(r, (d ? "<color=#5cf25a>✓</color> " : "• ") + o.Describe(), 0, 15, TextAnchor.MiddleLeft, d ? UIKit.Green : UIKit.TextC);
                if (active) { var bar = UIKit.Bar(r, d ? UIKit.Green : UIKit.Cyan, 12, 160); bar.fillAmount = o.Goal > 0 ? (float)prog / o.Goal : 1; UIKit.Cell(r, $"{prog}/{o.Goal}", 60, 14, TextAnchor.MiddleRight, UIKit.Muted); }
            }
            var rw = q.rewards;
            var foot = UIKit.Row(col, 8, 44);
            UIKit.Cell(foot, $"<color=#19d3ff>+{rw.xp} XP</color>  <color=#ffd400>+{rw.money:N0} ₽</color>{(rw.items.Count > 0 ? "  " + string.Join(", ", rw.items.Select(k => Database.NameOf(k.Key) + (k.Value > 1 ? " ×" + k.Value : ""))) : "")}", 0, 15);
            if (!active) UIKit.BigButton(foot, "Взять", UIKit.Yellow, () => { QuestSystem.Accept(q.id); Rebuild(); }, 130, 40, 17, UIKit.TextDark);
            else if (QuestSystem.ReadyToComplete(q)) UIKit.BigButton(foot, "Сдать", UIKit.Green, () => { QuestSystem.Complete(q.id); Done(); }, 130, 40, 17, UIKit.TextDark);
        }

        // ================================================================ ПРОКАЧКА
        void BuildProgress()
        {
            var left = UIKit.Rect(content, "left"); left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0.45f, 1); left.offsetMin = Vector2.zero; left.offsetMax = new Vector2(-10, 0);
            var lc = UIKit.Scroll(left, out var lsr); UIKit.Stretch(lsr.GetComponent<RectTransform>());
            UIKit.Header(lc, "Навыки", 26);
            UIKit.Label(lc, "Растут от действий. Каждые 10 уровней — особый бонус.", 15, TextAnchor.UpperLeft, UIKit.Muted, false, 24);
            foreach (var grp in Database.SkillList.GroupBy(s => s.group))
            {
                UIKit.Label(lc, grp.Key.ToUpperInvariant(), 17, TextAnchor.MiddleLeft, UIKit.Cyan, true, 28, true);
                foreach (var s in grp)
                {
                    int lv = P.SkillLevel(s.id); int next = ((lv / 10) + 1) * 10; string ms = s.milestones.TryGetValue(next, out var m) ? $"{next}: {m}" : "";
                    var card = UIKit.Card(lc, new Color(1, 1, 1, 0.07f), 14); var le = card.gameObject.AddComponent<LayoutElement>(); le.minHeight = 58;
                    var n = UIKit.Label(card.transform, $"{s.name}  <color=#ffd400>ур. {lv}</color>", 17, TextAnchor.MiddleLeft, Color.white, true); UIKit.Stretch(n.rectTransform, 14, 6, 14, 30);
                    var brt = UIKit.Rect(card.transform, "b"); brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.offsetMin = new Vector2(14, 8); brt.offsetMax = new Vector2(-14, 18); var bar = UIKit.Bar(brt, UIKit.Cyan, 10); UIKit.Stretch(bar.transform.parent.GetComponent<RectTransform>()); bar.fillAmount = P.SkillProgress01(s.id);
                    var d = UIKit.Label(card.transform, ms.Length > 0 ? ms : s.desc, 12, TextAnchor.MiddleRight, UIKit.Muted, false); UIKit.Stretch(d.rectTransform, 220, 6, 14, 30);
                }
            }
            var right = UIKit.Rect(content, "right"); right.anchorMin = new Vector2(0.45f, 0); right.anchorMax = new Vector2(1, 1); right.offsetMin = new Vector2(10, 0); right.offsetMax = Vector2.zero;
            var rcol = UIKit.Column(right, 8, 0); UIKit.Stretch(rcol);
            var hdr = UIKit.Row(rcol, 10, 44); UIKit.Header(hdr, $"Перки · очков {P.perkPoints}", 26);
            var br = UIKit.Row(rcol, 6, 44);
            foreach (var b in Database.Branches) { var bb = b; UIKit.BigButton(br, b.Value, perkBranch == b.Key ? UIKit.Yellow : new Color(1, 1, 1, 0.12f), () => { perkBranch = bb.Key; Rebuild(); }, 0, 40, 15, perkBranch == b.Key ? UIKit.TextDark : Color.white); }
            var sc = UIKit.Scroll(rcol, out var sr, true, new Vector2(300, 150)); var sle = sr.gameObject.AddComponent<LayoutElement>(); sle.flexibleHeight = 1;
            foreach (var p in Database.PerkList.Where(x => x.branch == perkBranch).OrderBy(x => x.tier))
            {
                bool have = P.perks.Contains(p.id); bool can = P.CanBuyPerk(p); var pp = p;
                var card = UIKit.Card(sc, have ? Color.Lerp(UIKit.Green, UIKit.Dark, 0.45f) : can ? UIKit.Panel : new Color(0, 0, 0, 0.35f), 18);
                var tier = UIKit.Label(card.transform, new string('★', p.tier), 16, TextAnchor.UpperRight, UIKit.Yellow, true); UIKit.Stretch(tier.rectTransform, 8, 8, 10, 0);
                var n = UIKit.Label(card.transform, p.name, 19, TextAnchor.UpperLeft, have ? UIKit.Green : can ? Color.white : UIKit.Muted, true, 0, true); UIKit.Stretch(n.rectTransform, 12, 10, 60, 0);
                var d = UIKit.Label(card.transform, p.desc, 14, TextAnchor.UpperLeft, UIKit.TextC, false); UIKit.Stretch(d.rectTransform, 12, 42, 12, 48);
                if (have) { var t = UIKit.Label(card.transform, "ИЗУЧЕНО", 14, TextAnchor.MiddleLeft, UIKit.Green, true); UIKit.Stretch(t.rectTransform, 12, 110, 12, 8); }
                else { var b = UIKit.BigButton(card.transform, $"Изучить ({p.cost})", can ? UIKit.Yellow : new Color(1, 1, 1, 0.1f), () => { if (P.BuyPerk(pp.id)) Done(); else Game.Notify(P.perkPoints < pp.cost ? "Не хватает очков" : "Сначала откройте предыдущий ряд"); }, 0, 36, 15, can ? UIKit.TextDark : UIKit.Muted); UIKit.Place(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(276, 36)); }
            }
        }

        // ================================================================ УБЕЖИЩЕ
        void BuildHideout()
        {
            var col = UIKit.Column(content, 8, 0); UIKit.Stretch(col);
            UIKit.Header(col, "Убежище", 26);
            var sc = UIKit.Scroll(col, out var sr); var le = sr.gameObject.AddComponent<LayoutElement>(); le.flexibleHeight = 1;
            var grid = UIKit.Grid(sc, new Vector2(440, 150), 12, 0);
            foreach (var m in Database.HideoutList)
            {
                int lv = P.HideoutLevel(m.id); var nl = HideoutSystem.NextLevel(m); string miss = nl != null ? HideoutSystem.MissingFor(m) : "";
                var card = UIKit.Card(grid, lv > 0 ? Color.Lerp(UIKit.Blue, UIKit.Dark, 0.5f) : UIKit.Panel, 20);
                var ic = UIKit.Icon(card.transform, "home", UIKit.Cyan, 44); UIKit.Place(ic.rectTransform, new Vector2(0, 1), new Vector2(14, -12), new Vector2(44, 44));
                var n = UIKit.Label(card.transform, $"{m.name}  <size=14><color=#b9c3e6>{new string('●', lv)}{new string('○', m.levels.Count - lv)}</color></size>", 20, TextAnchor.UpperLeft, Color.white, true, 0, true); UIKit.Stretch(n.rectTransform, 68, 10, 12, 0);
                var d = UIKit.Label(card.transform, m.desc + (nl != null ? $"\n<color=#b9c3e6>Далее: {nl.money:N0} ₽ + {string.Join(", ", nl.items.Select(k => Database.NameOf(k.Key) + "×" + k.Value))}</color>" : "\n<color=#5cf25a>Максимальный уровень</color>"), 13, TextAnchor.UpperLeft, UIKit.TextC, false); UIKit.Stretch(d.rectTransform, 68, 40, 12, 44);
                if (nl != null)
                {
                    var mm = m; var b = UIKit.BigButton(card.transform, miss.Length == 0 ? "Улучшить" : "Не хватает ресурсов", miss.Length == 0 ? UIKit.Green : new Color(1, 1, 1, 0.1f), () => { if (!HideoutSystem.Upgrade(mm)) Game.Notify("Не хватает: " + miss); else Done(); }, 0, 34, 15, miss.Length == 0 ? UIKit.TextDark : UIKit.Muted);
                    UIKit.Place(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(410, 34));
                }
            }
            UIKit.Header(sc, "Крафт", 24);
            foreach (var c in P.crafts) { var rd = Database.Recipe(c.recipe); double left = c.finishAt - PlayerProfile.Now(); UIKit.Label(sc, $"⚙ {rd?.name}: {(left > 0 ? $"{(int)(left / 60)} мин {(int)(left % 60)} с" : "готово — заберётся автоматически")}", 17, TextAnchor.MiddleLeft, UIKit.Yellow, true, 26); }
            var recipes = HideoutSystem.AvailableRecipes();
            if (recipes.Count == 0) UIKit.Label(sc, "Рецепты открываются улучшением верстака, медпункта, кухни и сборщика воды.", 16, TextAnchor.MiddleLeft, UIKit.Muted, false, 30);
            foreach (var rd in recipes)
            {
                string miss = HideoutSystem.MissingForRecipe(rd); var rr = rd;
                var r = UIKit.Row(sc, 8, 40);
                UIKit.Cell(r, $"<b>{rd.name}</b>  <color=#b9c3e6>{string.Join(", ", rd.inputs.Select(k => Database.NameOf(k.Key) + "×" + k.Value))} → {string.Join(", ", rd.output.Select(k => Database.NameOf(k.Key) + "×" + k.Value))} · {rd.time / 60:0} мин</color>{(miss.Length > 0 ? $"  <color=#ff3b5c>нет: {miss}</color>" : "")}", 0, 14);
                UIKit.BigButton(r, "Создать", miss.Length == 0 ? UIKit.Green : new Color(1, 1, 1, 0.1f), () => { if (HideoutSystem.StartCraft(rr)) Done(); }, 130, 34, 15, miss.Length == 0 ? UIKit.TextDark : UIKit.Muted);
            }
        }
    }
}
