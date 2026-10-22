using System.Collections.Generic;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Эффекты и звук: следы попаданий, искры, вспышки, трассеры, взрывы. Звуки синтезируются, если ассетов нет.</summary>
    public static class Fx
    {
        static readonly Dictionary<string, AudioClip> synth = new Dictionary<string, AudioClip>();
        static Transform pool; static Material decalMat, sparkMat, flashMat, bloodMat;
        static Transform Pool { get { if (pool == null) { pool = new GameObject("FxPool").transform; Object.DontDestroyOnLoad(pool.gameObject); } return pool; } }
        static readonly List<GameObject> decals = new List<GameObject>();

        public static void Play(AudioClip clip, Vector3 pos, float vol = 1f, float pitch = 1f, float maxDist = 60f)
        {
            if (clip == null) return;
            var go = new GameObject("sfx"); go.transform.SetParent(Pool, false); go.transform.position = pos;
            var a = go.AddComponent<AudioSource>(); a.clip = clip; a.volume = vol; a.pitch = pitch; a.spatialBlend = 1f; a.maxDistance = maxDist; a.minDistance = 2f; a.rolloffMode = AudioRolloffMode.Linear; a.dopplerLevel = 0;
            a.Play(); Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        public static void Play2D(AudioClip clip, float vol = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("sfx2d"); go.transform.SetParent(Pool, false);
            var a = go.AddComponent<AudioSource>(); a.clip = clip; a.volume = vol; a.spatialBlend = 0f; a.Play(); Object.Destroy(go, clip.length + 0.1f);
        }

        public static AudioClip Synth(string key, float dur, float decay, float thumpHz, float thumpAmt, float noiseAmt, int lowpass = 1)
        {
            if (synth.TryGetValue(key, out var c)) return c;
            int sr = 22050; int n = (int)(sr * dur); var data = new float[n];
            var rng = new System.Random(key.GetHashCode()); float prev = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr; float env = Mathf.Exp(-t * decay);
                float noise = (float)(rng.NextDouble() * 2 - 1);
                for (int k = 1; k < lowpass; k++) noise = (noise + prev) * 0.5f;
                prev = noise;
                float thump = Mathf.Sin(2 * Mathf.PI * thumpHz * t) * Mathf.Exp(-t * decay * 1.5f);
                data[i] = Mathf.Clamp(noise * noiseAmt * env + thump * thumpAmt, -1f, 1f);
            }
            c = AudioClip.Create(key, n, 1, sr, false); c.SetData(data, 0);
            synth[key] = c; return c;
        }

        public static AudioClip GunClip(string cls, bool suppressed)
        {
            if (suppressed) return AssetCatalog.FindClip("suppress", "silenced") ?? Synth("supp", 0.18f, 30f, 120f, 0.3f, 0.5f, 4);
            var found = AssetCatalog.FindClip("gun_" + cls, cls == "sniper" ? "mosin" : cls == "shotgun" ? "shotty" : cls == "pistol" ? "cz" : "sks");
            if (found != null) return found;
            switch (cls)
            {
                case "pistol": return Synth("pistol", 0.22f, 22f, 160f, 0.5f, 0.9f, 2);
                case "smg": return Synth("smg", 0.2f, 24f, 140f, 0.5f, 0.9f, 2);
                case "shotgun": return Synth("shotgun", 0.4f, 12f, 70f, 0.9f, 1f, 3);
                case "sniper": return Synth("sniper", 0.5f, 9f, 60f, 1f, 1f, 2);
                case "lmg": return Synth("lmg", 0.3f, 14f, 80f, 0.8f, 1f, 2);
                case "dmr": return Synth("dmr", 0.35f, 12f, 75f, 0.8f, 1f, 2);
                default: return Synth("ar", 0.28f, 16f, 100f, 0.7f, 1f, 2);
            }
        }

        public static void Gunshot(Vector3 pos, string cls, bool suppressed, float vol = 1f)
            => Play(GunClip(cls, suppressed), pos, suppressed ? vol * 0.35f : vol, Random.Range(0.94f, 1.06f), suppressed ? 40f : 220f);

        static Material Unlit(Color c)
        {
            var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            var m = new Material(sh) { color = c, enableInstancing = true }; return m;
        }

        /// <summary>Трассер: тонкая светящаяся линия на долю секунды.</summary>
        public static void Tracer(Vector3 origin, Vector3 dir, float len, Color c)
        {
            if (Physics.Raycast(origin, dir, out var hit, len)) len = hit.distance;
            if (len < 1.5f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(Pool, false);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = origin + dir * len * 0.5f; go.transform.rotation = Quaternion.LookRotation(dir); go.transform.localScale = new Vector3(0.012f, 0.012f, len);
            go.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Emissive(c, 4f);
            Object.Destroy(go, 0.04f);
        }

        /// <summary>Вспышка выстрела: короткий свет + плоский яркий квад у дула.</summary>
        public static void MuzzleFlash(Transform gun)
        {
            if (gun == null) return;
            var go = new GameObject("flash"); go.transform.SetParent(gun, false); go.transform.localPosition = new Vector3(0, 0.02f, 0.5f);
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.range = 5; l.intensity = 2.5f; l.color = new Color(1f, 0.75f, 0.4f); l.shadows = LightShadows.None;
            if (flashMat == null) { flashMat = AssetCatalog.Emissive(new Color(1f, 0.85f, 0.5f), 8f); }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.transform.SetParent(go.transform, false); q.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360)); q.transform.localScale = Vector3.one * 0.14f; Object.Destroy(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = flashMat;
            var q2 = GameObject.CreatePrimitive(PrimitiveType.Quad); q2.transform.SetParent(go.transform, false); q2.transform.localRotation = Quaternion.Euler(0, 90, 0); q2.transform.localScale = Vector3.one * 0.12f; Object.Destroy(q2.GetComponent<Collider>()); q2.GetComponent<Renderer>().sharedMaterial = flashMat;
            Object.Destroy(go, 0.04f);
        }

        /// <summary>Попадание: плоский след на поверхности (не сфера!), искры или брызги.</summary>
        public static void Impact(Vector3 pos, Vector3 normal, bool flesh)
        {
            if (decalMat == null) { decalMat = Unlit(new Color(0.08f, 0.08f, 0.08f)); bloodMat = Unlit(new Color(0.45f, 0.03f, 0.03f)); sparkMat = AssetCatalog.Emissive(new Color(1f, 0.8f, 0.4f), 5f); }
            if (!flesh)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Quad); d.transform.SetParent(Pool, false); Object.Destroy(d.GetComponent<Collider>());
                d.transform.position = pos + normal * 0.012f; d.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0, 360)); d.transform.localScale = Vector3.one * Random.Range(0.06f, 0.1f);
                d.GetComponent<Renderer>().sharedMaterial = decalMat;
                decals.Add(d); if (decals.Count > 200) { var old = decals[0]; decals.RemoveAt(0); if (old != null) Object.Destroy(old); }
                for (int i = 0; i < 4; i++)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Cube); s.transform.SetParent(Pool, false); Object.Destroy(s.GetComponent<Collider>());
                    s.transform.position = pos + normal * 0.03f; s.transform.localScale = new Vector3(0.015f, 0.015f, 0.08f);
                    var dir = (normal + Random.insideUnitSphere * 0.8f).normalized; s.transform.rotation = Quaternion.LookRotation(dir);
                    s.GetComponent<Renderer>().sharedMaterial = sparkMat; s.AddComponent<Spark>().Init(dir * Random.Range(3f, 7f));
                }
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Quad); s.transform.SetParent(Pool, false); Object.Destroy(s.GetComponent<Collider>());
                    s.transform.position = pos; s.transform.localScale = Vector3.one * Random.Range(0.04f, 0.09f);
                    var dir = (normal + Random.insideUnitSphere).normalized; s.transform.rotation = Quaternion.LookRotation(Random.onUnitSphere);
                    s.GetComponent<Renderer>().sharedMaterial = bloodMat; s.AddComponent<Spark>().Init(dir * Random.Range(1f, 3f), 0.5f);
                }
            }
            Play(AssetCatalog.FindClip(flesh ? "flesh" : "impact", "hit") ?? Synth(flesh ? "flesh" : "impact", 0.1f, 40f, 200f, 0.2f, 0.5f, 3), pos, 0.45f, Random.Range(0.8f, 1.2f), 25f);
        }

        public static void Explosion(Vector3 pos, float radius)
        {
            var l = new GameObject("explLight").AddComponent<Light>(); l.transform.SetParent(Pool, false); l.transform.position = pos + Vector3.up * 0.5f; l.type = LightType.Point; l.range = radius * 4; l.intensity = 8; l.color = new Color(1f, 0.7f, 0.3f); Object.Destroy(l.gameObject, 0.25f);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Quad); ring.transform.SetParent(Pool, false); Object.Destroy(ring.GetComponent<Collider>());
            ring.transform.position = pos + Vector3.up * 0.05f; ring.transform.rotation = Quaternion.Euler(90, 0, 0); ring.transform.localScale = Vector3.one * radius * 0.5f;
            ring.GetComponent<Renderer>().sharedMaterial = AssetCatalog.Emissive(new Color(1f, 0.6f, 0.2f), 3f); ring.AddComponent<Spark>().Init(Vector3.zero, 0.25f, radius * 2.5f);
            if (sparkMat == null) sparkMat = AssetCatalog.Emissive(new Color(1f, 0.8f, 0.4f), 5f);
            for (int i = 0; i < 24; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cube); s.transform.SetParent(Pool, false); Object.Destroy(s.GetComponent<Collider>());
                s.transform.position = pos + Vector3.up * 0.3f; s.transform.localScale = new Vector3(0.05f, 0.05f, 0.25f);
                var dir = (Vector3.up * 0.6f + Random.insideUnitSphere).normalized; s.transform.rotation = Quaternion.LookRotation(dir);
                s.GetComponent<Renderer>().sharedMaterial = sparkMat; s.AddComponent<Spark>().Init(dir * Random.Range(6f, 14f), 0.9f);
            }
            var smokeMat = AssetCatalog.Mat(new Color(0.25f, 0.22f, 0.2f));
            for (int i = 0; i < 10; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.transform.SetParent(Pool, false); Object.Destroy(q.GetComponent<Collider>());
                q.transform.position = pos + Random.insideUnitSphere * radius * 0.3f + Vector3.up * 0.5f; q.transform.localScale = Vector3.one * Random.Range(1f, 2f);
                q.GetComponent<Renderer>().sharedMaterial = smokeMat; q.AddComponent<Spark>().Init(Vector3.up * Random.Range(1f, 3f) + Random.insideUnitSphere, 1.8f, 3f, true);
            }
            Play(AssetCatalog.FindClip("explosion", "boom") ?? Synth("expl", 1.2f, 4f, 45f, 1f, 1f, 4), pos, 1f, 1f, 400f);
        }

        public static void Smoke(Vector3 pos, float radius, float dur)
        {
            var root = new GameObject("smoke"); root.transform.SetParent(Pool, false); root.transform.position = pos;
            var m = AssetCatalog.Mat(new Color(0.75f, 0.75f, 0.77f));
            for (int i = 0; i < 14; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Quad); s.transform.SetParent(root.transform, false); Object.Destroy(s.GetComponent<Collider>());
                s.transform.localPosition = Random.insideUnitSphere * radius * 0.5f + Vector3.up * radius * 0.4f; s.transform.localScale = Vector3.one * radius * Random.Range(0.8f, 1.3f);
                s.GetComponent<Renderer>().sharedMaterial = m; s.AddComponent<Spark>().Init(Vector3.up * 0.2f, dur, 1.2f, true);
            }
            Object.Destroy(root, dur + 0.5f);
        }

        public static void Footstep(Vector3 pos, float vol)
        {
            var clips = AssetCatalog.FindClips("footstep_concrete", 5);
            var c = clips.Count > 0 ? clips[Random.Range(0, clips.Count)] : Synth("step" + Random.Range(0, 3), 0.08f, 60f, 90f, 0.15f, 0.35f, 5);
            Play(c, pos, vol, Random.Range(0.9f, 1.1f), 25f);
        }

        public static void Click() => Play2D(AssetCatalog.FindClip("click-a", "click", "select") ?? Synth("click", 0.04f, 90f, 900f, 0.3f, 0.1f, 1), 0.4f);
        public static void Reload(Vector3 pos) => Play(AssetCatalog.FindClip("reload_rifle", "reload", "magazine") ?? Synth("reload", 0.25f, 25f, 300f, 0.15f, 0.25f, 4), pos, 0.6f, 1f, 15f);
        public static void Heal(Vector3 pos) => Play(AssetCatalog.FindClip("heal", "bandage", "zip") ?? Synth("heal", 0.5f, 8f, 400f, 0.05f, 0.15f, 6), pos, 0.5f, 1f, 10f);
    }
}
