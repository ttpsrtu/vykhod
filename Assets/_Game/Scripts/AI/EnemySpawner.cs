using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Vykhod
{
    /// <summary>Серверный спавн ботов: стартовая волна, подкрепления, снайперы на крышах, босс со свитой.</summary>
    public class EnemySpawner : MonoBehaviour
    {
        int targetCount; float nextWave; bool bossSpawned; readonly List<EnemyAI> alive = new List<EnemyAI>();

        void Start()
        {
            var map = World.Map; if (map == null) { enabled = false; return; }
            targetCount = Random.Range(map.enemiesMin, map.enemiesMax + 1);
            int initial = Mathf.CeilToInt(targetCount * 0.6f);
            for (int i = 0; i < initial; i++) SpawnRandom();
            int snipers = Mathf.Min(World.SniperSpots.Count, Random.Range(1, 4));
            foreach (var s in World.SniperSpots.OrderBy(_ => Random.value).Take(snipers)) Spawn("scav_sniper", s, true);
            if (Random.value < map.bossChance) SpawnBoss();
            nextWave = Time.time + 60f;
        }

        void Update()
        {
            alive.RemoveAll(a => a == null || a.Dead.Value);
            var s = Game.Manager.Net.Session; if (s != null) s.EnemiesAlive.Value = alive.Count;
            if (Time.time < nextWave) return;
            nextWave = Time.time + Random.Range(45f, 90f);
            if (s != null && s.TimeLeft < 300) return;
            int want = Mathf.Max(targetCount / 2, 4);
            int add = Mathf.Min(4, want - alive.Count);
            for (int i = 0; i < add; i++) SpawnRandom(true);
        }

        string PickType()
        {
            float r = Random.value;
            if (World.Night || World.Underground) { if (r < 0.3f) return "cultist"; if (r < 0.55f) return "raider"; return "scav"; }
            if (r < 0.2f) return "raider"; return "scav";
        }

        void SpawnRandom(bool farFromPlayers = false)
        {
            if (World.EnemySpawns.Count == 0) return;
            Vector3 p = Vector3.zero; bool ok = false;
            for (int i = 0; i < 12 && !ok; i++)
            {
                p = World.EnemySpawns[Random.Range(0, World.EnemySpawns.Count)];
                ok = true;
                if (farFromPlayers) foreach (var pl in Game.Manager.Net.Players.Values) if (pl != null && Vector3.Distance(pl.transform.position, p) < 60f) { ok = false; break; }
            }
            if (!ok) return;
            string type = PickType();
            Spawn(type, p + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f)));
            if (type == "raider" && Random.value < 0.5f) Spawn("raider", p + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f)));
        }

        void SpawnBoss()
        {
            var boss = Database.Enemies.Values.FirstOrDefault(e => e.boss && e.map == World.Map.id);
            if (boss == null) return;
            var p = World.BossSpawn;
            var b = Spawn(boss.id, p);
            for (int i = 0; i < boss.guards; i++) Spawn(boss.guardType, p + new Vector3(Random.Range(-6f, 6f), 0, Random.Range(-6f, 6f)));
            bossSpawned = true;
            var s = Game.Manager.Net.Session; if (s != null) s.BossAlive.Value = true;
            if (Stats.Has("bossInfo")) Game.Manager.Net.Session?.AnnounceRpc($"Разведцентр: на карте замечен {boss.name}");
        }

        public EnemyAI Spawn(string defId, Vector3 pos, bool sniperPost = false)
        {
            var prefab = Game.Manager.Net.EnemyPrefab; var def = Database.Enemy(defId);
            if (prefab == null || def == null) return null;
            if (!sniperPost && NavMesh.SamplePosition(pos, out var hit, 6f, NavMesh.AllAreas)) pos = hit.position;
            var go = Instantiate(prefab, pos, Quaternion.Euler(0, Random.Range(0, 360), 0));
            var ai = go.GetComponent<EnemyAI>();
            ai.DefId.Value = defId;
            ai.WeaponId.Value = def.weapons.Count > 0 ? def.weapons[Random.Range(0, def.weapons.Count)] : "pm";
            ai.IsSniperPost = sniperPost;
            go.GetComponent<NetworkObject>().Spawn(true);
            alive.Add(ai);
            return ai;
        }
    }
}
