using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    public enum UiScreen { Main, Play, Pause, Villages }

    /// <summary>
    /// Giao diện theo bản mẫu (Docs/UI/mockup.html): thanh biểu tượng nổi ở dưới (Bàn tay · Xây · Thiên nhiên · Trang trí · Xóa · Hoàn tác),
    /// khay công cụ theo nhóm, hàng tuỳ chọn theo công cụ, chip nổi cạnh vật đang chọn, đồng hồ, menu chính, tạm dừng, sổ khám phá, chụp ảnh.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public static bool MouseOverUI;
        public static UiScreen Screen = UiScreen.Main;
        public static bool JournalOpen;
        public static bool ShowHints = true;
        /// <summary>Chế độ chụp ảnh (F): ẩn UI, camera vẫn xoay được, Space = chụp.</summary>
        public static bool PhotoMode;
        /// <summary>Ẩn toàn bộ giao diện (chế độ chụp ảnh tự động).</summary>
        public static bool HideAll;
        /// <summary>Người chơi tự ẩn giao diện (F1) để ngắm làng.</summary>
        public static bool UiHidden;
        /// <summary>Frame mà Esc đã được dùng (để ToolManager không mở menu tạm dừng cùng lúc).</summary>
        public static int EscConsumedFrame = -1;
        /// <summary>Đang ở menu / sổ khám phá → công cụ và camera không nhận thao tác.</summary>
        public static bool Blocking => Screen != UiScreen.Play || JournalOpen;

        static readonly List<(string text, float until)> toasts = new();

        readonly List<Rect> rects = new();
        DayCycle day;
        int captureStep;
        float flashUntil;
        public static int PauseTab;
        string openGroup;       // nhóm đang mở khay: "build" / "nature" / "decor" / null
        Tool lastTool;
        Texture2D fadeTex;
        string renamingSlot;
        string renameBuf = "";
        string confirmDeleteSlot;
        float villageScroll;
        int journalPage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            MouseOverUI = false;
            Screen = UiScreen.Main;
            JournalOpen = false;
            ShowHints = true;
            PhotoMode = false;
            HideAll = false;
            UiHidden = false;
            EscConsumedFrame = -1;
            toasts.Clear();
        }

        public static void Toast(string msg)
        {
            toasts.Add((msg, Time.unscaledTime + 3.5f));
            if (toasts.Count > 4) toasts.RemoveAt(0);
        }

        public static void OpenPause()
        {
            if (ToolManager.I) ToolManager.I.ResetTools();
            Screen = UiScreen.Pause;
            if (AutoSaver.I && AutoSaver.I.Dirty) AutoSaver.I.SaveNow(false);
        }

        void Start() => day = GetComponent<DayCycle>();

        // ================= Nhóm công cụ (theo phím tắt của công cụ) =================
        static readonly (string id, string name, string icon, string[] keys)[] Groups =
        {
            ("build", "Xây", "build", new[] { "B", "W", "G", "K", "H", "N" }),
            ("nature", "Thiên nhiên", "nature", new[] { "T", "O", "L", "U" }),
            ("decor", "Trang trí", "decor", new[] { "D", "A", "P" }),
        };

        static readonly Dictionary<string, string> ToolIcon = new()
        {
            { "V", "hand" }, { "B", "house" }, { "W", "window" }, { "G", "wall" }, { "K", "tower" }, { "H", "path" }, { "N", "block" },
            { "T", "tree" }, { "O", "pond" }, { "L", "ground" }, { "U", "terrain" }, { "D", "prop" }, { "A", "ivy" }, { "P", "paint" }, { "X", "erase" }
        };

        static string GroupOf(Tool t)
        {
            if (t == null) return null;
            foreach (var g in Groups) foreach (var k in g.keys) if (k == t.Key) return g.id;
            return null;
        }

        static Tool ToolByKey(ToolManager tm, string key)
        {
            foreach (var t in tm.Tools) if (t.Key == key) return t;
            return null;
        }

        // ================= Chụp ảnh =================
        void TogglePhoto()
        {
            PhotoMode = !PhotoMode;
            var tm = ToolManager.I;
            if (PhotoMode) { if (tm) tm.Current.Exit(); }
            else
            {
                if (tm) tm.Current.Enter();
                EscConsumedFrame = Time.frameCount;
            }
        }

        void UpdatePhoto()
        {
            if (captureStep == 1)
            {
                var dir = System.IO.Path.Combine(Application.persistentDataPath, "photos");
                System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, $"LangGio_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
                ScreenCapture.CaptureScreenshot(path);
                captureStep = 2;
                return;
            }
            if (captureStep == 2)
            {
                captureStep = 0;
                flashUntil = Time.unscaledTime + 0.35f;
                Sfx.Play(SfxId.Shutter, 1f, 0.8f);
                Toast("Đã lưu ảnh vào thư mục photos");
                return;
            }
            if (!PhotoMode) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F)) { TogglePhoto(); return; }
            if (Input.GetKeyDown(KeyCode.Space)) captureStep = 1;
            if (day)
            {
                float dt = Time.unscaledDeltaTime * 0.08f;
                if (Input.GetKey(KeyCode.LeftBracket)) day.SetTime(day.time - dt);
                if (Input.GetKey(KeyCode.RightBracket)) day.SetTime(day.time + dt);
            }
        }

        void Update()
        {
            if (Screen != UiScreen.Play && PhotoMode) PhotoMode = false;
            if (WalkMode.Active) return;
            if (PhotoMode || captureStep > 0) { UpdatePhoto(); return; }
            if (Screen == UiScreen.Play && Input.GetKeyDown(KeyCode.F1)) UiHidden = !UiHidden;
            if (Screen == UiScreen.Play && !JournalOpen && !ToolManager.IsBusy && Input.GetKeyDown(KeyCode.F)) { TogglePhoto(); return; }
            if (Screen == UiScreen.Play && Input.GetKeyDown(KeyCode.J) && !ToolManager.IsBusy) JournalOpen = !JournalOpen;
            else if (JournalOpen && Input.GetKeyDown(KeyCode.Escape)) { JournalOpen = false; EscConsumedFrame = Time.frameCount; }
            else if (JournalOpen && Input.GetKeyDown(KeyCode.LeftArrow)) { journalPage = Mathf.Max(0, journalPage - 1); Sfx.Play(SfxId.Click, 1f, 0.5f); }
            else if (JournalOpen && Input.GetKeyDown(KeyCode.RightArrow)) { journalPage = Mathf.Min((Combos.Count - 1) / 15, journalPage + 1); Sfx.Play(SfxId.Click, 1f, 0.5f); }
            else if (Screen == UiScreen.Pause && Input.GetKeyDown(KeyCode.Escape)) Screen = UiScreen.Play;
            else if (Screen == UiScreen.Villages && Input.GetKeyDown(KeyCode.Escape))
            {
                if (renamingSlot != null) renamingSlot = null;
                else { confirmDeleteSlot = null; Screen = UiScreen.Main; }
            }
        }

        static float S => UiKit.S;
        static int Px(float v) => UiKit.Px(v);
        static float SW => UnityEngine.Screen.width;
        static float SH => UnityEngine.Screen.height;

        Rect Reg(Rect r) { rects.Add(r); return r; }

        // ================= Vẽ =================
        void OnGUI()
        {
            var tm = ToolManager.I;
            if (!tm || HideAll) return;
            rects.Clear();
            bool play = Screen == UiScreen.Play && !JournalOpen;
            UiKit.BeginFrame(play);

            if (WalkMode.Active)
            {
                DrawWalkHint();
                MouseOverUI = false;
                DrawToasts();
                UiKit.EndFrame();
                return;
            }

            if (PhotoMode || captureStep > 0)
            {
                if (captureStep == 0) DrawPhotoBar();
                MouseOverUI = false;
                DrawFlash();
                UiKit.EndFrame();
                return;
            }

            switch (Screen)
            {
                case UiScreen.Main:
                    UiKit.Interactive = true;   // menu chính phải nhận click
                    DrawMain();
                    break;
                case UiScreen.Villages:
                    UiKit.Interactive = true;
                    DrawVillages();
                    break;
                case UiScreen.Pause:
                    UiKit.Interactive = false;
                    DrawPlay(tm);
                    UiKit.Interactive = true;
                    DrawPause();
                    break;
                default:
                    if (UiHidden) DrawHiddenButton();
                    else
                    {
                        DrawPlay(tm);
                        if (JournalOpen) { UiKit.Interactive = true; DrawJournal(); }
                    }
                    break;
            }
            DrawToasts();
            DrawFlash();
            UiKit.EndFrame();

            if (Event.current.type == EventType.Repaint)
            {
                var mp = Event.current.mousePosition;
                bool over = Screen != UiScreen.Play || JournalOpen;
                foreach (var r in rects) if (r.Contains(mp)) { over = true; break; }
                MouseOverUI = over;
            }
        }

        // ================= Chế độ xây =================
        void DrawPlay(ToolManager tm)
        {
            float s = S, m = Px(18);
            var cur = tm.Current;
            if (cur != lastTool)
            {
                lastTool = cur;
                openGroup = GroupOf(cur);
            }

            // ---- Góc trái trên: menu + sổ khám phá ----
            {
                float b = Px(48), pad = Px(6);
                var r = Reg(new Rect(m, m, b * 2 + pad * 2 + Px(9), b + pad * 2));
                UiKit.Panel(r, r.height * 0.5f);
                if (UiKit.IconButton(new Rect(r.x + pad, r.y + pad, b, b), "menu", "menu", false, "Menu", "Esc", true)) OpenPause();
                UiKit.Divider(r.x + pad + b + Px(4), r.y + Px(14), r.height - Px(28));
                var bk = new Rect(r.x + pad + b + Px(9), r.y + pad, b, b);
                if (UiKit.IconButton(bk, "book", "book", JournalOpen, "Sổ khám phá", "J", true)) JournalOpen = !JournalOpen;
                int found = Combos.I ? Combos.I.FoundCount : 0;
                string badge = $"{found}/{Combos.Count}";
                var bs = UiKit.Measure(badge, 10f, true);
                var br = new Rect(bk.xMax - bs.x - Px(6), bk.y - Px(2), bs.x + Px(8), Px(17));
                UiKit.Round(new Rect(br.x - Px(2), br.y - Px(2), br.width + Px(4), br.height + Px(4)), UiKit.Cream, Px(10));
                UiKit.Round(br, UiKit.Accent, Px(9));
                UiKit.Label(br, badge, 10f, UiKit.Cream, true, TextAnchor.MiddleCenter);
            }

            // ---- Góc phải trên: đồng hồ + chụp ảnh / ẩn giao diện ----
            {
                float b = Px(48), pad = Px(6);
                var r2 = Reg(new Rect(SW - m - (b * 3 + pad * 2), m, b * 3 + pad * 2, b + pad * 2));
                UiKit.Panel(r2, r2.height * 0.5f);
                if (UiKit.IconButton(new Rect(r2.x + pad + b * 2, r2.y + pad, b, b), "cine", "play", false, "Ngắm cảnh", "F2", true))
                {
                    var rigC = Camera.main ? Camera.main.GetComponent<CameraRig>() : null;
                    if (rigC) rigC.StartCinematic();
                }
                if (UiKit.IconButton(new Rect(r2.x + pad, r2.y + pad, b, b), "photo", "camera", false, "Chụp ảnh", "F", true)) TogglePhoto();
                if (UiKit.IconButton(new Rect(r2.x + pad + b, r2.y + pad, b, b), "hide", "eyeOff", false, "Ẩn giao diện", "F1", true)) UiHidden = true;

                if (day)
                {
                    float cw = Px(250);
                    var cr = Reg(new Rect(r2.x - Px(10) - cw, m + Px(6), cw, b));
                    UiKit.Panel(cr, cr.height * 0.5f);
                    bool night = DayCycle.IsNight;
                    UiKit.Icon(new Rect(cr.x + Px(12), cr.center.y - Px(11), Px(22), Px(22)), night ? "moon" : "sun", night ? UiKit.InkSoft : UiKit.Gold);
                    float v = UiKit.Slider(new Rect(cr.x + Px(44), cr.y, cw - Px(112), cr.height), day.time);
                    if (!Mathf.Approximately(v, day.time)) day.SetTime(v);
                    float h = 5f + day.time * 15f;
                    int hh = (int)h, mm = (int)((h - hh) * 60f);
                    UiKit.Label(new Rect(cr.xMax - Px(60), cr.y, Px(50), cr.height), $"{hh:00}:{mm:00}", 13f, UiKit.Ink, true, TextAnchor.MiddleCenter);

                    // mùa: bấm để đổi Xuân → Hạ → Thu → Đông
                    string sn = Seasons.Names[Settings.Season];
                    float sw = UiKit.ChipWidth(sn, true) + Px(12);
                    var sr = Reg(new Rect(cr.xMax - sw, cr.yMax + Px(8), sw, Px(40)));
                    UiKit.Panel(sr, sr.height * 0.5f);
                    if (UiKit.Chip(new Rect(sr.x + Px(4), sr.y + Px(4), sw - Px(8), sr.height - Px(8)), "season", sn, false, "nature"))
                    {
                        Settings.Season = (Settings.Season + 1) % 4;
                        Toast("Mùa " + Seasons.Names[Settings.Season].ToLower());
                    }

                    // thời tiết: Nắng → Nhiều mây → Mưa
                    int wi = Settings.Weather;
                    string wn = Seasons.WeatherNames[wi];
                    float ww = UiKit.ChipWidth(wn, true) + Px(12);
                    var wr = Reg(new Rect(sr.x - Px(8) - ww, sr.y, ww, sr.height));
                    UiKit.Panel(wr, wr.height * 0.5f);
                    if (UiKit.Chip(new Rect(wr.x + Px(4), wr.y + Px(4), ww - Px(8), wr.height - Px(8)), "weather", wn, false, Seasons.WeatherIcons[wi]))
                    {
                        Settings.Weather = (wi + 1) % 3;
                        Toast(Seasons.WeatherNames[Settings.Weather]);
                    }
                }
            }

            // ---- Thanh dưới: Bàn tay | Xây Thiên nhiên Trang trí | Xóa | Hoàn tác Làm lại | Dạo làng ----
            float btn = Px(52), gap = Px(4), pd = Px(6);
            float dividers = 4 * Px(9);
            float dockW = pd * 2 + btn * 8 + gap * 7 + dividers;
            var dock = Reg(new Rect((SW - dockW) * 0.5f, SH - m - btn - pd * 2, dockW, btn + pd * 2));
            UiKit.Panel(dock, dock.height * 0.5f);
            float x = dock.x + pd, y = dock.y + pd;
            var hand = tm.Hand;
            if (UiKit.IconButton(new Rect(x, y, btn, btn), "t_V", "hand", cur == hand, hand.Name, "V")) { tm.Set(hand); openGroup = null; }
            x += btn + gap;
            UiKit.Divider(x + Px(2), y + Px(12), btn - Px(24)); x += Px(9);
            foreach (var g in Groups)
            {
                bool active = GroupOf(cur) == g.id;
                var gr = new Rect(x, y, btn, btn);
                if (UiKit.IconButton(gr, "g_" + g.id, g.icon, active || openGroup == g.id, g.name, null))
                {
                    if (openGroup == g.id && active) openGroup = null;
                    else
                    {
                        openGroup = g.id;
                        if (!active) tm.Set(ToolByKey(tm, g.keys[0]));
                        lastTool = tm.Current;
                    }
                }
                UiKit.Round(new Rect(gr.center.x - Px(2), gr.yMax - Px(9), Px(4), Px(4)), new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.4f), Px(2));
                x += btn + gap;
            }
            UiKit.Divider(x + Px(2), y + Px(12), btn - Px(24)); x += Px(9);
            var erase = ToolByKey(tm, "X");
            if (erase != null && UiKit.IconButton(new Rect(x, y, btn, btn), "t_X", "erase", cur == erase, erase.Name, "X")) { tm.Set(erase); openGroup = null; }
            x += btn + gap;
            UiKit.Divider(x + Px(2), y + Px(12), btn - Px(24)); x += Px(9);
            GUI.enabled = History.CanUndo && !ToolManager.IsBusy;
            if (UiKit.IconButton(new Rect(x, y, btn, btn), "undo", "undo", false, "Hoàn tác", "Z")) History.Undo();
            x += btn + gap;
            GUI.enabled = History.CanRedo && !ToolManager.IsBusy;
            if (UiKit.IconButton(new Rect(x, y, btn, btn), "redo", "redo", false, "Làm lại", "Y")) History.Redo();
            x += btn + gap;
            GUI.enabled = !ToolManager.IsBusy;
            UiKit.Divider(x + Px(2), y + Px(12), btn - Px(24)); x += Px(9);
            if (UiKit.IconButton(new Rect(x, y, btn, btn), "walk", "door", WalkMode.Active, "Dạo làng", "F3")) WalkMode.Toggle();
            GUI.enabled = true;

            float aboveY = dock.y - Px(10);

            // ---- Khay công cụ của nhóm đang mở ----
            if (openGroup != null)
            {
                string[] keys = null;
                foreach (var g in Groups) if (g.id == openGroup) keys = g.keys;
                float tb = Px(48);
                float trayW = pd * 2 + keys.Length * tb + (keys.Length - 1) * gap;
                var tray = Reg(new Rect((SW - trayW) * 0.5f, aboveY - tb - pd * 2, trayW, tb + pd * 2));
                UiKit.Panel(tray, tray.height * 0.5f);
                float tx = tray.x + pd;
                foreach (var k in keys)
                {
                    var t = ToolByKey(tm, k);
                    if (t != null && UiKit.IconButton(new Rect(tx, tray.y + pd, tb, tb), "t_" + k, ToolIcon[k], cur == t, t.Name, k)) { tm.Set(t); lastTool = t; }
                    tx += tb + gap;
                }
                aboveY = tray.y - Px(10);
            }

            // ---- Hàng tuỳ chọn của công cụ ----
            aboveY = DrawOptions(tm, aboveY);

            // ---- Thanh nội thất (đang xem trong nhà) ----
            aboveY = DrawInterior(tm, aboveY);

            // ---- Gợi ý thao tác ----
            if (ShowHints && !string.IsNullOrEmpty(cur.Hint))
            {
                var hs = UiKit.Measure(cur.Hint, 13f, true);
                UiKit.ShadowLabel(new Rect((SW - hs.x) * 0.5f - Px(10), aboveY - hs.y - Px(4), hs.x + Px(20), hs.y), cur.Hint, 13f);
            }

            DrawSelectionChip(tm);

            // ---- Trạng thái cạnh chuột ----
            string status = cur.Status;
            if (UiKit.Interactive && !string.IsNullOrEmpty(status) && !MouseOverUI && Event.current.type == EventType.Repaint)
            {
                var mp = Event.current.mousePosition;
                var ls = UiKit.Measure(status, 12.5f, true);
                var r = new Rect(mp.x + Px(18), mp.y + Px(18), ls.x + Px(20), Px(26));
                UiKit.Round(r, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.88f), Px(9));
                UiKit.Label(r, status, 12.5f, UiKit.Cream, true, TextAnchor.MiddleCenter);
            }
        }

        // ================= Hàng tuỳ chọn theo công cụ =================
        float DrawOptions(ToolManager tm, float aboveY)
        {
            var cur = tm.Current;
            var items = new List<(string text, string icon, bool on, System.Action act)>();
            var swatches = new List<(Color c, bool on, System.Action act)>();
            string stepLabel = null, stepValue = null;
            System.Action stepMinus = null, stepPlus = null, extraAct = null;
            string extraIcon = null, extraTip = null, swatchLabel = null;

            switch (cur.Key)
            {
                case "G":
                    for (int i = 0; i < WallGraph.StyleNames.Length; i++) { int k = i; items.Add((WallGraph.StyleNames[i], i == 0 ? "wall" : null, WallTool.Style == i, () => WallTool.Style = k)); }
                    stepLabel = "Cao"; stepValue = $"{GameConfig.WallHeights[Mathf.Clamp(WallTool.HeightIdx, 0, GameConfig.WallHeights.Length - 1)]:0.0} m";
                    stepMinus = () => WallTool.HeightIdx = Mathf.Max(0, WallTool.HeightIdx - 1);
                    stepPlus = () => WallTool.HeightIdx = Mathf.Min(GameConfig.WallHeights.Length - 1, WallTool.HeightIdx + 1);
                    break;
                case "U":
                {
                    var tt = tm.Terrain;
                    string[] ic = { "raise", "lower", "flatten", "smooth" };
                    for (int i = 0; i < 4; i++) { int k = i; items.Add((TerrainTool.ModeNames[i], ic[i], (int)tt.Mode == i, () => tt.Mode = (TerrainMode)k)); }
                    break;
                }
                case "T":
                    for (int i = 0; i < TreeEntity.TypeNames.Length; i++) { int k = i; items.Add((TreeEntity.TypeNames[i], i == 0 ? "tree" : null, TreeTool.Type == i, () => TreeTool.Type = k)); }
                    break;
                case "O":
                    items.Add(("Ao", "pond", !PondTool.RiverMode, () => PondTool.RiverMode = false));
                    items.Add(("Sông", null, PondTool.RiverMode, () => PondTool.RiverMode = true));
                    items.Add(("Nông", null, !PondTool.Deep, () => PondTool.Deep = false));
                    items.Add(("Sâu", null, PondTool.Deep, () => PondTool.Deep = true));
                    if (PondTool.RiverMode)
                    {
                        stepLabel = "Rộng"; stepValue = $"{PondTool.RiverWidth:0.#} m";
                        stepMinus = () => PondTool.RiverWidth = Mathf.Max(1.5f, PondTool.RiverWidth - 0.5f);
                        stepPlus = () => PondTool.RiverWidth = Mathf.Min(8f, PondTool.RiverWidth + 0.5f);
                    }
                    break;
                case "H":
                    for (int i = 0; i < PathEntity.StyleNames.Length; i++) { int k = i; items.Add((PathEntity.StyleNames[i], i == 0 ? "path" : null, PathTool.Style == i, () => PathTool.Style = k)); }
                    stepLabel = "Rộng"; stepValue = $"{PathTool.Width:0.#} m";
                    stepMinus = () => PathTool.Width = Mathf.Max(1f, PathTool.Width - 0.5f);
                    stepPlus = () => PathTool.Width = Mathf.Min(4f, PathTool.Width + 0.5f);
                    break;
                case "L":
                {
                    Color[] pc = { Palette.Hex("#8DB36B"), Palette.Hex("#C9DD9A"), Palette.Hex("#BDB5A6"), Palette.Hex("#E3D3A8"), Palette.Hex("#9C7A5B") };
                    for (int i = 0; i < pc.Length; i++) { int k = i; swatches.Add((pc[i], GroundPaintTool.Mode == i, () => GroundPaintTool.Mode = k)); }
                    swatchLabel = HeightField.PaintNames[Mathf.Clamp(GroundPaintTool.Mode, 0, HeightField.PaintNames.Length - 1)];
                    break;
                }
                case "N":
                    for (int i = 0; i < BlockCatalog.Count; i++) { int k = i; items.Add((BlockCatalog.Names[i], i == 0 ? "block" : null, BlockTool.Type == i, () => BlockTool.SetType(k))); }
                    for (int i = 0; i < BlockCatalog.MatColors.Length; i++) { int k = i; swatches.Add((BlockCatalog.MatColors[i], BlockTool.Mat == i, () => BlockTool.SetMat(k))); }
                    extraIcon = "rotate"; extraTip = $"Xoay ({BlockTool.UserYaw:0}°)";
                    extraAct = () => BlockTool.UserYaw = Mathf.Repeat(BlockTool.UserYaw + 90f, 360f);
                    break;
                case "W":
                    for (int i = 0; i < HouseEntity.OpeningNames.Length; i++) { int k = i; items.Add((HouseEntity.OpeningNames[i], i == 0 ? "window" : i == HouseEntity.WinDoor ? "door" : null, WindowTool.Kind == i, () => WindowTool.Kind = k)); }
                    break;
                case "D":
                    for (int i = 0; i < PropEntity.Names.Length; i++) { int k = i; items.Add((PropEntity.Names[i], i == 0 ? "prop" : null, PropTool.Type == i, () => PropTool.Type = k)); }
                    break;
                default:
                    return aboveY;
            }

            float ch = Px(38), gap = Px(4), pd = Px(6);
            float w = pd * 2;
            foreach (var it in items) w += UiKit.ChipWidth(it.text, it.icon != null, 12.5f) + gap;
            if (swatches.Count > 0) w += Px(14) + swatches.Count * (Px(26) + Px(8)) + (swatchLabel != null ? UiKit.Measure(swatchLabel, 12.5f, true).x + Px(16) : 0);
            if (stepLabel != null) w += Px(14) + UiKit.Measure(stepLabel, 12f, true).x + Px(10) + Px(130);
            if (extraIcon != null) w += Px(14) + ch;

            // Quá rộng (vd. 14 loại khối) → chia 2 hàng
            float maxW = SW - Px(80);
            bool twoRows = w > maxW;
            float rowH = ch + pd * 2;
            float totalH = twoRows ? rowH * 2 - pd : rowH;
            float pw = Mathf.Min(w, maxW);
            if (twoRows)
            {
                float w1 = pd * 2;
                foreach (var it in items) w1 += UiKit.ChipWidth(it.text, it.icon != null, 12.5f) + gap;
                pw = Mathf.Min(maxW, Mathf.Max(w1, w - w1 + pd * 2));
            }
            var r = Reg(new Rect((SW - pw) * 0.5f, aboveY - totalH, pw, totalH));
            UiKit.Panel(r, Px(22));
            float x = r.x + pd, y = r.y + pd;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                float cw = UiKit.ChipWidth(it.text, it.icon != null, 12.5f);
                if (x + cw > r.xMax - pd && !twoRows) break;
                if (UiKit.Chip(new Rect(x, y, cw, ch), "o_" + cur.Key + i, it.text, it.on, it.icon, 12.5f)) it.act();
                x += cw + gap;
            }
            if (twoRows) { x = r.x + pd; y += ch + pd; }
            if (swatches.Count > 0)
            {
                if (!twoRows) { UiKit.Divider(x + Px(4), y + Px(8), ch - Px(16)); x += Px(14); }
                foreach (var sw in swatches)
                {
                    if (UiKit.Swatch(new Rect(x, y + (ch - Px(26)) * 0.5f, Px(26), Px(26)), sw.c, sw.on)) sw.act();
                    x += Px(34);
                }
                if (swatchLabel != null)
                {
                    UiKit.Label(new Rect(x + Px(4), y, Px(200), ch), swatchLabel, 12.5f, UiKit.InkSoft, true);
                    x += UiKit.Measure(swatchLabel, 12.5f, true).x + Px(16);
                }
            }
            if (stepLabel != null)
            {
                UiKit.Divider(x + Px(4), y + Px(8), ch - Px(16)); x += Px(14);
                var lw = UiKit.Measure(stepLabel, 12f, true).x;
                UiKit.Label(new Rect(x, y, lw, ch), stepLabel, 12f, UiKit.InkSoft, true);
                x += lw + Px(10);
                var st = new Rect(x, y + Px(2), Px(126), ch - Px(4));
                UiKit.Round(st, UiKit.Cream2, st.height * 0.5f);
                if (UiKit.IconButton(new Rect(st.x + Px(2), st.y + Px(2), st.height - Px(4), st.height - Px(4)), "o_minus", "minus", false)) stepMinus();
                UiKit.Label(st, stepValue, 12.5f, UiKit.Ink, true, TextAnchor.MiddleCenter);
                if (UiKit.IconButton(new Rect(st.xMax - st.height + Px(2), st.y + Px(2), st.height - Px(4), st.height - Px(4)), "o_plus", "plus", false)) stepPlus();
                x += Px(130);
            }
            if (extraIcon != null)
            {
                UiKit.Divider(x + Px(4), y + Px(8), ch - Px(16)); x += Px(14);
                if (UiKit.IconButton(new Rect(x, y, ch, ch), "o_extra", extraIcon, false, extraTip, "R")) extraAct();
            }
            return r.y - Px(10);
        }

        // ================= Nội thất =================
        float DrawInterior(ToolManager tm, float aboveY)
        {
            var ih = Interior.House;
            if (!ih) return aboveY;
            float ch = Px(38), gap = Px(4), pd = Px(6);
            string label = $"Tầng {Interior.Floor + 1}/{ih.Floors}";
            float w = pd * 2 + UiKit.Measure(label, 13f, true).x + Px(20);
            foreach (int t in Furniture.Addable) w += UiKit.ChipWidth(Furniture.Name(t), true, 12.5f) + gap;
            w += Px(14) + ch * 3 + gap * 2;
            var r = Reg(new Rect((SW - w) * 0.5f, aboveY - ch - pd * 2, w, ch + pd * 2));
            UiKit.Panel(r, r.height * 0.5f);
            float x = r.x + pd + Px(8);
            UiKit.Label(new Rect(x, r.y, Px(200), r.height), label, 13f, UiKit.Ink, true);
            x += UiKit.Measure(label, 13f, true).x + Px(12);
            foreach (int t in Furniture.Addable)
            {
                float cw = UiKit.ChipWidth(Furniture.Name(t), true, 12.5f);
                if (UiKit.Chip(new Rect(x, r.y + pd, cw, ch), "f_" + t, Furniture.Name(t), false, "plus", 12.5f))
                {
                    var before = ih.Capture();
                    int part = ih.AddFurniture(t);
                    if (part >= 0)
                    {
                        History.Push(new ModifyEntityCommand(ih.Id, before, ih.Capture()));
                        if (tm.Current != tm.Hand) tm.Set(tm.Hand);
                        tm.Hand.SelectExternal(ih.Id, part);
                    }
                }
                x += cw + gap;
            }
            UiKit.Divider(x + Px(4), r.y + Px(14), r.height - Px(28)); x += Px(14);
            GUI.enabled = Interior.Floor < ih.Floors - 1;
            if (UiKit.IconButton(new Rect(x, r.y + pd, ch, ch), "f_up", "raise", false, "Lên tầng", "PgUp")) Interior.SetFloor(Interior.Floor + 1);
            x += ch + gap;
            GUI.enabled = Interior.Floor > 0;
            if (UiKit.IconButton(new Rect(x, r.y + pd, ch, ch), "f_dn", "lower", false, "Xuống tầng", "PgDn")) Interior.SetFloor(Interior.Floor - 1);
            x += ch + gap;
            GUI.enabled = true;
            if (UiKit.IconButton(new Rect(x, r.y + pd, ch, ch), "f_exit", "close", true, "Ra ngoài", "I")) Interior.Exit();
            return r.y - Px(10);
        }

        // ================= Chip nổi cạnh vật đang chọn =================
        void DrawSelectionChip(ToolManager tm)
        {
            if (tm.Current != tm.Hand) return;
            var hand = tm.Hand;
            if (hand.IsBoxSelecting && Event.current.type == EventType.Repaint)
            {
                var br = hand.BoxGuiRect;
                UiKit.Round(br, new Color(0.45f, 0.7f, 1f, 0.12f), Px(4));
                UiKit.Outline(br, new Color(0.45f, 0.7f, 1f, 0.85f), Px(4), Px(2));
            }
            var sel = hand.Selected;
            int multi = hand.MultiCount;
            string detail = hand.DetailLabel;
            if (!sel && multi == 0 && detail == null) return;

            string title = multi > 0 ? $"Đã chọn {multi} vật · Kéo để dời, R xoay, Ctrl+C chép" : sel ? sel.Describe : detail;
            // tách "Tên · chi tiết" thành 2 dòng cho gọn
            string head = title, sub = null;
            int cut = title.IndexOf(" · ");
            if (cut > 0) { head = title.Substring(0, cut); sub = title.Substring(cut + 3); }

            float b = Px(40), pd = Px(8);
            var hs = UiKit.Measure(head, 14f, true);
            float subW = Mathf.Min(Px(340), sub != null ? UiKit.Measure(sub, 12f).x : 0);
            float w = Mathf.Max(Mathf.Max(hs.x, subW) + pd * 2 + Px(8), b * 4 + pd * 2);
            float subH = sub != null ? UiKit.Text(12f, UiKit.InkSoft, false, TextAnchor.UpperLeft, true).CalcHeight(new GUIContent(sub), subW + 2) : 0;
            float h = pd + Px(22) + subH + Px(6) + b + pd;

            // vị trí: phía trên vật (nhà, cây, đồ...) · vật lớn (tường thành, khối) → giữa phía trên màn hình
            var cam = Camera.main;
            Vector2 anchor = new Vector2(SW * 0.5f, Px(90) + h);
            bool floating = false;
            if (sel && multi == 0 && cam && !(sel is WallGraph) && !(sel is BlockWorld) && !(sel is IvyLayer))
            {
                var rs = sel.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var bounds = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) bounds.Encapsulate(rs[i].bounds);
                    var sp = cam.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
                    if (sp.z > 0) { anchor = new Vector2(sp.x, SH - sp.y - Px(14)); floating = true; }
                }
            }
            var r = new Rect(anchor.x - w * 0.5f, anchor.y - h, w, h);
            r.x = Mathf.Clamp(r.x, Px(10), SW - w - Px(10));
            r.y = Mathf.Clamp(r.y, Px(80), SH - h - Px(160));
            Reg(r);
            UiKit.Panel(r, Px(18));
            if (floating && Event.current.type == EventType.Repaint)
            {
                float ax = Mathf.Clamp(anchor.x, r.x + Px(20), r.xMax - Px(20));
                var tri = new Rect(ax - Px(7), r.yMax - Px(7), Px(14), Px(14));
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, tri.center);
                UiKit.Round(tri, new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.95f), Px(3));
                GUI.matrix = m;
            }
            UiKit.Label(new Rect(r.x + pd + Px(4), r.y + pd, w, Px(22)), head, 14f, UiKit.Ink, true);
            if (sub != null)
                UiKit.Label(new Rect(r.x + pd + Px(4), r.y + pd + Px(22), subW + 2, subH), sub, 12f, UiKit.InkSoft, false, TextAnchor.UpperLeft, true);
            float x = r.x + pd, y = r.yMax - pd - b;
            if (sel || multi > 0)
            {
                GUI.enabled = multi > 0 || sel.CanRotate;
                if (UiKit.IconButton(new Rect(x, y, b, b), "s_rot", "rotate", false, "Xoay", "R")) hand.ActionRotate();
                x += b;
                GUI.enabled = hand.SelectedCanDuplicate;
                if (UiKit.IconButton(new Rect(x, y, b, b), "s_dup", "copy", false, "Nhân bản", "Ctrl D")) hand.ActionDuplicate();
                x += b;
                GUI.enabled = multi == 0 && sel is HouseEntity;
                bool inside = multi == 0 && sel is HouseEntity hh && Interior.IsViewing(hh.Id);
                if (UiKit.IconButton(new Rect(x, y, b, b), "s_in", "eye", inside, inside ? "Ra ngoài" : "Xem trong nhà", "I") && sel is HouseEntity he)
                {
                    if (Interior.IsViewing(he.Id)) Interior.Exit(); else Interior.Enter(he);
                }
                x += b;
                GUI.enabled = true;
            }
            if (UiKit.IconButton(new Rect(x, y, b, b), "s_del", "trash", false, sel || multi > 0 ? "Xoá" : "Tắt chi tiết này", "Del")) hand.ActionDelete();
        }

        // ================= Ẩn giao diện =================
        void DrawHiddenButton()
        {
            var r = Reg(new Rect(SW - Px(18) - Px(48), Px(18), Px(48), Px(48)));
            UiKit.Round(r, new Color(1f, 0.98f, 0.95f, 0.55f), r.width * 0.5f);
            if (UiKit.IconButton(r, "show", "eye", false, "Hiện giao diện", "F1", true)) UiHidden = false;
        }

        // ================= Dạo làng (S5) =================
        void DrawWalkHint()
        {
            string hint = WalkMode.IsRidingBoat
                ? "Đang ngồi thuyền  ·  Chuột: nhìn quanh  ·  E: lên bờ gần nhất  ·  F3 / Esc: thoát"
                : WalkMode.CanBoardBoat
                    ? "WASD: đi bộ  ·  Shift: chạy  ·  Chuột: nhìn quanh  ·  E: lên thuyền  ·  F3 / Esc: thoát"
                    : "WASD: đi bộ  ·  Shift: chạy  ·  Chuột: nhìn quanh  ·  F3 / Esc: thoát";
            var hs = UiKit.Measure(hint, 13f, true);
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.72f);
            UiKit.ShadowLabel(new Rect((SW - hs.x) * 0.5f - Px(10), SH - Px(44), hs.x + Px(20), hs.y), hint, 13f);
            GUI.color = old;
        }

        // ================= Chụp ảnh =================
        void DrawPhotoBar()
        {
            float m = Px(34), c = Px(46), t = Px(3);
            var col = new Color(1f, 0.98f, 0.95f, 0.85f);
            if (Event.current.type == EventType.Repaint)
            {
                // khung ngắm 4 góc
                foreach (var (cx, cy, sx, sy) in new[] { (m, m, 1, 1), (SW - m, m, -1, 1), (m, SH - m, 1, -1), (SW - m, SH - m, -1, -1) })
                {
                    UiKit.Round(new Rect(sx > 0 ? cx : cx - c, sy > 0 ? cy : cy - t, c, t), col, t);
                    UiKit.Round(new Rect(sx > 0 ? cx : cx - t, sy > 0 ? cy : cy - c, t, c), col, t);
                }
            }
            float bw = Px(330), bh = Px(72);
            var r = Reg(new Rect((SW - bw) * 0.5f, SH - Px(26) - bh, bw, bh));
            UiKit.Interactive = true;
            UiKit.Panel(r, bh * 0.5f);
            UiKit.Icon(new Rect(r.x + Px(18), r.center.y - Px(11), Px(22), Px(22)), DayCycle.IsNight ? "moon" : "sun", UiKit.Gold);
            if (day)
            {
                float v = UiKit.Slider(new Rect(r.x + Px(48), r.y, Px(140), bh), day.time);
                if (!Mathf.Approximately(v, day.time)) day.SetTime(v);
            }
            var sh = new Rect(r.x + Px(204), r.center.y - Px(29), Px(58), Px(58));
            UiKit.Round(sh, UiKit.Accent, sh.width * 0.5f);
            UiKit.Round(new Rect(sh.x + Px(4), sh.y + Px(4), sh.width - Px(8), sh.height - Px(8)), UiKit.Cream, sh.width);
            UiKit.Round(new Rect(sh.x + Px(7), sh.y + Px(7), sh.width - Px(14), sh.height - Px(14)), UiKit.Accent, sh.width);
            UiKit.Tip(sh, "Chụp", "Space");
            if (UiKit.IsHover(sh) && Event.current.type == EventType.MouseDown && Event.current.button == 0) { Event.current.Use(); captureStep = 1; }
            if (UiKit.IconButton(new Rect(r.xMax - Px(58), r.center.y - Px(22), Px(44), Px(44)), "ph_close", "close", false, "Thoát", "F")) TogglePhoto();

            // bộ lọc màu
            float fw = Px(8);
            for (int i = 0; i < RenderLook.FilterNames.Length; i++) fw += UiKit.ChipWidth(RenderLook.FilterNames[i], false) + Px(4);
            fw += Px(4);
            var fr = Reg(new Rect((SW - fw) * 0.5f, r.y - Px(12) - Px(44), fw, Px(44)));
            UiKit.Panel(fr, fr.height * 0.5f);
            float fx = fr.x + Px(6);
            for (int i = 0; i < RenderLook.FilterNames.Length; i++)
            {
                float w = UiKit.ChipWidth(RenderLook.FilterNames[i], false);
                if (UiKit.Chip(new Rect(fx, fr.y + Px(6), w, fr.height - Px(12)), "flt" + i, RenderLook.FilterNames[i], RenderLook.PhotoFilter == i))
                    RenderLook.PhotoFilter = i;
                fx += w + Px(4);
            }
        }

        // ================= Menu chính =================
        void DrawMain()
        {
            if (!fadeTex)
            {
                fadeTex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var c = UiKit.Cream;
                for (int i = 0; i < 256; i++)
                {
                    float t = i / 255f;
                    float a = t < 0.55f ? Mathf.Lerp(0.96f, 0.86f, t / 0.55f) : Mathf.Lerp(0.86f, 0f, (t - 0.55f) / 0.45f);
                    fadeTex.SetPixel(i, 0, new Color(c.r, c.g, c.b, a));
                }
                fadeTex.Apply();
            }
            Reg(new Rect(0, 0, SW, SH));
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(0, 0, SW * 0.62f, SH), fadeTex, ScaleMode.StretchToFill, true);
            float x = Px(96), y = SH * 0.24f;
            UiKit.Label(new Rect(x, y, Px(700), Px(110)), "Làng Gió", 88f, UiKit.Ink, true);
            UiKit.Icon(new Rect(x + UiKit.Measure("Làng Gió", 88f, true).x + Px(10), y + Px(16), Px(54), Px(54)), "ivy", UiKit.Moss);
            UiKit.Label(new Rect(x, y + Px(112), Px(600), Px(56)), "Vẽ một nét — ngôi làng tự mọc lên.\nXây chậm, ngắm lâu.", 17f, UiKit.InkSoft, false, TextAnchor.UpperLeft, true);
            float by = y + Px(196), bw = Px(320), bh = Px(64), gap = Px(10);
            var slots = SaveSystem.ListSlots();
            if (slots.Count > 0)
            {
                if (UiKit.BigButton(new Rect(x, by, bw, bh), "m_cont", "Tiếp tục", SaveSystem.SaveInfo, "play", true))
                {
                    if (SaveSystem.Load()) Screen = UiScreen.Play;
                    else Toast("Không mở được làng đã lưu");
                }
                by += bh + gap;
                if (UiKit.BigButton(new Rect(x, by, bw, bh - Px(8)), "m_list", "Các làng", $"{slots.Count} ngôi làng đã lưu", "book", false))
                {
                    renamingSlot = null;
                    confirmDeleteSlot = null;
                    villageScroll = 0f;
                    Screen = UiScreen.Villages;
                }
                by += bh - Px(8) + gap;
                if (UiKit.BigButton(new Rect(x, by, bw, bh - Px(8)), "m_new", "Làng mới", null, "plus", false)) { SaveSystem.NewVillage(); Screen = UiScreen.Play; }
            }
            else
            {
                if (UiKit.BigButton(new Rect(x, by, bw, bh), "m_new", "Làng mới", "Bắt đầu một ngôi làng", "play", true)) { SaveSystem.NewVillage(); Screen = UiScreen.Play; }
                by += bh + gap;
                if (UiKit.BigButton(new Rect(x, by, bw, bh - Px(8)), "m_list", "Các làng", null, "book", false))
                {
                    renamingSlot = null;
                    confirmDeleteSlot = null;
                    villageScroll = 0f;
                    Screen = UiScreen.Villages;
                }
            }
            by += bh + gap;
            if (UiKit.BigButton(new Rect(x, by, bw, bh - Px(8)), "m_quit", "Thoát", null, "exit", false, true)) Application.Quit();
            UiKit.Label(new Rect(x, SH - Px(44), Px(400), Px(20)), "v0.18 · bản dựng thử", 12f, UiKit.InkSoft, true);
        }

        // ================= Danh sách các làng =================
        void DrawVillages()
        {
            UiKit.Round(new Rect(0, 0, SW, SH), new Color(0.23f, 0.18f, 0.16f, 0.4f), 0);
            float cw = Px(304), ch = Px(256), gap = Px(16), pd = Px(28);
            float w = cw * 3 + gap * 2 + pd * 2;
            float h = Mathf.Min(SH - Px(48), ch * 2 + gap + Px(124));
            var r = Reg(new Rect((SW - w) * 0.5f, (SH - h) * 0.5f, w, h));
            UiKit.Panel(r, Px(26), 1f);

            var slots = SaveSystem.ListSlots();
            UiKit.Label(new Rect(r.x + pd, r.y + Px(22), Px(400), Px(38)), "Các làng", 28f, UiKit.Ink, true);
            UiKit.Label(new Rect(r.x + pd, r.y + Px(60), Px(520), Px(22)), "Bấm vào thẻ để mở làng · tạo mới hoặc quản lý các ngôi làng của bạn.", 13f, UiKit.InkSoft);

            float newW = UiKit.ChipWidth("Làng mới", true, 13f);
            if (UiKit.Chip(new Rect(r.xMax - pd - Px(44) - Px(12) - newW, r.y + Px(25), newW, Px(38)), "v_new", "Làng mới", true, "plus", 13f))
            {
                renamingSlot = null;
                confirmDeleteSlot = null;
                SaveSystem.NewVillage();
                Screen = UiScreen.Play;
                return;
            }
            if (UiKit.IconButton(new Rect(r.xMax - pd - Px(44), r.y + Px(22), Px(44), Px(44)), "v_close", "close", false, "Quay lại", "Esc", true))
            {
                renamingSlot = null;
                confirmDeleteSlot = null;
                Screen = UiScreen.Main;
                return;
            }

            var viewRect = new Rect(r.x + pd, r.y + Px(94), w - pd * 2, h - Px(94) - pd);
            int rows = Mathf.Max(1, (slots.Count + 2) / 3);
            float contentH = rows * ch + Mathf.Max(0, rows - 1) * gap;
            float maxScroll = Mathf.Max(0f, contentH - viewRect.height);
            if (Event.current.type == EventType.ScrollWheel && viewRect.Contains(Event.current.mousePosition))
            {
                villageScroll = Mathf.Clamp(villageScroll + Event.current.delta.y * Px(24), 0f, maxScroll);
                Event.current.Use();
            }
            villageScroll = Mathf.Clamp(villageScroll, 0f, maxScroll);

            if (slots.Count == 0)
            {
                UiKit.Label(viewRect, "Chưa có ngôi làng nào được lưu.", 15f, UiKit.InkSoft, true, TextAnchor.MiddleCenter);
                return;
            }

            GUI.BeginClip(viewRect);
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                int col = i % 3, row = i / 3;
                var c = new Rect(col * (cw + gap), row * (ch + gap) - villageScroll, cw, ch);
                if (c.yMax < 0 || c.y > viewRect.height) continue;

                bool hov = UiKit.IsHover(c);
                UiKit.Round(c, hov ? UiKit.Cream3 : UiKit.Cream2, Px(18));

                // Ảnh thu nhỏ 16:9
                float pad = Px(10);
                float tw = cw - pad * 2, th = Mathf.Round(tw * 9f / 16f);
                var tr = new Rect(c.x + pad, c.y + pad, tw, th);
                var thumb = SaveSystem.LoadThumb(s.slot);
                if (thumb)
                {
                    if (Event.current.type == EventType.Repaint)
                        GUI.DrawTexture(tr, thumb, ScaleMode.ScaleAndCrop, false);
                }
                else
                {
                    UiKit.Round(tr, UiKit.Cream, Px(12));
                    UiKit.Icon(new Rect(tr.center.x - Px(18), tr.center.y - Px(18), Px(36), Px(36)), "house", UiKit.InkSoft);
                }

                // Tên làng (hoặc ô nhập đổi tên)
                float ty = tr.yMax + Px(8);
                if (renamingSlot == s.slot)
                {
                    float saveChipW = UiKit.ChipWidth("Lưu", false, 12f);
                    var tfRect = new Rect(c.x + pad, ty, tw - saveChipW - Px(6), Px(28));
                    UiKit.Round(tfRect, UiKit.Cream, Px(8));
                    UiKit.Outline(tfRect, UiKit.Accent, Px(8), Px(1.5f));
                    GUI.SetNextControlName("rename_field");
                    var st = new GUIStyle(UiKit.Text(13.5f, UiKit.Ink, true, TextAnchor.MiddleLeft))
                    {
                        padding = new RectOffset(Px(8), Px(8), 0, 0),
                        clipping = TextClipping.Clip
                    };
                    renameBuf = GUI.TextField(tfRect, renameBuf ?? "", 40, st);
                    bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
                    if (UiKit.Chip(new Rect(tfRect.xMax + Px(6), ty, saveChipW, Px(28)), "v_rnsave_" + i, "Lưu", true, null, 12f) || enter)
                    {
                        if (enter) Event.current.Use();
                        SaveSystem.Rename(s.slot, renameBuf);
                        renamingSlot = null;
                        GUI.EndClip();
                        return;
                    }
                }
                else
                {
                    UiKit.Label(new Rect(c.x + pad + Px(2), ty, tw - Px(4), Px(22)), s.name, 15f, UiKit.Ink, true);
                }

                // Thời gian lưu + số công trình
                string meta = $"lưu {s.savedAt} · {s.entityCount} công trình";
                UiKit.Label(new Rect(c.x + pad + Px(2), ty + Px(23), tw - Px(4), Px(18)), meta, 11.5f, UiKit.InkSoft);

                // Hàng chip: Đổi tên · Nhân bản · Xoá
                float cy = c.yMax - pad - Px(28), chH = Px(28), cx = c.x + pad;
                float wRen = UiKit.ChipWidth("Đổi tên", false, 11.5f);
                if (UiKit.Chip(new Rect(cx, cy, wRen, chH), "v_ren_" + i, "Đổi tên", renamingSlot == s.slot, null, 11.5f))
                {
                    renamingSlot = renamingSlot == s.slot ? null : s.slot;
                    renameBuf = s.name;
                    confirmDeleteSlot = null;
                }
                cx += wRen + Px(4);

                float wDup = UiKit.ChipWidth("Nhân bản", false, 11.5f);
                if (UiKit.Chip(new Rect(cx, cy, wDup, chH), "v_dup_" + i, "Nhân bản", false, null, 11.5f))
                {
                    renamingSlot = null;
                    confirmDeleteSlot = null;
                    SaveSystem.Duplicate(s.slot);
                    Toast("Đã nhân bản làng");
                    GUI.EndClip();
                    return;
                }
                cx += wDup + Px(4);

                bool confirming = confirmDeleteSlot == s.slot;
                string delLabel = confirming ? "Chắc chưa?" : "Xoá";
                float wDel = UiKit.ChipWidth(delLabel, false, 11.5f);
                if (UiKit.Chip(new Rect(cx, cy, wDel, chH), "v_del_" + i, delLabel, confirming, null, 11.5f))
                {
                    renamingSlot = null;
                    if (confirming)
                    {
                        SaveSystem.Delete(s.slot);
                        confirmDeleteSlot = null;
                        Toast("Đã xoá làng");
                        GUI.EndClip();
                        return;
                    }
                    confirmDeleteSlot = s.slot;
                }

                // Click vào vùng trên của thẻ (ảnh / tên) → mở làng
                var openArea = new Rect(c.x, c.y, c.width, cy - c.y - Px(2));
                if (renamingSlot != s.slot && Event.current.type == EventType.MouseDown && Event.current.button == 0 && openArea.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    Sfx.Play(SfxId.Click, 1f, 0.5f);
                    renamingSlot = null;
                    confirmDeleteSlot = null;
                    if (SaveSystem.Load(s.slot)) Screen = UiScreen.Play;
                    else Toast("Không mở được làng đã lưu");
                    GUI.EndClip();
                    return;
                }
            }
            GUI.EndClip();
        }

        // ================= Tạm dừng =================
        void DrawPause()
        {
            UiKit.Round(new Rect(0, 0, SW, SH), new Color(0.23f, 0.18f, 0.16f, 0.4f), 0);
            float w = Px(520), pd = Px(28);
            float h = Px(PauseTab == 1 ? 600 : PauseTab == 2 ? 560 : 520);   // tab Hình ảnh / Chơi có nhiều dòng hơn
            var r = Reg(new Rect((SW - w) * 0.5f, (SH - h) * 0.5f, w, h));
            UiKit.Panel(r, Px(26), 1f);
            float x = r.x + pd, y = r.y + pd, bw = w - pd * 2;
            UiKit.Label(new Rect(x, y, bw, Px(40)), "Tạm dừng", 26f, UiKit.Ink, true);
            y += Px(50);
            if (UiKit.BigButton(new Rect(x, y, bw, Px(54)), "p_resume", "Tiếp tục", null, "play", true)) Screen = UiScreen.Play;
            y += Px(62);
            float half = (bw - Px(10)) * 0.5f;
            if (UiKit.BigButton(new Rect(x, y, half, Px(58)), "p_save", "Lưu làng", "Ctrl + S", "save", false) && AutoSaver.I) AutoSaver.I.SaveNow(true);
            if (UiKit.BigButton(new Rect(x + half + Px(10), y, half, Px(58)), "p_menu", "Menu chính", null, "door", false))
            {
                if (AutoSaver.I) AutoSaver.I.SaveNow(false);
                Screen = UiScreen.Main;
            }
            y += Px(72);

            // thẻ cài đặt
            var tabs = new Rect(x, y, bw, Px(40));
            UiKit.Round(tabs, UiKit.Cream2, Px(14));
            string[] names = { "Âm thanh", "Hình ảnh", "Chơi" };
            float tw = (bw - Px(8)) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var tr = new Rect(tabs.x + Px(4) + i * tw, tabs.y + Px(4), tw, tabs.height - Px(8));
                if (PauseTab == i) UiKit.Round(tr, UiKit.Cream, Px(10));
                UiKit.Label(tr, names[i], 13f, PauseTab == i ? UiKit.Ink : UiKit.InkSoft, true, TextAnchor.MiddleCenter);
                if (UiKit.IsHover(tr) && Event.current.type == EventType.MouseDown) { Event.current.Use(); PauseTab = i; Sfx.Play(SfxId.Click, 1f, 0.5f); }
            }
            y += Px(52);

            float rowH = Px(44);
            void SliderRow(string label, string sub, float v, System.Action<float> set)
            {
                UiKit.Label(new Rect(x + Px(4), y + Px(4), bw * 0.5f, Px(20)), label, 14f, UiKit.Ink, true);
                if (sub != null) UiKit.Label(new Rect(x + Px(4), y + Px(22), bw * 0.6f, Px(18)), sub, 11.5f, UiKit.InkSoft);
                float nv = UiKit.Slider(new Rect(x + bw - Px(170), y, Px(160), rowH), v);
                if (!Mathf.Approximately(nv, v)) set(nv);
                y += rowH;
            }
            void SwitchRow(string label, string sub, bool v, string id, System.Action<bool> set)
            {
                UiKit.Label(new Rect(x + Px(4), y + (sub != null ? Px(4) : Px(12)), bw * 0.7f, Px(20)), label, 14f, UiKit.Ink, true);
                if (sub != null) UiKit.Label(new Rect(x + Px(4), y + Px(22), bw * 0.7f, Px(18)), sub, 11.5f, UiKit.InkSoft);
                bool nv = UiKit.Switch(new Rect(x + bw - Px(52), y + Px(10), Px(44), Px(26)), v, id);
                if (nv != v) set(nv);
                y += rowH;
            }
            switch (PauseTab)
            {
                case 0:
                    SliderRow("Hiệu ứng", "Tiếng pốp, đặt, xoá, chuông khám phá", Settings.SfxVolume, v => Settings.SfxVolume = v);
                    SliderRow("Âm nền", "Gió, chim hót, dế kêu", Settings.AmbVolume, v => Settings.AmbVolume = v);
                    break;
                case 1:
                    SwitchRow("Cỏ và hoa trang trí", null, Settings.Decor, "sw_decor", v => Settings.Decor = v);
                    SwitchRow("Bóng đổ", null, Settings.Shadows, "sw_shadow", v => Settings.Shadows = v);
                    SwitchRow("Viền tối quanh màn hình", null, Settings.Vignette, "sw_vig", v => Settings.Vignette = v);
                    SwitchRow("Mô hình thu nhỏ", "Mờ xa / gần như nhìn mô hình", Settings.Miniature, "sw_mini", v => Settings.Miniature = v);
                    SwitchRow("Đồ hoạ nhẹ", "Cho máy yếu: bớt hiệu ứng, chạy mượt hơn", Settings.Lite, "sw_lite", v => Settings.Lite = v);
                    break;
                default:
                    if (day) SwitchRow("Giờ tự chạy", "1 phút thật = 1 giờ trong làng", day.autoRun, "sw_auto", v => day.autoRun = v);
                    SwitchRow("Mùa tự trôi", "Tự chuyển Xuân → Hạ → Thu → Đông & thời tiết", Settings.SeasonAuto, "sw_sauto", v => Settings.SeasonAuto = v);
                    {
                        float m = Mathf.Clamp(Settings.SeasonMinutes, 1f, 15f);
                        SliderRow($"Thời gian mỗi mùa ({Mathf.RoundToInt(m)} phút)", "1..15 phút thật cho một mùa", (m - 1f) / 14f, v => Settings.SeasonMinutes = Mathf.Round(1f + v * 14f));
                    }
                    SwitchRow("Hiện gợi ý thao tác", null, ShowHints, "sw_hint", v => ShowHints = v);
                    break;
            }
            if (UiKit.BigButton(new Rect(x, r.yMax - pd - Px(50), bw, Px(50)), "p_quit", "Thoát game", null, "exit", false, true))
            {
                if (AutoSaver.I) AutoSaver.I.SaveNow(false);
                Application.Quit();
            }
        }

        // ================= Sổ khám phá =================
        static readonly string[] JournalIcons =
        {
            "wall", "path", "wall", "pond", "tree", "house", "pond", "tower", "prop", "pond", "pond", "tower", "ground", "tree", "tower",
            "pond", "nature", "decor", "terrain", "pond", "pond"
        };
        static readonly Color[] JournalTints =
        {
            Palette.Hex("#E6DED2"), Palette.Hex("#E9DCC0"), Palette.Hex("#E6DED2"), Palette.Hex("#D6E9EF"), Palette.Hex("#DCEACB"),
            Palette.Hex("#F6DED4"), Palette.Hex("#D6E9EF"), Palette.Hex("#F6DED4"), Palette.Hex("#F6E3A9"), Palette.Hex("#D6E9EF"),
            Palette.Hex("#DCEACB"), Palette.Hex("#F6E3A9"), Palette.Hex("#DCEACB"), Palette.Hex("#DCEACB"), Palette.Hex("#F6DED4"),
            Palette.Hex("#D6E9EF"), Palette.Hex("#E9DCC0"), Palette.Hex("#F6E3A9"), Palette.Hex("#F6DED4"), Palette.Hex("#D6E9EF"), Palette.Hex("#D6E9EF")
        };

        void DrawJournal()
        {
            UiKit.Round(new Rect(0, 0, SW, SH), new Color(0.23f, 0.18f, 0.16f, 0.4f), 0);
            float cw = Px(186), ch = Px(150), gap = Px(12), pd = Px(30);
            float w = cw * 5 + gap * 4 + pd * 2, h = ch * 3 + gap * 2 + Px(156);
            var r = Reg(new Rect((SW - w) * 0.5f, (SH - h) * 0.5f, w, h));
            UiKit.Panel(r, Px(26), 1f);
            var combos = Combos.I;
            int found = combos ? combos.FoundCount : 0;
            UiKit.Label(new Rect(r.x + pd, r.y + Px(24), Px(400), Px(40)), "Sổ khám phá", 28f, UiKit.Ink, true);
            UiKit.Label(new Rect(r.x + pd, r.y + Px(64), w - pd * 2, Px(22)), "Đặt các thứ cạnh nhau để làng tự mọc thêm chi tiết. Ô chưa mở chỉ có một lời gợi ý.", 13f, UiKit.InkSoft);

            // tiến độ + đóng
            float px = r.xMax - pd - Px(44) - Px(20) - (Combos.Count * Px(15) + Px(50));
            for (int i = 0; i < Combos.Count; i++)
                UiKit.Round(new Rect(px + i * Px(15), r.y + Px(40), Px(10), Px(10)), combos && combos.Found[i] ? UiKit.Accent : UiKit.Cream3, Px(5));
            UiKit.Label(new Rect(px + Combos.Count * Px(15) + Px(4), r.y + Px(32), Px(50), Px(26)), $"{found}/{Combos.Count}", 13f, UiKit.InkSoft, true);
            if (UiKit.IconButton(new Rect(r.xMax - pd - Px(44), r.y + Px(22), Px(44), Px(44)), "j_close", "close", false, "Đóng", "J", true)) JournalOpen = false;

            int pages = (Combos.Count + 14) / 15;
            journalPage = Mathf.Clamp(journalPage, 0, pages - 1);
            int start = journalPage * 15;
            int end = Mathf.Min(Combos.Count, start + 15);

            for (int i = start; i < end; i++)
            {
                int idx = i - start;
                int col = idx % 5, row = idx / 5;
                var c = new Rect(r.x + pd + col * (cw + gap), r.y + Px(104) + row * (ch + gap), cw, ch);
                bool f = combos && combos.Found[i];
                if (f) UiKit.Round(c, UiKit.Cream2, Px(18));
                else UiKit.Outline(c, UiKit.Cream3, Px(18), Px(2));
                var em = new Rect(c.x + Px(14), c.y + Px(14), Px(42), Px(42));
                UiKit.Round(em, f ? JournalTints[i] : UiKit.Cream2, Px(14));
                if (f) UiKit.Icon(new Rect(em.x + Px(9), em.y + Px(9), Px(24), Px(24)), JournalIcons[i], UiKit.Ink);
                else UiKit.Label(em, "?", 20f, UiKit.InkSoft, true, TextAnchor.MiddleCenter);
                UiKit.Label(new Rect(c.x + Px(14), c.y + Px(62), cw - Px(24), Px(22)), f ? Combos.Names[i] : "Chưa tìm ra", 14.5f, f ? UiKit.Ink : UiKit.InkSoft, true);
                UiKit.Label(new Rect(c.x + Px(14), c.y + Px(86), cw - Px(24), ch - Px(92)), f ? Combos.Descs[i] : Combos.Hints[i], 11.5f, UiKit.InkSoft, false, TextAnchor.UpperLeft, true);
            }

            // chuyển trang ‹ › (hoặc phím ←/→)
            float navY = r.yMax - Px(42), navBtnW = Px(38), navH = Px(32), lblW = Px(84);
            float navX = r.center.x - (navBtnW * 2 + lblW) * 0.5f;
            GUI.enabled = journalPage > 0;
            if (UiKit.Chip(new Rect(navX, navY, navBtnW, navH), "j_prev", "‹", false, null, 16f)) journalPage--;
            GUI.enabled = true;
            UiKit.Label(new Rect(navX + navBtnW, navY, lblW, navH), $"Trang {journalPage + 1}/{pages}", 13f, UiKit.InkSoft, true, TextAnchor.MiddleCenter);
            GUI.enabled = journalPage < pages - 1;
            if (UiKit.Chip(new Rect(navX + navBtnW + lblW, navY, navBtnW, navH), "j_next", "›", false, null, 16f)) journalPage++;
            GUI.enabled = true;

            if (combos && combos.SuppressedCount > 0)
            {
                string t = $"Hiện lại {combos.SuppressedCount} chi tiết đã tắt";
                float bw = UiKit.ChipWidth(t, true, 12.5f);
                if (UiKit.Chip(new Rect(r.xMax - pd - bw, navY, bw, navH), "j_restore", t, false, "eye", 12.5f))
                    History.Execute(new ComboSuppressCommand(false, combos.Suppressed.ToArray()));
            }
        }

        // ================= Thông báo =================
        void DrawToasts()
        {
            float y = Px(84);
            toasts.RemoveAll(t => t.until < Time.unscaledTime);
            foreach (var (msg, until) in toasts)
            {
                bool disc = msg.StartsWith("Khám phá mới") || msg.StartsWith("✨ Khám phá mới");
                var ts = UiKit.Measure(msg, 14f, true);
                float h = Px(48);
                float w = ts.x + Px(30) + (disc ? Px(42) : 0);
                float age = 3.5f - (until - Time.unscaledTime);
                float k = Mathf.Clamp01(age / 0.25f), fade = Mathf.Clamp01((until - Time.unscaledTime) / 0.4f);
                var r = new Rect((SW - w) * 0.5f, y - (1f - k) * Px(16), w, h);
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Min(k, fade));
                UiKit.Panel(r, h * 0.5f);
                float x = r.x + Px(15);
                if (disc)
                {
                    var sp = new Rect(r.x + Px(8), r.y + Px(7), Px(34), Px(34));
                    UiKit.Round(sp, Palette.Hex("#F6E3A9"), Px(17));
                    UiKit.Icon(new Rect(sp.x + Px(7), sp.y + Px(7), Px(20), Px(20)), "spark", Palette.Hex("#B9811E"));
                    x = sp.xMax + Px(10);
                }
                UiKit.Label(new Rect(x, r.y, ts.x + 4, h), msg, 14f, UiKit.Ink, true);
                GUI.color = old;
                y += h + Px(8);
            }
        }

        void DrawFlash()
        {
            float a = (flashUntil - Time.unscaledTime) / 0.35f;
            if (a <= 0f || Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a * 0.8f);
            GUI.DrawTexture(new Rect(0, 0, SW, SH), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
