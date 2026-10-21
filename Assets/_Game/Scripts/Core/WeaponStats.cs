using System.Collections.Generic;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Итоговые характеристики оружия с учётом модов, навыков, перков и мастерства.</summary>
    public class WeaponComputed
    {
        public WeaponDef def;
        public float recoilV, recoilH, ergo, moa, rpm, zoom = 1, velocityMul = 1, dmgMul = 1, reloadTime, adsTime, swapTime, hipSpread, loud = 1, weight, night;
        public int magCap;
        public bool suppressed, laser, light, auto, burst, single;
        public float ShotInterval => 60f / Mathf.Max(30f, rpm);
    }

    public static class WeaponStats
    {
        static readonly Dictionary<string, float> baseReload = new Dictionary<string, float>
        { ["pistol"] = 1.6f, ["smg"] = 2.2f, ["ar"] = 2.6f, ["dmr"] = 2.9f, ["sniper"] = 3.4f, ["shotgun"] = 3.0f, ["lmg"] = 5.5f, ["melee"] = 0 };

        public static WeaponComputed Compute(ItemInstance w)
        {
            var def = Database.Weapon(w?.id);
            if (def == null) return null;
            var c = new WeaponComputed { def = def };
            float recoilMod = 0, ergoMod = 0, moaMod = 0, capMod = 0, reloadMod = 0, velMod = 0, dmgMod = 0, wMod = 0;
            foreach (var kv in w.mods)
            {
                var m = Database.Mod(kv.Value); if (m == null) continue;
                recoilMod += m.Stat("recoil"); ergoMod += m.Stat("ergo") * Stats.Mul("modErgo"); moaMod += m.Stat("moa");
                capMod += m.Stat("capacity"); reloadMod += m.Stat("reload"); velMod += m.Stat("velocity"); dmgMod += m.Stat("dmg"); wMod += m.Stat("weight");
                if (m.Stat("zoom") > c.zoom) c.zoom = m.Stat("zoom");
                if (m.Stat("suppressed") > 0) { c.suppressed = true; c.loud = Mathf.Min(c.loud, 1 + m.Stat("loud")); }
                if (m.Stat("laser") > 0) c.laser = true;
                if (m.Stat("light") > 0) c.light = true;
                c.night = Mathf.Max(c.night, m.Stat("night"));
            }
            string cls = def.cls;
            int mastery = Game.Profile?.MasteryLevel(def.id) ?? 0;
            float masteryRecoil = 1f - 0.04f * mastery;
            float recoilMul = Mathf.Max(0.15f, 1 + recoilMod) * Stats.WeaponMul("recoil", cls) * masteryRecoil;
            if (Stats.Has("recoil_m50")) recoilMul *= 0.85f;
            c.recoilV = def.recoilV * recoilMul;
            c.recoilH = def.recoilH * recoilMul * (Stats.Has("recoil_m10") ? 0.9f : 1f);
            c.ergo = Mathf.Clamp(def.ergo + ergoMod + Stats.WeaponFlat("ergo", cls), 5, 100);
            c.moa = Mathf.Max(0.1f, def.moa * Mathf.Max(0.3f, 1 + moaMod));
            c.rpm = def.rpm * Stats.Mul("rpm");
            c.magCap = Mathf.Max(1, def.magCap + Mathf.RoundToInt(capMod));
            c.velocityMul = 1 + velMod;
            c.dmgMul = (1 + dmgMod) * Stats.Mul("damage");
            float rb = baseReload.TryGetValue(cls, out var r) ? r : 2.5f;
            c.reloadTime = rb * Mathf.Max(0.3f, 1 + reloadMod) * Stats.WeaponMul("reload", cls) * Stats.Mul("reload") * (1f - 0.03f * mastery) * (Stats.Has(cls + "_m10") ? 0.9f : 1f);
            c.adsTime = (0.18f + (100 - c.ergo) / 350f) * Stats.WeaponMul("ads", cls) * (Stats.Has(cls + "_m20") ? 0.85f : 1f);
            c.swapTime = (0.35f + (100 - c.ergo) / 250f) / Stats.Mul("swapSpeed");
            c.hipSpread = c.moa * 4f * Stats.Mul("hipSpread") * (Stats.Has(cls + "_m30") ? 0.8f : 1f);
            c.weight = def.weight + wMod;
            c.auto = def.modes.Contains("auto"); c.burst = def.modes.Contains("burst"); c.single = def.modes.Contains("single");
            return c;
        }

        /// <summary>Урон патрона с учётом оружия и перков.</summary>
        public static float Damage(WeaponComputed c, AmmoDef ammo, bool lowHp)
        {
            float d = (ammo?.dmg ?? 30) * c.dmgMul;
            if (lowHp) d *= Stats.Mul("lowHpDamage");
            return d;
        }
    }
}
