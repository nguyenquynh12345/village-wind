using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Cọ dây leo: giữ + kéo trên tường thành, nhà, khối để phủ dây leo · Alt + kéo: gỡ · [ / ]: mảng to / nhỏ.
    /// </summary>
    public class IvyTool : Tool
    {
        static readonly Color AddColor = new Color(0.45f, 0.7f, 0.4f, 0.6f);
        static readonly Color RemoveColor = new Color(0.95f, 0.35f, 0.3f, 0.5f);

        float size = 1f;
        bool painting, removing, changed;
        string before;
        readonly GroundRing ring = new();

        public override string Name => "Dây leo";
        public override string Key => "A";
        public override bool Busy => painting;
        public override string Hint => "Giữ + kéo lên tường thành / nhà / khối: phủ dây leo  ·  Alt + kéo: gỡ  ·  [ / ]: mảng to / nhỏ";
        public override string Status => $"Dây leo · cỡ {size:0.#}" + (IvyLayer.I ? $" · {IvyLayer.I.Count} mảng" : "");

        public override void Exit()
        {
            Finish();
            ring.Hide();
        }

        public override void Update(ToolContext c)
        {
            var ivy = IvyLayer.I;
            if (!ivy) return;
            if (Input.GetKeyDown(KeyCode.LeftBracket)) size = Mathf.Max(0.5f, size - 0.25f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) size = Mathf.Min(2.5f, size + 0.25f);

            bool has = Surface(c, out var hit, out var owner);
            bool alt = painting ? removing : c.alt;
            if (has && !c.overUI) ring.Show(hit.point, size * 0.5f, alt ? RemoveColor : AddColor); else ring.Hide();

            if (!painting)
            {
                if (!Input.GetMouseButtonDown(0) || c.overUI) return;
                painting = true;
                removing = c.alt;
                changed = false;
                before = ivy.Capture();
            }

            if (has)
            {
                if (removing)
                {
                    if (ivy.RemoveNear(hit.point, size * 0.6f) > 0) changed = true;
                }
                else if (!ivy.HasNear(hit.point, 0.35f * size))
                {
                    ivy.AddPatch(owner, hit.point, hit.normal, size);
                    changed = true;
                }
            }
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) Finish();
        }

        void Finish()
        {
            if (!painting) return;
            painting = false;
            var ivy = IvyLayer.I;
            if (changed && ivy && before != null) History.Push(new ModifyEntityCommand(ivy.Id, before, ivy.Capture()));
            before = null;
        }

        /// <summary>Mặt bám được: nhà, tường thành / tháp, khối xây.</summary>
        static bool Surface(ToolContext c, out RaycastHit hit, out Entity owner)
        {
            owner = null;
            var ray = c.Ray;
            float gd = HeightField.I.Raycast(ray, out var gp) ? Vector3.Distance(ray.origin, gp) : float.MaxValue;
            if (!Physics.Raycast(ray, out hit, 1000f) || hit.distance > gd + 0.05f) return false;
            owner = hit.collider.GetComponentInParent<Entity>();
            return owner && owner.Id != 0 && (owner is HouseEntity || owner is WallGraph || owner is BlockWorld);
        }
    }
}
