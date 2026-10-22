using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>Минимальный читаемый HUD: здоровье, силы, патроны, таймер, компас с ближайшим выходом, одна строка задания.</summary>
    public class HUD
    {
        Canvas canvas; Text hpText, ammo, weapon, mode, timer, enemies, hint, compass, questText, statusText, deathText, usingText, weightText;
        Image stamina, damageVignette, hitMarker, flashImg, nvImg, usingBar, hpFill; RectTransform partsRow, usingRoot, hintChip, chatCol, killCol, deathPanel;
        readonly Image[] partBars = new Image[7];
        InputField chatField;
        readonly List<(GameObject go, float until)> feed = new List<(GameObject, float)>();
        float hitUntil, damageAlpha, senseUntil, flashAlpha; Text senseText;
        public bool ChatOpen;

        public void Show() { if (canvas == null) Build(); canvas.gameObject.SetActive(true); deathPanel.gameObject.SetActive(false); RefreshHealth(); RefreshAmmo(); RefreshQuests(); }
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); }

        static Color Panel => new Color(0.02f, 0.03f, 0.08f, 0.62f);

        void Build()
        {
            canvas = UIKit.NewCanvas("HUD", 10); var t = canvas.transform;
            damageVignette = UIKit.Img(t, new Color(0.8f, 0, 0.05f, 0)); UIKit.Stretch(damageVignette.rectTransform);
            nvImg = UIKit.Img(t, new Color(0.2f, 1f, 0.3f, 0)); UIKit.Stretch(nvImg.rectTransform);
            flashImg = UIKit.Img(t, new Color(1, 1, 1, 0)); UIKit.Stretch(flashImg.rectTransform);
            // прицел
            var dot = UIKit.Img(t, new Color(1, 1, 1, 0.95f)); dot.sprite = UIKit.Circle(); UIKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 4));
            foreach (var (p, s) in new[] { (new Vector2(0, 12), new Vector2(2, 8)), (new Vector2(0, -12), new Vector2(2, 8)), (new Vector2(12, 0), new Vector2(8, 2)), (new Vector2(-12, 0), new Vector2(8, 2)) })
            { var l = UIKit.Img(t, new Color(1, 1, 1, 0.9f)); UIKit.Place(l.rectTransform, new Vector2(0.5f, 0.5f), p, s); l.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f); }
            hitMarker = UIKit.Img(t, new Color(1, 1, 1, 0)); UIKit.Place(hitMarker.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20)); hitMarker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            // здоровье (лево-низ): одна большая полоса + силы + статусы + компактные части тела при ранениях
            var hp = UIKit.Card(t, Panel, 16, "health"); UIKit.Place(hp.rectTransform, new Vector2(0, 0), new Vector2(24, 24), new Vector2(380, 120));
            var col = UIKit.Column(hp.transform, 6, 12); UIKit.Stretch(col);
            var hpRow = UIKit.Row(col, 10, 30); UIKit.Icon(hpRow, "heart", UIKit.Red, 24).gameObject.AddComponent<LayoutElement>().minWidth = 24; hpFill = UIKit.Bar(hpRow, UIKit.Green, 24);
            hpText = UIKit.Label(hpFill.transform.parent, "440", 18, TextAnchor.MiddleCenter, Color.white, true, 0, true); UIKit.Stretch(hpText.rectTransform);
            var srow = UIKit.Row(col, 10, 14); UIKit.Icon(srow, "skills", UIKit.Cyan, 14).gameObject.AddComponent<LayoutElement>().minWidth = 24; stamina = UIKit.Bar(srow, UIKit.Cyan, 10);
            partsRow = UIKit.Row(col, 4, 16);
            string[] ab = { "Г", "Гр", "Ж", "ЛР", "ПР", "ЛН", "ПН" };
            for (int i = 0; i < 7; i++) { var c = UIKit.Column(partsRow, 1, 0); c.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1; partBars[i] = UIKit.Bar(c, UIKit.Green, 6); UIKit.Label(c, ab[i], 10, TextAnchor.MiddleCenter, UIKit.Muted, true, 10); }
            statusText = UIKit.Label(col, "", 14, TextAnchor.MiddleLeft, UIKit.Red, true, 18, true);
            weightText = UIKit.Label(t, "", 13, TextAnchor.LowerLeft, UIKit.Muted, true, 0, true); UIKit.Place(weightText.rectTransform, new Vector2(0, 0), new Vector2(28, 148), new Vector2(380, 18));
            // оружие (право-низ)
            var wp = UIKit.Card(t, Panel, 16, "weapon"); UIKit.Place(wp.rectTransform, new Vector2(1, 0), new Vector2(-24, 24), new Vector2(330, 110));
            ammo = UIKit.Label(wp.transform, "", 56, TextAnchor.LowerRight, Color.white, true, 0, true); UIKit.Stretch(ammo.rectTransform, 14, 30, 16, 8);
            weapon = UIKit.Label(wp.transform, "", 17, TextAnchor.UpperRight, UIKit.Yellow, true, 0, true); UIKit.Stretch(weapon.rectTransform, 14, 10, 16, 80);
            mode = UIKit.Label(wp.transform, "", 13, TextAnchor.LowerLeft, UIKit.Muted, true, 0, true); UIKit.Stretch(mode.rectTransform, 14, 80, 120, 12);
            // верх-центр: таймер + враги + компас
            var tc = UIKit.Card(t, Panel, 16, "timer"); UIKit.Place(tc.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(300, 48));
            timer = UIKit.Label(tc.transform, "", 28, TextAnchor.MiddleCenter, Color.white, true, 0, true); UIKit.Stretch(timer.rectTransform, 12, 2, 120, 2);
            enemies = UIKit.Label(tc.transform, "", 15, TextAnchor.MiddleRight, UIKit.Red, true, 0, true); UIKit.Stretch(enemies.rectTransform, 170, 2, 14, 2);
            compass = UIKit.Label(t, "", 16, TextAnchor.MiddleCenter, Color.white, true, 0, true); UIKit.Place(compass.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -72), new Vector2(1000, 24));
            questText = UIKit.Label(t, "", 15, TextAnchor.MiddleCenter, UIKit.Yellow, true, 0, true); UIKit.Place(questText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -98), new Vector2(1000, 22));
            // килл-фид и чат
            killCol = UIKit.Column(t, 2, 0); killCol.anchorMin = new Vector2(1, 1); killCol.anchorMax = new Vector2(1, 1); killCol.pivot = new Vector2(1, 1); killCol.anchoredPosition = new Vector2(-24, -140); killCol.sizeDelta = new Vector2(420, 240); killCol.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperRight;
            chatCol = UIKit.Column(t, 2, 0); chatCol.anchorMin = new Vector2(0, 0); chatCol.anchorMax = new Vector2(0, 0); chatCol.pivot = new Vector2(0, 0); chatCol.anchoredPosition = new Vector2(24, 220); chatCol.sizeDelta = new Vector2(560, 200); chatCol.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            chatField = UIKit.Field(t, "Сообщение… (Enter)", "", 560, 36); UIKit.Place(chatField.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(24, 178), new Vector2(560, 36)); chatField.gameObject.SetActive(false);
            // подсказка и прогресс
            hintChip = UIKit.Chip(t, "", new Color(0, 0, 0.05f, 0.7f), null, 19, 42); UIKit.Place(hintChip, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(600, 42)); hint = hintChip.Find("chipText").GetComponent<Text>();
            usingRoot = UIKit.Rect(t, "using"); UIKit.Place(usingRoot, new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(360, 36));
            usingText = UIKit.Label(usingRoot, "", 16, TextAnchor.UpperCenter, UIKit.Yellow, true, 0, true); UIKit.Stretch(usingText.rectTransform, 0, 0, 0, 14);
            var ub = UIKit.Rect(usingRoot, "bar"); ub.anchorMin = new Vector2(0, 0); ub.anchorMax = new Vector2(1, 0); ub.offsetMin = Vector2.zero; ub.offsetMax = new Vector2(0, 8); usingBar = UIKit.Bar(ub, UIKit.Yellow, 8); UIKit.Stretch(usingBar.transform.parent.GetComponent<RectTransform>());
            senseText = UIKit.Label(t, "", 24, TextAnchor.MiddleCenter, UIKit.Red, true, 0, true); UIKit.Place(senseText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(600, 36));
            // смерть
            deathPanel = UIKit.Fill(t, new Color(0.2f, 0, 0.03f, 0.85f), "death");
            var dc = UIKit.Card(deathPanel, UIKit.PanelSolid, 24); UIKit.Place(dc.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 320));
            deathText = UIKit.Label(dc.transform, "", 24, TextAnchor.MiddleCenter, Color.white, true, 0, true); UIKit.Stretch(deathText.rectTransform, 20, 20, 20, 100);
            var db = UIKit.BigButton(dc.transform, "В лобби", UIKit.Yellow, () => Game.Manager.FinishRaid(false, "Погиб"), 320, 60, 26, UIKit.TextDark); UIKit.Place(db.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(320, 60));
            deathPanel.gameObject.SetActive(false);
        }

        public void RefreshHealth()
        {
            var lp = Game.Manager.Net.LocalPlayer; if (lp == null || hpFill == null) return;
            var h = lp.Health.Value; float mul = lp.MaxHpMul.Value;
            float tot = Mathf.Clamp01(h.Total / h.MaxTotal(mul)); hpFill.fillAmount = tot; hpFill.color = tot < 0.3f ? UIKit.Red : tot < 0.6f ? UIKit.Orange : UIKit.Green;
            hpText.text = $"{h.Total:0} / {h.MaxTotal(mul):0}";
            bool anyHurt = false;
            for (int i = 0; i < 7; i++)
            {
                float f = Mathf.Clamp01(h.Get(i) / BodyHealth.MaxOf(i, mul)); partBars[i].fillAmount = f;
                partBars[i].color = f <= 0 ? new Color(0.35f, 0.35f, 0.35f) : f < 0.35f ? UIKit.Red : f < 0.7f ? UIKit.Orange : UIKit.Green;
                if (f < 0.7f || h.Bleeding(i) || h.HeavyBleeding(i) || h.Fractured(i)) anyHurt = true;
            }
            partsRow.gameObject.SetActive(anyHurt);
            var st = new List<string>();
            if (h.AnyBleed) st.Add("<color=#ff3b5c>▼ КРОВЬ</color>"); if (h.AnyFracture) st.Add("<color=#ffd400>✕ ПЕРЕЛОМ</color>"); if (lp.InPain && !lp.PainFree) st.Add("<color=#ff8a2a>БОЛЬ</color>");
            if (h.energy < 20) st.Add("<color=#ffd400>ГОЛОД</color>"); if (h.hydration < 20) st.Add("<color=#19d3ff>ЖАЖДА</color>");
            statusText.text = string.Join("   ", st); statusText.gameObject.SetActive(st.Count > 0);
        }

        public void RefreshAmmo()
        {
            var lp = Game.Manager.Net.LocalPlayer; if (lp == null || lp.Weapons == null || ammo == null) return;
            var w = lp.Weapons.Current;
            if (w == null) { weapon.text = "нет оружия"; ammo.text = ""; mode.text = ""; return; }
            var def = w.Info.weapon;
            weapon.text = def.name;
            ammo.text = def.IsMelee ? "" : $"{w.ammoInMag}<size=26><color=#b9c3e6> | {lp.Weapons.AmmoReserve}</color></size>";
            mode.text = def.IsMelee ? "" : $"{lp.Weapons.ModeName} · {Database.NameOf(w.ammoId).Replace(def.caliber + " ", "")}{(lp.Weapons.WC != null && lp.Weapons.WC.suppressed ? " · глушитель" : "")}";
            var lo = Game.Manager.RaidLoadout; weightText.text = lp.Ctrl.Overweight > 0 ? $"<color=#ff3b5c>ПЕРЕГРУЗ {lo.Weight:0.0}/{lp.Ctrl.CarryWeight:0} кг</color>" : "";
        }

        public void RefreshQuests()
        {
            if (questText == null) return;
            var map = World.Map?.id; questText.text = "";
            foreach (var q in QuestSystem.Active())
            {
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i]; if (QuestSystem.ObjectiveDone(q, i)) continue;
                    if (!string.IsNullOrEmpty(o.map) && o.map != "*" && o.map != map) continue;
                    questText.text = $"{q.name}: {o.Describe()}  <color=#b9c3e6>{QuestSystem.ObjectiveProgress(q, i)}/{o.Goal}</color>"; return;
                }
            }
        }

        public void SetHint(string text) { if (hint != null) { hint.text = text; hintChip.gameObject.SetActive(!string.IsNullOrEmpty(text)); } }
        public void Damaged(string part, float dmg) { damageAlpha = Mathf.Min(0.6f, damageAlpha + dmg / 70f); RefreshHealth(); }
        public void HitMarker(bool head) { hitUntil = Time.time + 0.12f; hitMarker.color = head ? UIKit.Red : Color.white; }
        public void Flash(float power) { flashAlpha = Mathf.Clamp01(power); }
        public void SetNV(bool on) { nvImg.color = new Color(0.2f, 1f, 0.3f, on ? 0.18f : 0); }
        public void SixthSense() { senseUntil = Time.time + 2f; }
        public void ShowDeath(string attacker, string weapon) { deathPanel.gameObject.SetActive(true); deathText.text = $"<size=44><color=#ff3b5c>ВЫ ПОГИБЛИ</color></size>\n{attacker}{(string.IsNullOrEmpty(weapon) ? "" : " · " + weapon)}\n<size=15><color=#b9c3e6>Снаряжение потеряно, кроме защищённого контейнера.</color></size>"; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        public void AddChat(string who, string msg) { var t = UIKit.Label(chatCol, $"<color=#ffd400>{who}:</color> {msg}", 16, TextAnchor.MiddleLeft, Color.white, true, 0, true); feed.Add((t.gameObject, Time.time + 12f)); }
        public void AddKill(string text) { var t = UIKit.Label(killCol, text, 15, TextAnchor.MiddleRight, Color.white, true, 0, true); feed.Add((t.gameObject, Time.time + 8f)); }
        public void OpenChat() { ChatOpen = true; chatField.gameObject.SetActive(true); chatField.text = ""; chatField.ActivateInputField(); chatField.Select(); }

        public void Tick()
        {
            var lp = Game.Manager.Net.LocalPlayer;
            if (ChatOpen)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { string m = chatField.text.Trim(); if (m.Length > 0) Game.Manager.Net.Session?.ChatRpc(m); ChatOpen = false; chatField.gameObject.SetActive(false); }
                if (Input.GetKeyDown(KeyCode.Escape)) { ChatOpen = false; chatField.gameObject.SetActive(false); }
            }
            for (int i = feed.Count - 1; i >= 0; i--) if (Time.time > feed[i].until) { if (feed[i].go != null) Object.Destroy(feed[i].go); feed.RemoveAt(i); }
            damageAlpha = Mathf.MoveTowards(damageAlpha, 0, Time.deltaTime * 0.6f); damageVignette.color = new Color(0.8f, 0, 0.05f, damageAlpha);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0, Time.deltaTime * 0.35f); flashImg.color = new Color(1, 1, 1, flashAlpha);
            hitMarker.color = new Color(hitMarker.color.r, hitMarker.color.g, hitMarker.color.b, Time.time < hitUntil ? 1 : 0);
            senseText.text = Time.time < senseUntil ? "ВАС ЗАМЕТИЛИ" : "";
            if (lp == null) return;
            stamina.fillAmount = lp.Ctrl.Stamina / Mathf.Max(1, lp.Ctrl.MaxStamina);
            var s = Game.Manager.Net.Session;
            if (s != null) { double left = s.TimeLeft; timer.text = $"{(int)(left / 60):00}:{(int)(left % 60):00}"; enemies.text = $"{s.EnemiesAlive.Value}{(s.BossAlive.Value ? " · БОСС" : "")}"; }
            // компас: направление и ближайший доступный выход
            float yaw = lp.transform.eulerAngles.y;
            string[] dirs = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
            ExtractZone best = null; float bestD = float.MaxValue; bool showAll = Stats.Has("showExtracts");
            foreach (var e in World.Extracts) { if (!showAll && !string.IsNullOrEmpty(e.Def.cond)) continue; float d = Vector3.Distance(e.transform.position, lp.transform.position); if (d < bestD) { bestD = d; best = e; } }
            string ex = "";
            if (best != null) { var d = best.transform.position - lp.transform.position; float ang = Mathf.DeltaAngle(yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg); string arrow = Mathf.Abs(ang) < 20 ? "▲" : ang > 0 ? "▶" : "◀"; ex = $"   <color=#5cf25a>{arrow} {best.Def.name} {bestD:0} м</color>"; }
            compass.text = $"{dirs[Mathf.RoundToInt(yaw / 45f) % 8]} {yaw:000}°{ex}";
            bool usingNow = lp.Weapons.Using; float sp = lp.Interact.SearchProgress;
            usingText.text = usingNow ? lp.Weapons.UsingText : lp.Weapons.Reloading ? "Перезарядка…" : sp > 0 ? "Осмотр…" : "";
            usingRoot.gameObject.SetActive(usingNow || sp > 0 || lp.Weapons.Reloading);
            if (sp > 0) usingBar.fillAmount = sp; else if (usingNow) usingBar.fillAmount = 1;
            if (Time.frameCount % 30 == 0) { RefreshQuests(); RefreshAmmo(); }
        }
    }
}
