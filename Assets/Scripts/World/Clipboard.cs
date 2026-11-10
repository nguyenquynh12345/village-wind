using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Bộ nhớ tạm sao chép / dán cụm vật (Ctrl+C / Ctrl+V).
    /// </summary>
    public static class Clipboard
    {
        static readonly Color OkTint = new Color(1f, 1f, 1f, 0.25f);
        static readonly Color BadTint = new Color(0.95f, 0.3f, 0.25f, 0.6f);

        struct Item
        {
            public string type;
            public string json;
            public Vector3 pos;
        }

        struct PreviewItem
        {
            public Entity entity;
            public Vector3 pos;
            public float baseY;
        }

        static readonly List<Item> items = new();
        static readonly List<PreviewItem> previews = new();
        static Vector3 center;
        static bool pasting;

        public static bool HasData => items.Count > 0;
        public static bool IsPasting => pasting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            CancelPaste();
            items.Clear();
            center = Vector3.zero;
        }

        public static bool IsGlobal(Entity e) =>
            !e || e is WallGraph || e is BlockWorld || e is IvyLayer;

        public static Vector3 EntityCenter(Entity e)
        {
            if (!e) return Vector3.zero;
            if (e is HouseEntity h) return new Vector3(h.P.x, 0f, h.P.z);
            if (e is TreeEntity t) return new Vector3(t.P.x, 0f, t.P.z);
            if (e is PropEntity p) return new Vector3(p.P.x, 0f, p.P.z);
            if (e is PathEntity pa && pa.P.pts != null && pa.P.pts.Count > 0)
            {
                Vector2 c = Vector2.zero;
                foreach (var pt in pa.P.pts) c += pt;
                c /= pa.P.pts.Count;
                return new Vector3(c.x, 0f, c.y);
            }
            if (e is RiverEntity rv && rv.P.pts != null && rv.P.pts.Count > 0)
            {
                Vector2 c = Vector2.zero;
                foreach (var pt in rv.P.pts) c += pt;
                c /= rv.P.pts.Count;
                return new Vector3(c.x, 0f, c.y);
            }
            if (e is PondEntity po && po.Mask != null)
            {
                var c = po.Mask.Centroid();
                return new Vector3(c.x, 0f, c.y);
            }
            var rs = e.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                return new Vector3(b.center.x, 0f, b.center.z);
            }
            return new Vector3(e.transform.position.x, 0f, e.transform.position.z);
        }

        public static void Copy(IEnumerable<Entity> entities)
        {
            CancelPaste();
            items.Clear();
            center = Vector3.zero;
            if (entities == null) return;
            foreach (var e in entities)
            {
                if (!e || IsGlobal(e) || !e.CanDuplicate) continue;
                var pos = EntityCenter(e);
                items.Add(new Item { type = e.TypeName, json = e.Capture(), pos = pos });
                center += pos;
            }
            if (items.Count == 0) return;
            center /= items.Count;
            center.y = 0f;
            GameUI.Toast($"Đã sao chép {items.Count} vật  ·  Ctrl+V để dán");
        }

        public static void BeginPaste()
        {
            CancelPaste();
            if (items.Count == 0) return;
            pasting = true;
            var hf = HeightField.I;
            foreach (var it in items)
            {
                var ent = EntityManager.CreateTemp(it.type, it.json);
                if (!ent) continue;
                foreach (var col in ent.GetComponentsInChildren<Collider>(true))
                    col.enabled = false;
                ent.SetTint(OkTint);
                float by = hf ? hf.HeightAt(it.pos.x, it.pos.z) : 0f;
                previews.Add(new PreviewItem { entity = ent, pos = it.pos, baseY = by });
            }
        }

        public static void CancelPaste()
        {
            pasting = false;
            foreach (var pv in previews)
            {
                if (!pv.entity) continue;
                pv.entity.gameObject.SetActive(false);
                Object.Destroy(pv.entity.gameObject);
            }
            previews.Clear();
        }

        public static void UpdatePaste(ToolContext c, System.Action<List<int>> onPlaced = null)
        {
            if (!pasting) return;
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPaste();
                GameUI.EscConsumedFrame = Time.frameCount;
                return;
            }
            if (!c.Ground(out var g)) return;
            var delta = new Vector3(g.x - center.x, 0f, g.z - center.z);

            bool houseOverlap = false;
            foreach (var pv in previews)
            {
                if (pv.entity is HouseEntity hp)
                {
                    var newCenter = new Vector2(hp.P.x + delta.x, hp.P.z + delta.z);
                    foreach (var existing in EntityManager.OfType<HouseEntity>())
                        if (existing.ContainsPoint(newCenter, 0f)) { houseOverlap = true; break; }
                    if (houseOverlap) break;
                }
            }
            var tint = houseOverlap ? BadTint : OkTint;
            var hf = HeightField.I;
            foreach (var pv in previews)
            {
                if (!pv.entity) continue;
                float ny = hf ? hf.HeightAt(pv.pos.x + delta.x, pv.pos.z + delta.z) : 0f;
                pv.entity.transform.position = new Vector3(delta.x, ny - pv.baseY, delta.z);
                pv.entity.SetTint(tint);
            }

            if (Input.GetMouseButtonDown(0) && !c.overUI)
            {
                var cmd = new CompositeCommand();
                var cmds = new List<CreateEntityCommand>();
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    var tmp = EntityManager.CreateTemp(it.type, it.json);
                    if (!tmp) continue;
                    tmp.Translate(delta);
                    string movedJson = tmp.Capture();
                    tmp.gameObject.SetActive(false);
                    Object.Destroy(tmp.gameObject);
                    float delay = Mathf.Min(i * 0.03f, 0.25f);
                    var cc = new CreateEntityCommand(it.type, movedJson, delay);
                    cmd.Add(cc);
                    cmds.Add(cc);
                }
                CancelPaste();
                if (cmd.Count > 0)
                {
                    History.Execute(cmd);
                    Sfx.Pop();
                    if (onPlaced != null)
                    {
                        var ids = new List<int>(cmds.Count);
                        foreach (var cc in cmds) if (cc.Id > 0) ids.Add(cc.Id);
                        onPlaced(ids);
                    }
                }
            }
        }
    }
}
