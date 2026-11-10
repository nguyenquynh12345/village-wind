using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    [Serializable]
    public class HouseParams
    {
        public float x, z, angle;               // tâm nền + góc xoay (độ)
        public float w = 4f, l = 6f;            // rộng (trục x cục bộ), dài (trục z cục bộ)
        public int floors = 1;                  // 1..4
        public List<int> mats = new() { 0, 0, 0, 0 };   // chất liệu từng tầng
        public int roof;                        // 0 hai dốc · 1 bốn dốc · 2 bằng · 3 nón
        public float pitch = GameConfig.PitchDefault;
        public int door;                        // cạnh có cửa: 0 +x · 1 −x · 2 +z · 3 −z
        public int seed;
        public int roofColor;                   // 0 ngói đỏ · 1 ngói xám · 2 rơm · 3 rêu
        public int doorColor;                   // 0 nâu · 1 xanh lá · 2 xanh dương · 3 đỏ
        public bool custom;                     // false = nội thất tự sinh (không lưu) · true = dùng danh sách furn
        public List<Furn> furn = new();
        public List<Wing> wings = new();        // cánh nhà (L/T/U)
        public int taper;                       // bit f: tầng f hẹp hơn tầng dưới 0.3 m mỗi phía
        public int balcony;                     // bit f: tầng f có ban công (phía cửa)
        public bool ridgeSwap;                  // nóc chạy theo trục ngắn
        public bool winCustom;                  // true = dùng danh sách wins (người chơi đã sửa ô cửa)
        public List<Win> wins = new();
        public int dormers;                     // cửa sổ mái mỗi bên: 0 / 1 / 2
        public int chimney;                     // 0 tự động · 1 luôn có · 2 không
    }

    /// <summary>
    /// Nhà: nền chữ nhật + 1–4 tầng + 4 kiểu mái. Mọi thứ sửa được bằng tay nắm.
    /// Cửa sổ, cửa ra vào, ống khói, chân móng trên dốc: tự sinh.
    /// </summary>
    public partial class HouseEntity : Entity<HouseParams>
    {
        public static readonly string[] RoofNames = { "Hai dốc", "Bốn dốc", "Bằng", "Nón" };
        public static readonly string[] MatNames = { "Vữa trắng", "Gỗ", "Đá", "Gạch" };
        static readonly Color[] MatColors =
        {
            Palette.Hex("#F1E6D3"), Palette.Hex("#B98A5E"), Palette.Hex("#CFC6B8"), Palette.Hex("#B7684F")
        };
        public static readonly string[] RoofColorNames = { "Ngói đỏ", "Ngói xám", "Rơm", "Rêu" };
        static readonly Color[] RoofColors =
        {
            Palette.Hex("#B8573F"), Palette.Hex("#7D8590"), Palette.Hex("#C9A552"), Palette.Hex("#6E8B4E")
        };
        static readonly Color[] DoorColors =
        {
            Palette.Hex("#6B4F3E"), Palette.Hex("#5E8A4A"), Palette.Hex("#4F7FA6"), Palette.Hex("#B5483A")
        };

        const int RoofGable = 0, RoofHip = 1, RoofFlat = 2, RoofCone = 3;
        static readonly Color[] BoxBlooms =
        {
            Palette.Hex("#E5536A"), Palette.Hex("#F6D55C"), Palette.Hex("#FFFFFF"), Palette.Hex("#E59AB0"), Palette.Hex("#B9A3E3")
        };

        float baseY, roofTop;
        readonly bool[] lean = new bool[4];

        /// <summary>Có cạnh nào sát tường thành không (tổ hợp E6).</summary>
        public bool LeansOnWall { get; private set; }
        public float BaseY => baseY;
        public float RoofTopLocal => WallTop + roofTop;
        public int DoorSide => Mathf.Abs(P.door) % 4;
        public Vector3 LocalToWorld(Vector3 local) => ToWorld(local);
        Color RoofColor => RoofColors[Mathf.Abs(P.roofColor) % RoofColors.Length];
        Color RoofUnder { get { var c = RoofColor * 0.62f; c.a = 1f; return c; } }

        /// <summary>Điểm trước cửa ra vào (trên mặt đất) — đường đi hút vào đây.</summary>
        public Vector3 DoorPoint
        {
            get
            {
                var p = ToWorld(SideNormal(DoorSide) * (SideHalf(DoorSide) + 1.2f));
                p.y = HeightField.I.HeightAt(p.x, p.z);
                return p;
            }
        }
        HouseParams startP;
        bool rotInit;
        float rotRef;

        public override string TypeName => "House";

        public float W => Mathf.Clamp(P.w, GameConfig.HouseMinSize, GameConfig.HouseMaxWidth);
        public float L => Mathf.Clamp(P.l, GameConfig.HouseMinSize, GameConfig.HouseMaxLength);
        public int Floors => Mathf.Clamp(P.floors, 1, GameConfig.MaxFloors);
        public float WallTop => Floors * GameConfig.FloorHeight;
        Quaternion Rot => Quaternion.Euler(0, P.angle, 0);
        int RoofType => P.roof == RoofCone && !ConeAllowed(W, L) ? RoofHip : Mathf.Abs(P.roof) % 4;

        public Obb Footprint => Obb.From(P.x, P.z, P.angle, W, L);
        public override Matrix4x4 Frame => Matrix4x4.TRS(new Vector3(P.x, baseY, P.z), Rot, Vector3.one);

        public override string Describe
        {
            get
            {
                var wgd = IsWingPart(ActivePart) ? WingAt(ActivePart) : null;
                if (wgd != null) return WingDescribe(wgd);
                var fu = ActivePart >= FurnBase ? FurnAt(ActivePart) : null;
                if (fu != null)
                    return $"{Furniture.Name(fu.t)} · kiểu {Mathf.Abs(fu.v) % Furniture.Variants + 1}/{Furniture.Variants} · tầng {fu.f + 1}";
                return $"Nhà · {W:0.#} × {L:0.#} m" + (P.wings != null && P.wings.Count > 0 ? $" + {P.wings.Count} cánh" : "") +
                       $" · {Floors} tầng · mái {RoofNames[RoofType]}" +
                       (RoofType != RoofFlat ? $" {P.pitch:0}°" : "") +
                       (Interior.IsViewing(Id) ? $" · đang xem tầng {viewFloor + 1}" : "");
            }
        }

        static bool ConeAllowed(float w, float l) => w <= GameConfig.ConeRoofMaxSize && l <= GameConfig.ConeRoofMaxSize;

        int Mat(int f)
        {
            while (P.mats.Count < GameConfig.MaxFloors) P.mats.Add(0);
            return Mathf.Abs(P.mats[f]) % MatColors.Length;
        }

        // ---------- Mặt đất dưới nền ----------
        public static void GroundRange(HouseParams p, out float min, out float max)
        {
            var hf = HeightField.I;
            min = float.MaxValue; max = float.MinValue;
            foreach (var b in BoxesOf(p))
            {
                int nx = Mathf.CeilToInt(b.hx * 2f) + 1, nz = Mathf.CeilToInt(b.hz * 2f) + 1;
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        var q = b.c + b.ax * (b.hx * (2f * i / (nx - 1) - 1f)) + b.az * (b.hz * (2f * j / (nz - 1) - 1f));
                        float g = hf.HeightAt(q.x, q.y);
                        min = Mathf.Min(min, g);
                        max = Mathf.Max(max, g);
                    }
            }
        }

        /// <summary>Kiểm tra có đặt được nhà không (lúc tạo mới). ignoreId = nhà đang sửa.</summary>
        public static bool CanPlace(HouseParams p, int ignoreId, out string reason)
        {
            var boxes = BoxesOf(p);
            float r2 = GameConfig.BuildRadius * GameConfig.BuildRadius;
            foreach (var bx in boxes)
                for (int i = 0; i < 4; i++)
                    if (bx.Corner(i).sqrMagnitude > r2) { reason = "Ngoài vùng xây"; return false; }

            GroundRange(p, out var gmin, out var gmax);
            if (gmax - gmin > GameConfig.HouseMaxSlope) { reason = "Đất quá dốc"; return false; }

            var gr = WallGraph.I;
            if (gr)
            {
                foreach (var (pos, radius) in gr.Towers())
                    foreach (var bx in boxes)
                        if (bx.Contains(pos, radius)) { reason = "Chồng lên tháp"; return false; }
                foreach (var q in gr.WallSamples(1f))
                    foreach (var bx in boxes)
                        if (bx.Contains(q, GameConfig.WallThickness * 0.5f)) { reason = "Chồng lên tường"; return false; }
            }

            foreach (var h in EntityManager.OfType<HouseEntity>())
                if (h.Id != ignoreId && AnyOverlap(boxes, h.Boxes, 0.1f)) { reason = "Chồng lên nhà khác"; return false; }

            foreach (var pond in EntityManager.OfType<PondEntity>())
            {
                if (pond.Mask == null || !pond.WorldRect.Overlaps(Rect.MinMaxRect(
                        p.x - 20f, p.z - 20f, p.x + 20f, p.z + 20f))) continue;
                foreach (var box in boxes)
                    for (int i = 0; i <= 8; i++)
                        for (int j = 0; j <= 8; j++)
                        {
                            var q = box.c + box.ax * (box.hx * (i / 4f - 1f)) + box.az * (box.hz * (j / 4f - 1f));
                            if (pond.Mask.ContainsWorld(q.x, q.y)) { reason = "Chồng lên ao"; return false; }
                        }
            }

            reason = null;
            return true;
        }

        /// <summary>Cạnh dài nào quay về phía camera (để đặt cửa).</summary>
        public static int SideFacing(HouseParams p, Vector3 dirWorld, bool longSidesOnly)
        {
            var local = Quaternion.Inverse(Quaternion.Euler(0, p.angle, 0)) * dirWorld;
            bool xLong = p.l >= p.w;   // trục dài là z → cạnh dài là ±x
            if (longSidesOnly) return xLong ? (local.x >= 0 ? 0 : 1) : (local.z >= 0 ? 2 : 3);
            if (Mathf.Abs(local.x) >= Mathf.Abs(local.z)) return local.x >= 0 ? 0 : 1;
            return local.z >= 0 ? 2 : 3;
        }

        // ---------- Sinh hình ----------
        static Vector3 SideNormal(int s) => s switch
        {
            0 => Vector3.right, 1 => Vector3.left, 2 => Vector3.forward, _ => Vector3.back
        };

        float SideHalf(int s) => s < 2 ? W * 0.5f : L * 0.5f;      // khoảng từ tâm tới cạnh
        float SideLen(int s) => s < 2 ? L : W;                     // chiều dài cạnh
        static Vector3 SideTangent(int s) => s < 2 ? Vector3.forward : Vector3.right;

        public override void Rebuild()
        {
            float w = W, l = L, fh = GameConfig.FloorHeight;
            int F = Floors;
            GroundRange(P, out var gmin, out var gmax);
            baseY = gmax + GameConfig.FoundationHeight;

            var v = NewVisual();
            v.transform.SetPositionAndRotation(new Vector3(P.x, baseY, P.z), Rot);
            var t = v.transform;

            // Chân móng: từ dưới mặt đất thấp nhất lên tới sàn
            float plinthBottom = gmin - 0.5f - baseY;
            Prim(PrimitiveType.Cube, t, new Vector3(0, plinthBottom * 0.5f, 0),
                 new Vector3(w + 0.3f, -plinthBottom, l + 0.3f), Palette.StoneDark, keepCollider: true, name: "Plinth");
            detail = new DetailMesh(P.seed);
            BuildFoundationDetail(w, l, plinthBottom);

            int door = Mathf.Abs(P.door) % 4;

            // E6: cạnh nào nằm sát tường thành → không mở cửa sổ ở cạnh đó
            LeansOnWall = false;
            var gr = WallGraph.I;
            for (int s = 0; s < 4; s++)
            {
                lean[s] = false;
                if (!gr) continue;
                for (int k = -1; k <= 1 && !lean[s]; k++)
                {
                    var probe = ToWorldFlat(SideNormal(s) * (SideHalf(s) + 0.5f) + SideTangent(s) * (SideLen(s) * 0.35f * k));
                    if (gr.NearestSeg(new Vector2(probe.x, probe.z), 0.9f, out _, out _)) lean[s] = true;
                }
                LeansOnWall |= lean[s];
            }

            // Xem trong nhà: tầng đang xem bị "cắt" (sàn + 4 vách), các tầng trên và mái bị bỏ
            bool view = Id != 0 && Interior.IsViewing(Id);
            if (view) Interior.ClampFloor(F);
            int fv = view ? Mathf.Clamp(Interior.Floor, 0, F - 1) : -1;
            viewFloor = Mathf.Max(fv, 0);
            for (int s = 0; s < 4; s++) { cutWalls[s] = null; cutWins[s].Clear(); }
            roomLight = null;
            furnObjs.Clear();

            for (int f = 0; f < F; f++)
            {
                if (view && f > fv) break;
                float y0 = f * fh;
                bool cut = f == fv;
                float ins = Inset(f), fw = w - 2f * ins, fl = l - 2f * ins;
                bool balc = HasBalcony(f);
                if (cut)
                {
                    cutY0 = y0;
                    Prim(PrimitiveType.Cube, t, new Vector3(0, y0 + 0.03f, 0), new Vector3(fw - 0.02f, 0.06f, fl - 0.02f),
                         FloorBoard, keepCollider: true, name: "IFloor");
                    for (int s = 0; s < 4; s++)
                    {
                        var n = SideNormal(s);
                        var scale = s < 2 ? new Vector3(InteriorWall, fh, fl) : new Vector3(fw, fh, InteriorWall);
                        cutWalls[s] = Prim(PrimitiveType.Cube, t, n * (SideHalf(s) - ins - InteriorWall * 0.5f) + Vector3.up * (y0 + fh * 0.5f),
                                           scale, MatColors[Mat(f)], keepCollider: true, name: "Floor" + f).WithPattern(MatColors[Mat(f)], WallPattern(f));
                    }
                }
                else
                    Prim(PrimitiveType.Cube, t, new Vector3(0, y0 + fh * 0.5f, 0), new Vector3(fw, fh, fl),
                         MatColors[Mat(f)], keepCollider: true, name: "Floor" + f).WithPattern(MatColors[Mat(f)], WallPattern(f));
                if (f > 0)
                    Prim(PrimitiveType.Cube, t, new Vector3(0, y0, 0), new Vector3(fw + 0.14f, 0.18f, fl + 0.14f), Palette.Wood, name: "Trim");
                if (balc) BuildBalcony(t, f, y0, ins, cut ? cutWins[door] : null);

                // Cửa sổ / cửa phụ (tự sinh hoặc người chơi tự đặt)
                BuildOpenings(t, f, y0, ins, cut);
                if (!cut) BuildWallDetail(f, y0, fw, fl);
            }

            // Cửa ra vào + bậc thềm
            {
                var nrm = SideNormal(door);
                var doorGo = Prim(PrimitiveType.Cube, t, nrm * (SideHalf(door) + 0.04f) + Vector3.up * 1.0f,
                     door < 2 ? new Vector3(0.12f, 2.0f, 1.1f) : new Vector3(1.1f, 2.0f, 0.12f),
                     DoorColors[Mathf.Abs(P.doorColor) % DoorColors.Length], keepCollider: true, name: "Door");
                if (fv == 0) cutWins[door].Add(doorGo);
                Prim(PrimitiveType.Cube, t, nrm * (SideHalf(door) + 0.45f) + Vector3.down * 0.15f,
                     door < 2 ? new Vector3(0.6f, 0.3f, 1.6f) : new Vector3(1.6f, 0.3f, 0.6f), Palette.StoneDark, name: "Step");
                BuildDoorDressing(t, door, fv == 0 ? cutWins[door] : null);
            }

            BuildWings(t, fv, plinthBottom);

            detail?.Build("Detail", t);
            detail = null;

            if (view) BuildInterior(t, fv);
            else
            {
                float ti = Inset(F - 1);
                BuildRoof(t, w - 2f * ti, l - 2f * ti);
            }
            ApplyTint();
            ApplyFurnTints();

            // Cây dưới nền nhà (vị trí cũ + mới) kiểm tra lại có bị che không
            if (Id != 0)
            {
                float R = Reach + 1f;
                var rect = new Rect(P.x - R, P.z - R, 2 * R, 2 * R);
                EntityManager.NotifyTrees(hasLastRect ? Union(rect, lastRect) : rect);
                lastRect = rect;
                hasLastRect = true;
            }
        }

        Rect lastRect;
        bool hasLastRect;

        Vector3 ToWorldFlat(Vector3 local) => new Vector3(P.x, 0, P.z) + Rot * local;

        // ---------- Cọ màu ----------
        public override string PaintCategory(Collider c)
        {
            if (!c) return null;
            string n = c.gameObject.name;
            if (n.StartsWith("Floor")) return "houseFloor";
            if (n == "Door") return "houseDoor";
            if (n == "RoofMesh" || n == "Cone" || n == "Slab") return "houseRoof";
            return null;
        }

        public override int PaintCount(string cat) => cat switch
        {
            "houseFloor" => MatColors.Length,
            "houseDoor" => DoorColors.Length,
            "houseRoof" => RoofColors.Length,
            _ => 0
        };

        public override int PaintGet(Collider c)
        {
            string n = c.gameObject.name;
            if (n.StartsWith("Floor") && int.TryParse(n.Substring(5), out int f)) return Mat(f);
            if (n == "Door") return Mathf.Abs(P.doorColor) % DoorColors.Length;
            return Mathf.Abs(P.roofColor) % RoofColors.Length;
        }

        public override void PaintSet(Collider c, int v)
        {
            string n = c.gameObject.name;
            if (n.StartsWith("Floor") && int.TryParse(n.Substring(5), out int f)) { Mat(f); P.mats[f] = v; }
            else if (n == "Door") P.doorColor = v;
            else P.roofColor = v;
            Rebuild();
        }

        public override void OnRemoved()
        {
            if (hasLastRect) EntityManager.NotifyTrees(lastRect);
        }

        void BuildRoof(Transform parent, float w, float l)
        {
            var roofGo = new GameObject("Roof");
            roofGo.transform.SetParent(parent, false);
            roofGo.transform.localPosition = new Vector3(0, WallTop, 0);
            // Trong khung mái: nóc luôn chạy theo trục z cục bộ = trục dài
            bool swap = (w > l) != P.ridgeSwap;
            if (swap) roofGo.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            float hx = (swap ? l : w) * 0.5f, hz = (swap ? w : l) * 0.5f;
                float o = GameConfig.RoofOverhang;
            float tp = Mathf.Tan(Mathf.Clamp(P.pitch, GameConfig.PitchMin, GameConfig.PitchMax) * Mathf.Deg2Rad);
            var rt = roofGo.transform;

            int type = RoofType;
            if (type == RoofFlat)
            {
                Prim(PrimitiveType.Cube, rt, new Vector3(0, 0.125f, 0), new Vector3(2 * hx + 0.4f, 0.25f, 2 * hz + 0.4f), Palette.StoneDark, true, name: "Slab");
                Prim(PrimitiveType.Cube, rt, new Vector3(0, 0.55f, hz + 0.1f), new Vector3(2 * hx + 0.4f, 0.6f, 0.2f), Palette.StoneDark, name: "Parapet");
                Prim(PrimitiveType.Cube, rt, new Vector3(0, 0.55f, -hz - 0.1f), new Vector3(2 * hx + 0.4f, 0.6f, 0.2f), Palette.StoneDark, name: "Parapet");
                Prim(PrimitiveType.Cube, rt, new Vector3(hx + 0.1f, 0.55f, 0), new Vector3(0.2f, 0.6f, 2 * hz + 0.4f), Palette.StoneDark, name: "Parapet");
                Prim(PrimitiveType.Cube, rt, new Vector3(-hx - 0.1f, 0.55f, 0), new Vector3(0.2f, 0.6f, 2 * hz + 0.4f), Palette.StoneDark, name: "Parapet");
                Prim(PrimitiveType.Cube, rt, new Vector3(hx * 0.4f, 0.32f, hz * 0.4f), new Vector3(0.8f, 0.15f, 0.8f), Palette.Wood, name: "Hatch");
                roofTop = 0.85f;
            }
            else if (type == RoofCone)
            {
                float r = Mathf.Max(hx, hz) * 1.25f + o;
                var g = MeshObj("Cone", MeshLib.Cone(16), rt, Vector3.zero, new Vector3(r, r * tp, r), RoofUnder);
                g.AddComponent<MeshCollider>().sharedMesh = MeshLib.Cone(16);
                roofTop = r * tp;
                var cdm = new DetailMesh(P.seed * 5 + 1);
                WallGraph.ConeTiles(cdm, Vector3.zero, r, r * tp, RoofColor);
                cdm.Build("RoofTiles", rt);
                Prim(PrimitiveType.Cylinder, rt, new Vector3(0, r * tp + 0.3f, 0), new Vector3(0.06f, 0.4f, 0.06f), Palette.Ink, name: "Spire");
                Prim(PrimitiveType.Sphere, rt, new Vector3(0, r * tp + 0.05f, 0), Vector3.one * 0.18f, Palette.Hex("#C9A13B"), name: "Finial");
            }
            else
            {
                var mesh = RoofMeshOf(hx, hz, tp, type != RoofGable, out float top, false);
                if (type == RoofGable) BuildGables(rt, hx, hz, tp, Floors - 1);
                var g = new GameObject("RoofMesh");
                g.transform.SetParent(rt, false);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                g.AddComponent<MeshRenderer>().sharedMaterial = Palette.Mat(RoofUnder);   // lớp lót tối dưới ngói
                BuildRoofTiles(rt, hx, hz, tp, type != RoofGable, RoofColor, P.seed * 7 + 3, true);
                g.AddComponent<MeshCollider>().sharedMesh = mesh;
                roofTop = top;
                BuildDormers(rt, hx, hz, tp);
            }

            // Ống khói: tự động (nhà lớn) / luôn có / không
            if (HasChimney)
            {
                float side = (P.seed & 1) == 0 ? 1f : -1f;
                float h = type == RoofFlat ? 1.4f : roofTop + 0.6f;
                var cpos = new Vector3(hx * 0.45f * side, 0, hz * 0.4f);
                Prim(PrimitiveType.Cube, rt, cpos + Vector3.up * (h * 0.5f), new Vector3(0.6f, h, 0.6f), FoundationStone, name: "Chimney");
                // nắp ống khói + ống sành
                Prim(PrimitiveType.Cube, rt, cpos + Vector3.up * (h + 0.05f), new Vector3(0.78f, 0.12f, 0.78f), FoundationDark, name: "ChimneyCap");
                Prim(PrimitiveType.Cylinder, rt, cpos + Vector3.up * (h + 0.22f), new Vector3(0.24f, 0.12f, 0.24f), Palette.Hex("#B7684F"), name: "ChimneyPot");
                for (int k = 0; k < 4; k++)   // vài viên đá lộ quanh thân
                    Prim(PrimitiveType.Cube, rt, cpos + new Vector3((k % 2 == 0 ? 0.31f : -0.31f), h * (0.35f + 0.15f * k), (k < 2 ? 0.1f : -0.12f)),
                         new Vector3(0.04f, 0.16f, 0.3f), FoundationDark, name: "ChimneyStone");
            }
        }

        public override void PlayPop(float delay)
        {
            if (!visual) return;
            PopIn.Add(visual, delay, true, 0.35f);
            var roof = visual.transform.Find("Roof");
            if (roof) PopIn.Add(roof.gameObject, delay + 0.3f, false, 0.35f);
        }

        // ---------- Di chuyển / xoay ----------
        public override void Translate(Vector3 d)
        {
            if (ActivePart >= FurnBase && Interior.IsViewing(Id)) { MoveFurn(d); return; }
            if (IsWingPart(ActivePart)) { SlideWing(d); return; }
            P.x += d.x;
            P.z += d.z;
            Rebuild();
        }

        public override void Rotate(float degrees)
        {
            if (ActivePart >= FurnBase && Interior.IsViewing(Id))
            {
                if (FurnAt(ActivePart) == null) return;
                Materialize();
                var fu = FurnAt(ActivePart);
                fu.r = (fu.r + (degrees > 0 ? 1 : 3)) & 3;
                ClampFurn(fu);
                RebuildFurniture();
                return;
            }
            P.angle = Mathf.Repeat(P.angle + degrees, 360f);
            Rebuild();
        }

        public override bool AffectedBy(Rect r)
        {
            float R = Reach + 1f;
            return r.Overlaps(new Rect(P.x - R, P.z - R, 2 * R, 2 * R));
        }

        // ---------- Tay nắm ----------
        Vector3 ToWorld(Vector3 local) => new Vector3(P.x, baseY, P.z) + Rot * local;

        public override void GetHandles(List<HandleDef> list)
        {
            bool view = Interior.IsViewing(Id);
            float fh = GameConfig.FloorHeight;
            if (view && ActivePart >= FurnBase)
            {
                var fu = FurnAt(ActivePart);
                if (fu == null) return;
                float y = viewFloor * fh + Furniture.Height(fu.t) + 0.5f;
                list.Add(new HandleDef("fv", HandleKind.Cycle, ToWorld(new Vector3(fu.x - 0.35f, y, fu.z))));
                list.Add(new HandleDef("frot", HandleKind.Cycle, ToWorld(new Vector3(fu.x + 0.35f, y, fu.z))));
                return;
            }
            if (IsWingPart(ActivePart))
            {
                var wg = WingAt(ActivePart);
                if (wg != null) WingHandles(list, wg);
                return;
            }

            float hx = W * 0.5f, hz = L * 0.5f;
            list.Add(new HandleDef("e+x", HandleKind.Point, ToWorld(new Vector3(hx + 0.7f, 0.3f, 0))));
            list.Add(new HandleDef("e-x", HandleKind.Point, ToWorld(new Vector3(-hx - 0.7f, 0.3f, 0))));
            list.Add(new HandleDef("e+z", HandleKind.Point, ToWorld(new Vector3(0, 0.3f, hz + 0.7f))));
            list.Add(new HandleDef("e-z", HandleKind.Point, ToWorld(new Vector3(0, 0.3f, -hz - 0.7f))));
            list.Add(new HandleDef("rot", HandleKind.Point, ToWorld(new Vector3(hx + 1.1f, 0.3f, hz + 1.1f))));

            // Mặt quay về camera: tay nắm tầng + chất liệu đặt ở đó cho dễ thấy
            var center = new Vector3(P.x, baseY, P.z);
            int face = SideFacing(P, TowardCamera(center), false);
            var n = SideNormal(face);
            var tan = SideTangent(face);
            float half = SideHalf(face), along = SideLen(face) * 0.3f;

            if (view)
            {
                // Đang xem trong: chỉ đổi tầng xem + nút ra ngoài (kích thước vẫn chỉnh được)
                float y0 = viewFloor * fh;
                if (viewFloor < Floors - 1)
                    list.Add(new HandleDef("fup", HandleKind.Cycle, ToWorld(n * (half + 0.9f) + Vector3.up * (y0 + 2.4f))));
                if (viewFloor > 0)
                    list.Add(new HandleDef("fdn", HandleKind.Cycle, ToWorld(n * (half + 0.9f) + Vector3.up * (y0 + 0.4f))));
                list.Add(new HandleDef("eye", HandleKind.Cycle, ToWorld(n * (half + 0.9f) + tan * along + Vector3.up * (y0 + 1.4f))));
                return;
            }

            list.Add(new HandleDef("floors", HandleKind.Arrow, ToWorld(n * (half + 0.9f) + Vector3.up * (WallTop + 0.5f))));
            for (int f = 0; f < Floors; f++)
            {
                float fy = f * GameConfig.FloorHeight;
                list.Add(new HandleDef("mat" + f, HandleKind.Cycle,
                    ToWorld(n * (half + 0.5f) + tan * along + Vector3.up * (fy + 1.4f))));
                if (f == 0) continue;
                // Tầng 2+: hẹp dần · ban công
                list.Add(new HandleDef("tp" + f, HandleKind.Cycle,
                    ToWorld(n * (half + 0.5f) - tan * along + Vector3.up * (fy + 1.4f))));
                list.Add(new HandleDef("bc" + f, HandleKind.Cycle,
                    ToWorld(n * (half + 0.5f) + Vector3.up * (fy + 0.5f))));
            }

            int door = Mathf.Abs(P.door) % 4;
            list.Add(new HandleDef("door", HandleKind.Cycle,
                ToWorld(SideNormal(door) * (SideHalf(door) + 0.6f) + Vector3.up * 2.4f)));

            // Mái: đổi kiểu + độ dốc, đặt trên nóc
            float topY = WallTop + roofTop + 1.2f;
            var ridge = (W > L) != P.ridgeSwap ? Vector3.right : Vector3.forward;
            list.Add(new HandleDef("roof", HandleKind.Cycle, ToWorld(Vector3.up * topY + ridge * 1.6f)));
            if (RoofType != RoofFlat)
                list.Add(new HandleDef("pitch", HandleKind.Arrow, ToWorld(Vector3.up * topY - ridge * 1.6f)));
            // Con mắt: xem trong nhà
            list.Add(new HandleDef("eye", HandleKind.Cycle, ToWorld(Vector3.up * (topY + 0.4f))));
            // Cửa sổ mái · ống khói
            var perp = ridge == Vector3.right ? Vector3.forward : Vector3.right;
            if (RoofType == RoofGable || RoofType == RoofHip)
                list.Add(new HandleDef("dorm", HandleKind.Cycle, ToWorld(Vector3.up * topY + perp * 1.6f)));
            list.Add(new HandleDef("chim", HandleKind.Cycle, ToWorld(Vector3.up * topY - perp * 1.6f)));
        }

        public override void BeginHandle(string key)
        {
            startP = JsonUtility.FromJson<HouseParams>(Capture());
            rotInit = false;
        }

        static float Snap(float v, float step) => Mathf.Round(v / step) * step;

        public override void DragHandle(string key, HandleInput input)
        {
            var sp = startP;
            if (sp == null) return;
            var before = Capture();

            if (key.StartsWith("w"))
            {
                if (DragWing(key, input, sp) && Capture() != before) Rebuild();
                return;
            }

            switch (key)
            {
                case "floors":
                    P.floors = Mathf.Clamp(sp.floors + Mathf.RoundToInt(input.axisDelta / GameConfig.FloorDragStep), 1, GameConfig.MaxFloors);
                    break;

                case "pitch":
                    P.pitch = Mathf.Clamp(Snap(sp.pitch + input.axisDelta * GameConfig.PitchPerMeter, 5f),
                                          GameConfig.PitchMin, GameConfig.PitchMax);
                    break;

                case "rot":
                    if (!input.hasGround) return;
                    float a = Mathf.Atan2(input.ground.x - sp.x, input.ground.z - sp.z) * Mathf.Rad2Deg;
                    if (!rotInit) { rotRef = a; rotInit = true; }
                    float ang = sp.angle + Mathf.DeltaAngle(rotRef, a);
                    P.angle = Mathf.Repeat(input.shift ? Mathf.Round(ang) : Snap(ang, GameConfig.HouseAngleSnap), 360f);
                    break;

                default:
                    if (!key.StartsWith("e") || !input.hasGround) return;
                    bool axisX = key[2] == 'x';
                    float sign = key[1] == '+' ? 1f : -1f;
                    var r0 = Quaternion.Euler(0, sp.angle, 0);
                    var local = Quaternion.Inverse(r0) * (input.ground - new Vector3(sp.x, 0, sp.z));
                    float startSize = axisX ? sp.w : sp.l;
                    float maxSize = axisX ? GameConfig.HouseMaxWidth : GameConfig.HouseMaxLength;
                    float coord = (axisX ? local.x : local.z) * sign - 0.7f;   // vị trí cạnh mới (trừ khoảng tay nắm)
                    float size = Mathf.Clamp(Snap(coord + startSize * 0.5f, GameConfig.HouseSnap), GameConfig.HouseMinSize, maxSize);
                    float shift = sign * (size - startSize) * 0.5f;            // cạnh đối diện đứng yên
                    var c = new Vector3(sp.x, 0, sp.z) + r0 * (axisX ? new Vector3(shift, 0, 0) : new Vector3(0, 0, shift));
                    P.x = c.x; P.z = c.z;
                    if (axisX) P.w = size; else P.l = size;
                    break;
            }

            if (Capture() != before) Rebuild();
        }

        public override void ClickHandle(string key)
        {
            switch (key)
            {
                case "eye":
                    if (Interior.IsViewing(Id)) Interior.Exit(); else Interior.Enter(this);
                    return;
                case "fup": Interior.SetFloor(Interior.Floor + 1); return;
                case "fdn": Interior.SetFloor(Interior.Floor - 1); return;
                case "fv":
                case "frot":
                {
                    if (FurnAt(ActivePart) == null) return;
                    Materialize();
                    var fu = FurnAt(ActivePart);
                    if (key == "fv") fu.v = (Mathf.Abs(fu.v) + 1) % Furniture.Variants;
                    else { fu.r = (fu.r + 1) & 3; ClampFurn(fu); }
                    RebuildFurniture();
                    return;
                }
            }
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (key == "dorm") { P.dormers = (Mathf.Abs(P.dormers) + 1) % 3; Rebuild(); return; }
            if (key == "chim") { P.chimney = (Mathf.Abs(P.chimney) + 1) % 3; Rebuild(); return; }
            if (key == "roof" && shiftHeld) P.ridgeSwap = !P.ridgeSwap;     // Shift + click: đổi hướng nóc
            else if (key.StartsWith("tp") && int.TryParse(key.Substring(2), out int tf)) P.taper ^= 1 << tf;
            else if (key.StartsWith("bc") && int.TryParse(key.Substring(2), out int bf)) P.balcony ^= 1 << bf;
            else if (key == "roof")
            {
                int r = (RoofType + 1) % 4;
                if (r == RoofCone && !ConeAllowed(W, L)) r = RoofGable;
                P.roof = r;
            }
            else if (key == "door") P.door = (Mathf.Abs(P.door) + 1) % 4;
            else if (key.StartsWith("mat") && int.TryParse(key.Substring(3), out int f))
            {
                Mat(f);
                P.mats[f] = (Mat(f) + 1) % MatColors.Length;
            }
            Rebuild();
        }

        // =====================================================================
        //  NỘI THẤT (C.10)
        // =====================================================================
        public const int FurnBase = 1000;               // part = FurnBase + chỉ số đồ đạc
        const float InteriorWall = 0.15f;               // bề dày vách khi cắt lớp
        static readonly Color FloorBoard = Palette.Hex("#C9A57C");
        static readonly Color RoomLightColor = Palette.Hex("#FFC77A");

        int viewFloor;
        float cutY0;
        readonly GameObject[] cutWalls = new GameObject[4];
        readonly List<GameObject>[] cutWins = { new(), new(), new(), new() };
        Light roomLight;
        readonly Dictionary<int, GameObject> furnObjs = new();
        readonly Dictionary<int, Color> furnTints = new();
        string autoKey;
        List<Furn> autoList;
        bool rawValid;
        int rawPart;
        Vector2 raw;
        static MaterialPropertyBlock fmpb;

        bool LongZ => L >= W;
        float InX => W * 0.5f - InteriorWall - Inset(Floors - 1);
        float InZ => L * 0.5f - InteriorWall - Inset(Floors - 1);
        int RoomCount => W * L < 20f ? 1 : W * L <= 40f ? 2 : 3;

        // ---------- Part / chọn ----------
        public override int PartOf(Collider c)
        {
            var tag = c ? c.GetComponentInParent<PartTag>() : null;
            return tag ? tag.part : -1;
        }

        public override bool HasPart(int part)
        {
            if (part < 0) return true;
            if (IsWingPart(part)) return WingAt(part) != null;
            if (part < FurnBase) return false;
            if (!Interior.IsViewing(Id)) return false;
            var fu = FurnAt(part);
            return fu != null && fu.f == viewFloor;
        }

        public override bool CanDuplicate => ActivePart < 0;

        public override bool DeletePart(int part)
        {
            if (IsWingPart(part))
            {
                if (WingAt(part) == null) return false;
                P.wings.RemoveAt(part - WingBase);
                Rebuild();
                return true;
            }
            if (part < FurnBase || FurnAt(part) == null) return false;
            Materialize();
            P.furn.RemoveAt(part - FurnBase);
            Rebuild();
            return true;
        }

        public override void EndEdit() => rawValid = false;

        public override bool DragPlaneY(out float y)
        {
            y = baseY + viewFloor * GameConfig.FloorHeight + 0.06f;
            return ActivePart >= FurnBase && Interior.IsViewing(Id);
        }

        public override void SetPartTint(int part, Color? c)
        {
            if (part < 0) { SetTint(c); return; }
            if (c == null) furnTints.Remove(part); else furnTints[part] = c.Value;
            ApplyFurnTint(part);
        }

        void ApplyFurnTints()
        {
            foreach (var k in furnObjs.Keys) ApplyFurnTint(k);
            foreach (var k in wingObjs.Keys) ApplyFurnTint(k);
        }

        void ApplyFurnTint(int part)
        {
            if (!furnObjs.TryGetValue(part, out var go) && !wingObjs.TryGetValue(part, out go)) return;
            if (!go) return;
            fmpb ??= new MaterialPropertyBlock();
            bool has = furnTints.TryGetValue(part, out var t);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!has) { r.SetPropertyBlock(null); continue; }
                var col = Color.Lerp(r.sharedMaterial.color, new Color(t.r, t.g, t.b, 1f), t.a);
                fmpb.Clear();
                fmpb.SetColor("_Color", col);
                fmpb.SetColor("_BaseColor", col);
                r.SetPropertyBlock(fmpb);
            }
        }

        // ---------- Danh sách đồ đạc ----------
        /// <summary>Đồ đạc đang có hiệu lực: tự sinh (chưa chỉnh) hoặc danh sách người chơi đã chỉnh.</summary>
        List<Furn> Layout()
        {
            if (P.custom) return P.furn ??= new List<Furn>();
            string key = $"{W:0.##}|{L:0.##}|{Floors}|{DoorSide}|{P.seed}";
            if (autoList == null || key != autoKey)
            {
                autoKey = key;
                autoList = AutoLayout();
            }
            return autoList;
        }

        Furn FurnAt(int part)
        {
            var l = Layout();
            int i = part - FurnBase;
            return i >= 0 && i < l.Count ? l[i] : null;
        }

        /// <summary>Lần đầu người chơi sửa nội thất: chép bộ tự sinh vào tham số (từ đây game nhớ mọi thay đổi).</summary>
        void Materialize()
        {
            if (P.custom) return;
            var src = Layout();
            P.furn = new List<Furn>(src.Count);
            foreach (var f in src) P.furn.Add(f.Clone());
            P.custom = true;
        }

        void ClampFurn(Furn fu)
        {
            var h = Furniture.Half(fu);
            fu.x = InX > h.x ? Mathf.Clamp(fu.x, -InX + h.x, InX - h.x) : 0f;
            fu.z = InZ > h.y ? Mathf.Clamp(fu.z, -InZ + h.y, InZ - h.y) : 0f;
        }

        void MoveFurn(Vector3 worldDelta)
        {
            if (FurnAt(ActivePart) == null) return;
            Materialize();
            var fu = FurnAt(ActivePart);
            if (!rawValid || rawPart != ActivePart)
            {
                raw = new Vector2(fu.x, fu.z);
                rawValid = true;
                rawPart = ActivePart;
            }
            var ld = Quaternion.Inverse(Rot) * worldDelta;
            raw += new Vector2(ld.x, ld.z);
            float ox = fu.x, oz = fu.z;
            fu.x = Snap(raw.x, 0.25f);
            fu.z = Snap(raw.y, 0.25f);
            ClampFurn(fu);
            if (fu.x != ox || fu.z != oz) RebuildFurniture();
        }

        /// <summary>Thêm 1 món vào tầng đang xem, ở chỗ trống gần giữa nhà. Trả về part của món mới (-1 = không thêm được).</summary>
        public int AddFurniture(int type)
        {
            if (!Interior.IsViewing(Id)) return -1;
            Materialize();
            var fu = new Furn { t = type, f = viewFloor };
            var occ = new List<Rect>();
            foreach (var o in P.furn)
                if (o.f == viewFloor) occ.Add(Furniture.Footprint(o));
            if (StairRect(out var sr, out _)) occ.Add(sr);

            var cands = new List<Vector2>();
            for (float x = -InX; x <= InX; x += 0.25f)
                for (float z = -InZ; z <= InZ; z += 0.25f)
                    cands.Add(new Vector2(x, z));
            cands.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
            var inner = Rect.MinMaxRect(-InX, -InZ, InX, InZ);
            bool ok = false;
            foreach (var c in cands)
            {
                fu.x = c.x;
                fu.z = c.y;
                var r = Furniture.Footprint(fu);
                if (!Inside(inner, r) || !Free(r, occ)) continue;
                ok = true;
                break;
            }
            if (!ok) { fu.x = 0; fu.z = 0; }
            P.furn.Add(fu);
            Rebuild();
            return FurnBase + P.furn.Count - 1;
        }

        static bool Inside(Rect outer, Rect r) =>
            r.xMin >= outer.xMin - 1e-3f && r.xMax <= outer.xMax + 1e-3f &&
            r.yMin >= outer.yMin - 1e-3f && r.yMax <= outer.yMax + 1e-3f;

        static bool Free(Rect r, List<Rect> occ)
        {
            foreach (var o in occ)
                if (o.Overlaps(r)) return false;
            return true;
        }

        // ---------- Phòng, vách ngăn, cầu thang ----------
        /// <summary>Cầu thang (nhà ≥ 2 tầng) ở góc xa cửa nhất, dọc trục dài. dir = hướng đi lên dọc trục dài (±1).</summary>
        bool StairRect(out Rect r, out float dir)
        {
            r = default;
            dir = 1f;
            if (Floors < 2) return false;
            int d = DoorSide;
            float cx = d == 0 ? -1f : d == 1 ? 1f : ((P.seed & 2) != 0 ? 1f : -1f);
            float cz = d == 2 ? -1f : d == 3 ? 1f : ((P.seed & 4) != 0 ? 1f : -1f);
            float sx = Mathf.Min(LongZ ? 1.0f : 2.6f, 2 * InX);
            float sz = Mathf.Min(LongZ ? 2.6f : 1.0f, 2 * InZ);
            var c = new Vector2(cx * (InX - sx * 0.5f), cz * (InZ - sz * 0.5f));
            r = new Rect(c.x - sx * 0.5f, c.y - sz * 0.5f, sx, sz);
            dir = LongZ ? cz : cx;
            return true;
        }

        /// <summary>Vị trí vách ngăn trên trục dài (vuông góc trục dài), né cầu thang.</summary>
        List<float> Partitions()
        {
            var res = new List<float>();
            int n = RoomCount;
            float Lg = LongZ ? L : W;
            bool st = StairRect(out var sr, out _);
            float s0 = LongZ ? sr.yMin : sr.xMin, s1 = LongZ ? sr.yMax : sr.xMax;
            for (int k = 1; k < n; k++)
            {
                float p = -Lg * 0.5f + Lg * k / n;
                if (st && p > s0 - 0.3f && p < s1 + 0.3f) p = s0 + s1 > 0 ? s0 - 0.3f : s1 + 0.3f;
                res.Add(p);
            }
            res.Sort();
            return res;
        }

        List<Rect> Rooms()
        {
            var cuts = new List<float> { LongZ ? -InZ : -InX };
            cuts.AddRange(Partitions());
            cuts.Add(LongZ ? InZ : InX);
            var rooms = new List<Rect>();
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                float a = cuts[i] + (i > 0 ? 0.06f : 0f), b = cuts[i + 1] - (i + 2 < cuts.Count ? 0.06f : 0f);
                if (b - a < 0.5f) continue;
                rooms.Add(LongZ ? Rect.MinMaxRect(-InX, a, InX, b) : Rect.MinMaxRect(a, -InZ, b, InZ));
            }
            return rooms;
        }

        /// <summary>Vị trí ống khói (cục bộ nhà) — bếp lò đặt bên dưới.</summary>
        Vector2 ChimneyLocal()
        {
            bool swap = (W > L) != P.ridgeSwap;
            float hx = (swap ? L : W) * 0.5f, hz = (swap ? W : L) * 0.5f;
            float side = (P.seed & 1) == 0 ? 1f : -1f;
            float rx = hx * 0.45f * side, rz = hz * 0.4f;
            return swap ? new Vector2(rz, -rx) : new Vector2(rx, rz);
        }

        // ---------- Nội thất tự sinh ----------
        List<Furn> AutoLayout()
        {
            var list = new List<Furn>();
            var rng = new System.Random(P.seed * 7919 + 17);
            var rooms = Rooms();
            if (rooms.Count == 0) return list;
            bool st = StairRect(out var sr, out _);
            var parts = Partitions();

            var dn = SideNormal(DoorSide);
            var dp = new Vector2(dn.x, dn.z) * (SideHalf(DoorSide) - 0.6f);
            int dr = 0;
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Contains(dp)) dr = i;

            for (int f = 0; f < Floors; f++)
            {
                var occ = new List<Rect>();
                if (st) occ.Add(Grow(sr, 0.15f));
                foreach (var p in parts)   // chừa lối đi qua ô cửa vách ngăn
                    occ.Add(LongZ ? new Rect(-0.7f, p - 0.8f, 1.4f, 1.6f) : new Rect(p - 0.8f, -0.7f, 1.6f, 1.4f));

                if (f == 0)
                {
                    occ.Add(new Rect(dp.x - 0.9f, dp.y - 0.9f, 1.8f, 1.8f));   // trước cửa ra vào
                    var room = rooms[dr];

                    // Bàn + 2 ghế giữa phòng có cửa
                    int tr = room.width >= room.height ? 0 : 1;
                    var table = new Furn { t = Furniture.Table, f = 0, r = tr, v = rng.Next(3), x = room.center.x, z = room.center.y };
                    if (PlaceNear(list, occ, room, table))
                    {
                        int cv = rng.Next(3);
                        for (int k = -1; k <= 1; k += 2)
                        {
                            var ch = new Furn { t = Furniture.Chair, f = 0, v = cv };
                            if (tr == 0) { ch.x = table.x; ch.z = table.z + k * 0.72f; ch.r = k > 0 ? 2 : 0; }
                            else { ch.x = table.x + k * 0.72f; ch.z = table.z; ch.r = k > 0 ? 3 : 1; }
                            var fr = Furniture.Footprint(ch);
                            if (Inside(room, fr) && Free(fr, occ)) { list.Add(ch); occ.Add(fr); }
                        }
                    }

                    // Bếp lò: dưới ống khói nếu có
                    bool stove = false;
                    if (HasChimney)
                    {
                        var cp = ChimneyLocal();
                        int ri = dr;
                        for (int i = 0; i < rooms.Count; i++)
                            if (rooms[i].Contains(cp)) ri = i;
                        stove = PlaceWall(list, occ, rooms[ri], Furniture.Stove, 0, rng, cp);
                    }
                    if (!stove) PlaceWall(list, occ, room, Furniture.Stove, 0, rng, null);
                    PlaceWall(list, occ, room, Furniture.Cabinet, 0, rng, null);
                    PlaceWall(list, occ, room, Furniture.Plant, 0, rng, null);

                    for (int i = 0; i < rooms.Count; i++)
                    {
                        if (i == dr) continue;
                        PlaceWall(list, occ, rooms[i], rng.Next(2) == 0 ? Furniture.Cabinet : Furniture.Chest, 0, rng, null);
                        PlaceWall(list, occ, rooms[i], Furniture.Plant, 0, rng, null);
                    }
                }
                else
                {
                    for (int i = 0; i < rooms.Count; i++)
                    {
                        PlaceWall(list, occ, rooms[i], Furniture.Bed, f, rng, null);
                        PlaceWall(list, occ, rooms[i], Furniture.Chest, f, rng, null);
                        PlaceWall(list, occ, rooms[i], Furniture.Cabinet, f, rng, null);
                        if (i == 0) PlaceWall(list, occ, rooms[i], Furniture.Lamp, f, rng, null);
                    }
                }
            }
            return list;
        }

        static Rect Grow(Rect r, float m) => new Rect(r.x - m, r.y - m, r.width + 2 * m, r.height + 2 * m);

        /// <summary>Đặt ở vị trí cho trước, nếu vướng thì dịch dần ra xung quanh (bước 0.25 m).</summary>
        static bool PlaceNear(List<Furn> list, List<Rect> occ, Rect room, Furn fu)
        {
            float x0 = fu.x, z0 = fu.z;
            for (int ring = 0; ring <= 8; ring++)
                for (int i = -ring; i <= ring; i++)
                    for (int j = -ring; j <= ring; j++)
                    {
                        if (Mathf.Max(Mathf.Abs(i), Mathf.Abs(j)) != ring) continue;
                        fu.x = x0 + i * 0.25f;
                        fu.z = z0 + j * 0.25f;
                        var r = Furniture.Footprint(fu);
                        if (!Inside(room, r) || !Free(r, occ)) continue;
                        list.Add(fu);
                        occ.Add(r);
                        return true;
                    }
            return false;
        }

        /// <summary>Đặt sát 1 vách của phòng, lưng tựa tường. pref = điểm muốn gần (vd. dưới ống khói).</summary>
        static bool PlaceWall(List<Furn> list, List<Rect> occ, Rect room, int type, int f, System.Random rng, Vector2? pref)
        {
            var sides = new List<int> { 0, 1, 2, 3 };
            for (int i = sides.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (sides[i], sides[j]) = (sides[j], sides[i]);
            }
            if (pref.HasValue)
            {
                var p = pref.Value;
                float D(int s) => s switch
                {
                    0 => room.xMax - p.x, 1 => p.x - room.xMin, 2 => room.yMax - p.y, _ => p.y - room.yMin
                };
                sides.Sort((a, b) => D(a).CompareTo(D(b)));
            }

            int v = rng.Next(Furniture.Variants);
            double t01 = rng.NextDouble();
            foreach (int s in sides)
            {
                var fu = new Furn { t = type, f = f, v = v, r = Furniture.RotFacingAway(SideNormal(s)) };
                var h = Furniture.Half(fu);
                float tMin, tMax, t0;
                if (s < 2)
                {
                    fu.x = s == 0 ? room.xMax - h.x - 0.02f : room.xMin + h.x + 0.02f;
                    tMin = room.yMin + h.y; tMax = room.yMax - h.y;
                    t0 = pref.HasValue ? pref.Value.y : Mathf.Lerp(tMin, tMax, (float)t01);
                }
                else
                {
                    fu.z = s == 2 ? room.yMax - h.y - 0.02f : room.yMin + h.y + 0.02f;
                    tMin = room.xMin + h.x; tMax = room.xMax - h.x;
                    t0 = pref.HasValue ? pref.Value.x : Mathf.Lerp(tMin, tMax, (float)t01);
                }
                if (tMin > tMax) continue;
                t0 = Mathf.Clamp(t0, tMin, tMax);
                for (int k = 0; k < 80; k++)
                {
                    float t = t0 + ((k + 1) / 2) * 0.25f * (k % 2 == 0 ? 1f : -1f);
                    if (t < tMin - 1e-3f || t > tMax + 1e-3f) continue;
                    if (s < 2) fu.z = t; else fu.x = t;
                    var r = Furniture.Footprint(fu);
                    if (!Inside(room, r) || !Free(r, occ)) continue;
                    list.Add(fu);
                    occ.Add(r);
                    return true;
                }
            }
            return false;
        }

        // ---------- Dựng hình khi xem trong ----------
        void BuildInterior(Transform t, int fv)
        {
            float fh = GameConfig.FloorHeight, y0 = fv * fh;
            var grp = new GameObject("Interior").transform;
            grp.SetParent(t, false);

            // Vách ngăn có ô cửa ở giữa
            float A = LongZ ? InX : InZ;
            foreach (float p in Partitions())
            {
                float len = A - 0.5f, c = (A + 0.5f) * 0.5f;
                for (int k = -1; k <= 1; k += 2)
                {
                    if (len <= 0.05f) continue;
                    var pos = LongZ ? new Vector3(k * c, y0 + fh * 0.5f, p) : new Vector3(p, y0 + fh * 0.5f, k * c);
                    var sc = LongZ ? new Vector3(len, fh, 0.12f) : new Vector3(0.12f, fh, len);
                    Prim(PrimitiveType.Cube, grp, pos, sc, Palette.Cream, name: "Partition");
                }
                var lp = LongZ ? new Vector3(0, y0 + 2.1f + (fh - 2.1f) * 0.5f, p) : new Vector3(p, y0 + 2.1f + (fh - 2.1f) * 0.5f, 0);
                var ls = LongZ ? new Vector3(1.0f, fh - 2.1f, 0.12f) : new Vector3(0.12f, fh - 2.1f, 1.0f);
                Prim(PrimitiveType.Cube, grp, lp, ls, Palette.Cream, name: "Lintel");
            }

            // Cầu thang
            if (StairRect(out var sr, out float dir))
            {
                if (fv < Floors - 1)
                {
                    const int n = 9;
                    float len = LongZ ? sr.height : sr.width, step = len / n, rise = fh / n;
                    float start = LongZ ? (dir > 0 ? sr.yMin : sr.yMax) : (dir > 0 ? sr.xMin : sr.xMax);
                    for (int i = 0; i < n; i++)
                    {
                        float h = rise * (i + 1), along = start + dir * step * (i + 0.5f);
                        var pos = LongZ ? new Vector3(sr.center.x, y0 + h * 0.5f, along) : new Vector3(along, y0 + h * 0.5f, sr.center.y);
                        var sc = LongZ ? new Vector3(sr.width, h, step) : new Vector3(step, h, sr.height);
                        Prim(PrimitiveType.Cube, grp, pos, sc, i % 2 == 0 ? Palette.Wood : FloorBoard, name: "Stair");
                    }
                }
                else if (fv > 0)
                {
                    // Tầng trên cùng: ô cầu thang từ dưới lên + lan can
                    Prim(PrimitiveType.Cube, grp, new Vector3(sr.center.x, y0 + 0.07f, sr.center.y),
                         new Vector3(sr.width, 0.02f, sr.height), Palette.Ink, name: "StairHole");
                    if (LongZ)
                    {
                        float rx = sr.center.x - Mathf.Sign(sr.center.x) * sr.width * 0.5f;
                        Prim(PrimitiveType.Cube, grp, new Vector3(rx, y0 + 0.95f, sr.center.y), new Vector3(0.06f, 0.06f, sr.height), Palette.Wood, name: "Rail");
                        for (int k = -1; k <= 1; k++)
                            Prim(PrimitiveType.Cube, grp, new Vector3(rx, y0 + 0.5f, sr.center.y + k * sr.height * 0.45f), new Vector3(0.06f, 0.9f, 0.06f), Palette.Wood, name: "Rail");
                    }
                    else
                    {
                        float rz = sr.center.y - Mathf.Sign(sr.center.y) * sr.height * 0.5f;
                        Prim(PrimitiveType.Cube, grp, new Vector3(sr.center.x, y0 + 0.95f, rz), new Vector3(sr.width, 0.06f, 0.06f), Palette.Wood, name: "Rail");
                        for (int k = -1; k <= 1; k++)
                            Prim(PrimitiveType.Cube, grp, new Vector3(sr.center.x + k * sr.width * 0.45f, y0 + 0.5f, rz), new Vector3(0.06f, 0.9f, 0.06f), Palette.Wood, name: "Rail");
                    }
                }
            }

            // Đèn phòng (sáng hơn khi tối)
            var lg = new GameObject("RoomLight");
            lg.transform.SetParent(grp, false);
            lg.transform.localPosition = new Vector3(0, y0 + fh - 0.4f, 0);
            roomLight = lg.AddComponent<Light>();
            roomLight.type = LightType.Point;
            roomLight.range = Mathf.Max(W, L) + 2f;
            roomLight.color = RoomLightColor;
            roomLight.shadows = LightShadows.None;
            roomLight.intensity = 0.3f;

            BuildFurniture(fv);
        }

        void BuildFurniture(int fv)
        {
            if (!visual) return;
            var old = visual.transform.Find("Furn");
            if (old)
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            furnObjs.Clear();
            var grp = new GameObject("Furn").transform;
            grp.SetParent(visual.transform, false);
            float y = fv * GameConfig.FloorHeight + 0.06f;
            var list = Layout();
            for (int i = 0; i < list.Count; i++)
            {
                var fu = list[i];
                if (fu.f != fv) continue;
                ClampFurn(fu);
                furnObjs[FurnBase + i] = Furniture.Build(fu, grp, y, FurnBase + i);
            }
        }

        /// <summary>Hiệu ứng nảy cho đồ đạc + vách khi vừa vào nhà / đổi tầng.</summary>
        public void PopInterior()
        {
            if (!visual) return;
            int i = 0;
            foreach (var go in furnObjs.Values)
                if (go) PopIn.Add(go, 0.05f + 0.035f * i++);
            var grp = visual.transform.Find("Interior");
            if (!grp) return;
            foreach (Transform ch in grp)
                if (ch.name == "Partition" || ch.name == "Lintel" || ch.name == "Stair" || ch.name == "Rail")
                    PopIn.Add(ch.gameObject, 0f, true, 0.25f);
        }

        void RebuildFurniture()
        {
            if (!visual || !Interior.IsViewing(Id)) { Rebuild(); return; }
            BuildFurniture(viewFloor);
            ApplyFurnTints();
        }

        void LateUpdate()
        {
            if (!visual || !Interior.IsViewing(Id)) return;
            var cam = Camera.main;
            if (!cam) return;
            float fh = GameConfig.FloorHeight;
            var toCam = cam.transform.position - new Vector3(P.x, 0, P.z);
            toCam.y = 0;
            toCam.Normalize();
            for (int s = 0; s < 4; s++)
            {
                var wall = cutWalls[s];
                if (!wall) continue;
                bool low = Vector3.Dot(Rot * SideNormal(s), toCam) > 0.2f;
                float h = low ? 1.0f : fh;
                var lp = wall.transform.localPosition;
                var ls = wall.transform.localScale;
                if (Mathf.Abs(ls.y - h) < 1e-3f) continue;
                wall.transform.localPosition = new Vector3(lp.x, cutY0 + h * 0.5f, lp.z);
                wall.transform.localScale = new Vector3(ls.x, h, ls.z);
                foreach (var w in cutWins[s])
                    if (w) w.SetActive(!low);
            }
            if (roomLight)
                roomLight.intensity = DayCycle.IsNight ? 1.6f : DayCycle.IsEvening ? 0.9f : 0.3f;
        }
    }
}
