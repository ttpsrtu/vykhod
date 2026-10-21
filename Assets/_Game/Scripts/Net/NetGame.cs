using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Сетевой слой: хост (сервер+клиент) и подключение по IP. Кооп и PvP — одна сессия, разные отряды.</summary>
    public class NetGame : MonoBehaviour
    {
        public NetworkManager NM;
        public UnityTransport Transport;
        public RaidSession Session;
        public PlayerNet LocalPlayer;
        public readonly Dictionary<ulong, PlayerNet> Players = new Dictionary<ulong, PlayerNet>();
        public readonly Dictionary<ulong, string> ClientNames = new Dictionary<ulong, string>();
        public readonly Dictionary<ulong, string> ClientSquads = new Dictionary<ulong, string>();
        public ushort Port = 7777;
        public GameObject PlayerPrefab, EnemyPrefab, SessionPrefab, WorldItemPrefab;
        public bool IsHost => NM != null && NM.IsHost;
        public bool IsServer => NM != null && NM.IsServer;
        public bool IsRunning => NM != null && NM.IsListening;
        public string Squad = "A";
        bool worldReadyNotified;

        void Awake()
        {
            PlayerPrefab = Resources.Load<GameObject>("Prefabs/Player");
            EnemyPrefab = Resources.Load<GameObject>("Prefabs/Enemy");
            SessionPrefab = Resources.Load<GameObject>("Prefabs/Session");
            WorldItemPrefab = Resources.Load<GameObject>("Prefabs/WorldItem");
            if (PlayerPrefab == null || SessionPrefab == null)
                Debug.LogError("Нет сетевых префабов в Resources/Prefabs. В редакторе: меню Vykhod → Setup Project.");
            NM = gameObject.GetComponent<NetworkManager>();
            if (NM == null) NM = gameObject.AddComponent<NetworkManager>();
            Transport = gameObject.GetComponent<UnityTransport>();
            if (Transport == null) Transport = gameObject.AddComponent<UnityTransport>();
            var cfg = NM.NetworkConfig ?? new NetworkConfig();
            cfg.NetworkTransport = Transport;
            cfg.EnableSceneManagement = false;
            cfg.ConnectionApproval = true;
            cfg.TickRate = 30;
            cfg.PlayerPrefab = PlayerPrefab;
            foreach (var p in new[] { PlayerPrefab, EnemyPrefab, SessionPrefab, WorldItemPrefab })
                if (p != null) cfg.Prefabs.Add(new NetworkPrefab { Prefab = p });
            NM.NetworkConfig = cfg;
            NM.ConnectionApprovalCallback = Approve;
            NM.OnClientConnectedCallback += OnClientConnected;
            NM.OnClientDisconnectCallback += OnClientDisconnected;
            NM.OnTransportFailure += () => { Debug.LogWarning("Transport failure"); Game.Manager.OnConnectionLost(); };
        }

        byte[] Payload()
        {
            var d = new Dictionary<string, object> { ["name"] = Game.Profile?.name ?? Game.Settings.playerName, ["squad"] = Squad, ["ver"] = Application.version };
            return Encoding.UTF8.GetBytes(Json.Serialize(d));
        }

        void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse resp)
        {
            string name = "Игрок", squad = "A";
            try
            {
                var d = J.D(Json.Parse(Encoding.UTF8.GetString(req.Payload)));
                name = J.S(d, "name", name); squad = J.S(d, "squad", squad);
            }
            catch { }
            ClientNames[req.ClientNetworkId] = name;
            ClientSquads[req.ClientNetworkId] = squad;
            resp.Approved = true;
            resp.CreatePlayerObject = true;
            var spawn = World.PickPlayerSpawn(squad);
            resp.Position = spawn;
            resp.Rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            resp.Pending = false;
        }

        public bool Host(string mapId, string mode, int seed)
        {
            if (PlayerPrefab == null || SessionPrefab == null) return false;
            Squad = mode == "pvp" ? "P" + Random.Range(1000, 9999) : "A";
            worldReadyNotified = false;
            Transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
            NM.NetworkConfig.ConnectionData = Payload();
            if (!NM.StartHost()) return false;
            var go = Instantiate(SessionPrefab);
            var s = go.GetComponent<RaidSession>();
            s.Init(mapId, mode, seed, Database.Map(mapId)?.raidMinutes ?? 30);
            go.GetComponent<NetworkObject>().Spawn();
            Game.Notify($"Хост запущен на порту {Port}. Друзья подключаются по вашему IP.");
            return true;
        }

        public bool Join(string ip, ushort port)
        {
            if (PlayerPrefab == null || SessionPrefab == null) return false;
            worldReadyNotified = false;
            Port = port;
            Squad = "A";
            Transport.SetConnectionData(ip, port);
            NM.NetworkConfig.ConnectionData = Payload();
            return NM.StartClient();
        }

        public void Shutdown()
        {
            if (NM != null && NM.IsListening) NM.Shutdown();
            Session = null; LocalPlayer = null; Players.Clear(); ClientNames.Clear(); ClientSquads.Clear();
            worldReadyNotified = false;
        }

        void OnClientConnected(ulong id)
        {
            if (NM.IsServer && id != NM.LocalClientId)
            {
                string n = ClientNames.TryGetValue(id, out var nn) ? nn : "Игрок";
                Session?.ChatBroadcastRpc("Система", $"{n} вошёл в рейд");
            }
        }

        void OnClientDisconnected(ulong id)
        {
            Players.Remove(id);
            if (NM.IsServer)
            {
                string n = ClientNames.TryGetValue(id, out var nn) ? nn : "Игрок";
                if (id != NM.LocalClientId) Session?.ChatBroadcastRpc("Система", $"{n} покинул рейд");
            }
            else if (id == NM.LocalClientId || !NM.IsConnectedClient)
            {
                Game.Manager.OnConnectionLost();
            }
        }

        public void RegisterPlayer(PlayerNet p)
        {
            Players[p.OwnerClientId] = p;
            if (p.IsOwner) { LocalPlayer = p; TryReady(); }
        }

        public void OnSessionReady() => TryReady();

        void TryReady()
        {
            if (worldReadyNotified || LocalPlayer == null || Session == null || !World.IsReady) return;
            worldReadyNotified = true;
            LocalPlayer.OnWorldReady();
            Game.Manager.OnRaidReady();
            if (IsServer) World.StartServerSystems();
        }

        public string NameOf(ulong clientId)
        {
            if (Players.TryGetValue(clientId, out var p)) return p.PlayerName.Value.ToString();
            return ClientNames.TryGetValue(clientId, out var n) ? n : "Игрок";
        }

        public bool SameSquad(PlayerNet a, PlayerNet b)
        {
            if (a == null || b == null) return false;
            if (Session != null && Session.Mode.Value == 0) return true;
            return a.Squad.Value.Equals(b.Squad.Value);
        }

        public static string LocalIPv4()
        {
            try
            {
                foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) return ip.ToString();
            }
            catch { }
            return "?";
        }
    }
}
