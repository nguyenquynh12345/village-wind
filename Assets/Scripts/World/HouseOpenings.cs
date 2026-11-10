using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>1 ô cửa trên tường nhà chính. s = cạnh, f = tầng, a = vị trí dọc cạnh, k = kiểu.</summary>
    [Serializable]
    public class Win
    {
        public int s, f, k;
        public float a;
        public Win Clone() => (Win)MemberwiseClone();
    }

    /// <summary>Cửa sổ / cửa ra vào: tự sinh mỗi 2.2 m, hoặc người chơi tự đặt / dời / xoá (công cụ Cửa sổ).</summary>
    public partial class HouseEntity
    {
        public static readonly string[] OpeningNames = { "Cửa sổ vuông", "Cửa sổ vòm", "Cửa sổ tròn", "Cửa ra vào", "Cửa sổ cao" };
        public const int WinSquare = 0, WinArch = 1, WinRound = 2, WinDoor = 3, WinTall = 4;

        // ---------- Danh sách ô cửa ----------
        List<Win> Openings()
        {
            if (P.winCustom) return P.wins ??= new List<Win>();
            var list = new List<Win>();
            int door = DoorSide;
            for (int f = 0; f < Floors; f++)
            {
                float ins = Inset(f);
                bool balc = HasBalcony(f);
                for (int s = 0; s < 4; s++)
                {
                    float len = SideLen(s) - 2f * ins;
                    if (len < 2.4f) continue;
                    int count = Mathf.FloorToInt((len - 2f) / GameConfig.WindowSpacing) + 1;
                    for (int k = 0; k < count; k++)
                    {
                        float along = (k - (count - 1) * 0.5f) * GameConfig.WindowSpacing;
                        if ((f == 0 || balc) && s == door && Mathf.Abs(along) < 1.3f) continue;
                        list.Add(new Win { s = s, f = f, a = along, k = WinSquare });
                    }
                }
            }
            return list;
        }

        void MaterializeOpenings()
        {
            if (P.winCustom) return;
            P.wins = Openings();
            P.winCustom = true;
        }

        // ---------- Dựng hình ----------
        void BuildOpenings(Transform t, int f, float y0, float ins, bool cut)
        {
            var list = Openings();
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o.f != f) continue;
                int s = Mathf.Abs(o.s) % 4;
                float len = SideLen(s) - 2f * ins;
                if (lean[s] || Mathf.Abs(o.a) > len * 0.5f - 0.45f || WingCovers(s, o.a)) continue;
                BuildOpening(t, s, o.a, y0, ins, Mathf.Abs(o.k) % OpeningNames.Length, i, f, cut ? cutWins[s] : null);
            }
        }

        void BuildOpening(Transform t, int s, float along, float y0, float ins, int kind, int index, int f, List<GameObject> cutList)
        {
            var nrm = SideNormal(s);
            var tan = SideTangent(s);
            float face = SideHalf(s) - ins;
            Vector3 Sc(float w, float h, float d) => s < 2 ? new Vector3(d, h, w) : new Vector3(w, h, d);
            GameObject Add(GameObject g) { cutList?.Add(g); return g; }
            var glassMat = Palette.WindowMat();

            switch (kind)
            {
                case WinDoor:
                {
                    var d = Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.04f) + tan * along + Vector3.up * (y0 + 1.0f), Sc(1.0f, 2.0f, 0.12f),
                                     DoorColors[Mathf.Abs(P.doorColor) % DoorColors.Length], keepCollider: true, name: "Door"));
                    Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.02f) + tan * along + Vector3.up * (y0 + 1.05f), Sc(1.2f, 2.15f, 0.05f), Palette.Cream, name: "WinFrame"));
                    if (f == 0)
                        Prim(PrimitiveType.Cube, t, nrm * (face + 0.45f) + tan * along + Vector3.down * 0.15f, Sc(1.5f, 0.3f, 0.6f), Palette.StoneDark, name: "Step");
                    return;
                }
                case WinRound:
                {
                    var rot = Quaternion.LookRotation(nrm) * Quaternion.Euler(90f, 0, 0);
                    var w = Add(Prim(PrimitiveType.Cylinder, t, nrm * (face + 0.03f) + tan * along + Vector3.up * (y0 + 1.6f),
                                     new Vector3(0.6f, 0.04f, 0.6f), Palette.Ink, localRot: rot, name: "Window"));
                    w.GetComponent<Renderer>().sharedMaterial = glassMat;
                    Add(Prim(PrimitiveType.Cylinder, t, nrm * (face + 0.01f) + tan * along + Vector3.up * (y0 + 1.6f),
                             new Vector3(0.76f, 0.03f, 0.76f), Palette.Cream, localRot: rot, name: "WinFrame"));
                    return;
                }
            }

            float wh = kind == WinTall ? 1.6f : 1.0f, cy = kind == WinTall ? y0 + 1.3f : y0 + 1.5f;
            var win = Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.03f) + tan * along + Vector3.up * cy, Sc(0.7f, wh, 0.08f), Palette.Ink, name: "Window"));
            win.GetComponent<Renderer>().sharedMaterial = glassMat;
            Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.01f) + tan * along + Vector3.up * cy, Sc(0.86f, wh + 0.16f, 0.05f), Palette.Cream, name: "WinFrame"));
            if (kind == WinArch)
            {
                var rot = Quaternion.LookRotation(nrm) * Quaternion.Euler(90f, 0, 0);
                var arch = Add(Prim(PrimitiveType.Cylinder, t, nrm * (face + 0.03f) + tan * along + Vector3.up * (cy + wh * 0.5f),
                                    new Vector3(0.7f, 0.04f, 0.7f), Palette.Ink, localRot: rot, name: "Window"));
                arch.GetComponent<Renderer>().sharedMaterial = glassMat;
                Add(Prim(PrimitiveType.Cylinder, t, nrm * (face + 0.01f) + tan * along + Vector3.up * (cy + wh * 0.5f),
                         new Vector3(0.86f, 0.03f, 0.86f), Palette.Cream, localRot: rot, name: "WinFrame"));
            }
            // song cửa (chữ thập) + cánh cửa chớp (khoảng 1/2 số cửa sổ, cùng màu cửa chính)
            Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.075f) + tan * along + Vector3.up * cy, Sc(0.045f, wh, 0.03f), Palette.Cream, name: "Mullion"));
            Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.075f) + tan * along + Vector3.up * (cy + (kind == WinTall ? 0.25f : 0.05f)), Sc(0.7f, 0.045f, 0.03f), Palette.Cream, name: "Mullion"));
            if (((P.seed >> 3) + index * 7 + f) % 2 == 0)
            {
                var shutter = DoorColors[Mathf.Abs(P.doorColor) % DoorColors.Length];
                for (int k = -1; k <= 1; k += 2)
                {
                    var sh = Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.045f) + tan * (along + k * 0.66f) + Vector3.up * cy, Sc(0.4f, wh + 0.06f, 0.05f), shutter, name: "Shutter"));
                    for (int b = -1; b <= 1; b += 2)   // 2 nẹp ngang trên cánh chớp
                        Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.075f) + tan * (along + k * 0.66f) + Vector3.up * (cy + b * wh * 0.3f), Sc(0.36f, 0.06f, 0.02f), shutter * 0.8f, name: "Shutter"));
                }
            }
            float sillY = cy - wh * 0.5f - 0.04f;
            Add(Prim(PrimitiveType.Cube, t, nrm * (face + 0.06f) + tan * along + Vector3.up * sillY, Sc(0.95f, 0.08f, 0.18f), Palette.Wood, name: "Sill"));

            // Hộp hoa dưới cửa sổ (theo seed, khoảng 1/3 số cửa sổ)
            int fh2 = (P.seed * 31 + s * 7 + index * 13 + f * 17) & 7;
            if (fh2 >= 3 || kind == WinTall) return;
            var boxPos = nrm * (face + 0.2f) + tan * along + Vector3.up * (sillY - 0.1f);
            Add(Prim(PrimitiveType.Cube, t, boxPos, Sc(0.8f, 0.18f, 0.22f), Palette.Wood, name: "FlowerBox"));
            for (int q = -1; q <= 1; q++)
                Add(Prim(PrimitiveType.Sphere, t, boxPos + tan * (q * 0.24f) + Vector3.up * 0.14f, Vector3.one * 0.17f,
                         BoxBlooms[(fh2 + q + 3) % BoxBlooms.Length], name: "Bloom"));
        }

        // ---------- Ống khói · cửa sổ mái ----------
        bool HasChimney => P.chimney == 1 || (P.chimney == 0 && W * L >= GameConfig.ChimneyMinArea);

        /// <summary>Cửa sổ mái trên 2 mái dốc (khung mái: nóc dọc trục z, mặt mái y = (hx − |x|)·tan).</summary>
        void BuildDormers(Transform rt, float hx, float hz, float tp)
        {
            int n = Mathf.Abs(P.dormers) % 3;
            if (n == 0 || hx < 1.6f || hz < 1.4f) return;
            var wallCol = MatColors[Mat(Floors - 1)];
            float chimSide = (P.seed & 1) == 0 ? 1f : -1f;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < n; i++)
                {
                    float zc = n == 1 ? 0f : (i == 0 ? -1f : 1f) * hz * 0.45f;
                    if (HasChimney && side == chimSide && Mathf.Abs(zc - hz * 0.4f) < 1.1f) continue;
                    float xFront = side * (hx * 0.55f + 0.55f);
                    float yFront = Mathf.Max(0.05f, (hx - Mathf.Abs(xFront)) * tp);
                    const float H = 1.0f, Wd = 1.1f, D = 1.6f;
                    var g = new GameObject("Dormer");
                    g.transform.SetParent(rt, false);
                    var gt = g.transform;
                    float xc = xFront - side * D * 0.5f;
                    Prim(PrimitiveType.Cube, gt, new Vector3(xc, yFront + H * 0.5f, zc), new Vector3(D, H, Wd), wallCol, name: "DormerWall");
                    var w = Prim(PrimitiveType.Cube, gt, new Vector3(xFront + side * 0.02f, yFront + 0.5f, zc), new Vector3(0.05f, 0.55f, 0.5f),
                                 Palette.Ink, name: "Window");
                    w.GetComponent<Renderer>().sharedMaterial = Palette.WindowMat();
                    Prim(PrimitiveType.Cube, gt, new Vector3(xFront + side * 0.01f, yFront + 0.5f, zc), new Vector3(0.04f, 0.68f, 0.64f),
                         Palette.Cream, name: "WinFrame");
                    // mái nhỏ: nóc chạy ra ngoài (trục x)
                    var mesh = RoofMeshOf(Wd * 0.5f, D * 0.5f, 1f, false, out _);
                    var rg = new GameObject("RoofMesh");
                    rg.transform.SetParent(gt, false);
                    rg.transform.localPosition = new Vector3(xc, yFront + H, zc);
                    rg.transform.localRotation = Quaternion.Euler(0, 90f, 0);
                    rg.AddComponent<MeshFilter>().sharedMesh = mesh;
                    rg.AddComponent<MeshRenderer>().sharedMaterial = Palette.Mat(RoofColor);
                }
        }

        // ---------- Sửa bằng công cụ Cửa sổ ----------
        /// <summary>Điểm trúng có nằm trên mặt tường nhà chính không → cạnh, tầng, vị trí dọc cạnh.</summary>
        public bool WallHit(RaycastHit h, out int s, out int f, out float along)
        {
            s = f = 0;
            along = 0f;
            if (h.collider && h.collider.GetComponentInParent<PartTag>()) return false;   // cánh nhà / đồ đạc
            if (Mathf.Abs(h.normal.y) > 0.5f) return false;
            var local = Quaternion.Inverse(Rot) * (h.point - new Vector3(P.x, baseY, P.z));
            float fh = GameConfig.FloorHeight;
            if (local.y < 0f || local.y > WallTop) return false;
            f = Mathf.Clamp(Mathf.FloorToInt(local.y / fh), 0, Floors - 1);
            float ins = Inset(f);
            float dx = Mathf.Abs(Mathf.Abs(local.x) - (W * 0.5f - ins)), dz = Mathf.Abs(Mathf.Abs(local.z) - (L * 0.5f - ins));
            if (Mathf.Min(dx, dz) > 0.35f) return false;
            if (dx <= dz) { s = local.x > 0 ? 0 : 1; along = local.z; }
            else { s = local.z > 0 ? 2 : 3; along = local.x; }
            along = Mathf.Round(along / 0.25f) * 0.25f;
            return true;
        }

        /// <summary>Vị trí tâm ô cửa trong thế giới (để vẽ xem trước).</summary>
        public Vector3 OpeningCenter(int s, int f, float along, int kind)
        {
            float y = f * GameConfig.FloorHeight + (kind == WinDoor ? 1.0f : kind == WinTall ? 1.3f : kind == WinRound ? 1.6f : 1.5f);
            return ToWorld(SideNormal(s) * (SideHalf(s) - Inset(f) + 0.06f) + SideTangent(s) * along + Vector3.up * y);
        }

        public Quaternion OpeningRotation(int s) => Rot * Quaternion.LookRotation(SideNormal(s));

        /// <summary>Đặt (kind ≥ 0) hoặc xoá (kind &lt; 0) ô cửa ở vị trí này. Trả về true nếu có thay đổi.</summary>
        public bool EditOpening(int s, int f, float along, int kind, out string reason)
        {
            reason = null;
            float len = SideLen(s) - 2f * Inset(f);
            var list = Openings();
            int hit = -1;
            float best = 0.9f;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o.f != f || Mathf.Abs(o.s) % 4 != s) continue;
                float d = Mathf.Abs(o.a - along);
                if (d < best) { best = d; hit = i; }
            }

            if (kind < 0)
            {
                if (hit < 0) { reason = "Không có ô cửa ở đây"; return false; }
                MaterializeOpenings();
                P.wins.RemoveAt(hit);
                Rebuild();
                return true;
            }

            if (Mathf.Abs(along) > len * 0.5f - 0.5f) { reason = "Quá sát góc nhà"; return false; }
            if (f == 0 && s == DoorSide && Mathf.Abs(along) < 1.1f) { reason = "Trùng cửa chính"; return false; }
            if (WingCovers(s, along)) { reason = "Bị cánh nhà che"; return false; }
            MaterializeOpenings();
            if (hit >= 0)
            {
                var o = P.wins[hit];
                if (o.k == kind && Mathf.Approximately(o.a, along)) { reason = "Đã có"; return false; }
                o.k = kind;
                o.a = along;
            }
            else
            {
                // không chồng lên ô cửa khác cùng tầng
                foreach (var o in P.wins)
                    if (o.f == f && Mathf.Abs(o.s) % 4 == s && Mathf.Abs(o.a - along) < 1.0f) { reason = "Chồng lên ô cửa khác"; return false; }
                P.wins.Add(new Win { s = s, f = f, a = along, k = kind });
            }
            Rebuild();
            return true;
        }
    }
}
