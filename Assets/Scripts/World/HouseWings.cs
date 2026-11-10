using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>Cánh nhà (C.1 mở rộng L/T/U): hình chữ nhật dính vào 1 cạnh của nền chính.</summary>
    [Serializable]
    public class Wing
    {
        public int side;            // cạnh của nền chính: 0 +x · 1 −x · 2 +z · 3 −z
        public float off;           // lệch tâm dọc cạnh (m)
        public float w = 4f;        // bề ngang dọc cạnh
        public float d = 3f;        // độ sâu nhô ra
        public int floors = 1;      // ≤ số tầng nhà chính
    }

    /// <summary>
    /// Phần mở rộng của nhà: cánh nhà L/T/U, tầng hẹp dần, ban công, mái ghép.
    /// </summary>
    public partial class HouseEntity
    {
        public const int WingBase = 500;                // part = WingBase + chỉ số cánh
        public const int MaxWings = 4;
        const float WingMinW = 2.5f, WingMinD = 2f, WingMaxD = 10f;
        const float TaperStep = 0.3f;

        readonly Dictionary<int, GameObject> wingObjs = new();

        // ================= Hình học (tĩnh, dùng được khi chưa có vật) =================
        static float ClampW(HouseParams p) => Mathf.Clamp(p.w, GameConfig.HouseMinSize, GameConfig.HouseMaxWidth);
        static float ClampL(HouseParams p) => Mathf.Clamp(p.l, GameConfig.HouseMinSize, GameConfig.HouseMaxLength);
        static float HalfOf(HouseParams p, int s) => s < 2 ? ClampW(p) * 0.5f : ClampL(p) * 0.5f;
        static float LenOf(HouseParams p, int s) => s < 2 ? ClampL(p) : ClampW(p);

        /// <summary>Kích thước cánh đã kẹp hợp lệ: tâm (cục bộ nhà), cỡ theo trục x / z cục bộ.</summary>
        public static void WingRect(HouseParams p, Wing wg, out Vector3 center, out float sx, out float sz)
        {
            int s = Mathf.Abs(wg.side) % 4;
            float len = LenOf(p, s);
            float w = Mathf.Clamp(wg.w, WingMinW, len);
            float d = Mathf.Clamp(wg.d, WingMinD, WingMaxD);
            float lim = (len - w) * 0.5f;
            float off = Mathf.Clamp(wg.off, -lim, lim);
            center = SideNormal(s) * (HalfOf(p, s) + d * 0.5f) + SideTangent(s) * off;
            sx = s < 2 ? d : w;
            sz = s < 2 ? w : d;
        }

        /// <summary>Các hình chữ nhật chiếm chỗ: nền chính + các cánh.</summary>
        public static List<Obb> BoxesOf(HouseParams p)
        {
            var list = new List<Obb> { Obb.From(p.x, p.z, p.angle, ClampW(p), ClampL(p)) };
            if (p.wings == null) return list;
            var r = Quaternion.Euler(0, p.angle, 0);
            foreach (var wg in p.wings)
            {
                WingRect(p, wg, out var c, out float sx, out float sz);
                var wc = new Vector3(p.x, 0, p.z) + r * c;
                list.Add(Obb.From(wc.x, wc.z, p.angle, sx, sz));
            }
            return list;
        }

        public List<Obb> Boxes => BoxesOf(P);

        public bool ContainsPoint(Vector2 q, float margin)
        {
            foreach (var b in Boxes) if (b.Contains(q, margin)) return true;
            return false;
        }

        /// <summary>Khoảng cách (m) từ điểm tới nền nhà gần nhất (0 nếu nằm trong).</summary>
        public float DistanceTo(Vector2 q)
        {
            float best = float.MaxValue;
            foreach (var b in Boxes)
            {
                float d = Mathf.Max(0f, Mathf.Max(Mathf.Abs(Vector2.Dot(q - b.c, b.ax)) - b.hx,
                                                   Mathf.Abs(Vector2.Dot(q - b.c, b.az)) - b.hz));
                best = Mathf.Min(best, d);
            }
            return best;
        }

        /// <summary>Bán kính bao quanh tâm nhà (kể cả cánh).</summary>
        float Reach
        {
            get
            {
                float r = 0f;
                var c = new Vector2(P.x, P.z);
                foreach (var b in Boxes)
                    for (int i = 0; i < 4; i++) r = Mathf.Max(r, (b.Corner(i) - c).magnitude);
                return r;
            }
        }

        static bool AnyOverlap(List<Obb> a, List<Obb> b, float margin)
        {
            foreach (var x in a)
                foreach (var y in b)
                    if (x.Overlaps(y, margin)) return true;
            return false;
        }

        // ================= Tầng hẹp dần + ban công =================
        bool Tapered(int f) => f > 0 && (P.taper & (1 << f)) != 0;
        bool HasBalcony(int f) => f > 0 && (P.balcony & (1 << f)) != 0;

        /// <summary>Tầng f lùi vào bao nhiêu mỗi phía (cộng dồn các tầng hẹp dần bên dưới).</summary>
        float Inset(int f)
        {
            float s = 0f;
            for (int k = 1; k <= f; k++)
                if (Tapered(k)) s += TaperStep;
            float maxIns = Mathf.Min(W, L) * 0.5f - 1.2f;
            return Mathf.Clamp(s, 0f, Mathf.Max(0f, maxIns));
        }

        void BuildBalcony(Transform t, int f, float y0, float ins, List<GameObject> cutList)
        {
            int s = DoorSide;
            var n = SideNormal(s);
            var tan = SideTangent(s);
            float half = SideHalf(s) - ins, bw = Mathf.Min(2.6f, SideLen(s) - 2f * ins - 0.8f);
            const float bd = 0.9f;
            if (bw < 1.2f) return;
            var up = Vector3.up;

            Prim(PrimitiveType.Cube, t, n * (half + bd * 0.5f) + up * (y0 - 0.06f),
                 s < 2 ? new Vector3(bd, 0.12f, bw) : new Vector3(bw, 0.12f, bd), Palette.Wood, name: "Balcony");
            Prim(PrimitiveType.Cube, t, n * (half + bd - 0.03f) + up * (y0 + 0.9f),
                 s < 2 ? new Vector3(0.05f, 0.05f, bw) : new Vector3(bw, 0.05f, 0.05f), Palette.Wood, name: "Balcony");
            for (float a = -bw * 0.5f; a <= bw * 0.5f + 1e-3f; a += bw / Mathf.Max(1, Mathf.Round(bw / 0.4f)))
                Prim(PrimitiveType.Cube, t, n * (half + bd - 0.03f) + tan * a + up * (y0 + 0.45f),
                     new Vector3(0.04f, 0.9f, 0.04f), Palette.Wood, name: "Balcony");
            for (int k = -1; k <= 1; k += 2)
                Prim(PrimitiveType.Cube, t, n * (half + bd * 0.5f) + tan * (k * bw * 0.5f) + up * (y0 + 0.9f),
                     s < 2 ? new Vector3(bd, 0.05f, 0.05f) : new Vector3(0.05f, 0.05f, bd), Palette.Wood, name: "Balcony");

            // cửa kính ra ban công
            var door = Prim(PrimitiveType.Cube, t, n * (half + 0.03f) + up * (y0 + 1.0f),
                            s < 2 ? new Vector3(0.08f, 2.0f, 0.9f) : new Vector3(0.9f, 2.0f, 0.08f), Palette.Ink, name: "Window");
            door.GetComponent<Renderer>().sharedMaterial = Palette.WindowMat();
            cutList?.Add(door);
        }

        // ================= Mái (dùng chung cho nhà chính và cánh) =================
        /// <summary>Lưới mái hai dốc / bốn dốc, nóc chạy dọc trục z. hx, hz = nửa kích thước tường.</summary>
        static Mesh RoofMeshOf(float hx, float hz, float tp, bool hip, out float top, bool gables = true)
        {
            var mb = new MeshBuilder();
            float o = GameConfig.RoofOverhang;
            float ex = hx + o, ez = hz + o, H = ex * tp, e = o * tp;
            top = H - e;
            var a0 = new Vector3(-ex, -e, -ez); var a1 = new Vector3(-ex, -e, ez);
            var b0 = new Vector3(ex, -e, -ez); var b1 = new Vector3(ex, -e, ez);
            if (!hip)
            {
                var r0 = new Vector3(0, top, -ez); var r1 = new Vector3(0, top, ez);
                mb.Quad(a0, a1, r1, r0, new Vector3(-1, 1, 0)); mb.Quad(a0, a1, r1, r0, Vector3.down);
                mb.Quad(b0, b1, r1, r0, new Vector3(1, 1, 0)); mb.Quad(b0, b1, r1, r0, Vector3.down);
                if (gables)
                {
                    mb.Tri(new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(0, top, hz), Vector3.forward);
                    mb.Tri(new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(0, top, -hz), Vector3.back);
                }
            }
            else
            {
                float r1z = Mathf.Max(0f, ez - ex);
                var r0 = new Vector3(0, top, -r1z); var r1 = new Vector3(0, top, r1z);
                mb.Quad(a0, a1, r1, r0, new Vector3(-1, 1, 0)); mb.Quad(a0, a1, r1, r0, Vector3.down);
                mb.Quad(b0, b1, r1, r0, new Vector3(1, 1, 0)); mb.Quad(b0, b1, r1, r0, Vector3.down);
                mb.Tri(a1, b1, r1, new Vector3(0, 1, 1)); mb.Tri(a1, b1, r1, Vector3.down);
                mb.Tri(a0, b0, r0, new Vector3(0, 1, -1)); mb.Tri(a0, b0, r0, Vector3.down);
            }
            return mb.ToMesh("Roof");
        }

        /// <summary>2 tam giác đầu hồi (mái hai dốc) — cùng màu + hoa văn với tường tầng trên cùng.</summary>
        void BuildGables(Transform rt, float hx, float hz, float tp, int floor)
        {
            float top = hx * tp;
            var mb = new MeshBuilder();
            mb.Tri(new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(0, top, hz), Vector3.forward);
            mb.Tri(new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(0, top, -hz), Vector3.back);
            var g = new GameObject("Gable");
            g.transform.SetParent(rt, false);
            g.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Gable");
            g.AddComponent<MeshRenderer>().sharedMaterial = Palette.Mat(MatColors[Mat(floor)], WallPattern(floor));
        }

        // ================= Dựng cánh nhà =================
        void BuildWings(Transform t, int fv, float plinthBottom)
        {
            wingObjs.Clear();
            if (P.wings == null || P.wings.Count == 0) return;
            float fh = GameConfig.FloorHeight;
            bool view = fv >= 0;
            float tp = Mathf.Tan(Mathf.Clamp(P.pitch, GameConfig.PitchMin, GameConfig.PitchMax) * Mathf.Deg2Rad);
            int roofType = RoofType;

            for (int i = 0; i < P.wings.Count; i++)
            {
                var wg = P.wings[i];
                int s = Mathf.Abs(wg.side) % 4;
                WingRect(P, wg, out var c, out float sx, out float sz);
                int wf = Mathf.Clamp(wg.floors, 1, Floors);

                var g = new GameObject("Wing" + i);
                g.transform.SetParent(t, false);
                g.transform.localPosition = c;
                g.AddComponent<PartTag>().part = WingBase + i;
                wingObjs[WingBase + i] = g;
                var gt = g.transform;

                Prim(PrimitiveType.Cube, gt, new Vector3(0, plinthBottom * 0.5f, 0),
                     new Vector3(sx + 0.3f, -plinthBottom, sz + 0.3f), Palette.StoneDark, keepCollider: true, name: "Plinth");

                for (int f = 0; f < wf; f++)
                {
                    if (view && f > fv) break;
                    float y0 = f * fh;
                    if (view && f == fv)
                    {
                        // tầng đang xem: sàn + tường thấp 1 m ở 3 mặt ngoài
                        Prim(PrimitiveType.Cube, gt, new Vector3(0, y0 + 0.03f, 0), new Vector3(sx, 0.06f, sz), FloorBoard, keepCollider: true, name: "IFloor");
                        for (int q = 0; q < 4; q++)
                        {
                            if (q == (s ^ 1)) continue;
                            float half = q < 2 ? sx * 0.5f : sz * 0.5f;
                            var sc = q < 2 ? new Vector3(InteriorWall, 1f, sz) : new Vector3(sx, 1f, InteriorWall);
                            Prim(PrimitiveType.Cube, gt, SideNormal(q) * (half - InteriorWall * 0.5f) + Vector3.up * (y0 + 0.5f),
                                 sc, MatColors[Mat(f)], keepCollider: true, name: "Floor" + f).WithPattern(MatColors[Mat(f)], WallPattern(f));
                        }
                        continue;
                    }
                    Prim(PrimitiveType.Cube, gt, new Vector3(0, y0 + fh * 0.5f, 0), new Vector3(sx, fh, sz),
                         MatColors[Mat(f)], keepCollider: true, name: "Floor" + f).WithPattern(MatColors[Mat(f)], WallPattern(f));
                    if (f > 0)
                        Prim(PrimitiveType.Cube, gt, new Vector3(0, y0, 0), new Vector3(sx + 0.14f, 0.18f, sz + 0.14f), Palette.Wood, name: "Trim");
                    WingWindows(gt, s, sx, sz, y0);
                }

                if (view) continue;

                // Mái cánh: nóc chạy theo hướng nhô ra, đâm vào mái chính để ghép khe mái
                var roof = new GameObject("Roof");
                roof.transform.SetParent(gt, false);
                float wy = wf * fh;
                if (roofType == RoofFlat)
                {
                    roof.transform.localPosition = new Vector3(0, wy, 0);
                    Prim(PrimitiveType.Cube, roof.transform, new Vector3(0, 0.125f, 0), new Vector3(sx + 0.4f, 0.25f, sz + 0.4f),
                         Palette.StoneDark, true, name: "Slab");
                    continue;
                }
                var n = SideNormal(s);
                float d = s < 2 ? sx : sz, wAcross = s < 2 ? sz : sx;
                float eIn = SideHalf(s) * 0.5f;
                roof.transform.localPosition = new Vector3(0, wy, 0) - n * (eIn * 0.5f);
                roof.transform.localRotation = Quaternion.LookRotation(n);
                var mesh = RoofMeshOf(wAcross * 0.5f, (d + eIn) * 0.5f, tp, roofType != RoofGable, out _, false);
                if (roofType == RoofGable) BuildGables(roof.transform, wAcross * 0.5f, (d + eIn) * 0.5f, tp, wf - 1);
                var rg = new GameObject("RoofMesh");
                rg.transform.SetParent(roof.transform, false);
                rg.AddComponent<MeshFilter>().sharedMesh = mesh;
                rg.AddComponent<MeshRenderer>().sharedMaterial = Palette.Mat(RoofUnder);
                rg.AddComponent<MeshCollider>().sharedMesh = mesh;
                BuildRoofTiles(roof.transform, wAcross * 0.5f, (d + eIn) * 0.5f, tp, roofType != RoofGable, RoofColor, P.seed * 13 + i, true);
            }
        }

        void WingWindows(Transform gt, int attachSide, float sx, float sz, float y0)
        {
            int back = attachSide ^ 1;
            for (int q = 0; q < 4; q++)
            {
                if (q == back) continue;
                float half = q < 2 ? sx * 0.5f : sz * 0.5f, len = q < 2 ? sz : sx;
                if (len < 2.4f) continue;
                int count = Mathf.FloorToInt((len - 2f) / GameConfig.WindowSpacing) + 1;
                var nrm = SideNormal(q);
                var tan = SideTangent(q);
                for (int k = 0; k < count; k++)
                {
                    float along = (k - (count - 1) * 0.5f) * GameConfig.WindowSpacing;
                    var win = Prim(PrimitiveType.Cube, gt, nrm * (half + 0.03f) + tan * along + Vector3.up * (y0 + 1.5f),
                                   q < 2 ? new Vector3(0.08f, 1.0f, 0.7f) : new Vector3(0.7f, 1.0f, 0.08f), Palette.Ink, name: "Window");
                    win.GetComponent<Renderer>().sharedMaterial = Palette.WindowMat();
                    Prim(PrimitiveType.Cube, gt, nrm * (half + 0.01f) + tan * along + Vector3.up * (y0 + 1.5f),
                         q < 2 ? new Vector3(0.05f, 1.16f, 0.86f) : new Vector3(0.86f, 1.16f, 0.05f), Palette.Cream, name: "WinFrame");
                    Prim(PrimitiveType.Cube, gt, nrm * (half + 0.06f) + tan * along + Vector3.up * (y0 + 0.96f),
                         q < 2 ? new Vector3(0.18f, 0.08f, 0.95f) : new Vector3(0.95f, 0.08f, 0.18f), Palette.Wood, name: "Sill");
                }
            }
        }

        /// <summary>Vị trí along (dọc cạnh s của nền chính) bị cánh nhà che → không mở cửa sổ ở đó.</summary>
        bool WingCovers(int s, float along)
        {
            if (P.wings == null) return false;
            foreach (var wg in P.wings)
            {
                if (Mathf.Abs(wg.side) % 4 != s) continue;
                WingRect(P, wg, out var c, out float sx, out float sz);
                float centre = Vector3.Dot(c, SideTangent(s)), half = (s < 2 ? sz : sx) * 0.5f;
                if (Mathf.Abs(along - centre) < half + 0.5f) return true;
            }
            return false;
        }

        // ================= Chọn / sửa cánh =================
        bool IsWingPart(int part) => part >= WingBase && part < FurnBase;
        Wing WingAt(int part)
        {
            int i = part - WingBase;
            return P.wings != null && i >= 0 && i < P.wings.Count ? P.wings[i] : null;
        }

        string WingDescribe(Wing wg)
        {
            WingRect(P, wg, out _, out float sx, out float sz);
            return $"Cánh nhà · {sx:0.#} × {sz:0.#} m · {Mathf.Clamp(wg.floors, 1, Floors)} tầng · Delete: bỏ cánh";
        }

        void WingHandles(List<HandleDef> list, Wing wg)
        {
            int s = Mathf.Abs(wg.side) % 4;
            WingRect(P, wg, out var c, out float sx, out float sz);
            var n = SideNormal(s);
            var tan = SideTangent(s);
            float d = s < 2 ? sx : sz, w = s < 2 ? sz : sx;
            int wf = Mathf.Clamp(wg.floors, 1, Floors);
            list.Add(new HandleDef("wd", HandleKind.Point, ToWorld(c + n * (d * 0.5f + 0.7f) + Vector3.up * 0.3f)));
            list.Add(new HandleDef("wa", HandleKind.Point, ToWorld(c - tan * (w * 0.5f + 0.7f) + Vector3.up * 0.3f)));
            list.Add(new HandleDef("wb", HandleKind.Point, ToWorld(c + tan * (w * 0.5f + 0.7f) + Vector3.up * 0.3f)));
            list.Add(new HandleDef("wfl", HandleKind.Arrow, ToWorld(c + n * (d * 0.5f + 0.9f) + Vector3.up * (wf * GameConfig.FloorHeight + 0.5f))));
        }

        /// <summary>Kéo tay nắm của cánh (dùng tham số lúc bắt đầu kéo để không trôi).</summary>
        bool DragWing(string key, HandleInput input, HouseParams sp)
        {
            int idx = ActivePart - WingBase;
            if (sp.wings == null || idx < 0 || idx >= sp.wings.Count || idx >= P.wings.Count) return false;
            var w0 = sp.wings[idx];
            var wg = P.wings[idx];
            int s = Mathf.Abs(w0.side) % 4;
            if (key == "wfl")
            {
                wg.floors = Mathf.Clamp(w0.floors + Mathf.RoundToInt(input.axisDelta / GameConfig.FloorDragStep), 1, Floors);
                return true;
            }
            if (!input.hasGround) return false;
            var r0 = Quaternion.Euler(0, sp.angle, 0);
            var local = Quaternion.Inverse(r0) * (input.ground - new Vector3(sp.x, 0, sp.z));
            var n = SideNormal(s);
            var tan = SideTangent(s);
            if (key == "wd")
            {
                float proj = Vector3.Dot(local, n) - HalfOf(sp, s) - 0.7f;
                wg.d = Mathf.Clamp(Snap(proj, GameConfig.HouseSnap), WingMinD, WingMaxD);
                return true;
            }
            float len = LenOf(sp, s);
            float a0 = w0.off - w0.w * 0.5f, b0 = w0.off + w0.w * 0.5f;
            float tv = Vector3.Dot(local, tan);
            if (key == "wa")
            {
                float a = Mathf.Clamp(Snap(tv + 0.7f, GameConfig.HouseSnap), -len * 0.5f, b0 - WingMinW);
                wg.w = b0 - a; wg.off = (a + b0) * 0.5f;
                return true;
            }
            if (key == "wb")
            {
                float b = Mathf.Clamp(Snap(tv - 0.7f, GameConfig.HouseSnap), a0 + WingMinW, len * 0.5f);
                wg.w = b - a0; wg.off = (a0 + b) * 0.5f;
                return true;
            }
            return false;
        }

        void SlideWing(Vector3 worldDelta)
        {
            var wg = WingAt(ActivePart);
            if (wg == null) return;
            if (!rawValid || rawPart != ActivePart)
            {
                raw = new Vector2(wg.off, 0f);
                rawValid = true;
                rawPart = ActivePart;
            }
            var ld = Quaternion.Inverse(Rot) * worldDelta;
            raw.x += Vector3.Dot(ld, SideTangent(Mathf.Abs(wg.side) % 4));
            float old = wg.off;
            float len = LenOf(P, Mathf.Abs(wg.side) % 4), w = Mathf.Clamp(wg.w, WingMinW, len);
            wg.off = Mathf.Clamp(Snap(raw.x, GameConfig.HouseSnap), -(len - w) * 0.5f, (len - w) * 0.5f);
            if (!Mathf.Approximately(old, wg.off)) Rebuild();
        }

        /// <summary>
        /// Tạo cánh từ 1 nét kéo (cục bộ nhà): bắt đầu trong nền chính, kết thúc ngoài cạnh.
        /// Trả về null nếu nét kéo không ra khỏi nền.
        /// </summary>
        public static Wing WingFromDrag(HouseParams p, Vector3 startWorld, Vector3 endWorld)
        {
            var r = Quaternion.Inverse(Quaternion.Euler(0, p.angle, 0));
            var ls = r * (startWorld - new Vector3(p.x, 0, p.z));
            var le = r * (endWorld - new Vector3(p.x, 0, p.z));
            float W = ClampW(p), L = ClampL(p);
            float ex = Mathf.Abs(le.x) - W * 0.5f, ez = Mathf.Abs(le.z) - L * 0.5f;
            if (ex < 1f && ez < 1f) return null;
            int s = ex >= ez ? (le.x > 0 ? 0 : 1) : (le.z > 0 ? 2 : 3);
            float depth = s < 2 ? ex : ez;
            var tan = SideTangent(s);
            float a = Vector3.Dot(ls, tan), b = Vector3.Dot(le, tan);
            float len = LenOf(p, s);
            float w = Mathf.Clamp(Snap(Mathf.Max(Mathf.Abs(b - a), 3f), GameConfig.HouseSnap), WingMinW, len);
            float off = Mathf.Clamp(Snap((a + b) * 0.5f, GameConfig.HouseSnap), -(len - w) * 0.5f, (len - w) * 0.5f);
            return new Wing
            {
                side = s, w = w, off = off,
                d = Mathf.Clamp(Snap(depth, GameConfig.HouseSnap), WingMinD, WingMaxD),
                floors = Mathf.Clamp(p.floors, 1, GameConfig.MaxFloors)
            };
        }

        /// <summary>Ẩn / hiện hình (công cụ Nhà ẩn nhà gốc khi đang xem trước cánh mới).</summary>
        public void SetVisualActive(bool on)
        {
            if (visual) visual.SetActive(on);
        }
    }
}
