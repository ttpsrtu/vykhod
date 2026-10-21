using UnityEngine;

namespace Vykhod
{
    /// <summary>Часть тела для попаданий.</summary>
    public class Hitbox : MonoBehaviour
    {
        public string part = "thorax";
        public float mult = 1f;
        public MonoBehaviour owner; // PlayerNet или EnemyAI
    }
}
