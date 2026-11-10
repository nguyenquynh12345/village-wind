using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    [Serializable]
    public class PondParams
    {
        public int x0, z0, w, h;   // khung mặt nạ (ô 0.5 m, chỉ số toàn cục)
        public string bits;        // mặt nạ nén (base64)
        public bool deep;
        public int seed;
    }

    /// <summary>
    /// Ao: mặt nạ ô 0.5 m. Đất trong ao tự lõm xuống (lớp "carve" của HeightField, không sửa đất gốc → hoàn tác sạch).
    /// Mực nước = điểm thấp nhất trên bờ − 0.1 m → không bao giờ tràn.
    /// </summary>
    public class PondEntity : Entity<PondParams>
    {
        static readonly Color WaterColor = Palette.Hex("#7FB7C9");

        PondMask mask, startMask;
        Rect lastRect;
        bool hasLast, editing, startDeep;
        Vector2 pending, anchor;
        readonly Dictionary<string, Vector3> handlePos = new();

        public override string TypeName => "Pond";
        public PondMask Mask => mask;
        public bool IsDeep => P.deep;
        public float WaterLevel { get; private set; }
        float Depth => P.deep ? GameConfig.PondDeep : GameConfig.PondShallow;

        public Rect WorldRect
        {
            get
            {
                if (mask == null) return new Rect(0, 0, 0, 0);
                var r = mask.WorldRect;
                float pad = GameConfig.PondBank + 0.5f;
                return Rect.MinMaxRect(r.xMin - pad, r.yMin - pad, r.xMax + pad, r.yMax + pad);
            }
        }

        public override string Describe => mask == null ? "Ao" : $"Ao · {mask.Area:0} m² · {(P.deep ? "sâu" : "nông")}";

        public void SetMask(PondMask m)
        {
            mask = m;
            m.WriteTo(P);
        }

        /// <summary>Đất bị đào xuống bao nhiêu tại (x, z): sâu ở giữa, thoải dần ra bờ.</summary>
        public float CarveAt(float x, float z)
        {
            if (mask == null || !WorldRect.Contains(new Vector2(x, z))) return 0f;
            float f = mask.Coverage(x, z, GameConfig.PondBank);
            return Depth * Mathf.SmoothStep(0f, 1f, f);
        }

        // ---------- Sinh hình ----------
        public override void Rebuild()
        {
            mask = PondMask.FromParams(P);
            var r = WorldRect;
            var area = hasLast ? Union(lastRect, r) : r;
            lastRect = r;
            hasLast = true;
            if (Id != 0)
            {
                HeightField.I.RecomputeCarve(area);
                // Đang kéo tay nắm: chỉ cập nhật phần nhẹ; thả chuột mới cập nhật hết (nhà, tường, cây)
                EntityManager.TerrainChanged(area, editing, this);
            }
            BuildVisual();
        }

        public override void TerrainChangedIn(Rect r, bool light)
        {
            if (!light) BuildVisual();
        }

        public override void OnRemoved()
        {
            if (!hasLast) return;
            mask = null;
            HeightField.I.RecomputeCarve(lastRect);
            EntityManager.TerrainChanged(lastRect, false, this);
        }

        void BuildVisual()
        {
            var v = NewVisual();
            if (mask == null || mask.Count == 0) return;
            var hf = HeightField.I;
            const float C = PondMask.C;

            var contour = mask.Contour();
            float level = float.MaxValue;
            foreach (var c in contour)
                level = Mathf.Min(level, hf.BaseHeightAt(PondMask.Center(c.x), PondMask.Center(c.y)));
            if (level == float.MaxValue) level = 0f;
            level -= 0.1f;
            WaterLevel = level;

            // Mặt nước: phủ các ô có nước + nới thêm 1 ô (đất bờ sẽ che phần thừa)
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var index = new Dictionary<long, int>();
            int V(int gx, int gz)
            {
                long key = ((long)gx << 32) ^ (uint)gz;
                if (index.TryGetValue(key, out int i)) return i;
                i = verts.Count;
                verts.Add(new Vector3(gx * C, level, gz * C));
                index[key] = i;
                return i;
            }
            for (int gz = mask.z0 - 1; gz <= mask.z0 + mask.h; gz++)
                for (int gx = mask.x0 - 1; gx <= mask.x0 + mask.w; gx++)
                {
                    bool near = mask.Get(gx, gz) || mask.Get(gx + 1, gz) || mask.Get(gx - 1, gz) || mask.Get(gx, gz + 1) || mask.Get(gx, gz - 1);
                    if (!near) continue;
                    int a = V(gx, gz), b = V(gx + 1, gz), c = V(gx, gz + 1), d = V(gx + 1, gz + 1);
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            var mesh = new Mesh { name = "Water" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var water = new GameObject("Water");
            water.transform.SetParent(v.transform, false);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = water.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Palette.WaterMat(WaterColor);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            water.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Đá bờ + lau sậy (chọn ô theo seed → ổn định khi dựng lại)
            var center = mask.Centroid();
            foreach (var c in contour)
            {
                int hsh = Hash(c.x, c.y, P.seed);
                if (hsh % 5 == 0)
                {
                    var p = new Vector2(PondMask.Center(c.x), PondMask.Center(c.y));
                    var outward = (p - center).normalized * 0.4f;
                    var q = p + outward;
                    float y = hf.HeightAt(q.x, q.y);
                    float s = 0.35f + (hsh % 7) * 0.05f;
                    Prim(PrimitiveType.Sphere, v.transform, new Vector3(q.x, y, q.y), new Vector3(s * 1.3f, s * 0.7f, s), Palette.StoneDark, name: "Stone");
                }
                else if (hsh % 11 == 0)
                {
                    var p = new Vector2(PondMask.Center(c.x), PondMask.Center(c.y));
                    for (int k = 0; k < 3; k++)
                    {
                        var o = new Vector2(((hsh >> k) % 5 - 2) * 0.08f, ((hsh >> (k + 3)) % 5 - 2) * 0.08f);
                        float y = Mathf.Max(hf.HeightAt(p.x + o.x, p.y + o.y), level - 0.2f);
                        Prim(PrimitiveType.Cylinder, v.transform, new Vector3(p.x + o.x, y + 0.5f, p.y + o.y),
                             new Vector3(0.06f, 0.5f + k * 0.08f, 0.06f), Palette.Moss, name: "Reed");
                    }
                }
            }
            ApplyTint();
        }

        static int Hash(int x, int z, int seed)
        {
            unchecked
            {
                int h = x * 73856093 ^ z * 19349663 ^ seed * 83492791;
                h ^= h >> 13;
                return h & 0x7fffffff;
            }
        }

        // ---------- Sửa ----------
        public override void Translate(Vector3 d)
        {
            if (mask == null) return;
            editing = true;
            pending += new Vector2(d.x, d.z);
            int sx = Mathf.RoundToInt(pending.x / PondMask.C), sz = Mathf.RoundToInt(pending.y / PondMask.C);
            if (sx == 0 && sz == 0) return;
            pending -= new Vector2(sx, sz) * PondMask.C;
            P.x0 += sx;
            P.z0 += sz;
            Rebuild();
        }

        public override void Rotate(float degrees)
        {
            if (mask == null) return;
            var m = PondMask.Rotate(mask, mask.Centroid(), degrees);
            if (m == null) return;
            m.WriteTo(P);
            Rebuild();
        }

        public override void EndEdit()
        {
            pending = Vector2.zero;
            startMask = null;
            if (editing)
            {
                editing = false;
                Rebuild();
            }
            MergeTouching();
        }

        List<ICommand> sideEffects;
        public override List<ICommand> TakeSideEffects()
        {
            var s = sideEffects;
            sideEffects = null;
            return s;
        }

        /// <summary>Sau khi kéo / nắn bờ: chạm ao khác → gộp thành 1 (ao kia bị xoá, hoàn tác được).</summary>
        void MergeTouching()
        {
            if (mask == null || Id == 0) return;
            bool merged = false;
            foreach (var o in new List<PondEntity>(EntityManager.OfType<PondEntity>()))
            {
                if (o == this || o.Mask == null || !mask.Touches(o.Mask)) continue;
                var u = PondMask.Union(mask, o.Mask);
                if (u == null) continue;
                var del = new DeleteEntityCommand(o);
                del.Do();
                (sideEffects ??= new List<ICommand>()).Add(del);
                SetMask(u);
                merged = true;
            }
            if (merged)
            {
                Rebuild();
                GameUI.Toast("Hai ao chạm nhau đã gộp thành 1");
            }
        }

        public override bool AffectedBy(Rect r) => hasLast && r.Overlaps(lastRect);

        public override void GetHandles(List<HandleDef> list)
        {
            handlePos.Clear();
            if (mask == null) return;
            var hf = HeightField.I;
            var c = mask.Centroid();
            const int Bins = 12;
            var best = new Vector2[Bins];
            var bestD = new float[Bins];
            foreach (var cell in mask.Contour())
            {
                var p = new Vector2(PondMask.Center(cell.x), PondMask.Center(cell.y));
                var d = p - c;
                int bin = Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(d.y, d.x) / (2f * Mathf.PI) * Bins, Bins));
                bin = Mathf.Clamp(bin, 0, Bins - 1);
                if (d.sqrMagnitude > bestD[bin]) { bestD[bin] = d.sqrMagnitude; best[bin] = p; }
            }
            for (int i = 0; i < Bins; i++)
            {
                if (bestD[i] <= 0f) continue;
                var pos = new Vector3(best[i].x, hf.BaseHeightAt(best[i].x, best[i].y) + 0.6f, best[i].y);
                string key = "h" + i;
                handlePos[key] = pos;
                list.Add(new HandleDef(key, HandleKind.Point, pos));
            }
            list.Add(new HandleDef("depth", HandleKind.Arrow, new Vector3(c.x, WaterLevel + 2.5f, c.y)));
        }

        public override void BeginHandle(string key)
        {
            startMask = mask?.Clone();
            startDeep = P.deep;
            if (handlePos.TryGetValue(key, out var p)) anchor = new Vector2(p.x, p.z);
        }

        public override void DragHandle(string key, HandleInput input)
        {
            if (startMask == null) return;
            if (key == "depth")
            {
                bool deep = startDeep;
                if (input.axisDelta > 0.6f) deep = false;
                if (input.axisDelta < -0.6f) deep = true;
                if (deep == P.deep) return;
                P.deep = deep;
                Rebuild();
                return;
            }
            if (!input.hasGround) return;
            editing = true;
            var d = new Vector2(input.ground.x, input.ground.z) - anchor;
            var m = PondMask.Warp(startMask, anchor, d, GameConfig.PondHandleFalloff);
            if (m == null || m.Area < GameConfig.PondMinArea) return;
            m.WriteTo(P);
            Rebuild();
        }
    }
}
