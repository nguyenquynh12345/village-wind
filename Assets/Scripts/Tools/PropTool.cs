using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Đặt đồ trang trí: click đặt 1 món · giữ + kéo = rải nhiều món (cách nhau ≥ 1.6 m) ·
    /// 1–9, 0: loại · R: xoay 45° (Shift: 15°). Trỏ lên mái bằng / mặt khối → đặt lên trên đó.
    /// </summary>
    public class PropTool : Tool
    {
        static readonly Color OkTint = new Color(1f, 1f, 1f, 0.2f);

        public static int Type = PropEntity.Lantern;
        float yaw;
        bool dragging;
        Vector3 lastPlaced;
        CompositeCommand cmd;
        PropEntity preview;
        int seed;

        public override string Name => "Đồ trang trí";
        public override string Key => "D";
        public override bool Busy => dragging;
        public override string Hint => "Click: đặt  ·  Giữ + kéo: rải nhiều món  ·  1–9, 0: loại  ·  R: xoay 45° (Shift: 15°)";
        public override string Status => $"{PropEntity.Names[Type]} · {yaw:0}°";

        public override void Enter() => seed = Random.Range(0, 100000);

        public override void Exit()
        {
            Finish();
            if (preview) { preview.gameObject.SetActive(false); Object.Destroy(preview.gameObject); }
            preview = null;
        }

        public override void Update(ToolContext c)
        {
            if (!c.ctrl)
            {
                for (int i = 0; i < 9; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Type = i;
                if (Input.GetKeyDown(KeyCode.Alpha0)) Type = 9;
                if (Input.GetKeyDown(KeyCode.R)) yaw = Mathf.Repeat(yaw + (c.shift ? 15f : 45f), 360f);
            }

            bool ok = Spot(c, out var p, out bool raised);
            ShowPreview(ok && !c.overUI, p, raised);

            if (!dragging)
            {
                if (!ok || c.overUI || !Input.GetMouseButtonDown(0)) return;
                dragging = true;
                cmd = new CompositeCommand();
                Place(p, raised);
                return;
            }

            if (ok && Vector3.Distance(Flat(p), Flat(lastPlaced)) >= 1.6f) Place(p, raised);
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) Finish();
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        void Place(Vector3 p, bool raised)
        {
            var pp = new PropParams { type = Type, x = p.x, z = p.z, yaw = yaw, raised = raised, y = p.y, seed = seed++ };
            var create = new CreateEntityCommand("Prop", JsonUtility.ToJson(pp), 0f);
            create.Do();
            cmd.Add(create);
            lastPlaced = p;
            if (dragging) yaw = Mathf.Repeat(yaw + Random.Range(-25f, 25f), 360f);   // rải: lệch góc chút cho tự nhiên
        }

        void Finish()
        {
            if (!dragging) return;
            dragging = false;
            if (cmd != null && cmd.Count > 0) History.Push(cmd);
            cmd = null;
        }

        static bool Spot(ToolContext c, out Vector3 p, out bool raised)
        {
            raised = false;
            var ray = c.Ray;
            bool hasGround = c.Ground(out p);
            float gd = hasGround ? Vector3.Distance(ray.origin, p) : float.MaxValue;
            var hits = Physics.RaycastAll(ray, 1000f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.distance > gd - 0.1f) break;
                if (h.collider.GetComponent<HandleView>() || h.collider.GetComponentInParent<PropEntity>()) continue;
                if (h.normal.y < 0.7f) break;      // trúng tường đứng → không đặt lên được
                p = h.point;
                raised = true;
                break;
            }
            return hasGround && WallTool.InBuildArea(p);
        }

        void ShowPreview(bool on, Vector3 p, bool raised)
        {
            if (!on)
            {
                if (preview) preview.gameObject.SetActive(false);
                return;
            }
            if (!preview)
            {
                var go = new GameObject("PropPreview");
                preview = go.AddComponent<PropEntity>();   // không đăng ký vào EntityManager
                foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;
            }
            preview.gameObject.SetActive(true);
            var pp = preview.P;
            if (pp.type != Type || pp.x != p.x || pp.z != p.z || pp.yaw != yaw || pp.raised != raised || pp.y != p.y)
            {
                preview.P = new PropParams { type = Type, x = p.x, z = p.z, yaw = yaw, raised = raised, y = p.y, seed = seed };
                preview.Rebuild();
                foreach (var col in preview.GetComponentsInChildren<Collider>()) col.enabled = false;
                preview.SetTint(OkTint);
            }
        }
    }
}
