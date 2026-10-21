using UnityEngine;

namespace Vykhod
{
    /// <summary>Зона экстракции.</summary>
    public class ExtractZone : MonoBehaviour
    {
        public ExtractDef Def;
        public float Progress;
        public bool LocalInside;
        float needTime => Mathf.Max(2f, 9f * Stats.Mul("extractTime", 0.2f));

        public string Condition()
        {
            if (string.IsNullOrEmpty(Def.cond)) return "";
            if (Def.cond == "coop") return "нужны двое из отряда";
            if (Def.cond.StartsWith("money:")) return "платный: " + Def.cond.Substring(6) + " ₽";
            if (Def.cond.StartsWith("key_")) return "нужен ключ: " + Database.NameOf(Def.cond);
            return Def.cond;
        }

        public bool CanUse(out string why)
        {
            why = "";
            if (string.IsNullOrEmpty(Def.cond)) return true;
            var lo = Game.Manager.RaidLoadout;
            if (Def.cond == "coop")
            {
                int n = 0; var lp = Game.Manager.Net.LocalPlayer;
                foreach (var p in Game.Manager.Net.Players.Values) if (!p.Dead.Value && Game.Manager.Net.SameSquad(p, lp) && Vector3.Distance(p.transform.position, transform.position) < 6f) n++;
                if (n < 2) { why = "нужны двое из отряда в зоне"; return false; }
                return true;
            }
            if (Def.cond.StartsWith("money:")) { long m = long.Parse(Def.cond.Substring(6)); if (Game.Profile.money < m) { why = $"нужно {m} ₽ на счету"; return false; } return true; }
            if (Def.cond.StartsWith("key_")) { if (!lo.HasKey(Def.cond)) { why = "нужен ключ: " + Database.NameOf(Def.cond); return false; } return true; }
            return true;
        }

        void Update()
        {
            if (Game.State != GameState.Raid || Game.Manager.RaidFinishing) return;
            var lp = Game.Manager.Net.LocalPlayer; if (lp == null || lp.Dead.Value || lp.Extracted.Value) { Progress = 0; return; }
            var d = lp.transform.position - transform.position; d.y = 0;
            LocalInside = d.magnitude < 5f;
            if (!LocalInside) { Progress = 0; return; }
            if (!CanUse(out var why)) { Progress = 0; Game.Manager.UI.SetHint($"Выход «{Def.name}»: {why}"); return; }
            Progress += Time.deltaTime;
            Game.Manager.UI.SetHint($"Выход «{Def.name}»: {Mathf.CeilToInt(needTime - Progress)} с");
            if (Progress >= needTime)
            {
                if (Def.cond.StartsWith("money:")) Game.Profile.money -= long.Parse(Def.cond.Substring(6));
                lp.Extracted.Value = true;
                Game.Manager.Net.Session?.AnnounceRpc($"{Game.Profile.name} вышел через «{Def.name}»");
                Game.Manager.FinishRaid(true, Def.name);
            }
        }
    }
}
