using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>1 mảng dây leo, lưu theo khung cục bộ của vật nó bám (o = id vật).</summary>
    [Serializable]
    public class IvyPatch
    {
        public int o, seed;
        public Vector3 p, n;
        public float s = 1f;
    }

    [Serializable]
    public class IvyParams
    {
        public List<IvyPatch> patches = new();
    }

    /// <summary>
    /// Dây leo vẽ bằng cọ lên tường thành, nhà, khối. Bám theo vật (dời nhà → dây leo đi theo; xoá nhà → ẩn, hoàn tác → hiện lại).
    /// 1 vật duy nhất, luôn tồn tại; hình gộp thành 1 lưới, không có va chạm.
    /// </summary>
    public class IvyLayer : Entity<IvyParams>
    {
        public static IvyLayer I;

        static readonly Color[] Leaves = { Palette.Hex("#4F7A3A"), Palette.Hex("#6E9A4E"), Palette.Hex("#3E6A34"), Palette.Hex("#86AE5C") };
        static readonly Color[] Blossoms = { Palette.Hex("#F4EFE6"), Palette.Hex("#E59AB0"), Palette.Hex("#B9A3E3") };

        float pending = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        void Awake()
        {
            I = this;
            History.Changed += Schedule;
        }

        void OnDestroy()
        {
            History.Changed -= Schedule;
            if (I == this) I = null;
        }

        void Schedule() => pending = 0.25f;

        void Update()
        {
            if (pending < 0f) return;
            pending -= Time.deltaTime;
            if (pending < 0f) Rebuild();
        }

        public override string TypeName => "Ivy";
        public override string Describe => $"Dây leo · {P.patches.Count} mảng";
        public int Count => P.patches?.Count ?? 0;

        public override void Translate(Vector3 delta) { }
        public override bool AffectedBy(Rect r) => false;
        public override void TerrainChangedIn(Rect r, bool light) { }
        public override bool CanDuplicate => false;
        public override bool CanRotate => false;
        public override bool DeletePart(int part) => true;

        // ---------- Sửa ----------
        static bool OwnerFrame(int owner, out Matrix4x4 m)
        {
            var e = EntityManager.Get(owner);
            m = e ? e.Frame : Matrix4x4.identity;
            return e;
        }

        public void AddPatch(Entity owner, Vector3 worldPoint, Vector3 worldNormal, float size)
        {
            var inv = owner.Frame.inverse;
            P.patches.Add(new IvyPatch
            {
                o = owner.Id,
                p = inv.MultiplyPoint3x4(worldPoint),
                n = inv.MultiplyVector(worldNormal).normalized,
                s = size,
                seed = UnityEngine.Random.Range(0, 1000000)
            });
            pending = 0f;
        }

        public bool HasNear(Vector3 world, float r)
        {
            foreach (var pt in P.patches)
                if (OwnerFrame(pt.o, out var m) && (m.MultiplyPoint3x4(pt.p) - world).sqrMagnitude < r * r) return true;
            return false;
        }

        public int RemoveNear(Vector3 world, float r)
        {
            int removed = P.patches.RemoveAll(pt => OwnerFrame(pt.o, out var m) && (m.MultiplyPoint3x4(pt.p) - world).sqrMagnitude < r * r);
            if (removed > 0) pending = 0f;
            return removed;
        }

        // ---------- Dựng hình ----------
        public override void Rebuild()
        {
            pending = -1f;
            var root = NewVisual();
            P.patches ??= new List<IvyPatch>();
            if (P.patches.Count == 0) return;

            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            foreach (var pt in P.patches)
            {
                if (!OwnerFrame(pt.o, out var m)) continue;   // vật bám đã bị xoá
                var wp = m.MultiplyPoint3x4(pt.p);
                var wn = m.MultiplyVector(pt.n).normalized;
                Patch(v, n, c, t, wp, wn, pt.s, pt.seed);
            }
            if (v.Count == 0) return;

            var mesh = new Mesh { name = "Ivy" };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            var g = new GameObject("IvyMesh");
            g.transform.SetParent(root.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = Palette.VertexColorMat();
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Patch(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t, Vector3 p, Vector3 nrm, float size, int seed)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            var t1 = Vector3.Cross(nrm, Vector3.up);
            if (t1.sqrMagnitude < 1e-4f) t1 = Vector3.Cross(nrm, Vector3.right);
            t1.Normalize();
            var t2 = Vector3.Cross(t1, nrm).normalized;   // ~ hướng lên trên mặt tường
            if (Vector3.Dot(t2, Vector3.up) < 0f) t2 = -t2;
            int count = Mathf.RoundToInt(16 * size);
            for (int i = 0; i < count; i++)
            {
                float u = (R() - 0.5f) * 0.9f * size;
                float w = (R() * 0.95f - 0.65f) * size;   // rủ xuống nhiều hơn
                var center = p + t1 * u + t2 * w + nrm * (0.03f + R() * 0.05f);
                float ls = (0.09f + R() * 0.07f) * Mathf.Sqrt(size);
                float ang = R() * Mathf.PI * 2f;
                var a1 = (t1 * Mathf.Cos(ang) + t2 * Mathf.Sin(ang)) * ls;
                var a2 = (t1 * -Mathf.Sin(ang) + t2 * Mathf.Cos(ang)) * ls * 0.65f;
                bool bloom = R() < 0.05f;
                var col = bloom ? Blossoms[rng.Next(Blossoms.Length)] : Leaves[rng.Next(Leaves.Length)];
                if (bloom) { a1 *= 0.6f; a2 = a2.normalized * a1.magnitude; }
                int b = v.Count;
                v.Add(center - a1); v.Add(center + a2); v.Add(center + a1); v.Add(center - a2);
                for (int k = 0; k < 4; k++) { n.Add(nrm); c.Add(col); }
                t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
                t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2);
            }
        }
    }
}
