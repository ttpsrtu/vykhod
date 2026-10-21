using UnityEngine;
using UnityEngine.UI;

namespace Vykhod
{
    public class MainMenuUI
    {
        Canvas canvas; RectTransform settings, join; InputField nameField, ipField;

        public void Show() { if (canvas == null) Build(); canvas.gameObject.SetActive(true); settings.gameObject.SetActive(false); join.gameObject.SetActive(false); }
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); }

        void Build()
        {
            canvas = UIKit.NewCanvas("MainMenu", 30);
            UIKit.Panel(canvas.transform, new Color(0.05f, 0.06f, 0.07f, 1));
            var title = UIKit.Label(canvas.transform, "ВЫХОД", 96, TextAnchor.MiddleCenter, UIKit.Accent, true); UIKit.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(800, 120));
            var sub = UIKit.Label(canvas.transform, "экстракшен-шутер от первого лица · кооператив и PvP", 20, TextAnchor.MiddleCenter, UIKit.Muted); UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -200), new Vector2(800, 30));
            var col = UIKit.Column(canvas.transform, 10, 0); UIKit.Place(col, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(420, 420));
            nameField = UIKit.Field(col, "Имя бойца", Game.Settings.playerName, 0, 42);
            UIKit.Btn(col, "Продолжить", () => { if (!Game.Manager.ContinueGame()) Game.Notify("Сохранения нет — начните новую игру"); }, 0, 48, SaveSystem.Exists ? UIKit.Accent : UIKit.BtnC);
            UIKit.Btn(col, "Новая игра", () => { string n = string.IsNullOrWhiteSpace(nameField.text) ? "Боец" : nameField.text.Trim(); Game.Manager.NewGame(n); }, 0, 48);
            UIKit.Btn(col, "Подключиться к другу (по IP)", () => { join.gameObject.SetActive(true); settings.gameObject.SetActive(false); }, 0, 48);
            UIKit.Btn(col, "Настройки", () => { settings.gameObject.SetActive(true); join.gameObject.SetActive(false); }, 0, 48);
            UIKit.Btn(col, "Выход", () => Application.Quit(), 0, 48, UIKit.Bad * 0.7f);
            var ver = UIKit.Label(canvas.transform, $"v{Application.version} · {Application.platform} · Unity {Application.unityVersion}", 13, TextAnchor.LowerLeft, UIKit.Muted); UIKit.Place(ver.rectTransform, new Vector2(0, 0), new Vector2(12, 8), new Vector2(600, 20));
            // настройки
            settings = UIKit.Box(canvas.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-560, -260), new Vector2(-40, 260), UIKit.Panel2, "settings");
            var sc = UIKit.Column(settings, 8, 14); UIKit.Stretch(sc);
            UIKit.Header(sc, "Настройки");
            UIKit.SliderRow(sc, "Чувствительность", 0.2f, 5f, Game.Settings.sensitivity, v => Game.Settings.sensitivity = v);
            UIKit.SliderRow(sc, "Поле зрения", 60, 110, Game.Settings.fov, v => Game.Settings.fov = v, "0");
            UIKit.SliderRow(sc, "Громкость", 0, 1, Game.Settings.volume, v => Game.Settings.volume = v);
            UIKit.SliderRow(sc, "Качество (0-2)", 0, 2, Game.Settings.quality, v => Game.Settings.quality = Mathf.RoundToInt(v), "0");
            UIKit.Btn(sc, "Инверсия мыши: " + (Game.Settings.invertY ? "вкл" : "выкл"), null, 0, 36).onClick.AddListener(() => { Game.Settings.invertY = !Game.Settings.invertY; settings.gameObject.SetActive(false); });
            UIKit.Label(sc, "macOS: сборка нативная (Metal), настройки те же. На ноутбуках ставьте качество 1.", 14, TextAnchor.UpperLeft, UIKit.Muted, false, 40);
            UIKit.Btn(sc, "Применить и закрыть", () => { Game.Settings.Save(); Game.Settings.Apply(); settings.gameObject.SetActive(false); }, 0, 42, UIKit.Accent);
            UIKit.Btn(sc, "Удалить сохранение", () => { SaveSystem.Delete(); Game.Notify("Сохранение удалено"); }, 0, 36, UIKit.Bad * 0.6f);
            settings.gameObject.SetActive(false);
            // подключение
            join = UIKit.Box(canvas.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-560, -160), new Vector2(-40, 160), UIKit.Panel2, "join");
            var jc = UIKit.Column(join, 8, 14); UIKit.Stretch(jc);
            UIKit.Header(jc, "Подключение к хосту");
            UIKit.Label(jc, "Хост запускает рейд из убежища, вы вводите его IP (в локальной сети, через Radmin VPN/Hamachi/ZeroTier или белый IP с открытым портом 7777). Нужно существующее сохранение.", 15, TextAnchor.UpperLeft, UIKit.Muted, false, 70);
            ipField = UIKit.Field(jc, "IP адрес хоста", Game.Settings.lastIp, 0, 42);
            UIKit.Btn(jc, "Подключиться", () =>
            {
                if (Game.Profile == null) { var p = SaveSystem.Load(); if (p == null) { Game.Notify("Сначала создайте персонажа (Новая игра)"); return; } Game.Profile = p; }
                string ip = ipField.text.Trim(); ushort port = 7777; if (ip.Contains(":")) { var parts = ip.Split(':'); ip = parts[0]; ushort.TryParse(parts[1], out port); }
                Game.Manager.JoinRaid(ip, port);
            }, 0, 44, UIKit.Accent);
            UIKit.Btn(jc, "Закрыть", () => join.gameObject.SetActive(false), 0, 36);
            join.gameObject.SetActive(false);
        }
    }
}
