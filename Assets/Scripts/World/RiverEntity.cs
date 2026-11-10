using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    [Serializable]
    public class RiverParams
    {
        public List<Vector2> pts = new();   // đường tâm dòng chảy (từ cao xuống thấp)
        public float width = 3f;            // 1.5 .. 8 m
        public bool deep;                   // nông (0.4 m) / sâu (1 m)
        public int seed;
    }

    /// <summary>
    /// Sông / suối: đào lòng theo đường gấp khúc trong lớp carve của HeightField (hoàn tác sạch).
    /// Mực nước mỗi đoạn = min(bờ trái, bờ phải) − 0.1 m và ép không tăng theo chiều chảy.
    /// Tự nối thông lòng đào và khớp mực nước khi đầu/cuối cách ao ≤ 1 m.
    /// </summary>
    public class RiverEntity : Entity<RiverParams>
    {
        static readonly Color WaterColor = Palette.Hex("#7FB7C9");

        Rect lastRect;
        bool hasLast, editing;
        RiverParams startP;
        bool dragInit;
        Vector2 dragRef;

        // Dữ liệu nối ao & mẫu mực nước dọc dòng chảy
        bool hasStartPond, hasEndPond;
        Vector2 startPondPt, endPondPt;
        List<Vector2> cachedPts;
        float[] cachedLevels;

        public override string TypeName => "River";
        public float Width => Mathf.Clamp(P.width, 1.5f, 8f);
        public bool IsDeep => P.deep;
        public List<Vector2> Points => P.pts;
        public float Length => Geo.Len(P.pts);
        float Depth => P.deep ? GameConfig.PondDeep : GameConfig.PondShallow;
        public Rect Area => Geo.Bounds(P.pts, Width * 0.5f + GameConfig.PondBank + 1.5f);

        public override string Describe => $"Sông · {Length:0} m · {(P.deep ? "sâu" : "nông")} · rộng {Width:0.#} m";

        /// <summary>Đảm bảo chiều điểm từ đầu cao xuống đầu thấp.</summary>
        public void OrientFlow()
        {
            if (P.pts == null || P.pts.Count < 2 || HeightField.I == null) return;
            var hf = HeightField.I;
            var p0 = P.pts[0];
            var p1 = P.pts[P.pts.Count - 1];
            float h0 = FindTouchingPond(p0, out var pond0, out _) ? pond0.WaterLevel : hf.BaseHeightAt(p0.x, p0.y);
            float h1 = FindTouchingPond(p1, out var pond1, out _) ? pond1.WaterLevel : hf.BaseHeightAt(p1.x, p1.y);
            if (h0 < h1) P.pts.Reverse();
        }

        static bool FindTouchingPond(Vector2 pt, out PondEntity hitPond, out Vector2 joinPt)
        {
            hitPond = null;
            joinPt = pt;
            float bestCov = 0f;
            foreach (var pond in EntityManager.OfType<PondEntity>())
            {
                if (pond.Mask == null || !pond.WorldRect.Contains(pt)) continue;
                float cov = pond.Mask.Coverage(pt.x, pt.y, 1f);
                if (cov > bestCov)
                {
                    bestCov = cov;
                    hitPond = pond;
                    var c = pond.Mask.Centroid();
                    var dir = c - pt;
                    joinPt = dir.sqrMagnitude > 1e-4f ? pt + dir.normalized * Mathf.Min(1f, dir.magnitude) : pt;
                }
            }
            return hitPond != null;
        }

        void UpdatePondLinks(out PondEntity pondStart, out PondEntity pondEnd)
        {
            pondStart = null;
            pondEnd = null;
            hasStartPond = false;
            hasEndPond = false;
            if (P.pts == null || P.pts.Count < 2) return;
            if (FindTouchingPond(P.pts[0], out pondStart, out startPondPt))
                hasStartPond = true;
            if (FindTouchingPond(P.pts[P.pts.Count - 1], out pondEnd, out endPondPt))
                hasEndPond = true;
        }

        /// <summary>Đất bị đào xuống bao nhiêu tại (x, z): sâu ở lòng sông, thoải dần ra hai bờ.</summary>
        public float CarveAt(float x, float z)
        {
            if (P.pts == null || P.pts.Count < 2) return 0f;
            var p = new Vector2(x, z);
            if (!Area.Contains(p)) return 0f;

            Geo.Closest(P.pts, p, out _, out _, out float d);
            if (hasStartPond)
                d = Mathf.Min(d, DistSeg(p, startPondPt, P.pts[0]));
            if (hasEndPond)
                d = Mathf.Min(d, DistSeg(p, P.pts[P.pts.Count - 1], endPondPt));

            float hw = Width * 0.5f;
            float bank = GameConfig.PondBank;
            if (d >= hw + bank) return 0f;
            float f = d <= hw ? 1f : 1f - (d - hw) / bank;
            return Depth * Mathf.SmoothStep(0f, 1f, f);
        }

        static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        public bool Covers(Vector2 p, float margin)
        {
            if (P.pts == null || P.pts.Count < 2 || !Area.Contains(p)) return false;
            Geo.Closest(P.pts, p, out _, out _, out float d);
            return d < Width * 0.5f + margin;
        }

        public float WaterLevelAt(Vector2 p)
        {
            if (cachedPts != null && cachedLevels != null && cachedPts.Count >= 2)
            {
                Geo.Closest(cachedPts, p, out int seg, out Vector2 q, out _);
                int i0 = Mathf.Clamp(seg, 0, cachedLevels.Length - 1);
                int i1 = Mathf.Clamp(seg + 1, 0, cachedLevels.Length - 1);
                float segLen = Vector2.Distance(cachedPts[i0], cachedPts[i1]);
                float t = segLen > 1e-4f ? Mathf.Clamp01(Vector2.Distance(cachedPts[i0], q) / segLen) : 0f;
                return Mathf.Lerp(cachedLevels[i0], cachedLevels[i1], t);
            }
            return HeightField.I != null ? HeightField.I.BaseHeightAt(p.x, p.y) - 0.15f : 0f;
        }

        public override void Rebuild()
        {
            if (P.pts == null || P.pts.Count < 2)
            {
                NewVisual();
                return;
            }
            if (!editing) OrientFlow();
            UpdatePondLinks(out var pondStart, out var pondEnd);

            var r = Area;
            var area = hasLast ? Union(lastRect, r) : r;
            lastRect = r;
            hasLast = true;
            if (Id != 0)
            {
                HeightField.I.RecomputeCarve(area);
                EntityManager.TerrainChanged(area, editing, this);
            }
            BuildVisual(pondStart, pondEnd);
        }

        public override void TerrainChangedIn(Rect r, bool light)
        {
            if (!light && P.pts != null && P.pts.Count >= 2)
            {
                bool prevStart = hasStartPond, prevEnd = hasEndPond;
                UpdatePondLinks(out var pondStart, out var pondEnd);
                if (hasStartPond != prevStart || hasEndPond != prevEnd)
                    HeightField.I.RecomputeCarve(Area);
                BuildVisual(pondStart, pondEnd);
            }
        }

        public override void OnRemoved()
        {
            if (!hasLast) return;
            P.pts.Clear();
            HeightField.I.RecomputeCarve(lastRect);
            EntityManager.TerrainChanged(lastRect, false, this);
        }

        void BuildVisual(PondEntity pondStart, PondEntity pondEnd)
        {
            var v = NewVisual();
            if (P.pts == null || P.pts.Count < 2) return;
            var hf = HeightField.I;

            var raw = new List<Vector2>(P.pts);
            if (hasStartPond && Vector2.Distance(startPondPt, raw[0]) > 0.1f)
                raw.Insert(0, startPondPt);
            if (hasEndPond && Vector2.Distance(endPondPt, raw[raw.Count - 1]) > 0.1f)
                raw.Add(endPondPt);

            var pts = Geo.Resample(raw, 0.5f);
            int n = pts.Count;
            if (n < 2) return;

            float hw = Width * 0.5f;
            float sampleOff = hw + 0.5f;
            var levels = new float[n];
            var normals = new Vector2[n];

            for (int i = 0; i < n; i++)
            {
                var dir = (i < n - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                if (i > 0 && i < n - 1) dir = (pts[i + 1] - pts[i - 1]).normalized;
                var nrm = new Vector2(-dir.y, dir.x);
                normals[i] = nrm;

                var left = pts[i] + nrm * sampleOff;
                var right = pts[i] - nrm * sampleOff;
                float bL = hf.BaseHeightAt(left.x, left.y);
                float bR = hf.BaseHeightAt(right.x, right.y);
                levels[i] = Mathf.Min(bL, bR) - 0.1f;
            }

            if (pondStart != null)
                levels[0] = pondStart.WaterLevel;

            // Ép mực nước không tăng theo chiều chảy
            for (int i = 1; i < n; i++)
                levels[i] = Mathf.Min(levels[i], levels[i - 1]);

            // Khớp mực nước ở đầu nối vào ao cuối dòng
            if (pondEnd != null)
            {
                float target = pondEnd.WaterLevel;
                int blendCount = Mathf.Min(n, 6);
                for (int k = 0; k < blendCount; k++)
                {
                    int idx = n - 1 - k;
                    float t = 1f - (float)k / blendCount;
                    levels[idx] = Mathf.Lerp(levels[idx], target, t);
                }
                for (int i = 1; i < n; i++)
                    levels[i] = Mathf.Min(levels[i], levels[i - 1]);
                levels[n - 1] = target;
            }

            // Làm mượt nhẹ bậc mực nước để dải nước liền mạch
            if (n >= 3)
            {
                var smoothed = new float[n];
                smoothed[0] = levels[0];
                smoothed[n - 1] = levels[n - 1];
                for (int i = 1; i < n - 1; i++)
                    smoothed[i] = (levels[i - 1] + levels[i] * 2f + levels[i + 1]) * 0.25f;
                for (int i = 1; i < n; i++)
                    smoothed[i] = Mathf.Min(smoothed[i], smoothed[i - 1]);
                if (pondEnd != null) smoothed[n - 1] = pondEnd.WaterLevel;
                levels = smoothed;
            }

            cachedPts = pts;
            cachedLevels = levels;

            // Dải lưới mặt nước (rộng hơn lòng sông một chút để bờ đất che mép)
            float meshHalf = hw + 0.38f;
            var verts = new List<Vector3>(n * 2);
            var uvs = new List<Vector2>(n * 2);
            var tris = new List<int>((n - 1) * 6);
            float accLen = 0f;

            for (int i = 0; i < n; i++)
            {
                if (i > 0) accLen += Vector2.Distance(pts[i - 1], pts[i]);
                var p = pts[i];
                var nrm = normals[i];
                float y = levels[i];

                var pL = p + nrm * meshHalf;
                var pR = p - nrm * meshHalf;
                verts.Add(new Vector3(pL.x, y, pL.y));
                verts.Add(new Vector3(pR.x, y, pR.y));
                uvs.Add(new Vector2(-meshHalf, accLen));
                uvs.Add(new Vector2(meshHalf, accLen));

                if (i == 0) continue;
                int b0 = (i - 1) * 2, b1 = i * 2;
                tris.Add(b0); tris.Add(b1); tris.Add(b0 + 1);
                tris.Add(b0 + 1); tris.Add(b1); tris.Add(b1 + 1);
            }

            if (tris.Count >= 3)
            {
                var va = verts[tris[0]]; var vb = verts[tris[1]]; var vc = verts[tris[2]];
                if (Vector3.Cross(vb - va, vc - va).y < 0f)
                {
                    for (int i = 0; i < tris.Count; i += 3)
                    {
                        int tmp = tris[i + 1];
                        tris[i + 1] = tris[i + 2];
                        tris[i + 2] = tmp;
                    }
                }
            }

            var mesh = new Mesh { name = "RiverWater" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var water = new GameObject("Water");
            water.transform.SetParent(v.transform, false);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = water.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Palette.RiverWaterMat(WaterColor);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            water.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Đá cuội rải dọc 2 bờ (mỗi ~2 m) và bụi lau ở các khúc cong (~30% đoạn bờ)
            int startIdx = hasStartPond ? 3 : 1;
            int endIdx = hasEndPond ? n - 3 : n - 1;
            for (int i = startIdx; i < endIdx; i += 4)
            {
                var p = pts[i];
                var nrm = normals[i];
                float waterY = levels[i];

                // Độ cong tại đoạn này
                float curv = 0f;
                if (i >= 2 && i < n - 2)
                {
                    var d0 = (pts[i] - pts[i - 2]).normalized;
                    var d1 = (pts[i + 2] - pts[i]).normalized;
                    curv = 1f - Mathf.Clamp01(Vector2.Dot(d0, d1));
                }

                for (int side = -1; side <= 1; side += 2)
                {
                    int hsh = Hash(i, side, P.seed);
                    var bankDir = nrm * side;
                    float bankDist = hw + 0.25f + (hsh % 5) * 0.06f;
                    var q = p + bankDir * bankDist;
                    if (InAnyPond(q)) continue;

                    float y = hf.HeightAt(q.x, q.y);
                    float s = 0.32f + (hsh % 7) * 0.05f;
                    Prim(PrimitiveType.Sphere, v.transform, new Vector3(q.x, y, q.y),
                         new Vector3(s * 1.3f, s * 0.7f, s), Palette.StoneDark, name: "Stone");

                    // Bụi lau ở khúc cong hoặc ~30% đoạn bờ
                    bool spawnReed = curv > 0.04f ? (hsh % 10 < 6) : (hsh % 10 < 3);
                    if (spawnReed)
                    {
                        var rq = p + bankDir * (hw + 0.1f);
                        for (int k = 0; k < 3; k++)
                        {
                            var o = new Vector2(((hsh >> k) % 5 - 2) * 0.08f, ((hsh >> (k + 3)) % 5 - 2) * 0.08f);
                            float ry = Mathf.Max(hf.HeightAt(rq.x + o.x, rq.y + o.y), waterY - 0.15f);
                            Prim(PrimitiveType.Cylinder, v.transform, new Vector3(rq.x + o.x, ry + 0.48f, rq.y + o.y),
                                 new Vector3(0.06f, 0.48f + k * 0.08f, 0.06f), Palette.Moss, name: "Reed");
                        }
                    }
                }
            }

            ApplyTint();
        }

        static bool InAnyPond(Vector2 p)
        {
            foreach (var pond in EntityManager.OfType<PondEntity>())
                if (pond.Mask != null && pond.WorldRect.Contains(p) && pond.Mask.Coverage(p.x, p.y, 0.3f) > 0.1f)
                    return true;
            return false;
        }

        static int Hash(int a, int b, int seed)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663 ^ seed * 83492791;
                h ^= h >> 13;
                return h & 0x7fffffff;
            }
        }

        // ---------- Biến đổi & Tay nắm ----------
        public override bool AffectedBy(Rect r) => hasLast && r.Overlaps(lastRect);

        public override void Translate(Vector3 d)
        {
            editing = true;
            var dv = new Vector2(d.x, d.z);
            for (int i = 0; i < P.pts.Count; i++) P.pts[i] += dv;
            Rebuild();
        }

        public override void Rotate(float degrees)
        {
            if (P.pts.Count == 0) return;
            editing = true;
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

        Vector3 G(Vector2 p, float up) => new Vector3(p.x, HeightField.I.HeightAt(p.x, p.y) + up, p.y);

        public override void GetHandles(List<HandleDef> list)
        {
            if (P.pts == null || P.pts.Count < 2) return;
            list.Add(new HandleDef("a", HandleKind.Point, G(P.pts[0], 0.6f)));
            list.Add(new HandleDef("b", HandleKind.Point, G(P.pts[P.pts.Count - 1], 0.6f)));
            var mid = Geo.PointAt(P.pts, Length * 0.5f);
            list.Add(new HandleDef("bend", HandleKind.Point, G(mid, 0.6f)));
            list.Add(new HandleDef("width", HandleKind.Arrow, G(mid, 2.2f)));
            var gm = G(mid, 0f);
            list.Add(new HandleDef("deep", HandleKind.Cycle, gm + TowardCamera(gm) * (Width * 0.5f + 1.2f) + Vector3.up * 0.8f));
        }

        public override void BeginHandle(string key)
        {
            startP = JsonUtility.FromJson<RiverParams>(Capture());
            dragInit = false;
            if (key == "bend" && startP.pts.Count < 4)
                startP.pts = Geo.Resample(startP.pts, 1.5f);
        }

        public override void DragHandle(string key, HandleInput input)
        {
            if (startP == null) return;
            if (key == "width")
            {
                float w = Mathf.Clamp(Mathf.Round((startP.width + input.axisDelta * 0.5f) * 2f) / 2f, 1.5f, 8f);
                if (Mathf.Approximately(w, P.width)) return;
                editing = true;
                P.width = w;
                Rebuild();
                return;
            }
            if (!input.hasGround) return;
            editing = true;
            var gp = new Vector2(input.ground.x, input.ground.z);
            P = JsonUtility.FromJson<RiverParams>(JsonUtility.ToJson(startP));
            var pts = P.pts;
            int n = pts.Count;
            float total = Geo.Len(pts);
            if (total < 1e-4f) return;

            if (key == "a" || key == "b")
            {
                var d = gp - (key == "a" ? pts[0] : pts[n - 1]);
                float acc = 0f;
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
                float acc = 0f;
                for (int i = 1; i < n - 1; i++)
                {
                    acc += Vector2.Distance(startP.pts[i - 1], startP.pts[i]);
                    pts[i] += d * Mathf.Sin(Mathf.PI * acc / total);
                }
            }
            Rebuild();
        }

        public override void EndEdit()
        {
            startP = null;
            if (editing)
            {
                editing = false;
                Rebuild();
            }
        }

        public override void ClickHandle(string key)
        {
            if (key != "deep") return;
            P.deep = !P.deep;
            Rebuild();
        }
    }
}
