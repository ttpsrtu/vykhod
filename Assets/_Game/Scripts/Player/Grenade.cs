using UnityEngine;

namespace Vykhod
{
    /// <summary>Граната: таймер, взрыв с проверкой видимости, вспышка/дым.</summary>
    public class Grenade : MonoBehaviour
    {
        ItemDef def; PlayerNet owner; float t;
        public void Init(ItemDef d, PlayerNet o) { def = d; owner = o; t = Time.time + d.P("fuse", 3.5f); }
        void Update()
        {
            if (Time.time < t) return;
            Destroy(gameObject);
            var pos = transform.position; float radius = def.P("radius", 6) * Stats.Mul("throwAccuracy"); string kind = def.PS("kind", "frag");
            if (kind == "smoke") { Fx.Smoke(pos, radius, 20f); return; }
            Fx.Explosion(pos, radius); owner.ExplodeRpc(pos, radius);
            float dmg = def.P("dmg", 100) * Stats.Mul("grenadeDamage");
            foreach (var c in Physics.OverlapSphere(pos, radius))
            {
                var hb = c.GetComponent<Hitbox>(); if (hb == null) continue;
                float d = Vector3.Distance(pos, c.transform.position); float f = Mathf.Clamp01(1f - d / radius);
                if (Physics.Raycast(pos, (c.transform.position - pos).normalized, out var h, d) && h.collider != c) continue;
                if (kind == "flash") { if (hb.owner is PlayerNet pf && pf.IsOwner) Game.Manager.UI.Flash(3f * f); continue; }
                if (hb.owner is EnemyAI ai && !ai.Dead.Value && hb.part == "thorax") ai.HitRpc("thorax", dmg * f, 30, def.id, d, false, false, true, 0);
                else if (hb.owner is PlayerNet pn && hb.part == "thorax") { if (pn == owner) pn.SelfDamageRpc("thorax", dmg * f * 0.8f); else if (!Game.Manager.Net.SameSquad(owner, pn)) pn.HitRpc("thorax", dmg * f, 30, def.name); }
            }
            if (kind == "flash" && Vector3.Distance(pos, owner.transform.position) < radius) Game.Manager.UI.Flash(2f);
        }
    }
}
