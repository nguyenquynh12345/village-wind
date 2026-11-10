using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>Đổi giờ trong ngày: xoay mặt trời, đổi màu ánh sáng, bầu trời và sương.</summary>
    public class DayCycle : MonoBehaviour
    {
        public static DayCycle I;
        public Light sun;
        public Camera cam;
        [Range(0, 1)] public float time = 0.72f;
        /// <summary>Giờ tự chạy: 1 phút thật = 1 giờ trong game.</summary>
        public bool autoRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        void Awake()
        {
            I = this;
            Settings.Changed += Apply;
        }

        void OnDestroy() => Settings.Changed -= Apply;

        /// <summary>Sau 18:30.</summary>
        public static bool IsNight => I && I.time >= 0.9f;
        /// <summary>Sau 17:00 (ống khói bắt đầu có khói).</summary>
        public static bool IsEvening => I && I.time >= 0.8f;

        void Update()
        {
            if (!autoRun) return;
            time += Time.deltaTime / 900f;
            if (time > 1f) time = 0f;
            Apply();
        }

        static readonly Color skyDay = Palette.Hex("#BFDCE8");
        static readonly Color skyLow = Palette.Hex("#F2C79A");
        static readonly Color sunDay = Palette.Hex("#FFF4E0");
        static readonly Color sunLow = Palette.Hex("#FFB070");
        static readonly Color skyNight = Palette.Hex("#3A4A6B");
        static readonly Color TopDay = Palette.Hex("#5E9BD3"), TopLow = Palette.Hex("#C9A8B8"), TopNight = Palette.Hex("#1F2A48");
        static readonly Color HorizonDay = Palette.Hex("#C6DDEA"), HorizonLow = Palette.Hex("#F6C99A"), HorizonNight = Palette.Hex("#4A5878");

        void Start() => Apply();

        public void Refresh() => Apply();

        public void SetTime(float t)
        {
            time = Mathf.Clamp01(t);
            Apply();
        }

        /// <summary>Ví dụ: "Hoàng hôn · 18:30" (05:00 → 20:00).</summary>
        public string Label
        {
            get
            {
                float h = 5f + time * 15f;
                int hh = (int)h, mm = (int)((h - hh) * 60f);
                string name = time < 0.18f ? "Bình minh" : time < 0.75f ? "Ban ngày" : "Hoàng hôn";
                return $"{name} · {hh:00}:{mm:00}";
            }
        }

        void Apply()
        {
            if (!sun || !cam) return;
            sun.shadows = Settings.Shadows ? LightShadows.Soft : LightShadows.None;
            float elev = Mathf.Lerp(6f, 62f, Mathf.Sin(time * Mathf.PI));
            float az = Mathf.Lerp(-70f, 250f, time);
            sun.transform.rotation = Quaternion.Euler(elev, az, 0);

            float k = Mathf.InverseLerp(6f, 62f, elev);
            float dusk = Mathf.InverseLerp(0.86f, 1f, time);   // chạng vạng → tối dần
            sun.color = Color.Lerp(Color.Lerp(sunLow, sunDay, k), skyNight, dusk * 0.5f);
            float oc = Seasons.Overcast;
            sun.intensity = Mathf.Lerp(1.35f, 1.9f, k) * (1f - 0.7f * dusk) * (1f - 0.55f * oc) * (1f - 0.18f * Seasons.Snow);

            Color sky = Color.Lerp(Color.Lerp(skyLow, skyDay, k), skyNight, dusk);
            cam.backgroundColor = sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            // ánh sáng môi trường dịu + hơi lạnh → phía khuất nắng xanh tím nhẹ, bóng đổ hiện rõ
            var cool = Palette.Hex("#9DB2D8");
            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(sky, cool, 0.4f), skyNight, dusk * 0.5f) * Mathf.Lerp(0.95f, 0.7f, dusk);
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(Color.Lerp(sky, Palette.Grass, 0.3f), cool, 0.2f), Palette.Hex("#E8D2C0"), 0.15f) * Mathf.Lerp(0.78f, 0.45f, dusk);
            RenderSettings.ambientGroundColor = Palette.Hex("#6F7F55") * 0.32f;

            // Bầu trời chuyển màu + hậu kỳ theo giờ (M14)
            Color top = Color.Lerp(Color.Lerp(TopLow, TopDay, k), TopNight, dusk);
            Color horizon = Color.Lerp(Color.Lerp(HorizonLow, HorizonDay, k), HorizonNight, dusk);
            top = Color.Lerp(top, Color.Lerp(Palette.Hex("#8E9AA6"), TopNight, dusk), oc * 0.7f);         // trời xám khi u ám
            horizon = Color.Lerp(horizon, Color.Lerp(Palette.Hex("#B9C0C4"), HorizonNight, dusk), oc * 0.6f);
            Color bottom = Color.Lerp(horizon, Palette.Hex("#A9C48B"), 0.45f * (1f - dusk));
            Color glow = Color.Lerp(sunLow, Palette.Hex("#FFF1D6"), k);
            if (RenderLook.I) RenderLook.I.SetTimeOfDay(top, horizon, bottom, -sun.transform.forward, glow, 1f - k, dusk);
            Shader.SetGlobalColor("_LG_SkyColor", Color.Lerp(horizon, top, 0.45f));
            Shader.SetGlobalFloat("_LG_CloudShadow", 0.38f * k * (1f - dusk) * (1f - 0.8f * oc));   // bóng mây trôi chỉ rõ khi nắng cao

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = RenderSettings.skybox ? horizon : sky;
            RenderSettings.fogStartDistance = Mathf.Lerp(90f, 35f, Seasons.Rain);   // mưa: xa mờ hơn
            RenderSettings.fogEndDistance = Mathf.Lerp(260f, 170f, Seasons.Rain);
        }
    }
}
