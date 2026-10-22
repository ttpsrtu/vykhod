using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Vykhod
{
    /// <summary>Шум в мире (выстрелы, шаги) — для слуха ботов. Только на сервере.</summary>
    public static class Noise
    {
        struct N { public Vector3 pos; public float radius, time; }
        static readonly List<N> list = new List<N>();
        public static void Emit(Vector3 pos, float radius) { list.Add(new N { pos = pos, radius = radius, time = Time.time }); if (list.Count > 64) list.RemoveAt(0); }
        public static bool Heard(Vector3 listener, float since, float hearMul, out Vector3 where)
        {
            where = Vector3.zero; bool any = false; float best = float.MaxValue;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].time < since) break;
                float d = Vector3.Distance(listener, list[i].pos);
                if (d < list[i].radius * hearMul && d < best) { best = d; where = list[i].pos; any = true; }
            }
            return any;
        }
    }

    /// <summary>Бот: серверный ИИ (NavMesh, восприятие, стрельба, укрытие, бегство) + клиентская визуализация.</summary>
    public class EnemyAI : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> DefId = new NetworkVariable<FixedString32Bytes>();
        public NetworkVariable<FixedString32Bytes> WeaponId = new NetworkVariable<FixedString32Bytes>();
        public NetworkVariable<float> Hp = new NetworkVariable<float>();
        public NetworkVariable<bool> Dead = new NetworkVariable<bool>();
        public NetworkVariable<float> Yaw = new NetworkVariable<float>();
        public EnemyDef Def; public WeaponDef WDef; public AmmoDef AmmoD; public WeaponComputed WC;
        public bool IsSniperPost;

        NavMeshAgent agent; Transform eye; GameObject body; Transform gunT; Animator anim; Vector3 lastPos;
        enum S { Patrol, Investigate, Combat, Flee }
        S state = S.Patrol;
        PlayerNet target; Vector3 lastKnown, home; float lastSeen = -99, nextPerception, nextShot, burstLeft, burstPause, reloadEnd, nextRepath, stateTime, fleeUntil, detectTimer, idleUntil;
        int magLeft; float maxHp; bool ready;
        static readonly string[] parts = { "head", "thorax", "stomach", "leftarm", "rightarm", "leftleg", "rightleg" };
        static readonly float[] partW = { 0.1f, 0.34f, 0.18f, 0.09f, 0.09f, 0.1f, 0.1f };

        public override void OnNetworkSpawn()
        {
            Def = Database.Enemy(DefId.Value.ToString()) ?? Database.Enemies.Values.First();
            WDef = Database.Weapon(WeaponId.Value.ToString()) ?? Database.Weapon("pm");
            BuildVisual();
            Dead.OnValueChanged += (a, b) => { if (b) OnDeadVisual(); };
            if (IsServer) ServerInit();
            else if (Dead.Value) OnDeadVisual();
        }

        void ServerInit()
        {
            maxHp = Def.hp; Hp.Value = maxHp;
            var w = new ItemInstance(WDef.id);
            if (!WDef.IsMelee)
            {
                var ammo = Database.AmmoFor(WDef.caliber); AmmoD = ammo.Count > 0 ? ammo[Def.boss ? Mathf.Min(1, ammo.Count - 1) : 0] : null;
                if (Def.boss) foreach (var slot in WDef.slots) if (Random.value < 0.5f) { var ms = Database.ModsFor(WDef, slot); if (ms.Count > 0) w.mods[slot] = ms[Random.Range(0, ms.Count)].id; }
            }
            WC = WeaponStats.Compute(w) ?? new WeaponComputed { def = WDef, rpm = 500, magCap = 10, moa = 5 };
            magLeft = WC.magCap;
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.speed = Def.speed; agent.acceleration = 12; agent.angularSpeed = 360; agent.radius = 0.4f; agent.height = 1.8f; agent.stoppingDistance = 1.2f; agent.avoidancePriority = Random.Range(30, 70);
            if (NavMesh.SamplePosition(transform.position, out var hit, 8f, NavMesh.AllAreas)) agent.Warp(hit.position); else agent.enabled = false;
            home = transform.position;
            idleUntil = Time.time + Random.Range(0f, 4f);
            ready = true;
        }

        void BuildVisual()
        {
            body = new GameObject("body"); body.transform.SetParent(transform, false);
            Color c = Def.boss ? new Color(0.6f, 0.1f, 0.1f) : Def.id == "raider" ? new Color(0.2f, 0.25f, 0.2f) : Def.id == "cultist" ? new Color(0.1f, 0.05f, 0.15f) : Def.sniper ? new Color(0.3f, 0.35f, 0.25f) : new Color(0.45f, 0.4f, 0.3f);
            // люди: анимированные персонажи Quaternius (солдаты/рабочие/гражданские по типу бота)
            string who = Def.boss ? "BlueSoldier_Male" : Def.id == "raider" ? "Soldier_Male" : Def.id == "cultist" ? "Ninja_Male" : Def.sniper ? "Soldier_Female" : (Random.value < 0.5f ? "Worker_Male" : "Casual_Male");
            var path = AssetCatalog.FindModel(who, "quaternius_characters") ?? AssetCatalog.FindModel("Soldier", "quaternius_characters") ?? AssetCatalog.FindModel("character", "character");
            GameObject model = path != null ? AssetCatalog.Spawn(path, body.transform, 1.8f, false) : null;
            if (model != null)
            {
                var mb = AssetCatalog.MeshBounds(model); model.transform.position += Vector3.up * (transform.position.y - mb.min.y); model.transform.localRotation = Quaternion.identity;
                anim = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                var ctrl = Resources.Load<RuntimeAnimatorController>("Anim/Soldier"); if (ctrl != null) anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                if (Def.boss) foreach (var r in model.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) m.color = Color.Lerp(m.color, c, 0.35f);
            }
            else
            {
                var torso = AssetCatalog.Prim(PrimitiveType.Capsule, body.transform, new Vector3(0, 1.0f, 0), new Vector3(0.6f, 0.62f, 0.6f), c, false);
                var head = AssetCatalog.Prim(PrimitiveType.Sphere, body.transform, new Vector3(0, 1.62f, 0), new Vector3(0.32f, 0.32f, 0.32f), new Color(0.85f, 0.7f, 0.6f), false);
                if (Def.boss) AssetCatalog.Prim(PrimitiveType.Cube, body.transform, new Vector3(0, 1.85f, 0), new Vector3(0.36f, 0.14f, 0.36f), new Color(0.9f, 0.8f, 0.2f), false);
                var legs = AssetCatalog.Prim(PrimitiveType.Cube, body.transform, new Vector3(0, 0.4f, 0), new Vector3(0.45f, 0.8f, 0.35f), c * 0.7f, false);
            }
            // хитбоксы
            Hit("head", new Vector3(0, 1.62f, 0), 0.2f, 2.3f); Hit("thorax", new Vector3(0, 1.15f, 0), 0.32f, 1f); Hit("stomach", new Vector3(0, 0.78f, 0), 0.3f, 1.1f); Hit("legs", new Vector3(0, 0.35f, 0), 0.28f, 0.65f);
            eye = new GameObject("eye").transform; eye.SetParent(transform, false); eye.localPosition = new Vector3(0, 1.6f, 0.1f);
            gunT = new GameObject("gun").transform; gunT.SetParent(eye, false); gunT.localPosition = new Vector3(0.25f, -0.25f, 0.3f);
            var gp = AssetCatalog.GunModel(WDef);
            var g = gp != null ? AssetCatalog.Spawn(gp, gunT, WDef.cls == "pistol" ? 0.25f : 0.8f, false, false, WDef.modelVariant) : AssetCatalog.GunPlaceholder(WDef, gunT);
            if (gp != null) { AssetCatalog.OrientGun(g); AssetCatalog.Recenter(g); }
            gameObject.layer = 0;
        }

        void Hit(string part, Vector3 pos, float r, float mult)
        {
            var go = new GameObject("hb_" + part); go.transform.SetParent(transform, false); go.transform.localPosition = pos;
            var sc = go.AddComponent<SphereCollider>(); sc.radius = r; sc.isTrigger = false;
            if (part == "thorax" || part == "stomach" || part == "legs") { var cc = go.AddComponent<CapsuleCollider>(); cc.radius = r; cc.height = part == "legs" ? 0.9f : 0.5f; Destroy(sc); }
            var hb = go.AddComponent<Hitbox>(); hb.part = part; hb.mult = mult; hb.owner = this;
        }

        // ------------------------------------------------------------ серверная логика
        void Update()
        {
            if (anim != null && !Dead.Value)
            {
                float sp = Time.deltaTime > 0 ? (transform.position - lastPos).magnitude / Time.deltaTime : 0; lastPos = transform.position;
                anim.SetFloat("Speed", Mathf.Clamp(sp, 0, 6));
            }
            if (!IsServer || !ready || Dead.Value) return;
            if (agent == null || !agent.enabled) { if (NavMesh.SamplePosition(transform.position, out var h, 10f, NavMesh.AllAreas)) { agent.enabled = true; agent.Warp(h.position); } else return; }
            Yaw.Value = transform.eulerAngles.y;
            if (Time.time >= nextPerception) { nextPerception = Time.time + 0.25f; Perceive(); }
            stateTime += Time.deltaTime;
            switch (state)
            {
                case S.Patrol: Patrol(); break;
                case S.Investigate: Investigate(); break;
                case S.Combat: Combat(); break;
                case S.Flee: Flee(); break;
            }
            if (reloadEnd > 0 && Time.time >= reloadEnd) { reloadEnd = 0; magLeft = WC.magCap; }
        }

        float SightRange => (Def.sniper ? 220f : Def.boss ? 110f : 85f) * (World.Night || World.Underground ? 0.45f : 1f);

        void Perceive()
        {
            PlayerNet best = null; float bestD = float.MaxValue;
            foreach (var p in Game.Manager.Net.Players.Values)
            {
                if (p == null || p.Dead.Value || p.Extracted.Value) continue;
                float d = Vector3.Distance(eye.position, p.transform.position);
                float range = SightRange * (1f - p.Stealth.Value) * (p.Crouching.Value ? 0.75f : 1f);
                if (p.Flashlight.Value && (World.Night || World.Underground)) range *= 2.5f;
                if (d > range) continue;
                var dir = (p.transform.position + Vector3.up * 1.2f - eye.position);
                float ang = Vector3.Angle(transform.forward, dir);
                if (ang > 70 && d > 5) continue;
                if (Physics.Raycast(eye.position + dir.normalized * 0.6f, dir.normalized, out var hit, d) && hit.collider.GetComponentInParent<PlayerNet>() != p) continue;
                if (d < bestD) { bestD = d; best = p; }
            }
            if (best != null)
            {
                detectTimer += 0.25f;
                float need = Def.reaction * (1f + best.Stealth.Value * 2f) * (state == S.Combat ? 0f : 1f);
                if (detectTimer >= need)
                {
                    if (state != S.Combat) { state = S.Combat; stateTime = 0; Noise.Emit(transform.position, 25f); }
                    target = best; lastKnown = best.transform.position; lastSeen = Time.time;
                    if (best.IsOwner == false && best.IsSpawned) best.SpottedRpc(RpcTarget.Single(best.OwnerClientId, RpcTargetUse.Temp));
                }
            }
            else detectTimer = Mathf.Max(0, detectTimer - 0.1f);
            // слух
            if (state != S.Combat && Noise.Heard(transform.position, Time.time - 0.3f, Def.sniper ? 0.8f : 1.2f, out var where))
            {
                if (state != S.Investigate || Random.value < 0.3f) { state = S.Investigate; stateTime = 0; lastKnown = where; }
            }
            if (!Def.boss && !Def.sniper && Hp.Value < maxHp * 0.25f && state == S.Combat && Random.value < 0.4f) { state = S.Flee; fleeUntil = Time.time + 6f; }
        }

        void SetDest(Vector3 p)
        {
            if (Time.time < nextRepath) return; nextRepath = Time.time + 0.4f;
            if (NavMesh.SamplePosition(p, out var h, 4f, NavMesh.AllAreas)) agent.SetDestination(h.position);
        }

        void Patrol()
        {
            if (IsSniperPost) { agent.isStopped = true; return; }
            agent.isStopped = Time.time < idleUntil;
            agent.speed = Def.speed * 0.55f;
            if (!agent.pathPending && (agent.remainingDistance < 1.5f || !agent.hasPath) && Time.time >= idleUntil)
            {
                idleUntil = Time.time + Random.Range(1f, 5f);
                var r = Random.insideUnitCircle * 35f; SetDest(home + new Vector3(r.x, 0, r.y));
            }
        }

        void Investigate()
        {
            agent.isStopped = false; agent.speed = Def.speed * 0.8f;
            SetDest(lastKnown);
            if ((!agent.pathPending && agent.remainingDistance < 2f) || stateTime > 20f) { state = S.Patrol; stateTime = 0; idleUntil = Time.time + 2f; }
        }

        void Combat()
        {
            if (target == null || target.Dead.Value || target.Extracted.Value) { target = null; state = S.Investigate; stateTime = 0; return; }
            float d = Vector3.Distance(transform.position, target.transform.position);
            bool visible = Time.time - lastSeen < 0.6f;
            if (Time.time - lastSeen > 4f) { state = S.Investigate; stateTime = 0; return; }
            // лицом к цели
            var look = target.transform.position - transform.position; look.y = 0;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 540 * Time.deltaTime);
            float prefer = WDef.IsMelee ? 1.5f : Def.sniper ? 90f : WDef.cls == "shotgun" || WDef.cls == "smg" || WDef.cls == "pistol" ? 12f : 25f;
            agent.speed = Def.speed;
            if (IsSniperPost) agent.isStopped = true;
            else if (d > prefer * 1.3f || !visible) { agent.isStopped = false; SetDest(lastKnown); }
            else if (d < prefer * 0.5f && !WDef.IsMelee) { agent.isStopped = false; SetDest(transform.position - look.normalized * 6f); }
            else { agent.isStopped = Random.value < 0.9f; if (!agent.isStopped) SetDest(transform.position + Vector3.Cross(look.normalized, Vector3.up) * (Random.value < 0.5f ? 4 : -4)); }
            if (visible) Shoot(d);
        }

        void Flee()
        {
            agent.isStopped = false; agent.speed = Def.speed * 1.1f;
            if (target != null) { var away = (transform.position - target.transform.position).normalized; SetDest(transform.position + away * 20f); }
            if (Time.time > fleeUntil) { state = S.Combat; stateTime = 0; }
        }

        void Shoot(float dist)
        {
            if (WDef.IsMelee) { if (dist < 2.2f && Time.time >= nextShot) { nextShot = Time.time + 1f; HitPlayer(target, WDef.meleeDmg, 10, "thorax", dist); } return; }
            if (reloadEnd > 0) return;
            if (magLeft <= 0) { reloadEnd = Time.time + 2.5f; return; }
            if (burstPause > Time.time) return;
            if (Time.time < nextShot) return;
            float interval = Mathf.Max(60f / Mathf.Min(WC.rpm, WC.auto ? 650f : 300f), 0.09f);
            if (WC.auto) { if (burstLeft <= 0) burstLeft = Random.Range(2, 6); }
            else interval = Mathf.Max(interval, Def.sniper ? 1.6f : 0.35f);
            nextShot = Time.time + interval;
            magLeft--;
            if (WC.auto) { burstLeft--; if (burstLeft <= 0) burstPause = Time.time + Random.Range(0.4f, 1.2f); }
            // точность
            float acc = Def.accuracy * Mathf.Clamp01(1.15f - dist / (Def.sniper ? 260f : 120f));
            if (target.Moving.Value) acc *= 0.7f; if (target.Crouching.Value) acc *= 0.85f;
            if (World.Night && !World.Underground) acc *= 0.8f;
            bool hit = Random.value < acc;
            var origin = gunT.position; var dir = (target.transform.position + Vector3.up * (hit ? 1.1f : Random.Range(0.3f, 2.2f)) - origin).normalized;
            if (!hit) dir = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-6f, 6f), 0) * dir;
            // проверка перекрытия (стены)
            if (Physics.Raycast(origin + dir * 0.6f, dir, out var h, dist + 1f) && h.collider.GetComponentInParent<PlayerNet>() != target) hit = false;
            ShotFxRpc(origin, dir, WC.suppressed);
            Noise.Emit(origin, WC.suppressed ? 25f : 110f);
            if (hit)
            {
                float r = Random.value, acc2 = 0; string part = "thorax";
                for (int i = 0; i < parts.Length; i++) { acc2 += partW[i]; if (r <= acc2) { part = parts[i]; break; } }
                float dmg = AmmoD?.dmg ?? 30; if (AmmoD != null && AmmoD.pellets > 1) dmg *= Mathf.Clamp(AmmoD.pellets * Mathf.Clamp01(1.2f - dist / 30f), 1, AmmoD.pellets) * 0.6f;
                HitPlayer(target, dmg, AmmoD?.pen ?? 10, part, dist);
            }
        }

        void HitPlayer(PlayerNet p, float dmg, float pen, string part, float dist)
        {
            p.ServerDamage(part, dmg, pen, Def.name, WDef.name, 0, false);
        }

        [Rpc(SendTo.Everyone)]
        void ShotFxRpc(Vector3 origin, Vector3 dir, bool suppressed)
        {
            Fx.Tracer(origin, dir, 120f, new Color(1f, 0.8f, 0.4f));
            Fx.Gunshot(origin, WDef.cls, suppressed, 0.8f);
            if (gunT != null) Fx.MuzzleFlash(gunT);
            if (anim != null) anim.SetTrigger("Shoot");
        }

        /// <summary>Попадание от игрока (клиент → сервер).</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void HitRpc(string part, float dmg, float pen, string weaponId, float dist, bool headshot, bool suppressed, bool grenade, int mods, RpcParams p = default)
        {
            if (Dead.Value) return;
            float mult = part == "head" ? 2.3f : part == "legs" ? 0.65f : part == "stomach" ? 1.1f : 1f;
            float armor = Def.armorClass * 10f;
            if (armor > 0 && (part == "thorax" || part == "stomach" || (part == "head" && Def.armorClass >= 4))) dmg *= Mathf.Clamp(pen / armor, 0.15f, 1f) * (pen >= armor ? 1f : 0.9f);
            Hp.Value -= dmg * mult;
            if (state != S.Combat) { state = S.Combat; stateTime = 0; lastSeen = Time.time; var atk = Game.Manager.Net.Players.TryGetValue(p.Receive.SenderClientId, out var pl) ? pl : null; if (atk != null) { target = atk; lastKnown = atk.transform.position; } }
            if (Hp.Value <= 0) Die(p.Receive.SenderClientId, weaponId, dist, headshot, suppressed, grenade, mods);
        }

        void Die(ulong killer, string weaponId, float dist, bool headshot, bool suppressed, bool grenade, int mods)
        {
            Dead.Value = true;
            if (agent != null) agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Game.Manager.Net.Session?.ReportKillRpc(Def.id, Def.boss);
            var wd = Database.Weapon(weaponId);
            var ki = new KillInfo { enemyId = Def.id, map = World.Map.id, weaponId = weaponId, cls = wd?.cls ?? "", family = wd?.family ?? "", headshot = headshot, suppressed = suppressed, grenade = grenade, dist = dist, mods = mods, boss = Def.boss };
            string json = Json.Serialize(new Dictionary<string, object> { ["enemyId"] = ki.enemyId, ["map"] = ki.map, ["weaponId"] = ki.weaponId, ["cls"] = ki.cls, ["family"] = ki.family, ["headshot"] = headshot, ["suppressed"] = suppressed, ["grenade"] = grenade, ["dist"] = dist, ["mods"] = mods, ["boss"] = Def.boss, ["xp"] = Def.xp, ["name"] = Def.name });
            KilledRpc(json, RpcTarget.Single(killer, RpcTargetUse.Temp));
            Game.Manager.Net.Session?.KillFeedRpc(Game.Manager.Net.NameOf(killer), Def.name, wd?.name ?? weaponId);
            DeathRpc(transform.position, transform.eulerAngles.y, Random.Range(1, 1 << 30));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void KilledRpc(string json, RpcParams p)
        {
            var d = J.D(Json.Parse(json));
            var ki = new KillInfo { enemyId = J.S(d, "enemyId"), map = J.S(d, "map"), weaponId = J.S(d, "weaponId"), cls = J.S(d, "cls"), family = J.S(d, "family"), headshot = J.B(d, "headshot"), suppressed = J.B(d, "suppressed"), grenade = J.B(d, "grenade"), dist = J.F(d, "dist"), mods = J.I(d, "mods"), boss = J.B(d, "boss") };
            int xp = J.I(d, "xp", 50);
            var r = Game.Manager.Raid; r.kills++; if (ki.headshot) r.headshots++; if (ki.boss) r.bossKills++; r.xp += xp + (ki.headshot ? 20 : 0) + (int)(ki.dist / 5);
            var prof = Game.Profile;
            prof.mastery[ki.weaponId] = (prof.mastery.TryGetValue(ki.weaponId, out var m) ? m : 0) + 1;
            string skill = ki.cls == "pistol" ? "pistols" : ki.cls == "smg" ? "smg" : ki.cls == "ar" ? "ar" : ki.cls == "shotgun" ? "shotguns" : ki.cls == "dmr" ? "dmr" : ki.cls == "sniper" ? "sniper" : ki.cls == "lmg" ? "lmg" : ki.cls == "melee" ? "melee" : "";
            if (skill != "") prof.AddSkillXp(skill, 25 + (ki.headshot ? 10 : 0));
            if (ki.grenade) prof.AddSkillXp("throwables", 40);
            prof.AddSkillXp("stress", 8); prof.AddSkillXp("perception", ki.dist > 60 ? 15 : 4);
            QuestSystem.OnKill(ki);
            Game.Notify($"Убит: {J.S(d, "name")} (+{xp} XP)");
        }

        [Rpc(SendTo.Everyone)]
        void DeathRpc(Vector3 pos, float yaw, int seed)
        {
            // труп = контейнер с лутом бота (детерминированный порядок на всех клиентах)
            var c = World.AddContainer(pos, Quaternion.Euler(0, yaw, 0), "body");
            c.Title = Def.name; c.gameObject.AddComponent<BodyLoot>().Init(Def.id, seed);
            OnDeadVisual();
        }

        void OnDeadVisual()
        {
            if (anim != null) anim.SetBool("Dead", true);
            else if (body != null) { body.transform.localRotation = Quaternion.Euler(-90, 0, 0); body.transform.localPosition = new Vector3(0, 0.3f, -0.6f); }
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (IsServer) StartCoroutine(DespawnLater());
        }

        System.Collections.IEnumerator DespawnLater() { yield return new WaitForSeconds(anim != null ? 12f : 3f); if (IsSpawned) NetworkObject.Despawn(true); }
    }
}
