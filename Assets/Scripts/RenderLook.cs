using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LangGio
{
    /// <summary>
    /// "Vẻ ngoài" của game (M14): bầu trời chuyển màu, hậu kỳ URP (tô màu ấm, bloom, viền tối,
    /// tilt-shift làm làng trông như mô hình thu nhỏ), khử răng cưa. Nếu chưa chạy URP thì chỉ có bầu trời.
    /// </summary>
    public class RenderLook : MonoBehaviour
    {
        public static RenderLook I;
        public static bool Urp => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;

        Camera cam;
        CameraRig rig;
        Material sky;
        VolumeProfile profile;
        Vignette vignette;
        DepthOfField dof;
        ColorAdjustments grade;

        /// <summary>Bộ lọc ảnh (chỉ bật trong chế độ chụp): 0 gốc, 1 ấm, 2 mát, 3 cổ điển, 4 đen trắng.</summary>
        public static int PhotoFilter;
        public static readonly string[] FilterNames = { "Gốc", "Ấm", "Mát", "Cổ điển", "Đen trắng" };
        static readonly Color BaseFilter = new Color(1f, 0.98f, 0.95f);
        float fSat, fCon;
        Color fCol = BaseFilter;
        WhiteBalance balance;
        Bloom bloom;
        bool lowEnd;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        void Awake() => I = this;

        void Start()
        {
            cam = Camera.main;
            rig = cam ? cam.GetComponent<CameraRig>() : null;

            var sh = Shader.Find("LangGio/SkyGradient");
            if (sh)
            {
                sky = new Material(sh) { name = "Sky" };
                RenderSettings.skybox = sky;
                if (cam) cam.clearFlags = CameraClearFlags.Skybox;
            }

            if (Urp && cam) SetupUrp();
            Settings.Changed += Apply;
            Apply();
            if (DayCycle.I) DayCycle.I.Refresh();
        }

        void OnDestroy() => Settings.Changed -= Apply;

        void SetupUrp()
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;

            var go = new GameObject("PostFX");
            go.transform.SetParent(transform, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(0f);
            grade.contrast.Override(16f);
            grade.saturation.Override(0f);
            grade.colorFilter.Override(new Color(1f, 0.98f, 0.95f));

            balance = profile.Add<WhiteBalance>(true);
            balance.temperature.Override(6f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.93f, 0.95f, 1.08f, 0f));   // bóng hơi ngả xanh tím cho mát mắt

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.65f);

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.16f, 0.11f, 0.09f));

            dof = profile.Add<DepthOfField>(true);
            lowEnd = SystemInfo.graphicsMemorySize < 3000;   // card yếu (vd. GT 730): làm mờ xa kiểu Gaussian, nhẹ hơn Bokeh nhiều
            dof.mode.Override(lowEnd ? DepthOfFieldMode.Gaussian : DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(40f);
            dof.focalLength.Override(90f);
            dof.aperture.Override(3.2f);
        }

        void Apply()
        {
            bool lite = Settings.Lite;
            if (vignette) vignette.active = Settings.Vignette;
            if (dof) dof.active = Settings.Miniature && !lite;
            ApplyQuality(lite);
        }

        float baseScale = -1f; int baseMsaa; float baseShadowDist;

        /// <summary>Chất lượng dựng hình: chỉnh trực tiếp URP asset + bật/tắt SSAO (lưu lại giá trị gốc để trả về).</summary>
        void ApplyQuality(bool lite)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)) return;
            if (baseScale < 0f) { baseScale = asset.renderScale; baseMsaa = asset.msaaSampleCount; baseShadowDist = asset.shadowDistance; }
            asset.renderScale = lite ? 0.8f : baseScale;
            asset.msaaSampleCount = lite ? 1 : baseMsaa;
            asset.shadowDistance = lite ? Mathf.Min(baseShadowDist, 80f) : baseShadowDist;
            SetSsao(asset, !lite);
            if (cam)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.antialiasing = lite ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
        }

        static void SetSsao(UniversalRenderPipelineAsset asset, bool on)
        {
            var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (!(f?.GetValue(asset) is ScriptableRendererData[] list)) return;
            foreach (var d in list)
                if (d) foreach (var feat in d.rendererFeatures)
                        if (feat && feat.GetType().Name.Contains("Occlusion")) feat.SetActive(on);
        }

        void OnApplicationQuit()
        {
            // trong Editor, URP asset là file dự án: trả về chất lượng gốc để không lưu nhầm
            if (Application.isEditor && baseScale >= 0f) ApplyQuality(false);
        }

        void LateUpdate()
        {
            if (grade)
            {
                float sat = 0f, con = 16f; Color fc = BaseFilter;
                switch (GameUI.PhotoMode ? PhotoFilter : 0)
                {
                    case 1: sat = 10f; fc = new Color(1f, 0.93f, 0.82f); break;
                    case 2: sat = -6f; fc = new Color(0.88f, 0.96f, 1.05f); break;
                    case 3: sat = -32f; con = 6f; fc = new Color(1f, 0.9f, 0.74f); break;
                    case 4: sat = -100f; con = 26f; break;
                }
                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f);
                fSat = Mathf.Lerp(fSat, sat, k); fCon = Mathf.Lerp(fCon == 0 ? 16f : fCon, con, k); fCol = Color.Lerp(fCol, fc, k);
                grade.saturation.value = fSat;
                grade.contrast.value = fCon;
                grade.colorFilter.value = fCol;
            }
            // Tilt-shift: luôn lấy nét đúng chỗ camera đang nhìn
            if (dof && dof.active && cam && rig)
            {
                float d = Mathf.Max(1f, Vector3.Distance(cam.transform.position, rig.target));
                dof.focusDistance.value = d;
                dof.gaussianStart.value = d * 1.2f;
                dof.gaussianEnd.value = d * 2.6f;
            }
        }

        /// <summary>Gọi từ DayCycle mỗi khi đổi giờ.</summary>
        public void SetTimeOfDay(Color top, Color horizon, Color bottom, Vector3 sunDir, Color sunGlow, float dusk, float night)
        {
            if (sky)
            {
                sky.SetColor("_Top", top);
                sky.SetColor("_Horizon", horizon);
                sky.SetColor("_Bottom", bottom);
                sky.SetColor("_SunColor", sunGlow * (1f - night));
                sky.SetVector("_SunDir", sunDir);
                sky.SetColor("_CloudColor", Color.Lerp(Color.white, new Color(1f, 0.8f, 0.7f), dusk) * Mathf.Lerp(1f, 0.45f, night));
                sky.SetFloat("_Clouds", 0.55f + 0.45f * Seasons.Overcast);
                sky.SetFloat("_Night", night);
            }
            if (balance) balance.temperature.value = Mathf.Lerp(6f, 24f, dusk) - night * 30f;
            if (grade) grade.postExposure.value = Mathf.Lerp(0f, 0.05f, night);
            if (bloom) bloom.intensity.value = Mathf.Lerp(0.55f, 1.1f, night);
        }
    }
}
