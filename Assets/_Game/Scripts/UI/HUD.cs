using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>Боевой интерфейс в ярком стиле: карточки со скруглениями, крупные цифры, минимум текста.</summary>
    public class HUD
    {
        Canvas canvas; Text ammo, weapon, mode, timer, enemies, hint, compass, questText, statusText, deathText, usingText, weightText;
        Image stamina, damageVignette, hitMarker, flashImg, nvImg, usingBar, hpTotal;
        readonly Image[] partBars = new Image[7]; readonly Text[] partLabels = new Text[7];
        RectTransform chatCol, killCol, deathPanel, usingRoot, hintChip; InputField chatField;
        readonly List<(GameObject go, float until)> feed = new List<(GameObject, float)>();
        float hitUntil, damageAlpha, senseUntil, flashAlpha; Text senseText;
        public bool ChatOpen;

        public void Show() { if (canvas == null) Build(); canvas.gameObject.SetActive(true); deathPanel.gameObject.SetActive(false); RefreshHealth(); RefreshAmmo(); RefreshQuests(); }
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); }

        void Build()
        {
            canvas = UIKit.NewCanvas("HUD", 10); var t = canvas.transform;
            damageVignette = UIKit.Img(t, new Color(0.9f, 0, 0.1f, 0)); UIKit.Stretch(damageVignette.rectTransform);
            nvImg = UIKit.Img(t, new Color(0.2f, 1f, 0.3f, 0)); UIKit.Stretch(nvImg.rectTransform);
            flashImg = UIKit.Img(t, new Color(1, 1, 1, 0)); UIKit.Stretch(flashImg.rectTransform);
            // прицел: точка + 4 штриха
            var dot = UIKit.Img(t, new Color(1, 1, 1, 0.9f)); dot.sprite = UIKit.Circle(); UIKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5, 5));
            foreach (var (p, s) in new[] { (new Vector2(0, 14), new Vector2(3, 10)), (new Vector2(0, -14), new Vector2(3, 10)), (new Vector2(14, 0), new Vector2(10, 3)), (new Vector2(-14, 0), new Vector2(10, 3)) })
            { var l = UIKit.Img(t, new Color(1, 1, 1, 0.8f)); UIKit.Place(l.rectTransform, new Vector2(0.5f, 0.5f), p, s); l.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f); }
            hitMarker = UIKit.Img(t, new Color(1, 1, 1, 0)); UIKit.Place(hitMarker.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22)); hitMarker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            // здоровье (лево-низ)
            var hp = UIKit.Card(t, UIKit.Panel, 22, "health"); UIKit.Place(hp.rectTransform, new Vector2(0, 0), new Vector2(20, 20), new Vector2(360, 250));
            var col = UIKit.Column(hp.transform, 4, 14); UIKit.Stretch(col);
            var hpRow = UIKit.Row(col, 8, 30); UIKit.Icon(hpRow, "heart", UIKit.Red, 26).gameObject.AddComponent<LayoutElement>().minWidth = 26; hpTotal = UIKit.Bar(hpRow, UIKit.Green, 18);
            for (int i = 0; i < 7; i++) { var row = UIKit.Row(col, 6, 18); partLabels[i] = UIKit.Cell(row, BodyHealth.PartNames[i], 86, 13, TextAnchor.MiddleLeft, UIKit.Muted); partBars[i] = UIKit.Bar(row, UIKit.Green, 10); }
            var srow = UIKit.Row(col, 6, 20); UIKit.Cell(srow, "Силы", 86, 13, TextAnchor.MiddleLeft, UIKit.Muted); stamina = UIKit.Bar(srow, UIKit.Cyan, 12);
            statusText = UIKit.Label(col, "", 13, TextAnchor.MiddleLeft, UIKit.Red, true, 18);
            weightText = UIKit.Label(col, "", 12, TextAnchor.MiddleLeft, UIKit.Muted, false, 16);
            // оружие (право-низ)
            var wp = UIKit.Card(t, UIKit.Panel, 22, "weapon"); UIKit.Place(wp.rectTransform, new Vector2(1, 0), new Vector2(-20, 20), new Vector2(400, 130));
            weapon = UIKit.Label(wp.transform, "", 20, TextAnchor.UpperRight, Color.white, true, 0, true); UIKit.Stretch(weapon.rectTransform, 14, 12, 14, 80);
            ammo = UIKit.Label(wp.transform, "", 52, TextAnchor.LowerRight, UIKit.Yellow, true, 0, true); UIKit.Stretch(ammo.rectTransform, 14, 40, 14, 6);
            mode = UIKit.Label(wp.transform, "", 15, TextAnchor.LowerLeft, UIKit.Cyan, true, 0, true); UIKit.Stretch(mode.rectTransform, 14, 80, 150, 12);
            // верх-центр: таймер и враги
            var tc = UIKit.Card(t, UIKit.Panel, 22, "timer"); UIKit.Place(tc.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(360, 56));
            timer = UIKit.Label(tc.transform, "", 30, TextAnchor.MiddleCenter, UIKit.Yellow, true, 0, true); UIKit.Stretch(timer.rectTransform, 16, 4, 150, 4);
            enemies = UIKit.Label(tc.transform, "", 17, TextAnchor.MiddleRight, UIKit.Red, true, 0, true); UIKit.Stretch(enemies.rectTransform, 200, 4, 16, 4);
            compass = UIKit.Label(t, "", 17, TextAnchor.MiddleCenter, Color.white, true, 0, true); UIKit.Place(compass.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1000, 28));
            // квесты (лево-верх)
            questText = UIKit.Label(t, "", 15, TextAnchor.UpperLeft, UIKit.TextC, false, 0, true); UIKit.Place(questText.rectTransform, new Vector2(0, 1), new Vector2(20, -20), new Vector2(480, 420));
            // килл-фид и чат
            killCol = UIKit.Column(t, 2, 0); killCol.anchorMin = new Vector2(1, 1); killCol.anchorMax = new Vector2(1, 1); killCol.pivot = new Vector2(1, 1); killCol.anchoredPosition = new Vector2(-20, -220); killCol.sizeDelta = new Vector2(460, 300); killCol.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperRight;
            chatCol = UIKit.Column(t, 2, 0); chatCol.anchorMin = new Vector2(0, 0); chatCol.anchorMax = new Vector2(0, 0); chatCol.pivot = new Vector2(0, 0); chatCol.anchoredPosition = new Vector2(20, 330); chatCol.sizeDelta = new Vector2(560, 220); chatCol.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            chatField = UIKit.Field(t, "Сообщение… (Enter)", "", 560, 40); UIKit.Place(chatField.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(20, 284), new Vector2(560, 40)); chatField.gameObject.SetActive(false);
            // подсказка и прогресс
            hintChip = UIKit.Chip(t, "", new Color(0, 0, 0.1f, 0.6f), null, 20, 44); UIKit.Place(hintChip, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(600, 44)); hint = hintChip.Find("chipText").GetComponent<Text>();
            usingRoot = UIKit.Rect(t, "using"); UIKit.Place(usingRoot, new Vector2(0.5f, 0), new Vector2(0, 222), new Vector2(420, 40));
            usingText = UIKit.Label(usingRoot, "", 18, TextAnchor.UpperCenter, UIKit.Yellow, true, 0, true); UIKit.Stretch(usingText.rectTransform, 0, 0, 0, 16);
            var ub = UIKit.Rect(usingRoot, "bar"); ub.anchorMin = new Vector2(0, 0); ub.anchorMax = new Vector2(1, 0); ub.offsetMin = new Vector2(0, 0); ub.offsetMax = new Vector2(0, 12); usingBar = UIKit.Bar(ub, UIKit.Yellow, 12); UIKit.Stretch(usingBar.transform.parent.GetComponent<RectTransform>());
            senseText = UIKit.Label(t, "", 28, TextAnchor.MiddleCenter, UIKit.Red, true, 0, true); UIKit.Place(senseText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 130), new Vector2(600, 40));
            // смерть
            deathPanel = UIKit.Fill(t, new Color(0.3f, 0, 0.05f, 0.85f), "death");
            var dc = UIKit.Card(deathPanel, UIKit.PanelSolid, 28); UIKit.Place(dc.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 360));
            deathText = UIKit.Label(dc.transform, "", 28, TextAnchor.MiddleCenter, Color.white, true, 0, true); UIKit.Stretch(deathText.rectTransform, 20, 20, 20, 110);
            var db = UIKit.BigButton(dc.transform, "В лобби", UIKit.Yellow, () => Game.Manager.FinishRaid(false, "Погиб"), 360, 64, 28, UIKit.TextDark); UIKit.Place(db.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(360, 64));
            deathPanel.gameObject.SetActive(false);
        }

        public void RefreshHealth()
        {
            var lp = Game.Manager.Net.LocalPlayer; if (lp == null || partBars[0] == null) return;
            var h = lp.Health.Value; float mul = lp.MaxHpMul.Value;
            float tot = Mathf.Clamp01(h.Total / h.MaxTotal(mul)); hpTotal.fillAmount = tot; hpTotal.color = tot < 0.3f ? UIKit.Red : tot < 0.6f ? UIKit.Orange : UIKit.Green;
            for (int i = 0; i < 7; i++)
            {
                float f = Mathf.Clamp01(h.Get(i) / BodyHealth.MaxOf(i, mul)); partBars[i].fillAmount = f;
                partBars[i].color = f <= 0 ? new Color(0.3f, 0.3f, 0.3f) : f < 0.35f ? UIKit.Red : f < 0.7f ? UIKit.Orange : UIKit.Green;
                string fl = ""; if (h.HeavyBleeding(i)) fl += " <color=#ff3b5c>▼▼</color>"; else if (h.Bleeding(i)) fl += " <color=#ff8a8a>▼</color>"; if (h.Fractured(i)) fl += " <color=#ffd400>✕</color>";
                partLabels[i].text = BodyHealth.PartNames[i] + fl;
            }
            var st = new List<string>();
            if (h.energy < 20) st.Add("ГОЛОД"); if (h.hydration < 20) st.Add("ЖАЖДА"); if (lp.InPain && !lp.PainFree) st.Add("БОЛЬ"); if (h.AnyBleed) st.Add("КРОВОТЕЧЕНИЕ"); if (h.AnyFracture) st.Add("ПЕРЕЛОМ");
            statusText.text = string.Join(" · ", st);
        }

        public void RefreshAmmo()
        {
            var lp = Game.Manager.Net.LocalPlayer; if (lp == null || lp.Weapons == null || ammo == null) return;
            var w = lp.Weapons.Current;
            if (w == null) { weapon.text = "— пусто —"; ammo.text = ""; mode.text = ""; return; }
            var def = w.Info.weapon;
            weapon.text = $"{def.name}  <size=13><color=#b9c3e6>{w.durability:0}%{(lp.Weapons.WC != null && lp.Weapons.WC.suppressed ? " · глушитель" : "")}</color></size>";
            ammo.text = def.IsMelee ? "" : $"{w.ammoInMag}<size=24><color=#b9c3e6> / {lp.Weapons.AmmoReserve}</color></size>";
            mode.text = def.IsMelee ? "" : $"{lp.Weapons.ModeName} · {Database.NameOf(w.ammoId).Replace(def.caliber + " ", "")}";
            var lo = Game.Manager.RaidLoadout; weightText.text = $"Вес {lo.Weight:0.0} / {lp.Ctrl.CarryWeight:0} кг" + (lp.Ctrl.Overweight > 0 ? "  <color=#ff3b5c>ПЕРЕГРУЗ</color>" : "");
        }

        public void RefreshQuests()
        {
            if (questText == null) return;
            var sb = new System.Text.StringBuilder(); var map = World.Map?.id; int shown = 0;
            foreach (var q in QuestSystem.Active())
            {
                bool relevant = string.IsNullOrEmpty(q.map) || q.map == map || q.objectives.Any(o => string.IsNullOrEmpty(o.map) || o.map == "*" || o.map == map);
                if (!relevant || shown >= 4) continue; shown++;
                sb.AppendLine($"<color=#ffd400><b>{q.name}</b></color>");
                for (int i = 0; i < q.objectives.Count; i++) { var o = q.objectives[i]; bool done = QuestSystem.ObjectiveDone(q, i); sb.AppendLine($"  {(done ? "<color=#5cf25a>✓</color>" : "<color=#19d3ff>•</color>")} {o.Describe()}  <color=#b9c3e6>{QuestSystem.ObjectiveProgress(q, i)}/{o.Goal}</color>"); }
            }
            questText.text = sb.ToString();
        }

        public void SetHint(string text) { if (hint != null) { hint.text = text; hintChip.gameObject.SetActive(!string.IsNullOrEmpty(text)); } }
        public void Damaged(string part, float dmg) { damageAlpha = Mathf.Min(0.7f, damageAlpha + dmg / 60f); RefreshHealth(); }
        public void HitMarker(bool head) { hitUntil = Time.time + 0.12f; hitMarker.color = head ? UIKit.Red : Color.white; }
        public void Flash(float power) { flashAlpha = Mathf.Clamp01(power); }
        public void SetNV(bool on) { nvImg.color = new Color(0.2f, 1f, 0.3f, on ? 0.18f : 0); }
        public void SixthSense() { senseUntil = Time.time + 2f; }
        public void ShowDeath(string attacker, string weapon) { deathPanel.gameObject.SetActive(true); deathText.text = $"<size=48><color=#ff3b5c>ВЫ ПОГИБЛИ</color></size>\n{attacker}{(string.IsNullOrEmpty(weapon) ? "" : " · " + weapon)}\n<size=16><color=#b9c3e6>Снаряжение потеряно, кроме защищённого контейнера.</color></size>"; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        public void AddChat(string who, string msg) { var t = UIKit.Label(chatCol, $"<color=#ffd400>{who}:</color> {msg}", 17, TextAnchor.MiddleLeft, Color.white, true, 0, true); feed.Add((t.gameObject, Time.time + 12f)); }
        public void AddKill(string text) { var t = UIKit.Label(killCol, text, 16, TextAnchor.MiddleRight, Color.white, true, 0, true); feed.Add((t.gameObject, Time.time + 8f)); }
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
            damageAlpha = Mathf.MoveTowards(damageAlpha, 0, Time.deltaTime * 0.6f); damageVignette.color = new Color(0.9f, 0, 0.1f, damageAlpha);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0, Time.deltaTime * 0.35f); flashImg.color = new Color(1, 1, 1, flashAlpha);
            hitMarker.color = new Color(hitMarker.color.r, hitMarker.color.g, hitMarker.color.b, Time.time < hitUntil ? 1 : 0);
            senseText.text = Time.time < senseUntil ? "ВАС ЗАМЕТИЛИ!" : "";
            if (lp == null) return;
            stamina.fillAmount = lp.Ctrl.Stamina / Mathf.Max(1, lp.Ctrl.MaxStamina);
            var s = Game.Manager.Net.Session;
            if (s != null) { double left = s.TimeLeft; timer.text = $"{(int)(left / 60):00}:{(int)(left % 60):00}"; enemies.text = $"врагов {s.EnemiesAlive.Value}{(s.BossAlive.Value ? " · БОСС" : "")}"; }
            float yaw = lp.transform.eulerAngles.y; var sb = new System.Text.StringBuilder();
            string[] dirs = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
            sb.Append($"<color=#ffd400>{dirs[Mathf.RoundToInt(yaw / 45f) % 8]} {yaw:000}°</color>   ");
            bool showAll = Stats.Has("showExtracts");
            foreach (var e in World.Extracts)
            {
                if (!showAll && !string.IsNullOrEmpty(e.Def.cond) && !e.LocalInside) continue;
                var d = e.transform.position - lp.transform.position; float ang = Mathf.DeltaAngle(yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                if (Mathf.Abs(ang) < 60) sb.Append($"<color=#5cf25a>▲ {e.Def.name} {d.magnitude:0}м</color>   ");
            }
            if (Stats.Has("memory_m30") || showAll)
                foreach (var p in World.Pois) if (QuestSystem.PoisNeeded(World.Map.id).Contains(p.Name)) { var d = p.transform.position - lp.transform.position; float ang = Mathf.DeltaAngle(yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg); if (Mathf.Abs(ang) < 60) sb.Append($"<color=#ffd400>◆ {p.Name} {d.magnitude:0}м</color>   "); }
            compass.text = sb.ToString();
            bool usingNow = lp.Weapons.Using; float sp = lp.Interact.SearchProgress;
            usingText.text = usingNow ? lp.Weapons.UsingText : lp.Weapons.Reloading ? "Перезарядка…" : sp > 0 ? "Осмотр…" : "";
            usingRoot.gameObject.SetActive(usingNow || sp > 0 || lp.Weapons.Reloading);
            if (sp > 0) usingBar.fillAmount = sp; else if (usingNow) usingBar.fillAmount = 1;
            if (Time.frameCount % 30 == 0) { RefreshQuests(); RefreshAmmo(); }
        }
    }
}
