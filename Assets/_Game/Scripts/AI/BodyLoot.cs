using UnityEngine;

namespace Vykhod
{
    /// <summary>Метка на трупе: чей лут генерировать.</summary>
    public class BodyLoot : MonoBehaviour
    {
        public string EnemyId; public int Seed;
        public void Init(string id, int seed) { EnemyId = id; Seed = seed; }
    }
}
