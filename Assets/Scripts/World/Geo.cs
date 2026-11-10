using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>Hình học 2D (x, z) dùng chung.</summary>
    public static class Geo
    {
        public static bool SegX(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, out Vector2 x)
        {
            x = default;
            Vector2 r = p2 - p, s = q2 - q;
            float den = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(den) < 1e-6f) return false;
            Vector2 qp = q - p;
            float t = (qp.x * s.y - qp.y * s.x) / den;
            float u = (qp.x * r.y - qp.y * r.x) / den;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            x = p + r * t;
            return true;
        }

        public static void Closest(List<Vector2> poly, Vector2 p, out int k, out Vector2 q, out float dist)
        {
            k = 0; q = poly.Count > 0 ? poly[0] : p; dist = float.MaxValue;
            for (int i = 0; i < poly.Count - 1; i++)
            {
                var a = poly[i]; var ab = poly[i + 1] - a;
                float l2 = ab.sqrMagnitude;
                float t = l2 < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                var c = a + ab * t;
                float d = Vector2.Distance(p, c);
                if (d < dist) { dist = d; k = i; q = c; }
            }
            if (poly.Count == 1) dist = Vector2.Distance(p, poly[0]);
        }

        public static float Len(List<Vector2> p)
        {
            float l = 0;
            for (int i = 1; i < p.Count; i++) l += Vector2.Distance(p[i - 1], p[i]);
            return l;
        }

        public static Vector2 PointAt(List<Vector2> poly, float distance)
        {
            float acc = 0;
            for (int i = 0; i < poly.Count - 1; i++)
            {
                float l = Vector2.Distance(poly[i], poly[i + 1]);
                if (acc + l >= distance && l > 1e-6f) return Vector2.Lerp(poly[i], poly[i + 1], (distance - acc) / l);
                acc += l;
            }
            return poly[poly.Count - 1];
        }

        public static List<Vector2> Resample(List<Vector2> p, float step)
        {
            var r = new List<Vector2>();
            float len = Len(p);
            if (p.Count < 2 || len < 1e-4f) { r.AddRange(p); return r; }
            int n = Mathf.Max(1, Mathf.RoundToInt(len / step));
            for (int i = 0; i <= n; i++) r.Add(PointAt(p, len * i / n));
            return r;
        }

        public static Rect Bounds(List<Vector2> poly, float pad)
        {
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            foreach (var p in poly) { x0 = Mathf.Min(x0, p.x); z0 = Mathf.Min(z0, p.y); x1 = Mathf.Max(x1, p.x); z1 = Mathf.Max(z1, p.y); }
            return x0 > x1 ? new Rect(0, 0, 0, 0) : Rect.MinMaxRect(x0 - pad, z0 - pad, x1 + pad, z1 + pad);
        }

        public static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
        public static Vector3 X0Z(Vector2 v) => new Vector3(v.x, 0, v.y);
    }
}
