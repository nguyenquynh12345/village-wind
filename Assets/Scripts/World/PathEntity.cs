using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    [Serializable]
    public class PathParams
    {
        public List<Vector2> pts = new();   // đường giữa đã làm mượt
        public float width = 2f;            // 1..4 m
        public int style;                   // 0 đất (gần nhà tự thành đá cuội) · 1 đá cuội · 2 ván gỗ
    }

    /// <summary>
    /// Đường đi: dải phẳng bám mặt đất, viền mềm hoà vào cỏ. Dốc &gt; 25° tự thành bậc thang.
    /// Sửa: kéo 2 đầu, uốn giữa, đổi bề rộng, đổi kiểu.
    /// </summary>
    public class PathEntity : Entity<PathParams>
    {
        public static readonly string[] StyleNames = { "Đất", "Đá cuội", "Ván gỗ" };
        static readonly Color[] StyleColors = { Palette.Hex("#B99D74"), Palette.Hex("#C9C0B3"), Palette.Hex("#A0764E") };
        static readonly Color Cobble = Palette.Hex("#C9C0B3");

        Rect lastRect;
        bool hasLast;
        PathParams startP;
        bool dragInit;
        Vector2 dragRef;

        public override string TypeName => "Path";
        public float Width => Mathf.Clamp(P.width, 1f, 4f);
        public List<Vector2> Points => P.pts;
        public float Length => Geo.Len(P.pts);
        public override string Describe => $"Đường · {Length:0} m · {StyleNames[Mathf.Abs(P.style) % 3]} · rộng {Width:0.#} m";
        public Rect Area => Geo.Bounds(P.pts, Width * 0.5f + 1f);

        public override void Rebuild()
        {
            var v = NewVisual();
            if (P.pts.Count < 2) return;
            var hf = HeightField.I;
            var pts = Geo.Resample(P.pts, 0.5f);
            int n = pts.Count;
            int style = Mathf.Abs(P.style) % 3;
            float half = Width * 0.5f, edge = 0.35f;

            var houses = new List<HouseEntity>(EntityManager.OfType<HouseEntity>());
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                var dir = (i < n - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                if (i > 0 && i < n - 1) dir = (pts[i + 1] - pts[i - 1]).normalized;
                var nrm = new Vector2(-dir.y, dir.x);

                Color c = StyleColors[style];
                if (style == 0)
                {
                    float near = 99f;
                    foreach (var h in houses)
                    {
                        near = Mathf.Min(near, h.DistanceTo(pts[i]));
                    }
                    c = Color.Lerp(Cobble, c, Mathf.InverseLerp(4f, 6f, near));   // trong 6 m quanh nhà → đá cuội
                }

                // 4 đỉnh ngang: viền ngoài trái, mép trái, mép phải, viền ngoài phải
                float[] offs = { half + edge, half, -half, -(half + edge) };
                for (int k = 0; k < 4; k++)
                {
                    var q = pts[i] + nrm * offs[k];
                    verts.Add(new Vector3(q.x, hf.HeightAt(q.x, q.y) + 0.04f, q.y));
                    var grass = Palette.Grass;
                    // alpha = mức phủ hoa văn: lõi đường 1, viền cỏ 0
                    var vc = k == 0 || k == 3 ? Color.Lerp(c, grass, 0.85f) : c;
                    vc.a = k == 0 || k == 3 ? 0f : 1f;
                    cols.Add(vc);
                }
                if (i == 0) continue;
                int b0 = (i - 1) * 4, b1 = i * 4;
                for (int k = 0; k < 3; k++)
                {
                    int a = b0 + k, b = b0 + k + 1, cc = b1 + k, d = b1 + k + 1;
                    tris.Add(a); tris.Add(cc); tris.Add(b);
                    tris.Add(b); tris.Add(cc); tris.Add(d);
                }
            }

            // Tam giác phải hướng lên: kiểm tra chiều và lật nếu cần
            if (n >= 2)
            {
                var a = verts[tris[0]]; var b = verts[tris[1]]; var c = verts[tris[2]];
                if (Vector3.Cross(b - a, c - a).y < 0)
                    for (int i = 0; i < tris.Count; i += 3) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
            }

            var mesh = new Mesh { name = "Path" };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("PathMesh");
            go.transform.SetParent(v.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Palette.VertexColorMat(style == 1 ? Palette.PatCobble : style == 2 ? Palette.PatWood : Palette.PatDirt);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Ván gỗ: thanh ngang · Dốc > 25°: bậc thang
            for (int i = 0; i < n - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                float ya = hf.HeightAt(a.x, a.y), yb = hf.HeightAt(b.x, b.y);
                float slope = Mathf.Atan2(Mathf.Abs(yb - ya), Vector2.Distance(a, b)) * Mathf.Rad2Deg;
                var dir3 = new Vector3(b.x - a.x, 0, b.y - a.y);
                if (dir3.sqrMagnitude < 1e-6f) continue;
                var rot = Quaternion.LookRotation(dir3);
                if (slope > 25f)
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(a.x, Mathf.Max(ya, yb) - 0.05f, a.y),
                         new Vector3(Width, 0.18f, 0.45f), Palette.StoneDark, localRot: rot, name: "Step");
                else if (style == 2 && i % 2 == 0)
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(a.x, ya + 0.07f, a.y),
                         new Vector3(Width * 0.95f, 0.06f, 0.4f), Palette.Wood, localRot: rot, name: "Plank");
            }
            ApplyTint();

            var r = Area;
            if (Id != 0) EntityManager.NotifyTrees(hasLast ? Union(lastRect, r) : r);
            lastRect = r;
            hasLast = true;
        }

        public override void OnRemoved()
        {
            if (hasLast) EntityManager.NotifyTrees(lastRect);
        }

        public override bool AffectedBy(Rect r) => r.Overlaps(Area);

        public override void Translate(Vector3 d)
        {
            for (int i = 0; i < P.pts.Count; i++) P.pts[i] += new Vector2(d.x, d.z);
            Rebuild();
        }

        public override void Rotate(float degrees)
        {
            if (P.pts.Count == 0) return;
            Vector2 c = Vector2.zero;
            foreach (var p in P.pts) c += p;
            c /= P.pts.Count;
            var q = Quaternion.Euler(0, degrees, 0);
            for (int i = 0; i < P.pts.Count; i++)
            {
                var r = q * new Vector3(P.pts[i].x - c.x, 0, P.pts[i].y - c.y);
                P.pts[i] = new Vector2(r.x + c.x, r.z + c.y);
            }
            Rebuild();
        }

        /// <summary>Cây có nằm trên đường không.</summary>
        public bool Covers(Vector2 p, float margin)
        {
            if (!Area.Contains(p)) return false;
            Geo.Closest(P.pts, p, out _, out _, out float d);
            return d < Width * 0.5f + margin;
        }

        // ---------- Tay nắm ----------
        Vector3 G(Vector2 p, float up) => new Vector3(p.x, HeightField.I.HeightAt(p.x, p.y) + up, p.y);

        public override void GetHandles(List<HandleDef> list)
        {
            if (P.pts.Count < 2) return;
            list.Add(new HandleDef("a", HandleKind.Point, G(P.pts[0], 0.6f)));
            list.Add(new HandleDef("b", HandleKind.Point, G(P.pts[P.pts.Count - 1], 0.6f)));
            var mid = Geo.PointAt(P.pts, Length * 0.5f);
            list.Add(new HandleDef("bend", HandleKind.Point, G(mid, 0.6f)));
            list.Add(new HandleDef("width", HandleKind.Arrow, G(mid, 2.2f)));
            var gm = G(mid, 0f);
            list.Add(new HandleDef("style", HandleKind.Cycle, gm + TowardCamera(gm) * (Width * 0.5f + 1.2f) + Vector3.up * 0.8f));
        }

        public override void BeginHandle(string key)
        {
            startP = JsonUtility.FromJson<PathParams>(Capture());
            dragInit = false;
            if (key == "bend" && startP.pts.Count < 4)
                startP.pts = Geo.Resample(startP.pts, 1.5f);
        }

        public override void DragHandle(string key, HandleInput input)
        {
            if (startP == null) return;
            if (key == "width")
            {
                float w = Mathf.Clamp(Mathf.Round((startP.width + input.axisDelta * 0.5f) * 2f) / 2f, 1f, 4f);
                if (Mathf.Approximately(w, P.width)) return;
                P.width = w;
                Rebuild();
                return;
            }
            if (!input.hasGround) return;
            var gp = new Vector2(input.ground.x, input.ground.z);
            P = JsonUtility.FromJson<PathParams>(JsonUtility.ToJson(startP));
            var pts = P.pts;
            int n = pts.Count;
            float total = Geo.Len(pts);
            if (total < 1e-4f) return;

            if (key == "a" || key == "b")
            {
                var d = gp - (key == "a" ? pts[0] : pts[n - 1]);
                float acc = 0;
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) acc += Vector2.Distance(startP.pts[i - 1], startP.pts[i]);
                    float t = acc / total;
                    pts[i] += d * (key == "a" ? 1f - t : t);
                }
            }
            else if (key == "bend")
            {
                if (!dragInit) { dragRef = gp; dragInit = true; }
                var d = gp - dragRef;
                float acc = 0;
                for (int i = 1; i < n - 1; i++)
                {
                    acc += Vector2.Distance(startP.pts[i - 1], startP.pts[i]);
                    pts[i] += d * Mathf.Sin(Mathf.PI * acc / total);
                }
            }
            Rebuild();
        }

        public override void EndEdit() => startP = null;

        public override void ClickHandle(string key)
        {
            if (key != "style") return;
            P.style = (Mathf.Abs(P.style) + 1) % 3;
            Rebuild();
        }

        // ---------- Cọ màu ----------
        public override string PaintCategory(Collider c) => "pathStyle";
        public override int PaintCount(string cat) => 3;
        public override int PaintGet(Collider c) => Mathf.Abs(P.style) % 3;
        public override void PaintSet(Collider c, int v) { P.style = v; Rebuild(); }
    }
}
