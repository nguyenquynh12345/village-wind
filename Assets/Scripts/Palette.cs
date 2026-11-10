using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>Màu lấy từ Style Guide trong Figma (collection "Làng Gió / Colors").</summary>
    public static class Palette
    {
        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static readonly Color Grass = Hex("#8DB36B");
        public static readonly Color Moss = Hex("#5E8A4A");
        public static readonly Color Stone = Hex("#D8CFC2");
        public static readonly Color StoneLight = Hex("#E6DED2");
        public static readonly Color StoneDark = Hex("#C2B8AA");
        public static readonly Color Roof = Hex("#C8664A");
        public static readonly Color Wood = Hex("#7A5A44");
        public static readonly Color Sky = Hex("#BFDCE8");
        public static readonly Color Cream = Hex("#FFFBF3");
        public static readonly Color Ink = Hex("#3B2F2A");
        public static readonly Color Accent = Hex("#C8664A");

        static readonly Dictionary<(Color, int), Material> cache = new();

        /// <summary>Hoa văn bề mặt vẽ trong shader.</summary>
        public const int PatNone = 0, PatPlaster = 1, PatWood = 2, PatStone = 3, PatBrick = 4, PatGrass = 5, PatDirt = 6, PatCobble = 7;

        static Material waterMat;
        static Material riverWaterMat;

        /// <summary>Mặt nước ao (shader LangGio/Water; không có thì dùng màu trơn).</summary>
        public static Material WaterMat(Color fallback)
        {
            if (waterMat) return waterMat;
            var sh = Shader.Find("LangGio/Water");
            waterMat = sh ? new Material(sh) { name = "Water" } : new Material(Mat(fallback));
            if (waterMat.HasProperty("_FlowSpeed")) waterMat.SetFloat("_FlowSpeed", 0f);
            return waterMat;
        }

        /// <summary>Mặt nước sông chảy xuôi dòng (_FlowSpeed = 0.6).</summary>
        public static Material RiverWaterMat(Color fallback)
        {
            if (riverWaterMat) return riverWaterMat;
            var sh = Shader.Find("LangGio/Water");
            riverWaterMat = sh ? new Material(sh) { name = "RiverWater" } : new Material(Mat(fallback));
            if (riverWaterMat.HasProperty("_FlowSpeed")) riverWaterMat.SetFloat("_FlowSpeed", 0.6f);
            return riverWaterMat;
        }
        static Shader lit;
        static Material vertexColorMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            lit = null;
            vertexColorMat = null;
            vcPattern.Clear();
            waterMat = null;
            riverWaterMat = null;
            windowMat = null;
            lanternMat = null;
        }

        static Material windowMat, lanternMat;

        /// <summary>Material dùng chung cho mọi cửa sổ — ban đêm đổi sang vàng sáng (LivingWorld).</summary>
        public static Material WindowMat()
        {
            if (windowMat) return windowMat;
            windowMat = new Material(Mat(Ink)) { name = "Window" };
            return windowMat;
        }

        /// <summary>Đèn lồng / đèn chuông: luôn sáng vàng.</summary>
        public static Material LanternMat()
        {
            if (lanternMat) return lanternMat;
            lanternMat = new Material(Mat(Hex("#FFD27A"))) { name = "Lantern" };
            Glow(lanternMat, Hex("#FFC060"), 1.6f);
            return lanternMat;
        }

        public static void Glow(Material m, Color c, float intensity)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * intensity);
        }

        public static void NoGlow(Material m)
        {
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        }

        static readonly Dictionary<int, Material> vcPattern = new();

        /// <summary>Màu theo đỉnh + hoa văn (vd. răng cưa đá gộp thành 1 lưới).</summary>
        public static Material VertexColorMat(int pattern)
        {
            if (pattern == PatNone) return VertexColorMat();
            if (vcPattern.TryGetValue(pattern, out var m) && m) return m;
            m = new Material(VertexColorMat()) { name = "VertexColor" + pattern };
            if (m.HasProperty("_Pattern")) m.SetFloat("_Pattern", pattern);
            vcPattern[pattern] = m;
            return m;
        }

        /// <summary>Material dùng màu theo đỉnh (mặt đất cỏ / đá).</summary>
        public static Material VertexColorMat()
        {
            if (vertexColorMat) return vertexColorMat;
            var sh = Shader.Find("LangGio/Stylized");
            if (sh != null)
            {
                vertexColorMat = new Material(sh) { name = "VertexColor" };
                vertexColorMat.SetFloat("_UseVertexColor", 1f);
                vertexColorMat.SetFloat("_Noise", 0.6f);
                return vertexColorMat;
            }
            sh = Shader.Find("LangGio/VertexColorLit");
            if (sh == null) sh = Shader.Find("Standard");
            vertexColorMat = new Material(sh);
            return vertexColorMat;
        }

        /// <summary>Material đơn sắc, mờ (không bóng) — dùng chung theo màu.</summary>
        public static Material Mat(Color c) => Mat(c, PatNone);

        /// <summary>Đổi material của khối sang màu + hoa văn.</summary>
        public static GameObject WithPattern(this GameObject g, Color c, int pattern)
        {
            var r = g ? g.GetComponent<Renderer>() : null;
            if (r) r.sharedMaterial = Mat(c, pattern);
            return g;
        }

        /// <summary>Material đơn sắc + hoa văn (vữa / ván gỗ / đá xếp / gạch).</summary>
        public static Material Mat(Color c, int pattern)
        {
            if (cache.TryGetValue((c, pattern), out var m) && m) return m;
            if (lit == null) lit = Shader.Find("LangGio/Stylized");
            if (lit == null) lit = Shader.Find("Standard");
            m = new Material(lit) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Pattern")) m.SetFloat("_Pattern", pattern);
            cache[(c, pattern)] = m;
            return m;
        }
    }
}
