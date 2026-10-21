using UnityEngine;
using UnityEngine.UI;

namespace Vykhod
{
    public class ResultsUI
    {
        Canvas canvas; RectTransform col;
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); }
        public void Show(bool extracted)
        {
            if (canvas == null) { canvas = UIKit.NewCanvas("Results", 30); UIKit.Background(canvas.transform); var card = UIKit.Card(canvas.transform, UIKit.Panel, 32); UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 720)); col = UIKit.Column(card.transform, 14, 28); UIKit.Stretch(col); }
            canvas.gameObject.SetActive(true); UIKit.Clear(col);
            var r = Game.Manager.Raid; var p = Game.Profile;
            UIKit.Label(col, extracted ? "ВЫ ВЫШЛИ!" : "РЕЙД ПРОВАЛЕН", 72, TextAnchor.MiddleCenter, extracted ? UIKit.Green : UIKit.Red, true, 90, true);
            UIKit.Label(col, r.extractName + "  ·  " + (Database.Map(Game.Manager.RaidMap)?.name ?? "") + "  ·  " + (Game.Manager.RaidMode == "pvp" ? "PvP" : "Кооператив"), 22, TextAnchor.MiddleCenter, UIKit.Muted, true, 34);
            var grid = UIKit.Grid(col, new Vector2(260, 110), 12, 0); grid.GetComponent<GridLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            Stat(grid, "Убийства", r.kills.ToString(), UIKit.Red, "enemy"); Stat(grid, "В голову", r.headshots.ToString(), UIKit.Orange, "enemy"); Stat(grid, "Боссы", r.bossKills.ToString(), UIKit.Purple, "skull");
            Stat(grid, "Лут найден", $"{r.lootValue:N0} ₽", UIKit.Yellow, "money"); Stat(grid, "Опыт", $"+{(extracted ? r.xp : r.xp / 2):N0}", UIKit.Cyan, "level"); Stat(grid, "Урон", $"{r.damageDealt:0}", UIKit.Pink, "heart");
            long need = PlayerProfile.XpForLevel(p.level + 1), prev = PlayerProfile.XpForLevel(p.level);
            UIKit.Label(col, $"Уровень {p.level}   ·   {p.money:N0} ₽   ·   выходов {p.extractions} из {p.raids}, серия {p.extractStreak}", 20, TextAnchor.MiddleCenter, UIKit.TextC, true, 30);
            UIKit.Bar(col, UIKit.Cyan, 18).fillAmount = need > prev ? Mathf.Clamp01((float)(p.xp - prev) / (need - prev)) : 1;
            if (!extracted) UIKit.Label(col, "Потеряно всё, кроме защищённого контейнера" + (Stats.Has("keepRig") ? " и разгрузки" : "") + ". Застрахованное может вернуться.", 18, TextAnchor.MiddleCenter, UIKit.Muted, false, 30);
            UIKit.BigButton(col, "В лобби", UIKit.Yellow, () => Game.Manager.EnterHideout(), 0, 76, 32, UIKit.TextDark);
        }

        static void Stat(Transform grid, string title, string value, Color c, string icon)
        {
            var card = UIKit.Card(grid, Color.Lerp(c, UIKit.Dark, 0.55f), 18);
            var ic = UIKit.Icon(card.transform, icon, c, 40); UIKit.Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(40, 40));
            var v = UIKit.Label(card.transform, value, 34, TextAnchor.MiddleLeft, Color.white, true, 0, true); UIKit.Stretch(v.rectTransform, 66, 8, 8, 40);
            var t = UIKit.Label(card.transform, title.ToUpperInvariant(), 15, TextAnchor.MiddleLeft, UIKit.Muted, true); UIKit.Stretch(t.rectTransform, 66, 60, 8, 8);
        }
    }
}
