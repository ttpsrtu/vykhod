using System.Collections.Generic;
using UnityEngine;

namespace Vykhod
{
    /// <summary>
    /// Агрегатор модификаторов: навыки (perLevel × уровень), перки, убежище, экипировка.
    /// Любая система спрашивает Stats.Get("recoil") и получает сумму (напр. -0.23 = −23%).
    /// Stats.Mul("recoil") = 1 + Get(...) с нижней границей.
    /// </summary>
    public static class Stats
    {
        static readonly Dictionary<string, float> values = new Dictionary<string, float>();
        public static int Version { get; private set; }

        public static float Get(string key) => values.TryGetValue(key, out var v) ? v : 0f;
        public static float Mul(string key, float min = 0.1f) => Mathf.Max(min, 1f + Get(key));
        public static bool Has(string key) => Get(key) > 0;

        static void Add(string k, float v) { if (string.IsNullOrEmpty(k) || k == "none") return; values[k] = Get(k) + v; }

        public static void Rebuild(PlayerProfile p)
        {
            values.Clear();
            if (p == null) return;
            foreach (var s in Database.SkillList)
            {
                int lv = p.SkillLevel(s.id);
                foreach (var kv in s.perLevel) Add(kv.Key, kv.Value * lv);
                // рубежи навыков дают дополнительные бонусы
                if (lv >= 10) Add(s.id + "_m10", 1); if (lv >= 20) Add(s.id + "_m20", 1);
                if (lv >= 30) Add(s.id + "_m30", 1); if (lv >= 40) Add(s.id + "_m40", 1); if (lv >= 50) Add(s.id + "_m50", 1);
            }
            foreach (var pid in p.perks)
            {
                var perk = Database.Perk(pid);
                if (perk != null) foreach (var kv in perk.effects) Add(kv.Key, kv.Value);
            }
            foreach (var m in Database.HideoutList)
            {
                int lv = p.HideoutLevel(m.id);
                for (int i = 0; i < lv && i < m.levels.Count; i++) foreach (var kv in m.levels[i].bonus) Add(kv.Key, kv.Value);
            }
            // экипировка: штраф скорости от брони
            var armor = p.loadout.eq.Get("armor");
            if (armor?.Info?.item != null) Add("sprintSpeed", armor.Info.item.P("speed"));
            var rig = p.loadout.eq.Get("rig");
            if (rig?.Info?.item != null && rig.Info.item.P("armorClass") > 0) Add("sprintSpeed", -0.03f);
            Version++;
        }

        /// <summary>Модификатор для конкретного класса оружия + общий.</summary>
        public static float WeaponMul(string key, string cls, float min = 0.1f) => Mathf.Max(min, 1f + Get(key) + Get(key + "_" + cls));
        public static float WeaponFlat(string key, string cls) => Get(key + "_" + cls);
    }
}
