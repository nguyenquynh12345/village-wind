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

        /// <summary>
        /// Chi tiết thủ công M15 cho đoạn tường (gộp chung vào 1 DetailMesh cho mỗi đoạn tường):
        /// - Kiểu 0, 1, 3 (tường đá / bờ rào thấp): đá xếp từng viên so le theo chiều dài cộng dồn dọc 2 mặt tường.
        /// - Kiểu 2 (hàng rào gỗ): ván gỗ dọc so le + thanh giằng ngang dọc 2 mặt.
        /// </summary>
        public static void AddSurfaceDetail(DetailMesh dm, List<Vector3> pts, bool closed, float[] ground, float[] top, float thickness, int style, Vector3 origin)
        {
            int n = pts.Count;
            int segs = closed ? n : n - 1;
            if (segs <= 0) return;
            float half = thickness * 0.5f;
            Color railWood = Palette.Hex("#6B4F3E");

            if (style == 2)
            {
                for (int i = 0; i < segs; i++)
                {
                    int j = (i + 1) % n;
                    var a = new Vector3(pts[i].x - origin.x, 0f, pts[i].z - origin.z);
                    var b = new Vector3(pts[j].x - origin.x, 0f, pts[j].z - origin.z);
                    var d = b - a;
                    float len = d.magnitude;
                    if (len < 0.15f) continue;
                    var dir = d / len;
                    var left = new Vector3(-dir.z, 0f, dir.x);

                    float y0A = ground[i] - 0.12f - origin.y;
                    float y0B = ground[j] - 0.12f - origin.y;
                    float y1A = top[i] - 0.04f - origin.y;
                    float y1B = top[j] - 0.04f - origin.y;

                    // Hàng rào gỗ: ván gỗ đứng sát nhau + 2 thanh giằng ngang
                    float avgH = ((y1A - y0A) + (y1B - y0B)) * 0.5f;
                    if (avgH < 0.2f) continue;
                    var rotL = Quaternion.LookRotation(left, Vector3.up);
                    var rotR = Quaternion.LookRotation(-left, Vector3.up);

                    float u = 0.06f;
                    while (u < len - 0.06f)
                    {
                        float pw = Mathf.Min(dm.Range(0.24f, 0.34f), len - u);
                        if (pw < 0.1f) break;
                        float t = (u + pw * 0.5f) / len;
                        float yb = Mathf.Lerp(y0A, y0B, t);
                        float yt = Mathf.Lerp(y1A, y1B, t) + dm.Range(-0.04f, 0.04f);
                        float ph = Mathf.Max(0.2f, yt - yb);
                        var mid = Vector3.Lerp(a, b, t) + Vector3.up * (yb + ph * 0.5f);
                        var colL = dm.Vary(Palette.Wood, 0.08f);
                        var colR = dm.Vary(Palette.Wood, 0.08f);
                        dm.Box(mid + left * (half + 0.015f), new Vector3(pw - 0.025f, ph, 0.04f), rotL, colL, skipBottom: true, skipBack: true);
                        dm.Box(mid - left * (half + 0.015f), new Vector3(pw - 0.025f, ph, 0.04f), rotR, colR, skipBottom: true, skipBack: true);
                        u += pw;
                    }

                    // Thanh giằng ngang trên và dưới
                    foreach (float rf in new[] { 0.28f, 0.76f })
                    {
                        var rA = a + Vector3.up * Mathf.Lerp(y0A, y1A, rf);
                        var rB = b + Vector3.up * Mathf.Lerp(y0B, y1B, rf);
                        var rD = rB - rA;
                        float rLen = rD.magnitude;
                        if (rLen < 0.1f) continue;
                        var rRot = Quaternion.LookRotation(rD / rLen, Vector3.up);
                        var rMid = (rA + rB) * 0.5f;
                        dm.Box(rMid + left * (half + 0.035f), new Vector3(0.05f, 0.11f, rLen), rRot, dm.Vary(railWood, 0.05f), skipBottom: true, skipBack: true);
                        dm.Box(rMid - left * (half + 0.035f), new Vector3(0.05f, 0.11f, rLen), rRot, dm.Vary(railWood, 0.05f), skipBottom: true, skipBack: true);
                    }
                }
                return;
            }

            // Tường đá (kiểu 0, 1) và bờ rào thấp (kiểu 3): xếp đá theo chiều dài cộng dồn dọc 2 mặt
            var s = new float[segs + 1];
            float hSum = 0f;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n;
                var a = new Vector3(pts[i].x - origin.x, 0f, pts[i].z - origin.z);
                var b = new Vector3(pts[j].x - origin.x, 0f, pts[j].z - origin.z);
                float segLen = (b - a).magnitude;
                s[i + 1] = s[i] + segLen;
                float hA = (top[i] - 0.04f) - (ground[i] - 0.12f);
                float hB = (top[j] - 0.04f) - (ground[j] - 0.12f);
                hSum += (hA + hB) * 0.5f * segLen;

                if (style == 3 && segLen >= 0.15f)
                {
                    // Bờ rào thấp: gờ đá mũ trên đỉnh
                    var tA = a + Vector3.up * (top[i] - origin.y + 0.02f);
                    var tB = b + Vector3.up * (top[j] - origin.y + 0.02f);
                    var tD = tB - tA;
                    if (tD.sqrMagnitude > 1e-4f)
                        dm.Box((tA + tB) * 0.5f, new Vector3(thickness + 0.1f, 0.07f, tD.magnitude + 0.02f),
                               Quaternion.LookRotation(tD.normalized), dm.Vary(Palette.StoneDark, 0.05f), skipBottom: true);
                }
            }

            float totalLen = s[segs];
            if (totalLen < 0.15f) return;
            float avgWallH = hSum / totalLen;
            if (avgWallH < 0.08f) return;

            float chMin = style == 3 ? 0.22f : 0.36f;
            float chMax = style == 3 ? 0.32f : 0.48f;
            float slMin = style == 3 ? 0.42f : 0.62f;
            float slMax = style == 3 ? 0.78f : 1.12f;
            const float depth = 0.08f;
            Color foundationStone = Palette.Hex("#B5AC9F");
            Color foundationDark = Palette.Hex("#958D82");
            Color moss = Palette.Hex("#8E9A78");

            foreach (float side in new[] { 1f, -1f })
            {
                float f = 0f;
                int course = 0;
                while (f < avgWallH - 0.02f)
                {
                    float ch = Mathf.Min(dm.Range(chMin, chMax), avgWallH - f);
                    float fMid = (f + ch * 0.5f) / avgWallH;
                    float u = 0f;
                    bool oddStart = (course % 2 == 1);
                    int segIdx = 0;

                    while (u < totalLen - 0.05f)
                    {
                        float sl = oddStart
                            ? Mathf.Min(dm.Range(0.1f, 0.25f), totalLen - u)
                            : Mathf.Min(dm.Range(slMin, slMax), totalLen - u);
                        oddStart = false;
                        if (sl < 0.08f) break;

                        float midU = u + sl * 0.5f;
                        while (segIdx < segs - 1 && s[segIdx + 1] < midU) segIdx++;
                        float segLen = s[segIdx + 1] - s[segIdx];
                        if (segLen > 1e-4f)
                        {
                            int j = (segIdx + 1) % n;
                            float t = Mathf.Clamp01((midU - s[segIdx]) / segLen);
                            var a = new Vector3(pts[segIdx].x - origin.x, 0f, pts[segIdx].z - origin.z);
                            var b = new Vector3(pts[j].x - origin.x, 0f, pts[j].z - origin.z);
                            var dir = (b - a) / segLen;
                            var nrm = new Vector3(-dir.z, 0f, dir.x) * side;

                            float y0A = ground[segIdx] - 0.12f - origin.y;
                            float y0B = ground[j] - 0.12f - origin.y;
                            float y1A = top[segIdx] - 0.04f - origin.y;
                            float y1B = top[j] - 0.04f - origin.y;
                            float yBot = Mathf.Lerp(y0A, y0B, t);
                            float yTop = Mathf.Lerp(y1A, y1B, t);
                            float localH = yTop - yBot;

                            if (localH > 0.08f)
                            {
                                float cy = Mathf.Lerp(yBot, yTop, fMid);
                                float stoneH = ch * (localH / avgWallH);
                                float roll = dm.Rand();
                                var col = roll < 0.06f ? dm.Vary(moss, 0.08f)
                                        : roll < 0.3f  ? dm.Vary(foundationDark, 0.06f)
                                                       : dm.Vary(foundationStone, 0.08f);
                                var center = Vector3.Lerp(a, b, t) + nrm * (half + dm.Range(0f, 0.035f)) + Vector3.up * cy;
                                var baseRot = Quaternion.LookRotation(nrm, Vector3.up);
                                var rot = baseRot * Quaternion.Euler(dm.Range(-2f, 2f), dm.Range(-3f, 3f), dm.Range(-2.5f, 2.5f));
                                dm.Box(center, new Vector3(Mathf.Max(0.05f, sl - 0.035f), Mathf.Max(0.05f, stoneH - 0.03f), depth), rot, col, skipBottom: true, skipBack: true);
                            }
                        }
                        u += sl;
                    }
                    f += ch;
                    course++;
                }
            }
        }
    }
}
