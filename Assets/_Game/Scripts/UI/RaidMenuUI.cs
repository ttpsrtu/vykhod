using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>Меню в рейде: плитки инвентаря и контейнера, действия через попап, пауза.</summary>
    public class RaidMenuUI
    {
        Canvas canvas; RectTransform invContent, lootContent, pausePanel, invPanel, lootPanel; Text lootTitle, invTitle;
        public bool Open;
        int containerId = -1; List<ItemInstance> containerItems = new List<ItemInstance>(); LootContainer container;

        void Build()
        {
            canvas = UIKit.NewCanvas("RaidMenu", 20);
            var root = UIKit.Fill(canvas.transform, new Color(0.02f, 0.02f, 0.1f, 0.6f));
            invPanel = UIKit.Box(root, new Vector2(0, 0), new Vector2(0.55f, 1), new Vector2(24, 24), new Vector2(-8, -24), UIKit.PanelSolid, "inv", 26);
            invTitle = UIKit.Header(invPanel, "Снаряжение", 28); UIKit.Place(invTitle.rectTransform, new Vector2(0, 1), new Vector2(20, -10), new Vector2(900, 44));
            var sc = UIKit.Rect(invPanel, "scrollHost"); UIKit.Stretch(sc, 12, 64, 12, 12);
            invContent = UIKit.Scroll(sc, out var sr); UIKit.Stretch(sr.GetComponent<RectTransform>());
            lootPanel = UIKit.Box(root, new Vector2(0.55f, 0), new Vector2(1, 1), new Vector2(8, 24), new Vector2(-24, -24), UIKit.PanelSolid, "loot", 26);
            lootTitle = UIKit.Header(lootPanel, "Контейнер", 28); UIKit.Place(lootTitle.rectTransform, new Vector2(0, 1), new Vector2(20, -10), new Vector2(700, 44));
            var sc2 = UIKit.Rect(lootPanel, "scrollHost2"); UIKit.Stretch(sc2, 12, 64, 12, 12);
            lootContent = UIKit.Scroll(sc2, out var sr2); UIKit.Stretch(sr2.GetComponent<RectTransform>());
            pausePanel = UIKit.Box(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -300), new Vector2(300, 300), UIKit.PanelSolid, "pause", 28);
            var pc = UIKit.Column(pausePanel, 12, 24); UIKit.Stretch(pc);
            UIKit.Label(pc, "ПАУЗА", 44, TextAnchor.MiddleCenter, UIKit.Yellow, true, 60, true);
            UIKit.Label(pc, "Игра продолжается — боты не ждут", 16, TextAnchor.MiddleCenter, UIKit.Muted, false, 24);
            UIKit.BigButton(pc, "Продолжить", UIKit.Green, Hide, 0, 60, 24, UIKit.TextDark);
            UIKit.BigButton(pc, "Инвентарь (Tab)", UIKit.Blue, ShowInventory, 0, 56, 22);
            UIKit.BigButton(pc, "Настройки", UIKit.Purple, MainMenuUI.SettingsPopup, 0, 56, 22);
            UIKit.Label(pc, $"Ваш IP для друзей: {NetGame.LocalIPv4()} : {Game.Manager.Net.Port}", 15, TextAnchor.MiddleCenter, UIKit.Muted, false, 24);
            UIKit.BigButton(pc, "Покинуть рейд (всё теряется)", UIKit.Red, () => Game.Manager.AbortRaid(), 0, 52, 18);
        }

        public void ShowInventory() { if (canvas == null) Build(); canvas.gameObject.SetActive(true); Open = true; pausePanel.gameObject.SetActive(false); invPanel.gameObject.SetActive(true); lootPanel.gameObject.SetActive(true); if (container == null) containerId = -1; Refresh(); }
        public void ShowPause() { if (canvas == null) Build(); canvas.gameObject.SetActive(true); Open = true; pausePanel.gameObject.SetActive(true); invPanel.gameObject.SetActive(false); lootPanel.gameObject.SetActive(false); }
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); Open = false; container = null; containerId = -1; UIKit.CloseAllPopups(); }
        public void OpenWith(LootContainer c) { container = c; containerId = c.Id; containerItems.Clear(); ShowInventory(); }
        public void SetContainer(int id, List<ItemInstance> items) { if (id != containerId) return; containerItems = items; Refresh(); }

        public void Tick()
        {
            if (!Open || container == null) return;
            var lp = Game.Manager.Net.LocalPlayer;
            if (lp != null && Vector3.Distance(lp.transform.position, container.transform.position) > 4.5f) { container = null; containerId = -1; Refresh(); }
        }

        public void Refresh()
        {
            if (!Open || invContent == null) return;
            var lo = Game.Manager.RaidLoadout; var lp = Game.Manager.Net.LocalPlayer;
            UIKit.Clear(invContent);
            invTitle.text = $"СНАРЯЖЕНИЕ  <size=16><color=#b9c3e6>{lo.Weight:0.0} кг · {lo.Value:N0} ₽</color></size>";
            var eqGrid = UIKit.Grid(invContent, new Vector2(118, 134), 8, 0);
            foreach (var slot in Equipment.SlotNames)
            {
                var it = lo.eq.Get(slot); string s = slot;
                if (it != null) UIKit.Tile(eqGrid, it.Info, it, () => ItemPopup(it, "eq:" + s, null), null, lp.Weapons.Current == it, Equipment.SlotTitles[slot]);
                else { var e = UIKit.Card(eqGrid, new Color(1, 1, 1, 0.06f), 16); var t = UIKit.Label(e.transform, Equipment.SlotTitles[slot], 13, TextAnchor.MiddleCenter, UIKit.Muted, true); UIKit.Stretch(t.rectTransform, 4, 4, 4, 4); }
            }
            foreach (var g in lo.Grids())
            {
                UIKit.Label(invContent, $"{g.Key}  <color=#b9c3e6>свободно {g.Value.FreeCells} из {g.Value.W * g.Value.H}</color>", 18, TextAnchor.MiddleLeft, UIKit.Yellow, true, 28, true);
                var gg = UIKit.Grid(invContent, new Vector2(104, 118), 6, 0);
                foreach (var it in g.Value.Items.ToList()) { var ii = it; var grid = g.Value; UIKit.Tile(gg, it.Info, it, () => ItemPopup(ii, "grid", grid)); }
            }
            UIKit.Clear(lootContent);
            if (container == null)
            {
                lootTitle.text = "РЯДОМ НЕТ КОНТЕЙНЕРА";
                UIKit.Label(lootContent, "Подойдите к ящику, телу или шкафу и зажмите F.\n\n<color=#ffd400>Управление</color>\nWASD движение · Shift бег · Ctrl присед · Q/E наклон\nЛКМ огонь · ПКМ прицел · R перезарядка · B режим · T патроны\nG граната · 5 аптечка · 6 еда · N ПНВ · L фонарь · 1–4 оружие\nF взаимодействие · Tab инвентарь · Enter чат · Esc пауза", 17, TextAnchor.UpperLeft, UIKit.TextC, false);
                return;
            }
            lootTitle.text = container.Title.ToUpperInvariant();
            if (containerItems.Count == 0) UIKit.Label(lootContent, "Пусто", 18, TextAnchor.MiddleLeft, UIKit.Muted, true, 30);
            else UIKit.BigButton(lootContent, "Взять всё", UIKit.Green, () => { for (int i = containerItems.Count - 1; i >= 0; i--) Game.Manager.Net.Session.TakeFromContainerRpc(containerId, i); }, 0, 48, 20, UIKit.TextDark);
            var lg = UIKit.Grid(lootContent, new Vector2(118, 134), 8, 0);
            for (int i = 0; i < containerItems.Count; i++) { int idx = i; var it = containerItems[i]; UIKit.Tile(lg, it.Info, it, () => Game.Manager.Net.Session.TakeFromContainerRpc(containerId, idx), null, false, $"{it.Value:N0} ₽"); }
        }

        void ItemPopup(ItemInstance it, string ctx, Grid fromGrid)
        {
            var info = it.Info; if (info == null) return; var lo = Game.Manager.RaidLoadout; var lp = Game.Manager.Net.LocalPlayer;
            UIKit.Popup(info.name, 760, 480, out var c, UIKit.RarityColor(info.rarity));
            var top = UIKit.Row(c, 16, 100); var ic = UIKit.Icon(top, UIKit.IconHintFor(info), Color.white, 90, info.name.Substring(0, 1)); ic.gameObject.AddComponent<LayoutElement>().minWidth = 90;
            UIKit.Cell(top, UIKit.Describe(it), 0, 16);
            var acts = UIKit.Grid(c, new Vector2(230, 52), 8, 0);
            if (ctx.StartsWith("eq:"))
            {
                string slot = ctx.Substring(3);
                if (slot == "primary" || slot == "secondary" || slot == "holster" || slot == "melee") UIKit.BigButton(acts, "В руки", UIKit.Green, () => { lp.Weapons.Switch(slot); After(); }, 0, 48, 17, UIKit.TextDark);
                if (slot != "secure") UIKit.BigButton(acts, "Снять", UIKit.Blue, () => { if (lo.TryAdd(it)) { lo.eq.Set(slot, null); After(); } else Game.Notify("Нет места"); }, 0, 48, 17);
                if (container != null) UIKit.BigButton(acts, "В контейнер", UIKit.Orange, () => { lo.eq.Set(slot, null); Put(it); }, 0, 48, 17, UIKit.TextDark);
            }
            else
            {
                if (info.IsWeapon || info.type == "armor" || info.type == "helmet" || info.type == "rig" || info.type == "backpack" || (info.type == "tool" && info.item.P("night") > 0)) UIKit.BigButton(acts, "Надеть", UIKit.Green, () => { Equip(it); After(); }, 0, 48, 17, UIKit.TextDark);
                if (info.type == "med" || info.type == "food" || (info.type == "tool" && info.item.Has("repair"))) UIKit.BigButton(acts, "Использовать", UIKit.Cyan, () => { if (lp.Weapons.UseItem(it)) Hide(); }, 0, 48, 17, UIKit.TextDark);
                if (info.IsMod) UIKit.BigButton(acts, "На оружие в руках", UIKit.Purple, () => Attach(it), 0, 48, 16);
                if (container != null) UIKit.BigButton(acts, "В контейнер", UIKit.Orange, () => { fromGrid?.Remove(it); Put(it); }, 0, 48, 17, UIKit.TextDark);
                UIKit.BigButton(acts, "Выбросить", UIKit.Red, () => { fromGrid?.Remove(it); Game.Manager.Net.Session.DropItemRpc(it.ToJsonString(), lp.transform.position + lp.transform.forward); After(); }, 0, 48, 17);
            }
        }

        void Put(ItemInstance it) { Game.Manager.Net.Session.PutInContainerRpc(containerId, it.ToJsonString()); After(); }

        void Equip(ItemInstance it)
        {
            var lo = Game.Manager.RaidLoadout; var info = it.Info; string slot;
            if (info.IsWeapon) slot = info.weapon.cls == "pistol" ? "holster" : info.weapon.cls == "melee" ? "melee" : lo.eq.Get("primary") == null ? "primary" : "secondary";
            else if (info.type == "tool") slot = "headgear"; else slot = info.type;
            if (!Equipment.SlotAccepts(slot, it)) return;
            var old = lo.eq.Get(slot); lo.RemoveItem(it); lo.eq.Set(slot, it);
            if (old != null && !lo.TryAdd(old)) { lo.eq.Set(slot, old); lo.TryAdd(it); Game.Notify("Нет места для замены"); }
        }

        void Attach(ItemInstance mod)
        {
            var lp = Game.Manager.Net.LocalPlayer; var w = lp.Weapons.Current; var m = mod.Info.mod;
            if (w == null || !m.Fits(w.Info.weapon)) { Game.Notify("Не подходит к текущему оружию"); return; }
            var lo = Game.Manager.RaidLoadout;
            if (w.mods.TryGetValue(m.slot, out var oldId)) { var old = new ItemInstance(oldId); if (!lo.TryAdd(old)) { Game.Notify("Нет места для снятого мода"); return; } }
            lo.RemoveItem(mod); w.mods[m.slot] = mod.id; Game.Profile.AddSkillXp("intellect", 3); After();
        }

        void After()
        {
            UIKit.CloseAllPopups();
            var lp = Game.Manager.Net.LocalPlayer;
            lp.UpdateGearVars(); lp.Weapons.Rebuild(); if (lp.Weapons.Current == null) foreach (var s in new[] { "primary", "secondary", "holster", "melee" }) if (Game.Manager.RaidLoadout.eq.Get(s) != null) { lp.Weapons.Switch(s); break; }
            Stats.Rebuild(Game.Profile); Refresh(); Game.Manager.UI.OnInventoryChanged();
        }
    }
}
