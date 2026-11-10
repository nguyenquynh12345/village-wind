using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Cỏ lún phún + hoa dại rải trên mặt đất (chỉ để đẹp, không lưu, không chọn được).
    /// Tránh nhà, ao, đường, tường, tháp và chỗ dốc. Sinh lại sau mỗi thay đổi (chậm 0.6 s để gộp).
    /// Vị trí từng bụi cố định theo ô lưới (hash) nên không "nhảy" khi sinh lại.
    /// </summary>
    public class GroundDecor : MonoBehaviour
    {
        const float Spacing = 1.2f;
        const float ChunkSize = 32f;
        const float Grid = 1f;   // ô lưới "bị chiếm"

        static readonly Color GrassDark = Palette.Hex("#4F7A3C");
        static readonly Color GrassLight = Palette.Hex("#A8C772");
        static readonly Color[] Flowers =
        {
            Palette.Hex("#FFFFFF"), Palette.Hex("#F6D55C"), Palette.Hex("#E59AB0"), Palette.Hex("#B9A3E3"), Palette.Hex("#F08A5D")
        };

        readonly List<GameObject> chunks = new();
        bool[] blocked;
        int gn;
        float pending = 0.2f;

        void OnEnable()
        {
            History.Changed += Schedule;
            Settings.Changed += Schedule;
        }

        void OnDisable()
        {
            History.Changed -= Schedule;
            Settings.Changed -= Schedule;
        }

        void Schedule() => pending = 0.6f;

        void Update()
        {
            if (pending <= 0f) return;
            pending -= Time.unscaledDeltaTime;
            if (pending <= 0f) Rebuild();
        }

        void Clear()
        {
            foreach (var c in chunks) if (c) Destroy(c);
            chunks.Clear();
        }

        void Rebuild()
        {
            Clear();
            var hf = HeightField.I;
            if (!Settings.Decor || !hf) return;
            Rasterize();

            float half = GameConfig.WorldHalf - 1f;
            int nc = Mathf.CeilToInt(2f * half / ChunkSize);
            if (!windMat)
            {
                windMat = new Material(Palette.VertexColorMat()) { name = "DecorWind" };
                windMat.SetFloat("_Wind", 1f);
            }
            var mat = windMat;
            var uv = new List<Vector2>();
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();

            for (int cx = 0; cx < nc; cx++)
                for (int cz = 0; cz < nc; cz++)
                {
                    v.Clear(); c.Clear(); t.Clear(); uv.Clear();
                    float baseY = 0f;
                    float x0 = -half + cx * ChunkSize, z0 = -half + cz * ChunkSize;
                    var org = new Vector3(x0 + ChunkSize * 0.5f, 0, z0 + ChunkSize * 0.5f);
                    int n = Mathf.CeilToInt(ChunkSize / Spacing);
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            Weigh(v, uv, baseY);
                            float gx = x0 + i * Spacing, gz = z0 + j * Spacing;
                            int ix = Mathf.FloorToInt(gx / Spacing), iz = Mathf.FloorToInt(gz / Spacing);
                            float h0 = Hash(ix, iz, 1);
                            float px = gx + (Hash(ix, iz, 2) - 0.5f) * Spacing * 0.9f;
                            float pz = gz + (Hash(ix, iz, 3) - 0.5f) * Spacing * 0.9f;
                            int paint = hf.PaintAt(px, pz);
                            bool meadow = paint == 1;
                            if (h0 > (meadow ? 0.95f : paint == 2 ? 0.5f : 0.5f)) continue;
                            if (paint == 3 || paint == 4) continue;          // cát, đất: trơn
                            if (Mathf.Abs(px) > half || Mathf.Abs(pz) > half || IsBlocked(px, pz)) continue;
                            float y = hf.HeightAt(px, pz);
                            Weigh(v, uv, baseY);
                            baseY = y - 0.02f;
                            float slope = Mathf.Max(Mathf.Abs(hf.HeightAt(px + 0.7f, pz) - y), Mathf.Abs(hf.HeightAt(px, pz + 0.7f) - y));
                            if (slope > 0.55f) continue;
                            var p = new Vector3(px, y - 0.02f, pz) - org;
                            float rot = Hash(ix, iz, 4) * Mathf.PI * 2f;
                            float size = 0.95f + Hash(ix, iz, 5) * 0.7f;
                            if (paint == 2)
                            {
                                Pebble(v, c, t, p, rot, 0.6f + size * 0.4f);   // sỏi: đá nhỏ thay cỏ
                                continue;
                            }
                            Tuft(v, c, t, p, rot, meadow ? size * 1.15f : size);
                            if (meadow)
                            {
                                for (int q = 0; q < 2; q++)
                                {
                                    float a = rot + q * 2.4f;
                                    Flower(v, c, t, p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (0.25f + q * 0.2f), a,
                                           Flowers[(int)(Hash(ix, iz, 8 + q) * Flowers.Length) % Flowers.Length]);
                                }
                            }
                            else if (Hash(ix, iz, 6) < 0.18f)
                                Flower(v, c, t, p + new Vector3(Mathf.Cos(rot) * 0.15f, 0, Mathf.Sin(rot) * 0.15f), rot,
                                       Flowers[(int)(Hash(ix, iz, 7) * Flowers.Length) % Flowers.Length]);
                        }
                    if (v.Count == 0) continue;
                    Weigh(v, uv, baseY);

                    var mesh = new Mesh { name = "Decor" };
                    if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.SetVertices(v);
                    mesh.SetColors(c);
                    mesh.SetUVs(0, uv);
                    var nrm = new Vector3[v.Count];
                    for (int k = 0; k < nrm.Length; k++) nrm[k] = Vector3.up;   // pháp tuyến hướng lên: cỏ sáng mềm như mặt đất
                    mesh.normals = nrm;
                    mesh.SetTriangles(t, 0);
                    mesh.RecalculateBounds();

                    var go = new GameObject("Decor");
                    go.transform.SetParent(transform, false);
                    go.transform.position = org;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    chunks.Add(go);
                }
        }

        Material windMat;

        /// <summary>Gán trọng số gió (uv.x) cho các đỉnh vừa thêm: gốc 0, ngọn ~1.</summary>
        static void Weigh(List<Vector3> v, List<Vector2> uv, float baseY)
        {
            for (int k = uv.Count; k < v.Count; k++)
                uv.Add(new Vector2(Mathf.Clamp01((v[k].y - baseY) / 0.6f), 0f));
        }

        // 3 lá cỏ chụm lại, vẽ cả 2 mặt
        static void Tuft(List<Vector3> v, List<Color> c, List<int> t, Vector3 p, float rot, float size)
        {
            for (int b = 0; b < 3; b++)
            {
                float a = rot + b * 2.094f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var side = new Vector3(-dir.z, 0, dir.x) * 0.05f * size;
                var tip = p + dir * 0.12f * size + Vector3.up * (0.22f + 0.08f * b) * size;
                Blade(v, c, t, p - side, p + side, tip, GrassDark, GrassLight);
            }
        }

        static void Flower(List<Vector3> v, List<Color> c, List<int> t, Vector3 p, float rot, Color col)
        {
            var side = new Vector3(Mathf.Cos(rot), 0, Mathf.Sin(rot)) * 0.015f;
            var top = p + Vector3.up * 0.32f;
            Blade(v, c, t, p - side, p + side, top, GrassDark, GrassDark);
            // bông: 2 hình thoi chéo nhau
            for (int k = 0; k < 2; k++)
            {
                float a = rot + k * Mathf.PI * 0.5f;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.07f;
                var u = Vector3.up * 0.06f;
                int i0 = v.Count;
                v.Add(top - d); v.Add(top + u); v.Add(top + d); v.Add(top - u);
                for (int q = 0; q < 4; q++) c.Add(col);
                t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2); t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3);
                t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1); t.Add(i0); t.Add(i0 + 3); t.Add(i0 + 2);
            }
        }

        static void Pebble(List<Vector3> v, List<Color> c, List<int> t, Vector3 p, float rot, float size)
        {
            var col = Color.Lerp(Palette.Hex("#A39C90"), Palette.Hex("#D2CCC0"), Mathf.Repeat(rot, 1f));
            float r = 0.09f * size;
            int i0 = v.Count;
            for (int k = 0; k < 4; k++)
            {
                float a = rot + k * Mathf.PI * 0.5f;
                v.Add(p + new Vector3(Mathf.Cos(a) * r, 0.01f, Mathf.Sin(a) * r * 0.8f));
                c.Add(col * 0.85f);
            }
            v.Add(p + Vector3.up * r * 0.7f);
            c.Add(col);
            for (int k = 0; k < 4; k++)
            {
                t.Add(i0 + k); t.Add(i0 + 4); t.Add(i0 + (k + 1) % 4);
                t.Add(i0 + k); t.Add(i0 + (k + 1) % 4); t.Add(i0 + 4);
            }
        }

        static void Blade(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 b, Vector3 tip, Color low, Color high)
        {
            int i0 = v.Count;
            v.Add(a); v.Add(b); v.Add(tip);
            c.Add(low); c.Add(low); c.Add(high);
            t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
            t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1);
        }

        static float Hash(int x, int z, int k)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(k * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        // ---------- Lưới "bị chiếm" 1 m ----------
        bool IsBlocked(float x, float z)
        {
            int i = Mathf.FloorToInt((x + GameConfig.WorldHalf) / Grid), j = Mathf.FloorToInt((z + GameConfig.WorldHalf) / Grid);
            if (i < 0 || j < 0 || i >= gn || j >= gn) return true;
            return blocked[j * gn + i];
        }

        void Mark(float x, float z)
        {
            int i = Mathf.FloorToInt((x + GameConfig.WorldHalf) / Grid), j = Mathf.FloorToInt((z + GameConfig.WorldHalf) / Grid);
            if (i < 0 || j < 0 || i >= gn || j >= gn) return;
            blocked[j * gn + i] = true;
        }

        void MarkDisc(Vector2 p, float r)
        {
            for (float dx = -r; dx <= r; dx += Grid * 0.5f)
                for (float dz = -r; dz <= r; dz += Grid * 0.5f)
                    if (dx * dx + dz * dz <= r * r) Mark(p.x + dx, p.y + dz);
        }

        void Rasterize()
        {
            gn = Mathf.CeilToInt(2f * GameConfig.WorldHalf / Grid);
            if (blocked == null || blocked.Length != gn * gn) blocked = new bool[gn * gn];
            else System.Array.Clear(blocked, 0, blocked.Length);

            // ô có tâm (x, z) bị chiếm nếu điểm giữa ô nằm trong vật (+ lề nhỏ)
            void Scan(Rect r, System.Func<Vector2, bool> inside)
            {
                for (float x = Mathf.Floor(r.xMin) + 0.5f; x <= r.xMax; x += Grid)
                    for (float z = Mathf.Floor(r.yMin) + 0.5f; z <= r.yMax; z += Grid)
                        if (inside(new Vector2(x, z))) Mark(x, z);
            }

            foreach (var h in EntityManager.OfType<HouseEntity>())
            {
                var boxes = h.Boxes;
                float R = Mathf.Sqrt(h.W * h.W + h.L * h.L) * 0.5f + 12f;
                var hp = h.P;
                Scan(new Rect(hp.x - R, hp.z - R, 2 * R, 2 * R), q => { foreach (var b in boxes) if (b.Contains(q, 0.7f)) return true; return false; });
            }

            foreach (var pond in EntityManager.OfType<PondEntity>())
            {
                if (pond.Mask == null) continue;
                var r = pond.WorldRect;
                r = new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f);
                Scan(r, q => pond.Mask.ContainsWorld(q.x, q.y) || pond.Mask.ContainsWorld(q.x + 0.6f, q.y) ||
                             pond.Mask.ContainsWorld(q.x - 0.6f, q.y) || pond.Mask.ContainsWorld(q.x, q.y + 0.6f) ||
                             pond.Mask.ContainsWorld(q.x, q.y - 0.6f));
            }

            foreach (var path in EntityManager.OfType<PathEntity>())
            {
                var p = path;
                Scan(p.Area, q => p.Covers(q, 0.5f));
            }

            foreach (var river in EntityManager.OfType<RiverEntity>())
            {
                var rv = river;
                Scan(rv.Area, q => rv.Covers(q, 0.6f));
            }

            if (BlockWorld.I)
            {
                var hf = HeightField.I;
                foreach (var pc in BlockWorld.I.All)
                {
                    BlockWorld.BoxOf(pc, out var o, out float y0, out _);
                    if (y0 > hf.HeightAt(pc.x, pc.z) + 0.8f) continue;
                    float R = o.hx + o.hz + 1f;
                    Scan(new Rect(pc.x - R, pc.z - R, 2 * R, 2 * R), q => o.Contains(q, 0.4f));
                }
            }

            var gr = WallGraph.I;
            if (gr)
            {
                foreach (var q in gr.WallSamples(0.5f)) MarkDisc(q, GameConfig.WallThickness * 0.5f + 0.6f);
                foreach (var (pos, radius) in gr.Towers()) MarkDisc(pos, radius + 0.6f);
            }
        }
    }
}
