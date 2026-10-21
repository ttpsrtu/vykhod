using UnityEngine;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>Главное меню: одна большая кнопка «Играть», остальное — во всплывающих окнах.</summary>
    public class MainMenuUI
    {
        Canvas canvas; RectTransform profileChip;

        public void Show() { if (canvas == null) Build(); canvas.gameObject.SetActive(true); UIKit.SetChip(profileChip, ProfileText()); }
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); UIKit.CloseAllPopups(); }

        string ProfileText()
        {
            var p = Game.Profile ?? SaveSystem.Load();
            return p != null ? $"{p.name}  ·  уровень {p.level}" : Game.Settings.playerName;
        }

        void Build()
        {
            canvas = UIKit.NewCanvas("MainMenu", 30);
            UIKit.Background(canvas.transform);
            // логотип
            var logo = UIKit.Label(canvas.transform, "ВЫХОД", 150, TextAnchor.MiddleLeft, UIKit.Yellow, true, 0, true); UIKit.Place(logo.rectTransform, new Vector2(0, 1), new Vector2(90, -90), new Vector2(900, 170));
            logo.GetComponent<Outline>().effectDistance = new Vector2(5, -5);
            var sub = UIKit.Label(canvas.transform, "ЭКСТРАКШЕН-ШУТЕР · КООПЕРАТИВ И PVP", 26, TextAnchor.MiddleLeft, UIKit.Cyan, true, 0, true); UIKit.Place(sub.rectTransform, new Vector2(0, 1), new Vector2(96, -270), new Vector2(900, 40));
            var tag = UIKit.Label(canvas.transform, "6 карт · 78 стволов · 125 модулей · 69 заданий · прокачка · убежище", 20, TextAnchor.MiddleLeft, UIKit.Muted, true, 0, true); UIKit.Place(tag.rectTransform, new Vector2(0, 1), new Vector2(96, -312), new Vector2(900, 30));
            // профиль
            profileChip = UIKit.Chip(canvas.transform, ProfileText(), new Color(0, 0, 0, 0.35f), "level", 22, 52); UIKit.Place(profileChip, new Vector2(1, 1), new Vector2(-40, -30), new Vector2(400, 52));
            // колонка кнопок
            var col = UIKit.Column(canvas.transform, 16, 0); UIKit.Place(col, new Vector2(1, 0.5f), new Vector2(-80, -40), new Vector2(520, 460));
            UIKit.BigButton(col, "Играть", UIKit.Yellow, Play, 0, 110, 48, UIKit.TextDark, "play");
            UIKit.BigButton(col, "Подключиться к другу", UIKit.Cyan, JoinPopup, 0, 76, 28, UIKit.TextDark, "join");
            UIKit.BigButton(col, "Настройки", UIKit.Purple, SettingsPopup, 0, 76, 28, Color.white, "settings");
            UIKit.BigButton(col, "Выход", UIKit.Red, () => Application.Quit(), 0, 64, 24, Color.white, "exit");
            var ver = UIKit.Label(canvas.transform, $"v{Application.version} · {Application.platform}", 16, TextAnchor.LowerLeft, UIKit.Muted, true, 0, true); UIKit.Place(ver.rectTransform, new Vector2(0, 0), new Vector2(24, 16), new Vector2(600, 24));
        }

        void Play()
        {
            if (Game.Manager.ContinueGame()) return;
            var content = UIKit.Popup("Новый боец", 620, 320, out var c);
            UIKit.Label(c, "Как вас называть в рейде?", 22, TextAnchor.MiddleLeft, UIKit.Muted, true, 34);
            var f = UIKit.Field(c, "Имя бойца", Game.Settings.playerName, 0, 56);
            UIKit.BigButton(c, "Начать", UIKit.Yellow, () => { string n = string.IsNullOrWhiteSpace(f.text) ? "Боец" : f.text.Trim(); UIKit.CloseAllPopups(); Game.Manager.NewGame(n); }, 0, 70, 30, UIKit.TextDark);
        }

        void JoinPopup()
        {
            UIKit.Popup("Подключиться к другу", 760, 420, out var c, UIKit.Cyan);
            UIKit.Label(c, "Друг запускает рейд из лобби и сообщает свой IP. В интернете нужен белый IP с открытым портом 7777 или Radmin VPN / Hamachi / ZeroTier.", 19, TextAnchor.UpperLeft, UIKit.Muted, false, 80);
            var f = UIKit.Field(c, "IP адрес хоста, например 26.10.20.30", Game.Settings.lastIp, 0, 56);
            UIKit.BigButton(c, "Подключиться", UIKit.Cyan, () =>
            {
                if (Game.Profile == null) { var p = SaveSystem.Load(); if (p == null) { Game.Notify("Сначала нажмите «Играть» и создайте бойца"); return; } Game.Profile = p; }
                string ip = f.text.Trim(); ushort port = 7777; if (ip.Contains(":")) { var ps = ip.Split(':'); ip = ps[0]; ushort.TryParse(ps[1], out port); }
                UIKit.CloseAllPopups(); Game.Manager.JoinRaid(ip, port);
            }, 0, 70, 30, UIKit.TextDark);
        }

        public static void SettingsPopup()
        {
            UIKit.Popup("Настройки", 820, 560, out var c, UIKit.Purple);
            UIKit.SliderRow(c, "Чувствительность", 0.2f, 5f, Game.Settings.sensitivity, v => Game.Settings.sensitivity = v);
            UIKit.SliderRow(c, "Поле зрения", 60, 110, Game.Settings.fov, v => Game.Settings.fov = v, "0");
            UIKit.SliderRow(c, "Громкость", 0, 1, Game.Settings.volume, v => Game.Settings.volume = v);
            UIKit.SliderRow(c, "Качество графики", 0, 2, Game.Settings.quality, v => Game.Settings.quality = Mathf.RoundToInt(v), "0");
            var inv = UIKit.Btn(c, "Инверсия мыши: " + (Game.Settings.invertY ? "вкл" : "выкл"), null, 0, 48, UIKit.Blue, 20);
            inv.onClick.AddListener(() => { Game.Settings.invertY = !Game.Settings.invertY; inv.GetComponentInChildren<Text>().text = "ИНВЕРСИЯ МЫШИ: " + (Game.Settings.invertY ? "ВКЛ" : "ВЫКЛ"); });
            var row = UIKit.Row(c, 12, 64);
            UIKit.BigButton(row, "Применить", UIKit.Yellow, () => { Game.Settings.Save(); Game.Settings.Apply(); UIKit.CloseAllPopups(); }, 0, 60, 24, UIKit.TextDark);
            UIKit.BigButton(row, "Новый персонаж", UIKit.Red, () => { SaveSystem.Delete(); Game.Profile = null; Game.Notify("Сохранение удалено"); UIKit.CloseAllPopups(); }, 300, 60, 20, Color.white);
        }
    }
}
