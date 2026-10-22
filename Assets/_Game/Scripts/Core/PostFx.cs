using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Vykhod
{
    /// <summary>
    /// Постобработка в стиле Escape from Duckov: мягкое затенение углов (AO), тёплая ACES-тонировка с насыщенностью и контрастом,
    /// лёгкий bloom на светлых поверхностях и фонарях, виньетка. Создаётся кодом, без ассетов профиля.
    /// </summary>
    public static class PostFx
    {
        static PostProcessProfile profile; static PostProcessVolume volume; static ColorGrading grading; static AmbientOcclusion ao; static Bloom bloom; static Vignette vignette;
        const int Layer = 31;

        public static void Attach(Camera cam)
        {
            if (cam == null || Game.Settings.quality == 0) return;
            var holder = Resources.Load<GameObject>("Prefabs/PostFx");
            var res = holder != null ? holder.GetComponent<PostFxHolder>()?.resources : null;
            if (res == null) { Debug.LogWarning("[PostFx] Prefabs/PostFx с PostProcessResources не найден — постобработка выключена"); return; }
            var layer = cam.GetComponent<PostProcessLayer>() ?? cam.gameObject.AddComponent<PostProcessLayer>();
            layer.Init(res);
            layer.volumeLayer = 1 << Layer; layer.volumeTrigger = cam.transform;
            layer.antialiasingMode = PostProcessLayer.Antialiasing.None; // MSAA уже включён на камере
            layer.fog.enabled = true; layer.fog.excludeSkybox = true;
            layer.stopNaNPropagation = true;
            EnsureVolume();
            Apply();
            Debug.Log("[PostFx] постобработка включена");
        }

        static void EnsureVolume()
        {
            if (volume != null) return;
            profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            ao = profile.AddSettings<AmbientOcclusion>(); ao.enabled.Override(true);
            ao.mode.Override(AmbientOcclusionMode.ScalableAmbientObscurance); ao.intensity.Override(1.15f); ao.radius.Override(0.55f); ao.quality.Override(AmbientOcclusionQuality.Medium); ao.ambientOnly.Override(false);
            bloom = profile.AddSettings<Bloom>(); bloom.enabled.Override(true);
            bloom.intensity.Override(1.4f); bloom.threshold.Override(1.45f); bloom.softKnee.Override(0.5f); bloom.diffusion.Override(7f); bloom.color.Override(new Color(1f, 0.96f, 0.9f));
            grading = profile.AddSettings<ColorGrading>(); grading.enabled.Override(true);
            grading.gradingMode.Override(GradingMode.HighDefinitionRange); grading.tonemapper.Override(Tonemapper.ACES);
            grading.postExposure.Override(0.3f); grading.contrast.Override(14f); grading.saturation.Override(16f); grading.temperature.Override(7f); grading.tint.Override(2f);
            grading.lift.Override(new Vector4(1f, 1f, 1.02f, -0.01f)); grading.gamma.Override(new Vector4(1f, 1f, 1f, 0.02f)); grading.gain.Override(new Vector4(1.03f, 1.0f, 0.96f, 0.02f));
            vignette = profile.AddSettings<Vignette>(); vignette.enabled.Override(true);
            vignette.intensity.Override(0.26f); vignette.smoothness.Override(0.5f); vignette.roundness.Override(1f); vignette.color.Override(new Color(0.02f, 0.02f, 0.04f));
            var go = new GameObject("PostFxVolume") { layer = Layer }; Object.DontDestroyOnLoad(go);
            volume = go.AddComponent<PostProcessVolume>(); volume.isGlobal = true; volume.profile = profile; volume.priority = 1;
        }

        /// <summary>Подстройка под ночь/метро (чуть холоднее и темнее) и качество (на низком AO выключен).</summary>
        public static void Apply()
        {
            if (volume == null) return;
            bool night = World.Night, under = World.Underground; int q = Game.Settings.quality;
            ao.enabled.Override(q >= 2); ao.quality.Override(q >= 2 ? AmbientOcclusionQuality.Medium : AmbientOcclusionQuality.Low);
            grading.temperature.Override(under ? -4f : night ? -8f : 7f);
            grading.postExposure.Override(under ? 0.15f : night ? 0.25f : 0.3f);
            grading.saturation.Override(under ? 4f : night ? 6f : 16f);
            bloom.intensity.Override(under || night ? 2.6f : 1.4f);
            vignette.intensity.Override(under ? 0.38f : 0.26f);
        }

        /// <summary>Временный «удар» по экрану (попадание/взрыв): усиление виньетки, плавно возвращается в Apply().</summary>
        public static void Punch(float amount)
        {
            if (vignette == null) return;
            vignette.intensity.Override(Mathf.Clamp(vignette.intensity.value + amount, 0.26f, 0.6f));
        }

        /// <summary>Плавный возврат виньетки к базовому значению (вызывается из HUD каждый кадр).</summary>
        public static void Relax(float dt)
        {
            if (vignette == null) return;
            float baseV = World.Underground ? 0.38f : 0.26f;
            if (vignette.intensity.value > baseV) vignette.intensity.Override(Mathf.MoveTowards(vignette.intensity.value, baseV, dt * 0.35f));
        }
    }
}
