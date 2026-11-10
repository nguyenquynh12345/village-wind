using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Chi tiết thủ công cho nhà (M15): móng xếp đá từng viên, đá góc tường, khung gỗ, ván ốp,
    /// mái lợp từng hàng ngói so le, nóc ngói, ván diềm. Tất cả gộp thành vài lưới để chạy nhẹ.
    /// </summary>
    public partial class HouseEntity
    {
        static readonly Color FoundationStone = Palette.Hex("#B5AC9F");
        static readonly Color FoundationDark = Palette.Hex("#958D82");
        static readonly Color Moss = Palette.Hex("#8E9A78");
        static readonly Color Timber = Palette.Hex("#6B4F3E");
        static readonly Color[] QuoinColors = { Palette.Hex("#D9D1C4"), Palette.Hex("#A8763F"), Palette.Hex("#E3DCCF"), Palette.Hex("#E9E1D3") };

        DetailMesh detail;

        /// <summary>Hoa văn tường theo chất liệu tầng: vữa / ván gỗ / đá xếp / gạch.</summary>
        int WallPattern(int f) => Mat(f) switch { 0 => Palette.PatPlaster, 1 => Palette.PatWood, 2 => Palette.PatStone, _ => Palette.PatBrick };

        // ---------- Móng đá ----------
        /// <summary>Xếp các hàng đá so le dọc mặt phẳng từ đầu A tới đầu B (dùng chung cho móng nhà và tường thành).</summary>
        public static void AddStoneCourses(DetailMesh dm, Vector3 a, Vector3 b, float y0A, float y1A, float y0B, float y1B,
                                           Vector3 nrm, float faceDist, float chMin = 0.2f, float chMax = 0.28f,
                                           float slMin = 0.32f, float slMax = 0.62f, float depth = 0.16f)
        {
            var ab = b - a;
            ab.y = 0f;
            float len = ab.magnitude;
            if (len < 0.15f) return;
            var tan = ab / len;
            float hA = y1A - y0A, hB = y1B - y0B;
            float avgH = (hA + hB) * 0.5f;
            if (avgH < 0.08f) return;

            var baseRot = Quaternion.LookRotation(nrm, Vector3.up);
            float f = 0f;
            int course = 0;
            while (f < avgH - 0.02f)
            {
                float ch = Mathf.Min(dm.Range(chMin, chMax), avgH - f);
                float fMid = (f + ch * 0.5f) / avgH;
                float u = course % 2 == 0 ? 0f : dm.Range(0.1f, 0.25f);
                while (u < len - 0.05f)
                {
                    float sl = Mathf.Min(dm.Range(slMin, slMax), len - u);
                    if (sl < 0.1f) break;
                    float t = (u + sl * 0.5f) / len;
                    float yBot = Mathf.Lerp(y0A, y0B, t);
                    float yTop = Mathf.Lerp(y1A, y1B, t);
                    float localH = yTop - yBot;
                    if (localH > 0.08f)
                    {
                        float cy = Mathf.Lerp(yBot, yTop, fMid);
                        float stoneH = ch * (localH / avgH);
                        float roll = dm.Rand();
                        var col = roll < 0.06f ? dm.Vary(Moss, 0.08f)
                                : roll < 0.3f  ? dm.Vary(FoundationDark, 0.06f)
                                               : dm.Vary(FoundationStone, 0.08f);
                        var center = Vector3.Lerp(a, b, t) + nrm * (faceDist + dm.Range(0f, 0.035f)) + Vector3.up * cy;
                        var rot = baseRot * Quaternion.Euler(dm.Range(-2f, 2f), dm.Range(-3f, 3f), dm.Range(-2.5f, 2.5f));
                        dm.Box(center, new Vector3(sl - 0.035f, Mathf.Max(0.05f, stoneH - 0.03f), depth), rot, col, skipBottom: true, skipBack: true);
                    }
                    u += sl;
                }
                f += ch;
                course++;
            }
        }

        void BuildFoundationDetail(float w, float l, float plinthBottom)
        {
            var dm = detail;
            float bottom = Mathf.Max(plinthBottom, -1.6f);
            for (int s = 0; s < 4; s++)
            {
                var nrm = SideNormal(s);
                var tan = SideTangent(s);
                float face = (s < 2 ? w : l) * 0.5f + 0.15f;
                float len = (s < 2 ? l : w) + 0.3f;
                AddStoneCourses(dm, -tan * (len * 0.5f), tan * (len * 0.5f), bottom, 0f, bottom, 0f, nrm, face);
                // gờ đá trên cùng, nơi tường bắt đầu
                dm.Box(nrm * (face - 0.02f) + Vector3.up * 0.01f, s < 2 ? new Vector3(0.24f, 0.07f, len + 0.12f) : new Vector3(len + 0.12f, 0.07f, 0.24f),
                       Quaternion.identity, dm.Vary(FoundationStone, 0.03f), skipBottom: true);
            }
        }

        // ---------- Tường: đá góc, khung gỗ, ván ốp, đá lộ ----------
        void BuildWallDetail(int f, float y0, float fw, float fl)
        {
            var dm = detail;
            float fh = GameConfig.FloorHeight;
            int mat = Mat(f);
            float hx = fw * 0.5f, hz = fl * 0.5f;

            // ô cửa trên tầng này (để khung gỗ / đá né)
            var wins = new List<Win>();
            foreach (var o in Openings()) if (o.f == f) wins.Add(o);
            bool NearOpening(int s, float a, float pad)
            {
                if (f == 0 && s == DoorSide && Mathf.Abs(a) < 0.55f + pad) return true;
                foreach (var o in wins)
                    if (Mathf.Abs(o.s) % 4 == s && Mathf.Abs(o.a - a) < 0.5f + pad) return true;
                return false;
            }

            // Đá góc so le (vữa, đá, gạch)
            if (mat != 1)
            {
                var qc = QuoinColors[mat];
                int courses = Mathf.FloorToInt(fh / 0.34f);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        for (int k = 0; k < courses; k++)
                        {
                            float y = y0 + 0.19f + k * 0.34f;
                            bool longX = (k + (sx * sz > 0 ? 0 : 1)) % 2 == 0;
                            float lx = longX ? 0.55f : 0.34f, lz = longX ? 0.34f : 0.55f;
                            var col = dm.Vary(qc, 0.05f);
                            dm.Box(new Vector3(sx * (hx - lx * 0.5f + 0.03f), y, sz * (hz + 0.03f)), new Vector3(lx, 0.29f, 0.08f), Quaternion.identity, col);
                            dm.Box(new Vector3(sx * (hx + 0.03f), y, sz * (hz - lz * 0.5f + 0.03f)), new Vector3(0.08f, 0.29f, lz), Quaternion.identity, col);
                        }
            }

            for (int s = 0; s < 4; s++)
            {
                if (lean[s]) continue;
                var nrm = SideNormal(s);
                var tan = SideTangent(s);
                float face = (s < 2 ? hx : hz);
                float len = (s < 2 ? fl : fw);
                var q = Quaternion.LookRotation(nrm);

                if (mat == 0 && f >= 1)
                {
                    // Khung gỗ: dầm trên / dưới, cột, thanh chéo
                    dm.Box(nrm * (face + 0.03f) + Vector3.up * (y0 + 0.08f), s < 2 ? new Vector3(0.08f, 0.16f, len + 0.08f) : new Vector3(len + 0.08f, 0.16f, 0.08f), Quaternion.identity, dm.Vary(Timber, 0.05f));
                    dm.Box(nrm * (face + 0.03f) + Vector3.up * (y0 + fh - 0.08f), s < 2 ? new Vector3(0.08f, 0.16f, len + 0.08f) : new Vector3(len + 0.08f, 0.16f, 0.08f), Quaternion.identity, dm.Vary(Timber, 0.05f));
                    int posts = Mathf.Max(1, Mathf.RoundToInt(len / 1.1f));
                    for (int k = 0; k <= posts; k++)
                    {
                        float a = -len * 0.5f + len * k / posts;
                        a = Mathf.Clamp(a, -len * 0.5f + 0.06f, len * 0.5f - 0.06f);
                        if (k > 0 && k < posts && NearOpening(s, a, 0.1f)) continue;
                        dm.Box(nrm * (face + 0.03f) + tan * a + Vector3.up * (y0 + fh * 0.5f), new Vector3(0.14f, fh - 0.2f, 0.07f), q, dm.Vary(Timber, 0.05f));
                    }
                    // thanh chéo ở 2 ô sát góc
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float a0 = side * (len * 0.5f - 0.1f), a1 = side * (len * 0.5f - 0.95f);
                        if (NearOpening(s, (a0 + a1) * 0.5f, 0.15f)) continue;
                        var p0 = tan * a0 + Vector3.up * (y0 + 0.2f);
                        var p1 = tan * a1 + Vector3.up * (y0 + fh * 0.6f);
                        var dir = (p1 - p0);
                        var rot = Quaternion.LookRotation(dir.normalized, nrm);
                        dm.Box(nrm * (face + 0.03f) + (p0 + p1) * 0.5f, new Vector3(0.12f, 0.06f, dir.magnitude), rot, dm.Vary(Timber, 0.05f));
                    }
                }
                else if (mat == 1)
                {
                    // Ván ốp đứng
                    for (float a = -len * 0.5f + 0.2f; a < len * 0.5f - 0.1f; a += 0.34f)
                    {
                        if (NearOpening(s, a, 0.05f)) continue;
                        dm.Box(nrm * (face + 0.015f) + tan * a + Vector3.up * (y0 + fh * 0.5f), new Vector3(0.05f, fh - 0.04f, 0.03f), q, dm.Vary(Timber * 1.15f, 0.06f));
                    }
                }
                else if (mat == 2 || mat == 3)
                {
                    // Đá / gạch lộ ra rải rác trên mặt tường
                    int count = Mathf.RoundToInt(len * (mat == 2 ? 2.2f : 1.4f));
                    var baseCol = MatColors[mat];
                    for (int k = 0; k < count; k++)
                    {
                        float a = dm.Range(-len * 0.5f + 0.4f, len * 0.5f - 0.4f);
                        float y = dm.Range(0.25f, fh - 0.25f);
                        bool inWin = NearOpening(s, a, 0.2f) && y > 0.8f && y < 2.2f;
                        if (inWin || (f == 0 && s == DoorSide && Mathf.Abs(a) < 0.9f)) continue;
                        var size = mat == 2 ? new Vector3(dm.Range(0.28f, 0.5f), dm.Range(0.18f, 0.3f), 0.05f) : new Vector3(0.24f, 0.08f, 0.04f);
                        var col = dm.Vary(baseCol * (dm.Rand() < 0.5f ? 0.9f : 1.07f), 0.05f);
                        int cluster = mat == 3 ? 3 : 1;
                        for (int c = 0; c < cluster; c++)
                            dm.Box(nrm * (face + 0.015f) + tan * (a + (c % 2) * 0.13f) + Vector3.up * (y0 + y + c * 0.1f), size,
                                   q * Quaternion.Euler(0, 0, dm.Range(-3f, 3f)), col, skipBack: true);
                    }
                }
            }
        }

        // ---------- Cửa chính: khung gỗ + mái hiên nhỏ ----------
        void BuildDoorDressing(Transform t, int s, List<GameObject> cutList)
        {
            var nrm = SideNormal(s);
            float face = SideHalf(s);
            Vector3 Sc(float w, float h, float d) => s < 2 ? new Vector3(d, h, w) : new Vector3(w, h, d);
            var tan = SideTangent(s);
            void Add(GameObject g) => cutList?.Add(g);
            Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.05f) + tan * 0.62f + Vector3.up * 1.08f, Sc(0.14f, 2.16f, 0.1f), Timber, name: "DoorFrame"));
            Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.05f) - tan * 0.62f + Vector3.up * 1.08f, Sc(0.14f, 2.16f, 0.1f), Timber, name: "DoorFrame"));
            Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.05f) + Vector3.up * 2.2f, Sc(1.4f, 0.16f, 0.12f), Timber, name: "DoorFrame"));

            // mái hiên: nóc chạy ra ngoài theo pháp tuyến tường
            var canopy = new GameObject("DoorCanopy");
            canopy.transform.SetParent(t, false);
            canopy.transform.localPosition = nrm * (face + 0.42f) + Vector3.up * 2.42f;
            canopy.transform.localRotation = Quaternion.LookRotation(nrm);
            var mesh = RoofMeshOf(0.7f, 0.42f, 0.75f, false, out _);
            var rg = new GameObject("RoofMesh");
            rg.transform.SetParent(canopy.transform, false);
            rg.AddComponent<MeshFilter>().sharedMesh = mesh;
            rg.AddComponent<MeshRenderer>().sharedMaterial = Palette.Mat(RoofUnder);
            BuildRoofTiles(canopy.transform, 0.7f, 0.42f, 0.75f, false, RoofColor, P.seed * 3 + 11, true);
            Add(canopy);
            for (int k = -1; k <= 1; k += 2)   // 2 thanh chống
            {
                var br = Prim(PrimitiveType.Cube, t, nrm * (face + 0.22f) + tan * (k * 0.62f) + Vector3.up * 2.22f, Sc(0.08f, 0.08f, 0.5f), Timber,
                              localRot: Quaternion.AngleAxis(-35f * (s % 2 == 0 ? 1f : -1f), tan), name: "Bracket");
                Add(br);
            }
        }

        // ---------- Mái ngói ----------
        /// <summary>Lợp ngói lên mái hai dốc / bốn dốc (khung mái: nóc dọc trục z, mặt mái y = (hx − |x|)·tan).</summary>
        static void BuildRoofTiles(Transform rt, float hx, float hz, float tp, bool hip, Color roofCol, int seed, bool barge)
        {
            var dm = new DetailMesh(seed);
            float o = GameConfig.RoofOverhang;
            float ex = hx + o, ez = hz + o, e = o * tp, top = hx * tp;
            var a0 = new Vector3(-ex, -e, -ez); var a1 = new Vector3(-ex, -e, ez);
            var b0 = new Vector3(ex, -e, -ez); var b1 = new Vector3(ex, -e, ez);
            var ridgeCol = roofCol * 0.82f; ridgeCol.a = 1f;
            if (!hip)
            {
                var r0 = new Vector3(0, top, -ez); var r1 = new Vector3(0, top, ez);
                Slope(dm, b0, b1, r0, r1, roofCol);
                Slope(dm, a1, a0, r1, r0, roofCol);
                RidgeCap(dm, r0, r1, ridgeCol);
                if (barge)
                {
                    Barge(dm, b0, r0, -1f); Barge(dm, a0, r0, -1f);
                    Barge(dm, b1, r1, 1f); Barge(dm, a1, r1, 1f);
                }
            }
            else
            {
                float r1z = Mathf.Max(0f, ez - ex);
                var r0 = new Vector3(0, top, -r1z); var r1 = new Vector3(0, top, r1z);
                Slope(dm, b0, b1, r0, r1, roofCol);
                Slope(dm, a1, a0, r1, r0, roofCol);
                Slope(dm, b1, a1, r1, r1, roofCol);
                Slope(dm, a0, b0, r0, r0, roofCol);
                if (r1z > 0.05f) RidgeCap(dm, r0, r1, ridgeCol);
                RidgeCap(dm, a1, r1, ridgeCol); RidgeCap(dm, b1, r1, ridgeCol);
                RidgeCap(dm, a0, r0, ridgeCol); RidgeCap(dm, b0, r0, ridgeCol);
            }
            // ván diềm dưới mép mái
            Fascia(dm, a0, a1); Fascia(dm, b0, b1);
            dm.Build("RoofTiles", rt);
        }

        /// <summary>Lợp 1 mặt mái: mép dưới eL→eR, mép trên rL→rR (rL = rR cho mặt tam giác).</summary>
        static void Slope(DetailMesh dm, Vector3 eL, Vector3 eR, Vector3 rL, Vector3 rR, Color col)
        {
            const float rowH = 0.32f, tileW = 0.3f;
            var up = (rL + rR) * 0.5f - (eL + eR) * 0.5f;
            float slopeLen = up.magnitude;
            if (slopeLen < 0.1f) return;
            var sd = up / slopeLen;
            var row0 = (eR - eL).normalized;
            var n = Vector3.Cross(sd, row0).normalized;
            if (n.y < 0) n = -n;
            var rot = Quaternion.LookRotation(sd, n) * Quaternion.Euler(6f, 0, 0);   // mép dưới viên ngói hơi vểnh
            int rows = Mathf.Max(1, Mathf.CeilToInt(slopeLen / rowH));
            for (int r = 0; r < rows; r++)
            {
                float t = (r + 0.5f) / rows;
                var L = Vector3.Lerp(eL, rL, t);
                var R = Vector3.Lerp(eR, rR, t);
                float width = (R - L).magnitude;
                if (width < tileW * 0.6f) continue;
                var rd = (R - L) / width;
                int count = Mathf.Max(1, Mathf.RoundToInt(width / tileW));
                float tw = width / count;
                bool odd = r % 2 == 1;
                for (int k = 0; k < count; k++)
                {
                    float u = (k + 0.5f) * tw + (odd ? tw * 0.5f : 0f);
                    float wTile = tw;
                    if (u + tw * 0.5f > width) { wTile = width - (u - tw * 0.5f); u = width - wTile * 0.5f; }   // viên cuối hàng so le: cắt ngắn
                    if (wTile < 0.05f) continue;
                    var p = L + rd * u + n * (0.045f + dm.Range(-0.008f, 0.008f));
                    var jr = rot * Quaternion.Euler(dm.Range(-1.5f, 1.5f), dm.Range(-2f, 2f), dm.Range(-1.5f, 1.5f));
                    dm.Box(p, new Vector3(wTile * 0.93f, 0.045f, rowH * 1.2f), jr, dm.Vary(col, 0.07f), skipBottom: true, skipBack: true);
                }
                if (odd)
                {
                    float wTile = tw * 0.5f;   // nửa viên đầu hàng
                    var p = L + rd * (wTile * 0.5f) + n * 0.045f;
                    dm.Box(p, new Vector3(wTile * 0.9f, 0.045f, rowH * 1.2f), rot, dm.Vary(col, 0.07f), skipBottom: true, skipBack: true);
                }
            }
        }

        static void RidgeCap(DetailMesh dm, Vector3 a, Vector3 b, Color col)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.1f) return;
            var dir = d / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0, 0, 45f);
            int count = Mathf.Max(1, Mathf.RoundToInt(len / 0.3f));
            for (int i = 0; i < count; i++)
            {
                var p = a + dir * ((i + 0.5f) * len / count) + Vector3.up * 0.06f;
                dm.Box(p, new Vector3(0.2f, 0.2f, len / count * 1.05f), rot, dm.Vary(col, 0.05f), skipBottom: true);
            }
        }

        static void Barge(DetailMesh dm, Vector3 eave, Vector3 ridge, float side)
        {
            var d = ridge - eave;
            float len = d.magnitude;
            var dir = d / len;
            var up = Vector3.ProjectOnPlane(Vector3.up, dir).normalized;
            var p = (eave + ridge) * 0.5f + Vector3.forward * (side * 0.03f) - up * 0.05f;
            dm.Box(p, new Vector3(0.07f, 0.22f, len + 0.12f), Quaternion.LookRotation(dir, up), dm.Vary(Timber, 0.04f));
        }

        static void Fascia(DetailMesh dm, Vector3 a, Vector3 b)
        {
            var d = b - a;
            float len = d.magnitude;
            var p = (a + b) * 0.5f + Vector3.down * 0.06f;
            dm.Box(p, new Vector3(0.06f, 0.14f, len), Quaternion.LookRotation(d / len, Vector3.up), dm.Vary(Timber, 0.04f), skipBack: false);
        }
    }
}
