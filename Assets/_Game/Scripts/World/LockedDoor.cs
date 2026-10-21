using UnityEngine;

namespace Vykhod
{
    /// <summary>Запертая дверь: ключ, отмычка (перк) или взлом.</summary>
    public class LockedDoor : MonoBehaviour
    {
        public int Id;
        public string KeyId;
        public bool Open;
        public string RoomName = "комната";
        public void SetOpen(bool open)
        {
            Open = open;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = !open;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = !open;
        }
    }
}
