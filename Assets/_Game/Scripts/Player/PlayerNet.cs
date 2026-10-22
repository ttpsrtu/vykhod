using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Сетевая сущность игрока: здоровье (сервер), состояние (владелец), урон/лечение через RPC, вид от третьего лица для других.</summary>
    public class PlayerNet : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<FixedString32Bytes> Squad = new NetworkVariable<FixedString32Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<FixedString32Bytes> WeaponId = new NetworkVariable<FixedString32Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<BodyHealth> Health = new NetworkVariable<BodyHealth>();
        public NetworkVariable<bool> Dead = new NetworkVariable<bool>();
        public NetworkVariable<bool> Extracted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> Crouching = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> Moving = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> Flashlight = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> Stealth = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> MaxHpMul = new NetworkVariable<float>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> BleedResist = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<int> ArmorClass = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<int> HelmetClass = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<int> Level = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public FPSController Ctrl; public PlayerWeapons Weapons; public PlayerInteract Interact; public Camera Cam;
        public Transform Head;
        GameObject body; Transform remoteGun; Light remoteLight; string shownWeapon = ""; Animator bodyAnim; Vector3 lastBodyPos;
        float serverTick; bool initDone; float painUntil; public bool LastStandUsed, SecondWindUsed;
        public bool IsAlive => !Dead.Value && !Extracted.Value;
        public float Hp => Health.Value.Total;
        public float MaxHp => Health.Value.MaxTotal(MaxHpMul.Value);
        public bool InPain => Time.time < painUntil;

        public override void OnNetworkSpawn()
        {
            Head = transform.Find("Head"); if (Head == null) { Head = new GameObject("Head").transform; Head.SetParent(transform, false); Head.localPosition = new Vector3(0, 1.65f, 0); }
            Ctrl = GetComponent<FPSController>() ?? gameObject.AddComponent<FPSController>();
            Weapons = GetComponent<PlayerWeapons>() ?? gameObject.AddComponent<PlayerWeapons>();
            Interact = GetComponent<PlayerInteract>() ?? gameObject.AddComponent<PlayerInteract>();
            Ctrl.enabled = false; Weapons.enabled = false; Interact.enabled = false;
            if (IsOwner)
            {
                PlayerName.Value = Game.Profile?.name ?? Game.Settings.playerName;
                Squad.Value = Game.Manager.Net.Squad;
                MaxHpMul.Value = Stats.Mul("maxHealth"); BleedResist.Value = Mathf.Clamp01(-Stats.Get("bleedChance"));
                Level.Value = Game.Profile?.level ?? 1;
                UpdateGearVars();
                Stealth.Value = Mathf.Clamp01(-Stats.Get("aiDetect") - Stats.Get("noise") * 0.5f);
                InitHpRpc();
            }
            BuildBody();
            Health.OnValueChanged += OnHealthChanged;
            Dead.OnValueChanged += (a, b) => { if (b) OnDied(); };
            Game.Manager.Net.RegisterPlayer(this);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && Cam != null) Destroy(Cam.gameObject);
            Game.Manager.Net.Players.Remove(OwnerClientId);
        }

        public void UpdateGearVars()
        {
            var lo = Game.Manager.RaidLoadout; if (lo == null) return;
            ArmorClass.Value = (int)(lo.eq.Get("armor")?.Info?.item?.P("armorClass") ?? 0);
            var rig = lo.eq.Get("rig"); if (rig?.Info?.item != null && rig.Info.item.P("armorClass") > ArmorClass.Value) ArmorClass.Value = (int)rig.Info.item.P("armorClass");
            HelmetClass.Value = (int)(lo.eq.Get("helmet")?.Info?.item?.P("armorClass") ?? 0);
        }

        [Rpc(SendTo.Server)]
        void InitHpRpc() { Health.Value = BodyHealth.Full(MaxHpMul.Value); initDone = true; }

        /// <summary>Мир построен — включаем управление и камеру.</summary>
        public void OnWorldReady()
        {
            if (!IsOwner) return;
            Game.Manager.SetSceneCameras(false);
            var cgo = new GameObject("PlayerCamera"); cgo.transform.SetParent(Head, false);
            Cam = cgo.AddComponent<Camera>(); Cam.nearClipPlane = 0.04f; Cam.farClipPlane = 1200f; Cam.fieldOfView = Game.Settings.fov; Cam.backgroundColor = World.SkyColor;
            Cam.clearFlags = RenderSettings.skybox != null && !World.Underground ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            Cam.allowHDR = true; Cam.allowMSAA = true;
            Cam.tag = "MainCamera";
            var rootListener = Game.Manager.GetComponent<AudioListener>(); if (rootListener != null) rootListener.enabled = false;
            cgo.AddComponent<AudioListener>();
            var fl = new GameObject("Flashlight").AddComponent<Light>(); fl.transform.SetParent(cgo.transform, false); fl.type = LightType.Spot; fl.range = 45; fl.spotAngle = 45; fl.intensity = 2.5f; fl.shadows = LightShadows.None; fl.enabled = false; remoteLight = fl;
            Ctrl.enabled = true; Weapons.enabled = true; Interact.enabled = true;
            transform.position = World.Snap(transform.position) + Vector3.up * 0.6f;
            // своё тело не видно от первого лица
            if (body != null) foreach (var r in body.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        void BuildBody()
        {
            body = new GameObject("body"); body.transform.SetParent(transform, false);
            Color c = new Color(0.25f, 0.35f, 0.5f);
            var path = AssetCatalog.FindModel("BlueSoldier_Male", "quaternius_characters") ?? AssetCatalog.FindModel("Soldier", "quaternius_characters");
            var model = path != null ? AssetCatalog.Spawn(path, body.transform, 1.8f, false) : null;
            if (model != null)
            {
                var mb = AssetCatalog.MeshBounds(model); model.transform.position += Vector3.up * (transform.position.y - mb.min.y);
                bodyAnim = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                var ctrl = Resources.Load<RuntimeAnimatorController>("Anim/Soldier"); if (ctrl != null) bodyAnim.runtimeAnimatorController = ctrl; bodyAnim.applyRootMotion = false;
            }
            else
            {
                AssetCatalog.Prim(PrimitiveType.Capsule, body.transform, new Vector3(0, 1.0f, 0), new Vector3(0.6f, 0.62f, 0.6f), c, false);
                AssetCatalog.Prim(PrimitiveType.Sphere, body.transform, new Vector3(0, 1.62f, 0), new Vector3(0.32f, 0.32f, 0.32f), new Color(0.85f, 0.7f, 0.6f), false);
                AssetCatalog.Prim(PrimitiveType.Cube, body.transform, new Vector3(0, 0.4f, 0), new Vector3(0.45f, 0.8f, 0.35f), c * 0.7f, false);
            }
            Hit("head", new Vector3(0, 1.62f, 0), 0.2f); Hit("thorax", new Vector3(0, 1.15f, 0), 0.32f); Hit("stomach", new Vector3(0, 0.78f, 0), 0.3f); Hit("legs", new Vector3(0, 0.35f, 0), 0.28f);
            remoteGun = new GameObject("rgun").transform; remoteGun.SetParent(body.transform, false); remoteGun.localPosition = new Vector3(0.25f, 1.3f, 0.35f);
            gameObject.layer = 0;
        }

        void Hit(string part, Vector3 pos, float r)
        {
            var go = new GameObject("hb_" + part); go.transform.SetParent(transform, false); go.transform.localPosition = pos;
            var sc = go.AddComponent<SphereCollider>(); sc.radius = r; sc.isTrigger = true;
            var hb = go.AddComponent<Hitbox>(); hb.part = part; hb.owner = this;
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (bodyAnim != null && !Dead.Value)
            {
                float sp = Time.deltaTime > 0 ? (transform.position - lastBodyPos).magnitude / Time.deltaTime : 0; lastBodyPos = transform.position;
                bodyAnim.SetFloat("Speed", Mathf.Clamp(sp, 0, 7));
            }
            if (IsOwner)
            {
                string w = Weapons != null && Weapons.Current != null ? Weapons.Current.id : "";
                if (WeaponId.Value.ToString() != w) WeaponId.Value = w;
                if (remoteLight != null) remoteLight.enabled = Flashlight.Value;
                if (Input.GetKeyDown(KeyCode.L) && Ctrl.enabled) Flashlight.Value = !Flashlight.Value;
            }
            else
            {
                // третье лицо: оружие в руках и поворот тела
                string w = WeaponId.Value.ToString();
                if (w != shownWeapon) { shownWeapon = w; foreach (Transform ch in remoteGun) Destroy(ch.gameObject); var def = Database.Weapon(w); if (def != null) { var p = AssetCatalog.FindModel(def.model, "weapon|guns"); var g = p != null ? AssetCatalog.Spawn(p, remoteGun, def.cls == "pistol" ? 0.25f : 0.8f, false, false) : AssetCatalog.GunPlaceholder(def, remoteGun); if (p != null) { AssetCatalog.OrientGun(g); AssetCatalog.Recenter(g); } } }
                if (body != null && bodyAnim == null) body.transform.localScale = new Vector3(1, Crouching.Value ? 0.7f : 1f, 1);
                if (remoteLight == null && Flashlight.Value) { remoteLight = new GameObject("rfl").AddComponent<Light>(); remoteLight.transform.SetParent(remoteGun, false); remoteLight.type = LightType.Spot; remoteLight.range = 40; remoteLight.spotAngle = 45; remoteLight.intensity = 2f; remoteLight.shadows = LightShadows.None; }
                if (remoteLight != null) remoteLight.enabled = Flashlight.Value;
            }
            if (IsServer && initDone && !Dead.Value) ServerTick();
        }

        // ------------------------------------------------------------ сервер: урон, кровотечения, голод
        void ServerTick()
        {
            serverTick += Time.deltaTime; if (serverTick < 1f) return; serverTick = 0;
            var h = Health.Value; bool changed = false;
            for (int i = 0; i < 7; i++)
            {
                if (h.HeavyBleeding(i)) { h.Set(i, Mathf.Max(0, h.Get(i) - 1.6f)); changed = true; }
                else if (h.Bleeding(i)) { h.Set(i, Mathf.Max(0, h.Get(i) - 0.5f)); changed = true; }
            }
            h.energy = Mathf.Max(0, h.energy - 0.045f); h.hydration = Mathf.Max(0, h.hydration - 0.06f);
            if (h.energy <= 0 || h.hydration <= 0) { int w = h.WorstPart(MaxHpMul.Value); h.Set(w, Mathf.Max(0, h.Get(w) - 0.5f)); }
            changed = true;
            // уничтоженная конечность — урон уходит в грудь
            for (int i = 2; i < 7; i++) if (h.Get(i) <= 0 && (h.Bleeding(i) || h.HeavyBleeding(i))) { h.thorax = Mathf.Max(0, h.thorax - 0.4f); }
            if (changed) Health.Value = h;
            if (h.IsDead) Die("Кровопотеря", "", 0, false);
        }

        /// <summary>Урон на сервере. part: head/thorax/stomach/legs/arms/...</summary>
        public void ServerDamage(string part, float dmg, float pen, string attackerName, string weaponName, ulong attackerClient, bool byPlayer)
        {
            if (!IsServer || Dead.Value || Extracted.Value) return;
            int i = BodyHealth.Index(part);
            float prot = i == 0 ? HelmetClass.Value * 10f : (i == 1 || i == 2) ? ArmorClass.Value * 10f : 0;
            bool blocked = false;
            if (prot > 0) { if (pen < prot * 0.6f && Random.value < 0.5f) { dmg *= 0.25f; blocked = true; } else dmg *= Mathf.Clamp(pen / prot, 0.2f, 1f); }
            var h = Health.Value;
            float cur = h.Get(i);
            if (cur <= 0) { h.thorax = Mathf.Max(0, h.thorax - dmg * 0.7f); }
            else
            {
                float left = cur - dmg; h.Set(i, Mathf.Max(0, left));
                if (left < 0) h.thorax = Mathf.Max(0, h.thorax + left * 0.5f);
            }
            if (!blocked && dmg > 8)
            {
                float bleedCh = (dmg > 35 ? 0.35f : 0.2f) * (1f - BleedResist.Value);
                if (Random.value < bleedCh) { if (dmg > 40 && Random.value < 0.4f) h.heavyBleed |= (byte)(1 << i); else h.bleed |= (byte)(1 << i); }
                if (i >= 3 && dmg > 25 && Random.value < 0.18f) h.fracture |= (byte)(1 << i);
            }
            Health.Value = h;
            DamagedRpc(part, dmg, attackerName, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
            if (h.IsDead) Die(attackerName, weaponName, attackerClient, byPlayer);
        }

        void Die(string attackerName, string weaponName, ulong attackerClient, bool byPlayer)
        {
            if (Dead.Value) return;
            Dead.Value = true;
            Game.Manager.Net.Session?.KillFeedRpc(attackerName, PlayerName.Value.ToString(), weaponName);
            DiedRpc(attackerName, weaponName, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
            if (byPlayer && attackerClient != OwnerClientId) PlayerKillRewardRpc(PlayerName.Value.ToString(), RpcTarget.Single(attackerClient, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void DamagedRpc(string part, float dmg, string attacker, RpcParams p)
        {
            Game.Manager.UI?.OnDamaged(part, dmg, attacker);
            Game.Profile?.AddSkillXp("vitality", dmg * 0.3f); Game.Profile?.AddSkillXp("stress", dmg * 0.2f);
            if (!InPain && !Stats.Has("stress_m10")) painUntil = Time.time + 6f;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void DiedRpc(string attacker, string weapon, RpcParams p)
        {
            // перки на выживание: последний рубеж / второе дыхание
            if (Stats.Has("lastStand") && !LastStandUsed) { LastStandUsed = true; ReviveRpc(0.12f); Game.Notify("Последний рубеж!"); return; }
            if (Stats.Has("secondWind") && !SecondWindUsed) { SecondWindUsed = true; ReviveRpc(0.5f); Game.Notify("Второе дыхание!"); return; }
            Game.Manager.UI?.OnDeath(attacker, weapon);
        }

        [Rpc(SendTo.Server)]
        void ReviveRpc(float frac)
        {
            var full = BodyHealth.Full(MaxHpMul.Value); var h = Health.Value;
            for (int i = 0; i < 7; i++) h.Set(i, Mathf.Max(h.Get(i), full.Get(i) * frac));
            h.bleed = 0; h.heavyBleed = 0; Health.Value = h; Dead.Value = false;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void PlayerKillRewardRpc(string victim, RpcParams p)
        {
            Game.Manager.Raid.playerKills++; Game.Manager.Raid.xp += 400;
            Game.Notify($"Вы убили игрока {victim} (+400 XP)");
        }

        void OnDied()
        {
            if (bodyAnim != null) bodyAnim.SetBool("Dead", true);
            else if (body != null) { body.transform.localRotation = Quaternion.Euler(-90, 0, 0); body.transform.localPosition = new Vector3(0, 0.3f, -0.6f); }
            if (IsOwner) { Ctrl.enabled = false; Weapons.enabled = false; Interact.enabled = false; }
        }

        void OnHealthChanged(BodyHealth a, BodyHealth b) { if (IsOwner) Game.Manager.UI?.OnHealthChanged(); if (!b.IsDead && Dead.Value && IsServer) { } if (IsOwner && !b.IsDead && Dead.Value == false && Ctrl != null && !Ctrl.enabled && Game.State == GameState.Raid && !Extracted.Value) { Ctrl.enabled = true; Weapons.enabled = true; Interact.enabled = true; if (body != null) { body.transform.localRotation = Quaternion.identity; body.transform.localPosition = Vector3.zero; } } }

        // ------------------------------------------------------------ RPC от владельца/других
        /// <summary>PvP-попадание от другого игрока.</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void HitRpc(string part, float dmg, float pen, string weaponName, RpcParams p = default)
        {
            var attacker = Game.Manager.Net.Players.TryGetValue(p.Receive.SenderClientId, out var a) ? a : null;
            if (attacker == null || attacker == this) return;
            if (Game.Manager.Net.SameSquad(attacker, this)) return; // нет дружественного огня
            ServerDamage(part, dmg, pen, attacker.PlayerName.Value.ToString(), weaponName, p.Receive.SenderClientId, true);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void HealRpc(int part, float amount, bool stopBleed, bool fixFracture, bool surgery, float surgeryFrac, RpcParams p = default)
        {
            var h = Health.Value; float max = BodyHealth.MaxOf(part, MaxHpMul.Value);
            if (surgery && h.Get(part) <= 0) h.Set(part, max * surgeryFrac);
            else if (h.Get(part) > 0 || part == 1 || part == 0) h.Set(part, Mathf.Min(max, h.Get(part) + amount));
            if (stopBleed) { h.bleed &= (byte)~(1 << part); h.heavyBleed &= (byte)~(1 << part); }
            if (fixFracture) h.fracture &= (byte)~(1 << part);
            Health.Value = h;
        }

        [Rpc(SendTo.Server)]
        public void HealAllRpc(float amount) { var h = Health.Value; for (int i = 0; i < 7; i++) if (h.Get(i) > 0) h.Set(i, Mathf.Min(BodyHealth.MaxOf(i, MaxHpMul.Value), h.Get(i) + amount)); Health.Value = h; }

        [Rpc(SendTo.Server)]
        public void EatRpc(float energy, float hydration) { var h = Health.Value; h.energy = Mathf.Clamp(h.energy + energy, 0, 100); h.hydration = Mathf.Clamp(h.hydration + hydration, 0, 100); Health.Value = h; }

        [Rpc(SendTo.Server)]
        public void SelfDamageRpc(string part, float dmg) { ServerDamage(part, dmg, 999, "Падение", "", 0, false); }

        [Rpc(SendTo.Server)]
        public void ShotRpc(Vector3 origin, Vector3 dir, bool suppressed, string cls)
        {
            Noise.Emit(origin, suppressed ? 25f : (cls == "pistol" ? 80f : 130f));
            ShotFxRpc(origin, dir, suppressed, cls);
        }

        [Rpc(SendTo.NotMe)]
        void ShotFxRpc(Vector3 origin, Vector3 dir, bool suppressed, string cls)
        {
            Fx.Tracer(origin, dir, 150f, new Color(1f, 0.9f, 0.6f)); Fx.Gunshot(origin, cls, suppressed, 0.9f); if (remoteGun != null) Fx.MuzzleFlash(remoteGun);
        }

        [Rpc(SendTo.Server)]
        public void StepRpc(Vector3 pos, float radius) { Noise.Emit(pos, radius); }

        [Rpc(SendTo.Server)]
        public void ExplodeRpc(Vector3 pos, float radius) { Noise.Emit(pos, 200f); ExplodeFxRpc(pos, radius); }

        [Rpc(SendTo.NotMe)]
        void ExplodeFxRpc(Vector3 pos, float radius) { Fx.Explosion(pos, radius); }

        [Rpc(SendTo.SpecifiedInParams)]
        public void SpottedRpc(RpcParams p) { if (Stats.Has("sixthSense")) Game.Manager.UI?.SixthSense(); }

        public void SetPain(float seconds) { painUntil = Time.time + seconds; }
        public void ClearPain(float seconds) { painUntil = Time.time - 1; painFreeUntil = Time.time + seconds; }
        float painFreeUntil;
        public bool PainFree => Time.time < painFreeUntil;
    }
}
