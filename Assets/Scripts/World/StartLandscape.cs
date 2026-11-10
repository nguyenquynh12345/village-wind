using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Cảnh khởi đầu cho làng mới: khoảng đất bằng ở giữa để xây nhà, xung quanh là đồi thoải,
    /// một hồ lớn + một ao nhỏ, rừng thông / rừng cây tròn, liễu ven hồ, cây hoa, đồng hoa dại,
    /// bãi cát ven hồ, đường đất từ bìa rừng dẫn vào giữa. Mỗi seed cho một cảnh khác.
    /// </summary>
    public static class StartLandscape
    {
        const float Clear = 24f;      // bán kính khoảng đất trống giữa làng (không cây, không ao)

        struct Lake { public Vector2 c; public float rx, rz, rot; }

        public static void Generate(int seed)
        {
            var hf = HeightField.I;
            if (!hf) return;
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            float half = GameConfig.WorldHalf;

            // ---- Địa hình: giữa gần phẳng, ra xa thành đồi thoải, mép thế giới cao lên như vành thung lũng ----
            var h = hf.BaseHeights();
            int n = Mathf.RoundToInt(Mathf.Sqrt(h.Length));
            float cell = 2f * half / (n - 1);
            float ox = R(0f, 100f), oz = R(0f, 100f);
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float wx = -half + x * cell, wz = -half + z * cell;
                    float d = Mathf.Sqrt(wx * wx + wz * wz);
                    float hills = Mathf.PerlinNoise(wx * 0.03f + ox, wz * 0.03f + oz) * 0.65f + Mathf.PerlinNoise(wx * 0.08f + oz, wz * 0.08f + ox) * 0.35f;
                    float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Clear + 8f, half * 0.95f, d));
                    float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.7f, half, Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz))));
                    float y = (hills - 0.45f) * Mathf.Lerp(0.5f, 5.5f, rise) + rim * 3.5f;
                    if (d < Clear) y *= Mathf.Lerp(0.35f, 1f, d / Clear);   // sân làng: chỉ gợn nhẹ
                    h[z * n + x] = y;
                }

            // hồ cần lòng chảo phẳng: hạ nhẹ đất quanh vị trí hồ trước khi đặt
            var lakes = new List<Lake>();
            float la = R(0f, Mathf.PI * 2f);
            lakes.Add(new Lake { c = Dir(la) * R(38f, 46f), rx = R(10f, 13f), rz = R(7f, 9f), rot = R(0f, 180f) });
            lakes.Add(new Lake { c = Dir(la + R(2.2f, 3.6f)) * R(32f, 40f), rx = R(4.5f, 6f), rz = R(3.5f, 4.5f), rot = R(0f, 180f) });
            foreach (var lk in lakes)
            {
                float lr = Mathf.Max(lk.rx, lk.rz) + 6f;
                float baseY = 0f;
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        float wx = -half + x * cell, wz = -half + z * cell;
                        float d = Vector2.Distance(new Vector2(wx, wz), lk.c);
                        if (d > lr + 6f) continue;
                        float k = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(lr, lr + 6f, d));
                        h[z * n + x] = Mathf.Lerp(h[z * n + x], baseY, k);
                    }
            }
            hf.SetBase(h);

            // ---- Hồ, ao ----
            foreach (var lk in lakes)
            {
                var poly = new List<Vector2>();
                float ph = R(0f, 6f);
                var q = Quaternion.Euler(0, 0, lk.rot);
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI * 2f / 32f;
                    float wob = 1f + 0.12f * Mathf.Sin(a * 3f + ph) + 0.06f * Mathf.Sin(a * 5f + ph * 2f);
                    Vector2 p = new Vector2(Mathf.Cos(a) * lk.rx, Mathf.Sin(a) * lk.rz) * wob;
                    p = (Vector2)(q * p);
                    poly.Add(lk.c + p);
                }
                var mask = PondMask.Rasterize(poly);
                if (mask == null) continue;
                var pp = new PondParams { deep = lk.rx > 8f, seed = rng.Next(1000) };
                mask.WriteTo(pp);
                EntityManager.Spawn("Pond", JsonUtility.ToJson(pp));
                // bãi cát mỏng dọc bờ, chỉ vài khúc (không bao kín) cho tự nhiên
                for (int i = 0; i < 28; i++)
                {
                    float a = i * Mathf.PI * 2f / 28f;
                    if (Mathf.Sin(a * 2f + ph) < -0.2f) continue;
                    var e = lk.c + (Vector2)(q * new Vector2(Mathf.Cos(a) * (lk.rx + 0.6f), Mathf.Sin(a) * (lk.rz + 0.6f)));
                    hf.PaintBrush(new Vector3(e.x, 0, e.y), 1.6f, 3);
                }
            }

            bool NearLake(Vector2 p, float margin)
            {
                foreach (var lk in lakes)
                {
                    var d = p - lk.c;
                    var l = (Vector2)(Quaternion.Euler(0, 0, -lk.rot) * d);
                    float e = (l.x * l.x) / ((lk.rx + margin) * (lk.rx + margin)) + (l.y * l.y) / ((lk.rz + margin) * (lk.rz + margin));
                    if (e < 1f) return true;
                }
                return false;
            }

            // ---- Đường đất: từ bìa rừng vào giữa làng ----
            float ra = la + Mathf.PI + R(-0.6f, 0.6f);
            var road = new List<Vector2>();
            Vector2 start = Dir(ra) * (half - 6f), end = Dir(ra) * 6f;
            Vector2 side = new Vector2(-Dir(ra).y, Dir(ra).x);
            float bend = R(12f, 20f) * (R(0f, 1f) < 0.5f ? -1f : 1f);
            Vector2 c1 = Vector2.Lerp(start, end, 0.3f) + side * bend, c2 = Vector2.Lerp(start, end, 0.7f) - side * bend * 0.6f;
            for (int i = 0; i <= 48; i++)
            {
                float t = i / 48f, u = 1f - t;
                road.Add(u * u * u * start + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * end);   // đường cong Bézier uốn lượn
            }
            var pathP = new PathParams { width = 2.2f, style = 0 };
            for (int i = 0; i < road.Count - 1; i++)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(road[i], road[i + 1]) / 0.8f);
                for (int k = 0; k < steps; k++) pathP.pts.Add(Vector2.Lerp(road[i], road[i + 1], k / (float)steps));
            }
            pathP.pts.Add(road[road.Count - 1]);
            EntityManager.Spawn("Path", JsonUtility.ToJson(pathP));
            hf.PaintBrush(new Vector3(end.x, 0, end.y), 4.5f, 2);   // sân sỏi nhỏ cuối đường: chỗ dựng nhà đầu tiên

            bool NearRoad(Vector2 p, float m)
            {
                for (int i = 0; i < road.Count; i += 2)
                    if ((road[i] - p).sqrMagnitude < m * m) return true;
                return false;
            }

            var trees = new List<Vector2>();
            bool Free(Vector2 p, float minGap)
            {
                if (p.magnitude < Clear || Mathf.Abs(p.x) > half - 3f || Mathf.Abs(p.y) > half - 3f) return false;
                if (NearLake(p, 1.5f) || NearRoad(p, 3f)) return false;
                foreach (var t in trees) if ((t - p).sqrMagnitude < minGap * minGap) return false;
                return true;
            }
            void Plant(Vector2 p, int type, float size)
            {
                trees.Add(p);
                EntityManager.Spawn("Tree", JsonUtility.ToJson(new TreeParams { x = p.x, z = p.y, type = type, size = size, rot = R(0f, 360f) }));
            }

            // ---- Rừng: 3 cánh rừng (thông / cây tròn / pha trộn) ở vòng ngoài ----
            float[] forestAng = { la + R(1.2f, 1.8f), la - R(1.2f, 1.8f), la + Mathf.PI + R(0.9f, 1.4f), la + Mathf.PI - R(0.9f, 1.4f) };
            for (int f = 0; f < forestAng.Length; f++)
            {
                var fc = Dir(forestAng[f]) * R(52f, 64f);
                float fr = R(15f, 21f);
                int count = f >= 2 ? 26 : 38;
                for (int k = 0, tries = 0; k < count && tries < count * 12; tries++)
                {
                    float a = R(0f, Mathf.PI * 2f), r = Mathf.Sqrt(R(0f, 1f)) * fr;
                    var p = fc + Dir(a) * r;
                    if (!Free(p, 2.3f)) continue;
                    int type = f == 0 || f == 3 ? (R(0f, 1f) < 0.85f ? 1 : 0) : f == 1 ? (R(0f, 1f) < 0.8f ? 0 : 2) : (R(0f, 1f) < 0.5f ? 1 : 0);
                    float edge = r / fr;   // cây bìa rừng nhỏ hơn, giữa rừng to hơn
                    Plant(p, type, Mathf.Lerp(1.7f, 1.0f, edge) * R(0.85f, 1.15f));
                    k++;
                }
                // bụi cây ở bìa rừng
                for (int k = 0, tries = 0; k < 6 && tries < 60; tries++)
                {
                    var p = fc + Dir(R(0f, Mathf.PI * 2f)) * (fr + R(0f, 4f));
                    if (!Free(p, 2f)) continue;
                    Plant(p, 2, R(0.7f, 1.1f)); k++;
                }
            }

            // ---- Liễu + cây hoa ven hồ ----
            foreach (var lk in lakes)
            {
                int willows = lk.rx > 8f ? 3 : 1;
                for (int k = 0, tries = 0; k < willows && tries < 40; tries++)
                {
                    float a = R(0f, Mathf.PI * 2f);
                    var p = lk.c + (Vector2)(Quaternion.Euler(0, 0, lk.rot) * new Vector2(Mathf.Cos(a) * (lk.rx + 3f), Mathf.Sin(a) * (lk.rz + 3f)));
                    if (!Free(p, 5f)) continue;
                    Plant(p, 4, R(0.9f, 1.2f)); k++;
                }
            }
            for (int k = 0, tries = 0; k < 5 && tries < 80; tries++)
            {
                var p = Dir(R(0f, Mathf.PI * 2f)) * R(Clear + 2f, Clear + 14f);
                if (!Free(p, 7f)) continue;
                Plant(p, 3, R(0.9f, 1.2f)); k++;
            }

            // ---- Cây lẻ rải rác ngoài sân làng ----
            for (int k = 0, tries = 0; k < 16 && tries < 300; tries++)
            {
                var p = Dir(R(0f, Mathf.PI * 2f)) * R(Clear + 4f, half - 6f);
                if (!Free(p, 6f)) continue;
                float u = R(0f, 1f);
                Plant(p, u < 0.45f ? 0 : u < 0.75f ? 2 : 1, R(0.8f, 1.3f)); k++;
            }

            // ---- Tô đất: đồng hoa dại, sỏi dưới chân đồi ----
            for (int k = 0; k < 4; k++)
            {
                var p = Dir(R(0f, Mathf.PI * 2f)) * R(Clear + 4f, half - 15f);
                if (NearLake(p, 2f)) continue;
                hf.PaintBrush(new Vector3(p.x, 0, p.y), R(3.5f, 6f), 1);
            }
            for (int k = 0; k < 4; k++)
            {
                var p = Dir(R(0f, Mathf.PI * 2f)) * R(half - 25f, half - 10f);
                hf.PaintBrush(new Vector3(p.x, 0, p.y), R(2.5f, 4f), 2);
            }
        }

        static Vector2 Dir(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }
}
