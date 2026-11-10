using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Công cụ mặc định: rê để xem, click để chọn, kéo vật để di chuyển, kéo tay nắm để chỉnh.
    /// Với tường thành, chọn được từng phần (1 tháp / 1 đoạn).
    /// R: xoay · Ctrl+D: nhân bản · Delete: xoá · Esc: bỏ chọn.
    /// </summary>
    public class HandTool : Tool
    {
        enum Mode { Idle, Pending, Body, Handle }

        static readonly Color HoverTint = new Color(1f, 1f, 1f, 0.3f);
        static readonly Color SelectTint = new Color(0.45f, 0.7f, 1f, 0.35f);

        int selId, selPart = -1, hovId, hovPart = -1;
        Mode mode;
        Vector2 pressPos;
        Vector3 lastGround, axisAnchor;
        float axisStartY;
        string before, handleKey;
        readonly HandleSet handles = new();
        readonly List<HandleDef> defs = new();

        public override string Name => "Bàn tay";
        public override string Key => "V";
        public override bool Busy => mode == Mode.Body || mode == Mode.Handle;
        public bool HasSelection => (selId != 0 && EntityManager.Get(selId)) || selDetail;

        // Chi tiết tổ hợp (cổng, cầu, giếng...): chọn được, Delete = tắt
        ComboTag selDetail, hovDetail;
        public string DetailLabel => selDetail ? selDetail.Label : null;

        void HoverDetail(ComboTag t)
        {
            if (hovDetail == t) return;
            if (hovDetail && hovDetail != selDetail) hovDetail.SetTint(null);
            hovDetail = t;
            if (hovDetail && hovDetail != selDetail) hovDetail.SetTint(HoverTint);
        }

        void SelectDetail(ComboTag t)
        {
            if (selDetail == t) return;
            if (selDetail) selDetail.SetTint(null);
            selDetail = t;
            if (selDetail) selDetail.SetTint(SelectTint);
        }

        /// <summary>Vật đang chọn (đã gán ActivePart = phần đang chọn).</summary>
        public Entity Selected
        {
            get
            {
                var e = EntityManager.Get(selId);
                if (e) e.ActivePart = selPart;
                return e;
            }
        }

        public override string Hint => Interior.Active
            ? "Trong nhà: kéo đồ đạc để xếp  ·  R: xoay  ·  Delete: xoá  ·  PageUp/PageDown: đổi tầng  ·  I / Esc: ra ngoài"
            : Selected
            ? "Kéo vật: di chuyển  ·  Kéo tay nắm: chỉnh  ·  R: xoay  ·  Ctrl+D: nhân bản  ·  Delete: xoá  ·  Esc: bỏ chọn"
            : "Click vào nhà, đoạn tường hoặc tháp để chọn và chỉnh sửa";

        public override void Exit()
        {
            CancelDrag();
            SetHover(0, -1);
            Select(0, -1);
            HoverDetail(null);
            SelectDetail(null);
            handles.Hide();
        }

        public override void Update(ToolContext c)
        {
            var sel = Selected;
            if (selId != 0 && (!sel || !sel.HasPart(selPart))) { selId = 0; selPart = -1; mode = Mode.Idle; sel = null; }

            switch (mode)
            {
                case Mode.Handle: UpdateHandleDrag(c, sel); break;
                case Mode.Body: UpdateBodyDrag(c, sel); break;
                case Mode.Pending: UpdatePending(c, sel); break;
                default: UpdateIdle(c, sel); break;
            }

            // Tô màu rê / chọn
            sel = Selected;
            var hov = EntityManager.Get(hovId);
            if (hov && !(hovId == selId && hovPart == selPart)) hov.SetPartTint(hovPart, HoverTint);
            if (sel) sel.SetPartTint(selPart, SelectTint);

            // Tay nắm
            if (sel)
            {
                defs.Clear();
                sel.GetHandles(defs);
                handles.Show(defs, c.cam, mode == Mode.Handle ? handleKey : null);
            }
            else handles.Hide();
        }

        void UpdateIdle(ToolContext c, Entity sel)
        {
            Picker.Pick(c.cam, out var e, out int part, out var hv);
            var det = !c.overUI && !hv ? Picker.PickComboOver(c.cam, e) : null;
            if (det) e = null;
            if (!c.overUI && e && !hv) SetHover(e.Id, part); else SetHover(0, -1);
            HoverDetail(det);

            if (selDetail)
            {
                if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
                {
                    GameUI.Toast($"Đã tắt: {selDetail.Label}  ·  Z để hiện lại");
                    var key = selDetail.key;
                    SelectDetail(null);
                    HoverDetail(null);
                    History.Execute(new ComboSuppressCommand(true, key));
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Escape)) { SelectDetail(null); return; }
            }

            if (sel)
            {
                if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
                {
                    var b = sel.Capture();
                    if (sel.DeletePart(selPart)) PushModify(sel, b);
                    else History.Execute(new DeleteEntityCommand(sel));
                    Select(0, -1);
                    return;
                }
                float wheel = Input.mouseScrollDelta.y;
                if (c.shift && Mathf.Abs(wheel) > 0.01f && sel.CanRotate && !c.overUI)
                {
                    var b = sel.Capture();
                    sel.Rotate(wheel > 0 ? 15f : -15f);
                    sel.EndEdit();
                    PushModify(sel, b);
                }
                if (Input.GetKeyDown(KeyCode.R) && sel.CanRotate)
                {
                    var b = sel.Capture();
                    sel.Rotate(c.shift ? -15f : 15f);
                    sel.EndEdit();
                    PushModify(sel, b);
                }
                if (c.ctrl && Input.GetKeyDown(KeyCode.D) && sel.CanDuplicate)
                {
                    var dup = EntityManager.Spawn(sel.TypeName, sel.Capture(), 0, 0f);
                    dup.Translate(new Vector3(2f, 0, 2f));
                    History.Push(new CreateEntityCommand(dup.TypeName, dup.Capture(), -1f, dup.Id));
                    Select(dup.Id, -1);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Escape)) { Select(0, -1); return; }
            }

            if (!Input.GetMouseButtonDown(0) || c.overUI) return;

            if (hv && sel)
            {
                if (hv.kind == HandleKind.Cycle)
                {
                    var b = sel.Capture();
                    sel.ClickHandle(hv.key);
                    sel.EndEdit();
                    PushModify(sel, b);
                    return;
                }
                mode = Mode.Handle;
                handleKey = hv.key;
                before = sel.Capture();
                axisAnchor = hv.transform.position;
                axisStartY = AxisY(c, axisAnchor);
                sel.BeginHandle(hv.key);
                return;
            }

            if (!e && hovDetail)
            {
                Select(0, -1);
                SelectDetail(hovDetail);
                return;
            }
            SelectDetail(null);

            if (e)
            {
                // Đang xem trong nhà mà click vật khác → ra ngoài
                if (Interior.Active && e.Id != Interior.HouseId) Interior.Exit(false);
                Select(e.Id, part);
                mode = Mode.Pending;
                pressPos = Input.mousePosition;
                return;
            }
            if (Interior.Active) Interior.Exit(false);
            Select(0, -1);
        }

        // ---------- Thao tác nhanh (nút trên chip chọn vật) ----------
        public bool CanAct => mode == Mode.Idle && (Selected || selDetail);
        public bool SelectedCanDuplicate { get { var s = Selected; return s && s.CanDuplicate; } }

        public void ActionRotate(bool fine = false)
        {
            var sel = Selected;
            if (mode != Mode.Idle || !sel || !sel.CanRotate) return;
            var b = sel.Capture();
            sel.Rotate(fine ? -15f : 15f);
            sel.EndEdit();
            PushModify(sel, b);
        }

        public void ActionDuplicate()
        {
            var sel = Selected;
            if (mode != Mode.Idle || !sel || !sel.CanDuplicate) return;
            var dup = EntityManager.Spawn(sel.TypeName, sel.Capture(), 0, 0f);
            dup.Translate(new Vector3(2f, 0, 2f));
            History.Push(new CreateEntityCommand(dup.TypeName, dup.Capture(), -1f, dup.Id));
            Select(dup.Id, -1);
        }

        public void ActionDelete()
        {
            if (mode != Mode.Idle) return;
            if (selDetail)
            {
                var key = selDetail.key;
                GameUI.Toast($"Đã tắt: {selDetail.Label}  ·  Z để hiện lại");
                SelectDetail(null);
                HoverDetail(null);
                History.Execute(new ComboSuppressCommand(true, key));
                return;
            }
            var sel = Selected;
            if (!sel) return;
            var before = sel.Capture();
            if (sel.DeletePart(selPart)) PushModify(sel, before);
            else History.Execute(new DeleteEntityCommand(sel));
            Select(0, -1);
        }

        /// <summary>Chọn từ bên ngoài (ví dụ món đồ vừa thêm từ thanh nội thất).</summary>
        public void SelectExternal(int id, int part)
        {
            if (mode != Mode.Idle) return;
            Select(id, part);
        }

        /// <summary>Điểm chuột khi kéo: mặt đất, hoặc mặt sàn nếu vật yêu cầu (đồ đạc trên lầu).</summary>
        static bool DragPoint(ToolContext c, Entity sel, out Vector3 g)
        {
            if (sel && sel.DragPlaneY(out float y))
            {
                var ray = c.Ray;
                var plane = new Plane(Vector3.up, new Vector3(0, y, 0));
                if (plane.Raycast(ray, out float dist)) { g = ray.GetPoint(dist); return true; }
                g = default;
                return false;
            }
            return c.Ground(out g);
        }

        void UpdatePending(ToolContext c, Entity sel)
        {
            if (!sel || Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) { mode = Mode.Idle; return; }
            if (((Vector2)Input.mousePosition - pressPos).magnitude > GameConfig.DragThresholdPx && DragPoint(c, sel, out var g))
            {
                mode = Mode.Body;
                before = sel.Capture();
                lastGround = g;
            }
        }

        void UpdateBodyDrag(ToolContext c, Entity sel)
        {
            if (!sel) { mode = Mode.Idle; return; }
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { CancelDrag(); return; }
            if (DragPoint(c, sel, out var g))
            {
                var d = g - lastGround;
                d.y = 0;
                if (d.sqrMagnitude > 1e-6f)
                {
                    sel.Translate(d);
                    lastGround = g;
                }
            }
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
            {
                sel.EndEdit();
                PushModify(sel, before);
                mode = Mode.Idle;
            }
        }

        void UpdateHandleDrag(ToolContext c, Entity sel)
        {
            if (!sel) { mode = Mode.Idle; return; }
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { CancelDrag(); return; }

            var input = new HandleInput { axisDelta = AxisY(c, axisAnchor) - axisStartY, shift = c.shift };
            input.hasGround = c.Ground(out var g);
            input.ground = g;
            sel.DragHandle(handleKey, input);

            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
            {
                sel.EndEdit();
                PushModify(sel, before);
                mode = Mode.Idle;
            }
        }

        void CancelDrag()
        {
            var sel = Selected;
            if ((mode == Mode.Body || mode == Mode.Handle) && sel && before != null) sel.Restore(before);
            mode = Mode.Idle;
        }

        void Select(int id, int part)
        {
            if (id == selId && part == selPart) return;
            var old = EntityManager.Get(selId);
            if (old) old.SetPartTint(selPart, null);
            selId = id;
            selPart = part;
            var now = EntityManager.Get(selId);
            if (now) now.ActivePart = selPart;
        }

        void SetHover(int id, int part)
        {
            if (hovId == id && hovPart == part) return;
            var old = EntityManager.Get(hovId);
            if (old && !(hovId == selId && hovPart == selPart)) old.SetPartTint(hovPart, null);
            hovId = id;
            hovPart = part;
        }

        static void PushModify(Entity e, string before)
        {
            var side = e.TakeSideEffects();
            var after = e.Capture();
            if (side == null || side.Count == 0)
            {
                if (after != before) History.Push(new ModifyEntityCommand(e.Id, before, after));
                return;
            }
            var cmd = new CompositeCommand();
            foreach (var s in side) cmd.Add(s);
            cmd.Add(new ModifyEntityCommand(e.Id, before, after));
            History.Push(cmd);
        }

        /// <summary>Độ cao của điểm trên trục đứng (qua anchor) gần tia chuột nhất.</summary>
        static float AxisY(ToolContext c, Vector3 anchor)
        {
            var ray = c.Ray;
            Vector3 u = Vector3.up, d = ray.direction, w0 = new Vector3(anchor.x, 0, anchor.z) - ray.origin;
            float b = Vector3.Dot(u, d), dd = Vector3.Dot(u, w0), e = Vector3.Dot(d, w0);
            float denom = 1f - b * b;
            if (denom < 1e-4f) return anchor.y;
            return (b * e - dd) / denom;
        }
    }
}
