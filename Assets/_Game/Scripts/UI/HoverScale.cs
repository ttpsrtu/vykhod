using UnityEngine;
using UnityEngine.EventSystems;

namespace Vykhod
{
    /// <summary>Лёгкое увеличение элемента при наведении (ощущение «живого» меню).</summary>
    public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float Scale = 1.05f;
        float target = 1f;
        void OnEnable() { target = 1f; transform.localScale = Vector3.one; }
        public void OnPointerEnter(PointerEventData e) { target = Scale; }
        public void OnPointerExit(PointerEventData e) { target = 1f; }
        void Update() { transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, Time.unscaledDeltaTime * 14f); }
    }
}
