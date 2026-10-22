using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Vykhod
{
    /// <summary>Держатель ссылки на ресурсы Post Processing Stack v2 (шейдеры). Префаб Resources/Prefabs/PostFx создаёт ProjectSetup.</summary>
    public class PostFxHolder : MonoBehaviour
    {
        public PostProcessResources resources;
    }
}
