using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Chế độ chụp ảnh tự động (để kiểm tra hình ảnh không cần người chơi):
    ///   LangGio.exe -langgio-tour -tour-out "C:\duong\dan"
    /// Dựng 1 làng mẫu cố định, chụp nhiều góc ở nhiều giờ, ghi done.txt rồi thoát. Không lưu gì vào file làng.
    /// </summary>
    public class ScreenshotTour : MonoBehaviour
    {
        public static bool Requested => HasArg("-langgio-tour");

        static bool HasArg(string a)
        {
            foreach (var s in Environment.GetCommandLineArgs()) if (s == a) return true;
            return false;
        }

        static string ArgValue(string a)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == a) return args[i + 1];
            return null;
        }

        struct View
        {
            public string name;
            public float yaw, pitch, dist;
            public Vector3 target;
            public float[] times;
        }

        static readonly View[] Views =
        {
            new View { name = "overview", yaw = 35, pitch = 38, dist = 62, target = new Vector3(4, 0, 3), times = new[] { 0.45f, 0.83f, 0.97f } },
            new View { name = "houseA", yaw = 120, pitch = 24, dist = 16, target = new Vector3(-6, 2, 1), times = new[] { 0.45f, 0.83f } },
            new View { name = "gate", yaw = 0, pitch = 20, dist = 18, target = new Vector3(-4, 2, -10), times = new[] { 0.45f } },
            new View { name = "outside", yaw = 250, pitch = 28, dist = 22, target = new Vector3(16, 2, -3), times = new[] { 0.45f, 0.83f } },
            new View { name = "pond", yaw = 200, pitch = 32, dist = 24, target = new Vector3(20, 0, 15), times = new[] { 0.45f } },
            new View { name = "trees", yaw = 90, pitch = 30, dist = 22, target = new Vector3(-25, 1, 6), times = new[] { 0.45f } },
            new View { name = "horizon", yaw = 215, pitch = 12, dist = 34, target = new Vector3(2, 5, 2), times = new[] { 0.45f, 0.83f, 0.98f } },
        };

        /// <summary>
        /// Chụp cảnh 3D bằng RenderTexture (không cần cửa sổ hiển thị — vẫn chạy khi Remote Desktop ngắt).
        /// Song song vẫn ghi ảnh màn hình thường với hậu tố _screen nếu cần so sánh.
        /// </summary>
        static void Grab(string file)
        {
            var cam = Camera.main;
            if (!cam) { ScreenCapture.CaptureScreenshot(file); return; }
            var rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Destroy(tex);
        }

        IEnumerator Start()
        {
            AutoSaver.Disabled = true;
            Application.runInBackground = true;   // vẫn chạy khi cửa sổ không được chọn
            GameUI.HideAll = true;
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            string outDir = ArgValue("-tour-out") ?? Path.Combine(Application.persistentDataPath, "tour");
            Directory.CreateDirectory(outDir);
            var log = new List<string> { "Làng Gió tour " + DateTime.Now, "GPU: " + SystemInfo.graphicsDeviceName + " · " + SystemInfo.graphicsDeviceType,
                                         "Pipeline: " + (RenderLook.Urp ? "URP" : "Built-in") };
            yield return null;

            SaveSystem.NewVillage(false);
            GameUI.Screen = UiScreen.Play;
            yield return null;
            try { BuildVillage(); }
            catch (Exception ex) { log.Add("BuildVillage lỗi: " + ex); }
            yield return new WaitForSeconds(3f);   // tổ hợp, cỏ, thú vật, dây leo cập nhật xong

            var day = DayCycle.I;
            if (day) day.autoRun = false;

            // đo FPS: tổng quan ban ngày, rồi khi mưa
            IEnumerator Fps(string label)
            {
                yield return new WaitForSeconds(1f);
                int frames = 0; float t0 = Time.realtimeSinceStartup, worst = 0f;
                while (Time.realtimeSinceStartup - t0 < 4f) { frames++; worst = Mathf.Max(worst, Time.unscaledDeltaTime); yield return null; }
                float dt = Time.realtimeSinceStartup - t0;
                log.Add($"FPS {label}: {frames / dt:0.0} (khung chậm nhất {worst * 1000f:0} ms)");
            }
            var rigF = Camera.main ? Camera.main.GetComponent<CameraRig>() : null;
            if (rigF) rigF.SetState(35, 40, 45, Vector3.zero);
            if (day) day.SetTime(0.45f);
            yield return Fps("ban ngày");
            int keepW = Settings.Weather;
            Settings.Weather = 2;
            yield return new WaitForSeconds(4f);
            yield return Fps("mưa");
            Settings.Weather = keepW;
            bool keepLite = Settings.Lite;
            Settings.Lite = true;
            yield return new WaitForSeconds(4f);
            yield return Fps("đồ hoạ nhẹ");
            Settings.Lite = keepLite;
            var rig = Camera.main ? Camera.main.GetComponent<CameraRig>() : null;
            float lastTime = -1f;
            foreach (var v in Views)
                foreach (var t in v.times)
                {
                    if (day && !Mathf.Approximately(t, lastTime)) { day.SetTime(t); lastTime = t; yield return new WaitForSeconds(1.2f); }
                    if (rig) rig.SetState(v.yaw, v.pitch, v.dist, v.target);
                    yield return new WaitForSeconds(0.5f);
                    string file = Path.Combine(outDir, $"{v.name}_{Mathf.RoundToInt(t * 100):00}.png");
                    Grab(file);
                    yield return null;
                    yield return null;
                    log.Add("Chụp " + file);
                }

            // ---- Bốn mùa (giữ lại mùa người chơi đã chọn) ----
            int keepSeason = Settings.Season;
            if (day) day.SetTime(0.45f);
            if (rig) rig.SetState(35, 38, 40, new Vector3(0, 0, 0));
            for (int s = 0; s < 4; s++)
            {
                Settings.Season = s;
                yield return new WaitForSeconds(3.5f);    // chờ chuyển mùa + hạt rơi kín khung
                string file = Path.Combine(outDir, $"season_{s}.png");
                Grab(file);
                yield return null;
                yield return null;
                log.Add("Chụp " + file);
            }
            Settings.Season = keepSeason == 3 ? 1 : keepSeason;
            int keepWeather = Settings.Weather;
            Settings.Weather = 2;
            yield return new WaitForSeconds(6f);
            {
                string file = Path.Combine(outDir, "weather_rain.png");
                Grab(file);
                yield return null;
                yield return null;
            }
            // tạnh mưa → cầu vồng (sáng sớm, mặt trời thấp; chụp 2 hướng ngược nhau)
            Settings.Weather = 0;
            if (day) day.SetTime(0.18f);
            yield return new WaitForSeconds(7f);
            float az = Mathf.Lerp(-70f, 250f, 0.18f);
            for (int k = 0; k < 2; k++)
            {
                if (rig) rig.SetState(az + k * 180f, 15f, 30f, Vector3.zero);
                yield return new WaitForSeconds(0.5f);
                Grab(Path.Combine(outDir, $"rainbow_{k}.png"));
                yield return null;
            }
            Settings.Weather = keepWeather;
            Settings.Season = keepSeason;

            // ---- Ảnh có giao diện ----
            if (day) day.SetTime(0.45f);
            if (rig) rig.SetState(35, 38, 40, new Vector3(0, 0, 0));
            GameUI.HideAll = false;
            var tm = ToolManager.I;
            IEnumerator Shot(string name)
            {
                yield return new WaitForSeconds(0.6f);
                string file = Path.Combine(outDir, name + ".png");
                ScreenCapture.CaptureScreenshot(file);
                yield return null;
                yield return null;
                log.Add("Chụp " + file);
            }
            if (tm)
            {
                foreach (var t in tm.Tools) if (t.Key == "G") tm.Set(t);
                yield return Shot("ui_wall");
                foreach (var t in tm.Tools) if (t.Key == "N") tm.Set(t);
                yield return Shot("ui_block");
                tm.Set(tm.Hand);
                foreach (var h in EntityManager.OfType<HouseEntity>())
                    if (Mathf.Approximately(h.P.x, 17)) { tm.Hand.SelectExternal(h.Id, -1); break; }
                if (rig) rig.SetState(250, 30, 26, new Vector3(16, 2, 0));
                yield return Shot("ui_select");
                GameUI.JournalOpen = true;
                yield return Shot("ui_journal");
                GameUI.JournalOpen = false;
                GameUI.OpenPause();
                yield return Shot("ui_pause");
                GameUI.PauseTab = 1;
                yield return Shot("ui_pause_gfx");
                GameUI.PauseTab = 0;
                GameUI.Screen = UiScreen.Main;
                yield return Shot("ui_main");
            }

            // ---- Cảnh khởi đầu của "Làng mới" (đồi, hồ, rừng) ----
            GameUI.HideAll = true;
            GameUI.Screen = UiScreen.Play;
            UnityEngine.Random.InitState(1234);
            SaveSystem.NewVillage(true);
            if (day) day.SetTime(0.4f);
            yield return new WaitForSeconds(4f);
            log.Add("Cây trong cảnh mới: " + System.Linq.Enumerable.Count(EntityManager.OfType<TreeEntity>()));
            if (rig) rig.SetState(35, 32, 70, Vector3.zero);
            yield return Fps("cảnh mới");
            var land = new (string n, float yaw, float pitch, float dist)[] { ("land_top", 30, 70, 90), ("land_a", 35, 32, 70), ("land_b", 215, 22, 55), ("land_c", 120, 14, 40) };
            foreach (var v in land)
            {
                if (rig) rig.SetState(v.yaw, v.pitch, v.dist, Vector3.zero);
                yield return new WaitForSeconds(0.8f);
                Grab(Path.Combine(outDir, v.n + ".png"));
                yield return null;
            }

            yield return new WaitForSeconds(1f);
            log.Add("Xong");
            File.WriteAllLines(Path.Combine(outDir, "done.txt"), log);
            Application.Quit();
        }

        // ================= Làng mẫu =================
        static void BuildVillage()
        {
            // Vòng tường thành có tháp ở 4 góc
            var corners = new[] { new Vector3(-14, 0, -10), new Vector3(6, 0, -10), new Vector3(6, 0, 10), new Vector3(-14, 0, 10) };
            var path = new List<Vector3>();
            for (int i = 0; i < 4; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % 4];
                int steps = Mathf.CeilToInt(Vector3.Distance(a, b) / 0.8f);
                for (int k = 0; k < steps; k++) path.Add(Vector3.Lerp(a, b, k / (float)steps));
            }
            path.Add(corners[0]);
            WallGraph.I.AddStroke(path, new List<Vector3> { corners[1], corners[2], corners[3] }, true, GameConfig.WallHeightDefault, 0);

            // Nhà
            House(new HouseParams
            {
                x = -6, z = 1, w = 5, l = 8, floors = 2, mats = new() { 0, 0, 0, 0 }, roof = 0, pitch = 42, door = 0, seed = 11,
                roofColor = 0, doorColor = 1, wings = new() { new Wing { side = 1, off = 2, w = 4, d = 3, floors = 1 } }
            });
            House(new HouseParams { x = 1, z = -4, angle = 90, w = 4, l = 5, floors = 1, mats = new() { 3, 3, 3, 3 }, roof = 1, pitch = 38, door = 2, seed = 22, roofColor = 1, doorColor = 2 });
            House(new HouseParams { x = 17, z = 2, w = 4.5f, l = 7, floors = 2, mats = new() { 2, 1, 1, 1 }, roof = 0, pitch = 45, door = 1, seed = 33, roofColor = 0, doorColor = 3, dormers = 1, chimney = 1 });
            House(new HouseParams { x = 15, z = -9, w = 4, l = 4, floors = 3, mats = new() { 2, 2, 0, 0 }, roof = 3, pitch = 50, door = 1, seed = 44, roofColor = 2, doorColor = 0 });

            // Ao
            var poly = new List<Vector2>();
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                float r = 1f + 0.12f * Mathf.Sin(a * 3f);
                poly.Add(new Vector2(20f + Mathf.Cos(a) * 6f * r, 15f + Mathf.Sin(a) * 4.5f * r));
            }
            var mask = PondMask.Rasterize(poly);
            if (mask != null)
            {
                var pp = new PondParams { deep = false, seed = 5 };
                mask.WriteTo(pp);
                EntityManager.Spawn("Pond", JsonUtility.ToJson(pp));
            }

            // Đường: qua cổng thành, và qua ao (cầu gỗ)
            Road(new[] { new Vector2(-4, -26), new Vector2(-4, -16), new Vector2(-4, -8), new Vector2(-3, -2) }, 2.2f, 0);
            Road(new[] { new Vector2(14.5f, 2), new Vector2(18, 6), new Vector2(20, 12), new Vector2(21, 20), new Vector2(22, 26) }, 2f, 1);

            // Cây
            var rng = new System.Random(7);
            for (int i = 0; i < 14; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 1.5f + (float)rng.NextDouble() * 7f;
                Tree(-26f + Mathf.Cos(a) * r, 8f + Mathf.Sin(a) * r, rng.Next(3), 0.8f + (float)rng.NextDouble() * 0.6f);
            }
            Tree(13, 19, 0, 1.2f); Tree(27, 12, 0, 1.1f); Tree(26, 20, 0, 1f);
            Tree(-22, -18, 1, 1.3f); Tree(-18, -23, 1, 1.1f); Tree(28, -12, 1, 1.4f); Tree(-1, 6, 3, 1f);

            // Đồ trang trí
            Prop(PropEntity.Lantern, -2.5f, -16, 90); Prop(PropEntity.Lantern, -2.5f, -6, 90);
            Prop(PropEntity.Bench, 2, 5, 180); Prop(PropEntity.Barrel, 14.3f, 6, 0); Prop(PropEntity.Haystack, 22, -4, 0);
            Prop(PropEntity.Scarecrow, 24, -7, 30); Prop(PropEntity.FlowerPot, -3, -1, 0); Prop(PropEntity.Woodpile, 19.5f, -1, 90);

            // Tô đất: bãi hoa, sân sỏi, đất
            var hf = HeightField.I;
            hf.PaintBrush(new Vector3(15, 0, 22), 4f, 1);
            hf.PaintBrush(new Vector3(28, 0, 8), 3f, 1);
            hf.PaintBrush(new Vector3(-4, 0, 0), 4.5f, 2);
            hf.PaintBrush(new Vector3(22, 0, -5), 3f, 4);

            // Một chòi nhỏ bằng khối
            var bw = BlockWorld.I;
            if (bw)
            {
                float gy = hf.HeightAt(-24, -12);
                for (int i = -1; i <= 1; i++)
                    for (int j = -1; j <= 1; j++)
                        bw.Add(new Piece { t = BlockCatalog.Slab, m = 2, x = -24 + i, y = gy, z = -12 + j });
                for (int i = -1; i <= 1; i += 2)
                    for (int j = -1; j <= 1; j += 2)
                        for (int k = 0; k < 2; k++)
                            bw.Add(new Piece { t = BlockCatalog.Post, m = 3, x = -24 + i * 1.375f, y = gy + 0.1f + k, z = -12 + j * 1.375f });
                for (int i = -1; i <= 1; i++)
                {
                    bw.Add(new Piece { t = BlockCatalog.Slope, m = 6, x = -24 + i, y = gy + 2.1f, z = -13.5f, yaw = 0 });
                    bw.Add(new Piece { t = BlockCatalog.Slope, m = 6, x = -24 + i, y = gy + 2.1f, z = -10.5f, yaw = 180 });
                }
            }

            // Dây leo trên nhà A (mặt +x) và tường phía nam
            var ivy = IvyLayer.I;
            HouseEntity houseA = null;
            foreach (var h in EntityManager.OfType<HouseEntity>()) if (Mathf.Approximately(h.P.x, -6)) houseA = h;
            if (ivy && houseA)
                for (int i = 0; i < 6; i++)
                    ivy.AddPatch(houseA, new Vector3(-3.47f, houseA.BaseY + 0.6f + i * 0.35f, -3.2f + i * 0.25f), Vector3.right, 1.2f);
            if (ivy && WallGraph.I)
                for (int i = 0; i < 8; i++)
                    ivy.AddPatch(WallGraph.I, new Vector3(-12f + i * 0.6f, hf.HeightAt(-12f + i * 0.6f, -10.5f) + 0.8f + (i % 3) * 0.4f, -10.46f), Vector3.back, 1.1f);

            History.NotifyChanged();
        }

        static void House(HouseParams p) => EntityManager.Spawn("House", JsonUtility.ToJson(p));

        static void Road(Vector2[] pts, float width, int style)
        {
            var pp = new PathParams { width = width, style = style };
            var raw = new List<Vector2>(pts);
            for (int i = 0; i < raw.Count - 1; i++)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(raw[i], raw[i + 1]) / 0.8f);
                for (int k = 0; k < steps; k++) pp.pts.Add(Vector2.Lerp(raw[i], raw[i + 1], k / (float)steps));
            }
            pp.pts.Add(raw[raw.Count - 1]);
            EntityManager.Spawn("Path", JsonUtility.ToJson(pp));
        }

        static void Tree(float x, float z, int type, float size) =>
            EntityManager.Spawn("Tree", JsonUtility.ToJson(new TreeParams { x = x, z = z, type = type, size = size, rot = x * 37f + z * 11f }));

        static void Prop(int type, float x, float z, float yaw) =>
            EntityManager.Spawn("Prop", JsonUtility.ToJson(new PropParams { type = type, x = x, z = z, yaw = yaw, seed = (int)(x * 13 + z * 7) }));
    }
}
