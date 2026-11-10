using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Xây tự do bằng mảnh ghép (M10). Mảnh mới tự hít vào mặt của mảnh đang trỏ, hoặc bám lưới 0.5 m trên mặt đất.
    /// Click: đặt · Giữ + kéo: xếp 1 hàng (Shift: cả mảng · Space: lên cao · Shift+Space: cả bức tường)
    /// Alt + click/kéo: tháo · Ctrl + click/kéo: thay bằng mảnh đang chọn · C: lấy mẫu · R: xoay 90° (Shift+R: 15°).
    /// </summary>
    public class BlockTool : Tool
    {
        enum Mode { Idle, Placing, Removing, Replacing }

        static readonly Color OkColor = new Color(0.55f, 0.85f, 0.6f, 0.45f);
        static readonly Color BadColor = new Color(0.95f, 0.35f, 0.3f, 0.45f);
        static readonly Color RemoveColor = new Color(0.95f, 0.3f, 0.25f, 0.55f);
        static readonly Color ReplaceColor = new Color(1f, 0.85f, 0.35f, 0.5f);
        const int MaxRow = 40, MaxArea = 20;

        public static int Type = BlockCatalog.Cube;
        public static int Mat;
        public static float UserYaw;
        static bool matManual;

        Mode mode;
        string before;
        Piece first;
        bool changed;
        readonly List<Piece> row = new();
        readonly List<bool> rowOk = new();

        public override string Name => "Khối";
        public override string Key => "N";
        public override bool Busy => mode != Mode.Idle;
        public override string Hint =>
            "Click: đặt  ·  Kéo: xếp hàng (Shift: mảng · Space: lên cao · Shift+Space: bức tường)  ·  R: xoay 90° (Shift+R: 15°)  ·  " +
            "Alt+click/kéo: tháo  ·  Ctrl+click/kéo: thay  ·  C: lấy mẫu  ·  [ / ] hoặc 1–9: loại  ·  M: chất liệu";
        public override string Status =>
            $"{BlockCatalog.Names[Type]} · {BlockCatalog.MatNames[Mat]} · {UserYaw:0}°" +
            (mode == Mode.Placing ? $" · {CountOk()} mảnh" : "");

        int CountOk()
        {
            int k = 0;
            foreach (var b in rowOk) if (b) k++;
            return k;
        }

        public static void SetType(int t)
        {
            Type = BlockCatalog.Clamp(t);
            if (!matManual) Mat = BlockCatalog.DefaultMat[Type];
        }

        public static void SetMat(int m)
        {
            Mat = Mathf.Abs(m) % BlockCatalog.MatColors.Length;
            matManual = true;
        }

        public override void Exit()
        {
            Finish(false);
        }

        public override void Update(ToolContext c)
        {
            var bw = BlockWorld.I;
            if (!bw) return;

            if (mode == Mode.Idle) Keys(c, bw);

            switch (mode)
            {
                case Mode.Placing: UpdatePlacing(c); return;
                case Mode.Removing:
                case Mode.Replacing: UpdateBrush(c, bw); return;
            }

            if (c.overUI) return;
            var hover = HoverPiece(c, bw);

            if (c.alt)
            {
                if (hover != null) BlockWorld.DrawGhost(hover, RemoveColor, 0.05f);
                if (Input.GetMouseButtonDown(0)) Begin(Mode.Removing, bw);
                return;
            }
            if (c.ctrl)
            {
                if (hover != null)
                {
                    var rep = hover.Clone();
                    rep.t = Type; rep.m = Mat;
                    BlockWorld.DrawGhost(rep, ReplaceColor, 0.05f);
                }
                if (Input.GetMouseButtonDown(0)) Begin(Mode.Replacing, bw);
                return;
            }

            if (!Candidate(c, bw, out var cand)) return;
            bool ok = Valid(bw, cand);
            BlockWorld.DrawGhost(cand, ok ? OkColor : BadColor, 0.005f);
            if (Input.GetMouseButtonDown(0) && ok)
            {
                Begin(Mode.Placing, bw);
                first = cand;
                row.Clear(); rowOk.Clear();
                row.Add(cand); rowOk.Add(true);
            }
        }

        void Keys(ToolContext c, BlockWorld bw)
        {
            if (c.ctrl) return;
            if (Input.GetKeyDown(KeyCode.R)) UserYaw = Mathf.Repeat(UserYaw + (c.shift ? 15f : 90f), 360f);
            if (Input.GetKeyDown(KeyCode.LeftBracket)) SetType((Type + BlockCatalog.Count - 1) % BlockCatalog.Count);
            if (Input.GetKeyDown(KeyCode.RightBracket)) SetType((Type + 1) % BlockCatalog.Count);
            if (Input.GetKeyDown(KeyCode.M)) SetMat(Mat + 1);
            for (int i = 0; i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SetType(i);
            if (Input.GetKeyDown(KeyCode.Alpha0)) SetType(9);
            if (Input.GetKeyDown(KeyCode.C))
            {
                var h = HoverPiece(c, bw);
                if (h != null)
                {
                    Type = BlockCatalog.Clamp(h.t);
                    SetMat(h.m);
                    GameUI.Toast($"Lấy mẫu: {BlockCatalog.Describe(h)}");
                }
            }
        }

        void Begin(Mode m, BlockWorld bw)
        {
            mode = m;
            before = bw.Capture();
            changed = false;
        }

        void Finish(bool commit)
        {
            var bw = BlockWorld.I;
            if (mode == Mode.Placing && commit && bw)
            {
                for (int i = 0; i < row.Count; i++)
                    if (rowOk[i]) { bw.Add(row[i].Clone()); changed = true; }
                if (changed) Sfx.Pop();
            }
            if (mode != Mode.Idle && bw && before != null)
            {
                if (changed && commit) History.Push(new ModifyEntityCommand(bw.Id, before, bw.Capture()));
                else if (changed) bw.Restore(before);
            }
            mode = Mode.Idle;
            before = null;
            first = null;
            row.Clear(); rowOk.Clear();
        }

        // ---------- Kéo xếp hàng / mảng / tường ----------
        void UpdatePlacing(ToolContext c)
        {
            var bw = BlockWorld.I;
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { Finish(false); return; }

            var size = BlockCatalog.Size(first.t);
            var rot = Quaternion.Euler(0, first.yaw, 0);
            var inv = Quaternion.Inverse(rot);
            var center = BlockWorld.Center(first);
            bool vertical = Input.GetKey(KeyCode.Space);
            var ray = c.Ray;

            Plane plane;
            if (vertical)
            {
                var f = c.cam.transform.forward;
                f.y = 0;
                plane = new Plane(f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward, center);
            }
            else plane = new Plane(Vector3.up, center);

            int i0 = 0, i1 = 0, j0 = 0, j1 = 0, k0 = 0, k1 = 0;   // i: x cục bộ, j: y, k: z cục bộ
            if (plane.Raycast(ray, out float dist))
            {
                var dl = inv * (ray.GetPoint(dist) - center);
                float ix = dl.x / size.x, iy = dl.y / size.y, iz = dl.z / size.z;
                bool xDominant = Mathf.Abs(ix) >= Mathf.Abs(iz);
                if (vertical)
                {
                    int ny = Mathf.Clamp(Mathf.RoundToInt(iy), -MaxArea, MaxArea);
                    j0 = Mathf.Min(0, ny); j1 = Mathf.Max(0, ny);
                    if (c.shift)
                    {
                        int nh = Mathf.Clamp(Mathf.RoundToInt(xDominant ? ix : iz), -MaxArea, MaxArea);
                        if (xDominant) { i0 = Mathf.Min(0, nh); i1 = Mathf.Max(0, nh); }
                        else { k0 = Mathf.Min(0, nh); k1 = Mathf.Max(0, nh); }
                    }
                }
                else if (c.shift)
                {
                    int nx = Mathf.Clamp(Mathf.RoundToInt(ix), -MaxArea, MaxArea), nz = Mathf.Clamp(Mathf.RoundToInt(iz), -MaxArea, MaxArea);
                    i0 = Mathf.Min(0, nx); i1 = Mathf.Max(0, nx);
                    k0 = Mathf.Min(0, nz); k1 = Mathf.Max(0, nz);
                }
                else
                {
                    int nh = Mathf.Clamp(Mathf.RoundToInt(xDominant ? ix : iz), -MaxRow, MaxRow);
                    if (xDominant) { i0 = Mathf.Min(0, nh); i1 = Mathf.Max(0, nh); }
                    else { k0 = Mathf.Min(0, nh); k1 = Mathf.Max(0, nh); }
                }
            }

            row.Clear(); rowOk.Clear();
            var hf = HeightField.I;
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                    for (int k = k0; k <= k1; k++)
                    {
                        var p = first.Clone();
                        var off = rot * new Vector3(i * size.x, 0, k * size.z);
                        p.x += off.x;
                        p.z += off.z;
                        p.y += j * size.y;
                        row.Add(p);
                        bool ok = Valid(bw, p) && p.y > hf.HeightAt(p.x, p.z) - size.y * 0.9f;
                        rowOk.Add(ok);
                    }
            for (int n = 0; n < row.Count; n++) BlockWorld.DrawGhost(row[n], rowOk[n] ? OkColor : BadColor, 0.005f);

            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) Finish(true);
        }

        // ---------- Tháo / thay bằng cách quét ----------
        void UpdateBrush(ToolContext c, BlockWorld bw)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { Finish(false); return; }
            var h = HoverPiece(c, bw);
            if (h != null)
            {
                if (mode == Mode.Removing)
                {
                    bw.Remove(h.id);
                    changed = true;
                }
                else if (h.t != Type || h.m != Mat)
                {
                    var old = bw.ChunkOf(h);
                    h.t = Type;
                    h.m = Mat;
                    bw.Changed(h, old);
                    changed = true;
                }
            }
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) Finish(true);
        }

        // ---------- Tính vị trí mảnh mới ----------
        static bool Valid(BlockWorld bw, Piece p) =>
            WallTool.InBuildArea(new Vector3(p.x, 0, p.z)) && !bw.Overlaps(p);

        static bool FirstHit(ToolContext c, out RaycastHit hit, out bool any, out Vector3 ground, out bool hasGround)
        {
            var ray = c.Ray;
            hasGround = HeightField.I.Raycast(ray, out ground);
            float gd = hasGround ? Vector3.Distance(ray.origin, ground) : float.MaxValue;
            hit = default;
            any = false;
            var hits = Physics.RaycastAll(ray, 1000f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.distance > gd + 0.05f) break;
                if (h.collider.isTrigger || h.collider.GetComponent<HandleView>()) continue;
                hit = h;
                any = true;
                return true;
            }
            return hasGround;
        }

        static Piece HoverPiece(ToolContext c, BlockWorld bw)
        {
            if (!FirstHit(c, out var hit, out bool any, out _, out _) || !any) return null;
            return bw.PieceFromHit(hit);
        }

        static float Snap(float v, float step) => Mathf.Round(v / step) * step;

        static bool Candidate(ToolContext c, BlockWorld bw, out Piece p)
        {
            p = new Piece { t = Type, m = Mat, yaw = UserYaw };
            if (!FirstHit(c, out var hit, out bool any, out var ground, out bool hasGround)) return false;
            var size = BlockCatalog.Size(Type);

            var target = any ? bw.PieceFromHit(hit) : null;
            if (target != null)
            {
                // Hít vào mặt của mảnh đang trỏ, theo khung của mảnh đó
                var rotT = Quaternion.Euler(0, target.yaw, 0);
                var inv = Quaternion.Inverse(rotT);
                var hT = BlockCatalog.Size(target.t) * 0.5f;
                var cT = BlockWorld.Center(target);
                var nl = inv * hit.normal;
                int axis = Mathf.Abs(nl.x) >= Mathf.Abs(nl.y) && Mathf.Abs(nl.x) >= Mathf.Abs(nl.z) ? 0
                         : Mathf.Abs(nl.y) >= Mathf.Abs(nl.z) ? 1 : 2;
                float sign = Mathf.Sign(nl[axis]);

                float rel = UserYaw * Mathf.Deg2Rad, ca = Mathf.Abs(Mathf.Cos(rel)), sa = Mathf.Abs(Mathf.Sin(rel));
                var hN = new Vector3(ca * size.x * 0.5f + sa * size.z * 0.5f, size.y * 0.5f, sa * size.x * 0.5f + ca * size.z * 0.5f);
                var hl = inv * (hit.point - cT);
                var cl = hl;
                cl[axis] = sign * (hT[axis] + hN[axis]);
                for (int a = 0; a < 3; a++)
                {
                    if (a == axis) continue;
                    float step = Mathf.Max(0.25f, 2f * hN[a]);
                    float r = Snap(hl[a] - hN[a] + hT[a], step);
                    cl[a] = -hT[a] + r + hN[a];
                }
                var wc = cT + rotT * cl;
                p.x = wc.x;
                p.z = wc.z;
                p.y = wc.y - hN.y;
                p.yaw = Mathf.Repeat(target.yaw + UserYaw, 360f);
                return true;
            }

            // Mặt đất / mái nhà / tường...: bám lưới 0.5 m theo hướng xoay hiện tại
            Vector3 pt, nrm;
            bool onTerrain;
            if (any) { pt = hit.point; nrm = hit.normal; onTerrain = false; }
            else if (hasGround) { pt = ground; nrm = Vector3.up; onTerrain = true; }
            else return false;

            if (nrm.y < 0.5f)
            {
                var flat = new Vector3(nrm.x, 0, nrm.z).normalized;
                pt += flat * (Mathf.Max(size.x, size.z) * 0.5f + 0.02f);
            }
            var rot = Quaternion.Euler(0, UserYaw, 0);
            var l = Quaternion.Inverse(rot) * new Vector3(pt.x, 0, pt.z);
            l.x = Snap(l.x - size.x * 0.5f, 0.5f) + size.x * 0.5f;
            l.z = Snap(l.z - size.z * 0.5f, 0.5f) + size.z * 0.5f;
            var w = rot * l;
            p.x = w.x;
            p.z = w.z;
            if (onTerrain || nrm.y < 0.5f)
            {
                float g = HeightField.I.HeightAt(p.x, p.z);
                p.y = nrm.y < 0.5f ? Mathf.Max(g, Snap(pt.y - size.y * 0.5f, 0.25f)) : g - 0.03f;
            }
            else p.y = pt.y;
            return true;
        }
    }
}
