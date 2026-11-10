using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace LangGio
{
    [Serializable]
    public class GNode
    {
        public int id;
        public float x, z;
        public bool manual;       // người chơi đặt / chỉnh tay → game không tự xoá (R7, R8)
        public int level = -1;    // -1 = để quy tắc tự quyết; 0..3 = cố định số tầng tháp
        public int roof;          // 0 nón · 1 bằng răng cưa · 2 chòi gỗ
    }

    [Serializable]
    public class GSeg
    {
        public int id, a, b;                       // 2 nút đầu / cuối (a == b: vòng kín)
        public List<Vector2> mid = new();          // điểm giữa (x, z), không gồm 2 đầu
        public int heightIdx = GameConfig.WallHeightDefault;
        public int style;                          // 0 đá có lỗ châu mai · 1 đá trơn · 2 hàng rào gỗ · 3 bờ rào thấp
    }

    [Serializable]
    public class WallGraphParams
    {
        public List<GNode> nodes = new();
        public List<GSeg> segs = new();
        public int nextId = 1;
    }

    /// <summary>
    /// Toàn bộ tường thành của làng: đồ thị Nút–Đoạn. Là 1 Entity duy nhất; mỗi nút / đoạn là 1 "phần" (part)
    /// chọn và sửa riêng được. Chỉ phần nào đổi mới sinh lại hình (so sánh "chữ ký").
    /// Quy tắc tự động R1–R8: xem Docs/CoChe-Gameplay.md, mục C.4.
    /// </summary>
    public class WallGraph : Entity<WallGraphParams>
    {
        public static WallGraph I;

        public static readonly string[] StyleNames = { "Đá có lỗ châu mai", "Đá trơn", "Hàng rào gỗ", "Bờ rào thấp" };
        static readonly string[] RoofNames = { "Nón", "Bằng răng cưa", "Chòi gỗ" };
        static readonly Color[] StyleColors = { Palette.Stone, Palette.StoneLight, Palette.Wood, Palette.StoneDark };

        // ---- Trạng thái lúc chạy (không lưu) ----
        readonly Dictionary<int, int> eff = new();            // nút → số tầng tháp thực tế (0 = không tháp)
        readonly Dictionary<int, GameObject> objs = new();    // part → hình
        readonly Dictionary<int, string> sigs = new();        // part → chữ ký lần dựng trước
        readonly Dictionary<int, Color> tints = new();
        readonly Dictionary<int, float> nodeTop = new(), nodeBody = new();
        readonly Dictionary<int, Vector3> segMid = new();     // đoạn → điểm giữa trên đỉnh tường
        readonly HashSet<int> force = new();                  // part buộc dựng lại (địa hình đổi)
        readonly HashSet<int> quiet = new();                  // part mới nhưng không nảy (do tách đoạn cũ)
        static MaterialPropertyBlock gmpb;

        WallGraphParams startP;
        bool dragInit;
        Vector2 dragRef;
        int startLevel, draggedNode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        void Awake() => I = this;

        public override string TypeName => "WallGraph";
        public override bool CanDuplicate => false;
        public override bool CanRotate => false;

        // ================= Part id =================
        public static int NodePart(int id) => id * 2;
        public static int SegPart(int id) => id * 2 + 1;
        public static bool IsNodePart(int p) => p >= 0 && p % 2 == 0;
        public static bool IsSegPart(int p) => p >= 0 && p % 2 == 1;
        public static int IdOf(int p) => p / 2;

        // ================= Tra cứu trên dữ liệu =================
        public static GNode FindNode(WallGraphParams g, int id)
        {
            foreach (var n in g.nodes) if (n.id == id) return n;
            return null;
        }

        public static GSeg FindSeg(WallGraphParams g, int id)
        {
            foreach (var s in g.segs) if (s.id == id) return s;
            return null;
        }

        public static Vector2 P2(GNode n) => new Vector2(n.x, n.z);

        public static List<Vector2> Poly(WallGraphParams g, GSeg s)
        {
            var l = new List<Vector2>(s.mid.Count + 2);
            var a = FindNode(g, s.a);
            var b = FindNode(g, s.b);
            if (a != null) l.Add(P2(a));
            l.AddRange(s.mid);
            if (b != null) l.Add(P2(b));
            return l;
        }

        public static float Len(List<Vector2> p)
        {
            float l = 0;
            for (int i = 1; i < p.Count; i++) l += Vector2.Distance(p[i - 1], p[i]);
            return l;
        }

        static int Degree(WallGraphParams g, int id)
        {
            int d = 0;
            foreach (var s in g.segs) { if (s.a == id) d++; if (s.b == id) d++; }
            return d;
        }

        static int NewNode(WallGraphParams g, Vector2 p, bool manual = false, int level = -1)
        {
            var n = new GNode { id = g.nextId++, x = p.x, z = p.y, manual = manual, level = level };
            g.nodes.Add(n);
            return n.id;
        }

        static GSeg NewSeg(WallGraphParams g, int a, int b, List<Vector2> mid, int heightIdx, int style)
        {
            var s = new GSeg { id = g.nextId++, a = a, b = b, mid = mid, heightIdx = heightIdx, style = style };
            g.segs.Add(s);
            return s;
        }

        static WallGraphParams Clone(WallGraphParams g) => JsonUtility.FromJson<WallGraphParams>(JsonUtility.ToJson(g));

        static void Closest(List<Vector2> poly, Vector2 p, out int k, out Vector2 q, out float dist)
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
        }

        static Vector2 PointAt(List<Vector2> poly, float distance)
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

        static bool SegX(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, out Vector2 x)
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

        // ================= Thao tác trên đồ thị =================
        /// <summary>Tách đoạn s tại điểm gần x nhất. useNode &gt; 0: dùng nút có sẵn. Trả về id nút ở chỗ tách.</summary>
        static int Split(WallGraphParams g, GSeg s, Vector2 x, int useNode, out GSeg right, HashSet<int> quietOut)
        {
            var poly = Poly(g, s);
            Closest(poly, x, out int k, out var q, out _);
            int nid = useNode > 0 ? useNode : NewNode(g, q);
            var qn = P2(FindNode(g, nid));

            var leftMid = new List<Vector2>();
            for (int i = 1; i <= k; i++) leftMid.Add(poly[i]);
            var rightMid = new List<Vector2>();
            for (int i = k + 1; i < poly.Count - 1; i++) rightMid.Add(poly[i]);
            if (leftMid.Count > 0 && Vector2.Distance(leftMid[leftMid.Count - 1], qn) < 0.3f) leftMid.RemoveAt(leftMid.Count - 1);
            if (rightMid.Count > 0 && Vector2.Distance(rightMid[0], qn) < 0.3f) rightMid.RemoveAt(0);

            int oldB = s.b;
            s.b = nid;
            s.mid = leftMid;
            right = NewSeg(g, nid, oldB, rightMid, s.heightIdx, s.style);
            quietOut?.Add(SegPart(right.id));
            return nid;
        }

        /// <summary>Dời nút; điểm giữa của các đoạn nối vào uốn theo (càng gần nút càng dời nhiều).</summary>
        static void MoveNode(WallGraphParams g, int id, Vector2 np)
        {
            var n = FindNode(g, id);
            if (n == null) return;
            var d = np - P2(n);
            if (d.sqrMagnitude < 1e-10f) return;
            foreach (var s in g.segs)
            {
                if (s.a != id && s.b != id) continue;
                var poly = Poly(g, s);
                float total = Len(poly);
                if (total < 1e-4f) continue;
                float acc = 0;
                for (int i = 1; i < poly.Count - 1; i++)
                {
                    acc += Vector2.Distance(poly[i - 1], poly[i]);
                    float t = acc / total;
                    var off = Vector2.zero;
                    if (s.a == id) off += d * (1f - t);
                    if (s.b == id) off += d * t;
                    s.mid[i - 1] += off;
                }
            }
            n.x = np.x;
            n.z = np.y;
        }

        static void Merge(WallGraphParams g, int keep, int drop)
        {
            if (keep == drop) return;
            foreach (var s in g.segs) { if (s.a == drop) s.a = keep; if (s.b == drop) s.b = keep; }
            g.nodes.RemoveAll(n => n.id == drop);
        }

        /// <summary>Nối 2 đoạn đi qua nút thành 1 đoạn rồi bỏ nút.</summary>
        static bool Join(WallGraphParams g, int nodeId)
        {
            GSeg s1 = null, s2 = null;
            foreach (var s in g.segs)
            {
                if (s.a != nodeId && s.b != nodeId) continue;
                if (s.a == s.b) return false;
                if (s1 == null) s1 = s; else if (s2 == null) s2 = s; else return false;
            }
            if (s1 == null || s2 == null) return false;

            var p1 = Poly(g, s1); int start = s1.a;
            if (s1.a == nodeId) { p1.Reverse(); start = s1.b; }      // p1 kết thúc ở nút
            var p2 = Poly(g, s2); int end = s2.b;
            if (s2.b == nodeId) { p2.Reverse(); end = s2.a; }        // p2 bắt đầu ở nút

            var mid = new List<Vector2>();
            for (int i = 1; i < p1.Count; i++) mid.Add(p1[i]);       // gồm cả điểm nút
            for (int i = 1; i < p2.Count - 1; i++) mid.Add(p2[i]);
            s1.a = start; s1.b = end; s1.mid = mid;
            g.segs.Remove(s2);
            g.nodes.RemoveAll(n => n.id == nodeId);
            return true;
        }

        /// <summary>Dọn dẹp sau mỗi thay đổi cấu trúc: R5 (đoạn quá dài), R7 (nút mồ côi), đoạn hỏng.</summary>
        static void Normalize(WallGraphParams g, HashSet<int> quietOut)
        {
            g.segs.RemoveAll(s => FindNode(g, s.a) == null || FindNode(g, s.b) == null);
            g.segs.RemoveAll(s => Len(Poly(g, s)) < 0.6f || (s.a == s.b && s.mid.Count < 3));

            for (int i = 0; i < g.segs.Count; i++)
            {
                var s = g.segs[i];
                float len = Len(Poly(g, s));
                if (len <= GameConfig.WallMaxSegment) continue;
                int pieces = Mathf.CeilToInt(len / GameConfig.WallMaxSegment);
                float step = len / pieces;
                var cur = s;
                for (int k = 1; k < pieces; k++)
                {
                    var pt = PointAt(Poly(g, cur), step);
                    int nid = Split(g, cur, pt, 0, out var right, quietOut);
                    FindNode(g, nid).level = 1;
                    cur = right;
                }
            }

            // R7: nút không còn đoạn nào (trừ tháp đứng riêng do người chơi đặt)
            g.nodes.RemoveAll(n => (!n.manual || n.level == 0) && Degree(g, n.id) == 0);
        }

        /// <summary>Tìm chỗ các đoạn mới cắt nhau / cắt đoạn cũ → chèn nút chung (ngã tư → có tháp).</summary>
        void Intersect(List<int> newSegs)
        {
            var g = P;
            for (int iter = 0; iter < 64; iter++)
            {
                bool found = false;
                foreach (int sid in newSegs.ToArray())
                {
                    var s = FindSeg(g, sid);
                    if (s == null) continue;
                    var pa = Poly(g, s);
                    foreach (var o in g.segs.ToArray())
                    {
                        if (o.id == sid) continue;
                        var pb = Poly(g, o);
                        for (int i = 0; i < pa.Count - 1 && !found; i++)
                            for (int j = 0; j < pb.Count - 1 && !found; j++)
                            {
                                if (!SegX(pa[i], pa[i + 1], pb[j], pb[j + 1], out var x)) continue;
                                if (NearEnd(g, s, x) || NearEnd(g, o, x)) continue;
                                int nid = Split(g, s, x, 0, out var r1, null);
                                newSegs.Add(r1.id);
                                bool oIsNew = newSegs.Contains(o.id);
                                Split(g, o, x, nid, out var r2, oIsNew ? null : quiet);
                                if (oIsNew) newSegs.Add(r2.id);
                                found = true;
                            }
                        if (found) break;
                    }
                    if (found) break;
                }
                if (!found) return;
            }
        }

        static bool NearEnd(WallGraphParams g, GSeg s, Vector2 x)
        {
            var a = FindNode(g, s.a); var b = FindNode(g, s.b);
            return (a != null && Vector2.Distance(P2(a), x) < GameConfig.IntersectEndGap)
                || (b != null && Vector2.Distance(P2(b), x) < GameConfig.IntersectEndGap);
        }

        // ================= API cho công cụ =================
        public int LevelOf(GNode n) => n != null && eff.TryGetValue(n.id, out var l) ? l : 0;
        public static float TowerRadius(int level) => GameConfig.TowerRadius[Mathf.Clamp(level, 1, 3)];

        public GNode NearestNode(Vector2 p, float r, int except = 0)
        {
            GNode best = null; float bd = r;
            foreach (var n in P.nodes)
            {
                if (n.id == except) continue;
                float d = Vector2.Distance(P2(n), p);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        public GNode NearestTower(Vector2 p, float r)
        {
            GNode best = null; float bd = r;
            foreach (var n in P.nodes)
            {
                if (LevelOf(n) <= 0) continue;
                float d = Vector2.Distance(P2(n), p);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        public bool NearestSeg(Vector2 p, float r, out GSeg seg, out Vector2 q)
        {
            seg = null; q = default; float bd = r;
            foreach (var s in P.segs)
            {
                Closest(Poly(P, s), p, out _, out var c, out float d);
                if (d < bd) { bd = d; seg = s; q = c; }
            }
            return seg != null;
        }

        public IEnumerable<(Vector2 pos, float radius)> Towers()
        {
            foreach (var n in P.nodes)
            {
                int L = LevelOf(n);
                if (L > 0) yield return (P2(n), TowerRadius(L));
            }
        }

        public IEnumerable<Vector2> WallSamples(float step)
        {
            foreach (var s in P.segs)
            {
                var poly = Poly(P, s);
                float len = Len(poly);
                for (float d = 0; d <= len; d += step) yield return PointAt(poly, d);
            }
        }

        /// <summary>Điểm đầu/cuối nét vẽ: hút vào nút (1.8 m) → hút vào thân tường (1.2 m, tách đoạn) → tạo nút mới.</summary>
        public int SnapOrCreate(Vector2 p)
        {
            var n = NearestNode(p, GameConfig.TowerSnap);
            if (n != null) return n.id;
            if (NearestSeg(p, GameConfig.SegmentSnap, out var s, out var q))
                return Split(P, s, q, 0, out _, quiet);
            return NewNode(P, p);
        }

        public int SplitAt(GSeg s, Vector2 q) => Split(P, s, q, 0, out _, quiet);
        public int AddNode(Vector2 p, bool manual, int level) => NewNode(P, p, manual, level);
        public GNode GetNode(int id) => FindNode(P, id);

        /// <summary>Thêm 1 nét tường đã xử lý (đường mượt + các góc) vào đồ thị.</summary>
        public void AddStroke(List<Vector3> path, List<Vector3> corners, bool closed, int heightIdx, int style)
        {
            int n = path.Count;
            var pts = new List<Vector2>(n);
            foreach (var p in path) pts.Add(new Vector2(p.x, p.z));

            int startId = SnapOrCreate(pts[0]);
            int endId = closed ? startId : SnapOrCreate(pts[n - 1]);

            // Vị trí các góc trên đường (chỗ cắt nét)
            var cuts = new List<int> { 0 };
            foreach (var c in corners)
            {
                var c2 = new Vector2(c.x, c.z);
                for (int i = 1; i < n - 1; i++)
                    if (Vector2.Distance(pts[i], c2) < 0.05f) { cuts.Add(i); break; }
            }
            cuts.Sort();
            cuts.Add(n - 1);

            var ids = new List<int>();
            for (int k = 0; k < cuts.Count; k++)
            {
                if (k == 0) ids.Add(startId);
                else if (k == cuts.Count - 1) ids.Add(endId);
                else
                {
                    var near = NearestNode(pts[cuts[k]], GameConfig.TowerSnap);
                    ids.Add(near != null ? near.id : NewNode(P, pts[cuts[k]]));
                }
            }

            var newSegs = new List<int>();
            for (int k = 0; k < cuts.Count - 1; k++)
            {
                if (cuts[k + 1] <= cuts[k]) continue;
                var mid = new List<Vector2>();
                for (int i = cuts[k] + 1; i < cuts[k + 1]; i++) mid.Add(pts[i]);
                newSegs.Add(NewSeg(P, ids[k], ids[k + 1], mid, heightIdx, style).id);
            }

            Intersect(newSegs);
            Commit();
        }

        /// <summary>Dọn dẹp (R5, R7) rồi dựng lại hình.</summary>
        public void Commit()
        {
            Normalize(P, quiet);
            Rebuild();
        }

        // ================= Quy tắc tháp R1–R4, R6, R8 =================
        void ComputeLevels()
        {
            eff.Clear();
            var dirs = new Dictionary<int, List<Vector2>>();
            foreach (var s in P.segs)
            {
                var poly = Poly(P, s);
                if (poly.Count < 2) continue;
                Add(dirs, s.a, (poly[1] - poly[0]).normalized);
                Add(dirs, s.b, (poly[poly.Count - 2] - poly[poly.Count - 1]).normalized);
            }

            var deg = new Dictionary<int, int>();
            foreach (var n in P.nodes)
            {
                int d = dirs.TryGetValue(n.id, out var l) ? l.Count : 0;
                deg[n.id] = d;
                int L;
                if (n.level >= 0) L = Mathf.Clamp(n.level, 0, 3);                       // R8: chỉnh tay
                else if (d <= 1) L = 1;                                                // R1 (và tháp đứng riêng)
                else if (d == 2)
                {
                    float turn = 180f - Vector2.Angle(l[0], l[1]);
                    L = turn > GameConfig.CornerAngle ? 1 : 0;                         // R2 / R3
                }
                else L = 1;                                                            // R4
                eff[n.id] = L;
            }

            // R6: 2 tháp tự sinh quá gần → bỏ 1
            for (int i = 0; i < P.nodes.Count; i++)
                for (int j = i + 1; j < P.nodes.Count; j++)
                {
                    var a = P.nodes[i]; var b = P.nodes[j];
                    if (eff[a.id] <= 0 || eff[b.id] <= 0) continue;
                    if (Vector2.Distance(P2(a), P2(b)) >= GameConfig.TowerMinSpacing) continue;
                    bool aAuto = a.level < 0, bAuto = b.level < 0;
                    if (!aAuto && !bAuto) continue;
                    GNode loser;
                    if (aAuto && !bAuto) loser = a;
                    else if (!aAuto) loser = b;
                    else loser = deg[a.id] < deg[b.id] ? a : deg[b.id] < deg[a.id] ? b : (a.id > b.id ? a : b);
                    eff[loser.id] = 0;
                }

            static void Add(Dictionary<int, List<Vector2>> d, int id, Vector2 v)
            {
                if (!d.TryGetValue(id, out var l)) d[id] = l = new List<Vector2>();
                l.Add(v);
            }
        }

        // ================= Dựng hình (chỉ phần thay đổi) =================
        public override void Rebuild()
        {
            if (!visual) NewVisual();
            ComputeLevels();
            var alive = new HashSet<int>();

            foreach (var n in P.nodes)
            {
                int part = NodePart(n.id);
                alive.Add(part);
                string sig = NodeSig(n);
                bool isNew = !objs.ContainsKey(part);
                if (isNew || force.Contains(part) || sigs[part] != sig)
                {
                    BuildNode(n, part);
                    TouchPart(part, new Rect(n.x - 3f, n.z - 3f, 6f, 6f));
                    sigs[part] = sig;
                    if (isNew && !quiet.Contains(part)) PopIn.Add(objs[part], 0.15f, false, 0.4f);
                }
            }

            foreach (var s in P.segs)
            {
                int part = SegPart(s.id);
                alive.Add(part);
                string sig = SegSig(s);
                bool isNew = !objs.ContainsKey(part);
                if (isNew || force.Contains(part) || sigs[part] != sig)
                {
                    BuildSeg(s, part);
                    TouchPart(part, PolyRect(Poly(P, s), 2f));
                    sigs[part] = sig;
                    if (isNew && !quiet.Contains(part)) PopSeg(objs[part]);
                }
            }

            foreach (var key in new List<int>(objs.Keys))
            {
                if (alive.Contains(key)) continue;
                DestroyPart(key);
                objs.Remove(key);
                sigs.Remove(key);
                tints.Remove(key);
                if (partRect.TryGetValue(key, out var old)) { Touch(old); partRect.Remove(key); }
            }

            force.Clear();
            quiet.Clear();

            // Phần tường đổi → cây ở đúng vùng đó kiểm tra lại có bị che không
            if (changedAny)
            {
                changedAny = false;
                EntityManager.NotifyTrees(changedBox);
            }
        }

        bool changedAny;
        Rect changedBox;
        readonly Dictionary<int, Rect> partRect = new();

        void Touch(Rect r)
        {
            changedBox = changedAny ? Union(changedBox, r) : r;
            changedAny = true;
        }

        void TouchPart(int part, Rect newRect)
        {
            if (partRect.TryGetValue(part, out var old)) Touch(old);
            partRect[part] = newRect;
            Touch(newRect);
        }

        static Rect PolyRect(List<Vector2> poly, float pad)
        {
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            foreach (var p in poly) { x0 = Mathf.Min(x0, p.x); z0 = Mathf.Min(z0, p.y); x1 = Mathf.Max(x1, p.x); z1 = Mathf.Max(z1, p.y); }
            return x0 > x1 ? new Rect(0, 0, 0, 0) : Rect.MinMaxRect(x0 - pad, z0 - pad, x1 + pad, z1 + pad);
        }

        static void PopSeg(GameObject go)
        {
            var body = go.transform.Find("Body");
            if (body) PopIn.Add(body.gameObject, 0f, true, 0.3f);
            int k = 0;
            foreach (Transform c in go.transform)
                if (c.name == "Merlon" || c.name == "Stake")
                    PopIn.Add(c.gameObject, 0.25f + 0.03f * k++, false, 0.3f);
        }

        static string F(float v) => v.ToString("F2", CultureInfo.InvariantCulture);

        string NodeSig(GNode n) => $"{F(n.x)},{F(n.z)},{eff[n.id]},{n.roof}";

        string SegSig(GSeg s)
        {
            var sb = new StringBuilder();
            var poly = Poly(P, s);
            foreach (var p in poly) sb.Append(F(p.x)).Append(',').Append(F(p.y)).Append(';');
            sb.Append('|').Append(s.heightIdx).Append(',').Append(s.style);
            sb.Append('|').Append(eff.TryGetValue(s.a, out var la) ? la : 0).Append(',').Append(eff.TryGetValue(s.b, out var lb) ? lb : 0);
            sb.Append('|').Append(Degree(P, s.a)).Append(',').Append(Degree(P, s.b));
            foreach (var t in TowersNear(poly)) sb.Append('|').Append(F(t.x)).Append(',').Append(F(t.y));
            return sb.ToString();
        }

        List<Vector2> TowersNear(List<Vector2> poly)
        {
            var res = new List<Vector2>();
            if (poly.Count == 0) return res;
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            foreach (var p in poly) { x0 = Mathf.Min(x0, p.x); z0 = Mathf.Min(z0, p.y); x1 = Mathf.Max(x1, p.x); z1 = Mathf.Max(z1, p.y); }
            var box = Rect.MinMaxRect(x0 - 3f, z0 - 3f, x1 + 3f, z1 + 3f);
            foreach (var n in P.nodes)
                if (eff.TryGetValue(n.id, out var L) && L > 0 && box.Contains(P2(n))) res.Add(P2(n));
            return res;
        }

        void DestroyPart(int part)
        {
            if (!objs.TryGetValue(part, out var go) || !go) return;
            go.SetActive(false);
            Destroy(go);
        }

        GameObject NewPartObject(string name, int part)
        {
            DestroyPart(part);
            var go = new GameObject(name);
            go.transform.SetParent(visual.transform, false);
            go.AddComponent<PartTag>().part = part;
            objs[part] = go;
            return go;
        }

        void BuildNode(GNode n, int part)
        {
            var go = NewPartObject("N" + n.id, part);
            var hf = HeightField.I;
            float g = hf.HeightAt(n.x, n.z);
            go.transform.position = new Vector3(n.x, g, n.z);
            int L = eff[n.id];
            if (L > 0) BuildTower(go.transform, n, L, g);
            else
            {
                // Nút không có tháp: vô hình, chỉ có vùng bấm để chọn / kéo
                var sc = go.AddComponent<SphereCollider>();
                sc.center = new Vector3(0, 1.3f, 0);
                sc.radius = 0.8f;
                nodeBody[n.id] = GameConfig.WallHeights[GameConfig.WallHeightDefault];
                nodeTop[n.id] = nodeBody[n.id];
            }
            ApplyPartTint(part);
        }

        void BuildTower(Transform t, GNode n, int L, float g)
        {
            var hf = HeightField.I;
            float r = TowerRadius(L);
            float gmin = g;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                gmin = Mathf.Min(gmin, hf.HeightAt(n.x + Mathf.Cos(a) * r, n.z + Mathf.Sin(a) * r));
            }

            float hgt = GameConfig.WallHeights[GameConfig.WallHeightDefault] + GameConfig.TowerExtra[L];
            float bottom = gmin - g - 0.6f;
            nodeBody[n.id] = hgt;

            Prim(PrimitiveType.Cylinder, t, new Vector3(0, (hgt + bottom) * 0.5f, 0),
                 new Vector3(r * 2f, (hgt - bottom) * 0.5f, r * 2f), Palette.StoneLight, keepCollider: true, name: "Body").WithPattern(Palette.StoneLight, Palette.PatStone);
            Prim(PrimitiveType.Cylinder, t, new Vector3(0, hgt, 0),
                 new Vector3(r * 2f + 0.3f, 0.18f, r * 2f + 0.3f), Palette.StoneDark, name: "Band").WithPattern(Palette.StoneDark, Palette.PatStone);
            // chân tháp loe ra + vành đá đỡ dưới đỉnh
            Prim(PrimitiveType.Cylinder, t, new Vector3(0, bottom * 0.5f + 0.25f, 0),
                 new Vector3(r * 2f + 0.28f, (0.5f - bottom) * 0.5f, r * 2f + 0.28f), Palette.StoneDark, name: "Footing").WithPattern(Palette.StoneDark, Palette.PatStone);
            var tdm = new DetailMesh(n.id * 977 + 13);
            int corbels = Mathf.Max(10, Mathf.RoundToInt(2f * Mathf.PI * r / 0.42f));
            for (int k = 0; k < corbels; k++)
            {
                float a = k * Mathf.PI * 2f / corbels;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                tdm.Box(dir * (r + 0.06f) + Vector3.up * (hgt - 0.28f), new Vector3(0.2f, 0.26f, 0.22f), Quaternion.LookRotation(dir), tdm.Vary(Palette.StoneDark, 0.06f));
            }

            switch (Mathf.Abs(n.roof) % 3)
            {
                case 0:
                {
                    float rh = GameConfig.TowerRoofHeight[L];
                    float R = r + 0.4f;
                    var under = Palette.Roof * 0.6f; under.a = 1f;
                    MeshObj("Roof", MeshLib.Cone(16), t, new Vector3(0, hgt + 0.1f, 0), new Vector3(R, rh, R), under);
                    ConeTiles(tdm, new Vector3(0, hgt + 0.1f, 0), R, rh, TowerRoofTile);
                    // chóp nhọn + quả cầu
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0, hgt + 0.1f + rh + 0.3f, 0), new Vector3(0.07f, 0.4f, 0.07f), Palette.Ink, name: "Spire");
                    Prim(PrimitiveType.Sphere, t, new Vector3(0, hgt + 0.1f + rh + 0.05f, 0), new Vector3(0.2f, 0.2f, 0.2f), Palette.Hex("#C9A13B"), name: "Finial");
                    nodeTop[n.id] = hgt + 0.1f + rh + 0.7f;
                    break;
                }
                case 1:
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0, hgt + 0.15f, 0),
                         new Vector3(r * 2f + 0.3f, 0.15f, r * 2f + 0.3f), Palette.StoneDark, name: "Deck");
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    {
                        tdm.Box(dir * (r + 0.05f) + Vector3.up * (hgt + 0.55f), new Vector3(0.5f, 0.55f, 0.5f), Quaternion.LookRotation(dir), tdm.Vary(Palette.Stone, 0.06f));
                        tdm.Box(dir * (r + 0.07f) + Vector3.up * (hgt + 0.86f), new Vector3(0.6f, 0.07f, 0.6f), Quaternion.LookRotation(dir), tdm.Vary(Palette.StoneDark, 0.05f));
                    }
                    }
                    nodeTop[n.id] = hgt + 0.85f;
                    break;
                default:
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k * Mathf.PI / 2f + Mathf.PI / 4f;
                        var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (r * 0.75f);
                        Prim(PrimitiveType.Cylinder, t, p + Vector3.up * (hgt + 0.7f), new Vector3(0.18f, 0.6f, 0.18f), Palette.Wood, name: "Post");
                    }
                    MeshObj("Roof", MeshLib.Cone(4), t, new Vector3(0, hgt + 1.3f, 0), new Vector3(r + 0.45f, 1.5f, r + 0.45f),
                            Palette.Wood, Quaternion.Euler(0, 45f, 0));
                    nodeTop[n.id] = hgt + 2.8f;
                    break;
            }

            for (int f = 0; ; f++)
            {
                float y = 1.4f + f * 2f;
                if (y > hgt - 0.9f) break;
                float a = f * Mathf.PI / 4f + Mathf.PI / 4f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                // cửa sổ hẹp có vòm, khung đá + bậu
                var q = Quaternion.LookRotation(dir);
                var win = Prim(PrimitiveType.Cube, t, dir * (r - 0.01f) + Vector3.up * y, new Vector3(0.3f, 0.6f, 0.14f),
                     Palette.Ink, localRot: q, name: "Window");
                win.GetComponent<Renderer>().sharedMaterial = Palette.WindowMat();
                var arch = Prim(PrimitiveType.Cylinder, t, dir * (r - 0.01f) + Vector3.up * (y + 0.3f), new Vector3(0.3f, 0.07f, 0.3f),
                     Palette.Ink, localRot: q * Quaternion.Euler(90f, 0, 0), name: "Window");
                arch.GetComponent<Renderer>().sharedMaterial = Palette.WindowMat();
                tdm.Box(dir * (r + 0.02f) + Vector3.up * (y - 0.36f), new Vector3(0.5f, 0.08f, 0.16f), q, tdm.Vary(Palette.StoneDark, 0.04f));
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    tdm.Box(dir * (r + 0.01f) + q * Vector3.right * (sgn * 0.21f) + Vector3.up * (y + 0.05f), new Vector3(0.1f, 0.78f, 0.1f), q, tdm.Vary(Palette.StoneDark, 0.04f));
            }
            tdm.Build("TowerDetail", t, true, Palette.VertexColorMat(Palette.PatStone));
        }

        static readonly Color TowerRoofTile = Palette.Hex("#B8573F");

        /// <summary>Lợp ngói mái nón: từng vòng so le, viên ngói nghiêng theo mặt nón.</summary>
        internal static void ConeTiles(DetailMesh dm, Vector3 baseCenter, float R, float H, Color col)
        {
            float slant = Mathf.Sqrt(R * R + H * H);
            int rows = Mathf.Max(3, Mathf.CeilToInt(slant / 0.3f));
            float rowLen = slant / rows;
            for (int row = 0; row < rows - 1; row++)
            {
                float t0 = (row + 0.5f) / rows;
                float rho = R * (1f - t0) + 0.04f;
                float y = H * t0;
                int count = Mathf.Max(5, Mathf.RoundToInt(2f * Mathf.PI * rho / 0.28f));
                float off = row % 2 == 0 ? 0f : 0.5f;
                float tw = 2f * Mathf.PI * rho / count;
                for (int k = 0; k < count; k++)
                {
                    float a = (k + off) * Mathf.PI * 2f / count;
                    var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var upSlope = (-radial * R + Vector3.up * H).normalized;
                    var normal = (radial * H + Vector3.up * R).normalized;
                    var rot = Quaternion.LookRotation(upSlope, normal) * Quaternion.Euler(6f + dm.Range(-1.5f, 1.5f), dm.Range(-2f, 2f), 0);
                    var p = baseCenter + radial * rho + Vector3.up * y + normal * 0.03f;
                    dm.Box(p, new Vector3(tw * 0.95f, 0.04f, rowLen * 1.25f), rot, dm.Vary(col, 0.07f), skipBottom: true, skipBack: true);
                }
            }
        }

        float SegHeight(GSeg s) => s.style == 3
            ? GameConfig.LowWallHeight
            : GameConfig.WallHeights[Mathf.Clamp(s.heightIdx, 0, GameConfig.WallHeights.Length - 1)];

        void BuildSeg(GSeg s, int part)
        {
            var go = NewPartObject("S" + s.id, part);
            var poly2 = Poly(P, s);
            if (poly2.Count < 2) return;

            int style = Mathf.Abs(s.style) % 4;
            float thick = GameConfig.WallStyleThickness[style];
            float H = SegHeight(s);
            bool closed = s.a == s.b;

            var poly = new List<Vector3>(poly2.Count);
            foreach (var p in poly2) poly.Add(new Vector3(p.x, 0, p.y));
            if (closed && poly.Count > 3) poly.RemoveAt(poly.Count - 1);

            // Nút không tháp mà có nhiều đoạn: kéo dài đầu đoạn 1 chút để 2 đoạn liền nhau
            if (!closed)
            {
                int la = eff.TryGetValue(s.a, out var x1) ? x1 : 0, lb = eff.TryGetValue(s.b, out var x2) ? x2 : 0;
                if (la == 0 && Degree(P, s.a) >= 2) poly[0] += (poly[0] - poly[1]).normalized * thick * 0.5f;
                int m = poly.Count - 1;
                if (lb == 0 && Degree(P, s.b) >= 2) poly[m] += (poly[m] - poly[m - 1]).normalized * thick * 0.5f;
            }

            var path = closed ? ResampleClosed(poly, GameConfig.WallStep) : PathUtil.Resample(poly, GameConfig.WallStep);
            int n = path.Count;
            if (n < 2) return;

            var hf = HeightField.I;
            var g = new float[n];
            float minG = float.MaxValue;
            Vector3 c = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                g[i] = hf.HeightAt(path[i].x, path[i].z);
                minG = Mathf.Min(minG, g[i]);
                c += path[i];
            }
            c /= n;

            var top = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0;
                for (int k = -2; k <= 2; k++)
                {
                    int idx = closed ? (i + k + n) % n : Mathf.Clamp(i + k, 0, n - 1);
                    sum += g[idx];
                }
                top[i] = sum / 5f + H;
            }

            var origin = new Vector3(c.x, minG - 0.6f, c.z);
            go.transform.position = origin;

            var mesh = WallMesh.Build(path, closed, top, thick, origin);
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = Palette.Mat(StyleColors[style], style == 2 ? Palette.PatWood : Palette.PatStone);
            body.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Trang trí trên đỉnh: lỗ châu mai (kiểu 0) hoặc cọc gỗ nhọn (kiểu 2), tránh tháp
            var towers = TowersNear(poly2);
            int segs = closed ? n : n - 1;
            float total = 0;
            for (int i = 0; i < segs; i++) total += PathUtil.Flat(path[(i + 1) % n] - path[i]).magnitude;

            var mdm = new DetailMesh(s.id * 131 + 7);
            if (style == 0 || style == 1)
                for (int i = 0; i < segs; i++)
                {
                    int j = (i + 1) % n;
                    var a3 = new Vector3(path[i].x, top[i], path[i].z) - origin;
                    var b3 = new Vector3(path[j].x, top[j], path[j].z) - origin;
                    var d3 = b3 - a3;
                    if (d3.sqrMagnitude < 1e-6f) continue;
                    mdm.Box((a3 + b3) * 0.5f + Vector3.up * 0.03f, new Vector3(thick + 0.14f, 0.08f, d3.magnitude + 0.04f),
                            Quaternion.LookRotation(d3.normalized), mdm.Vary(Palette.StoneDark, 0.04f), skipBottom: true);
                }
            float spacing = style == 2 ? 0.45f : GameConfig.MerlonSpacing;
            float sAcc = 0, next = style == 2 ? 0.2f : 0.55f;
            bool midSet = false;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n;
                Vector3 a = path[i], b = path[j];
                float len = PathUtil.Flat(b - a).magnitude;
                if (len < 1e-4f) continue;
                Vector3 dir = PathUtil.Flat(b - a) / len;

                if (!midSet && sAcc + len >= total * 0.5f)
                {
                    float tm = (total * 0.5f - sAcc) / len;
                    var pm = Vector3.Lerp(a, b, tm);
                    segMid[s.id] = new Vector3(pm.x, Mathf.Lerp(top[i], top[j], tm), pm.z);
                    midSet = true;
                }

                while (next <= sAcc + len)
                {
                    float tt = (next - sAcc) / len;
                    Vector3 p = Vector3.Lerp(a, b, tt);
                    bool nearTower = false;
                    foreach (var tp in towers)
                        if (Vector2.Distance(tp, new Vector2(p.x, p.z)) < GameConfig.MerlonTowerGap) { nearTower = true; break; }
                    float ty = Mathf.Lerp(top[i], top[j], tt);
                    if (!nearTower && style == 0)
                    {
                        float mh = 0.5f + mdm.Range(-0.03f, 0.05f);
                        var mq = Quaternion.LookRotation(dir) * Quaternion.Euler(0, mdm.Range(-1.5f, 1.5f), 0);
                        mdm.Box(new Vector3(p.x, ty + 0.07f + mh * 0.5f, p.z) - origin, new Vector3(thick + 0.04f, mh, 0.6f), mq, mdm.Vary(Palette.Stone, 0.06f), skipBottom: true);
                        mdm.Box(new Vector3(p.x, ty + 0.07f + mh + 0.04f, p.z) - origin, new Vector3(thick + 0.14f, 0.08f, 0.68f), mq, mdm.Vary(Palette.StoneDark, 0.05f), skipBottom: true);
                    }
                    else if (!nearTower && style == 2)
                        Prim(PrimitiveType.Cube, go.transform, new Vector3(p.x, ty + 0.1f, p.z) - origin,
                             new Vector3(0.28f, 0.28f, 0.28f), Palette.Wood,
                             localRot: Quaternion.LookRotation(dir) * Quaternion.Euler(0, 0, 45f), name: "Stake");
                    next += spacing;
                }
                sAcc += len;
            }
            mdm.Build("Merlons", go.transform, true, Palette.VertexColorMat(Palette.PatStone));

            ApplyPartTint(part);
        }

        static List<Vector3> ResampleClosed(List<Vector3> loop, float step)
        {
            var l = new List<Vector3>(loop) { loop[0] };
            var r = PathUtil.Resample(l, step);
            if (r.Count > 3) r.RemoveAt(r.Count - 1);
            return r;
        }

        // ================= Tô màu từng phần =================
        public override void SetPartTint(int part, Color? c)
        {
            if (part < 0) return;
            if (c == null) tints.Remove(part); else tints[part] = c.Value;
            ApplyPartTint(part);
        }

        void ApplyPartTint(int part)
        {
            if (!objs.TryGetValue(part, out var go) || !go) return;
            gmpb ??= new MaterialPropertyBlock();
            bool has = tints.TryGetValue(part, out var t);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!has) { r.SetPropertyBlock(null); continue; }
                var col = Color.Lerp(r.sharedMaterial.color, new Color(t.r, t.g, t.b, 1f), t.a);
                gmpb.Clear();
                gmpb.SetColor("_Color", col);
                gmpb.SetColor("_BaseColor", col);
                r.SetPropertyBlock(gmpb);
            }
        }

        // ================= Cọ màu =================
        public override string PaintCategory(Collider c)
        {
            int part = PartOf(c);
            if (IsSegPart(part)) return "wallStyle";
            if (IsNodePart(part) && LevelOf(FindNode(P, IdOf(part))) > 0) return "towerRoof";
            return null;
        }

        public override int PaintCount(string cat) => cat == "wallStyle" ? 4 : 3;

        public override int PaintGet(Collider c)
        {
            int part = PartOf(c);
            if (IsSegPart(part)) { var s = FindSeg(P, IdOf(part)); return s != null ? Mathf.Abs(s.style) % 4 : 0; }
            var n = FindNode(P, IdOf(part));
            return n != null ? Mathf.Abs(n.roof) % 3 : 0;
        }

        public override void PaintSet(Collider c, int v)
        {
            int part = PartOf(c);
            if (IsSegPart(part)) { var s = FindSeg(P, IdOf(part)); if (s != null) s.style = v; }
            else if (IsNodePart(part)) { var n = FindNode(P, IdOf(part)); if (n != null) n.roof = v; }
            Rebuild();
        }

        /// <summary>Đỉnh mái tháp (độ cao thế giới) — để cắm cờ, chuông.</summary>
        public float TowerTopY(GNode n) => HeightField.I.HeightAt(n.x, n.z) + TopOf(n);
        public float TowerBodyY(GNode n) => HeightField.I.HeightAt(n.x, n.z) + (nodeBody.TryGetValue(n.id, out var b) ? b : 4f);
        public float SegmentHeight(GSeg s) => SegHeight(s);

        // ================= Part / chọn =================
        public override int PartOf(Collider c)
        {
            var tag = c.GetComponentInParent<PartTag>();
            return tag ? tag.part : -1;
        }

        public override bool HasPart(int part)
        {
            if (IsNodePart(part)) return FindNode(P, IdOf(part)) != null;
            if (IsSegPart(part)) return FindSeg(P, IdOf(part)) != null;
            return false;
        }

        public override string Describe
        {
            get
            {
                if (IsNodePart(ActivePart))
                {
                    var n = FindNode(P, IdOf(ActivePart));
                    if (n == null) return "Tường thành";
                    int L = LevelOf(n);
                    string kind = n.level >= 0 ? "chỉnh tay" : "tự sinh";
                    return L > 0
                        ? $"Tháp · {L} tầng · mái {RoofNames[Mathf.Abs(n.roof) % 3]} · {kind}"
                        : $"Chỗ nối tường (không có tháp) · {Degree(P, n.id)} đoạn";
                }
                if (IsSegPart(ActivePart))
                {
                    var s = FindSeg(P, IdOf(ActivePart));
                    if (s == null) return "Tường thành";
                    return $"Đoạn tường · {Len(Poly(P, s)):0} m · {StyleNames[Mathf.Abs(s.style) % 4]} · cao {SegHeight(s):0.0} m";
                }
                return "Tường thành";
            }
        }

        public override bool DeletePart(int part)
        {
            if (IsSegPart(part))
            {
                P.segs.RemoveAll(s => s.id == IdOf(part));
                Commit();
                return true;
            }
            if (IsNodePart(part))
            {
                var n = FindNode(P, IdOf(part));
                if (n == null) return true;
                int d = Degree(P, n.id);
                if (d == 0) P.nodes.Remove(n);
                else if (d == 2 && (n.manual || LevelOf(n) == 0) && Join(P, n.id)) { }   // tháp tay / chỗ nối: nối liền 2 đoạn
                else { n.level = 0; n.manual = true; }                                     // tháp tự sinh: bỏ tháp, giữ chỗ nối
                Commit();
                return true;
            }
            return true;   // không bao giờ xoá cả đồ thị
        }

        // ================= Di chuyển (kéo thân) =================
        public override void Translate(Vector3 d)
        {
            var d2 = new Vector2(d.x, d.z);
            if (IsNodePart(ActivePart))
            {
                var n = FindNode(P, IdOf(ActivePart));
                if (n == null) return;
                MoveNode(P, n.id, P2(n) + d2);
                draggedNode = n.id;
            }
            else if (IsSegPart(ActivePart))
            {
                var s = FindSeg(P, IdOf(ActivePart));
                if (s == null) return;
                MoveNode(P, s.a, P2(FindNode(P, s.a)) + d2);
                if (s.b != s.a) MoveNode(P, s.b, P2(FindNode(P, s.b)) + d2);
            }
            else return;
            Rebuild();
        }

        public override void EndEdit()
        {
            // Thả nút gần nút khác → gộp
            if (draggedNode > 0)
            {
                var n = FindNode(P, draggedNode);
                if (n != null)
                {
                    var other = NearestNode(P2(n), GameConfig.TowerSnap, n.id);
                    if (other != null) Merge(P, other.id, n.id);
                }
            }
            draggedNode = 0;
            startP = null;
            Commit();
        }

        // ================= Địa hình =================
        public override bool AffectedBy(Rect r)
        {
            var rr = Rect.MinMaxRect(r.xMin - 3f, r.yMin - 3f, r.xMax + 3f, r.yMax + 3f);
            foreach (var n in P.nodes) if (rr.Contains(P2(n))) return true;
            foreach (var s in P.segs) foreach (var m in s.mid) if (rr.Contains(m)) return true;
            return false;
        }

        public override void TerrainChangedIn(Rect r, bool light)
        {
            var rr = Rect.MinMaxRect(r.xMin - 3f, r.yMin - 3f, r.xMax + 3f, r.yMax + 3f);
            foreach (var n in P.nodes)
                if (rr.Contains(P2(n))) force.Add(NodePart(n.id));
            if (!light)
                foreach (var s in P.segs)
                    foreach (var p in Poly(P, s))
                        if (rr.Contains(p)) { force.Add(SegPart(s.id)); break; }
            if (force.Count > 0) Rebuild();
        }

        // ================= Tay nắm =================
        Vector3 Ground(GNode n) => new Vector3(n.x, HeightField.I.HeightAt(n.x, n.z), n.z);
        float TopOf(GNode n) => nodeTop.TryGetValue(n.id, out var t) ? t : GameConfig.WallHeights[GameConfig.WallHeightDefault];

        public override void GetHandles(List<HandleDef> list)
        {
            if (IsNodePart(ActivePart))
            {
                var n = FindNode(P, IdOf(ActivePart));
                if (n == null) return;
                var g = Ground(n);
                int L = LevelOf(n);
                list.Add(new HandleDef("level", HandleKind.Arrow, g + Vector3.up * (TopOf(n) + 1.2f)));
                if (L > 0)
                {
                    float body = nodeBody.TryGetValue(n.id, out var bt) ? bt : 4f;
                    list.Add(new HandleDef("roof", HandleKind.Cycle, g + TowardCamera(g) * (TowerRadius(L) + 1f) + Vector3.up * (body * 0.6f)));
                }
            }
            else if (IsSegPart(ActivePart))
            {
                var s = FindSeg(P, IdOf(ActivePart));
                if (s == null) return;
                var a = FindNode(P, s.a); var b = FindNode(P, s.b);
                if (a != null) list.Add(new HandleDef("na", HandleKind.Point, Ground(a) + Vector3.up * (TopOf(a) + 0.9f)));
                if (b != null && s.b != s.a) list.Add(new HandleDef("nb", HandleKind.Point, Ground(b) + Vector3.up * (TopOf(b) + 0.9f)));
                if (segMid.TryGetValue(s.id, out var m))
                {
                    list.Add(new HandleDef("bend", HandleKind.Point, m + Vector3.up * 0.8f));
                    list.Add(new HandleDef("height", HandleKind.Arrow, m + Vector3.up * 2.4f));
                    var gm = new Vector3(m.x, HeightField.I.HeightAt(m.x, m.z), m.z);
                    list.Add(new HandleDef("style", HandleKind.Cycle, gm + TowardCamera(gm) * 1.6f + Vector3.up * ((m.y - gm.y) * 0.5f)));
                }
            }
        }

        public override void BeginHandle(string key)
        {
            startP = Clone(P);
            dragInit = false;
            draggedNode = 0;
            if (IsNodePart(ActivePart)) startLevel = LevelOf(FindNode(P, IdOf(ActivePart)));

            // Uốn đoạn thẳng: thêm điểm giữa để có cái mà uốn
            if (key == "bend" && IsSegPart(ActivePart))
            {
                var s = FindSeg(startP, IdOf(ActivePart));
                if (s != null && s.mid.Count < 3)
                {
                    var poly = Poly(startP, s);
                    float len = Len(poly);
                    int k = Mathf.Max(3, Mathf.RoundToInt(len / 1.5f));
                    var mid = new List<Vector2>();
                    for (int i = 1; i < k; i++) mid.Add(PointAt(poly, len * i / k));
                    s.mid = mid;
                }
            }
        }

        public override void DragHandle(string key, HandleInput input)
        {
            if (startP == null) return;
            int id = IdOf(ActivePart);

            if (key == "level")
            {
                var n = FindNode(P, id);
                if (n == null) return;
                int L = Mathf.Clamp(startLevel + Mathf.RoundToInt(input.axisDelta / GameConfig.TowerLevelDragStep), 0, 3);
                if (n.level == L) return;
                n.level = L;
                n.manual = true;
                Rebuild();
                return;
            }

            if (key == "height")
            {
                var s0 = FindSeg(startP, id); var s = FindSeg(P, id);
                if (s0 == null || s == null) return;
                int idx = Mathf.Clamp(s0.heightIdx + Mathf.RoundToInt(input.axisDelta / GameConfig.WallHeightDragStep),
                                      0, GameConfig.WallHeights.Length - 1);
                if (idx == s.heightIdx) return;
                s.heightIdx = idx;
                Rebuild();
                return;
            }

            if (!input.hasGround) return;
            var gp = new Vector2(input.ground.x, input.ground.z);
            P = Clone(startP);

            if (key == "na" || key == "nb")
            {
                var s = FindSeg(P, id);
                if (s == null) return;
                int nid = key == "na" ? s.a : s.b;
                MoveNode(P, nid, gp);
                draggedNode = nid;
            }
            else if (key == "bend")
            {
                if (!dragInit) { dragRef = gp; dragInit = true; }
                var d = gp - dragRef;
                var s = FindSeg(P, id);
                if (s == null) return;
                var poly = Poly(P, s);
                float total = Len(poly), acc = 0;
                for (int i = 1; i < poly.Count - 1; i++)
                {
                    acc += Vector2.Distance(poly[i - 1], poly[i]);
                    s.mid[i - 1] += d * Mathf.Sin(Mathf.PI * acc / total);
                }
            }
            Rebuild();
        }

        public override void ClickHandle(string key)
        {
            int id = IdOf(ActivePart);
            if (key == "roof")
            {
                var n = FindNode(P, id);
                if (n != null) n.roof = (Mathf.Abs(n.roof) + 1) % 3;
            }
            else if (key == "style")
            {
                var s = FindSeg(P, id);
                if (s != null) s.style = (Mathf.Abs(s.style) + 1) % 4;
            }
            Rebuild();
        }

        // ================= Xử lý nét vẽ =================
        /// <summary>
        /// Tìm góc gấp (RDP), cắt nét tại góc, làm mượt từng đoạn (Chaikin), chia đều điểm.
        /// Kết quả: đường cong mượt nhưng góc gấp vẫn sắc.
        /// </summary>
        public static List<Vector3> ProcessStroke(List<Vector3> raw, out List<Vector3> corners)
        {
            corners = new List<Vector3>();
            var keep = PathUtil.SimplifyIndices(raw, GameConfig.SimplifyEps);
            var cuts = new List<int> { 0 };
            for (int k = 1; k < keep.Count - 1; k++)
            {
                Vector3 d0 = PathUtil.Flat(raw[keep[k]] - raw[keep[k - 1]]);
                Vector3 d1 = PathUtil.Flat(raw[keep[k + 1]] - raw[keep[k]]);
                if (d0.sqrMagnitude < 1e-4f || d1.sqrMagnitude < 1e-4f) continue;
                if (Vector3.Angle(d0, d1) > GameConfig.CornerAngle)
                {
                    cuts.Add(keep[k]);
                    corners.Add(raw[keep[k]]);
                }
            }
            cuts.Add(raw.Count - 1);

            var path = new List<Vector3>();
            for (int c = 0; c < cuts.Count - 1; c++)
            {
                int a = cuts[c], b = cuts[c + 1];
                if (b <= a) continue;
                var rs = PathUtil.Resample(PathUtil.Chaikin(raw.GetRange(a, b - a + 1), 3), GameConfig.WallStep);
                if (path.Count > 0 && rs.Count > 0) rs.RemoveAt(0);
                path.AddRange(rs);
            }
            return path;
        }
    }
}
