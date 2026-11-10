using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Thú vật (thế giới sống): gà quanh nhà, cừu gặm cỏ quanh một số nhà, đàn chim bay trên làng, bướm trên bãi hoa dại.
    /// Không lưu — tự sinh từ hiện trạng làng.
    /// </summary>
    public class Fauna : MonoBehaviour
    {
        static readonly Color Wool = Palette.Hex("#F4F0E8"), Dark = Palette.Hex("#3B2F2A");
        static readonly Color[] WingColors = { Palette.Hex("#F6D55C"), Palette.Hex("#FFFFFF"), Palette.Hex("#E59AB0"), Palette.Hex("#8FB7E0"), Palette.Hex("#F08A5D") };

        readonly HashSet<int> stocked = new();
        readonly List<Bird> birds = new();
        readonly List<GameObject> butterflies = new();
        float pending = 1f;
        Transform root;

        void Awake()
        {
            root = new GameObject("Fauna").transform;
            History.Changed += Schedule;
        }

        void OnDestroy() => History.Changed -= Schedule;

        void Schedule() => pending = 1f;

        void Update()
        {
            if (pending <= 0f) return;
            pending -= Time.deltaTime;
            if (pending <= 0f) Refresh();
        }

        /// <summary>Con vật đi được tới đây không (không vào nhà, ao, khối xây, ngoài vùng xây, dốc đứng).</summary>
        public static bool Walkable(Vector2 p)
        {
            if (p.sqrMagnitude > GameConfig.BuildRadius * GameConfig.BuildRadius) return false;
            foreach (var h in EntityManager.OfType<HouseEntity>())
                if (h.ContainsPoint(p, 0.5f)) return false;
            foreach (var pond in EntityManager.OfType<PondEntity>())
                if (pond.Mask != null && pond.Mask.ContainsWorld(p.x, p.y)) return false;
            if (BlockWorld.I && BlockWorld.I.CoversGround(p, 0.3f)) return false;
            var gr = WallGraph.I;
            if (gr && gr.NearestSeg(p, 0.8f, out _, out _)) return false;
            var hf = HeightField.I;
            float y = hf.HeightAt(p.x, p.y);
            if (Mathf.Abs(hf.HeightAt(p.x + 0.8f, p.y) - y) > 0.6f || Mathf.Abs(hf.HeightAt(p.x, p.y + 0.8f) - y) > 0.6f) return false;
            return true;
        }

        void Refresh()
        {
            var houses = new List<HouseEntity>(EntityManager.OfType<HouseEntity>());
            stocked.RemoveWhere(id => !(EntityManager.Get(id) is HouseEntity));

            // Gà + cừu cho nhà mới
            foreach (var h in houses)
            {
                if (stocked.Contains(h.Id)) continue;
                stocked.Add(h.Id);
                int seed = h.P.seed;
                if (seed % 3 != 0)
                    for (int i = 0; i < 2 + seed % 2; i++) Spawn(h, false, i);
                if (seed % 2 == 0)
                    for (int i = 0; i < 2 + (seed / 7) % 2; i++) Spawn(h, true, i);
            }

            // Đàn chim bay quanh tâm làng
            var center = Vector3.zero;
            if (houses.Count > 0)
            {
                foreach (var h in houses) center += h.LocalToWorld(Vector3.zero);
                center /= houses.Count;
                center.y = 0;
            }
            if (birds.Count == 0)
                for (int i = 0; i < 5; i++) birds.Add(MakeBird(i));
            foreach (var b in birds)
            {
                if (!b) continue;
                b.center = center;
                b.radius = Mathf.Clamp(houses.Count * 3f + 12f, 14f, 40f);
            }

            // Bướm trên bãi hoa dại
            foreach (var g in butterflies) if (g) Destroy(g);
            butterflies.Clear();
            var pts = HeightField.I.PaintedPoints(1, 14, 11);
            for (int i = 0; i < pts.Count; i++) butterflies.Add(MakeButterfly(pts[i], i));
        }

        // ---------- Dựng con vật (khối xám) ----------
        void Spawn(HouseEntity h, bool sheep, int i)
        {
            var c = h.LocalToWorld(Vector3.zero);
            Vector2 p = default;
            bool ok = false;
            for (int k = 0; k < 12 && !ok; k++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(sheep ? 6f : 3f, sheep ? 12f : 6f);
                p = new Vector2(c.x + Mathf.Cos(a) * r, c.z + Mathf.Sin(a) * r);
                ok = Walkable(p);
            }
            if (!ok) return;

            var go = new GameObject(sheep ? "Sheep" : "Chicken");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(p.x, HeightField.I.HeightAt(p.x, p.y), p.y);
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            var cr = go.AddComponent<Critter>();
            cr.houseId = h.Id;
            cr.body = body;
            if (sheep)
            {
                // lông xù: lõi + nhiều cụm bông nhỏ, đầu đen có tai, mắt
                var wool = new DetailMesh(h.Id * 31 + i * 7 + 1);
                wool.Blob(new Vector3(0, 0.55f, 0), new Vector3(0.34f, 0.28f, 0.46f), Wool, Wool * 0.86f, 0.05f, 10, 6);
                for (int k = 0; k < 16; k++)
                {
                    float a = k * 2.39996f, yy = wool.Range(-0.12f, 0.2f);
                    float rr = Mathf.Sqrt(Mathf.Max(0f, 1f - (yy / 0.3f) * (yy / 0.3f)));
                    var wc = new Vector3(Mathf.Cos(a) * 0.3f * rr, 0.55f + yy, Mathf.Sin(a) * 0.42f * rr);
                    float s = wool.Range(0.13f, 0.18f);
                    wool.Blob(wc, new Vector3(s, s * 0.9f, s), wool.Vary(Wool, 0.03f), Wool * 0.85f, 0.08f, 7, 5);
                }
                wool.Blob(new Vector3(0, 0.84f, 0.4f), new Vector3(0.13f, 0.08f, 0.12f), Wool, Wool * 0.9f, 0.1f, 7, 4);   // chỏm lông trên đầu
                wool.Blob(new Vector3(0, 0.7f, 0.55f), new Vector3(0.13f, 0.14f, 0.17f), Dark, Dark, 0.03f, 8, 5);
                foreach (var ex in new[] { -1f, 1f })
                {
                    wool.Blob(new Vector3(ex * 0.15f, 0.76f, 0.47f), new Vector3(0.08f, 0.03f, 0.04f), Dark, Dark, 0f, 6, 3);   // tai
                    wool.Blob(new Vector3(ex * 0.07f, 0.74f, 0.66f), Vector3.one * 0.018f, Palette.Cream, Palette.Cream, 0f, 5, 3);   // mắt
                }
                wool.Build("Wool", body, true, Palette.VertexColorMat());
                foreach (var lx in new[] { -0.2f, 0.2f })
                    foreach (var lz in new[] { -0.3f, 0.3f })
                        Part(body, PrimitiveType.Cube, new Vector3(lx, 0.15f, lz), new Vector3(0.09f, 0.32f, 0.09f), Dark);
                cr.minDist = 5f; cr.radius = 13f; cr.speed = 0.45f; cr.hopHeight = 0.03f; cr.hopRate = 6f;
            }
            else
            {
                Part(body, PrimitiveType.Sphere, new Vector3(0, 0.22f, 0), new Vector3(0.26f, 0.24f, 0.34f), Wool);
                Part(body, PrimitiveType.Sphere, new Vector3(0, 0.38f, 0.13f), Vector3.one * 0.15f, Wool);
                Part(body, PrimitiveType.Cube, new Vector3(0, 0.47f, 0.13f), new Vector3(0.03f, 0.07f, 0.09f), Palette.Hex("#C8443A"));
                Part(body, PrimitiveType.Cube, new Vector3(0, 0.37f, 0.22f), new Vector3(0.04f, 0.03f, 0.07f), Palette.Hex("#E9A23B"));
                foreach (var lx in new[] { -0.06f, 0.06f })
                    Part(body, PrimitiveType.Cube, new Vector3(lx, 0.06f, 0), new Vector3(0.025f, 0.12f, 0.025f), Palette.Hex("#E9A23B"));
                cr.minDist = 2f; cr.radius = 6f; cr.speed = 0.9f; cr.hopHeight = 0.04f; cr.hopRate = 14f;
            }
        }

        Bird MakeBird(int i)
        {
            var go = new GameObject("Bird");
            go.transform.SetParent(root, false);
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            Part(body, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.14f, 0.12f, 0.36f), Dark);
            var wl = Wing(body, -1f, 0.45f, Dark);
            var wr = Wing(body, 1f, 0.45f, Dark);
            var b = go.AddComponent<Bird>();
            b.body = body;
            b.wingL = wl;
            b.wingR = wr;
            b.phase = i * 0.45f + Random.Range(0f, 0.2f);
            b.height = 18f + i * 1.3f;
            b.speed = 0.22f + i * 0.01f;
            return b;
        }

        GameObject MakeButterfly(Vector3 at, int i)
        {
            var go = new GameObject("Butterfly");
            go.transform.SetParent(root, false);
            go.transform.position = at + Vector3.up * 0.6f;
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            Part(body, PrimitiveType.Cube, Vector3.zero, new Vector3(0.03f, 0.03f, 0.14f), Dark);
            var col = WingColors[i % WingColors.Length];
            var b = go.AddComponent<Butterfly>();
            b.home = at;
            b.phase = i * 1.7f;
            b.body = body;
            b.wingL = Wing(body, -1f, 0.13f, col);
            b.wingR = Wing(body, 1f, 0.13f, col);
            return go;
        }

        static Transform Wing(Transform parent, float side, float span, Color c)
        {
            var pivot = new GameObject("Wing").transform;
            pivot.SetParent(parent, false);
            Part(pivot, PrimitiveType.Cube, new Vector3(side * span * 0.5f, 0, 0), new Vector3(span, 0.015f, span * 0.6f), c);
            return pivot;
        }

        static void Part(Transform parent, PrimitiveType t, Vector3 lp, Vector3 s, Color c)
        {
            var g = GameObject.CreatePrimitive(t);
            var col = g.GetComponent<Collider>();
            col.enabled = false;
            Object.Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localScale = s;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = Palette.Mat(c);
            r.shadowCastingMode = ShadowCastingMode.On;
        }
    }
}
