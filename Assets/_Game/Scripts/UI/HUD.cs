using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>Боевой интерфейс: здоровье по частям, выносливость, патроны, компас, таймер, квесты, чат, килл-фид, подсказки.</summary>
    public class HUD
    {
        Canvas canvas; Text ammo, weapon, timer, hint, compass, questText, statusText, deathText, usingText, weightText;
        Image stamina, damageVignette, hitMarker, flashImg, nvImg, usingBar, searchBar;
        readonly Image[] partBars = new Image[7]; readonly Text[] partLabels = new Text[7];
        RectTransform chatCol, killCol, deathPanel; InputField chatField;
        readonly List<(GameObject go, float until)> feed = new List<(GameObject, float)>();
        float hitUntil, damageAlpha, senseUntil, flashAlpha;
        public bool ChatOpen;
        Text senseText;

        public void Show()
        {
            if (canvas == null) Build();
            canvas.gameObject.SetActive(true); deathPanel.gameObject.SetActive(false);
            RefreshHealth(); RefreshAmmo(); RefreshQuests();
        }
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); }

        void Build()
        {
            canvas = UIKit.NewCanvas("HUD", 10);
            var t = canvas.transform;
            // виньетка урона, вспышка, ПНВ
            damageVignette = UIKit.Img(t, new Color(0.8f, 0, 0, 0)); UIKit.Stretch(damageVignette.rectTransform);
            nvImg = UIKit.Img(t, new Color(0.2f, 1f, 0.3f, 0)); UIKit.Stretch(nvImg.rectTransform);
            flashImg = UIKit.Img(t, new Color(1, 1, 1, 0)); UIKit.Stretch(flashImg.rectTransform);
            // прицел
            var cross = UIKit.Img(t, new Color(1, 1, 1, 0.8f)); UIKit.Place(cross.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 4));
            hitMarker = UIKit.Img(t, new Color(1, 1, 1, 0)); UIKit.Place(hitMarker.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18)); hitMarker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            // здоровье (лево-низ)
            var hp = UIKit.Box(t, Vector2.zero, Vector2.zero, new Vector2(16, 16), new Vector2(330, 230), new Color(0, 0, 0, 0.45f), "health");
            var col = UIKit.Column(hp, 3, 8); UIKit.Stretch(col);
            for (int i = 0; i < 7; i++)
            {
                var row = UIKit.Row(col, 6, 20); partLabels[i] = UIKit.Cell(row, BodyHealth.PartNames[i], 90, 15); partBars[i] = UIKit.Bar(row, UIKit.Good, 12);
            }
            var srow = UIKit.Row(col, 6, 20); UIKit.Cell(srow, "Силы", 90, 15); stamina = UIKit.Bar(srow, new Color(0.3f, 0.6f, 0.95f), 12);
            statusText = UIKit.Label(col, "", 14, TextAnchor.UpperLeft, UIKit.Bad, false, 22);
            weightText = UIKit.Label(col, "", 14, TextAnchor.UpperLeft, UIKit.Muted, false, 18);
            // оружие (право-низ)
            var wp = UIKit.Box(t, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-360, 16), new Vector2(-16, 110), new Color(0, 0, 0, 0.45f), "weapon");
            weapon = UIKit.Label(wp, "", 20, TextAnchor.UpperRight, UIKit.TextC, true); UIKit.Stretch(weapon.rectTransform, 10, 8, 10, 50);
            ammo = UIKit.Label(wp, "", 30, TextAnchor.LowerRight, UIKit.Accent, true); UIKit.Stretch(ammo.rectTransform, 10, 40, 10, 6);
            // компас и таймер (верх-центр)
            compass = UIKit.Label(t, "", 18, TextAnchor.UpperCenter, UIKit.TextC, true); UIKit.Place(compass.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(900, 30));
            timer = UIKit.Label(t, "", 22, TextAnchor.UpperCenter, UIKit.Accent, true); UIKit.Place(timer.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(300, 30));
            // квесты (лево-верх)
            questText = UIKit.Label(t, "", 15, TextAnchor.UpperLeft, UIKit.TextC); UIKit.Place(questText.rectTransform, new Vector2(0, 1), new Vector2(16, -16), new Vector2(460, 400));
            questText.gameObject.AddComponent<Outline>().effectColor = Color.black;
            // килл-фид (право-верх под уведомлениями)
            killCol = UIKit.Column(t, 2, 0); killCol.anchorMin = new Vector2(1, 1); killCol.anchorMax = new Vector2(1, 1); killCol.pivot = new Vector2(1, 1); killCol.anchoredPosition = new Vector2(-16, -200); killCol.sizeDelta = new Vector2(420, 300);
            killCol.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperRight;
            // чат (лево-низ над здоровьем)
            chatCol = UIKit.Column(t, 2, 0); chatCol.anchorMin = new Vector2(0, 0); chatCol.anchorMax = new Vector2(0, 0); chatCol.pivot = new Vector2(0, 0); chatCol.anchoredPosition = new Vector2(16, 300); chatCol.sizeDelta = new Vector2(520, 220);
            chatCol.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            chatField = UIKit.Field(t, "Сообщение… (Enter)", "", 520, 34); UIKit.Place(chatField.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(16, 258), new Vector2(520, 34)); chatField.gameObject.SetActive(false);
            // подсказка и прогресс действия (центр-низ)
            hint = UIKit.Label(t, "", 20, TextAnchor.MiddleCenter, UIKit.TextC, true); UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(900, 40)); hint.gameObject.AddComponent<Outline>().effectColor = Color.black;
            usingText = UIKit.Label(t, "", 18, TextAnchor.MiddleCenter, UIKit.Accent, true); UIKit.Place(usingText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 215), new Vector2(600, 30));
            var ub = UIKit.Rect(t, "usingBar"); UIKit.Place(ub, new Vector2(0.5f, 0), new Vector2(0, 195), new Vector2(400, 10)); usingBar = UIKit.Bar(ub, UIKit.Accent, 10); UIKit.Stretch(usingBar.transform.parent.GetComponent<RectTransform>());
            // шестое чувство
            senseText = UIKit.Label(t, "", 26, TextAnchor.MiddleCenter, UIKit.Bad, true); UIKit.Place(senseText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(600, 40));
            // экран смерти
            deathPanel = UIKit.Panel(t, new Color(0.1f, 0, 0, 0.85f), "death");
            deathText = UIKit.Label(deathPanel, "", 30, TextAnchor.MiddleCenter, UIKit.TextC, true); UIKit.Stretch(deathText.rectTransform, 0, 0, 0, 120);
            var db = UIKit.Btn(deathPanel, "Вернуться в убежище", () => Game.Manager.FinishRaid(false, "Погиб"), 360, 48, UIKit.Accent); UIKit.Place(db.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(360, 48));
            deathPanel.gameObject.SetActive(false);
        }

        public void RefreshHealth()
        {
            var lp = Game.Manager.Net.LocalPlayer; if (lp == null || partBars[0] == null) return;
            var h = lp.Health.Value; float mul = lp.MaxHpMul.Value;
            for (int i = 0; i < 7; i++)
            {
                float f = Mathf.Clamp01(h.Get(i) / BodyHealth.MaxOf(i, mul)); partBars[i].fillAmount = f;
                partBars[i].color = f <= 0 ? new Color(0.3f, 0.3f, 0.3f) : f < 0.35f ? UIKit.Bad : f < 0.7f ? UIKit.Accent : UIKit.Good;
                string fl = ""; if (h.HeavyBleeding(i)) fl += " <color=#ff4040>▼▼</color>"; else if (h.Bleeding(i)) fl += " <color=#ff8080>▼</color>"; if (h.Fractured(i)) fl += " <color=#ffd080>✕</color>";
                partLabels[i].text = BodyHealth.PartNames[i] + fl;
            }
            var st = new List<string>();
            if (h.energy < 20) st.Add("Голод"); if (h.hydration < 20) st.Add("Жажда"); if (lp.InPain && !lp.PainFree) st.Add("Боль"); if (h.AnyBleed) st.Add("Кровотечение"); if (h.AnyFracture) st.Add("Перелом");
            statusText.text = string.Join(" · ", st);
        }

        public void RefreshAmmo()
        {
            var lp = Game.Manager.Net.LocalPlayer; if (lp == null || lp.Weapons == null || ammo == null) return;
            var w = lp.Weapons.Current;
            if (w == null) { weapon.text = "— пусто —"; ammo.text = ""; return; }
            var def = w.Info.weapon;
            weapon.text = $"{def.name}  <size=14><color=#aaaaaa>{(lp.Weapons.WC != null ? $"эрг {lp.Weapons.WC.ergo:0} · {w.durability:0}%" : "")}</color></size>";
            ammo.text = def.IsMelee ? "" : $"{w.ammoInMag} / {lp.Weapons.AmmoReserve}  <size=16>{lp.Weapons.ModeName}  {Database.NameOf(w.ammoId).Replace(def.caliber + " ", "")}</size>";
            var lo = Game.Manager.RaidLoadout; weightText.text = $"Вес {lo.Weight:0.0} / {lp.Ctrl.CarryWeight:0} кг" + (lp.Ctrl.Overweight > 0 ? "  <color=#ff8080>ПЕРЕГРУЗ</color>" : "");
        }

        public void RefreshQuests()
        {
            if (questText == null) return;
            var sb = new System.Text.StringBuilder();
            var map = World.Map?.id;
            foreach (var q in QuestSystem.Active())
            {
                bool relevant = string.IsNullOrEmpty(q.map) || q.map == map || q.objectives.Any(o => string.IsNullOrEmpty(o.map) || o.map == "*" || o.map == map);
                if (!relevant) continue;
                sb.AppendLine($"<b>{q.name}</b> <color=#aaaaaa>({Database.Trader(q.trader)?.name})</color>");
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i]; bool done = QuestSystem.ObjectiveDone(q, i);
                    sb.AppendLine($"  {(done ? "<color=#7ee07e>✓</color>" : "•")} {o.Describe()}  {QuestSystem.ObjectiveProgress(q, i)}/{o.Goal}");
                }
            }
            questText.text = sb.ToString();
        }

        public void SetHint(string text) { if (hint != null) hint.text = text; }
        public void Damaged(string part, float dmg) { damageAlpha = Mathf.Min(0.7f, damageAlpha + dmg / 60f); RefreshHealth(); }
        public void HitMarker(bool head) { hitUntil = Time.time + 0.12f; hitMarker.color = head ? UIKit.Bad : Color.white; }
        public void Flash(float power) { flashAlpha = Mathf.Clamp01(power); }
        public void SetNV(bool on) { nvImg.color = new Color(0.2f, 1f, 0.3f, on ? 0.18f : 0); }
        public void SixthSense() { senseUntil = Time.time + 2f; senseText.text = "Вас заметили!"; }
        public void ShowDeath(string attacker, string weapon) { deathPanel.gameObject.SetActive(true); deathText.text = $"ВЫ ПОГИБЛИ\n<size=20>{attacker}{(string.IsNullOrEmpty(weapon) ? "" : " · " + weapon)}</size>\n<size=16>Снаряжение потеряно, кроме защищённого контейнера.</size>"; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        public void AddChat(string who, string msg)
        {
            var t = UIKit.Label(chatCol, $"<color=#ffb347>{who}:</color> {msg}", 16, TextAnchor.MiddleLeft); t.gameObject.AddComponent<Outline>().effectColor = Color.black;
            feed.Add((t.gameObject, Time.time + 12f));
        }
        public void AddKill(string text)
        {
            var t = UIKit.Label(killCol, text, 16, TextAnchor.MiddleRight); t.gameObject.AddComponent<Outline>().effectColor = Color.black;
            feed.Add((t.gameObject, Time.time + 8f));
        }

        public void OpenChat()
        {
            ChatOpen = true; chatField.gameObject.SetActive(true); chatField.text = ""; chatField.ActivateInputField(); chatField.Select();
        }

        public void Tick()
        {
            var lp = Game.Manager.Net.LocalPlayer;
            if (ChatOpen)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    string m = chatField.text.Trim(); if (m.Length > 0) Game.Manager.Net.Session?.ChatRpc(m);
                    ChatOpen = false; chatField.gameObject.SetActive(false);
                }
                if (Input.GetKeyDown(KeyCode.Escape)) { ChatOpen = false; chatField.gameObject.SetActive(false); }
            }
            for (int i = feed.Count - 1; i >= 0; i--) if (Time.time > feed[i].until) { if (feed[i].go != null) Object.Destroy(feed[i].go); feed.RemoveAt(i); }
            damageAlpha = Mathf.MoveTowards(damageAlpha, 0, Time.deltaTime * 0.6f); damageVignette.color = new Color(0.8f, 0, 0, damageAlpha);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0, Time.deltaTime * 0.35f); flashImg.color = new Color(1, 1, 1, flashAlpha);
            hitMarker.color = new Color(hitMarker.color.r, hitMarker.color.g, hitMarker.color.b, Time.time < hitUntil ? 1 : 0);
            senseText.text = Time.time < senseUntil ? "Вас заметили!" : "";
            if (lp == null) return;
            stamina.fillAmount = lp.Ctrl.Stamina / Mathf.Max(1, lp.Ctrl.MaxStamina);
            var s = Game.Manager.Net.Session;
            if (s != null) { double left = s.TimeLeft; timer.text = $"{(int)(left / 60):00}:{(int)(left % 60):00}   <size=16>врагов: {s.EnemiesAlive.Value}{(s.BossAlive.Value ? " · БОСС" : "")}</size>"; }
            // компас с маркерами выходов (и квестов — при перках/разведцентре)
            float yaw = lp.transform.eulerAngles.y;
            var sb = new System.Text.StringBuilder();
            string[] dirs = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
            sb.Append($"<b>{dirs[Mathf.RoundToInt(yaw / 45f) % 8]} {yaw:000}°</b>   ");
            bool showAll = Stats.Has("showExtracts");
            foreach (var e in World.Extracts)
            {
                if (!showAll && !string.IsNullOrEmpty(e.Def.cond) && !e.LocalInside) continue;
                var d = e.transform.position - lp.transform.position; float ang = Mathf.DeltaAngle(yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                if (Mathf.Abs(ang) < 60) sb.Append($"<color=#7ee07e>▲{e.Def.name} {d.magnitude:0}м</color>  ");
            }
            if (Stats.Has("memory_m30") || Stats.Has("showExtracts"))
                foreach (var p in World.Pois) if (QuestSystem.PoisNeeded(World.Map.id).Contains(p.Name)) { var d = p.transform.position - lp.transform.position; float ang = Mathf.DeltaAngle(yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg); if (Mathf.Abs(ang) < 60) sb.Append($"<color=#ffb347>◆{p.Name} {d.magnitude:0}м</color>  "); }
            compass.text = sb.ToString();
            // прогресс действия/осмотра
            bool usingNow = lp.Weapons.Using; float sp = lp.Interact.SearchProgress;
            usingText.text = usingNow ? lp.Weapons.UsingText : lp.Weapons.Reloading ? "Перезарядка…" : "";
            usingBar.transform.parent.gameObject.SetActive(usingNow || sp > 0);
            if (sp > 0) usingBar.fillAmount = sp;
            if (Time.frameCount % 30 == 0) { RefreshQuests(); RefreshAmmo(); }
        }
    }
}
