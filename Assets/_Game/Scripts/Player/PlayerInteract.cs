using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Клавиша F: контейнеры, предметы на земле, двери, установка квестовых предметов, лечение союзников.</summary>
    public class PlayerInteract : MonoBehaviour
    {
        PlayerNet net; LootContainer searching; float searchEnd; public float SearchProgress;
        LockedDoor picking; float pickEnd;

        void Awake() { net = GetComponent<PlayerNet>(); }

        void Update()
        {
            if (Game.State != GameState.Raid || net.Dead.Value || net.Cam == null) return;
            var ui = Game.Manager.UI;
            if (Input.GetKeyDown(KeyCode.Tab)) { ui.ToggleInventory(); }
            if (Input.GetKeyDown(KeyCode.Escape)) { ui.TogglePause(); }
            if (Input.GetKeyDown(KeyCode.Return) && !ui.ChatOpen) ui.OpenChat();
            if (ui.IsMenuOpen) { searching = null; picking = null; SearchProgress = 0; return; }
            if (searching != null)
            {
                SearchProgress = 1f - Mathf.Clamp01((searchEnd - Time.time) / searching.SearchTime);
                if (!Input.GetKey(KeyCode.F) && !Stats.Has("search_m40")) { ui.SetHint("Осмотр прерван"); searching = null; SearchProgress = 0; return; }
                if (Time.time >= searchEnd) { var c = searching; searching = null; SearchProgress = 0; Game.Profile.AddSkillXp("attention", 6); Game.Profile.AddSkillXp("search", 2); ui.OpenContainer(c); Game.Manager.Net.Session.RequestContainerRpc(c.Id); }
                else ui.SetHint($"Осматриваем {searching.Title}... {Mathf.CeilToInt(searchEnd - Time.time)}");
                return;
            }
            if (picking != null)
            {
                if (!Input.GetKey(KeyCode.F)) { picking = null; return; }
                if (Time.time >= pickEnd) { Game.Manager.Net.Session.OpenDoorRpc(picking.Id); Game.Profile.AddSkillXp("intellect", 20); picking = null; return; }
                ui.SetHint($"Вскрываем... {Mathf.CeilToInt(pickEnd - Time.time)}"); return;
            }
            var cam = net.Cam.transform;
            if (!Physics.Raycast(cam.position, cam.forward, out var hit, 3.2f, ~0, QueryTriggerInteraction.Collide)) { ui.SetHint(""); return; }
            var cont = hit.collider.GetComponentInParent<LootContainer>();
            var wi = hit.collider.GetComponentInParent<WorldItem>();
            var door = hit.collider.GetComponentInParent<LockedDoor>();
            var ally = hit.collider.GetComponentInParent<PlayerNet>();
            if (cont != null)
            {
                bool instant = cont.Kind == "body" && Stats.Has("search_m20");
                ui.SetHint($"[F] Осмотреть: {cont.Title}" + (Stats.Has("attention_m30") ? "  (видно содержимое)" : ""));
                if (Input.GetKeyDown(KeyCode.F)) { if (instant || cont.SearchTime < 0.5f) { ui.OpenContainer(cont); Game.Manager.Net.Session.RequestContainerRpc(cont.Id); } else { searching = cont; searchEnd = Time.time + cont.SearchTime; } }
            }
            else if (wi != null && wi.Item != null)
            {
                ui.SetHint($"[F] Подобрать: {wi.Item.Name}" + (wi.Item.count > 1 ? $" ×{wi.Item.count}" : ""));
                if (Input.GetKeyDown(KeyCode.F)) wi.PickupRpc();
            }
            else if (door != null && !door.Open)
            {
                var lo = Game.Manager.RaidLoadout; bool hasKey = string.IsNullOrEmpty(door.KeyId) || lo.HasKey(door.KeyId);
                if (hasKey) { ui.SetHint($"[F] Открыть: {door.RoomName}" + (string.IsNullOrEmpty(door.KeyId) ? "" : " (ключ)")); if (Input.GetKeyDown(KeyCode.F)) { Game.Manager.Net.Session.OpenDoorRpc(door.Id); UseKey(door.KeyId); } }
                else if (Stats.Has("lockpick") || Stats.Has("hackSpeed")) { ui.SetHint($"[F] Вскрыть замок: {door.RoomName} ({(Stats.Has("hackSpeed") ? 5 : 30)} с)"); if (Input.GetKeyDown(KeyCode.F)) { picking = door; pickEnd = Time.time + (Stats.Has("hackSpeed") ? 5f : 30f); } }
                else ui.SetHint($"Заперто: {door.RoomName}. Нужен {Database.NameOf(door.KeyId)}");
            }
            else if (ally != null && ally != net && !ally.Dead.Value && Game.Manager.Net.SameSquad(net, ally))
            {
                var med = Game.Manager.RaidLoadout.AllItems().FirstOrDefault(i => i.Type == "med" && i.Info.item.P("heal") > 0 && i.uses != 0);
                if (med != null && ally.Hp < ally.MaxHp - 1)
                {
                    ui.SetHint($"[F] Вылечить {ally.PlayerName.Value}: {med.Name}");
                    if (Input.GetKeyDown(KeyCode.F)) { int part = ally.Health.Value.WorstPart(ally.MaxHpMul.Value); ally.HealRpc(part, 30f * (Stats.Has("allyHeal") ? 2f : 1f), true, false, false, 0); med.uses = Mathf.Max(0, med.uses - 1); if (med.uses == 0) Game.Manager.RaidLoadout.RemoveItem(med); QuestSystem.OnHealAlly(); Game.Profile.AddSkillXp("health", 15); Fx.Heal(transform.position); ui.OnInventoryChanged(); }
                }
                else ui.SetHint(ally.PlayerName.Value + $"  {ally.Hp:0}/{ally.MaxHp:0}");
            }
            else
            {
                // установка квестового предмета у точки интереса
                var poi = World.Pois.FirstOrDefault(p => Vector3.Distance(p.transform.position, transform.position) < 14f);
                string need = poi != null ? QuestSystem.PlantNeededAt(World.Map.id, poi.Name) : null;
                if (need != null && Game.Manager.RaidLoadout.CountOf(need) > 0)
                {
                    ui.SetHint($"[F] Установить {Database.NameOf(need)} ({poi.Name})");
                    if (Input.GetKeyDown(KeyCode.F)) { Game.Manager.RaidLoadout.Take(need, 1); QuestSystem.OnPlant(World.Map.id, poi.Name, need); Game.Notify("Установлено: " + Database.NameOf(need)); ui.OnInventoryChanged(); }
                }
                else ui.SetHint("");
            }
        }

        void UseKey(string keyId)
        {
            if (string.IsNullOrEmpty(keyId)) return;
            var k = Game.Manager.RaidLoadout.AllItems().FirstOrDefault(i => i.id == keyId);
            if (k != null && k.uses > 0) { k.uses--; if (k.uses == 0) { Game.Manager.RaidLoadout.RemoveItem(k); Game.Notify("Ключ сломался"); } }
        }
    }
}
