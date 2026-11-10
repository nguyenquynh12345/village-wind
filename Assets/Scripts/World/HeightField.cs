using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    public enum TerrainMode { Raise, Lower, Flatten, Smooth }

    /// <summary>
    /// Mặt đất dạng lưới độ cao (ô 1 m). Chia thành khối 16 x 16 ô, chỉ khối bị sửa mới sinh lại mesh.
    /// Mọi vật lấy độ cao mặt đất qua <see cref="HeightAt"/>.
    /// </summary>
    public class HeightField : MonoBehaviour
    {
        public static HeightField I;

        const float C = GameConfig.CellSize;
        int cells, n, chunks;
        float origin;
        float[] h, tmp;
        float[] carve;   // phần đất bị ao đào xuống (tính lại từ các ao, không lưu)
        byte[] paint;    // lớp tô mặt đất: 0 cỏ · 1 hoa dại · 2 sỏi · 3 cát · 4 đất

        public static readonly string[] PaintNames = { "Cỏ", "Hoa dại", "Sỏi", "Cát", "Đất" };
        static readonly Color[] PaintColors =
        {
            Palette.Grass, Palette.Hex("#7FA656"), Palette.Hex("#BDB5A6"), Palette.Hex("#E3D3A8"), Palette.Hex("#9C7A5B")
        };
        Mesh[] meshes;
        bool[] dirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        void Awake()
        {
            I = this;
            origin = -GameConfig.WorldHalf;
            cells = Mathf.RoundToInt(2f * GameConfig.WorldHalf / C);
            n = cells + 1;
            chunks = cells / GameConfig.ChunkCells;
            h = new float[n * n];
            tmp = new float[n * n];
            carve = new float[n * n];
            paint = new byte[n * n];

            FillDefault();

            meshes = new Mesh[chunks * chunks];
            dirty = new bool[chunks * chunks];
            var mat = Palette.VertexColorMat(Palette.PatGrass);
            for (int cz = 0; cz < chunks; cz++)
                for (int cx = 0; cx < chunks; cx++)
                {
                    int i = cz * chunks + cx;
                    var go = new GameObject($"Chunk {cx},{cz}");
                    go.transform.SetParent(transform, false);
                    var m = new Mesh { name = go.name };
                    m.MarkDynamic();
                    go.AddComponent<MeshFilter>().sharedMesh = m;
                    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    meshes[i] = m;
                    dirty[i] = true;
                }
            RebuildDirty();
        }

        /// <summary>Mặt đất gợn nhẹ cho đỡ phẳng lì.</summary>
        void FillDefault()
        {
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float wx = origin + x * C, wz = origin + z * C;
                    h[z * n + x] = Mathf.PerlinNoise(wx * 0.035f + 10f, wz * 0.035f + 20f) * 0.8f - 0.4f;
                }
            System.Array.Clear(carve, 0, carve.Length);
            System.Array.Clear(paint, 0, paint.Length);
        }

        // ---------- Lớp tô mặt đất ----------
        /// <summary>Loại tô tại điểm gần nhất (0 = cỏ).</summary>
        public int PaintAt(float x, float z)
        {
            int ix = Mathf.RoundToInt((x - origin) / C), iz = Mathf.RoundToInt((z - origin) / C);
            if (ix < 0 || iz < 0 || ix >= n || iz >= n) return 0;
            return paint[iz * n + ix];
        }

        public byte[] PaintData() => (byte[])paint.Clone();

        public void SetPaint(byte[] data)
        {
            if (data == null || data.Length != paint.Length) System.Array.Clear(paint, 0, paint.Length);
            else System.Array.Copy(data, paint, paint.Length);
            MarkAll();
        }

        /// <summary>Tô vùng tròn (mép hơi lởm chởm cho tự nhiên). Trả về vùng lưới bị đổi.</summary>
        public RectInt PaintBrush(Vector3 c, float radius, byte value)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - radius - origin) / C), 0, n - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + radius - origin) / C), 0, n - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((c.z - radius - origin) / C), 0, n - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((c.z + radius - origin) / C), 0, n - 1);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float wx = origin + x * C, wz = origin + z * C;
                    float d = Mathf.Sqrt((wx - c.x) * (wx - c.x) + (wz - c.z) * (wz - c.z));
                    float edge = radius * (0.8f + 0.35f * Mathf.PerlinNoise(wx * 0.6f + 3.1f, wz * 0.6f + 7.7f));
                    if (d > edge) continue;
                    paint[z * n + x] = value;
                }
            var r = new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            MarkDirty(r);
            RebuildDirty();
            return r;
        }

        public byte[] ExtractPaint(byte[] src, RectInt r)
        {
            var o = new byte[r.width * r.height];
            for (int z = 0; z < r.height; z++)
                System.Array.Copy(src, (r.y + z) * n + r.x, o, z * r.width, r.width);
            return o;
        }

        public void SetPaintRegion(RectInt r, byte[] data)
        {
            for (int z = 0; z < r.height; z++)
                System.Array.Copy(data, z * r.width, paint, (r.y + z) * n + r.x, r.width);
            MarkDirty(r);
            RebuildDirty();
        }

        /// <summary>Các điểm (thế giới) đã tô loại v — để thả bướm trên bãi hoa.</summary>
        public List<Vector3> PaintedPoints(byte v, int max, int seed)
        {
            var list = new List<Vector3>();
            int count = 0;
            for (int i = 0; i < paint.Length; i++) if (paint[i] == v) count++;
            if (count == 0) return list;
            var rng = new System.Random(seed);
            int stride = Mathf.Max(1, count / Mathf.Max(1, max));
            int k = rng.Next(stride);
            for (int i = 0; i < paint.Length && list.Count < max; i++)
            {
                if (paint[i] != v) continue;
                if (k-- > 0) continue;
                k = stride - 1;
                int x = i % n, z = i / n;
                list.Add(new Vector3(origin + x * C, At(x, z), origin + z * C));
            }
            return list;
        }

        // ---------- Làng mới / lưu / tải ----------
        public void Regenerate()
        {
            FillDefault();
            MarkAll();
        }

        public float[] BaseHeights() => (float[])h.Clone();

        public void SetBase(float[] data)
        {
            if (data == null || data.Length != h.Length) { Regenerate(); return; }
            System.Array.Copy(data, h, h.Length);
            System.Array.Clear(carve, 0, carve.Length);
            MarkAll();
        }

        void MarkAll()
        {
            for (int i = 0; i < dirty.Length; i++) dirty[i] = true;
            RebuildDirty();
        }

        // ---------- Đọc ----------
        int Idx(int x, int z) => Mathf.Clamp(z, 0, n - 1) * n + Mathf.Clamp(x, 0, n - 1);
        /// <summary>Độ cao thật tại đỉnh lưới (đã trừ phần ao đào).</summary>
        public float At(int x, int z) { int i = Idx(x, z); return h[i] - carve[i]; }

        /// <summary>Độ cao mặt đất (đã trừ phần ao đào).</summary>
        public float HeightAt(float x, float z) => Sample(x, z, true);

        /// <summary>Độ cao đất gốc, chưa tính ao (dùng để tính mực nước ao theo bờ).</summary>
        public float BaseHeightAt(float x, float z) => Sample(x, z, false);

        float Sample(float x, float z, bool withCarve)
        {
            float gx = Mathf.Clamp((x - origin) / C, 0f, cells - 0.0001f);
            float gz = Mathf.Clamp((z - origin) / C, 0f, cells - 0.0001f);
            int ix = (int)gx, iz = (int)gz;
            float fx = gx - ix, fz = gz - iz;
            int i0 = iz * n + ix;
            float a = h[i0], b = h[i0 + 1], c = h[i0 + n], d = h[i0 + n + 1];
            if (withCarve)
            {
                a -= carve[i0]; b -= carve[i0 + 1]; c -= carve[i0 + n]; d -= carve[i0 + n + 1];
            }
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }

        /// <summary>Tính lại phần đất bị các ao và sông đào trong vùng r (thế giới), rồi dựng lại mesh.</summary>
        public void RecomputeCarve(Rect r)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((r.xMin - origin) / C), 0, n - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((r.xMax - origin) / C), 0, n - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((r.yMin - origin) / C), 0, n - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((r.yMax - origin) / C), 0, n - 1);

            var ponds = new List<PondEntity>();
            foreach (var p in EntityManager.OfType<PondEntity>())
                if (p.WorldRect.Overlaps(r)) ponds.Add(p);

            var rivers = new List<RiverEntity>();
            foreach (var rv in EntityManager.OfType<RiverEntity>())
                if (rv.Area.Overlaps(r)) rivers.Add(rv);

            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float wx = origin + x * C, wz = origin + z * C, c = 0f;
                    foreach (var p in ponds) c = Mathf.Max(c, p.CarveAt(wx, wz));
                    foreach (var rv in rivers) c = Mathf.Max(c, rv.CarveAt(wx, wz));
                    carve[z * n + x] = c;
                }

            MarkDirty(new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1));
            RebuildDirty();
        }

        public Vector3 OnGround(Vector3 p) { p.y = HeightAt(p.x, p.z); return p; }

        /// <summary>Bắn tia từ camera xuống mặt đất (dò từng bước rồi chia đôi cho chính xác).</summary>
        public bool Raycast(Ray ray, out Vector3 hit)
        {
            const float step = 0.5f;
            float t = 0f;
            for (int i = 0; i < 1600; i++)
            {
                t += step;
                var p = ray.GetPoint(t);
                if (ray.direction.y < 0 && p.y < GameConfig.MinHeight - 2f) break;
                bool inside = Mathf.Abs(p.x) <= GameConfig.WorldHalf && Mathf.Abs(p.z) <= GameConfig.WorldHalf;
                if (inside && p.y <= HeightAt(p.x, p.z))
                {
                    float lo = t - step, hi = t;
                    for (int k = 0; k < 10; k++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        var q = ray.GetPoint(mid);
                        if (q.y <= HeightAt(q.x, q.z)) hi = mid; else lo = mid;
                    }
                    hit = ray.GetPoint(hi);
                    hit.y = HeightAt(hit.x, hit.z);
                    return true;
                }
            }
            hit = default;
            return false;
        }

        // ---------- Cọ ----------
        /// <summary>Áp cọ trong 1 frame. Trả về vùng lưới bị đổi.</summary>
        public RectInt Brush(Vector3 c, float radius, TerrainMode mode, float strength, float dt, float flatHeight)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - radius - origin) / C), 0, n - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + radius - origin) / C), 0, n - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((c.z - radius - origin) / C), 0, n - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((c.z + radius - origin) / C), 0, n - 1);

            if (mode == TerrainMode.Smooth) System.Array.Copy(h, tmp, h.Length);

            float rate = GameConfig.BrushRate * strength * dt;
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float wx = origin + x * C, wz = origin + z * C;
                    float d = Mathf.Sqrt((wx - c.x) * (wx - c.x) + (wz - c.z) * (wz - c.z));
                    if (d > radius) continue;
                    float f = 1f - d / radius;
                    f = f * f * (3f - 2f * f);
                    int i = z * n + x;
                    switch (mode)
                    {
                        case TerrainMode.Raise: h[i] += rate * f; break;
                        case TerrainMode.Lower: h[i] -= rate * f; break;
                        case TerrainMode.Flatten: h[i] = Mathf.MoveTowards(h[i], flatHeight, rate * 1.5f * f); break;
                        case TerrainMode.Smooth:
                            float avg = (tmp[Idx(x - 1, z)] + tmp[Idx(x + 1, z)] + tmp[Idx(x, z - 1)] + tmp[Idx(x, z + 1)] + tmp[i]) / 5f;
                            h[i] = Mathf.Lerp(h[i], avg, Mathf.Clamp01(f * strength * dt * 8f));
                            break;
                    }
                    h[i] = Mathf.Clamp(h[i], GameConfig.MinHeight, GameConfig.MaxHeight);
                }

            var r = new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            MarkDirty(r);
            return r;
        }

        // ---------- Chụp / khôi phục vùng (cho hoàn tác) ----------
        public float[] Snapshot() => (float[])h.Clone();

        public float[] Extract(float[] src, RectInt r)
        {
            var o = new float[r.width * r.height];
            for (int z = 0; z < r.height; z++)
                System.Array.Copy(src, (r.y + z) * n + r.x, o, z * r.width, r.width);
            return o;
        }

        public float[] GetRegion(RectInt r) => Extract(h, r);

        public void SetRegion(RectInt r, float[] data)
        {
            for (int z = 0; z < r.height; z++)
                System.Array.Copy(data, z * r.width, h, (r.y + z) * n + r.x, r.width);
            MarkDirty(r);
            RebuildDirty();
            EntityManager.TerrainChanged(GridToWorld(r), false);
        }

        public Rect GridToWorld(RectInt r) =>
            new Rect(origin + (r.x - 1) * C, origin + (r.y - 1) * C, (r.width + 2) * C, (r.height + 2) * C);

        public static RectInt Union(RectInt a, RectInt b)
        {
            int x0 = Mathf.Min(a.xMin, b.xMin), z0 = Mathf.Min(a.yMin, b.yMin);
            int x1 = Mathf.Max(a.xMax, b.xMax), z1 = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(x0, z0, x1 - x0, z1 - z0);
        }

        // ---------- Mesh ----------
        void MarkDirty(RectInt r)
        {
            int cc = GameConfig.ChunkCells;
            int cx0 = Mathf.Clamp((r.xMin - 2) / cc, 0, chunks - 1), cx1 = Mathf.Clamp((r.xMax + 1) / cc, 0, chunks - 1);
            int cz0 = Mathf.Clamp((r.yMin - 2) / cc, 0, chunks - 1), cz1 = Mathf.Clamp((r.yMax + 1) / cc, 0, chunks - 1);
            for (int cz = cz0; cz <= cz1; cz++)
                for (int cx = cx0; cx <= cx1; cx++)
                    dirty[cz * chunks + cx] = true;
        }

        public void RebuildDirty()
        {
            for (int i = 0; i < dirty.Length; i++)
                if (dirty[i]) { BuildChunk(i); dirty[i] = false; }
        }

        void BuildChunk(int index)
        {
            int cc = GameConfig.ChunkCells, vn = cc + 1;
            int cx = index % chunks, cz = index / chunks;
            var verts = new Vector3[vn * vn];
            var normals = new Vector3[vn * vn];
            var colors = new Color[vn * vn];
            Color grass = Palette.Hex("#6C9A4B"), grassHigh = Palette.Hex("#86AA5C"), rock = Palette.StoneDark;
            Color dryPatch = Palette.Hex("#9EA75A"), lushPatch = Palette.Hex("#527F40");
            float r2 = GameConfig.BuildRadius * GameConfig.BuildRadius;

            for (int lz = 0; lz < vn; lz++)
                for (int lx = 0; lx < vn; lx++)
                {
                    int gx = cx * cc + lx, gz = cz * cc + lz;
                    int vi = lz * vn + lx;
                    float y = At(gx, gz);
                    float wx = origin + gx * C, wz = origin + gz * C;
                    verts[vi] = new Vector3(wx, y, wz);

                    var nrm = new Vector3(At(gx - 1, gz) - At(gx + 1, gz), 2f * C, At(gx, gz - 1) - At(gx, gz + 1)).normalized;
                    normals[vi] = nrm;

                    float slope = Vector3.Angle(nrm, Vector3.up);
                    Color col = Color.Lerp(grass, grassHigh, Mathf.InverseLerp(0f, 8f, y));
                    // mảng loang lớn: chỗ cỏ úa vàng, chỗ cỏ xanh đậm
                    float pa = Mathf.PerlinNoise(wx * 0.045f + 3.3f, wz * 0.045f + 8.1f);
                    float pb = Mathf.PerlinNoise(wx * 0.11f + 21.7f, wz * 0.11f + 4.4f);
                    col = Color.Lerp(col, dryPatch, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.8f, pa)) * 0.55f);
                    col = Color.Lerp(col, lushPatch, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.75f, pb)) * 0.45f);
                    int pv = paint[Idx(gx, gz)];
                    if (pv > 0) col = Color.Lerp(col, PaintColors[pv], 0.9f);
                    col = Color.Lerp(col, rock, Mathf.InverseLerp(GameConfig.RockSlope - 6f, GameConfig.RockSlope + 6f, slope));
                    if (wx * wx + wz * wz > r2) col *= 0.85f;   // ngoài vùng xây: tối hơn
                    col.a = 1f;
                    colors[vi] = col;
                }

            var tris = new int[cc * cc * 6];
            int t = 0;
            for (int lz = 0; lz < cc; lz++)
                for (int lx = 0; lx < cc; lx++)
                {
                    int a = lz * vn + lx, b = a + 1, c = a + vn, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }

            var m = meshes[index];
            m.Clear();
            m.vertices = verts;
            m.normals = normals;
            m.colors = colors;
            m.triangles = tris;
            m.RecalculateBounds();
        }
    }
}
