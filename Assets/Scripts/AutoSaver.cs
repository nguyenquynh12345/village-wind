using UnityEngine;

namespace LangGio
{
    /// <summary>Tự động lưu mỗi 2 phút (nếu có thay đổi), khi mở menu tạm dừng và khi thoát game.</summary>
    public class AutoSaver : MonoBehaviour
    {
        public static AutoSaver I;
        /// <summary>Tắt hẳn việc lưu (chế độ chụp ảnh tự động: không ghi đè làng của người chơi).</summary>
        public static bool Disabled;
        const float Interval = 120f;

        public bool Dirty { get; private set; }
        float timer = Interval;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; Disabled = false; }

        void Awake()
        {
            I = this;
            History.Changed += MarkDirty;
        }

        void OnDestroy() => History.Changed -= MarkDirty;

        void MarkDirty()
        {
            if (GameUI.Screen != UiScreen.Main) Dirty = true;
        }

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = Interval;
            if (Dirty && GameUI.Screen == UiScreen.Play) SaveNow(false);
        }

        public void SaveNow(bool announce)
        {
            if (Disabled) return;
            try
            {
                SaveSystem.Save();
                Dirty = false;
                if (announce) GameUI.Toast("Đã lưu làng");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Lưu thất bại: " + ex.Message);
                GameUI.Toast("Lưu thất bại — xem Console");
            }
        }

        void OnApplicationQuit()
        {
            if (Dirty) SaveNow(false);
        }
    }
}
