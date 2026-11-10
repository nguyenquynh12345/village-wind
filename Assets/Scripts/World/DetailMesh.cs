using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Gộp hàng trăm khối nhỏ (viên đá, viên ngói, thanh gỗ) thành 1 lưới có màu theo đỉnh —
    /// 1 lần vẽ cho cả ngôi nhà, đủ nhẹ cho card yếu. Mỗi khối có thể lệch / nghiêng / đổi màu chút ít cho tự nhiên.
    /// </summary>
    public class DetailMesh
    {
        readonly List<Vector3> v = new();
        readonly List<Vector3> n = new();
        readonly List<Color> c = new();
        readonly List<int> t = new();
        uint seed;

        public DetailMesh(int seed) { this.seed = (uint)seed * 2654435761u + 12345u; }

        public int VertexCount => v.Count;

        /// <summary>Số ngẫu nhiên 0..1 (tất định theo seed để nhà không đổi mỗi lần dựng lại).</summary>
        public float Rand()
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            return (seed & 0xFFFFFF) / (float)0x1000000;
        }

        public float Range(float a, float b) => a + (b - a) * Rand();

        /// <summary>Màu lệch nhẹ (sáng / tối, ấm / lạnh).</summary>
        public Color Vary(Color col, float amount)
        {
            float k = 1f + (Rand() - 0.5f) * 2f * amount;
            float warm = (Rand() - 0.5f) * amount * 0.5f;
            return new Color(Mathf.Clamp01(col.r * k + warm), Mathf.Clamp01(col.g * k), Mathf.Clamp01(col.b * k - warm), 1f);
        }

        void Face(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 nrm, Color col)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
            for (int k = 0; k < 4; k++) { n.Add(nrm); c.Add(col); }
            if (Vector3.Dot(Vector3.Cross(b - a, cc - a), nrm) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        /// <summary>Hộp theo tâm, kích thước, hướng. skipBottom/skipBack: bỏ mặt không bao giờ thấy (nhẹ hơn).</summary>
        public void Box(Vector3 center, Vector3 size, Quaternion rot, Color col, bool skipBottom = false, bool skipBack = false)
        {
            var h = size * 0.5f;
            Vector3 X = rot * Vector3.right, Y = rot * Vector3.up, Z = rot * Vector3.forward;
            Vector3 P(float x, float y, float z) => center + X * (x * h.x) + Y * (y * h.y) + Z * (z * h.z);
            // mặt trên sáng hơn chút, cạnh tối hơn chút → khối rõ hình dù ánh sáng phẳng
            var top = col * 1.04f; top.a = 1f;
            var side = col * 0.94f; side.a = 1f;
            Face(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), Y, top);
            if (!skipBottom) Face(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), -Y, side);
            Face(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), X, side);
            Face(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), -X, side);
            Face(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), -Z, col);
            if (!skipBack) Face(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), Z, col);
        }

        void TriAt(int i0, int i1, int i2, Vector3 want)
        {
            if (Vector3.Dot(Vector3.Cross(v[i1] - v[i0], v[i2] - v[i0]), want) >= 0f) { t.Add(i0); t.Add(i1); t.Add(i2); }
            else { t.Add(i0); t.Add(i2); t.Add(i1); }
        }

        /// <summary>
        /// Khối tròn lồi lõm (tán lá, bụi): hình cầu dẹt có bán kính r, mỗi đỉnh lệch ngẫu nhiên ±jitter,
        /// pháp tuyến mịn, màu chuyển từ dưới (tối) lên trên (sáng).
        /// </summary>
        public void Blob(Vector3 center, Vector3 r, Color top, Color bottom, float jitter, int lon = 9, int lat = 6)
        {
            int b = v.Count;
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat;
                float y = Mathf.Cos(th), s = Mathf.Sin(th);
                for (int j = 0; j < lon; j++)
                {
                    float ph = 2f * Mathf.PI * j / lon + (i % 2) * Mathf.PI / lon;
                    var dir = new Vector3(s * Mathf.Cos(ph), y, s * Mathf.Sin(ph));
                    float k = (i == 0 || i == lat) ? 1f : 1f + (Rand() - 0.5f) * 2f * jitter;
                    v.Add(center + Vector3.Scale(dir, r) * k);
                    n.Add(dir);
                    var col = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, (y + 1f) * 0.5f));
                    col.a = 1f;
                    c.Add(col);
                }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a0 = b + i * lon + j, a1 = b + i * lon + (j + 1) % lon;
                    int b0 = a0 + lon, b1 = a1 + lon;
                    var want = (v[a0] + v[b1]) * 0.5f - center;
                    TriAt(a0, b0, a1, want);
                    TriAt(a1, b0, b1, want);
                }
        }

        /// <summary>Thân / cành thuôn từ a (bán kính ra) tới b (bán kính rb). rb = 0 → hình nón.</summary>
        public void Taper(Vector3 a, Vector3 b, float ra, float rb, int sides, Color colA, Color colB)
        {
            var axis = (b - a).normalized;
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var w = Vector3.Cross(axis, u);
            int baseIdx = v.Count;
            for (int j = 0; j < sides; j++)
            {
                float ang = 2f * Mathf.PI * j / sides;
                var dir = u * Mathf.Cos(ang) + w * Mathf.Sin(ang);
                v.Add(a + dir * ra); n.Add(dir); c.Add(colA);
                v.Add(b + dir * rb); n.Add((dir + axis * (ra - rb) / Mathf.Max(0.01f, (b - a).magnitude)).normalized); c.Add(colB);
            }
            for (int j = 0; j < sides; j++)
            {
                int i0 = baseIdx + j * 2, i1 = i0 + 1, j0 = baseIdx + ((j + 1) % sides) * 2, j1 = j0 + 1;
                var want = (v[i0] + v[j0]) * 0.5f - a;
                want -= axis * Vector3.Dot(want, axis);
                TriAt(i0, i1, j0, want);
                TriAt(j0, i1, j1, want);
            }
        }

        /// <summary>Tạo GameObject chứa lưới (không va chạm). Trả về null nếu rỗng.</summary>
        public GameObject Build(string name, Transform parent, bool castShadows = true, Material material = null)
        {
            if (v.Count == 0) return null;
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material ? material : Palette.VertexColorMat();
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }
}
