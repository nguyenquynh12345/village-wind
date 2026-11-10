using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Hình dạng ao: lưới ô 0.5 m (có nước / không). Mọi ao dùng chung 1 lưới toàn cục
    /// nên gộp / trừ ao chỉ là phép OR / AND trên ô.
    /// </summary>
    public class PondMask
    {
        public const float C = GameConfig.PondCell;
        public int x0, z0, w, h;   // ô góc dưới-trái (chỉ số toàn cục) + kích thước
        public bool[] b;

        public PondMask(int x0, int z0, int w, int h)
        {
            this.x0 = x0; this.z0 = z0;
            this.w = Mathf.Max(0, w); this.h = Mathf.Max(0, h);
            b = new bool[this.w * this.h];
        }

        public static int Cell(float world) => Mathf.FloorToInt(world / C);
        public static float Center(int cell) => (cell + 0.5f) * C;

        public bool Get(int gx, int gz)
        {
            int lx = gx - x0, lz = gz - z0;
            return lx >= 0 && lz >= 0 && lx < w && lz < h && b[lz * w + lx];
        }

        public void Set(int gx, int gz, bool v)
        {
            int lx = gx - x0, lz = gz - z0;
            if (lx >= 0 && lz >= 0 && lx < w && lz < h) b[lz * w + lx] = v;
        }

        public bool ContainsWorld(float x, float z) => Get(Cell(x), Cell(z));

        public int Count
        {
            get { int c = 0; foreach (var v in b) if (v) c++; return c; }
        }

        public float Area => Count * C * C;

        public Rect WorldRect => new Rect(x0 * C, z0 * C, w * C, h * C);

        public PondMask Clone()
        {
            var m = new PondMask(x0, z0, w, h);
            Array.Copy(b, m.b, b.Length);
            return m;
        }

        /// <summary>Tỉ lệ ô có nước trong bán kính r quanh (x, z) — dùng làm bờ dốc thoải.</summary>
        public float Coverage(float x, float z, float r)
        {
            int cx0 = Cell(x - r), cx1 = Cell(x + r), cz0 = Cell(z - r), cz1 = Cell(z + r);
            int tot = 0, hit = 0;
            float r2 = r * r;
            for (int gz = cz0; gz <= cz1; gz++)
                for (int gx = cx0; gx <= cx1; gx++)
                {
                    float dx = Center(gx) - x, dz = Center(gz) - z;
                    if (dx * dx + dz * dz > r2) continue;
                    tot++;
                    if (Get(gx, gz)) hit++;
                }
            return tot == 0 ? 0f : (float)hit / tot;
        }

        /// <summary>Thu gọn khung về đúng phần có nước. Trả về null nếu rỗng.</summary>
        public PondMask Trim()
        {
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            for (int lz = 0; lz < h; lz++)
                for (int lx = 0; lx < w; lx++)
                    if (b[lz * w + lx])
                    {
                        minX = Mathf.Min(minX, lx); maxX = Mathf.Max(maxX, lx);
                        minZ = Mathf.Min(minZ, lz); maxZ = Mathf.Max(maxZ, lz);
                    }
            if (minX == int.MaxValue) return null;
            var m = new PondMask(x0 + minX, z0 + minZ, maxX - minX + 1, maxZ - minZ + 1);
            for (int lz = 0; lz < m.h; lz++)
                for (int lx = 0; lx < m.w; lx++)
                    m.b[lz * m.w + lx] = b[(lz + minZ) * w + lx + minX];
            return m;
        }

        public static PondMask Union(PondMask a, PondMask c)
        {
            if (a == null) return c?.Clone();
            if (c == null) return a.Clone();
            int nx0 = Mathf.Min(a.x0, c.x0), nz0 = Mathf.Min(a.z0, c.z0);
            int nx1 = Mathf.Max(a.x0 + a.w, c.x0 + c.w), nz1 = Mathf.Max(a.z0 + a.h, c.z0 + c.h);
            var m = new PondMask(nx0, nz0, nx1 - nx0, nz1 - nz0);
            for (int gz = nz0; gz < nz1; gz++)
                for (int gx = nx0; gx < nx1; gx++)
                    if (a.Get(gx, gz) || c.Get(gx, gz)) m.Set(gx, gz, true);
            return m;
        }

        /// <summary>a trừ đi c (ô nào c có nước thì a mất nước).</summary>
        public static PondMask Subtract(PondMask a, PondMask c)
        {
            var m = a.Clone();
            for (int gz = a.z0; gz < a.z0 + a.h; gz++)
                for (int gx = a.x0; gx < a.x0 + a.w; gx++)
                    if (c.Get(gx, gz)) m.Set(gx, gz, false);
            return m.Trim();
        }

        /// <summary>Hai ao chạm nhau (kể cả chỉ sát cạnh 1 ô).</summary>
        public bool Touches(PondMask o)
        {
            var r1 = new RectInt(x0 - 1, z0 - 1, w + 2, h + 2);
            var r2 = new RectInt(o.x0, o.z0, o.w, o.h);
            if (!r1.Overlaps(r2)) return false;
            for (int gz = z0; gz < z0 + h; gz++)
                for (int gx = x0; gx < x0 + w; gx++)
                {
                    if (!Get(gx, gz)) continue;
                    if (o.Get(gx, gz) || o.Get(gx + 1, gz) || o.Get(gx - 1, gz) || o.Get(gx, gz + 1) || o.Get(gx, gz - 1)) return true;
                }
            return false;
        }

        /// <summary>Tô hình tròn (v = true thêm nước, false bớt). Trả về mặt nạ mới (khung có thể to ra).</summary>
        public static PondMask Stamp(PondMask m, Vector2 c, float r, bool v)
        {
            int cx0 = Cell(c.x - r), cx1 = Cell(c.x + r), cz0 = Cell(c.y - r), cz1 = Cell(c.y + r);
            PondMask res;
            if (m == null) res = new PondMask(cx0, cz0, cx1 - cx0 + 1, cz1 - cz0 + 1);
            else if (v)
            {
                int nx0 = Mathf.Min(m.x0, cx0), nz0 = Mathf.Min(m.z0, cz0);
                int nx1 = Mathf.Max(m.x0 + m.w, cx1 + 1), nz1 = Mathf.Max(m.z0 + m.h, cz1 + 1);
                res = new PondMask(nx0, nz0, nx1 - nx0, nz1 - nz0);
                for (int gz = m.z0; gz < m.z0 + m.h; gz++)
                    for (int gx = m.x0; gx < m.x0 + m.w; gx++)
                        if (m.Get(gx, gz)) res.Set(gx, gz, true);
            }
            else res = m.Clone();

            float r2 = r * r;
            for (int gz = cz0; gz <= cz1; gz++)
                for (int gx = cx0; gx <= cx1; gx++)
                {
                    float dx = Center(gx) - c.x, dz = Center(gz) - c.y;
                    if (dx * dx + dz * dz <= r2) res.Set(gx, gz, v);
                }
            return res;
        }

        /// <summary>Tô đặc một đa giác (vòng kín).</summary>
        public static PondMask Rasterize(List<Vector2> poly)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var p in poly) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y); }
            int gx0 = Cell(minX), gx1 = Cell(maxX), gz0 = Cell(minZ), gz1 = Cell(maxZ);
            var m = new PondMask(gx0, gz0, gx1 - gx0 + 1, gz1 - gz0 + 1);
            for (int gz = gz0; gz <= gz1; gz++)
                for (int gx = gx0; gx <= gx1; gx++)
                    if (Inside(poly, new Vector2(Center(gx), Center(gz)))) m.Set(gx, gz, true);
            return m.Trim();
        }

        static bool Inside(List<Vector2> poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = poly[i]; var c = poly[j];
                if ((a.y > p.y) != (c.y > p.y) && p.x < (c.x - a.x) * (p.y - a.y) / (c.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Biến dạng: ô đích lấy giá trị từ src(p − d·w(|p − anchor|)), w giảm dần theo khoảng cách.</summary>
        public static PondMask Warp(PondMask src, Vector2 anchor, Vector2 d, float falloff)
        {
            int pad = Mathf.CeilToInt((d.magnitude + 1f) / C);
            var m = new PondMask(src.x0 - pad, src.z0 - pad, src.w + 2 * pad, src.h + 2 * pad);
            for (int gz = m.z0; gz < m.z0 + m.h; gz++)
                for (int gx = m.x0; gx < m.x0 + m.w; gx++)
                {
                    var p = new Vector2(Center(gx), Center(gz));
                    float r = Vector2.Distance(p, anchor) / falloff;
                    var s = p - d * Mathf.Exp(-r * r);
                    m.Set(gx, gz, src.ContainsWorld(s.x, s.y));
                }
            return m.Trim();
        }

        /// <summary>Xoay quanh tâm (độ).</summary>
        public static PondMask Rotate(PondMask src, Vector2 center, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad, cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            var wr = src.WorldRect;
            float R = Vector2.Distance(center, wr.min) + Vector2.Distance(wr.min, wr.max);
            int gx0 = Cell(center.x - R), gx1 = Cell(center.x + R), gz0 = Cell(center.y - R), gz1 = Cell(center.y + R);
            var m = new PondMask(gx0, gz0, gx1 - gx0 + 1, gz1 - gz0 + 1);
            for (int gz = gz0; gz <= gz1; gz++)
                for (int gx = gx0; gx <= gx1; gx++)
                {
                    var p = new Vector2(Center(gx), Center(gz)) - center;
                    // Quaternion.Euler(0, θ, 0): (x, z) → (x·cos + z·sin, −x·sin + z·cos). Điểm nguồn = xoay ngược.
                    var s = new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs) + center;
                    m.Set(gx, gz, src.ContainsWorld(s.x, s.y));
                }
            return m.Trim();
        }

        public Vector2 Centroid()
        {
            Vector2 s = Vector2.zero; int c = 0;
            for (int lz = 0; lz < h; lz++)
                for (int lx = 0; lx < w; lx++)
                    if (b[lz * w + lx]) { s += new Vector2(Center(x0 + lx), Center(z0 + lz)); c++; }
            return c > 0 ? s / c : new Vector2(Center(x0), Center(z0));
        }

        /// <summary>Ô có nước nằm sát ô không nước (bờ ao).</summary>
        public List<Vector2Int> Contour()
        {
            var l = new List<Vector2Int>();
            for (int gz = z0; gz < z0 + h; gz++)
                for (int gx = x0; gx < x0 + w; gx++)
                    if (Get(gx, gz) && (!Get(gx + 1, gz) || !Get(gx - 1, gz) || !Get(gx, gz + 1) || !Get(gx, gz - 1)))
                        l.Add(new Vector2Int(gx, gz));
            return l;
        }

        // ---- Lưu vào tham số ----
        public void WriteTo(PondParams p)
        {
            p.x0 = x0; p.z0 = z0; p.w = w; p.h = h;
            var bytes = new byte[(b.Length + 7) / 8];
            for (int i = 0; i < b.Length; i++) if (b[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
            p.bits = Convert.ToBase64String(bytes);
        }

        public static PondMask FromParams(PondParams p)
        {
            var m = new PondMask(p.x0, p.z0, p.w, p.h);
            if (string.IsNullOrEmpty(p.bits)) return m;
            var bytes = Convert.FromBase64String(p.bits);
            for (int i = 0; i < m.b.Length && (i >> 3) < bytes.Length; i++) m.b[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
            return m;
        }
    }
}
