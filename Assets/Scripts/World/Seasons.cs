using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Bốn mùa: chỉ đổi vài biến toàn cục cho shader (lá/cỏ ngả vàng cam, tuyết phủ mặt hướng lên),
    /// chuyển mượt trong ~2 giây nên mọi công trình đã xây đều đổi theo ngay.
    /// </summary>
    public class Seasons : MonoBehaviour
    {
        public static readonly string[] Names = { "Xuân", "Hạ", "Thu", "Đông" };
        static readonly string[] SeasonToasts = { "🌸 Sang xuân", "☀️ Sang hạ", "🍂 Sang thu", "❄️ Sang đông" };
        public static readonly string[] WeatherNames = { "Nắng", "Nhiều mây", "Mưa" };
        public static readonly string[] WeatherIcons = { "sun", "eye", "nature" };
        public static Seasons I;

        float spring, autumn, snow, overcast, wet, rainbowT;
        bool wasRaining;
        float lastSnowApplied = -1f;
        float seasonTimer, weatherTimer;
        int lastSeason = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        /// <summary>Độ u ám của trời (0 nắng → 1 mưa dày).</summary>
        public static float Overcast => I ? I.overcast : 0f;
        /// <summary>Mức mưa hiện tại (0..1): mặt đất ướt, hạt mưa.</summary>
        public static float Rain => I ? I.wet : 0f;

        /// <summary>Mức tuyết hiện tại (0..1), cho hiệu ứng hạt.</summary>
        public static float Snow => I ? I.snow : 0f;
        public static float Autumn => I ? I.autumn : 0f;

        void Awake()
        {
            I = this;
            lastSeason = Settings.Season;
            weatherTimer = Random.Range(120f, 300f);
            Target(out spring, out autumn, out snow);
            WeatherTarget(out overcast, out wet);
            Push();
        }

        static void Target(out float sp, out float au, out float sn)
        {
            int s = Settings.Season;
            sp = s == 0 ? 1f : 0f;
            au = s == 2 ? 1f : 0f;
            sn = s == 3 ? 1f : 0f;
        }

        static void WeatherTarget(out float oc, out float w)
        {
            int s = Settings.Weather;
            oc = s == 0 ? 0f : s == 1 ? 0.55f : 1f;
            w = s == 2 ? 1f : 0f;
        }

        static int RollWeather(int season)
        {
            float r = Random.value;
            return season switch
            {
                1 => r < 0.70f ? 0 : r < 0.85f ? 1 : 2,   // hạ: nắng nhiều
                2 => r < 0.25f ? 0 : r < 0.80f ? 1 : 2,   // thu: nhiều mây
                3 => r < 0.40f ? 0 : r < 0.70f ? 1 : 2,   // đông: không mưa mà tuyết (weather 2 = tuyết dày qua _LG_Snow)
                _ => r < 0.55f ? 0 : r < 0.85f ? 1 : 2,   // xuân: ôn hoà
            };
        }

        void Update()
        {
            if (Settings.Season != lastSeason)
            {
                lastSeason = Settings.Season;
                seasonTimer = 0f;
            }
            if (Settings.SeasonAuto && GameUI.Screen == UiScreen.Play)
            {
                float dt = Time.unscaledDeltaTime;
                seasonTimer += dt;
                float dur = Mathf.Max(1f, Settings.SeasonMinutes) * 60f;
                if (seasonTimer >= dur)
                {
                    seasonTimer = 0f;
                    int next = (Settings.Season + 1) % 4;
                    lastSeason = next;
                    Settings.Season = next;
                    GameUI.Toast(SeasonToasts[next]);
                }

                weatherTimer -= dt;
                if (weatherTimer <= 0f)
                {
                    weatherTimer = Random.Range(120f, 300f);
                    Settings.Weather = RollWeather(Settings.Season);
                }
            }

            WeatherTarget(out var oc, out var w);
            float kw = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 0.8f);
            float prevO = overcast;
            overcast = Mathf.Lerp(overcast, oc, kw);
            wet = Mathf.MoveTowards(wet, w, Time.unscaledDeltaTime * (w > wet ? 0.25f : 0.08f));   // khô chậm hơn ướt
            if ((Mathf.Abs(prevO - overcast) > 0.0005f || Mathf.Abs(lastSnowApplied - snow) > 0.002f) && DayCycle.I)
            {
                lastSnowApplied = snow;
                DayCycle.I.Refresh();
            }

            // cầu vồng: hiện sau khi tạnh mưa (không phải mùa đông, ban ngày), mờ dần trong ~40 s
            bool raining = Settings.Weather == 2;
            if (wasRaining && !raining && Settings.Season != 3) rainbowT = 45f;
            wasRaining = raining;
            rainbowT = Mathf.Max(0f, rainbowT - Time.unscaledDeltaTime);
            float rb = Mathf.Clamp01((45f - rainbowT) / 6f) * Mathf.Clamp01(rainbowT / 12f) * (DayCycle.IsEvening ? 0.4f : 1f);
            Shader.SetGlobalFloat("_LG_Rainbow", rb);
            Target(out var sp, out var au, out var sn);
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 1.6f);
            spring = Mathf.Lerp(spring, sp, k);
            autumn = Mathf.Lerp(autumn, au, k);
            snow = Mathf.Lerp(snow, sn, k);
            Push();
        }

        void Push()
        {
            Shader.SetGlobalFloat("_LG_Spring", spring);
            Shader.SetGlobalFloat("_LG_Autumn", autumn);
            Shader.SetGlobalFloat("_LG_Snow", snow);
            Shader.SetGlobalFloat("_LG_Wet", wet * (1f - snow));
        }
    }
}
