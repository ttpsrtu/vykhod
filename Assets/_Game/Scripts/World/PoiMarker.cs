using UnityEngine;

namespace Vykhod
{
    /// <summary>Точка интереса — для квестов "посетить"/"установить".</summary>
    public class PoiMarker : MonoBehaviour
    {
        public string Name;
        bool visited;
        void Update()
        {
            if (visited || Game.State != GameState.Raid) return;
            var lp = Game.Manager.Net.LocalPlayer; if (lp == null) return;
            if (Vector3.Distance(lp.transform.position, transform.position) < 14f)
            {
                visited = true;
                QuestSystem.OnVisit(World.Map.id, Name);
                Game.Profile.AddSkillXp("perception", 10);
                Game.Notify("Точка: " + Name);
            }
        }
    }
}
