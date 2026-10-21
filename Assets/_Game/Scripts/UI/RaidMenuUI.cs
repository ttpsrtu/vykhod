using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>Меню в рейде: инвентарь (экипировка + сетки), окно контейнера, пауза.</summary>
    public class RaidMenuUI
    {
        Canvas canvas; RectTransform invContent, lootContent, pausePanel, invPanel, lootPanel, detailCol;
        public bool Open;
        int containerId = -1; List<ItemInstance> containerItems = new List<ItemInstance>(); LootContainer container; Text lootTitle, invTitle;

        void Build()
        {
            canvas = UIKit.NewCanvas("RaidMenu", 20);
            var root = UIKit.Panel(canvas.transform, new Color(0, 0, 0, 0.55f));
            invPanel = UIKit.Box(root, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(20, 20), new Vector2(-10, -20), UIKit.Bg, "inv");
            invTitle = UIKit.Header(invPanel, "Снаряжение"); UIKit.Place(invTitle.rectTransform, new Vector2(0, 1), new Vector2(12, -8), new Vector2(600, 36));
            var sc = UIKit.Rect(invPanel, "scrollHost"); UIKit.Stretch(sc, 8, 52, 8, 8);
            invContent = UIKit.Scroll(sc, out var sr); UIKit.Stretch(sr.GetComponent<RectTransform>());
            lootPanel = UIKit.Box(root, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(10, 20), new Vector2(-20, -20), UIKit.Bg, "loot");
            lootTitle = UIKit.Header(lootPanel, "Контейнер"); UIKit.Place(lootTitle.rectTransform, new Vector2(0, 1), new Vector2(12, -8), new Vector2(600, 36));
            var sc2 = UIKit.Rect(lootPanel, "scrollHost2"); UIKit.Stretch(sc2, 8, 52, 8, 8);
            lootContent = UIKit.Scroll(sc2, out var sr2); UIKit.Stretch(sr2.GetComponent<RectTransform>());
            pausePanel = UIKit.Box(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-220, -220), new Vector2(220, 220), UIKit.Bg, "pause");
            var pc = UIKit.Column(pausePanel, 10, 20); UIKit.Stretch(pc);
            UIKit.Header(pc, "Пауза (игра продолжается)");
            UIKit.Btn(pc, "Продолжить", Hide, 0, 44);
            UIKit.Btn(pc, "Инвентарь (Tab)", ShowInventory, 0, 44);
            UIKit.SliderRow(pc, "Чувствительность", 0.2f, 5f, Game.Settings.sensitivity, v => { Game.Settings.sensitivity = v; Game.Settings.Save(); });
            UIKit.SliderRow(pc, "Громкость", 0f, 1f, Game.Settings.volume, v => { Game.Settings.volume = v; Game.Settings.Save(); Game.Settings.Apply(); });
            UIKit.Label(pc, $"Ваш IP для друзей: {NetGame.LocalIPv4()}  порт {Game.Manager.Net.Port}", 15, TextAnchor.MiddleCenter, UIKit.Muted);
            UIKit.Btn(pc, "Покинуть рейд (пропасть без вести)", () => Game.Manager.AbortRaid(), 0, 44, UIKit.Bad);
        }

        public void ShowInventory() { if (canvas == null) Build(); canvas.gameObject.SetActive(true); Open = true; pausePanel.gameObject.SetActive(false); invPanel.gameObject.SetActive(true); lootPanel.gameObject.SetActive(true); if (container == null) { containerId = -1; } Refresh(); }
        public void ShowPause() { if (canvas == null) Build(); canvas.gameObject.SetActive(true); Open = true; pausePanel.gameObject.SetActive(true); invPanel.gameObject.SetActive(false); lootPanel.gameObject.SetActive(false); }
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); Open = false; container = null; containerId = -1; }
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
            invTitle.text = $"Снаряжение   <size=16><color=#aaaaaa>вес {lo.Weight:0.0} кг · ценность {lo.Value}</color></size>";
            foreach (var slot in Equipment.SlotNames)
            {
                var it = lo.eq.Get(slot);
                var row = UIKit.Row(invContent, 6, 34); UIKit.Cell(row, Equipment.SlotTitles[slot], 120, 15, TextAnchor.MiddleLeft, UIKit.Muted);
                UIKit.Cell(row, it != null ? UIKit.ItemLine(it) : "<color=#555555>—</color>", 0, 16);
                if (it != null && (slot == "primary" || slot == "secondary" || slot == "holster" || slot == "melee")) UIKit.Btn(row, "В руки", () => { lp.Weapons.Switch(slot); Refresh(); }, 90, 30, null, 14);
                if (it != null && slot != "secure") UIKit.Btn(row, "Снять", () => { if (lo.TryAdd(it)) { lo.eq.Set(slot, null); AfterChange(); } else Game.Notify("Нет места"); }, 70, 30, null, 14);
                if (it != null && container != null) UIKit.Btn(row, "В ящик", () => { lo.eq.Set(slot, null); Put(it); }, 80, 30, null, 14);
            }
            foreach (var g in lo.Grids())
            {
                UIKit.Label(invContent, $"<b>{g.Key}</b>  <color=#aaaaaa>{g.Value.W}×{g.Value.H}, свободно {g.Value.FreeCells}</color>", 17, TextAnchor.MiddleLeft, UIKit.Accent, false, 28);
                foreach (var it in g.Value.Items.ToList())
                {
                    var row = UIKit.Row(invContent, 4, 32); UIKit.Cell(row, UIKit.ItemLine(it), 0, 15);
                    var info = it.Info; if (info == null) continue;
                    if (info.IsWeapon || info.type == "armor" || info.type == "helmet" || info.type == "rig" || info.type == "backpack" || (info.type == "tool" && info.item.P("night") > 0))
                        UIKit.Btn(row, "Надеть", () => Equip(it), 80, 28, null, 14);
                    if (info.type == "med" || info.type == "food" || (info.type == "tool" && info.item.Has("repair"))) UIKit.Btn(row, "Исп.", () => { if (lp.Weapons.UseItem(it)) Hide(); }, 60, 28, UIKit.Good * 0.6f, 14);
                    if (info.IsMod) UIKit.Btn(row, "На оружие", () => Attach(it), 100, 28, null, 14);
                    if (container != null) UIKit.Btn(row, "В ящик", () => { g.Value.Remove(it); Put(it); }, 80, 28, null, 14);
                    UIKit.Btn(row, "Бросить", () => { g.Value.Remove(it); Game.Manager.Net.Session.DropItemRpc(it.ToJsonString(), lp.transform.position + lp.transform.forward); AfterChange(); }, 80, 28, UIKit.Bad * 0.6f, 14);
                }
            }
            // контейнер
            UIKit.Clear(lootContent);
            if (container == null) { lootTitle.text = "Рядом нет контейнера"; UIKit.Label(lootContent, "Подойдите к ящику, телу или шкафу и нажмите F.\n\nУправление: WASD — движение, Shift — бег, Ctrl — присесть, Q/E — наклон, ЛКМ — огонь, ПКМ — прицел, R — перезарядка, B — режим огня, T — тип патронов, G — граната, 5 — аптечка, 6 — еда, N — ПНВ, L — фонарь, 1-4 — оружие, Tab — инвентарь, Enter — чат.", 15, TextAnchor.UpperLeft, UIKit.Muted); return; }
            lootTitle.text = container.Title;
            if (containerItems.Count == 0) UIKit.Label(lootContent, "Пусто", 16, TextAnchor.MiddleLeft, UIKit.Muted);
            for (int i = 0; i < containerItems.Count; i++)
            {
                int idx = i; var it = containerItems[i];
                var row = UIKit.Row(lootContent, 4, 32); UIKit.Cell(row, UIKit.ItemLine(it), 0, 15);
                UIKit.Cell(row, $"{it.Value} ₽", 80, 13, TextAnchor.MiddleRight, UIKit.Muted);
                UIKit.Btn(row, "Взять", () => Game.Manager.Net.Session.TakeFromContainerRpc(containerId, idx), 80, 28, UIKit.Good * 0.6f, 14);
            }
            if (containerItems.Count > 1) UIKit.Btn(lootContent, "Взять всё", () => { for (int i = containerItems.Count - 1; i >= 0; i--) Game.Manager.Net.Session.TakeFromContainerRpc(containerId, i); }, 0, 34);
        }

        void Put(ItemInstance it) { Game.Manager.Net.Session.PutInContainerRpc(containerId, it.ToJsonString()); AfterChange(); }

        void Equip(ItemInstance it)
        {
            var lo = Game.Manager.RaidLoadout; var info = it.Info; string slot = null;
            if (info.IsWeapon) slot = info.weapon.cls == "pistol" ? "holster" : info.weapon.cls == "melee" ? "melee" : lo.eq.Get("primary") == null ? "primary" : "secondary";
            else if (info.type == "tool") slot = "headgear"; else slot = info.type;
            if (!Equipment.SlotAccepts(slot, it)) return;
            var old = lo.eq.Get(slot);
            lo.RemoveItem(it);
            lo.eq.Set(slot, it);
            if (old != null && !lo.TryAdd(old)) { lo.eq.Set(slot, old); lo.TryAdd(it); Game.Notify("Нет места для замены"); }
            AfterChange();
        }

        void Attach(ItemInstance mod)
        {
            var lp = Game.Manager.Net.LocalPlayer; var w = lp.Weapons.Current; var m = mod.Info.mod;
            if (w == null || !m.Fits(w.Info.weapon)) { Game.Notify("Не подходит к текущему оружию"); return; }
            var lo = Game.Manager.RaidLoadout;
            if (w.mods.TryGetValue(m.slot, out var oldId)) { var old = new ItemInstance(oldId); if (!lo.TryAdd(old)) { Game.Notify("Нет места для снятого мода"); return; } }
            lo.RemoveItem(mod); w.mods[m.slot] = mod.id;
            Game.Profile.AddSkillXp("intellect", 3);
            AfterChange();
        }

        void AfterChange()
        {
            var lp = Game.Manager.Net.LocalPlayer;
            lp.UpdateGearVars(); lp.Weapons.Rebuild(); if (lp.Weapons.Current == null) foreach (var s in new[] { "primary", "secondary", "holster", "melee" }) if (Game.Manager.RaidLoadout.eq.Get(s) != null) { lp.Weapons.Switch(s); break; }
            Stats.Rebuild(Game.Profile);
            Refresh(); Game.Manager.UI.OnInventoryChanged();
        }
    }
}
