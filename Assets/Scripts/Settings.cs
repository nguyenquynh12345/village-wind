using UnityEngine;

namespace LangGio
{
    /// <summary>Cài đặt người chơi (lưu bằng PlayerPrefs, không nằm trong file làng).</summary>
    public static class Settings
    {
        public static float SfxVolume { get => Get("sfxVol", 0.8f); set => Set("sfxVol", value); }
        public static float AmbVolume { get => Get("ambVol", 0.6f); set => Set("ambVol", value); }
        public static bool Decor { get => Get("decor", 1f) > 0.5f; set { Set("decor", value ? 1f : 0f); Changed?.Invoke(); } }
        public static bool Shadows { get => Get("shadows", 1f) > 0.5f; set { Set("shadows", value ? 1f : 0f); Changed?.Invoke(); } }
        public static bool Vignette { get => Get("vignette", 1f) > 0.5f; set { Set("vignette", value ? 1f : 0f); Changed?.Invoke(); } }
        /// <summary>Tilt-shift: mờ xa / gần, làng trông như mô hình thu nhỏ.</summary>
        public static bool Miniature { get => Get("miniature", 1f) > 0.5f; set { Set("miniature", value ? 1f : 0f); Changed?.Invoke(); } }

        /// <summary>Đồ hoạ nhẹ cho máy yếu: tắt SSAO + làm mờ, giảm độ phân giải dựng hình, bớt hạt.</summary>
        public static bool Lite { get => Get("lite", 0f) > 0.5f; set { Set("lite", value ? 1f : 0f); Changed?.Invoke(); } }

        /// <summary>Mùa: 0 xuân, 1 hạ, 2 thu, 3 đông.</summary>
        public static int Season { get => Mathf.Clamp((int)Get("season", 1f), 0, 3); set { Set("season", value); Changed?.Invoke(); } }

        /// <summary>Tự chuyển mùa và thời tiết theo thời gian thực.</summary>
        public static bool SeasonAuto { get => Get("seasonAuto", 0f) > 0.5f; set { Set("seasonAuto", value ? 1f : 0f); Changed?.Invoke(); } }

        /// <summary>Số phút thực cho mỗi mùa khi bật SeasonAuto (mặc định 8 phút).</summary>
        public static float SeasonMinutes { get => Mathf.Clamp(Get("seasonMin", 8f), 1f, 30f); set { Set("seasonMin", Mathf.Clamp(value, 1f, 30f)); Changed?.Invoke(); } }

        /// <summary>Thời tiết: 0 nắng, 1 nhiều mây, 2 mưa.</summary>
        public static int Weather { get => Mathf.Clamp((int)Get("weather", 0f), 0, 2); set { Set("weather", value); Changed?.Invoke(); } }

        /// <summary>Cài đặt đồ hoạ đổi (cỏ trang trí, bóng đổ).</summary>
        public static event System.Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Changed = null;

        static float Get(string k, float def) => PlayerPrefs.GetFloat("LangGio." + k, def);

        static void Set(string k, float v)
        {
            if (Mathf.Approximately(Get(k, -1f), v)) return;
            PlayerPrefs.SetFloat("LangGio." + k, v);
        }
    }
}
