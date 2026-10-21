using System.Collections.Generic;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Контейнер с лутом. Содержимое хранит сервер (RaidSession).</summary>
    public class LootContainer : MonoBehaviour
    {
        public int Id;
        public string Kind = "crate";
        public string Title = "Ящик";
        public static readonly Dictionary<string, string> Titles = new Dictionary<string, string>
        {
            ["crate"] = "Ящик", ["locker"] = "Шкафчик", ["safe"] = "Сейф", ["medbag"] = "Медицинская сумка", ["weaponbox"] = "Оружейный ящик",
            ["jacket"] = "Куртка", ["desk"] = "Стол", ["body"] = "Тело", ["fridge"] = "Холодильник", ["toolbox"] = "Ящик с инструментами",
            ["shelf"] = "Полка", ["bag"] = "Рюкзак", ["server"] = "Серверная стойка", ["register"] = "Касса", ["cabinet"] = "Шкаф"
        };
        public float SearchTime => Mathf.Max(0.4f, (Kind == "safe" ? 4f : Kind == "body" ? 1.5f : 2.2f) * Mathf.Max(0.3f, 1f - Stats.Get("searchSpeed")) * (Stats.Has("attention_m10") ? 0.9f : 1f) * (Stats.Has("attention_m40") ? 0.7f : 1f));
    }
}
