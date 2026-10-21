using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Оружие от первого лица: viewmodel с модами, стрельба, отдача, разброс, перезарядка, режимы, гранаты, медицина.</summary>
    public class PlayerWeapons : MonoBehaviour
    {
        public string Slot = "primary";
        public bool Ads, Reloading, Jammed, Using;
        public int FireMode; // индекс в modes
        public float LookSensMul = 1f;
        public WeaponComputed WC;
        public ItemInstance Current => Game.Manager.RaidLoadout?.eq.Get(Slot);
        public string UsingText = "";
        PlayerNet net; FPSController ctrl; Transform hands; GameObject model; string builtKey = "";
        float nextShot, reloadEnd, recoilPitch, recoilYaw, swapEnd, useEnd, adsT, nvToggleT; int burstLeft; bool nv;
        Vector3 hipPos = new Vector3(0.24f, -0.24f, 0.42f), adsPos = new Vector3(0f, -0.145f, 0.28f);
        public bool NightVision => nv;

        void Awake() { net = GetComponent<PlayerNet>(); ctrl = GetComponent<FPSController>(); }

        void OnEnable()
        {
            if (net.Head == null) return; // до OnNetworkSpawn голова ещё не назначена — построим позже
            if (hands == null) { hands = new GameObject("Hands").transform; hands.SetParent(net.Head, false); }
            if (hands.parent != net.Head) hands.SetParent(net.Head, false);
            if (Current == null) foreach (var s in new[] { "primary", "secondary", "holster", "melee" }) if (Game.Manager.RaidLoadout.eq.Get(s) != null) { Slot = s; break; }
            Rebuild();
        }

        public void Rebuild()
        {
            var cur = Current; WC = cur != null ? WeaponStats.Compute(cur) : null;
            string key = cur == null ? "" : cur.id + "|" + string.Join(",", cur.mods.Values);
            if (key == builtKey && model != null) return;
            builtKey = key;
            if (model != null) Destroy(model);
            model = new GameObject("vm"); model.transform.SetParent(hands, false);
            if (cur == null) return;
            var def = cur.Info.weapon;
            var path = AssetCatalog.FindModel(def.model, "weapon|guns");
            GameObject g = path != null ? AssetCatalog.Spawn(path, model.transform, def.cls == "pistol" ? 0.26f : def.cls == "melee" ? 0.35f : def.cls == "smg" ? 0.5f : def.cls == "sniper" || def.cls == "lmg" ? 1.05f : 0.78f, false, true) : null;
            if (g != null) g.transform.localRotation = Quaternion.Euler(0, 90, 0); else g = AssetCatalog.GunPlaceholder(def, model.transform);
            float len = def.cls == "pistol" ? 0.2f : def.cls == "smg" ? 0.45f : def.cls == "sniper" || def.cls == "lmg" ? 1.05f : 0.75f;
            // визуализация модов
            foreach (var kv in cur.mods)
            {
                var m = Database.Mod(kv.Value); if (m == null) continue;
                switch (m.slot)
                {
                    case "muzzle": { var c = AssetCatalog.Prim(PrimitiveType.Cylinder, model.transform, new Vector3(0, 0.015f, len + (m.model == "suppressor" ? 0.1f : 0.03f)), new Vector3(m.model == "suppressor" ? 0.045f : 0.035f, m.model == "suppressor" ? 0.1f : 0.03f, m.model == "suppressor" ? 0.045f : 0.035f), new Color(0.1f, 0.1f, 0.1f), false); c.transform.localRotation = Quaternion.Euler(90, 0, 0); break; }
                    case "sight": case "sight_p": if (m.Stat("zoom") > 1) { var s = AssetCatalog.Prim(PrimitiveType.Cylinder, model.transform, new Vector3(0, 0.075f, len * 0.3f), new Vector3(0.035f, 0.08f, 0.035f), new Color(0.08f, 0.08f, 0.1f), false); s.transform.localRotation = Quaternion.Euler(90, 0, 0); } else AssetCatalog.Prim(PrimitiveType.Cube, model.transform, new Vector3(0, 0.06f, len * 0.25f), new Vector3(0.03f, 0.03f, 0.04f), new Color(0.1f, 0.1f, 0.12f), false); break;
                    case "grip": AssetCatalog.Prim(PrimitiveType.Cube, model.transform, new Vector3(0, -0.06f, len * 0.6f), new Vector3(0.025f, 0.08f, 0.03f), new Color(0.15f, 0.15f, 0.15f), false); break;
                    case "light": { var l = AssetCatalog.Prim(PrimitiveType.Cube, model.transform, new Vector3(0.035f, 0.0f, len * 0.7f), new Vector3(0.02f, 0.02f, 0.06f), new Color(0.2f, 0.2f, 0.2f), false); if (m.Stat("laser") > 0) { var lr = l.AddComponent<LineRenderer>(); lr.positionCount = 2; lr.startWidth = 0.004f; lr.endWidth = 0.004f; lr.material = AssetCatalog.Emissive(Color.red, 3f); lr.useWorldSpace = true; } break; }
                    case "bipod": AssetCatalog.Prim(PrimitiveType.Cube, model.transform, new Vector3(0, -0.07f, len * 0.75f), new Vector3(0.08f, 0.1f, 0.02f), new Color(0.15f, 0.15f, 0.15f), false); break;
                }
            }
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var t in model.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
            var rr = model.GetComponentsInChildren<Renderer>();
            if (rr.Length > 0) { var bb = rr[0].bounds; foreach (var r in rr) bb.Encapsulate(r.bounds); Debug.Log($"[Weapons] {def.id}: модель {(path ?? "placeholder")}, рендереров {rr.Length}, bounds {bb.center} {bb.size}, hands={hands.position} parent={hands.parent?.name}, cam={(net.Cam != null ? net.Cam.transform.position.ToString() : "нет")}"); }
            else Debug.LogWarning($"[Weapons] {def.id}: у модели нет рендереров ({path})");
            FireMode = Mathf.Clamp(FireMode, 0, Mathf.Max(0, def.modes.Count - 1));
            swapEnd = Time.time + (WC?.swapTime ?? 0.4f);
            Ads = false;
        }

        public string ModeName => Current == null ? "" : Current.Info.weapon.modes.Count == 0 ? "" : Current.Info.weapon.modes[FireMode] == "auto" ? "АВТО" : Current.Info.weapon.modes[FireMode] == "burst" ? "ОЧЕРЕДЬ" : "ОДИН";
        public int AmmoReserve => Current == null || string.IsNullOrEmpty(Current.ammoId) ? 0 : Game.Manager.RaidLoadout.CountOf(Current.ammoId);

        void Update()
        {
            if (Game.State != GameState.Raid || net.Dead.Value) return;
            bool uiOpen = Game.Manager.UI.IsMenuOpen;
            var cur = Current; var cam = net.Cam;
            // переключение слотов
            if (!uiOpen && !Using)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) Switch("primary"); if (Input.GetKeyDown(KeyCode.Alpha2)) Switch("secondary");
                if (Input.GetKeyDown(KeyCode.Alpha3)) Switch("holster"); if (Input.GetKeyDown(KeyCode.Alpha4)) Switch("melee");
                if (Input.GetKeyDown(KeyCode.G)) ThrowGrenade();
                if (Input.GetKeyDown(KeyCode.N) && Time.time > nvToggleT) ToggleNV();
                if (Input.GetKeyDown(KeyCode.Alpha5)) QuickMed("med"); if (Input.GetKeyDown(KeyCode.Alpha6)) QuickMed("food");
            }
            if (Using) { if (Time.time >= useEnd) FinishUse(); return; }
            // позиция viewmodel: прицеливание, бег, перезарядка
            bool wantAds = !uiOpen && Input.GetMouseButton(1) && cur != null && !cur.Info.weapon.IsMelee && !ctrl.Sprinting && !Reloading && Time.time > swapEnd;
            adsT = Mathf.MoveTowards(adsT, wantAds ? 1 : 0, Time.deltaTime / Mathf.Max(0.05f, WC?.adsTime ?? 0.3f));
            Ads = adsT > 0.6f;
            float zoom = Ads && WC != null && WC.zoom > 1 ? WC.zoom : 1f;
            if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Game.Settings.fov / (Ads ? (zoom > 1 ? zoom * (1f + Stats.Get("zoom")) : 1.25f) : 1f), Time.deltaTime * 12);
            LookSensMul = 1f / (Ads ? Mathf.Max(1.2f, zoom) : 1f);
            Vector3 targetPos = Vector3.Lerp(hipPos, adsPos, adsT);
            if (ctrl.Sprinting) targetPos += new Vector3(0.05f, -0.08f, -0.05f);
            if (Reloading) targetPos += new Vector3(0, -0.12f, 0);
            if (Time.time < swapEnd) targetPos += new Vector3(0, -0.25f, 0);
            float sway = (net.InPain && !net.PainFree ? 1.6f : 1f) * (net.Health.Value.larm <= 0 || net.Health.Value.rarm <= 0 ? 2f : 1f) * Mathf.Max(0.3f, 1f + Stats.Get("swayHurt"));
            var swayOff = new Vector3(Mathf.Sin(Time.time * 1.3f) * 0.004f, Mathf.Sin(Time.time * 1.7f) * 0.003f, 0) * sway * (Ads ? 0.5f : 1f);
            hands.localPosition = Vector3.Lerp(hands.localPosition, targetPos + swayOff, Time.deltaTime * 14);
            hands.localRotation = Quaternion.Slerp(hands.localRotation, Quaternion.Euler(ctrl.Sprinting ? 8 : 0, ctrl.Sprinting ? -12 : 0, 0), Time.deltaTime * 10);
            // отдача: возврат
            recoilPitch = Mathf.Lerp(recoilPitch, 0, Time.deltaTime * 8); recoilYaw = Mathf.Lerp(recoilYaw, 0, Time.deltaTime * 8);
            hands.localRotation *= Quaternion.Euler(-recoilPitch * 2.5f, recoilYaw, 0);
            // лазер
            var lr = model != null ? model.GetComponentInChildren<LineRenderer>() : null;
            if (lr != null && cam != null) { lr.SetPosition(0, lr.transform.position); lr.SetPosition(1, Physics.Raycast(cam.transform.position, cam.transform.forward, out var lh, 150f) ? lh.point : cam.transform.position + cam.transform.forward * 150f); }
            if (cur == null || uiOpen || Time.time < swapEnd) return;
            var def = cur.Info.weapon;
            // перезарядка
            if (Reloading) { if (Time.time >= reloadEnd) FinishReload(); return; }
            if (Input.GetKeyDown(KeyCode.R) && !def.IsMelee) { if (Jammed) { StartReload(true); } else if (cur.ammoInMag < WC.magCap && AmmoReserve > 0) StartReload(false); else if (AmmoReserve == 0) Game.Notify("Нет патронов " + cur.ammoId); }
            if (Input.GetKeyDown(KeyCode.B) && def.modes.Count > 1) { FireMode = (FireMode + 1) % def.modes.Count; Fx.Click(); }
            if (Input.GetKeyDown(KeyCode.T) && !def.IsMelee) CycleAmmo();
            // стрельба
            if (def.IsMelee) { if (Input.GetMouseButtonDown(0) && Time.time >= nextShot) Melee(def); return; }
            string mode = def.modes.Count > 0 ? def.modes[FireMode] : "single";
            bool trigger = mode == "auto" ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
            if (mode == "burst" && burstLeft > 0 && Time.time >= nextShot) { Fire(def); burstLeft--; return; }
            if (trigger && Time.time >= nextShot)
            {
                if (Jammed) { Game.Manager.UI.SetHint("Клин! Нажмите R"); return; }
                if (cur.ammoInMag <= 0) { Fx.Click(); nextShot = Time.time + 0.25f; if (AmmoReserve > 0 && !Reloading) StartReload(false); return; }
                Fire(def);
                if (mode == "burst") burstLeft = 2;
            }
        }

        public void Switch(string slot)
        {
            if (slot == Slot || Game.Manager.RaidLoadout.eq.Get(slot) == null) return;
            Slot = slot; Reloading = false; Jammed = false; builtKey = ""; Rebuild();
            Game.Profile.AddSkillXp("intellect", 0.2f);
        }

        void CycleAmmo()
        {
            var cur = Current; var def = cur.Info.weapon;
            var types = Game.Manager.RaidLoadout.AllItems().Where(i => i.IsAmmo && i.Info.ammo.caliber == def.caliber).Select(i => i.id).Distinct().ToList();
            if (types.Count == 0) return;
            int idx = types.IndexOf(cur.ammoId); string next = types[(idx + 1) % types.Count];
            if (next == cur.ammoId) return;
            // разряжаем магазин в инвентарь, меняем тип, перезаряжаем
            if (cur.ammoInMag > 0) Game.Manager.RaidLoadout.TryAdd(new ItemInstance(cur.ammoId, cur.ammoInMag));
            cur.ammoInMag = 0; cur.ammoId = next; StartReload(false);
            Game.Notify("Патроны: " + Database.NameOf(next));
        }

        void StartReload(bool unjam)
        {
            Reloading = true; reloadEnd = Time.time + (unjam ? 2.0f : WC.reloadTime); Ads = false; Fx.Reload(transform.position);
            if (!unjam) Game.Profile.AddSkillXp(SkillFor(Current.Info.weapon.cls), 0.6f);
        }

        void FinishReload()
        {
            Reloading = false; var cur = Current; if (cur == null) return;
            if (Jammed) { Jammed = false; return; }
            if (string.IsNullOrEmpty(cur.ammoId)) { var a = Database.AmmoFor(cur.Info.weapon.caliber); if (a.Count > 0) cur.ammoId = a[0].id; }
            int need = WC.magCap - cur.ammoInMag; int got = Game.Manager.RaidLoadout.Take(cur.ammoId, need);
            cur.ammoInMag += got;
            Game.Manager.UI.OnInventoryChanged();
        }

        static string SkillFor(string cls) => cls == "pistol" ? "pistols" : cls == "smg" ? "smg" : cls == "ar" ? "ar" : cls == "shotgun" ? "shotguns" : cls == "dmr" ? "dmr" : cls == "sniper" ? "sniper" : cls == "lmg" ? "lmg" : "melee";

        void Fire(WeaponDef def)
        {
            var cur = Current; var cam = net.Cam; if (cam == null) return;
            nextShot = Time.time + WC.ShotInterval;
            cur.ammoInMag--;
            // износ и клин
            cur.durability = Mathf.Max(0, cur.durability - 0.035f * Stats.Mul("durabilityLoss"));
            if (cur.durability < 30 && !Stats.Has("maintenance_m50") && Stats.Get("jamChance") > -0.99f && Random.value < (30 - cur.durability) / 250f * Mathf.Max(0f, 1f + Stats.Get("jamChance"))) { Jammed = true; Fx.Click(); return; }
            var ammo = Database.AmmoDef(cur.ammoId);
            bool lowHp = net.Hp < net.MaxHp * 0.3f;
            float dmgBase = WeaponStats.Damage(WC, ammo, lowHp);
            float spreadMoa = Ads ? WC.moa : WC.hipSpread; if (ctrl.Moving) spreadMoa *= 1.6f; if (ctrl.Crouched) spreadMoa *= 0.85f;
            if (net.Health.Value.larm <= 0 || net.Health.Value.rarm <= 0) spreadMoa *= 2f;
            int pellets = ammo?.pellets ?? 1; if (pellets > 1) spreadMoa += 60f;
            float range = def.cls == "sniper" ? 900 : def.cls == "dmr" ? 600 : def.cls == "pistol" || def.cls == "smg" ? 220 : def.cls == "shotgun" ? 70 : 450;
            for (int p = 0; p < pellets; p++)
            {
                var dir = cam.transform.forward;
                float ang = spreadMoa / 60f; dir = Quaternion.Euler(Random.Range(-ang, ang), Random.Range(-ang, ang), 0) * dir;
                var origin = cam.transform.position;
                var hits = Physics.RaycastAll(origin, dir, range).OrderBy(h => h.distance);
                foreach (var h in hits)
                {
                    if (h.collider.transform.root == transform.root) continue;
                    var hb = h.collider.GetComponent<Hitbox>();
                    float dmg = dmgBase * Mathf.Clamp(1f - Mathf.Max(0, h.distance - 60f) / 500f, 0.45f, 1f) * (pellets > 1 ? 0.6f : 1f);
                    if (hb != null && hb.owner is EnemyAI ai && !ai.Dead.Value)
                    {
                        ai.HitRpc(hb.part, dmg, ammo?.pen ?? 10, def.id, h.distance, hb.part == "head", WC.suppressed, false, cur.ModCount);
                        Fx.Impact(h.point, h.normal, true); Game.Manager.Raid.damageDealt += dmg; Game.Manager.UI.HitMarker(hb.part == "head");
                    }
                    else if (hb != null && hb.owner is PlayerNet pn && pn != net && !pn.Dead.Value)
                    {
                        if (!Game.Manager.Net.SameSquad(net, pn)) { pn.HitRpc(hb.part, dmg, ammo?.pen ?? 10, def.name); Fx.Impact(h.point, h.normal, true); Game.Manager.UI.HitMarker(hb.part == "head"); }
                    }
                    else Fx.Impact(h.point, h.normal, false);
                    break;
                }
            }
            // отдача
            float rv = WC.recoilV * 0.012f * (Ads ? 0.8f : 1f) * (ctrl.Crouched ? 0.85f : 1f), rh = WC.recoilH * 0.01f;
            if (Stats.Has("firstShotRecoil") && Time.time - lastShotT > 0.5f) rv *= 0.5f;
            lastShotT = Time.time;
            ctrl.Pitch -= rv * Random.Range(0.7f, 1.2f); transform.Rotate(0, rh * Random.Range(-1f, 1f), 0);
            recoilPitch += rv * 3f; recoilYaw += rh * Random.Range(-2f, 2f);
            Fx.Gunshot(cam.transform.position, def.cls, WC.suppressed, 1f); Fx.MuzzleFlash(model != null ? model.transform : hands);
            net.ShotRpc(cam.transform.position + cam.transform.forward * 0.6f, cam.transform.forward, WC.suppressed, def.cls);
            Game.Profile.AddSkillXp(SkillFor(def.cls), 0.35f); Game.Profile.AddSkillXp("recoil", 0.3f); Game.Profile.AddSkillXp("maintenance", 0.05f);
        }
        float lastShotT;

        void Melee(WeaponDef def)
        {
            nextShot = Time.time + 60f / def.rpm; var cam = net.Cam;
            hands.localPosition += new Vector3(0, 0, 0.25f);
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var h, 2.3f))
            {
                var hb = h.collider.GetComponent<Hitbox>(); float dmg = def.meleeDmg * Stats.Mul("meleeDamage") * (hb != null && hb.part == "head" ? 1.8f : 1f);
                if (hb != null && hb.owner is EnemyAI ai && !ai.Dead.Value) { ai.HitRpc(hb.part, dmg, 60, def.id, h.distance, hb.part == "head", true, false, 0); Fx.Impact(h.point, h.normal, true); Game.Manager.UI.HitMarker(false); }
                else if (hb != null && hb.owner is PlayerNet pn && pn != net && !Game.Manager.Net.SameSquad(net, pn)) { pn.HitRpc(hb.part, dmg, 60, def.name); Fx.Impact(h.point, h.normal, true); }
                else Fx.Impact(h.point, h.normal, false);
            }
            Game.Profile.AddSkillXp("melee", 2); Game.Profile.AddSkillXp("strength", 1);
        }

        // ------------------------------------------------------------ гранаты
        void ThrowGrenade()
        {
            var g = Game.Manager.RaidLoadout.AllItems().FirstOrDefault(i => i.Type == "throwable");
            if (g == null) { Game.Notify("Нет гранат"); return; }
            var def = g.Info.item; var cam = net.Cam;
            Game.Manager.RaidLoadout.RemoveItem(g.count > 1 ? null : g); if (g.count > 1) g.count--;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.transform.localScale = Vector3.one * 0.12f; go.transform.position = cam.transform.position + cam.transform.forward * 0.5f;
            go.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Mat(new Color(0.2f, 0.3f, 0.2f));
            var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.4f; rb.linearVelocity = (cam.transform.forward + Vector3.up * 0.15f).normalized * 16f * Stats.Mul("throwDistance");
            var gr = go.AddComponent<Grenade>(); gr.Init(def, net);
            Game.Profile.AddSkillXp("throwables", 5);
            Game.Manager.UI.OnInventoryChanged();
        }

        // ------------------------------------------------------------ медицина/еда/инструменты
        public void QuickMed(string type)
        {
            var it = Game.Manager.RaidLoadout.AllItems().Where(i => i.Type == type && (i.uses != 0)).OrderByDescending(i => i.Info.item.P("heal")).FirstOrDefault();
            if (it == null) { Game.Notify(type == "med" ? "Нет медицины" : "Нет еды"); return; }
            UseItem(it);
        }

        ItemInstance usingItem; int usingPart;
        public bool UseItem(ItemInstance it, int part = -1)
        {
            if (it == null || Using || Reloading) return false;
            var info = it.Info; if (info?.item == null) return false;
            var d = info.item; var h = net.Health.Value;
            switch (d.type)
            {
                case "med":
                    {
                        if (part < 0) part = h.WorstPart(net.MaxHpMul.Value);
                        bool need = h.Get(part) < BodyHealth.MaxOf(part, net.MaxHpMul.Value) || h.AnyBleed || h.AnyFracture || d.Has("pain");
                        if (!need) { Game.Notify("Лечить нечего"); return false; }
                        if (h.Get(part) <= 0 && !d.Has("surgery")) { part = h.WorstPart(net.MaxHpMul.Value); if (h.Get(part) <= 0) { Game.Notify("Нужен хирургический набор"); return false; } }
                        float t = d.P("time", 3) * Mathf.Max(0.3f, 1f - Stats.Get("healSpeed")); if (d.Has("surgery")) t *= Mathf.Max(0.3f, 1f - Stats.Get("surgerySpeed"));
                        Begin(it, part, t, "Лечение: " + d.name); return true;
                    }
                case "food": Begin(it, 0, d.P("time", 3), "Едим: " + d.name); return true;
                case "tool":
                    if (d.Has("repair")) { var w = Current; if (w == null || w.durability >= 100) { Game.Notify("Нечего чинить"); return false; } Begin(it, 0, 6f, "Ремонт " + w.Name); return true; }
                    return false;
            }
            return false;
        }

        void Begin(ItemInstance it, int part, float t, string text)
        {
            Using = true; usingItem = it; usingPart = part; useEnd = Time.time + t; UsingText = text; Ads = false;
            Fx.Heal(transform.position);
        }

        void FinishUse()
        {
            Using = false; UsingText = ""; var it = usingItem; usingItem = null; if (it == null) return;
            var d = it.Info.item; var h = net.Health.Value;
            if (d.type == "med")
            {
                float heal = d.P("heal");
                if (d.Has("surgery")) { net.HealRpc(usingPart, 0, true, false, true, Mathf.Clamp01(d.P("surgery") * (Stats.Has("surgery_m50") ? 1.6f : Stats.Has("surgery_m20") ? 1.2f : 1f)), default); }
                else if (heal > 0)
                {
                    float per = Mathf.Min(heal, 30f + (int)Stats.Get("medUses") * 10f); // одно применение лечит до 30 хп
                    net.HealRpc(usingPart, per, d.P("stopBleed") >= 1 || h.Bleeding(usingPart) && d.P("stopBleed") >= 1, d.P("fixFracture") > 0, false, 0, default);
                    if (it.uses > 0) it.uses = Mathf.Max(0, it.uses - Mathf.CeilToInt(per / Mathf.Max(1, heal / Mathf.Max(1, it.uses))));
                }
                else { net.HealRpc(usingPart, 0, d.P("stopBleed") > 0, d.P("fixFracture") > 0, false, 0, default); if (it.uses > 0) it.uses--; }
                if (d.Has("pain")) { net.ClearPain(d.P("pain") * Stats.Mul("painDuration")); if (it.uses > 0 && heal <= 0 && d.P("stopBleed") <= 0) { } }
                if (d.Has("stamina")) ctrl.Stamina = ctrl.MaxStamina;
                Game.Profile.AddSkillXp("health", 12); if (d.Has("surgery")) Game.Profile.AddSkillXp("surgery", 30);
                if (it.uses == 0) Game.Manager.RaidLoadout.RemoveItem(it);
            }
            else if (d.type == "food")
            {
                net.EatRpc(d.P("energy") * Stats.Mul("foodBonus"), d.P("hydration"));
                Game.Profile.AddSkillXp("metabolism", 10);
                it.count--; if (it.count <= 0) Game.Manager.RaidLoadout.RemoveItem(it);
            }
            else if (d.type == "tool" && d.Has("repair"))
            {
                var w = Current; if (w != null) w.durability = Mathf.Min(100, w.durability + d.P("repair"));
                Game.Profile.AddSkillXp("maintenance", 20); Game.Manager.RaidLoadout.RemoveItem(it);
            }
            Game.Manager.UI.OnInventoryChanged();
        }

        void ToggleNV()
        {
            nvToggleT = Time.time + 0.5f;
            bool has = Game.Manager.RaidLoadout.eq.Get("headgear") != null || (WC != null && WC.night >= 1) || Stats.Get("night") >= 1;
            if (!has) { Game.Notify("Нет прибора ночного видения"); return; }
            nv = !nv;
            Game.Manager.UI.SetNightVision(nv);
            RenderSettings.ambientLight = nv ? new Color(0.5f, 0.7f, 0.5f) : (World.Underground ? new Color(0.08f, 0.08f, 0.1f) : World.Night ? new Color(0.07f, 0.08f, 0.13f) : RenderSettings.ambientLight);
        }
    }

}
