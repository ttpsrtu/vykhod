using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Сетевой объект сессии рейда: карта, режим, сид, таймер, контейнеры, чат, килл-фид, двери.</summary>
    public class RaidSession : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> MapId = new NetworkVariable<FixedString32Bytes>();
        public NetworkVariable<int> Mode = new NetworkVariable<int>();          // 0 кооп, 1 pvp
        public NetworkVariable<int> Seed = new NetworkVariable<int>();
        public NetworkVariable<double> EndTime = new NetworkVariable<double>();
        public NetworkVariable<bool> Night = new NetworkVariable<bool>();
        public NetworkVariable<int> EnemiesAlive = new NetworkVariable<int>();
        public NetworkVariable<bool> BossAlive = new NetworkVariable<bool>();

        readonly Dictionary<int, List<ItemInstance>> containers = new Dictionary<int, List<ItemInstance>>(); // только сервер
        readonly HashSet<int> openDoors = new HashSet<int>();
        bool initialized;

        public void Init(string map, string mode, int seed, int minutes)
        {
            MapId.Value = map; Mode.Value = mode == "pvp" ? 1 : 0; Seed.Value = seed;
            Night.Value = new System.Random(seed).NextDouble() < 0.3;
            initialized = true;
            raidMinutes = minutes;
        }
        int raidMinutes = 30;

        public double TimeLeft => EndTime.Value - NetworkManager.ServerTime.Time;

        public override void OnNetworkSpawn()
        {
            Game.Manager.Net.Session = this;
            if (IsServer)
            {
                EndTime.Value = NetworkManager.ServerTime.Time + raidMinutes * 60.0;
            }
            else
            {
                var map = Database.Map(MapId.Value.ToString());
                if (map != null && !World.IsReady) StartCoroutine(BuildClient(map));
            }
            if (World.IsReady) Game.Manager.Net.OnSessionReady();
        }

        System.Collections.IEnumerator BuildClient(MapDef map)
        {
            Game.Manager.UI.ShowLoading("Строим карту: " + map.name);
            yield return null;
            World.Build(map, Seed.Value, false);
            yield return null;
            Game.Manager.Net.OnSessionReady();
        }

        void Update()
        {
            if (!IsServer || !IsSpawned) return;
            if (TimeLeft <= 0 && !timeUp) { timeUp = true; TimeUpRpc(); }
        }
        bool timeUp;

        [Rpc(SendTo.Everyone)]
        void TimeUpRpc()
        {
            if (Game.State == GameState.Raid) Game.Manager.FinishRaid(false, "Время рейда вышло");
        }

        // ------------------------------------------------------------ контейнеры (сервер — источник правды)
        List<ItemInstance> Contents(int id)
        {
            if (!containers.TryGetValue(id, out var l))
            {
                var c = World.Container(id);
                var body = c != null ? c.GetComponent<BodyLoot>() : null;
                if (body != null) { var def = Database.Enemy(body.EnemyId); l = def != null ? LootGen.EnemyLoot(def, body.Seed) : new List<ItemInstance>(); }
                else l = c != null ? LootGen.Generate(c, Seed.Value) : new List<ItemInstance>();
                containers[id] = l;
            }
            return l;
        }

        static string Pack(List<ItemInstance> l) => Json.Serialize(l.Select(i => (object)i.ToJson()).ToList());
        static List<ItemInstance> Unpack(string s) { var r = new List<ItemInstance>(); foreach (var d in J.DL(Json.Parse(s))) { var it = ItemInstance.FromJson(d); if (it != null) r.Add(it); } return r; }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestContainerRpc(int id, RpcParams p = default)
        {
            var l = Contents(id);
            ContainerContentsRpc(id, Pack(l), RpcTarget.Single(p.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void ContainerContentsRpc(int id, string json, RpcParams p)
        {
            Game.Manager.UI.OnContainerContents(id, Unpack(json));
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void TakeFromContainerRpc(int id, int index, RpcParams p = default)
        {
            var l = Contents(id);
            if (index >= 0 && index < l.Count)
            {
                var it = l[index]; l.RemoveAt(index);
                GiveItemRpc(it.ToJsonString(), RpcTarget.Single(p.Receive.SenderClientId, RpcTargetUse.Temp));
            }
            ContainerContentsRpc(id, Pack(l), RpcTarget.Single(p.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void PutInContainerRpc(int id, string itemJson, RpcParams p = default)
        {
            var l = Contents(id);
            var it = ItemInstance.FromJsonString(itemJson); if (it != null) l.Add(it);
            ContainerContentsRpc(id, Pack(l), RpcTarget.Single(p.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        public void GiveItemRpc(string json, RpcParams p)
        {
            var it = ItemInstance.FromJsonString(json); if (it == null) return;
            it.found = true;
            var lp = Game.Manager.Net.LocalPlayer;
            if (!Game.Manager.RaidLoadout.TryAdd(it))
            {
                Game.Notify("Нет места: " + it.Name);
                DropItemRpc(it.ToJsonString(), lp != null ? lp.transform.position + lp.transform.forward : Vector3.zero);
            }
            else
            {
                Game.Manager.Raid.lootValue += it.Value;
                Game.Profile.AddSkillXp("search", 3);
                if (it.Info != null && it.Info.rarity >= 3) Game.Profile.AddSkillXp("attention", 4);
                Game.Manager.UI.OnInventoryChanged();
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void DropItemRpc(string json, Vector3 pos, RpcParams p = default)
        {
            var prefab = Game.Manager.Net.WorldItemPrefab; if (prefab == null) return;
            var go = Instantiate(prefab, pos + Vector3.up * 0.3f, Quaternion.Euler(0, Random.Range(0, 360), 0));
            var wi = go.GetComponent<WorldItem>();
            wi.Json.Value = json;
            go.GetComponent<NetworkObject>().Spawn();
        }

        // ------------------------------------------------------------ двери
        public bool IsDoorOpen(int id) => openDoors.Contains(id);

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void OpenDoorRpc(int id, RpcParams p = default) { openDoors.Add(id); DoorStateRpc(id, true); }

        [Rpc(SendTo.Everyone)]
        void DoorStateRpc(int id, bool open)
        {
            openDoors.Add(id);
            var d = World.Door(id); if (d != null) d.SetOpen(open);
        }

        // ------------------------------------------------------------ чат, фид, события
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void ChatRpc(string msg, RpcParams p = default)
        {
            string n = Game.Manager.Net.NameOf(p.Receive.SenderClientId);
            ChatBroadcastRpc(n, msg.Length > 200 ? msg.Substring(0, 200) : msg);
        }

        [Rpc(SendTo.Everyone)]
        public void ChatBroadcastRpc(string who, string msg) { Game.Manager.UI.OnChat(who, msg); }

        [Rpc(SendTo.Everyone)]
        public void KillFeedRpc(string killer, string victim, string weapon) { Game.Manager.UI.OnKillFeed($"{killer} ▶ {victim}  [{weapon}]"); }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void AnnounceRpc(string text, RpcParams p = default) { AnnounceAllRpc(text); }

        [Rpc(SendTo.Everyone)]
        void AnnounceAllRpc(string text) { Game.Notify(text); }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void ReportKillRpc(string enemyId, bool boss, RpcParams p = default)
        {
            EnemiesAlive.Value = Mathf.Max(0, EnemiesAlive.Value - 1);
            if (boss) BossAlive.Value = false;
        }
    }
}
