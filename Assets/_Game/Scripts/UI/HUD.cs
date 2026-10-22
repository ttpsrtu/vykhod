using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>
    /// HUD рейда: компас-лента с меткой выхода, таймер и враги, карточка задания, здоровье/силы/статусы, оружие и патроны.
    /// Крупный текст с тенью на тёмных нейтральных панелях — читается на любом фоне.
    /// </summary>
    public class HUD
    {
        Canvas canvas; Text hpText, ammo, weapon, mode, ammoType, timer, enemies, hint, questText, statusText, weightText, deathText, usingText, extractText, senseText;
        Image stamina, damageVignette, hitMarker, flashImg, nvImg, usingBar, hpFill, modeChip; RectTransform partsPanel, usingRoot, hintChip, chatCol, killCol, deathPanel, band, extractMark, questCard;
        readonly Image[] partBars = new Image[7];
        InputField chatField;
        readonly List<(GameObject go, float until)> feed = new List<(GameObject, float)>();
        float hitUntil, damageAlpha, senseUntil, flashAlpha;
        public bool ChatOpen;
        const float Ppd = 4f; // пикселей на градус компаса: лента 640 px = 160°

        static readonly Color Panel = new Color(0.03f, 0.04f, 0.06f, 0.74f), Dim = new Color(0.72f, 0.76f, 0.84f);
        static Color Accent => UIKit.Yellow; static Color Good => UIKit.Green;

        public void Show() { if (canvas == null) Build(); canvas.gameObject.SetActive(true); deathPanel.gameObject.SetActive(false); RefreshHealth(); RefreshAmmo(); RefreshQuests(); }
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); }

        /// <summary>Текст с мягкой тенью (без жирной обводки «как в бюджетной игре»).</summary>
        static Text T(Transform p, string s, int size, TextAnchor a, Color c, bool bold = true)
        {
            var t = UIKit.Label(p, s, size, a, c, bold); t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.9f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        static Sprite vignette;
        /// <summary>Виньетка урона: прозрачный центр, красные края (а не заливка всего экрана).</summary>
        static Sprite VignetteSprite()
        {
            if (vignette != null) return vignette;
            int s = 160; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2 - 1, dy = (y + 0.5f) / s * 2 - 1; float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((d - 0.45f) / 0.75f); a = a * a;
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            vignette = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            return vignette;
        }

        static Image Box(Transform p, Vector2 anchor, Vector2 pos, Vector2 size, string name, int radius = 12)
        { var c = UIKit.Card(p, Panel, radius, name, false); UIKit.Place(c.rectTransform, anchor, pos, size); return c; }

        static Image BarBg(Transform p, Vector2 offMin, Vector2 offMax, int radius)
        {
            var bg = UIKit.Img(p, new Color(0, 0, 0, 0.5f), "barbg"); bg.sprite = UIKit.Rounded(radius); bg.type = Image.Type.Sliced;
            bg.rectTransform.anchorMin = new Vector2(0, 1); bg.rectTransform.anchorMax = new Vector2(1, 1); bg.rectTransform.pivot = new Vector2(0, 1); bg.rectTransform.offsetMin = offMin; bg.rectTransform.offsetMax = offMax;
            return bg;
        }

        static Image BarFill(Image bg, Color c, int radius)
        {
            var f = UIKit.Img(bg.transform, c, "fill"); f.sprite = UIKit.Rounded(radius); f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal; f.fillAmount = 1; UIKit.Stretch(f.rectTransform, 2, 2, 2, 2);
            return f;
        }

        void Build()
        {
            canvas = UIKit.NewCanvas("HUD", 10); var t = canvas.transform;
            damageVignette = UIKit.Img(t, new Color(0.8f, 0, 0.05f, 0)); damageVignette.sprite = VignetteSprite(); UIKit.Stretch(damageVignette.rectTransform);
            nvImg = UIKit.Img(t, new Color(0.2f, 1f, 0.3f, 0)); UIKit.Stretch(nvImg.rectTransform);
            flashImg = UIKit.Img(t, new Color(1, 1, 1, 0)); UIKit.Stretch(flashImg.rectTransform);
            // ---- прицел: точка и четыре штриха с тенью
            var dot = UIKit.Img(t, new Color(1, 1, 1, 0.95f)); dot.sprite = UIKit.Circle(); UIKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 4));
            foreach (var (p, s) in new[] { (new Vector2(0, 13), new Vector2(2, 9)), (new Vector2(0, -13), new Vector2(2, 9)), (new Vector2(13, 0), new Vector2(9, 2)), (new Vector2(-13, 0), new Vector2(9, 2)) })
            { var l = UIKit.Img(t, new Color(1, 1, 1, 0.92f)); UIKit.Place(l.rectTransform, new Vector2(0.5f, 0.5f), p, s); var sh = l.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.8f); sh.effectDistance = new Vector2(1, -1); }
            hitMarker = UIKit.Img(t, new Color(1, 1, 1, 0)); UIKit.Place(hitMarker.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22)); hitMarker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            // ---- верх-центр: компас-лента
            var comp = Box(t, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(640, 44), "compass");
            comp.gameObject.AddComponent<RectMask2D>();
            band = UIKit.Rect(comp.transform, "band"); band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f); band.pivot = new Vector2(0.5f, 0.5f); band.sizeDelta = new Vector2(1080 * Ppd, 44);
            string[] dirs = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
            for (int deg = -360; deg <= 720; deg += 15)
            {
                int d = ((deg % 360) + 360) % 360;
                if (d % 45 == 0)
                {
                    bool major = d % 90 == 0;
                    var l = T(band, dirs[d / 45], major ? 23 : 15, TextAnchor.MiddleCenter, major ? Color.white : Dim);
                    UIKit.Place(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(deg * Ppd, major ? 1 : 0), new Vector2(60, 44));
                }
                else { var tick = UIKit.Img(band, new Color(1, 1, 1, 0.35f)); UIKit.Place(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(deg * Ppd, -9), new Vector2(2, 10)); }
            }
            var centerMark = UIKit.Img(comp.transform, Accent, "center"); UIKit.Place(centerMark.rectTransform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(3, 11));
            extractMark = UIKit.Rect(comp.transform, "extract"); UIKit.Place(extractMark, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 44));
            var em = T(extractMark, "▲", 20, TextAnchor.MiddleCenter, Good); UIKit.Stretch(em.rectTransform);
            // под лентой: таймер · враги · ближайший выход
            var tc = Box(t, new Vector2(0.5f, 1), new Vector2(-70, -68), new Vector2(126, 36), "timer");
            timer = T(tc.transform, "", 24, TextAnchor.MiddleCenter, Color.white); UIKit.Stretch(timer.rectTransform);
            var ec = Box(t, new Vector2(0.5f, 1), new Vector2(70, -68), new Vector2(126, 36), "enemies");
            var sk = UIKit.Icon(ec.transform, "skull", UIKit.Red, 18); UIKit.Place(sk.rectTransform, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(18, 18));
            enemies = T(ec.transform, "", 20, TextAnchor.MiddleCenter, UIKit.Red); UIKit.Stretch(enemies.rectTransform, 30, 0, 8, 0);
            extractText = T(t, "", 17, TextAnchor.MiddleCenter, Good); UIKit.Place(extractText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(800, 24));

            // ---- верх-лево: задание
            var qc = Box(t, new Vector2(0, 1), new Vector2(28, -28), new Vector2(470, 66), "quest"); questCard = qc.rectTransform;
            var qs = UIKit.Img(qc.transform, Accent, "stripe"); qs.sprite = UIKit.Rounded(12); qs.type = Image.Type.Sliced; qs.rectTransform.anchorMin = Vector2.zero; qs.rectTransform.anchorMax = new Vector2(0, 1); qs.rectTransform.offsetMin = Vector2.zero; qs.rectTransform.offsetMax = new Vector2(5, 0);
            var qt = T(qc.transform, "ЗАДАНИЕ", 12, TextAnchor.UpperLeft, Accent); UIKit.Stretch(qt.rectTransform, 18, 8, 12, 40);
            questText = T(qc.transform, "", 16, TextAnchor.LowerLeft, Color.white); questText.horizontalOverflow = HorizontalWrapMode.Wrap; questText.verticalOverflow = VerticalWrapMode.Truncate; UIKit.Stretch(questText.rectTransform, 18, 26, 12, 8);

            // ---- низ-лево: здоровье, силы, статусы
            var hp = Box(t, new Vector2(0, 0), new Vector2(28, 28), new Vector2(440, 116), "health");
            var heart = UIKit.Icon(hp.transform, "heart", UIKit.Red, 28); UIKit.Place(heart.rectTransform, new Vector2(0, 1), new Vector2(16, -13), new Vector2(28, 28));
            var hpBg = BarBg(hp.transform, new Vector2(56, -44), new Vector2(-16, -13), 8);
            hpFill = BarFill(hpBg, Good, 8);
            hpText = T(hpBg.transform, "", 19, TextAnchor.MiddleCenter, Color.white); UIKit.Stretch(hpText.rectTransform);
            var bolt = UIKit.Icon(hp.transform, "skills", UIKit.Cyan, 20); UIKit.Place(bolt.rectTransform, new Vector2(0, 1), new Vector2(20, -51), new Vector2(20, 20));
            var stBg = BarBg(hp.transform, new Vector2(56, -64), new Vector2(-16, -51), 6);
            stamina = BarFill(stBg, UIKit.Cyan, 6);
            statusText = T(hp.transform, "", 15, TextAnchor.MiddleLeft, UIKit.Red); UIKit.Stretch(statusText.rectTransform, 56, 70, 12, 8);
            weightText = T(hp.transform, "", 14, TextAnchor.MiddleRight, UIKit.Red); UIKit.Stretch(weightText.rectTransform, 220, 70, 16, 8);
            // части тела — отдельная панель над здоровьем, только при ранениях
            var pp = Box(t, new Vector2(0, 0), new Vector2(28, 152), new Vector2(440, 46), "parts"); partsPanel = pp.rectTransform;
            var prow = UIKit.Row(pp.transform, 6, 46, 10); UIKit.Stretch(prow);
            string[] ab = { "Голова", "Грудь", "Живот", "Л.рука", "П.рука", "Л.нога", "П.нога" };
            for (int i = 0; i < 7; i++)
            {
                var c = UIKit.Column(prow, 2, 0); c.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                partBars[i] = UIKit.Bar(c, Good, 7);
                var l = T(c, ab[i], 10, TextAnchor.MiddleCenter, Dim); l.gameObject.AddComponent<LayoutElement>().minHeight = 12;
            }
            pp.gameObject.SetActive(false);

            // ---- низ-право: оружие и патроны
            var wp = Box(t, new Vector2(1, 0), new Vector2(-28, 28), new Vector2(390, 116), "weapon");
            weapon = T(wp.transform, "", 22, TextAnchor.UpperRight, Color.white); UIKit.Stretch(weapon.rectTransform, 110, 12, 18, 74);
            modeChip = UIKit.Card(wp.transform, new Color(1f, 0.83f, 0f, 0.16f), 8, "modechip", false); UIKit.Place(modeChip.rectTransform, new Vector2(0, 1), new Vector2(16, -12), new Vector2(88, 28));
            mode = T(modeChip.transform, "", 14, TextAnchor.MiddleCenter, Accent); UIKit.Stretch(mode.rectTransform);
            ammo = T(wp.transform, "", 58, TextAnchor.LowerRight, Color.white); UIKit.Stretch(ammo.rectTransform, 16, 36, 16, 2);
            ammoType = T(wp.transform, "", 14, TextAnchor.LowerLeft, Dim); UIKit.Stretch(ammoType.rectTransform, 18, 72, 160, 12);

            // ---- килл-фид (верх-право) и чат (над здоровьем)
            killCol = UIKit.Column(t, 2, 0); killCol.anchorMin = killCol.anchorMax = killCol.pivot = new Vector2(1, 1); killCol.anchoredPosition = new Vector2(-28, -28); killCol.sizeDelta = new Vector2(440, 240); killCol.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperRight;
            chatCol = UIKit.Column(t, 2, 0); chatCol.anchorMin = chatCol.anchorMax = chatCol.pivot = new Vector2(0, 0); chatCol.anchoredPosition = new Vector2(28, 250); chatCol.sizeDelta = new Vector2(560, 200); chatCol.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            chatField = UIKit.Field(t, "Сообщение… (Enter)", "", 560, 36); UIKit.Place(chatField.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(28, 208), new Vector2(560, 36)); chatField.gameObject.SetActive(false);

            // ---- подсказка взаимодействия, прогресс действия, «вас заметили»
            hintChip = UIKit.Chip(t, "", new Color(0.03f, 0.04f, 0.06f, 0.8f), null, 18, 40); UIKit.Place(hintChip, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(600, 40)); hint = hintChip.Find("chipText").GetComponent<Text>();
            usingRoot = UIKit.Rect(t, "using"); UIKit.Place(usingRoot, new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(360, 36));
            usingText = T(usingRoot, "", 16, TextAnchor.UpperCenter, Accent); UIKit.Stretch(usingText.rectTransform, 0, 0, 0, 14);
            var ub = UIKit.Rect(usingRoot, "bar"); ub.anchorMin = new Vector2(0, 0); ub.anchorMax = new Vector2(1, 0); ub.offsetMin = Vector2.zero; ub.offsetMax = new Vector2(0, 8); usingBar = UIKit.Bar(ub, Accent, 8); UIKit.Stretch(usingBar.transform.parent.GetComponent<RectTransform>());
            senseText = T(t, "", 24, TextAnchor.MiddleCenter, UIKit.Red); UIKit.Place(senseText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(600, 36));

            // ---- смерть
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
            float tot = Mathf.Clamp01(h.Total / h.MaxTotal(mul)); hpFill.fillAmount = tot; hpFill.color = tot < 0.3f ? UIKit.Red : tot < 0.6f ? UIKit.Orange : Good;
            hpText.text = $"{h.Total:0} / {h.MaxTotal(mul):0}";
            bool anyHurt = false;
            for (int i = 0; i < 7; i++)
            {
                float f = Mathf.Clamp01(h.Get(i) / BodyHealth.MaxOf(i, mul)); partBars[i].fillAmount = f;
                partBars[i].color = f <= 0 ? new Color(0.35f, 0.35f, 0.35f) : f < 0.35f ? UIKit.Red : f < 0.7f ? UIKit.Orange : Good;
                if (f < 0.7f || h.Bleeding(i) || h.HeavyBleeding(i) || h.Fractured(i)) anyHurt = true;
            }
            partsPanel.gameObject.SetActive(anyHurt);
            var st = new List<string>();
            if (h.AnyBleed) st.Add("<color=#ff3b5c>▼ КРОВОТЕЧЕНИЕ</color>"); if (h.AnyFracture) st.Add("<color=#ffd400>✕ ПЕРЕЛОМ</color>"); if (lp.InPain && !lp.PainFree) st.Add("<color=#ff8a2a>БОЛЬ</color>");
            if (h.energy < 20) st.Add("<color=#ffd400>ГОЛОД</color>"); if (h.hydration < 20) st.Add("<color=#19d3ff>ЖАЖДА</color>");
            statusText.text = string.Join("   ", st);
        }

        public void RefreshAmmo()
        {
            var lp = Game.Manager.Net.LocalPlayer; if (lp == null || lp.Weapons == null || ammo == null) return;
            var w = lp.Weapons.Current;
            if (w == null) { weapon.text = "нет оружия"; ammo.text = ""; ammoType.text = ""; modeChip.gameObject.SetActive(false); return; }
            var def = w.Info.weapon;
            weapon.text = def.name;
            modeChip.gameObject.SetActive(!def.IsMelee); mode.text = lp.Weapons.ModeName;
            ammo.text = def.IsMelee ? "" : $"{w.ammoInMag}<size=26><color=#b9c3e6>  /  {lp.Weapons.AmmoReserve}</color></size>";
            ammoType.text = def.IsMelee ? "" : $"{Database.NameOf(w.ammoId).Replace(def.caliber + " ", "")}{(lp.Weapons.WC != null && lp.Weapons.WC.suppressed ? " · глушитель" : "")}";
            var lo = Game.Manager.RaidLoadout; weightText.text = lp.Ctrl.Overweight > 0 ? $"ПЕРЕГРУЗ {lo.Weight:0.0}/{lp.Ctrl.CarryWeight:0} кг" : "";
        }

        public void RefreshQuests()
        {
            if (questText == null) return;
            var map = World.Map?.id; questText.text = ""; questCard.gameObject.SetActive(false);
            foreach (var q in QuestSystem.Active())
            {
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i]; if (QuestSystem.ObjectiveDone(q, i)) continue;
                    if (!string.IsNullOrEmpty(o.map) && o.map != "*" && o.map != map) continue;
                    questText.text = $"{o.Describe()}  <color=#ffd400>{QuestSystem.ObjectiveProgress(q, i)}/{o.Goal}</color>"; questCard.gameObject.SetActive(true); return;
                }
            }
        }

        public void SetHint(string text) { if (hint != null) { hint.text = text; hintChip.gameObject.SetActive(!string.IsNullOrEmpty(text)); } }
        public void Damaged(string part, float dmg) { damageAlpha = Mathf.Min(0.85f, damageAlpha + dmg / 60f); PostFx.Punch(dmg / 300f); RefreshHealth(); }
        public void HitMarker(bool head) { hitUntil = Time.time + 0.12f; hitMarker.color = head ? UIKit.Red : Color.white; }
        public void Flash(float power) { flashAlpha = Mathf.Clamp01(power); }
        public void SetNV(bool on) { nvImg.color = new Color(0.2f, 1f, 0.3f, on ? 0.18f : 0); }
        public void SixthSense() { senseUntil = Time.time + 2f; }
        public void ShowDeath(string attacker, string weapon) { deathPanel.gameObject.SetActive(true); deathText.text = $"<size=44><color=#ff3b5c>ВЫ ПОГИБЛИ</color></size>\n{attacker}{(string.IsNullOrEmpty(weapon) ? "" : " · " + weapon)}\n<size=15><color=#b9c3e6>Снаряжение потеряно, кроме защищённого контейнера.</color></size>"; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        public void AddChat(string who, string msg) { var t = T(chatCol, $"<color=#ffd400>{who}:</color> {msg}", 16, TextAnchor.MiddleLeft, Color.white); feed.Add((t.gameObject, Time.time + 12f)); }
        public void AddKill(string text) { var t = T(killCol, text, 15, TextAnchor.MiddleRight, Color.white); feed.Add((t.gameObject, Time.time + 8f)); }
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
            damageAlpha = Mathf.MoveTowards(damageAlpha, 0, Time.deltaTime * 0.6f); damageVignette.color = new Color(0.8f, 0, 0.05f, damageAlpha); PostFx.Relax(Time.deltaTime);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0, Time.deltaTime * 0.35f); flashImg.color = new Color(1, 1, 1, flashAlpha);
            hitMarker.color = new Color(hitMarker.color.r, hitMarker.color.g, hitMarker.color.b, Time.time < hitUntil ? 1 : 0);
            senseText.text = Time.time < senseUntil ? "ВАС ЗАМЕТИЛИ" : "";
            if (lp == null) return;
            stamina.fillAmount = lp.Ctrl.Stamina / Mathf.Max(1, lp.Ctrl.MaxStamina);
            var s = Game.Manager.Net.Session;
            if (s != null) { double left = s.TimeLeft; timer.text = $"{(int)(left / 60):00}:{(int)(left % 60):00}"; enemies.text = $"{s.EnemiesAlive.Value}{(s.BossAlive.Value ? "  БОСС" : "")}"; }
            // компас-лента: сдвиг по курсу, метка ближайшего доступного выхода
            float yaw = lp.transform.eulerAngles.y;
            band.anchoredPosition = new Vector2(-yaw * Ppd, 0);
            ExtractZone best = null; float bestD = float.MaxValue; bool showAll = Stats.Has("showExtracts");
            foreach (var e in World.Extracts) { if (!showAll && !string.IsNullOrEmpty(e.Def.cond)) continue; float d = Vector3.Distance(e.transform.position, lp.transform.position); if (d < bestD) { bestD = d; best = e; } }
            if (best != null)
            {
                var d = best.transform.position - lp.transform.position; float ang = Mathf.DeltaAngle(yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                extractMark.gameObject.SetActive(true); extractMark.anchoredPosition = new Vector2(Mathf.Clamp(ang * Ppd, -300, 300), 0);
                string arrow = Mathf.Abs(ang) <= 80 ? "" : ang > 0 ? "▶ " : "◀ ";
                extractText.text = $"{arrow}{best.Def.name} · {bestD:0} м";
            }
            else { extractMark.gameObject.SetActive(false); extractText.text = ""; }
            bool usingNow = lp.Weapons.Using; float sp = lp.Interact.SearchProgress;
            usingText.text = usingNow ? lp.Weapons.UsingText : lp.Weapons.Reloading ? "Перезарядка…" : sp > 0 ? "Осмотр…" : "";
            usingRoot.gameObject.SetActive(usingNow || sp > 0 || lp.Weapons.Reloading);
            if (sp > 0) usingBar.fillAmount = sp; else if (usingNow) usingBar.fillAmount = 1;
            if (Time.frameCount % 30 == 0) { RefreshQuests(); RefreshAmmo(); }
        }
    }
}
