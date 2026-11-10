using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Tổ hợp khám phá E1–E15 (móc câu H3 + H5). Chi tiết tổ hợp là "phái sinh": không lưu,
    /// mỗi khi làng đổi thì dựng lại toàn bộ từ hiện trạng. Chỉ lưu danh sách đã khám phá.
    /// </summary>
    public class Combos : MonoBehaviour
    {
        public static Combos I;

        public static readonly string[] Names =
        {
            "Cổng thành", "Cầu gỗ", "Cầu đá vòm", "Bến nhỏ", "Liễu rủ", "Nhà dựa tường", "Giếng làng", "Thành trì",
            "Đèn đường", "Đàn vịt", "Hoa súng", "Chuông tháp", "Vườn rau", "Rừng nhỏ", "Lâu đài",
            "Cối xay nước", "Cối xay gió", "Chợ phiên", "Đền đỉnh đồi", "Bến thuyền", "Thác nước"
        };

        public static readonly string[] Hints =
        {
            "Đường đi gặp tường đá thì sao nhỉ?", "Đường đi gặp nước thì sao nhỉ?", "Tường thành vắt qua ao?",
            "Nhà ở sát bờ ao?", "Cây tròn đứng bên bờ nước?", "Nhà dựa lưng vào tường thành?",
            "Ba nhà quây quần, có đường nối tới?", "Một vòng tường bao quanh nhà?", "Đêm xuống, con đường gần nhà...?",
            "Một cái ao thật rộng?", "Ao nông, có cây xung quanh?", "Tháp cao hết cỡ?",
            "Sau nhà còn khoảng đất trống?", "Thật nhiều cây đứng gần nhau?", "Nhà cao tầng trong vòng thành?",
            "Nhà bên dòng nước chảy…", "Trên đỉnh đồi lộng gió…", "Đông nhà quá, cần chỗ họp chợ",
            "Nơi cao nhất làng thì nên có gì?", "Bến có rồi, thiếu gì nhỉ?", "Dòng nước gặp vách đá…"
        };

        public static readonly string[] Descs =
        {
            "Đường cắt qua tường đá → cổng vòm.", "Đường đi qua ao → cầu ván gỗ.", "Tường vắt qua ao → vòm đá dưới chân tường.",
            "Nhà cách bờ ao ≤ 3 m → cầu tàu nhỏ.", "Cây tròn cách bờ ao ≤ 4 m → liễu rủ.", "Cạnh nhà sát tường → không mở cửa sổ phía đó.",
            "≥ 3 nhà gần nhau, ≥ 2 nhà có đường tới cửa → giếng nước.", "Vòng tường kín có nhà bên trong → cờ trên các tháp.",
            "Ban đêm, đường trong 6 m quanh nhà → đèn lồng.", "Ao ≥ 60 m² → đàn vịt.", "Ao nông + ≥ 3 cây quanh bờ → hoa súng.",
            "Tháp 3 tầng → chuông, ban đêm sáng đèn.", "Sau nhà trống 4 × 3 m → luống rau.", "≥ 12 cây trong 10 m → nấm, hoa dại.",
            "Nhà ≥ 3 tầng trong vòng thành → tháp nhỏ trên mái.",
            "Nhà cách sông ≤ 2 m → bánh xe nước quay ở cạnh nhà phía sông.",
            "Nhà 1–2 tầng cao hơn trung bình làng ≥ 5 m → cánh quạt gió trên mái.",
            "≥ 5 nhà trong 20 m, có chỗ trống ≥ 8 × 8 m nối đường → sạp hàng ban ngày.",
            "Tháp/nhà trên đỉnh đồi cao nhất làng (≥ 8 m) → cổng nhỏ và bậc đá dẫn lên.",
            "Bến nhỏ (E4) ở ao ≥ 120 m² hoặc ở sông → thuyền đậu cạnh bến.",
            "Sông chảy qua chỗ dốc > 35° → dải thác và bọt trắng ở chân."
        };

        public const int Count = 21;
        public readonly bool[] Found = new bool[Count];

        Transform deco, cur;
        public static Transform Deco => I ? I.deco : null;
        float dirtyAt = 0.5f;
        readonly HashSet<string> suppressed = new();

        Transform Parent => cur ? cur : deco;

        /// <summary>Các chi tiết người chơi đã tắt (lưu vào file làng).</summary>
        public List<string> Suppressed => new List<string>(suppressed);
        public int SuppressedCount => suppressed.Count;

        public void SetSuppressedList(List<string> list)
        {
            suppressed.Clear();
            if (list != null) foreach (var k in list) suppressed.Add(k);
            MarkDirty();
        }

        public void SetSuppressed(string key, bool hide)
        {
            if (hide) suppressed.Add(key); else suppressed.Remove(key);
            MarkDirty();
        }

        static string KeyOf(int combo, Vector2 anchor) =>
            $"{combo}:{Mathf.RoundToInt(anchor.x / 2f)}:{Mathf.RoundToInt(anchor.y / 2f)}";

        /// <summary>Mở 1 nhóm chi tiết (1 cổng, 1 cầu...). false = người chơi đã tắt chỗ này → bỏ qua.</summary>
        bool Begin(int combo, Vector2 anchor)
        {
            End();
            string key = KeyOf(combo, anchor);
            if (suppressed.Contains(key)) return false;
            var g = new GameObject(Names[combo]);
            g.transform.SetParent(deco, false);
            var tag = g.AddComponent<ComboTag>();
            tag.combo = combo;
            tag.key = key;
            cur = g.transform;
            return true;
        }

        void End()
        {
            if (!cur) return;
            var tag = cur.GetComponent<ComboTag>();
            if (tag) tag.FitCollider();
            cur = null;
        }
        bool lastNight;
        int lights;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        void Awake()
        {
            I = this;
            History.Changed += MarkDirty;
        }

        void OnDestroy() => History.Changed -= MarkDirty;

        public void MarkDirty() => dirtyAt = Time.time + 0.3f;

        public int FoundCount
        {
            get { int c = 0; foreach (var f in Found) if (f) c++; return c; }
        }

        public void SetFound(List<bool> list)
        {
            for (int i = 0; i < Count; i++) Found[i] = list != null && i < list.Count && list[i];
            MarkDirty();
        }

        void Update()
        {
            bool n = DayCycle.IsNight;
            if (n != lastNight) { lastNight = n; MarkDirty(); }
            if (dirtyAt > 0f && Time.time >= dirtyAt)
            {
                dirtyAt = -1f;
                Recompute();
            }
        }

        // ================= Dựng lại toàn bộ chi tiết tổ hợp =================
        void Recompute()
        {
            if (deco) Destroy(deco.gameObject);
            deco = new GameObject("Combos").transform;
            lights = 0;

            var hit = new bool[Count];
            var houses = new List<HouseEntity>(EntityManager.OfType<HouseEntity>());
            var paths = new List<PathEntity>(EntityManager.OfType<PathEntity>());
            var ponds = new List<PondEntity>(EntityManager.OfType<PondEntity>());
            var rivers = new List<RiverEntity>(EntityManager.OfType<RiverEntity>());
            var trees = new List<TreeEntity>();
            foreach (var t in EntityManager.OfType<TreeEntity>()) if (!t.Hidden) trees.Add(t);
            var gr = WallGraph.I;
            var hf = HeightField.I;

            // Tường: danh sách đường gấp khúc
            var walls = new List<(GSeg seg, List<Vector2> poly)>();
            if (gr) foreach (var s in gr.P.segs) walls.Add((s, WallGraph.Poly(gr.P, s)));

            // ---- E1 Cổng thành ----
            var gates = new List<Vector2>();
            foreach (var path in paths)
            {
                var pp = path.Points;
                foreach (var (s, poly) in walls)
                {
                    int style = Mathf.Abs(s.style) % 4;
                    for (int i = 0; i < pp.Count - 1; i++)
                        for (int j = 0; j < poly.Count - 1; j++)
                        {
                            if (!Geo.SegX(pp[i], pp[i + 1], poly[j], poly[j + 1], out var x)) continue;
                            if (style > 1)
                            {
                                // Hàng rào gỗ / bờ rào thấp: cổng gỗ nhỏ (không tính là khám phá)
                                bool near = false;
                                foreach (var g in gates) if (Vector2.Distance(g, x) < 2.5f) near = true;
                                if (near) continue;
                                gates.Add(x);
                                FenceGate(x, (poly[j + 1] - poly[j]).normalized, Mathf.Max(1.6f, path.Width + 0.3f), gr.SegmentHeight(s));
                                continue;
                            }
                            bool dup = false;
                            foreach (var g in gates) if (Vector2.Distance(g, x) < 2.5f) dup = true;
                            if (dup) continue;
                            gates.Add(x);
                            hit[0] = true;
                            if (!Begin(0, x)) continue;
                            Gate(x, (poly[j + 1] - poly[j]).normalized, gr.SegmentHeight(s), GameConfig.WallStyleThickness[style], Mathf.Max(2.4f, path.Width + 0.4f));
                            End();
                        }
                }
            }

            // ---- E2 Cầu gỗ ----
            foreach (var path in paths)
            {
                var pts = Geo.Resample(path.Points, 0.45f);
                bool open = false, show = false;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (!WaterAt(ponds, rivers, pts[i], out float waterLevel)) { if (open) { End(); open = false; } continue; }
                    hit[1] = true;
                    if (!open) { open = true; show = Begin(1, pts[i]); }
                    if (!show) continue;
                    var dir = (i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                    var rot = Quaternion.LookRotation(Geo.X0Z(dir));
                    float y = waterLevel + 0.3f;
                    Box(new Vector3(pts[i].x, y, pts[i].y), new Vector3(path.Width + 0.3f, 0.12f, 0.4f), Palette.Wood, rot);
                    if (i % 3 == 0)
                    {
                        var side = new Vector2(-dir.y, dir.x) * (path.Width * 0.5f + 0.15f);
                        foreach (var sgn in new[] { 1f, -1f })
                        {
                            var q = pts[i] + side * sgn;
                            Box(new Vector3(q.x, y + 0.45f, q.y), new Vector3(0.12f, 0.9f, 0.12f), Palette.Wood, rot);
                        }
                    }
                }
                End();
            }

            // ---- E3 Cầu đá vòm ----
            foreach (var (s, poly) in walls)
            {
                var pts = Geo.Resample(poly, 0.8f);
                float thick = GameConfig.WallStyleThickness[Mathf.Abs(s.style) % 4];
                bool open = false, show = false;
                for (int i = 0; i < pts.Count; i += 3)
                {
                    if (!WaterAt(ponds, rivers, pts[i], out float waterLevel)) { if (open) { End(); open = false; } continue; }
                    hit[2] = true;
                    if (!open) { open = true; show = Begin(2, pts[i]); }
                    if (!show) continue;
                    var dir = (i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                    var rot = Quaternion.LookRotation(Geo.X0Z(new Vector2(-dir.y, dir.x)));
                    Box(new Vector3(pts[i].x, waterLevel + 0.35f, pts[i].y), new Vector3(1.4f, 0.9f, thick + 0.12f), Palette.Ink, rot);
                }
                End();
            }

            // ---- E4 Bến nhỏ · E20 Bến thuyền · E16 Cối xay nước · E6 Nhà dựa tường · E13 Vườn rau ----
            foreach (var h in houses)
            {
                var fp = h.Footprint;
                // E4 + E20 + E16
                float best = 3f;
                Vector2 bestP = default, bestDir = default;
                float bestWaterY = 0f, pierReach = 2.6f;
                bool hasPier = false, pierForBoat = false;
                float bestRiverDist = 2.001f;
                Vector2 bestRiverQ = default;
                float bestRiverWaterY = 0f;

                foreach (var pond in ponds)
                {
                    if (pond.Mask == null || !pond.WorldRect.Overlaps(new Rect(fp.c.x - 15f, fp.c.y - 15f, 30f, 30f))) continue;
                    foreach (var cell in pond.Mask.Contour())
                    {
                        var p = new Vector2(PondMask.Center(cell.x), PondMask.Center(cell.y));
                        float d = BoxDist(fp, p);
                        if (d < best)
                        {
                            best = d;
                            bestP = p;
                            bestDir = (pond.Mask.Centroid() - p).normalized;
                            bestWaterY = pond.WaterLevel;
                            pierReach = 2.6f;
                            hasPier = true;
                            pierForBoat = pond.Mask.Area >= 120f;
                        }
                    }
                }
                foreach (var rv in rivers)
                {
                    if (rv.Points == null || rv.Points.Count < 2 || !rv.Area.Overlaps(new Rect(fp.c.x - 15f, fp.c.y - 15f, 30f, 30f))) continue;
                    Geo.Closest(rv.Points, fp.c, out _, out var q, out _);
                    var toHouse = (fp.c - q);
                    if (toHouse.sqrMagnitude < 1e-4f) continue;
                    var bankPt = q + toHouse.normalized * (rv.Width * 0.5f);
                    float d = BoxDist(fp, bankPt);
                    if (d < bestRiverDist)
                    {
                        bestRiverDist = d;
                        bestRiverQ = q;
                        bestRiverWaterY = rv.WaterLevelAt(q);
                    }
                    if (d < best)
                    {
                        best = d;
                        bestP = bankPt;
                        bestDir = -toHouse.normalized;
                        bestWaterY = rv.WaterLevelAt(q);
                        pierReach = Mathf.Min(2.6f, rv.Width * 0.4f + 0.3f);
                        hasPier = true;
                        pierForBoat = true;
                    }
                }
                if (hasPier) hit[3] = true;
                if (hasPier && Begin(3, bestP))
                {
                    var dir = bestDir;
                    float deckLen = pierReach + 0.4f;
                    var mid = bestP + dir * ((pierReach - 0.2f) * 0.5f);
                    var rot = Quaternion.LookRotation(Geo.X0Z(dir));
                    float y = bestWaterY + 0.35f;
                    Box(new Vector3(mid.x, y, mid.y), new Vector3(1.3f, 0.12f, deckLen), Palette.Wood, rot);
                    var end = bestP + dir * pierReach;
                    var side = new Vector2(-dir.y, dir.x) * 0.55f;
                    Box(new Vector3(end.x + side.x, y - 0.3f, end.y + side.y), new Vector3(0.15f, 1.0f, 0.15f), Palette.Wood, rot);
                    Box(new Vector3(end.x - side.x, y - 0.3f, end.y - side.y), new Vector3(0.15f, 1.0f, 0.15f), Palette.Wood, rot);
                    End();
                }

                // E20 Bến thuyền: E4 ở ao ≥ 120 m² hoặc ở sông → thuyền đậu cạnh bến
                if (hasPier && pierForBoat)
                {
                    hit[19] = true;
                    var side = new Vector2(-bestDir.y, bestDir.x);
                    var boatAnchor = bestP + bestDir * (pierReach * 0.72f) + side * 1.15f;
                    if (Begin(19, boatAnchor))
                    {
                        MooredBoat(boatAnchor, bestDir, bestWaterY);
                        End();
                    }
                }

                // E16 Cối xay nước: nhà cách sông ≤ 2 m → bánh xe nước quay ở cạnh nhà phía sông
                if (bestRiverDist <= 2f)
                {
                    hit[15] = true;
                    var toRiver = (bestRiverQ - fp.c).normalized;
                    int bestSide = 0;
                    float bestDot = -2f;
                    for (int s = 0; s < 4; s++)
                    {
                        var wn = Geo.XZ(h.LocalToWorld(SideNormal(s)) - h.LocalToWorld(Vector3.zero)).normalized;
                        float dot = Vector2.Dot(wn, toRiver);
                        if (dot > bestDot) { bestDot = dot; bestSide = s; }
                    }
                    var sn = SideNormal(bestSide);
                    var st = bestSide < 2 ? Vector3.forward : Vector3.right;
                    float sh = bestSide < 2 ? h.W * 0.5f : h.L * 0.5f;
                    float alongOff = bestSide == h.DoorSide ? Mathf.Min(1.2f, (bestSide < 2 ? h.L : h.W) * 0.28f) : 0f;
                    var wallPt = h.LocalToWorld(sn * (sh + 0.34f) + st * alongOff);
                    var outDir = (h.LocalToWorld(sn) - h.LocalToWorld(Vector3.zero)).normalized;
                    var tanDir = (h.LocalToWorld(st) - h.LocalToWorld(Vector3.zero)).normalized;
                    if (Begin(15, Geo.XZ(wallPt)))
                    {
                        WaterWheel(wallPt, outDir, tanDir, bestRiverWaterY);
                        End();
                    }
                }

                // E6
                if (h.LeansOnWall) hit[5] = true;

                // E13: sau nhà (phía đối diện cửa) trống 4 × 3 m
                int back = h.DoorSide ^ 1;
                var n = SideNormal(back);
                var tan = back < 2 ? Vector3.forward : Vector3.right;
                float half = back < 2 ? h.W * 0.5f : h.L * 0.5f;
                bool free = true;
                for (int a = 0; a <= 3 && free; a++)
                    for (int b = -2; b <= 2 && free; b++)
                    {
                        var wp = h.LocalToWorld(n * (half + 0.8f + a) + tan * b);
                        if (!FreeSpot(new Vector2(wp.x, wp.z), houses, ponds, paths, trees, gr)) free = false;
                    }
                if (free) hit[12] = true;
                if (free && Begin(12, fp.c))
                {
                    var crops = new DetailMesh(Hash((int)fp.c.x, (int)fp.c.y));
                    for (int row = 0; row < 3; row++)
                    {
                        var c = h.LocalToWorld(n * (half + 1.2f + row * 0.9f));
                        float g = hf.HeightAt(c.x, c.z);
                        var rot = Quaternion.LookRotation(h.LocalToWorld(tan) - h.LocalToWorld(Vector3.zero));
                        Box(new Vector3(c.x, g + 0.08f, c.z), new Vector3(0.5f, 0.16f, 3.6f), Palette.Hex("#8A6A4F"), rot);
                        for (int k = -3; k <= 3; k++)
                        {
                            var q = h.LocalToWorld(n * (half + 1.2f + row * 0.9f) + tan * (k * 0.5f));
                            var b = new Vector3(q.x, hf.HeightAt(q.x, q.z) + 0.16f, q.z);
                            if (row == 1) Carrot(crops, b);
                            else Cabbage(crops, b, row == 2);
                        }
                    }
                    crops.Build("Crops", Parent, true, Palette.VertexColorMat());
                    End();
                }
            }

            // ---- E5 Liễu rủ ----
            foreach (var t in trees) if (t.IsWillow) { hit[4] = true; break; }

            // ---- E7 Giếng làng ----
            var used = new HashSet<HouseEntity>();
            foreach (var h in houses)
            {
                if (used.Contains(h)) continue;
                var members = new List<HouseEntity>();
                foreach (var o in houses)
                    if (!used.Contains(o) && Vector2.Distance(o.Footprint.c, h.Footprint.c) <= 15f) members.Add(o);
                if (members.Count < 3) continue;
                int linked = 0;
                foreach (var m in members)
                {
                    var door = Geo.XZ(m.DoorPoint);
                    foreach (var path in paths)
                    {
                        Geo.Closest(path.Points, door, out _, out _, out float d);
                        if (d < 4f) { linked++; break; }
                    }
                }
                if (linked < 2) continue;
                Vector2 c = Vector2.zero;
                foreach (var m in members) { c += m.Footprint.c; used.Add(m); }
                c /= members.Count;
                if (!FindFree(ref c, houses, ponds, paths, trees, gr)) continue;
                hit[6] = true;
                if (Begin(6, c)) { Well(c); End(); }
            }

            // ---- E8 Thành trì · E15 Lâu đài ----
            var wallSegs = new List<(Vector2, Vector2)>();
            foreach (var (_, poly) in walls)
                for (int i = 0; i < poly.Count - 1; i++) wallSegs.Add((poly[i], poly[i + 1]));
            var enclosed = new List<HouseEntity>();
            if (wallSegs.Count > 2)
                foreach (var h in houses)
                    if (Enclosed(h.Footprint.c, wallSegs)) enclosed.Add(h);
            if (enclosed.Count > 0 && gr)
            {
                hit[7] = true;
                var towers = new List<GNode>();
                foreach (var nd in gr.P.nodes)
                {
                    if (gr.LevelOf(nd) <= 0) continue;
                    foreach (var h in enclosed)
                        if (Vector2.Distance(WallGraph.P2(nd), h.Footprint.c) < 45f) { towers.Add(nd); break; }
                }
                GNode tallest = null;
                foreach (var nd in towers) if (tallest == null || gr.TowerTopY(nd) > gr.TowerTopY(tallest)) tallest = nd;
                foreach (var nd in towers)
                    if (Begin(7, WallGraph.P2(nd))) { Flag(new Vector3(nd.x, gr.TowerTopY(nd), nd.z), nd == tallest); End(); }

                foreach (var h in enclosed)
                {
                    if (h.Floors < 3) continue;
                    hit[14] = true;
                    if (Interior.IsViewing(h.Id)) continue;   // đang xem trong nhà: mái đã ẩn
                    var corner = h.LocalToWorld(new Vector3(h.W * 0.5f - 0.5f, h.WallTop, h.L * 0.5f - 0.5f));
                    if (!Begin(14, Geo.XZ(corner))) continue;
                    Cyl(corner + Vector3.up * 1.3f, new Vector3(1.1f, 1.3f, 1.1f), Palette.StoneLight);
                    Cone(corner + Vector3.up * 2.6f, new Vector3(0.75f, 1.3f, 0.75f), Palette.Roof);
                    End();
                }
            }

            // ---- E9 Đèn đường (ban đêm) ----
            if (DayCycle.IsNight)
            {
                foreach (var path in paths)
                {
                    float len = Geo.Len(path.Points);
                    for (float d = 2f; d < len; d += 8f)
                    {
                        var p = Geo.PointAt(path.Points, d);
                        bool nearHouse = false;
                        foreach (var h in houses) if (BoxDist(h.Footprint, p) < 6f) { nearHouse = true; break; }
                        if (!nearHouse || WaterAt(ponds, rivers, p, out _)) continue;
                        var p2 = Geo.PointAt(path.Points, Mathf.Min(len, d + 0.5f));
                        var dir = (p2 - p).normalized;
                        var q = p + new Vector2(-dir.y, dir.x) * (path.Width * 0.5f + 0.4f);
                        hit[8] = true;
                        if (Begin(8, q)) { Lantern(new Vector3(q.x, hf.HeightAt(q.x, q.y), q.y)); End(); }
                    }
                }
            }

            // ---- E10 Đàn vịt · E11 Hoa súng ----
            foreach (var pond in ponds)
            {
                if (pond.Mask == null) continue;
                var c = pond.Mask.Centroid();
                if (pond.Mask.Area >= 60f)
                {
                    float r = float.MaxValue;
                    foreach (var cell in pond.Mask.Contour())
                        r = Mathf.Min(r, Vector2.Distance(c, new Vector2(PondMask.Center(cell.x), PondMask.Center(cell.y))));
                    hit[9] = true;
                    if (Begin(9, c))
                    {
                        for (int k = 0; k < 3; k++) DuckAt(new Vector3(c.x, pond.WaterLevel + 0.1f, c.y), Mathf.Max(1f, r * 0.55f), k * 0.7f);
                        End();
                    }
                }
                if (!pond.IsDeep)
                {
                    int near = 0;
                    foreach (var t in trees) if (pond.Mask.Coverage(t.XZ.x, t.XZ.y, 6f) > 0f) near++;
                    if (near >= 3)
                    {
                        hit[10] = true;
                        if (!Begin(10, c)) continue;
                        int placed = 0;
                        var m = pond.Mask;
                        for (int gz = m.z0; gz < m.z0 + m.h && placed < 10; gz++)
                            for (int gx = m.x0; gx < m.x0 + m.w && placed < 10; gx++)
                            {
                                if (!m.Get(gx, gz) || Hash(gx, gz) % 9 != 0) continue;
                                if (m.Coverage(PondMask.Center(gx), PondMask.Center(gz), 1.2f) < 0.99f) continue;
                                var p = new Vector3(PondMask.Center(gx), pond.WaterLevel + 0.04f, PondMask.Center(gz));
                                Cyl(p, new Vector3(0.7f, 0.01f, 0.7f), Palette.Hex("#6E9E4E"));
                                if (Hash(gx, gz) % 2 == 0) Ball(p + Vector3.up * 0.08f, Vector3.one * 0.18f, Palette.Hex("#F2C4CE"));
                                placed++;
                            }
                        End();
                    }
                }
            }

            // ---- E12 Chuông tháp ----
            if (gr)
                foreach (var nd in gr.P.nodes)
                {
                    if (gr.LevelOf(nd) < 3) continue;
                    hit[11] = true;
                    if (!Begin(11, WallGraph.P2(nd))) continue;
                    var top = new Vector3(nd.x, gr.TowerTopY(nd), nd.z);
                    Cyl(top + Vector3.up * 0.4f, new Vector3(0.08f, 0.4f, 0.08f), Palette.Wood);
                    Ball(top + Vector3.up * 0.95f, new Vector3(0.45f, 0.5f, 0.45f), Palette.Hex("#C9A13B"));
                    if (DayCycle.IsNight) LightAt(top + Vector3.up * 1.2f, 7f);
                    End();
                }

            // ---- E14 Rừng nhỏ ----
            var usedTrees = new HashSet<TreeEntity>();
            foreach (var t in trees)
            {
                if (usedTrees.Contains(t)) continue;
                var group = new List<TreeEntity>();
                foreach (var o in trees) if (Vector2.Distance(o.XZ, t.XZ) <= 10f) group.Add(o);
                if (group.Count < 12) continue;
                Vector2 c = Vector2.zero;
                foreach (var o in group) { c += o.XZ; usedTrees.Add(o); }
                c /= group.Count;
                hit[13] = true;
                if (!Begin(13, c)) continue;
                for (int k = 0; k < 12; k++)
                {
                    float a = Hash(k, (int)(c.x * 10)) % 628 / 100f, r = 1f + Hash((int)(c.y * 10), k) % 50 / 10f;
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    float g = hf.HeightAt(p.x, p.y);
                    if (k % 2 == 0)
                    {
                        Cyl(new Vector3(p.x, g + 0.12f, p.y), new Vector3(0.08f, 0.12f, 0.08f), Palette.Cream);
                        Ball(new Vector3(p.x, g + 0.26f, p.y), new Vector3(0.3f, 0.15f, 0.3f), Palette.Hex("#C0473A"));
                    }
                    else Ball(new Vector3(p.x, g + 0.12f, p.y), Vector3.one * 0.18f, k % 3 == 0 ? Palette.Hex("#F2D45C") : Palette.Hex("#E8A7B5"));
                }
                End();
            }

            // ---- Độ cao trung bình và đỉnh cao nhất của làng (cho E17, E19) ----
            float avgVillageY = 0f, maxVillageY = float.MinValue;
            int sampleCount = 0;
            float bHalf = GameConfig.BuildRadius;
            for (float sz = -bHalf; sz <= bHalf; sz += 4f)
                for (float sx = -bHalf; sx <= bHalf; sx += 4f)
                {
                    float y = hf.BaseHeightAt(sx, sz);
                    avgVillageY += y;
                    if (y > maxVillageY) maxVillageY = y;
                    sampleCount++;
                }
            if (sampleCount > 0) avgVillageY /= sampleCount;

            // ---- E17 Cối xay gió: nhà 1–2 tầng trên điểm cao hơn trung bình làng ≥ 5 m ----
            foreach (var h in houses)
            {
                if (h.Floors < 1 || h.Floors > 2) continue;
                var c = h.Footprint.c;
                float hy = Mathf.Max(h.BaseY, hf.HeightAt(c.x, c.y));
                if (hy - avgVillageY < 5f) continue;
                hit[16] = true;
                if (Interior.IsViewing(h.Id)) continue;
                if (Begin(16, c))
                {
                    WindmillSails(h);
                    End();
                }
            }

            // ---- E18 Chợ phiên: ≥ 5 nhà trong 20 m, có chỗ trống ≥ 8×8 m nối đường ----
            if (houses.Count >= 5 && paths.Count > 0)
            {
                var marketCenters = new List<Vector2>();
                var triedCenters = new List<Vector2>();
                foreach (var h in houses)
                {
                    var members = new List<HouseEntity>();
                    foreach (var o in houses)
                        if (Vector2.Distance(o.Footprint.c, h.Footprint.c) <= 20f) members.Add(o);
                    if (members.Count < 5) continue;

                    Vector2 c = Vector2.zero;
                    foreach (var m in members) c += m.Footprint.c;
                    c /= members.Count;

                    bool nearExisting = false;
                    foreach (var mc in marketCenters)
                        if (Vector2.Distance(mc, c) < 22f) { nearExisting = true; break; }
                    if (!nearExisting)
                        foreach (var tc in triedCenters)
                            if (Vector2.Distance(tc, c) < 22f) { nearExisting = true; break; }
                    if (nearExisting) continue;

                    bool foundSq = FindMarketSquare(c, houses, ponds, rivers, paths, trees, gr, out var sq);
                    triedCenters.Add(c);
                    if (!foundSq) continue;
                    marketCenters.Add(sq);
                    hit[17] = true;
                    if (!DayCycle.IsNight && Begin(17, sq))
                    {
                        MarketStalls(sq);
                        End();
                    }
                }
            }

            // ---- E19 Đền đỉnh đồi: tháp/nhà nằm trên đỉnh đồi cao nhất làng, cao ≥ 8 m ----
            {
                float bestPeakY = -1f;
                Vector2 peakPos = default, stepDir = Vector2.down;
                float startDist = 2.4f;
                foreach (var h in houses)
                {
                    var c = h.Footprint.c;
                    float y = Mathf.Max(h.BaseY, hf.HeightAt(c.x, c.y));
                    if (y > bestPeakY)
                    {
                        bestPeakY = y;
                        peakPos = c;
                        var dp = Geo.XZ(h.DoorPoint) - c;
                        stepDir = dp.sqrMagnitude > 1e-3f ? dp.normalized : DownhillDir(hf, c);
                        startDist = Mathf.Max(h.W, h.L) * 0.5f + 0.9f;
                    }
                }
                if (gr)
                {
                    foreach (var nd in gr.P.nodes)
                    {
                        if (gr.LevelOf(nd) <= 0) continue;
                        var c = WallGraph.P2(nd);
                        float y = hf.HeightAt(c.x, c.y);
                        if (y > bestPeakY)
                        {
                            bestPeakY = y;
                            peakPos = c;
                            stepDir = DownhillDir(hf, c);
                            startDist = GameConfig.TowerRadius[Mathf.Clamp(gr.LevelOf(nd), 0, 3)] + 0.9f;
                        }
                    }
                }
                if (bestPeakY >= 8f && bestPeakY >= maxVillageY - 1.8f)
                {
                    hit[18] = true;
                    if (Begin(18, peakPos))
                    {
                        HillShrine(peakPos, stepDir, startDist);
                        End();
                    }
                }
            }

            // ---- E21 Thác nước: sông chảy qua chỗ dốc > 35° ----
            float tan35 = Mathf.Tan(35f * Mathf.Deg2Rad);
            foreach (var rv in rivers)
            {
                if (rv.Points == null || rv.Points.Count < 2) continue;
                var pts = Geo.Resample(rv.Points, 0.8f);
                int n = pts.Count;
                int i = 0;
                while (i < n - 1)
                {
                    if (!IsWaterfallStep(hf, rv, pts, i, tan35)) { i++; continue; }
                    int i0 = i, i1 = i + 1;
                    while (i1 < n - 1 && IsWaterfallStep(hf, rv, pts, i1, tan35)) i1++;
                    hit[20] = true;
                    var mid = pts[(i0 + i1) / 2];
                    if (Begin(20, mid))
                    {
                        Waterfall(rv, pts, i0, i1);
                        End();
                    }
                    i = i1 + 1;
                }
            }
            End();

            // ---- Ghi nhận khám phá mới ----
            for (int i = 0; i < Count; i++)
                if (hit[i] && !Found[i])
                {
                    Found[i] = true;
                    GameUI.Toast($"✨ Khám phá mới: {Names[i]}  ({FoundCount}/{Count})");
                    Sfx.Play(SfxId.Chime, 1f, 0.7f);
                }
        }

        // ================= Kiểm tra =================
        static PondEntity PondAt(List<PondEntity> ponds, Vector2 p)
        {
            foreach (var pond in ponds)
                if (pond.Mask != null && pond.Mask.ContainsWorld(p.x, p.y)) return pond;
            return null;
        }

        static RiverEntity RiverAt(List<RiverEntity> rivers, Vector2 p)
        {
            foreach (var rv in rivers)
                if (rv.Covers(p, 0f)) return rv;
            return null;
        }

        static bool WaterAt(List<PondEntity> ponds, List<RiverEntity> rivers, Vector2 p, out float waterLevel)
        {
            var pond = PondAt(ponds, p);
            if (pond != null) { waterLevel = pond.WaterLevel; return true; }
            var rv = RiverAt(rivers, p);
            if (rv != null) { waterLevel = rv.WaterLevelAt(p); return true; }
            waterLevel = 0f;
            return false;
        }

        static float BoxDist(Obb b, Vector2 p)
        {
            var d = p - b.c;
            float dx = Mathf.Max(0f, Mathf.Abs(Vector2.Dot(d, b.ax)) - b.hx);
            float dz = Mathf.Max(0f, Mathf.Abs(Vector2.Dot(d, b.az)) - b.hz);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static bool FreeSpot(Vector2 p, List<HouseEntity> houses, List<PondEntity> ponds, List<PathEntity> paths, List<TreeEntity> trees, WallGraph gr)
        {
            if (!WallTool.InBuildArea(Geo.X0Z(p))) return false;
            foreach (var h in houses) if (h.ContainsPoint(p, 0.3f)) return false;
            if (PondAt(ponds, p) != null) return false;
            foreach (var rv in EntityManager.OfType<RiverEntity>()) if (rv.Covers(p, 0.4f)) return false;
            foreach (var path in paths) if (path.Covers(p, 0.2f)) return false;
            foreach (var t in trees) if (Vector2.Distance(t.XZ, p) < 1.2f) return false;
            if (gr && gr.NearestSeg(p, 1.0f, out _, out _)) return false;
            return true;
        }

        static bool FindFree(ref Vector2 c, List<HouseEntity> houses, List<PondEntity> ponds, List<PathEntity> paths, List<TreeEntity> trees, WallGraph gr)
        {
            for (int ring = 0; ring < 6; ring++)
                for (int k = 0; k < Mathf.Max(1, ring * 6); k++)
                {
                    float a = k * Mathf.PI * 2f / Mathf.Max(1, ring * 6);
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring * 1.5f;
                    bool ok = true;
                    for (int dx = -1; dx <= 1 && ok; dx++)
                        for (int dz = -1; dz <= 1 && ok; dz++)
                            if (!FreeSpot(p + new Vector2(dx, dz) * 1.1f, houses, ponds, paths, trees, gr)) ok = false;
                    if (ok) { c = p; return true; }
                }
            return false;
        }

        /// <summary>Bắn 8 tia; tia nào cũng gặp tường trong 70 m → bị bao quanh.</summary>
        static bool Enclosed(Vector2 p, List<(Vector2 a, Vector2 b)> segs)
        {
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f + 0.13f;
                var q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 70f;
                bool any = false;
                foreach (var (s0, s1) in segs)
                    if (Geo.SegX(p, q, s0, s1, out _)) { any = true; break; }
                if (!any) return false;
            }
            return true;
        }

        static Vector3 SideNormal(int s) => s switch
        {
            0 => Vector3.right, 1 => Vector3.left, 2 => Vector3.forward, _ => Vector3.back
        };

        static int Hash(int x, int z)
        {
            unchecked
            {
                int h = x * 73856093 ^ z * 19349663;
                h ^= h >> 13;
                return h & 0x7fffffff;
            }
        }

        // ================= Dựng chi tiết =================
        GameObject Prim(PrimitiveType t, Vector3 pos, Vector3 scale, Color c, Quaternion? rot = null)
        {
            var g = GameObject.CreatePrimitive(t);
            var col = g.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);
            g.transform.SetParent(Parent, false);
            g.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Palette.Mat(c);
            return g;
        }

        GameObject Box(Vector3 p, Vector3 s, Color c, Quaternion? r = null) => Prim(PrimitiveType.Cube, p, s, c, r);
        GameObject Ball(Vector3 p, Vector3 s, Color c) => Prim(PrimitiveType.Sphere, p, s, c);

        // bắp cải: lõi tròn + 5 lá ngoài xoè, mép lá đậm hơn (red = bắp cải tím)
        static void Cabbage(DetailMesh dm, Vector3 b, bool red)
        {
            Color core = red ? Palette.Hex("#8C5A86") : Palette.Hex("#A9C77A");
            Color leaf = red ? Palette.Hex("#6E4A73") : Palette.Hex("#6E9B4E");
            dm.Blob(b + Vector3.up * 0.06f, new Vector3(0.12f, 0.11f, 0.12f), dm.Vary(core, 0.05f), leaf, 0.08f, 8, 5);
            float a0 = dm.Range(0f, 6.28f);
            for (int i = 0; i < 5; i++)
            {
                float a = a0 + i * 1.2566f;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                dm.Blob(b + d * 0.12f + Vector3.up * 0.02f, new Vector3(0.11f, 0.045f, 0.11f), dm.Vary(leaf, 0.06f), leaf * 0.8f, 0.15f, 7, 4);
            }
        }

        // cà rốt: đỉnh cam lấp ló + chùm lá ngọn mảnh
        static void Carrot(DetailMesh dm, Vector3 b)
        {
            dm.Blob(b + Vector3.up * 0.0f, new Vector3(0.05f, 0.04f, 0.05f), Palette.Hex("#E3873A"), Palette.Hex("#C96A2A"), 0.05f, 6, 4);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.2566f + dm.Range(-0.3f, 0.3f);
                var top = b + new Vector3(Mathf.Cos(a) * 0.09f, dm.Range(0.2f, 0.3f), Mathf.Sin(a) * 0.09f);
                dm.Taper(b + Vector3.up * 0.03f, top, 0.012f, 0.006f, 4, Palette.Hex("#5E8F3E"), Palette.Hex("#7FB04F"));
                dm.Blob(top, new Vector3(0.05f, 0.03f, 0.05f), dm.Vary(Palette.Hex("#7FB04F"), 0.05f), Palette.Hex("#5E8F3E"), 0.2f, 5, 3);
            }
        }
        GameObject Cyl(Vector3 p, Vector3 s, Color c) => Prim(PrimitiveType.Cylinder, p, s, c);

        void Cone(Vector3 p, Vector3 s, Color c)
        {
            var g = new GameObject("Cone");
            g.transform.SetParent(Parent, false);
            g.transform.position = p;
            g.transform.localScale = s;
            g.AddComponent<MeshFilter>().sharedMesh = MeshLib.Cone(12);
            g.AddComponent<MeshRenderer>().sharedMaterial = Palette.Mat(c);
        }

        void Gate(Vector2 x, Vector2 tangent, float wallH, float thick, float width)
        {
            var hf = HeightField.I;
            float g = hf.HeightAt(x.x, x.y);
            var nrm = new Vector2(-tangent.y, tangent.x);
            var rot = Quaternion.LookRotation(Geo.X0Z(nrm));
            float openH = 2.3f, top = Mathf.Max(wallH, openH + 0.3f);
            // ô cổng có vòm tròn phía trên + đá vòm sáng màu
            float rectH = openH - width * 0.5f;
            Box(new Vector3(x.x, g + rectH * 0.5f, x.y), new Vector3(width, rectH, thick + 0.25f), Palette.Ink, rot);
            Prim(PrimitiveType.Cylinder, new Vector3(x.x, g + rectH, x.y), new Vector3(width, (thick + 0.25f) * 0.5f, width), Palette.Ink, rot * Quaternion.Euler(90f, 0, 0));
            for (int k = 0; k <= 8; k++)
            {
                float a = Mathf.PI * k / 8f;
                var off = Geo.X0Z(tangent) * (Mathf.Cos(a) * (width * 0.5f + 0.18f)) + Vector3.up * (Mathf.Sin(a) * (width * 0.5f + 0.18f));
                Box(new Vector3(x.x, g + rectH, x.y) + off, new Vector3(0.36f, 0.24f, thick + 0.32f), Palette.StoneLight, rot * Quaternion.Euler(0, 0, -Mathf.Rad2Deg * a + 90f));
            }
            foreach (var sgn in new[] { 1f, -1f })
            {
                var p = x + tangent * (width * 0.5f + 0.3f) * sgn;
                Box(new Vector3(p.x, g + (top + 0.4f) * 0.5f, p.y), new Vector3(0.6f, top + 0.4f, thick + 0.45f), Palette.StoneDark, rot);
            }
            Box(new Vector3(x.x, g + top + 0.1f, x.y), new Vector3(width + 1.2f, 0.45f, thick + 0.45f), Palette.StoneDark, rot);

            // cánh cổng gỗ (khép) ở cả hai mặt tường: ván dọc, khe giữa, nẹp sắt, tay nắm đồng
            var tan3 = Geo.X0Z(tangent);
            var nrm3 = Geo.X0Z(nrm);
            var basePt = new Vector3(x.x, g, x.y);
            float dw = width - 0.06f;
            foreach (var side in new[] { 1f, -1f })
            {
                var face = nrm3 * side * ((thick + 0.25f) * 0.5f + 0.015f);
                Box(basePt + face + Vector3.up * (rectH * 0.5f), new Vector3(dw, rectH, 0.04f), Palette.Wood, rot).WithPattern(Palette.Wood, Palette.PatWood);
                Prim(PrimitiveType.Cylinder, basePt + face + Vector3.up * rectH, new Vector3(dw, 0.02f, dw), Palette.Wood, rot * Quaternion.Euler(90f, 0, 0)).WithPattern(Palette.Wood, Palette.PatWood);
                var face2 = nrm3 * side * ((thick + 0.25f) * 0.5f + 0.045f);
                Box(basePt + face2 + Vector3.up * ((rectH + dw * 0.5f) * 0.5f), new Vector3(0.05f, rectH + dw * 0.5f - 0.05f, 0.03f), Palette.Ink, rot);   // khe giữa hai cánh
                foreach (var hy in new[] { 0.35f, rectH * 0.55f + 0.2f })
                    Box(basePt + face2 + Vector3.up * hy, new Vector3(dw * 0.94f, 0.1f, 0.03f), Palette.Ink, rot);
                foreach (var sg in new[] { 1f, -1f })
                    Box(basePt + face2 + nrm3 * side * 0.03f + tan3 * (0.18f * sg) + Vector3.up * (rectH * 0.45f), new Vector3(0.09f, 0.09f, 0.05f), Palette.Hex("#C9A44C"), rot);
            }
        }

        void FenceGate(Vector2 x, Vector2 tangent, float width, float h)
        {
            float g = HeightField.I.HeightAt(x.x, x.y);
            var rot = Quaternion.LookRotation(Geo.X0Z(tangent));
            float post = Mathf.Max(1.1f, h + 0.25f);
            foreach (var sgn in new[] { 1f, -1f })
            {
                var p = x + tangent * (width * 0.5f + 0.1f) * sgn;
                Box(new Vector3(p.x, g + post * 0.5f, p.y), new Vector3(0.2f, post, 0.2f), Palette.Hex("#6B4F3E"), rot);
                Box(new Vector3(p.x, g + post + 0.05f, p.y), new Vector3(0.28f, 0.1f, 0.28f), Palette.Hex("#6B4F3E"), rot);
            }
            // cánh cổng mở hé, xoay quanh trụ
            var hinge = x - tangent * (width * 0.5f);
            var dir = Quaternion.Euler(0, 65f, 0) * Geo.X0Z(tangent);
            float leaf = width * 0.9f;
            var mid = new Vector3(hinge.x, g, hinge.y) + dir * (leaf * 0.5f);
            var lrot = Quaternion.LookRotation(dir);
            Box(mid + Vector3.up * 0.75f, new Vector3(0.06f, 0.1f, leaf), Palette.Hex("#9A7352"), lrot);
            Box(mid + Vector3.up * 0.35f, new Vector3(0.06f, 0.1f, leaf), Palette.Hex("#9A7352"), lrot);
            for (int k = -1; k <= 1; k++)
                Box(mid + dir * (k * leaf * 0.4f) + Vector3.up * 0.55f, new Vector3(0.06f, 0.65f, 0.09f), Palette.Hex("#9A7352"), lrot);
        }

        void Well(Vector2 c)
        {
            float g = HeightField.I.HeightAt(c.x, c.y);
            Cyl(new Vector3(c.x, g + 0.45f, c.y), new Vector3(1.6f, 0.45f, 1.6f), Palette.StoneDark);
            Cyl(new Vector3(c.x, g + 0.91f, c.y), new Vector3(1.2f, 0.02f, 1.2f), Palette.Hex("#3B5A6B"));
            foreach (var sgn in new[] { 1f, -1f })
                Box(new Vector3(c.x + 0.75f * sgn, g + 1.5f, c.y), new Vector3(0.14f, 2.0f, 0.14f), Palette.Wood);
            Box(new Vector3(c.x, g + 2.4f, c.y), new Vector3(1.7f, 0.12f, 0.12f), Palette.Wood);
            var roof = new GameObject("Cone");
            roof.transform.SetParent(Parent, false);
            roof.transform.SetPositionAndRotation(new Vector3(c.x, g + 2.45f, c.y), Quaternion.Euler(0, 45f, 0));
            roof.transform.localScale = new Vector3(1.4f, 0.9f, 1.4f);
            roof.AddComponent<MeshFilter>().sharedMesh = MeshLib.Cone(4);
            roof.AddComponent<MeshRenderer>().sharedMaterial = Palette.Mat(Palette.Roof);
        }

        void Flag(Vector3 top, bool big)
        {
            float pole = big ? 2.6f : 1.4f;
            Cyl(top + Vector3.up * pole * 0.5f, new Vector3(0.07f, pole * 0.5f, 0.07f), Palette.Ink);
            var size = big ? new Vector3(0.05f, 0.7f, 1.2f) : new Vector3(0.04f, 0.35f, 0.6f);
            Box(top + Vector3.up * (pole - size.y * 0.5f) + new Vector3(0, 0, size.z * 0.5f), size, big ? Palette.Accent : Palette.Hex("#E9B44C"));
        }

        void Lantern(Vector3 ground)
        {
            // cột đèn: đế đá, thân sắt có vòng đai, lồng kính sáng, khung 4 góc, mái chóp + núm
            var ink = Palette.Ink;
            Box(ground + Vector3.up * 0.12f, new Vector3(0.34f, 0.24f, 0.34f), Palette.StoneDark);
            Box(ground + Vector3.up * 1.15f, new Vector3(0.09f, 1.9f, 0.09f), ink);
            Box(ground + Vector3.up * 0.32f, new Vector3(0.16f, 0.08f, 0.16f), ink);
            Box(ground + Vector3.up * 2.08f, new Vector3(0.22f, 0.06f, 0.22f), ink);
            var l = Box(ground + Vector3.up * 2.28f, new Vector3(0.24f, 0.32f, 0.24f), Palette.Cream);
            var r = l.GetComponent<Renderer>();
            r.sharedMaterial = Palette.LanternMat();
            r.shadowCastingMode = ShadowCastingMode.Off;
            var dm = new DetailMesh(Hash((int)(ground.x * 10f), (int)(ground.z * 10f)));
            foreach (var (sx, sz) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) })
                dm.Box(ground + new Vector3(sx * 0.125f, 2.28f, sz * 0.125f), new Vector3(0.035f, 0.36f, 0.035f), Quaternion.identity, ink);
            dm.Taper(ground + Vector3.up * 2.45f, ground + Vector3.up * 2.68f, 0.24f, 0.03f, 4, ink, ink);
            dm.Blob(ground + Vector3.up * 2.71f, Vector3.one * 0.04f, ink, ink, 0f, 6, 4);
            dm.Build("LampFrame", Parent, true, Palette.VertexColorMat());
            LightAt(ground + Vector3.up * 2.28f, 6f);
        }

        void LightAt(Vector3 p, float range)
        {
            if (lights >= 16) return;   // giới hạn số đèn để giữ hiệu năng
            lights++;
            var go = new GameObject("Light");
            go.transform.SetParent(Parent, false);
            go.transform.position = p;
            var li = go.AddComponent<Light>();
            li.type = LightType.Point;
            li.range = range;
            li.intensity = 1.4f;
            li.color = Palette.Hex("#FFC27A");
            li.shadows = LightShadows.None;
        }

        void DuckAt(Vector3 center, float radius, float phase)
        {
            var root = new GameObject("Duck");
            root.transform.SetParent(Parent, false);
            var d = root.AddComponent<Duck>();
            d.center = center;
            d.radius = radius;
            d.phase = phase;
            d.speed = 0.18f + phase * 0.03f;
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0, 0.12f, 0), new Vector3(0.45f, 0.3f, 0.65f), Palette.Cream);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0, 0.35f, 0.28f), Vector3.one * 0.25f, Palette.Cream);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.33f, 0.45f), new Vector3(0.1f, 0.06f, 0.14f), Palette.Hex("#E9A23B"));
        }

        static void Part(Transform parent, PrimitiveType t, Vector3 lp, Vector3 s, Color c)
        {
            var g = GameObject.CreatePrimitive(t);
            var col = g.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localScale = s;
            g.GetComponent<Renderer>().sharedMaterial = Palette.Mat(c);
        }

        // ================= E16–E21 (S7) =================
        static Vector2 DownhillDir(HeightField hf, Vector2 p)
        {
            float dx = hf.HeightAt(p.x + 2f, p.y) - hf.HeightAt(p.x - 2f, p.y);
            float dz = hf.HeightAt(p.x, p.y + 2f) - hf.HeightAt(p.x, p.y - 2f);
            var g = new Vector2(-dx, -dz);
            return g.sqrMagnitude > 1e-4f ? g.normalized : Vector2.down;
        }

        static bool IsWaterfallStep(HeightField hf, RiverEntity rv, List<Vector2> pts, int i, float tan35)
        {
            int j = Mathf.Min(pts.Count - 1, i + 2);
            var p0 = pts[i];
            var p1 = pts[j];
            float hd = Vector2.Distance(p0, p1);
            if (hd <= 0.2f) return false;
            float drop = Mathf.Max(hf.BaseHeightAt(p0.x, p0.y) - hf.BaseHeightAt(p1.x, p1.y), rv.WaterLevelAt(p0) - rv.WaterLevelAt(p1));
            return drop / hd > tan35;
        }

        static bool MarketSquareFree(Vector2 p, List<HouseEntity> houses, List<PondEntity> ponds, List<RiverEntity> rivers, List<PathEntity> paths, List<TreeEntity> trees, WallGraph gr)
        {
            var hf = HeightField.I;
            float y0 = hf.HeightAt(p.x, p.y);
            // Nối đường: ít nhất 1 con đường đi tới sát hoặc qua quảng trường 8×8 m
            bool linkedPath = false;
            foreach (var path in paths)
            {
                Geo.Closest(path.Points, p, out _, out _, out float d);
                if (d <= 6.5f) { linkedPath = true; break; }
            }
            if (!linkedPath) return false;

            // Trống ≥ 8×8 m (không vướng nhà, nước, cây, tường)
            for (int ix = -2; ix <= 2; ix++)
                for (int iz = -2; iz <= 2; iz++)
                {
                    var q = p + new Vector2(ix * 1.85f, iz * 1.85f);
                    if (!WallTool.InBuildArea(Geo.X0Z(q))) return false;
                    if (Mathf.Abs(hf.HeightAt(q.x, q.y) - y0) > 1.8f) return false;
                    foreach (var h in houses) if (h.ContainsPoint(q, 0.25f)) return false;
                    if (PondAt(ponds, q) != null) return false;
                    foreach (var rv in rivers) if (rv.Covers(q, 0.3f)) return false;
                    foreach (var t in trees) if (Vector2.Distance(t.XZ, q) < 1.0f) return false;
                    if (gr && gr.NearestSeg(q, 0.9f, out _, out _)) return false;
                }
            return true;
        }

        static bool FindMarketSquare(Vector2 c, List<HouseEntity> houses, List<PondEntity> ponds, List<RiverEntity> rivers, List<PathEntity> paths, List<TreeEntity> trees, WallGraph gr, out Vector2 best)
        {
            for (int ring = 0; ring <= 9; ring++)
            {
                int steps = Mathf.Max(1, ring * 8);
                for (int k = 0; k < steps; k++)
                {
                    float a = k * Mathf.PI * 2f / steps;
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ring * 1.8f);
                    if (MarketSquareFree(p, houses, ponds, rivers, paths, trees, gr))
                    {
                        best = p;
                        return true;
                    }
                }
            }
            best = default;
            return false;
        }

        void WaterWheel(Vector3 wallPt, Vector3 outDir, Vector3 tanDir, float waterY)
        {
            float g = HeightField.I.HeightAt(wallPt.x, wallPt.z);
            float hubY = Mathf.Max(g + 0.95f, waterY + 0.85f);
            var hubPos = new Vector3(wallPt.x, hubY, wallPt.z);
            var rot = Quaternion.LookRotation(tanDir, Vector3.up);

            // Giá đỡ & trục gỗ gắn từ tường nhà ra
            Box(hubPos - outDir * 0.18f, new Vector3(0.45f, 0.22f, 0.22f), Palette.Wood, rot);
            Prim(PrimitiveType.Cylinder, hubPos, new Vector3(0.18f, 0.36f, 0.18f), Palette.Ink, Quaternion.FromToRotation(Vector3.up, outDir));

            var wheel = new GameObject("WaterWheel");
            wheel.transform.SetParent(Parent, false);
            wheel.transform.SetPositionAndRotation(hubPos + outDir * 0.08f, rot);
            var sp = wheel.AddComponent<ComboSpinner>();
            sp.axis = Vector3.right;
            sp.speed = 42f;
            sp.pauseInWinter = true;

            float radius = 1.05f;
            Color darkWood = Palette.Hex("#6B4F3E");
            // 4 nan hoa chéo
            for (int i = 0; i < 4; i++)
            {
                var sRot = Quaternion.Euler(i * 45f, 0, 0);
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(bar.GetComponent<Collider>());
                bar.transform.SetParent(wheel.transform, false);
                bar.transform.SetLocalPositionAndRotation(Vector3.zero, sRot);
                bar.transform.localScale = new Vector3(0.28f, radius * 1.9f, 0.09f);
                bar.GetComponent<Renderer>().sharedMaterial = Palette.Mat(Palette.Wood);
            }
            // Vành bánh xe + 8 gàu múc nước
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                var lp = new Vector3(0, Mathf.Sin(a) * radius, Mathf.Cos(a) * radius);
                var pRot = Quaternion.Euler(-i * 45f, 0, 0);
                var paddle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(paddle.GetComponent<Collider>());
                paddle.transform.SetParent(wheel.transform, false);
                paddle.transform.SetLocalPositionAndRotation(lp, pRot);
                paddle.transform.localScale = new Vector3(0.46f, 0.22f, 0.07f);
                paddle.GetComponent<Renderer>().sharedMaterial = Palette.Mat(darkWood);

                foreach (float sx in new[] { -0.18f, 0.18f })
                {
                    var rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(rim.GetComponent<Collider>());
                    rim.transform.SetParent(wheel.transform, false);
                    rim.transform.SetLocalPositionAndRotation(lp * 0.92f + new Vector3(sx, 0, 0), pRot);
                    rim.transform.localScale = new Vector3(0.06f, 0.12f, 0.78f);
                    rim.GetComponent<Renderer>().sharedMaterial = Palette.Mat(Palette.Wood);
                }
            }
        }

        void WindmillSails(HouseEntity h)
        {
            var top = h.LocalToWorld(new Vector3(0, h.RoofTopLocal - 0.1f, h.L * 0.35f));
            var fwd = (h.LocalToWorld(Vector3.forward) - h.LocalToWorld(Vector3.zero)).normalized;
            var rot = Quaternion.LookRotation(fwd, Vector3.up);

            // Trụ đỡ và trục ngang trên mái
            Box(top + Vector3.down * 0.15f, new Vector3(0.55f, 0.65f, 0.55f), Palette.Wood, rot);
            var hubPos = top + fwd * 0.45f + Vector3.up * 0.1f;
            Prim(PrimitiveType.Cylinder, top + fwd * 0.2f + Vector3.up * 0.1f, new Vector3(0.16f, 0.35f, 0.16f), Palette.Ink, rot * Quaternion.Euler(90f, 0, 0));

            var rotor = new GameObject("WindmillRotor");
            rotor.transform.SetParent(Parent, false);
            rotor.transform.SetPositionAndRotation(hubPos, rot);
            var sp = rotor.AddComponent<ComboSpinner>();
            sp.axis = Vector3.forward;
            sp.speed = 36f;

            Part(rotor.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.28f, Palette.Ink);
            for (int i = 0; i < 4; i++)
            {
                var blade = new GameObject("Sail" + i).transform;
                blade.SetParent(rotor.transform, false);
                blade.localRotation = Quaternion.Euler(0, 0, i * 90f);
                Part(blade, PrimitiveType.Cube, new Vector3(0, 1.15f, 0), new Vector3(0.08f, 2.2f, 0.08f), Palette.Wood);
                var sail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(sail.GetComponent<Collider>());
                sail.transform.SetParent(blade, false);
                sail.transform.SetLocalPositionAndRotation(new Vector3(0.24f, 1.3f, 0.02f), Quaternion.Euler(0, 12f, 0));
                sail.transform.localScale = new Vector3(0.42f, 1.65f, 0.03f);
                sail.GetComponent<Renderer>().sharedMaterial = Palette.Mat(Palette.Cream);
            }
        }

        void MarketStalls(Vector2 c)
        {
            var hf = HeightField.I;
            Color[] awnings =
            {
                Palette.Hex("#D95B43"), Palette.Hex("#E8B84B"), Palette.Hex("#5E8A4A"), Palette.Hex("#4F86A6")
            };
            Vector2[] offsets =
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f),
                new Vector2(-1.9f,  1.5f), new Vector2(1.9f,  1.5f)
            };
            for (int i = 0; i < 4; i++)
            {
                var p = c + offsets[i];
                float g = hf.HeightAt(p.x, p.y);
                float yaw = (i < 2 ? 0f : 180f) + (i % 2 == 0 ? 8f : -8f);
                var rot = Quaternion.Euler(0, yaw, 0);
                var b = new Vector3(p.x, g, p.y);

                // 4 cột gỗ + bàn bày hàng
                foreach (var (sx, sz) in new[] { (-0.95f, -0.5f), (0.95f, -0.5f), (-0.95f, 0.5f), (0.95f, 0.5f) })
                    Box(b + rot * new Vector3(sx, 0.95f, sz), new Vector3(0.09f, 1.9f, 0.09f), Palette.Wood, rot);
                Box(b + rot * new Vector3(0, 0.78f, 0), new Vector3(2.0f, 0.1f, 1.1f), Palette.Wood, rot);

                // Hàng hoá trên sạp
                for (int k = -2; k <= 2; k++)
                {
                    var gp = b + rot * new Vector3(k * 0.36f, 0.92f, (k & 1) == 0 ? 0.12f : -0.12f);
                    if ((k + i) % 2 == 0)
                        Ball(gp, new Vector3(0.26f, 0.2f, 0.26f), awnings[(i + k + 4) % 4]);
                    else
                        Box(gp, new Vector3(0.28f, 0.18f, 0.24f), Palette.Hex("#C99A5B"), rot);
                }

                // Mái vải màu nghiêng nhẹ
                var roofRot = rot * Quaternion.Euler(12f, 0, 0);
                Box(b + rot * new Vector3(0, 1.92f, 0), new Vector3(2.25f, 0.08f, 1.45f), awnings[i], roofRot);
                Box(b + rot * new Vector3(0, 1.96f, 0), new Vector3(0.55f, 0.09f, 1.47f), Palette.Cream, roofRot);
            }
        }

        void HillShrine(Vector2 peak, Vector2 dir, float startDist)
        {
            var hf = HeightField.I;
            var nrm = new Vector2(-dir.y, dir.x);
            var rot = Quaternion.LookRotation(Geo.X0Z(dir));

            // 7 bậc đá dẫn lên đỉnh đồi
            int steps = 7;
            for (int i = 0; i < steps; i++)
            {
                float d = startDist + i * 0.65f;
                var sp = peak + dir * d;
                float g = hf.HeightAt(sp.x, sp.y);
                Box(new Vector3(sp.x, g + 0.09f, sp.y), new Vector3(1.7f, 0.18f, 0.55f), i % 2 == 0 ? Palette.StoneLight : Palette.StoneDark, rot);
            }

            // Cổng nhỏ (Torii / cổng đá đền) ở đầu bậc thang + 2 đèn đá nhỏ
            var gp = peak + dir * (startDist + steps * 0.65f + 0.3f);
            float gy = hf.HeightAt(gp.x, gp.y);
            float colW = 1.05f, colH = 2.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                var cp = gp + nrm * (colW * s);
                float cy = hf.HeightAt(cp.x, cp.y);
                Box(new Vector3(cp.x, cy + 0.15f, cp.y), new Vector3(0.38f, 0.3f, 0.38f), Palette.StoneDark, rot);
                Prim(PrimitiveType.Cylinder, new Vector3(cp.x, cy + colH * 0.5f, cp.y), new Vector3(0.22f, colH * 0.5f, 0.22f), Palette.Roof, rot);

                var lp = gp + nrm * ((colW + 0.65f) * s) + dir * 0.35f;
                float ly = hf.HeightAt(lp.x, lp.y);
                Box(new Vector3(lp.x, ly + 0.35f, lp.y), new Vector3(0.24f, 0.7f, 0.24f), Palette.StoneLight, rot);
                Box(new Vector3(lp.x, ly + 0.78f, lp.y), new Vector3(0.42f, 0.14f, 0.42f), Palette.StoneDark, rot);
            }
            Box(new Vector3(gp.x, gy + colH - 0.35f, gp.y), new Vector3(colW * 2f + 0.6f, 0.14f, 0.18f), Palette.Wood, rot);
            Box(new Vector3(gp.x, gy + colH + 0.05f, gp.y), new Vector3(colW * 2f + 0.95f, 0.2f, 0.28f), Palette.Roof, rot);
            Box(new Vector3(gp.x, gy + colH + 0.18f, gp.y), new Vector3(colW * 2f + 1.15f, 0.1f, 0.34f), Palette.StoneDark, rot);
        }

        void MooredBoat(Vector2 pos, Vector2 dir, float waterY)
        {
            var rot = Quaternion.LookRotation(Geo.X0Z(dir));
            var side = new Vector2(-dir.y, dir.x);
            var postPos = pos - side * 0.55f;
            Box(new Vector3(postPos.x, waterY + 0.25f, postPos.y), new Vector3(0.14f, 0.9f, 0.14f), Palette.Wood, rot);

            var root = new GameObject("MooredBoat");
            root.transform.SetParent(Parent, false);
            root.transform.SetPositionAndRotation(new Vector3(pos.x, waterY + 0.04f, pos.y), rot * Quaternion.Euler(0, 12f, 0));
            var bob = root.AddComponent<ComboBoatBob>();
            bob.baseY = waterY + 0.04f;
            bob.phase = Mathf.Abs(pos.x * 0.7f + pos.y * 1.1f);

            Color hull = Palette.Hex("#9A7352"), dark = Palette.Hex("#6B4F3E");
            Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.06f, 0), new Vector3(0.76f, 0.14f, 1.9f), dark);
            Part(root.transform, PrimitiveType.Cube, new Vector3(-0.34f, 0.16f, 0), new Vector3(0.08f, 0.18f, 1.75f), hull);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0.34f, 0.16f, 0), new Vector3(0.08f, 0.18f, 1.75f), hull);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.16f, 0.85f), new Vector3(0.6f, 0.18f, 0.22f), hull);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.16f, -0.85f), new Vector3(0.6f, 0.18f, 0.22f), hull);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.15f, -0.15f), new Vector3(0.66f, 0.06f, 0.28f), Palette.Wood);
        }

        void Waterfall(RiverEntity rv, List<Vector2> pts, int i0, int i1)
        {
            float w = rv.Width * 0.9f;
            Color waterFallCol = Palette.Hex("#7EC8DC");
            Color foamCol = Palette.Hex("#F2FAFD");

            for (int i = i0; i < i1; i++)
            {
                var p0 = pts[i];
                var p1 = pts[i + 1];
                float y0 = rv.WaterLevelAt(p0) + 0.05f;
                float y1 = rv.WaterLevelAt(p1) + 0.05f;
                var a = new Vector3(p0.x, y0, p0.y);
                var b = new Vector3(p1.x, y1, p1.y);
                var diff = b - a;
                float len = diff.magnitude;
                if (len < 0.05f) continue;
                var mid = (a + b) * 0.5f;
                var rot = Quaternion.LookRotation(diff.normalized, Vector3.up);

                // Dải thác nước + vệt bọt trắng
                Box(mid, new Vector3(w, 0.14f, len + 0.18f), waterFallCol, rot);
                for (int s = -1; s <= 1; s++)
                    Box(mid + rot * new Vector3(s * w * 0.28f, 0.05f, 0), new Vector3(w * 0.18f, 0.12f, len + 0.1f), foamCol, rot);

                var nrm = new Vector2(-(p1.y - p0.y), p1.x - p0.x).normalized;
                foreach (float side in new[] { -1f, 1f })
                {
                    var rp = (p0 + p1) * 0.5f + nrm * (side * (w * 0.5f + 0.25f));
                    Ball(new Vector3(rp.x, (y0 + y1) * 0.5f, rp.y), new Vector3(0.55f, 0.45f, 0.55f), Palette.StoneDark);
                }
            }

            // Bọt trắng sủi ở chân thác
            var foot = pts[i1];
            var prev = pts[Mathf.Max(0, i1 - 1)];
            var dir = (foot - prev).normalized;
            var nrmF = new Vector2(-dir.y, dir.x);
            float fy = rv.WaterLevelAt(foot) + 0.08f;
            for (int k = -3; k <= 3; k++)
            {
                float off = k * (w * 0.16f);
                var fp = foot + nrmF * off + dir * ((Mathf.Abs(k) % 2) * 0.2f);
                float sz = 0.36f + (3 - Mathf.Abs(k)) * 0.08f;
                Ball(new Vector3(fp.x, fy, fp.y), new Vector3(sz * 1.2f, sz * 0.65f, sz * 1.2f), foamCol);
            }
        }
    }

    public class ComboSpinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float speed = 40f;
        public bool pauseInWinter;

        void Update()
        {
            if (pauseInWinter && Seasons.Snow > 0.5f) return;
            transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
        }
    }

    public class ComboBoatBob : MonoBehaviour
    {
        public float baseY;
        public float phase;
        Renderer[] rends;

        void Update()
        {
            if (rends == null) rends = GetComponentsInChildren<Renderer>();
            bool show = Seasons.Snow < 0.5f;
            if (rends.Length > 0 && rends[0].enabled != show)
                foreach (var r in rends) if (r) r.enabled = show;
            if (!show) return;
            float t = Time.time + phase;
            var p = transform.localPosition;
            p.y = baseY + Mathf.Sin(t * 2.1f) * 0.025f;
            transform.localPosition = p;
        }
    }
}
