using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Bộ vẽ UI phong cách Làng Gió (IMGUI): khối bo tròn có bóng mềm, biểu tượng từ ảnh atlas,
    /// chữ Segoe UI (có dấu tiếng Việt), nút tròn nảy nhẹ khi rê, thanh trượt, công tắc, chú thích.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Cream = Palette.Hex("#FFFBF3"), Cream2 = Palette.Hex("#F6EEDF"), Cream3 = Palette.Hex("#EDE2CC");
        public static readonly Color Ink = Palette.Hex("#3B2F2A"), InkSoft = Palette.Hex("#7A6A5F");
        public static readonly Color Accent = Palette.Hex("#C8664A"), AccentSoft = Palette.Hex("#F6DED4"), Moss = Palette.Hex("#5E8A4A");
        public static readonly Color Line = Palette.Hex("#E6DACB"), Gold = Palette.Hex("#D99A2B");

        static readonly string[] IconNames =
        {
            "hand", "build", "nature", "decor", "house", "window", "wall", "tower", "path", "block", "tree", "pond", "ground", "terrain",
            "prop", "ivy", "paint", "erase", "undo", "redo", "menu", "book", "sun", "moon", "camera", "eye", "eyeOff", "rotate", "copy",
            "trash", "plus", "minus", "close", "gear", "spark", "play", "save", "door", "exit", "raise", "lower", "flatten", "smooth"
        };
        static Dictionary<string, int> iconIndex;
        static Texture2D atlas;
        static Font font;
        static readonly Dictionary<string, float> hover = new();
        static float lastRepaint;
        static GUIStyle label;

        public static float S => Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
        public static int Px(float v) => Mathf.RoundToInt(v * S);
        public static bool Interactive = true;

        // Chú thích chờ vẽ cuối khung hình
        static string tipText, tipKey;
        static Rect tipRect;
        static bool tipBelow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            iconIndex = null; atlas = null; font = null; label = null;
            hover.Clear();
            styleCache.Clear();
        }

        static void Init()
        {
            if (iconIndex != null) return;
            iconIndex = new Dictionary<string, int>();
            for (int i = 0; i < IconNames.Length; i++) iconIndex[IconNames[i]] = i;
            atlas = Resources.Load<Texture2D>("UI/icons");
            // Font hệ thống (có dấu tiếng Việt). Không có thì dùng font mặc định của IMGUI.
            font = null;
            try
            {
                var names = Font.GetOSInstalledFontNames();
                foreach (var n in new[] { "Segoe UI", "Arial" })
                    if (System.Array.IndexOf(names, n) >= 0) { font = Font.CreateDynamicFontFromOSFont(n, 16); break; }
            }
            catch { font = null; }
        }

        /// <summary>Gọi đầu OnGUI.</summary>
        public static void BeginFrame(bool interactive)
        {
            Init();
            Interactive = interactive;
            if (Event.current.type == EventType.Repaint) tipText = null;
        }

        // ---------- Chữ ----------
        static readonly Dictionary<(int, Color, bool, TextAnchor, bool), GUIStyle> styleCache = new();

        public static GUIStyle Text(float size, Color color, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false)
        {
            var key = (Px(size), color, bold, align, wrap);
            if (styleCache.TryGetValue(key, out var cached)) return cached;
            if (styleCache.Count > 400) styleCache.Clear();
            label ??= new GUIStyle(GUI.skin.label) { richText = false, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            var st = new GUIStyle(label)
            {
                font = font,
                fontSize = Px(size),
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = align,
                wordWrap = wrap,
                clipping = TextClipping.Overflow
            };
            // cùng 1 màu cho mọi trạng thái (mặc định Unity đổi màu chữ khi rê / bấm → lẫn vào nền)
            st.normal.textColor = st.hover.textColor = st.active.textColor = st.focused.textColor = color;
            st.onNormal.textColor = st.onHover.textColor = st.onActive.textColor = st.onFocused.textColor = color;
            st.normal.background = st.hover.background = st.active.background = st.focused.background = null;
            styleCache[key] = st;
            return st;
        }

        public static void Label(Rect r, string text, float size, Color color, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false) =>
            GUI.Label(r, text, Text(size, color, bold, align, wrap));

        public static Vector2 Measure(string text, float size, bool bold = false) => Text(size, Ink, bold).CalcSize(new GUIContent(text));

        /// <summary>Chữ trắng có bóng (gợi ý nổi trên cảnh).</summary>
        public static void ShadowLabel(Rect r, string text, float size, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var sh = Text(size, new Color(0.16f, 0.11f, 0.09f, 0.65f), true, align);
            GUI.Label(new Rect(r.x + 1, r.y + Px(1.5f), r.width, r.height), text, sh);
            GUI.Label(r, text, Text(size, Cream, true, align));
        }

        // ---------- Khối ----------
        public static void Round(Rect r, Color c, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, radius);
        }

        public static void Outline(Rect r, Color c, float radius, float width)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, width, radius);
        }

        /// <summary>Khối kem bo tròn có bóng mềm bên dưới.</summary>
        public static void Panel(Rect r, float radius, float alpha = 0.95f)
        {
            if (Event.current.type != EventType.Repaint) return;
            for (int k = 3; k >= 1; k--)
            {
                float g = Px(3f * k);
                Round(new Rect(r.x - g * 0.5f, r.y - g * 0.3f + Px(4), r.width + g, r.height + g), new Color(0.23f, 0.18f, 0.16f, 0.05f), radius + g * 0.5f);
            }
            var c = Cream; c.a = alpha;
            Round(r, c, radius);
        }

        public static void Divider(float x, float y, float h) => Round(new Rect(x, y, Mathf.Max(1, Px(1)), h), Line, 0);

        // ---------- Biểu tượng ----------
        public static void Icon(Rect r, string name, Color c)
        {
            if (Event.current.type != EventType.Repaint || !atlas || !iconIndex.TryGetValue(name, out int i)) return;
            const float cols = 8f, rows = 6f;
            int col = i % 8, row = i / 8;
            var uv = new Rect(col / cols, 1f - (row + 1) / rows, 1f / cols, 1f / rows);
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTextureWithTexCoords(r, atlas, uv, true);
            GUI.color = old;
        }

        // ---------- Tương tác ----------
        static float Hover(string id, bool on)
        {
            hover.TryGetValue(id, out float h);
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Clamp(Time.unscaledTime - lastRepaint, 0f, 0.05f);
                h = Mathf.MoveTowards(h, on ? 1f : 0f, dt * 9f);
                hover[id] = h;
            }
            return h;
        }

        public static void EndFrame()
        {
            if (Event.current.type == EventType.Repaint) lastRepaint = Time.unscaledTime;
            DrawTip();
        }

        static bool Clicked(Rect r)
        {
            var e = Event.current;
            if (!Interactive || e.type != EventType.MouseDown || e.button != 0 || !r.Contains(e.mousePosition)) return false;
            e.Use();
            Sfx.Play(SfxId.Click, 1f, 0.5f);
            return true;
        }

        public static bool IsHover(Rect r) => Interactive && r.Contains(Event.current.mousePosition);

        public static void Tip(Rect r, string text, string key, bool below = false)
        {
            if (!IsHover(r)) return;
            tipText = text; tipKey = key; tipRect = r; tipBelow = below;
        }

        static void DrawTip()
        {
            if (tipText == null || Event.current.type != EventType.Repaint) return;
            var ts = Measure(tipText, 12.5f, true);
            float kw = string.IsNullOrEmpty(tipKey) ? 0 : Measure(tipKey, 11.5f, true).x + Px(12);
            float w = ts.x + kw + Px(20), h = Px(28);
            float x = Mathf.Clamp(tipRect.center.x - w * 0.5f, Px(6), Screen.width - w - Px(6));
            float y = tipBelow ? tipRect.yMax + Px(8) : tipRect.y - h - Px(8);
            var r = new Rect(x, y, w, h);
            Round(r, Ink, Px(10));
            Label(new Rect(r.x + Px(10), r.y, ts.x + 2, h), tipText, 12.5f, Cream, true);
            if (kw > 0)
            {
                var kr = new Rect(r.xMax - kw - Px(4), r.y + Px(5), kw - Px(4), h - Px(10));
                Round(kr, new Color(1, 1, 1, 0.16f), Px(5));
                Label(kr, tipKey, 11.5f, Cream, true, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>Nút biểu tượng tròn (nảy nhẹ khi rê). on = đang chọn.</summary>
        public static bool IconButton(Rect r, string id, string icon, bool on, string tip = null, string key = null, bool tipBelow = false)
        {
            bool hv = IsHover(r);
            float h = Hover(id, hv);
            var rr = new Rect(r.x - h * Px(1.5f), r.y - h * Px(3f) - h * Px(1.5f), r.width + h * Px(3f), r.height + h * Px(3f));
            if (on) Round(rr, AccentSoft, rr.width * 0.5f);
            else if (h > 0.01f) Round(rr, new Color(Cream2.r, Cream2.g, Cream2.b, h), rr.width * 0.5f);
            float ic = r.width * 0.46f;
            Icon(new Rect(rr.center.x - ic * 0.5f, rr.center.y - ic * 0.5f, ic, ic), icon, on ? Accent : (GUI.enabled ? Ink : InkSoft));
            if (tip != null) Tip(r, tip, key, tipBelow);
            return GUI.enabled && Clicked(r);
        }

        /// <summary>Nút chữ dạng viên thuốc (chip), có thể kèm biểu tượng.</summary>
        public static bool Chip(Rect r, string id, string text, bool on, string icon = null, float size = 13f)
        {
            bool hv = IsHover(r);
            float h = Hover(id, hv);
            var rr = new Rect(r.x, r.y - h * Px(2), r.width, r.height);
            if (on) Round(rr, AccentSoft, rr.height * 0.5f);
            else if (h > 0.01f) Round(rr, new Color(Cream2.r, Cream2.g, Cream2.b, h), rr.height * 0.5f);
            float x = rr.x + Px(12);
            if (icon != null)
            {
                float ic = rr.height * 0.52f;
                Icon(new Rect(rr.x + Px(9), rr.center.y - ic * 0.5f, ic, ic), icon, on ? Accent : Ink);
                x = rr.x + Px(9) + ic + Px(6);
            }
            Label(new Rect(x, rr.y, rr.xMax - x - Px(8), rr.height), text, size, on ? Accent : Ink, true);
            return Clicked(r);
        }

        public static float ChipWidth(string text, bool icon, float size = 13f) =>
            Measure(text, size, true).x + Px(24) + (icon ? Px(24) : 0);

        /// <summary>Nút lớn (menu): nền kem / nền đất nung, biểu tượng + chữ + dòng phụ.</summary>
        public static bool BigButton(Rect r, string id, string text, string sub, string icon, bool primary, bool ghost = false)
        {
            bool hv = IsHover(r) && GUI.enabled;
            float h = Hover(id, hv);
            var rr = new Rect(r.x, r.y - h * Px(2), r.width, r.height);
            Color bg = primary ? Color.Lerp(Accent, Palette.Hex("#B95A3F"), h) : ghost ? new Color(Cream2.r, Cream2.g, Cream2.b, h) : Color.Lerp(Cream2, Cream3, h);
            if (!GUI.enabled) bg.a *= 0.5f;
            Round(rr, bg, Px(16));
            Color fg = primary ? Cream : (GUI.enabled ? Ink : InkSoft);
            float ic = Px(22);
            Icon(new Rect(rr.x + Px(18), rr.center.y - ic * 0.5f, ic, ic), icon, fg);
            float tx = rr.x + Px(52);
            if (string.IsNullOrEmpty(sub)) Label(new Rect(tx, rr.y, rr.width - Px(60), rr.height), text, 16f, fg, true);
            else
            {
                Label(new Rect(tx, rr.y + rr.height * 0.18f, rr.width - Px(60), rr.height * 0.4f), text, 16f, fg, true);
                var sc = fg; sc.a = 0.75f;
                Label(new Rect(tx, rr.y + rr.height * 0.55f, rr.width - Px(60), rr.height * 0.3f), sub, 12f, sc);
            }
            return GUI.enabled && Clicked(r);
        }

        /// <summary>Ô màu tròn.</summary>
        public static bool Swatch(Rect r, Color c, bool on)
        {
            float g = IsHover(r) ? Px(2) : 0;
            var rr = new Rect(r.x - g, r.y - g, r.width + g * 2, r.height + g * 2);
            if (on) Round(new Rect(rr.x - Px(3), rr.y - Px(3), rr.width + Px(6), rr.height + Px(6)), Ink, rr.width);
            Round(new Rect(rr.x - Px(1.5f), rr.y - Px(1.5f), rr.width + Px(3), rr.height + Px(3)), Cream, rr.width);
            Round(rr, c, rr.width * 0.5f);
            return Clicked(r);
        }

        /// <summary>Thanh trượt bo tròn. Trả về giá trị mới (0..1).</summary>
        public static float Slider(Rect r, float v)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, r);
            var e = Event.current;
            var track = new Rect(r.x, r.center.y - Px(3), r.width, Px(6));
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (Interactive && e.button == 0 && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; v = Mathf.InverseLerp(r.x, r.xMax, e.mousePosition.x); e.Use(); GUI.changed = true; }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id) { v = Mathf.InverseLerp(r.x, r.xMax, e.mousePosition.x); e.Use(); GUI.changed = true; }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    break;
                case EventType.Repaint:
                    Round(track, Cream3, Px(3));
                    Round(new Rect(track.x, track.y, track.width * v, track.height), Accent * 0.9f + new Color(0, 0, 0, 0.1f), Px(3));
                    float t = Px(18);
                    var th = new Rect(Mathf.Lerp(r.x, r.xMax, v) - t * 0.5f, r.center.y - t * 0.5f, t, t);
                    Round(new Rect(th.x - Px(1), th.y + Px(1), th.width + Px(2), th.height + Px(2)), new Color(0, 0, 0, 0.18f), t);
                    Round(th, Cream, t * 0.5f);
                    Round(new Rect(th.x + Px(3), th.y + Px(3), th.width - Px(6), th.height - Px(6)), Accent, t);
                    break;
            }
            return Mathf.Clamp01(v);
        }

        /// <summary>Công tắc bật / tắt. Trả về giá trị mới.</summary>
        public static bool Switch(Rect r, bool on, string id)
        {
            float h = Hover(id + "_on", on);
            Round(r, Color.Lerp(Cream3, Moss, h), r.height * 0.5f);
            float k = r.height - Px(6);
            Round(new Rect(Mathf.Lerp(r.x + Px(3), r.xMax - Px(3) - k, h), r.y + Px(3), k, k), Color.white, k);
            return Clicked(r) ? !on : on;
        }
    }
}
