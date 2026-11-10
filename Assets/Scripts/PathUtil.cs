using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>Xử lý nét vẽ: đơn giản hoá (tìm góc gấp), làm mượt, chia đều điểm.</summary>
    public static class PathUtil
    {
        public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        public static float Length(List<Vector3> p)
        {
            float l = 0;
            for (int i = 1; i < p.Count; i++) l += Vector3.Distance(p[i - 1], p[i]);
            return l;
        }

        /// <summary>Ramer–Douglas–Peucker: trả về chỉ số các điểm "quan trọng" (luôn gồm điểm đầu và cuối).</summary>
        public static List<int> SimplifyIndices(List<Vector3> p, float eps)
        {
            var result = new List<int>();
            if (p.Count == 0) return result;
            var keep = new bool[p.Count];
            keep[0] = keep[p.Count - 1] = true;
            if (p.Count > 2) Rdp(p, 0, p.Count - 1, eps, keep);
            for (int i = 0; i < p.Count; i++) if (keep[i]) result.Add(i);
            return result;
        }

        static void Rdp(List<Vector3> p, int a, int b, float eps, bool[] keep)
        {
            float maxD = 0; int idx = -1;
            for (int i = a + 1; i < b; i++)
            {
                float d = DistToSeg(p[i], p[a], p[b]);
                if (d > maxD) { maxD = d; idx = i; }
            }
            if (idx > 0 && maxD > eps)
            {
                keep[idx] = true;
                Rdp(p, a, idx, eps, keep);
                Rdp(p, idx, b, eps, keep);
            }
        }

        static float DistToSeg(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = a.y = b.y = 0;
            Vector3 ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 < 1e-8f) return Vector3.Distance(p, a);
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2);
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>Làm mượt Chaikin, giữ nguyên điểm đầu và cuối.</summary>
        public static List<Vector3> Chaikin(List<Vector3> p, int iterations)
        {
            var cur = p;
            for (int it = 0; it < iterations; it++)
            {
                if (cur.Count < 3) break;
                var next = new List<Vector3>(cur.Count * 2) { cur[0] };
                for (int i = 0; i < cur.Count - 1; i++)
                {
                    next.Add(Vector3.Lerp(cur[i], cur[i + 1], 0.25f));
                    next.Add(Vector3.Lerp(cur[i], cur[i + 1], 0.75f));
                }
                next.Add(cur[cur.Count - 1]);
                cur = next;
            }
            return cur;
        }

        /// <summary>Chia lại đường thành các đoạn dài đều nhau (~step mét).</summary>
        public static List<Vector3> Resample(List<Vector3> p, float step)
        {
            var r = new List<Vector3>();
            float len = Length(p);
            if (p.Count < 2 || len < 1e-4f) { r.AddRange(p); return r; }

            int n = Mathf.Max(1, Mathf.RoundToInt(len / step));
            float st = len / n;
            r.Add(p[0]);
            int seg = 0;
            float segStart = 0, segLen = Vector3.Distance(p[0], p[1]);
            for (int k = 1; k < n; k++)
            {
                float d = k * st;
                while (seg < p.Count - 2 && d > segStart + segLen)
                {
                    segStart += segLen;
                    seg++;
                    segLen = Vector3.Distance(p[seg], p[seg + 1]);
                }
                float t = segLen > 1e-6f ? (d - segStart) / segLen : 0f;
                r.Add(Vector3.Lerp(p[seg], p[seg + 1], Mathf.Clamp01(t)));
            }
            r.Add(p[p.Count - 1]);
            return r;
        }

        /// <summary>Quãng đường dọc theo path tới điểm gần q nhất (dùng để xếp thứ tự animation).</summary>
        public static float ArcAt(List<Vector3> path, Vector3 q)
        {
            float acc = 0, best = float.MaxValue, bestArc = 0;
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) acc += Vector3.Distance(path[i - 1], path[i]);
                float d = (Flat(path[i] - q)).sqrMagnitude;
                if (d < best) { best = d; bestArc = acc; }
            }
            return bestArc;
        }
    }
}
