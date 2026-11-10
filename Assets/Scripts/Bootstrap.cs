using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Tự dựng toàn bộ cảnh khi bấm Play — không cần kéo thả gì trong Editor.
    /// Chạy được với bất kỳ scene nào (kể cả scene "Untitled" mặc định).
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (GameObject.Find("LangGio")) return;

            // Tắt camera / đèn mặc định của scene để dùng của mình
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);

            var root = new GameObject("LangGio");

            // Mặt đất (phải có trước camera và công cụ)
            new GameObject("Terrain").AddComponent<HeightField>();

            // Camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraRig>();
            camGo.AddComponent<WalkMode>();

            // Mặt trời
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            QualitySettings.shadowDistance = 140f;

            var day = root.AddComponent<DayCycle>();
            day.sun = sun;
            day.cam = cam;

            // Âm thanh tự tổng hợp + cỏ hoa trang trí
            root.AddComponent<Sfx>();
            new GameObject("GroundDecor").AddComponent<GroundDecor>();
            new GameObject("OuterGround").AddComponent<OuterGround>();

            // Đồ thị tường thành: 1 vật duy nhất, luôn tồn tại
            EntityManager.Spawn("WallGraph", "{}");
            EntityManager.Spawn("Blocks", "{}");
            EntityManager.Spawn("Ivy", "{}");

            root.AddComponent<ToolManager>();
            root.AddComponent<Combos>();
            root.AddComponent<LivingWorld>();
            root.AddComponent<Fauna>();
            root.AddComponent<AmbientFx>();
            root.AddComponent<Seasons>();
            root.AddComponent<RenderLook>();
            root.AddComponent<AutoSaver>();
            root.AddComponent<GameUI>();
            GameUI.Screen = UiScreen.Main;
            if (ScreenshotTour.Requested) root.AddComponent<ScreenshotTour>();
        }
    }
}
