using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Đùn một đường thành khối tường có độ dày, nối góc kiểu miter.
    /// Chân tường bám mặt đất từng điểm; đỉnh tường theo mảng <c>top</c> (đã làm mượt).
    /// </summary>
    public static class WallMesh
    {
        /// <param name="pts">Đường giữa tường (chỉ dùng x, z). Vòng kín thì KHÔNG lặp điểm đầu ở cuối.</param>
        /// <param name="top">Độ cao đỉnh tường tại mỗi điểm (thế giới).</param>
        /// <param name="origin">Gốc toạ độ của mesh (đỉnh trả về = thế giới − origin).</param>
        public static Mesh Build(List<Vector3> pts, bool closed, float[] top, float thickness, Vector3 origin)
        {
            int n = pts.Count;
            var L = new Vector3[n];
            var R = new Vector3[n];
            float half = thickness * 0.5f;

            for (int i = 0; i < n; i++)
            {
                Vector3 prev, next;
                if (closed) { prev = pts[(i - 1 + n) % n]; next = pts[(i + 1) % n]; }
                else { prev = i > 0 ? pts[i - 1] : pts[i]; next = i < n - 1 ? pts[i + 1] : pts[i]; }

                Vector3 d0 = PathUtil.Flat(pts[i] - prev), d1 = PathUtil.Flat(next - pts[i]);
                if (d0.sqrMagnitude < 1e-6f) d0 = d1;
                if (d1.sqrMagnitude < 1e-6f) d1 = d0;
                d0.Normalize(); d1.Normalize();

                Vector3 n0 = new Vector3(-d0.z, 0, d0.x), n1 = new Vector3(-d1.z, 0, d1.x); // bên trái
                Vector3 m = n0 + n1;
                if (m.sqrMagnitude < 1e-4f) m = n0;
                m.Normalize();
                float miter = 1f / Mathf.Max(0.4f, Vector3.Dot(m, n1));

                L[i] = PathUtil.Flat(pts[i]) + m * (half * miter);
                R[i] = PathUtil.Flat(pts[i]) - m * (half * miter);
            }

            var hf = HeightField.I;
            var Lb = new Vector3[n]; var Lt = new Vector3[n];
            var Rb = new Vector3[n]; var Rt = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float lb = Mathf.Min(hf.HeightAt(L[i].x, L[i].z) - 0.5f, top[i] - 0.3f);
                float rb = Mathf.Min(hf.HeightAt(R[i].x, R[i].z) - 0.5f, top[i] - 0.3f);
                Lb[i] = new Vector3(L[i].x, lb, L[i].z) - origin;
                Rb[i] = new Vector3(R[i].x, rb, R[i].z) - origin;
                Lt[i] = new Vector3(L[i].x, top[i], L[i].z) - origin;
                Rt[i] = new Vector3(R[i].x, top[i], R[i].z) - origin;
            }

            var mb = new MeshBuilder();
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n;
                Vector3 d = PathUtil.Flat(pts[j] - pts[i]);
                if (d.sqrMagnitude < 1e-6f) continue;
                d.Normalize();
                Vector3 left = new Vector3(-d.z, 0, d.x);

                mb.Quad(Lb[i], Lb[j], Lt[j], Lt[i], left);
                mb.Quad(Rb[i], Rb[j], Rt[j], Rt[i], -left);
                mb.Quad(Lt[i], Lt[j], Rt[j], Rt[i], Vector3.up);
            }

            if (!closed && n >= 2)
            {
                Vector3 ds = PathUtil.Flat(pts[1] - pts[0]).normalized;
                mb.Quad(Lb[0], Rb[0], Rt[0], Lt[0], -ds);
                Vector3 de = PathUtil.Flat(pts[n - 1] - pts[n - 2]).normalized;
                mb.Quad(Lb[n - 1], Rb[n - 1], Rt[n - 1], Lt[n - 1], de);
            }

            return mb.ToMesh("Wall");
        }
    }
}
