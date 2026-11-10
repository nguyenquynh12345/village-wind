using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Rê lên vật → sáng đỏ; click → xoá (hoàn tác được). Tường: xoá từng đoạn / từng tháp; nhà: từng cánh.
    /// Click chi tiết tổ hợp → tắt chi tiết đó. Giữ + kéo = cọ xoá cây và đường (không xoá nhà / tường để tránh nhầm).
    /// </summary>
    public class EraseTool : Tool
    {
        static readonly Color EraseTint = new Color(0.95f, 0.3f, 0.25f, 0.55f);
        static readonly Color BrushColor = new Color(0.95f, 0.35f, 0.3f, 0.5f);

        int hoverId, hoverPart = -1;
        ComboTag hoverDetail;
        bool pressed, brushing;
        Vector2 pressPos;
        float radius = 2f;
        CompositeCommand brushCmd;
        readonly GroundRing ring = new();

        public override string Name => "Xóa";
        public override string Key => "X";
        public override bool Busy => brushing;
        public override string Hint =>
            "Click: xoá vật (tường: từng đoạn, nhà: từng cánh) hoặc tắt chi tiết tự mọc  ·  Giữ + kéo: cọ xoá cây và đường  ·  [ / ]: cỡ cọ  ·  Z: hoàn tác";
        public override string Status => brushing ? $"Cọ xoá {radius:0.#} m" : null;

        public override void Exit()
        {
            FinishBrush();
            pressed = false;
            SetHover(0, -1);
            SetDetail(null);
            ring.Hide();
        }

        public override void Update(ToolContext c)
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket)) radius = Mathf.Max(1f, radius - 0.5f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) radius = Mathf.Min(6f, radius + 0.5f);
            bool has = c.Ground(out var gp);

            if (brushing)
            {
                if (has) { ring.Show(gp, radius, BrushColor); BrushAt(new Vector2(gp.x, gp.z)); }
                if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) FinishBrush();
                return;
            }

            if (has && !c.overUI && (pressed || c.shift)) ring.Show(gp, radius, BrushColor); else ring.Hide();

            // Đang nhấn: kéo đủ xa → chuyển sang cọ xoá
            if (pressed)
            {
                if (((Vector2)Input.mousePosition - pressPos).magnitude > GameConfig.DragThresholdPx * 2f && has)
                {
                    pressed = false;
                    brushing = true;
                    brushCmd = new CompositeCommand();
                    SetHover(0, -1);
                    SetDetail(null);
                    BrushAt(new Vector2(gp.x, gp.z));
                    return;
                }
                if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
                {
                    pressed = false;
                    ClickDelete();
                }
                return;
            }

            Entity e = null;
            int part = -1;
            if (!c.overUI) Picker.Pick(c.cam, out e, out part, out _);
            var det = !c.overUI ? Picker.PickComboOver(c.cam, e) : null;
            if (det) e = null;
            SetHover(e ? e.Id : 0, part);
            SetDetail(det);

            if (Input.GetMouseButtonDown(0) && !c.overUI)
            {
                pressed = true;
                pressPos = Input.mousePosition;
            }
        }

        void ClickDelete()
        {
            if (hoverDetail)
            {
                var key = hoverDetail.key;
                GameUI.Toast($"Đã tắt: {hoverDetail.Label}  ·  Z để hiện lại");
                SetDetail(null);
                History.Execute(new ComboSuppressCommand(true, key));
                return;
            }
            var h = EntityManager.Get(hoverId);
            if (!h) return;
            int p = hoverPart;
            SetHover(0, -1);
            var before = h.Capture();
            if (h.DeletePart(p))
            {
                var after = h.Capture();
                if (after != before) History.Push(new ModifyEntityCommand(h.Id, before, after));
            }
            else History.Execute(new DeleteEntityCommand(h));
        }

        void BrushAt(Vector2 p)
        {
            var doomed = new List<Entity>();
            foreach (var t in EntityManager.OfType<TreeEntity>())
                if (Vector2.Distance(t.XZ, p) <= radius) doomed.Add(t);
            foreach (var path in EntityManager.OfType<PathEntity>())
                if (path.Covers(p, radius * 0.5f)) doomed.Add(path);
            foreach (var e in doomed)
            {
                var del = new DeleteEntityCommand(e);
                del.Do();
                brushCmd.Add(del);
            }
        }

        void FinishBrush()
        {
            if (!brushing) return;
            brushing = false;
            if (brushCmd != null && brushCmd.Count > 0) History.Push(brushCmd);
            brushCmd = null;
        }

        void SetHover(int id, int part)
        {
            if (id == hoverId && part == hoverPart) return;
            var old = EntityManager.Get(hoverId);
            if (old) old.SetPartTint(hoverPart, null);
            hoverId = id;
            hoverPart = part;
            var now = EntityManager.Get(hoverId);
            if (now) now.SetPartTint(hoverPart, EraseTint);
        }

        void SetDetail(ComboTag t)
        {
            if (hoverDetail == t) return;
            if (hoverDetail) hoverDetail.SetTint(null);
            hoverDetail = t;
            if (hoverDetail) hoverDetail.SetTint(EraseTint);
        }
    }
}
