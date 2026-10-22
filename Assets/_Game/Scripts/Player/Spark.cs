using UnityEngine;

namespace Vykhod
{
    /// <summary>Простая частица: летит, падает, растёт/уменьшается и исчезает. Без системы частиц — дёшево и работает везде.</summary>
    public class Spark : MonoBehaviour
    {
        Vector3 vel; float life, age, grow; bool billboard; Vector3 startScale;
        public void Init(Vector3 velocity, float lifetime = 0.35f, float growTo = 0f, bool faceCamera = false)
        {
            vel = velocity; life = lifetime; grow = growTo; billboard = faceCamera; startScale = transform.localScale;
        }
        void Update()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            vel += Physics.gravity * Time.deltaTime * (grow > 0 ? 0.1f : 1f);
            transform.position += vel * Time.deltaTime;
            float t = age / life;
            if (grow > 0) transform.localScale = startScale * Mathf.Lerp(1f, grow, t);
            else transform.localScale = startScale * (1f - t * 0.7f);
            if (billboard && Camera.main != null) transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }
}
