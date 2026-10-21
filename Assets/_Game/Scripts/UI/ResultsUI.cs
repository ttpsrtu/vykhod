using UnityEngine;

namespace Vykhod
{
    public class ResultsUI
    {
        Canvas canvas; RectTransform col;
        public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); }
        public void Show(bool extracted)
        {
            if (canvas == null) { canvas = UIKit.NewCanvas("Results", 30); UIKit.Panel(canvas.transform, UIKit.Bg); col = UIKit.Column(canvas.transform, 10, 20); UIKit.Place(col, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 600)); }
            canvas.gameObject.SetActive(true); UIKit.Clear(col);
            var r = Game.Manager.Raid; var p = Game.Profile;
            UIKit.Label(col, extracted ? "ВЫ ВЫШЛИ" : "РЕЙД ПРОВАЛЕН", 48, TextAnchor.MiddleCenter, extracted ? UIKit.Good : UIKit.Bad, true, 70);
            UIKit.Label(col, r.extractName, 20, TextAnchor.MiddleCenter, UIKit.Muted, false, 30);
            float mins = (Time.time - r.startTime) / 60f;
            string s = $"Карта: {Database.Map(Game.Manager.RaidMap)?.name}   Режим: {(Game.Manager.RaidMode == "pvp" ? "PvP" : "Кооп")}   Время: {mins:0} мин\n" +
                       $"Убийства: {r.kills}  (в голову {r.headshots}, боссов {r.bossKills}, игроков {r.playerKills})\n" +
                       $"Урон: {r.damageDealt:0}   Лут найден на: {r.lootValue} ₽   Квестовых предметов: {r.questItems}\n" +
                       $"Опыт: +{(extracted ? r.xp : r.xp / 2)}   Уровень: {p.level}   Деньги: {p.money} ₽\n" +
                       $"Всего рейдов: {p.raids}, выходов: {p.extractions}, смертей: {p.deaths}, серия: {p.extractStreak}";
            UIKit.Label(col, s, 18, TextAnchor.UpperCenter, UIKit.TextC, false, 160);
            if (!extracted) UIKit.Label(col, "Потеряно всё, кроме защищённого контейнера" + (Stats.Has("keepRig") ? " и разгрузки" : "") + ". Застрахованное может вернуться.", 16, TextAnchor.MiddleCenter, UIKit.Muted, false, 30);
            UIKit.Btn(col, "В убежище", () => Game.Manager.EnterHideout(), 0, 50, UIKit.Accent);
        }
    }
}
