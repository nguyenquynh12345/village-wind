using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    [Serializable]
    public class TreeParams
    {
        public float x, z;
        public int type;          // 0 tròn · 1 thông · 2 bụi · 3 cây hoa
        public float size = 1f;   // 0.6 .. 1.6
        public float rot;
    }

    /// <summary>
    /// Cây: 4 loại, đổi cỡ / loại bằng tay nắm. Bị nhà, tường, ao che → tự ẩn (vẫn lưu; bỏ công trình thì hiện lại).
    /// Mới trồng thì lớn dần; luôn đung đưa nhẹ.
    /// </summary>
    public class TreeEntity : Entity<TreeParams>
    {
        public static readonly string[] TypeNames = { "Cây tròn", "Cây thông", "Bụi", "Cây hoa" };
        static readonly Color Blossom = Palette.Hex("#E8A7B5");
        static readonly Color PineGreen = Palette.Hex("#4F7A45");

        float startSize;
        float topY;

        public override string TypeName => "Tree";
        public bool Hidden { get; private set; }
        public bool IsWillow { get; private set; }
        public int Type => Mathf.Abs(P.type) % 4;

        public override string PaintCategory(Collider c) => "treeType";
        public override int PaintCount(string cat) => 4;
        public override int PaintGet(Collider c) => Type;
        public override void PaintSet(Collider c, int v) { P.type = v; Rebuild(); }
        public Vector2 XZ => new Vector2(P.x, P.z);
        public override string Describe => $"{(IsWillow ? "Liễu rủ" : TypeNames[Mathf.Abs(P.type) % 4])} · cỡ {P.size:0.0}";

        /// <summary>Chỗ này có trồng cây được không (null = được, khác null = lý do).</summary>
        public static string BlockedReason(float x, float z)
        {
            var p = new Vector2(x, z);
            foreach (var h in EntityManager.OfType<HouseEntity>())
                if (h.ContainsPoint(p, 0.4f)) return "Trên nền nhà";
            if (BlockWorld.I && BlockWorld.I.CoversGround(p, 0.3f)) return "Dưới khối xây";
            foreach (var pond in EntityManager.OfType<PondEntity>())
                if (pond.Mask != null && pond.Mask.Coverage(x, z, 0.6f) > 0f) return "Dưới nước";
            foreach (var river in EntityManager.OfType<RiverEntity>())
                if (river.Covers(p, 0.5f)) return "Dưới nước";
            foreach (var path in EntityManager.OfType<PathEntity>())
                if (path.Covers(p, 0.4f)) return "Trên đường";
            var gr = WallGraph.I;
            if (gr)
            {
                if (gr.NearestSeg(p, 0.9f, out _, out _)) return "Trên tường";
                foreach (var (pos, radius) in gr.Towers())
                    if (Vector2.Distance(pos, p) < radius + 0.4f) return "Trên tháp";
            }
            var hf = HeightField.I;
            float dx = hf.HeightAt(x + 1f, z) - hf.HeightAt(x - 1f, z);
            float dz = hf.HeightAt(x, z + 1f) - hf.HeightAt(x, z - 1f);
            float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz) / 2f) * Mathf.Rad2Deg;
            if (slope > GameConfig.TreeMaxSlope) return "Dốc quá";
            return null;
        }

        public override void Rebuild()
        {
            var v = NewVisual();
            Hidden = Id != 0 && BlockedReason(P.x, P.z) != null;
            if (Hidden) { v.SetActive(false); return; }

            float g = HeightField.I.HeightAt(P.x, P.z);
            v.transform.SetPositionAndRotation(new Vector3(P.x, g, P.z), Quaternion.Euler(0, P.rot, 0));
            float s = Mathf.Clamp(P.size, GameConfig.TreeMinSize, GameConfig.TreeMaxSize);
            v.transform.localScale = Vector3.one * s;

            var growth = new GameObject("Growth").transform;
            growth.SetParent(v.transform, false);
            var crown = new GameObject("Crown").transform;
            crown.SetParent(growth, false);
            crown.gameObject.AddComponent<Sway>();

            // E5: cây tròn sát ao / sông → liễu rủ
            IsWillow = false;
            if (Mathf.Abs(P.type) % 4 == 0)
            {
                foreach (var pond in EntityManager.OfType<PondEntity>())
                    if (pond.Mask != null && pond.Mask.Coverage(P.x, P.z, 4f) > 0f) { IsWillow = true; break; }
                if (!IsWillow)
                    foreach (var river in EntityManager.OfType<RiverEntity>())
                        if (river.Covers(new Vector2(P.x, P.z), 4f)) { IsWillow = true; break; }
            }

            // Hình cây sinh bằng code (M16): thân thuôn có cành, tán gồm nhiều chùm lá lồi lõm.
            var trunk = new DetailMesh((int)(P.x * 73.1f + P.z * 19.7f) + Mathf.Abs(P.type) * 7);
            var leaves = new DetailMesh((int)(P.x * 31.3f + P.z * 57.9f) + 3);
            int kind = IsWillow ? 4 : Mathf.Abs(P.type) % 4;
            Vector3 canopyC;
            float canopyR;
            switch (kind)
            {
                case 1: BuildPine(trunk, leaves, out canopyC, out canopyR); break;
                case 2: BuildBush(leaves, out canopyC, out canopyR); break;
                case 4: BuildWillow(trunk, leaves, out canopyC, out canopyR); break;
                default: BuildBroadleaf(trunk, leaves, kind == 3, out canopyC, out canopyR); break;
            }
            trunk.Build("Trunk", growth);
            var leafGo = leaves.Build("Leaves", crown);
            if (leafGo)
            {
                var sc = leafGo.AddComponent<SphereCollider>();
                sc.center = canopyC;
                sc.radius = canopyR;
            }
            topY = canopyC.y + canopyR;            ApplyTint();
        }

        // ---------- Hình cây ----------
        static readonly Color Bark = Palette.Hex("#6B4F3E"), BarkLight = Palette.Hex("#8A6A50");
        static readonly Color LeafTop = Palette.Hex("#8DBB5E"), LeafBottom = Palette.Hex("#3D6634");
        static readonly Color PineTop = Palette.Hex("#5E8F55"), PineBottom = Palette.Hex("#2F5535");
        static readonly Color BlossomTop = Palette.Hex("#F6C9D3"), BlossomBottom = Palette.Hex("#C77E92");
        static readonly Color WillowTop = Palette.Hex("#A9C978"), WillowBottom = Palette.Hex("#5D8A47");

        static void BuildBroadleaf(DetailMesh tr, DetailMesh lv, bool blossom, out Vector3 canopyC, out float canopyR)
        {
            var lean = new Vector3(tr.Range(-0.15f, 0.15f), 0, tr.Range(-0.15f, 0.15f));
            var top = new Vector3(0, 1.55f, 0) + lean;
            tr.Taper(Vector3.zero, top, 0.2f, 0.12f, 7, Bark, BarkLight);
            canopyC = new Vector3(lean.x, 2.55f, lean.z);
            canopyR = 1.45f;
            Color lt = blossom ? BlossomTop : LeafTop, lb = blossom ? BlossomBottom : LeafBottom;
            // 3 cành chĩa lên đỡ các chùm lá
            int clusters = 6;
            for (int k = 0; k < clusters; k++)
            {
                float a = k * Mathf.PI * 2f / clusters + tr.Range(-0.3f, 0.3f);
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var pos = canopyC + dir * lv.Range(0.75f, 1.05f) + Vector3.up * lv.Range(-0.45f, 0.35f);
                if (k % 2 == 0) tr.Taper(top - Vector3.up * 0.25f, Vector3.Lerp(top, pos, 0.75f), 0.08f, 0.03f, 5, Bark, BarkLight);
                float rr = lv.Range(0.62f, 0.85f);
                lv.Blob(pos, new Vector3(rr, rr * 0.8f, rr), lv.Vary(lt, 0.06f), lv.Vary(lb, 0.06f), 0.16f);
            }
            lv.Blob(canopyC + Vector3.up * 0.25f, new Vector3(1.05f, 0.9f, 1.05f), lv.Vary(lt, 0.05f), lb, 0.14f, 11, 7);
            lv.Blob(canopyC + Vector3.up * 0.95f + lean, new Vector3(0.6f, 0.5f, 0.6f), lv.Vary(lt * 1.05f, 0.05f), lb, 0.16f);
            if (blossom)   // vài chùm hoa sáng hơn điểm xuyết
                for (int k = 0; k < 7; k++)
                {
                    float a = k * 2.4f;
                    var p = canopyC + new Vector3(Mathf.Cos(a) * 1.1f, lv.Range(-0.1f, 0.9f), Mathf.Sin(a) * 1.1f);
                    lv.Blob(p, Vector3.one * 0.18f, Palette.Hex("#FFFFFF"), Palette.Hex("#F2C4CE"), 0.1f, 6, 4);
                }
        }

        static void BuildPine(DetailMesh tr, DetailMesh lv, out Vector3 canopyC, out float canopyR)
        {
            tr.Taper(Vector3.zero, new Vector3(0, 3.9f, 0), 0.17f, 0.04f, 6, Bark, BarkLight);
            int tiers = 5;
            for (int k = 0; k < tiers; k++)
            {
                float t = k / (float)(tiers - 1);
                float y = 0.75f + t * 2.55f;
                float r = Mathf.Lerp(1.45f, 0.5f, t) * lv.Range(0.92f, 1.08f);
                float h = Mathf.Lerp(1.3f, 0.95f, t);
                // nón hơi rủ: đáy thấp hơn tâm một chút
                var col = lv.Vary(PineTop, 0.05f);
                lv.Taper(new Vector3(lv.Range(-0.05f, 0.05f), y, lv.Range(-0.05f, 0.05f)), new Vector3(0, y + h, 0), r, 0f, 9, PineBottom, col);
            }
            canopyC = new Vector3(0, 2.3f, 0);
            canopyR = 1.5f;
        }

        static void BuildBush(DetailMesh lv, out Vector3 canopyC, out float canopyR)
        {
            canopyC = new Vector3(0, 0.55f, 0);
            canopyR = 0.95f;
            int n = 5;
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n + lv.Range(-0.4f, 0.4f);
                var p = new Vector3(Mathf.Cos(a) * 0.45f, lv.Range(0.35f, 0.6f), Mathf.Sin(a) * 0.45f);
                float r = lv.Range(0.42f, 0.6f);
                lv.Blob(p, new Vector3(r, r * 0.85f, r), lv.Vary(LeafTop, 0.06f), LeafBottom, 0.18f, 8, 5);
            }
            lv.Blob(new Vector3(0, 0.75f, 0), new Vector3(0.55f, 0.5f, 0.55f), lv.Vary(LeafTop * 1.04f, 0.05f), LeafBottom, 0.16f, 8, 5);
        }

        static void BuildWillow(DetailMesh tr, DetailMesh lv, out Vector3 canopyC, out float canopyR)
        {
            var top = new Vector3(0.2f, 1.9f, 0.1f);
            tr.Taper(Vector3.zero, new Vector3(0.1f, 1.0f, 0), 0.24f, 0.17f, 7, Bark, BarkLight);
            tr.Taper(new Vector3(0.1f, 1.0f, 0), top, 0.17f, 0.11f, 7, BarkLight, Bark);
            canopyC = new Vector3(0.2f, 2.7f, 0.1f);
            canopyR = 1.7f;
            // vòm tán: lõi + nhiều cụm lá nhỏ nhô lên tạo đường viền mềm, không bị phẳng
            lv.Blob(canopyC, new Vector3(1.35f, 0.85f, 1.35f), WillowTop, WillowBottom, 0.1f, 14, 8);
            for (int k = 0; k < 9; k++)
            {
                float a = k * Mathf.PI * 2f / 9 + lv.Range(-0.2f, 0.2f);
                float rr = lv.Range(0.55f, 0.95f);
                var c = canopyC + new Vector3(Mathf.Cos(a) * rr, lv.Range(0.15f, 0.45f), Mathf.Sin(a) * rr);
                float s = lv.Range(0.5f, 0.7f);
                lv.Blob(c, new Vector3(s, s * 0.8f, s), lv.Vary(WillowTop, 0.05f), WillowBottom, 0.12f, 10, 6);
            }
            lv.Blob(canopyC + new Vector3(0, 0.6f, 0), new Vector3(0.7f, 0.5f, 0.7f), lv.Vary(WillowTop, 0.04f), WillowTop, 0.14f, 8, 5);

            // các dải lá rủ xuống: 2 vòng so le, chuỗi chùm lá nhỏ thuôn dần
            for (int ring = 0; ring < 2; ring++)
            {
                int strands = ring == 0 ? 20 : 14;
                for (int k = 0; k < strands; k++)
                {
                    float a = (k + ring * 0.5f) * Mathf.PI * 2f / strands + lv.Range(-0.12f, 0.12f);
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var p = canopyC + dir * (ring == 0 ? lv.Range(1.15f, 1.45f) : lv.Range(0.75f, 1.0f)) + Vector3.down * (ring == 0 ? 0.3f : 0.5f);
                    float len = ring == 0 ? lv.Range(1.4f, 2.1f) : lv.Range(0.9f, 1.4f);
                    int beads = 6;
                    for (int b = 0; b < beads; b++)
                    {
                        float f = b / (float)(beads - 1);
                        float r = Mathf.Lerp(0.2f, 0.07f, f);
                        var q = p + dir * (0.15f * f) + Vector3.down * (len * f);
                        lv.Blob(q, new Vector3(r, r * 1.7f, r), lv.Vary(Color.Lerp(WillowTop, WillowBottom, f * 0.5f), 0.06f), WillowBottom, 0.2f, 6, 4);
                    }
                }
            }
        }
        public override void PlayPop(float delay)
        {
            if (!visual || Hidden) return;
            PopIn.Add(visual, delay, false, 0.35f);
            var growth = visual.transform.Find("Growth");
            if (growth) growth.gameObject.AddComponent<Grow>();
        }

        public override void Translate(Vector3 d)
        {
            P.x += d.x;
            P.z += d.z;
            Rebuild();
        }

        public override void Rotate(float degrees)
        {
            P.rot = Mathf.Repeat(P.rot + degrees, 360f);
            Rebuild();
        }

        public override bool AffectedBy(Rect r) => r.Overlaps(new Rect(P.x - 1.5f, P.z - 1.5f, 3f, 3f));

        // ---- Tay nắm ----
        public override void GetHandles(List<HandleDef> list)
        {
            if (Hidden) return;
            float g = HeightField.I.HeightAt(P.x, P.z);
            float s = Mathf.Clamp(P.size, GameConfig.TreeMinSize, GameConfig.TreeMaxSize);
            var basePos = new Vector3(P.x, g, P.z);
            list.Add(new HandleDef("size", HandleKind.Arrow, basePos + Vector3.up * (topY * s + 0.8f)));
            list.Add(new HandleDef("type", HandleKind.Cycle, basePos + TowardCamera(basePos) * 1.8f + Vector3.up * 1.2f));
        }

        public override void BeginHandle(string key) => startSize = P.size;

        public override void DragHandle(string key, HandleInput input)
        {
            if (key != "size") return;
            float s = Mathf.Clamp(Mathf.Round((startSize + input.axisDelta * 0.25f) / 0.05f) * 0.05f,
                                  GameConfig.TreeMinSize, GameConfig.TreeMaxSize);
            if (Mathf.Approximately(s, P.size)) return;
            P.size = s;
            Rebuild();
        }

        public override void ClickHandle(string key)
        {
            if (key != "type") return;
            P.type = (Mathf.Abs(P.type) + 1) % 4;
            Rebuild();
        }
    }
}
