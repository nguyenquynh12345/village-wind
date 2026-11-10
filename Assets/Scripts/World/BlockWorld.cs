using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    [Serializable]
    public class BlockParams
    {
        public List<Piece> pieces = new();
        public int nextId = 1;
    }

    /// <summary>
    /// Tất cả mảnh ghép tự do (công cụ Khối) nằm trong 1 vật duy nhất, luôn tồn tại.
    /// Hình gộp theo cụm 8 × 8 m (1 lưới + 1 va chạm mỗi cụm) để hàng nghìn mảnh vẫn nhẹ.
    /// Part = id của mảnh → Bàn tay / Xóa chọn, kéo, xoay, xoá được từng mảnh.
    /// </summary>
    public class BlockWorld : Entity<BlockParams>
    {
        public static BlockWorld I;
        const float ChunkSize = 8f;

        class Chunk
        {
            public GameObject go;
            public Mesh mesh;
            public MeshCollider col;
            public readonly List<int> triStart = new(), triPiece = new();
        }

        readonly Dictionary<Vector2Int, Chunk> chunks = new();
        readonly Dictionary<Collider, Chunk> byCollider = new();
        readonly Dictionary<Vector2Int, List<Piece>> buckets = new();
        readonly Dictionary<int, Piece> byId = new();
        readonly HashSet<Vector2Int> dirty = new();
        readonly Dictionary<int, Color> tints = new();
        static Material overlayMat;
        static MaterialPropertyBlock mpb;

        Piece startPiece;
        bool rawValid;
        int rawPart;
        Vector3 raw;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        void Awake() => I = this;
        void OnDestroy() { if (I == this) I = null; }

        public override string TypeName => "Blocks";
        public int Count => P.pieces?.Count ?? 0;

        public override string Describe
        {
            get
            {
                var p = Get(ActivePart);
                return p != null
                    ? $"{BlockCatalog.Describe(p)}  ·  R: xoay 90° (Shift: 15°)  ·  🔶 đổi loại / chất liệu"
                    : $"Khối tự do · {Count} mảnh";
            }
        }

        // ================= Truy vấn =================
        public Piece Get(int id) => id >= 0 && byId.TryGetValue(id, out var p) ? p : null;

        static Vector2Int KeyOf(float x, float z) => new(Mathf.FloorToInt(x / ChunkSize), Mathf.FloorToInt(z / ChunkSize));
        static Vector2Int KeyOf(Piece p) => KeyOf(p.x, p.z);

        public static void BoxOf(Piece p, out Obb o, out float y0, out float y1)
        {
            var s = BlockCatalog.Size(p.t);
            o = Obb.From(p.x, p.z, p.yaw, s.x, s.z);
            y0 = p.y;
            y1 = p.y + s.y;
        }

        public static Vector3 Center(Piece p) => new(p.x, p.y + BlockCatalog.Size(p.t).y * 0.5f, p.z);

        IEnumerable<Piece> Near(float x, float z)
        {
            var k = KeyOf(x, z);
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                    if (buckets.TryGetValue(new Vector2Int(k.x + i, k.y + j), out var list))
                        foreach (var p in list) yield return p;
        }

        /// <summary>Mảnh mới có chồng lên mảnh khác không (chạm mặt thì được).</summary>
        public bool Overlaps(Piece c, int ignoreId = -1)
        {
            BoxOf(c, out var o, out float a0, out float a1);
            foreach (var p in Near(c.x, c.z))
            {
                if (p.id == ignoreId) continue;
                BoxOf(p, out var q, out float b0, out float b1);
                if (a1 <= b0 + 0.03f || b1 <= a0 + 0.03f) continue;
                if (o.Overlaps(q, -0.03f)) return true;
            }
            return false;
        }

        /// <summary>Có mảnh nào đặt sát đất tại (x, z) không — để cây / cỏ tránh.</summary>
        public bool CoversGround(Vector2 q, float margin)
        {
            var hf = HeightField.I;
            foreach (var p in Near(q.x, q.y))
            {
                BoxOf(p, out var o, out float y0, out _);
                if (y0 > hf.HeightAt(p.x, p.z) + 0.8f) continue;
                if (o.Contains(q, margin)) return true;
            }
            return false;
        }

        public IEnumerable<Piece> All => P.pieces;

        public Piece PieceFromHit(RaycastHit h)
        {
            if (!h.collider || !byCollider.TryGetValue(h.collider, out var ch)) return null;
            int tri = h.triangleIndex;
            if (tri < 0 || ch.triStart.Count == 0) return null;
            int lo = 0, hi = ch.triStart.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (ch.triStart[mid] <= tri) lo = mid; else hi = mid - 1;
            }
            return Get(ch.triPiece[lo]);
        }

        public bool IsBlockCollider(Collider c) => c && byCollider.ContainsKey(c);

        // ================= Sửa =================
        public int Add(Piece p)
        {
            p.id = P.nextId++;
            P.pieces.Add(p);
            Index(p);
            return p.id;
        }

        public bool Remove(int id)
        {
            var p = Get(id);
            if (p == null) return false;
            P.pieces.Remove(p);
            byId.Remove(id);
            var k = KeyOf(p);
            if (buckets.TryGetValue(k, out var list)) list.Remove(p);
            dirty.Add(k);
            tints.Remove(id);
            return true;
        }

        /// <summary>Gọi sau khi đổi vị trí / loại / xoay của 1 mảnh (oldKey = cụm trước khi đổi).</summary>
        public void Changed(Piece p, Vector2Int oldKey)
        {
            var k = KeyOf(p);
            if (k != oldKey)
            {
                if (buckets.TryGetValue(oldKey, out var list)) list.Remove(p);
                if (!buckets.TryGetValue(k, out var nl)) buckets[k] = nl = new List<Piece>();
                nl.Add(p);
                dirty.Add(oldKey);
            }
            dirty.Add(k);
        }

        public Vector2Int ChunkOf(Piece p) => KeyOf(p);

        void Index(Piece p)
        {
            byId[p.id] = p;
            var k = KeyOf(p);
            if (!buckets.TryGetValue(k, out var list)) buckets[k] = list = new List<Piece>();
            list.Add(p);
            dirty.Add(k);
        }

        // ================= Dựng hình =================
        public override void Rebuild()
        {
            NewVisual();
            chunks.Clear();
            byCollider.Clear();
            buckets.Clear();
            byId.Clear();
            dirty.Clear();
            P.pieces ??= new List<Piece>();
            foreach (var p in P.pieces)
            {
                Index(p);
                P.nextId = Mathf.Max(P.nextId, p.id + 1);
            }
            Flush();
        }

        void LateUpdate()
        {
            if (dirty.Count > 0) Flush();
            DrawTints();
        }

        void Flush()
        {
            var keys = new List<Vector2Int>(dirty);
            dirty.Clear();
            foreach (var k in keys)
            {
                BuildChunk(k);
                EntityManager.NotifyTrees(new Rect(k.x * ChunkSize - 1f, k.y * ChunkSize - 1f, ChunkSize + 2f, ChunkSize + 2f));
            }
        }

        void BuildChunk(Vector2Int k)
        {
            buckets.TryGetValue(k, out var list);
            chunks.TryGetValue(k, out var ch);
            if (list == null || list.Count == 0)
            {
                if (ch != null)
                {
                    if (ch.col) byCollider.Remove(ch.col);
                    if (ch.go) Destroy(ch.go);
                    chunks.Remove(k);
                }
                return;
            }
            if (ch == null || !ch.go)
            {
                ch = new Chunk();
                ch.go = new GameObject($"Blocks {k.x},{k.y}");
                ch.go.transform.SetParent(visual.transform, false);
                ch.go.transform.position = new Vector3(k.x * ChunkSize, 0, k.y * ChunkSize);
                ch.mesh = new Mesh { name = "Blocks" };
                ch.mesh.MarkDynamic();
                ch.go.AddComponent<MeshFilter>().sharedMesh = ch.mesh;
                var r = ch.go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Palette.VertexColorMat();
                r.shadowCastingMode = ShadowCastingMode.On;
                ch.col = ch.go.AddComponent<MeshCollider>();
                chunks[k] = ch;
                byCollider[ch.col] = ch;
            }

            var origin = ch.go.transform.position;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            ch.triStart.Clear();
            ch.triPiece.Clear();
            foreach (var p in list)
            {
                var sh = BlockCatalog.Shape(p.t);
                var rot = Quaternion.Euler(0, p.yaw, 0);
                var pos = new Vector3(p.x, p.y, p.z) - origin;
                var mat = BlockCatalog.MatColor(p.m);
                ch.triStart.Add(t.Count / 3);
                ch.triPiece.Add(p.id);
                int b = v.Count;
                for (int i = 0; i < sh.v.Length; i++)
                {
                    v.Add(pos + rot * sh.v[i]);
                    n.Add(rot * sh.n[i]);
                    c.Add(BlockCatalog.Tint(sh.c[i], mat));
                }
                foreach (int idx in sh.t) t.Add(b + idx);
            }
            ch.mesh.Clear();
            ch.mesh.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            ch.mesh.SetVertices(v);
            ch.mesh.SetNormals(n);
            ch.mesh.SetColors(c);
            ch.mesh.SetTriangles(t, 0);
            ch.mesh.RecalculateBounds();
            ch.col.sharedMesh = null;
            ch.col.sharedMesh = ch.mesh;
        }

        // ================= Tô sáng mảnh (rê / chọn) =================
        public override void SetPartTint(int part, Color? c)
        {
            if (part < 0) return;
            if (c == null) tints.Remove(part); else tints[part] = c.Value;
        }

        void DrawTints()
        {
            if (tints.Count == 0) return;
            foreach (var kv in tints)
            {
                var p = Get(kv.Key);
                if (p == null) continue;
                var col = kv.Value;
                DrawGhost(p, new Color(col.r, col.g, col.b, Mathf.Clamp(col.a, 0.25f, 0.6f)));
            }
        }

        /// <summary>Vẽ 1 mảnh trong suốt (xem trước / tô sáng). Gọi mỗi frame.</summary>
        public static void DrawGhost(Piece p, Color color, float grow = 0.03f)
        {
            if (!overlayMat)
            {
                overlayMat = new Material(Shader.Find("Sprites/Default")) { renderQueue = 3100 };
            }
            mpb ??= new MaterialPropertyBlock();
            mpb.SetColor("_Color", color);
            var s = BlockCatalog.Size(p.t);
            var k = 1f + grow;
            var pos = new Vector3(p.x, p.y - s.y * grow * 0.5f, p.z);
            var m = Matrix4x4.TRS(pos, Quaternion.Euler(0, p.yaw, 0), new Vector3(k, k, k));
            Graphics.DrawMesh(BlockCatalog.SingleMesh(p.t, p.m), m, overlayMat, 0, null, 0, mpb, ShadowCastingMode.Off, false);
        }

        // ================= Part (từng mảnh) cho Bàn tay / Xóa =================
        public override int PartOfHit(RaycastHit h) => PieceFromHit(h)?.id ?? -1;
        public override bool HasPart(int part) => part < 0 || byId.ContainsKey(part);
        public override bool CanDuplicate => false;
        public override bool AffectedBy(Rect r) => false;
        public override void TerrainChangedIn(Rect r, bool light) { }

        /// <summary>Luôn true: không bao giờ xoá cả thế giới khối, chỉ xoá từng mảnh.</summary>
        public override bool DeletePart(int part)
        {
            if (part >= 0) Remove(part);
            return true;
        }

        static float Snap(float v, float step) => Mathf.Round(v / step) * step;

        public override void Translate(Vector3 d)
        {
            var p = Get(ActivePart);
            if (p == null) return;
            if (!rawValid || rawPart != p.id)
            {
                raw = new Vector3(p.x, p.y, p.z);
                rawValid = true;
                rawPart = p.id;
            }
            raw += new Vector3(d.x, 0, d.z);
            var old = KeyOf(p);
            float nx = Snap(raw.x, 0.25f), nz = Snap(raw.z, 0.25f);
            if (Mathf.Approximately(nx, p.x) && Mathf.Approximately(nz, p.z)) return;
            p.x = nx;
            p.z = nz;
            Changed(p, old);
        }

        public override void Rotate(float degrees)
        {
            var p = Get(ActivePart);
            if (p == null) return;
            bool fine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            p.yaw = Mathf.Repeat(p.yaw + Mathf.Sign(degrees) * (fine ? 15f : 90f), 360f);
            Changed(p, KeyOf(p));
        }

        public override void EndEdit() => rawValid = false;

        /// <summary>Kéo mảnh trên cao: chiếu chuột lên mặt phẳng ở đáy mảnh (không phải mặt đất).</summary>
        public override bool DragPlaneY(out float y)
        {
            var p = Get(ActivePart);
            y = p != null ? p.y : 0f;
            return p != null;
        }

        public override void GetHandles(List<HandleDef> list)
        {
            var p = Get(ActivePart);
            if (p == null) return;
            var s = BlockCatalog.Size(p.t);
            var rot = Quaternion.Euler(0, p.yaw, 0);
            var top = new Vector3(p.x, p.y + s.y + 0.6f, p.z);
            float r = Mathf.Max(s.x, s.z) * 0.5f + 0.4f;
            list.Add(new HandleDef("py", HandleKind.Arrow, top));
            list.Add(new HandleDef("pt", HandleKind.Cycle, top + rot * Vector3.right * r));
            list.Add(new HandleDef("pm", HandleKind.Cycle, top - rot * Vector3.right * r));
            list.Add(new HandleDef("pr", HandleKind.Cycle, top + rot * Vector3.forward * r));
        }

        public override void BeginHandle(string key) => startPiece = Get(ActivePart)?.Clone();

        public override void DragHandle(string key, HandleInput input)
        {
            var p = Get(ActivePart);
            if (p == null || startPiece == null || key != "py") return;
            float y = startPiece.y + Snap(input.axisDelta, 0.25f);
            if (Mathf.Approximately(y, p.y)) return;
            p.y = y;
            Changed(p, KeyOf(p));
        }

        public override void ClickHandle(string key)
        {
            var p = Get(ActivePart);
            if (p == null) return;
            switch (key)
            {
                case "pt": p.t = (p.t + 1) % BlockCatalog.Count; break;
                case "pm": p.m = (Mathf.Abs(p.m) + 1) % BlockCatalog.MatColors.Length; break;
                case "pr": p.yaw = Mathf.Repeat(p.yaw + 90f, 360f); break;
                default: return;
            }
            Changed(p, KeyOf(p));
        }
    }
}
