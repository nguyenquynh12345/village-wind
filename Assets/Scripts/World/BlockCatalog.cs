using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>1 mảnh ghép tự do. (x, y, z) = tâm đáy; yaw = góc xoay quanh trục đứng (độ).</summary>
    [Serializable]
    public class Piece
    {
        public int id, t, m;
        public float x, y, z, yaw;
        public Piece Clone() => (Piece)MemberwiseClone();
    }

    /// <summary>Hình dạng 1 loại mảnh (khung cục bộ, gốc ở tâm đáy). Màu đỉnh: a = 1 → nhân màu chất liệu, a = 0 → màu cố định.</summary>
    public class BlockShape
    {
        public Vector3[] v, n;
        public Color[] c;
        public int[] t;
    }

    /// <summary>Danh mục mảnh ghép: tên, kích thước, chất liệu, hình dạng.</summary>
    public static class BlockCatalog
    {
        public const int Cube = 0, Half = 1, Slab = 2, PlankWall = 3, Post = 4, Beam = 5, Slope = 6, SlopeLow = 7,
                         RoofPanel = 8, Stairs = 9, Fence = 10, Arch = 11, Window = 12, Door = 13;

        public static readonly string[] Names =
        {
            "Khối", "Nửa khối", "Tấm sàn", "Vách ván", "Cột", "Dầm", "Mái dốc", "Mái thoải",
            "Tấm mái", "Bậc thang", "Hàng rào", "Vòm", "Cửa sổ", "Cửa ra vào"
        };

        static readonly Vector3[] Sizes =
        {
            new(1, 1, 1), new(1, 0.5f, 1), new(1, 0.1f, 1), new(1, 1, 0.1f), new(0.25f, 1, 0.25f), new(1, 0.2f, 0.2f),
            new(1, 1, 1), new(1, 0.5f, 1), new(1, 1, 1), new(1, 1, 1), new(1, 1, 0.15f), new(1, 1, 1),
            new(1, 1, 1), new(1, 2, 0.2f)
        };

        public static readonly string[] MatNames = { "Đá", "Đá tối", "Gỗ", "Gỗ tối", "Gạch", "Vữa trắng", "Ngói đỏ", "Ngói xám", "Rêu" };
        public static readonly Color[] MatColors =
        {
            Palette.Hex("#CFC6B8"), Palette.Hex("#9A9187"), Palette.Hex("#B98A5E"), Palette.Hex("#7A5A44"), Palette.Hex("#B7684F"),
            Palette.Hex("#F1E6D3"), Palette.Hex("#C8664A"), Palette.Hex("#7D8590"), Palette.Hex("#6E8B4E")
        };

        /// <summary>Chất liệu mặc định khi chọn loại mảnh (đổi được).</summary>
        public static readonly int[] DefaultMat = { 0, 0, 2, 2, 3, 3, 6, 6, 6, 0, 2, 0, 5, 3 };

        public static int Count => Names.Length;
        public static int Clamp(int t) => Mathf.Clamp(t, 0, Names.Length - 1);
        public static Vector3 Size(int t) => Sizes[Clamp(t)];
        public static Color MatColor(int m) => MatColors[Mathf.Abs(m) % MatColors.Length];
        public static string Describe(Piece p) => $"{Names[Clamp(p.t)]} · {MatNames[Mathf.Abs(p.m) % MatNames.Length]}";

        static readonly Color Glass = new Color(0.23f, 0.18f, 0.16f, 0f);
        static readonly Color DoorDark = new Color(0.36f, 0.27f, 0.2f, 0f);
        static BlockShape[] shapes;

        public static BlockShape Shape(int t)
        {
            shapes ??= new BlockShape[Names.Length];
            t = Clamp(t);
            return shapes[t] ??= BuildShape(t);
        }

        // ---------- Dựng hình từng loại ----------
        class TB
        {
            public readonly List<Vector3> v = new(), n = new();
            public readonly List<Color> c = new();
            public readonly List<int> t = new();

            public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 nrm, Color col)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
                for (int k = 0; k < 4; k++) { n.Add(nrm); c.Add(col); }
                if (Vector3.Dot(Vector3.Cross(b - a, cc - a), nrm) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
                else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 cc, Vector3 nrm, Color col)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(cc);
                for (int k = 0; k < 3; k++) { n.Add(nrm); c.Add(col); }
                if (Vector3.Dot(Vector3.Cross(b - a, cc - a), nrm) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
                else { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
            }

            /// <summary>Hộp theo tâm + kích thước.</summary>
            public void Box(Vector3 ctr, Vector3 s, Color col)
            {
                var h = s * 0.5f;
                Vector3 P(float x, float y, float z) => ctr + new Vector3(x * h.x, y * h.y, z * h.z);
                Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), Vector3.up, col);
                Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), Vector3.down, col);
                Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), Vector3.right, col);
                Quad(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), Vector3.left, col);
                Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), Vector3.forward, col);
                Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), Vector3.back, col);
            }

            /// <summary>Hình nêm: đáy đầy đủ, mặt dốc lên từ z−  tới z+, mặt sau đứng ở z+.</summary>
            public void Wedge(Vector3 s, Color col)
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, H = s.y;
                Vector3 a0 = new(-hx, 0, -hz), a1 = new(hx, 0, -hz), b0 = new(-hx, 0, hz), b1 = new(hx, 0, hz);
                Vector3 c0 = new(-hx, H, hz), c1 = new(hx, H, hz);
                Quad(a0, a1, b1, b0, Vector3.down, col);
                Quad(b0, b1, c1, c0, Vector3.forward, col);
                Quad(a0, a1, c1, c0, new Vector3(0, s.z, -H).normalized, col);
                Tri(a0, b0, c0, Vector3.left, col);
                Tri(a1, b1, c1, Vector3.right, col);
            }

            /// <summary>Tấm nghiêng mỏng từ (z−, đáy) lên (z+, đỉnh).</summary>
            public void Panel(Vector3 s, float th, Color col)
            {
                float hx = s.x * 0.5f, hz = s.z * 0.5f, H = s.y;
                var up = Vector3.up * th;
                Vector3 a0 = new(-hx, 0, -hz), a1 = new(hx, 0, -hz), b0 = new(-hx, H - th, hz), b1 = new(hx, H - th, hz);
                var top = new Vector3(0, s.z, -(H - th)).normalized;
                Quad(a0 + up, a1 + up, b1 + up, b0 + up, top, col);
                Quad(a0, a1, b1, b0, -top, col);
                Quad(a0, a0 + up, b0 + up, b0, Vector3.left, col);
                Quad(a1, a1 + up, b1 + up, b1, Vector3.right, col);
                Quad(a0, a1, a1 + up, a0 + up, Vector3.back, col);
                Quad(b0, b1, b1 + up, b0 + up, Vector3.forward, col);
            }
        }

        static BlockShape BuildShape(int t)
        {
            var b = new TB();
            var s = Sizes[t];
            Color full = new Color(1, 1, 1, 1), dim = new Color(0.86f, 0.86f, 0.86f, 1f);
            switch (t)
            {
                case Cube: case Half: case Slab: case Post: case Beam:
                    b.Box(new Vector3(0, s.y * 0.5f, 0), s, full);
                    break;
                case PlankWall:
                    for (int i = 0; i < 4; i++)
                        b.Box(new Vector3(-0.375f + i * 0.25f, 0.5f, 0), new Vector3(0.235f, 1f, 0.1f), i % 2 == 0 ? full : dim);
                    b.Box(new Vector3(0, 0.75f, -0.06f), new Vector3(1f, 0.08f, 0.03f), dim);
                    b.Box(new Vector3(0, 0.25f, -0.06f), new Vector3(1f, 0.08f, 0.03f), dim);
                    break;
                case Slope: case SlopeLow:
                    b.Wedge(s, full);
                    break;
                case RoofPanel:
                    b.Panel(s, 0.08f, full);
                    break;
                case Stairs:
                    for (int i = 0; i < 4; i++)
                    {
                        float h = (i + 1) * 0.25f;
                        b.Box(new Vector3(0, h * 0.5f, -0.375f + i * 0.25f), new Vector3(1f, h, 0.25f), i % 2 == 0 ? full : dim);
                    }
                    break;
                case Fence:
                    b.Box(new Vector3(-0.44f, 0.5f, 0), new Vector3(0.12f, 1f, 0.12f), full);
                    b.Box(new Vector3(0.44f, 0.5f, 0), new Vector3(0.12f, 1f, 0.12f), full);
                    b.Box(new Vector3(0, 0.8f, 0), new Vector3(1f, 0.08f, 0.06f), dim);
                    b.Box(new Vector3(0, 0.38f, 0), new Vector3(1f, 0.08f, 0.06f), dim);
                    break;
                case Arch:
                    b.Box(new Vector3(-0.375f, 0.35f, 0), new Vector3(0.25f, 0.7f, 1f), full);
                    b.Box(new Vector3(0.375f, 0.35f, 0), new Vector3(0.25f, 0.7f, 1f), full);
                    b.Box(new Vector3(0, 0.85f, 0), new Vector3(1f, 0.3f, 1f), full);
                    b.Box(new Vector3(-0.2f, 0.65f, 0), new Vector3(0.12f, 0.12f, 1f), dim);   // vai vòm
                    b.Box(new Vector3(0.2f, 0.65f, 0), new Vector3(0.12f, 0.12f, 1f), dim);
                    break;
                case Window:
                    b.Box(new Vector3(0, 0.5f, 0), Vector3.one, full);
                    b.Quad(new Vector3(-0.25f, 0.3f, 0.505f), new Vector3(0.25f, 0.3f, 0.505f), new Vector3(0.25f, 0.8f, 0.505f), new Vector3(-0.25f, 0.8f, 0.505f), Vector3.forward, Glass);
                    b.Quad(new Vector3(-0.25f, 0.3f, -0.505f), new Vector3(0.25f, 0.3f, -0.505f), new Vector3(0.25f, 0.8f, -0.505f), new Vector3(-0.25f, 0.8f, -0.505f), Vector3.back, Glass);
                    b.Box(new Vector3(0, 0.27f, 0.53f), new Vector3(0.62f, 0.06f, 0.06f), dim);
                    b.Box(new Vector3(0, 0.27f, -0.53f), new Vector3(0.62f, 0.06f, 0.06f), dim);
                    break;
                case Door:
                    b.Box(new Vector3(-0.42f, 1f, 0), new Vector3(0.16f, 2f, 0.2f), full);
                    b.Box(new Vector3(0.42f, 1f, 0), new Vector3(0.16f, 2f, 0.2f), full);
                    b.Box(new Vector3(0, 1.9f, 0), new Vector3(0.68f, 0.2f, 0.2f), full);
                    b.Box(new Vector3(0, 0.9f, 0), new Vector3(0.68f, 1.8f, 0.08f), DoorDark);
                    break;
            }
            return new BlockShape { v = b.v.ToArray(), n = b.n.ToArray(), c = b.c.ToArray(), t = b.t.ToArray() };
        }

        /// <summary>Lưới riêng cho 1 loại + chất liệu (dùng cho hình xem trước / tô sáng).</summary>
        static readonly Dictionary<int, Mesh> single = new();

        public static Mesh SingleMesh(int t, int m)
        {
            int key = Clamp(t) * 100 + Mathf.Abs(m) % MatColors.Length;
            if (single.TryGetValue(key, out var mesh) && mesh) return mesh;
            var sh = Shape(t);
            var col = MatColor(m);
            var cs = new Color[sh.c.Length];
            for (int i = 0; i < cs.Length; i++) cs[i] = Tint(sh.c[i], col);
            mesh = new Mesh { name = "Block" + key };
            mesh.vertices = sh.v;
            mesh.normals = sh.n;
            mesh.colors = cs;
            mesh.triangles = sh.t;
            mesh.RecalculateBounds();
            single[key] = mesh;
            return mesh;
        }

        public static Color Tint(Color shapeCol, Color mat) =>
            shapeCol.a > 0.5f ? new Color(mat.r * shapeCol.r, mat.g * shapeCol.g, mat.b * shapeCol.b, 1f)
                              : new Color(shapeCol.r, shapeCol.g, shapeCol.b, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            single.Clear();
        }
    }
}
