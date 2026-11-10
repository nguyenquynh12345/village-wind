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
        enum Mode { Idle, Pending, Body, Handle, Box }

        static readonly Color HoverTint = new Color(1f, 1f, 1f, 0.3f);
        static readonly Color SelectTint = new Color(0.45f, 0.7f, 1f, 0.35f);

        int selId, selPart = -1, hovId, hovPart = -1;
        int pendingClickId, pendingClickPart = -1;
        readonly List<int> multiSel = new();
        readonly Dictionary<int, string> multiBefore = new();
        Mode mode;
        Vector2 pressPos, boxCurPos;
        Vector3 lastGround, axisAnchor;
        float axisStartY;
        string before, handleKey;
        readonly HandleSet handles = new();
        readonly List<HandleDef> defs = new();

        public override string Name => "Bàn tay";
        public override string Key => "V";
        public override bool Busy => mode == Mode.Body || mode == Mode.Handle || mode == Mode.Box || Clipboard.IsPasting;
        public bool HasSelection => (selId != 0 && EntityManager.Get(selId)) || multiSel.Count > 0 || selDetail || Clipboard.IsPasting;
        public int MultiCount => multiSel.Count;
        public bool IsBoxSelecting => mode == Mode.Box && (boxCurPos - pressPos).magnitude > GameConfig.DragThresholdPx;

        public Rect BoxGuiRect
        {
            get
            {
                float sh = Screen.height;
                var a = new Vector2(pressPos.x, sh - pressPos.y);
                var b = new Vector2(boxCurPos.x, sh - boxCurPos.y);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
        }

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

        public override string Hint => Clipboard.IsPasting
            ? "Click: đặt cụm tại đây  ·  Esc / Chuột phải: huỷ dán"
            : Interior.Active
            ? "Trong nhà: kéo đồ đạc để xếp  ·  R: xoay  ·  Delete: xoá  ·  PageUp/PageDown: đổi tầng  ·  I / Esc: ra ngoài"
            : multiSel.Count > 0
            ? $"Đã chọn {multiSel.Count} vật  ·  Kéo vật: di chuyển  ·  R: xoay quanh tâm  ·  Ctrl+C: sao chép  ·  Ctrl+D: nhân bản  ·  Delete: xoá  ·  Esc: bỏ chọn"
            : Selected
            ? "Kéo vật: di chuyển  ·  Kéo tay nắm: chỉnh  ·  R: xoay  ·  Ctrl+C: sao chép  ·  Ctrl+D: nhân bản  ·  Delete: xoá  ·  Esc: bỏ chọn"
            : "Click vào vật để chọn  ·  Ctrl+kéo: chọn nhiều vật  ·  Ctrl+click: thêm/bớt vật";

        public override void Exit()
        {
            Clipboard.CancelPaste();
            CancelDrag();
            SetHover(0, -1);
            Select(0, -1);
            ClearMulti();
            HoverDetail(null);
            SelectDetail(null);
            handles.Hide();
        }

        public override void Update(ToolContext c)
        {
            for (int i = multiSel.Count - 1; i >= 0; i--)
                if (!EntityManager.Get(multiSel[i])) multiSel.RemoveAt(i);

            if (Clipboard.IsPasting)
            {
                handles.Hide();
                SetHover(0, -1);
                HoverDetail(null);
                Clipboard.UpdatePaste(c, ids =>
                {
                    ClearMulti();
                    Select(0, -1);
                    if (ids.Count == 1) Select(ids[0], -1);
                    else foreach (int id in ids) AddMulti(id);
                });
                return;
            }

            var sel = Selected;
            if (selId != 0 && (!sel || !sel.HasPart(selPart))) { selId = 0; selPart = -1; mode = Mode.Idle; sel = null; }

            switch (mode)
            {
                case Mode.Handle: UpdateHandleDrag(c, sel); break;
                case Mode.Body: UpdateBodyDrag(c, sel); break;
                case Mode.Pending: UpdatePending(c, sel); break;
                case Mode.Box: UpdateBox(c); break;
                default: UpdateIdle(c, sel); break;
            }

            // Tô màu rê / chọn
            sel = Selected;
            var hov = EntityManager.Get(hovId);
            if (hov && !(hovId == selId && hovPart == selPart) && !multiSel.Contains(hovId)) hov.SetPartTint(hovPart, HoverTint);
            if (sel) sel.SetPartTint(selPart, SelectTint);
            for (int i = 0; i < multiSel.Count; i++)
            {
                var me = EntityManager.Get(multiSel[i]);
                if (me) me.SetTint(SelectTint);
            }

            // Tay nắm (khi chọn nhiều vật thì ẩn tay nắm của từng vật)
            if (sel && multiSel.Count == 0)
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
            if (multiSel.Count > 0) hv = null;
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
                if (Input.GetKeyDown(KeyCode.Escape)) { SelectDetail(null); GameUI.EscConsumedFrame = Time.frameCount; return; }
            }

            if (multiSel.Count > 0)
            {
                if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
                {
                    ActionDelete();
                    return;
                }
                float wheel = Input.mouseScrollDelta.y;
                if (c.shift && Mathf.Abs(wheel) > 0.01f && !c.overUI)
                    RotateMulti(wheel > 0 ? 15f : -15f);
                if (Input.GetKeyDown(KeyCode.R))
                    RotateMulti(c.shift ? -15f : 15f);
                if (c.ctrl && Input.GetKeyDown(KeyCode.D))
                {
                    ActionDuplicate();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ClearMulti();
                    GameUI.EscConsumedFrame = Time.frameCount;
                    return;
                }
            }
            else if (sel)
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
                if (Input.GetKeyDown(KeyCode.Escape)) { Select(0, -1); GameUI.EscConsumedFrame = Time.frameCount; return; }
            }

            if (!Input.GetMouseButtonDown(0) || c.overUI) return;

            if (hv && sel && multiSel.Count == 0)
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

            if (c.ctrl)
            {
                if (e && !Clipboard.IsGlobal(e))
                {
                    if (Interior.Active) Interior.Exit(false);
                    SelectDetail(null);
                    if (selId != 0)
                    {
                        var oldSel = EntityManager.Get(selId);
                        int oldId = selId;
                        Select(0, -1);
                        if (oldSel && !Clipboard.IsGlobal(oldSel)) AddMulti(oldId);
                    }
                    ToggleMulti(e.Id);
                    return;
                }
                if (!e)
                {
                    if (Interior.Active) Interior.Exit(false);
                    mode = Mode.Box;
                    pressPos = Input.mousePosition;
                    boxCurPos = pressPos;
                    return;
                }
            }

            if (!e && hovDetail)
            {
                ClearMulti();
                Select(0, -1);
                SelectDetail(hovDetail);
                return;
            }
            SelectDetail(null);

            if (e)
            {
                // Đang xem trong nhà mà click vật khác → ra ngoài
                if (Interior.Active && e.Id != Interior.HouseId) Interior.Exit(false);
                if (multiSel.Count > 0 && multiSel.Contains(e.Id))
                {
                    pendingClickId = e.Id;
                    pendingClickPart = part;
                    mode = Mode.Pending;
                    pressPos = Input.mousePosition;
                    return;
                }
                ClearMulti();
                Select(e.Id, part);
                pendingClickId = 0;
                mode = Mode.Pending;
                pressPos = Input.mousePosition;
                return;
            }
            if (Interior.Active) Interior.Exit(false);
            ClearMulti();
            Select(0, -1);
        }

        // ---------- Thao tác nhanh (nút trên chip chọn vật / phím tắt) ----------
        public bool CanAct => mode == Mode.Idle && (Selected || multiSel.Count > 0 || selDetail);
        public bool SelectedCanDuplicate
        {
            get
            {
                if (multiSel.Count > 0) return true;
                var s = Selected;
                return s && s.CanDuplicate;
            }
        }

        public void CopySelection()
        {
            if (multiSel.Count > 0)
            {
                var list = new List<Entity>(multiSel.Count);
                foreach (int id in multiSel)
                {
                    var e = EntityManager.Get(id);
                    if (e) list.Add(e);
                }
                Clipboard.Copy(list);
                return;
            }
            var sel = Selected;
            if (sel && !Clipboard.IsGlobal(sel) && sel.CanDuplicate)
                Clipboard.Copy(new[] { sel });
        }

        public void StartPaste()
        {
            if (!Clipboard.HasData) return;
            CancelDrag();
            SetHover(0, -1);
            Select(0, -1);
            ClearMulti();
            HoverDetail(null);
            SelectDetail(null);
            Clipboard.BeginPaste();
        }

        public void ActionRotate(bool fine = false)
        {
            if (mode != Mode.Idle) return;
            if (multiSel.Count > 0)
            {
                RotateMulti(fine ? -15f : 15f);
                return;
            }
            var sel = Selected;
            if (!sel || !sel.CanRotate) return;
            var b = sel.Capture();
            sel.Rotate(fine ? -15f : 15f);
            sel.EndEdit();
            PushModify(sel, b);
        }

        public void ActionDuplicate()
        {
            if (mode != Mode.Idle) return;
            if (multiSel.Count > 0)
            {
                var cmd = new CompositeCommand();
                var newIds = new List<int>();
                foreach (int id in multiSel)
                {
                    var ent = EntityManager.Get(id);
                    if (!ent || !ent.CanDuplicate || Clipboard.IsGlobal(ent)) continue;
                    var dup = EntityManager.Spawn(ent.TypeName, ent.Capture(), 0, 0f);
                    dup.Translate(new Vector3(2f, 0, 2f));
                    cmd.Add(new CreateEntityCommand(dup.TypeName, dup.Capture(), -1f, dup.Id));
                    newIds.Add(dup.Id);
                }
                if (cmd.Count > 0)
                {
                    History.Push(cmd);
                    ClearMulti();
                    foreach (int id in newIds) AddMulti(id);
                }
                return;
            }
            var sel = Selected;
            if (!sel || !sel.CanDuplicate) return;
            var d = EntityManager.Spawn(sel.TypeName, sel.Capture(), 0, 0f);
            d.Translate(new Vector3(2f, 0, 2f));
            History.Push(new CreateEntityCommand(d.TypeName, d.Capture(), -1f, d.Id));
            Select(d.Id, -1);
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
            if (multiSel.Count > 0)
            {
                var cmd = new CompositeCommand();
                foreach (int id in multiSel)
                {
                    var ent = EntityManager.Get(id);
                    if (ent) cmd.Add(new DeleteEntityCommand(ent));
                }
                ClearMulti();
                if (cmd.Count > 0) History.Execute(cmd);
                return;
            }
            var sel = Selected;
            if (!sel) return;
            var before = sel.Capture();
            if (sel.DeletePart(selPart)) PushModify(sel, before);
            else History.Execute(new DeleteEntityCommand(sel));
            Select(0, -1);
        }

        void RotateMulti(float deg)
        {
            var ents = new List<(Entity e, Vector3 pos)>();
            Vector3 center = Vector3.zero;
            foreach (int id in multiSel)
            {
                var e = EntityManager.Get(id);
                if (!e) continue;
                var p = Clipboard.EntityCenter(e);
                ents.Add((e, p));
                center += p;
            }
            if (ents.Count == 0) return;
            center /= ents.Count;
            center.y = 0f;

            var q = Quaternion.Euler(0f, deg, 0f);
            var cmd = new CompositeCommand();
            foreach (var (e, p) in ents)
            {
                var b = e.Capture();
                if (e.CanRotate) e.Rotate(deg);
                var rel = new Vector3(p.x - center.x, 0f, p.z - center.z);
                var delta = q * rel - rel;
                if (delta.sqrMagnitude > 1e-6f) e.Translate(delta);
                e.EndEdit();
                var side = e.TakeSideEffects();
                if (side != null) foreach (var s in side) cmd.Add(s);
                var after = e.Capture();
                if (after != b) cmd.Add(new ModifyEntityCommand(e.Id, b, after));
            }
            if (cmd.Count > 0) History.Push(cmd);
        }

        /// <summary>Chọn từ bên ngoài (ví dụ món đồ vừa thêm từ thanh nội thất).</summary>
        public void SelectExternal(int id, int part)
        {
            if (mode != Mode.Idle) return;
            ClearMulti();
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

        void UpdateBox(ToolContext c)
        {
            boxCurPos = Input.mousePosition;
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                mode = Mode.Idle;
                GameUI.EscConsumedFrame = Time.frameCount;
                return;
            }
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
            {
                if ((boxCurPos - pressPos).magnitude > GameConfig.DragThresholdPx)
                {
                    Select(0, -1);
                    SelectDetail(null);
                    ClearMulti();
                    var r = Rect.MinMaxRect(
                        Mathf.Min(pressPos.x, boxCurPos.x),
                        Mathf.Min(pressPos.y, boxCurPos.y),
                        Mathf.Max(pressPos.x, boxCurPos.x),
                        Mathf.Max(pressPos.y, boxCurPos.y));
                    foreach (var ent in EntityManager.All)
                    {
                        if (!ent || Clipboard.IsGlobal(ent)) continue;
                        var rs = ent.GetComponentsInChildren<Renderer>();
                        if (rs.Length == 0) continue;
                        var bounds = rs[0].bounds;
                        for (int i = 1; i < rs.Length; i++) bounds.Encapsulate(rs[i].bounds);
                        var sp = c.cam.WorldToScreenPoint(bounds.center);
                        if (sp.z > 0f && r.Contains(new Vector2(sp.x, sp.y)))
                            AddMulti(ent.Id);
                    }
                }
                mode = Mode.Idle;
            }
        }

        void UpdatePending(ToolContext c, Entity sel)
        {
            if (multiSel.Count > 0)
            {
                if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
                {
                    if (pendingClickId != 0)
                    {
                        ClearMulti();
                        Select(pendingClickId, pendingClickPart);
                        pendingClickId = 0;
                    }
                    mode = Mode.Idle;
                    return;
                }
                if (((Vector2)Input.mousePosition - pressPos).magnitude > GameConfig.DragThresholdPx && c.Ground(out var g))
                {
                    mode = Mode.Body;
                    multiBefore.Clear();
                    foreach (int id in multiSel)
                    {
                        var ent = EntityManager.Get(id);
                        if (ent) multiBefore[id] = ent.Capture();
                    }
                    lastGround = g;
                }
                return;
            }
            if (!sel || Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) { mode = Mode.Idle; return; }
            if (((Vector2)Input.mousePosition - pressPos).magnitude > GameConfig.DragThresholdPx && DragPoint(c, sel, out var g0))
            {
                mode = Mode.Body;
                before = sel.Capture();
                lastGround = g0;
            }
        }

        void UpdateBodyDrag(ToolContext c, Entity sel)
        {
            if (multiSel.Count > 0)
            {
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelDrag();
                    GameUI.EscConsumedFrame = Time.frameCount;
                    return;
                }
                if (c.Ground(out var g))
                {
                    var d = g - lastGround;
                    d.y = 0;
                    if (d.sqrMagnitude > 1e-6f)
                    {
                        foreach (int id in multiSel)
                            EntityManager.Get(id)?.Translate(d);
                        lastGround = g;
                    }
                }
                if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
                {
                    var cmd = new CompositeCommand();
                    foreach (int id in multiSel)
                    {
                        var ent = EntityManager.Get(id);
                        if (!ent || !multiBefore.TryGetValue(id, out var bef)) continue;
                        ent.EndEdit();
                        var side = ent.TakeSideEffects();
                        if (side != null) foreach (var s in side) cmd.Add(s);
                        var aft = ent.Capture();
                        if (aft != bef) cmd.Add(new ModifyEntityCommand(id, bef, aft));
                    }
                    if (cmd.Count > 0) History.Push(cmd);
                    multiBefore.Clear();
                    mode = Mode.Idle;
                }
                return;
            }

            if (!sel) { mode = Mode.Idle; return; }
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { CancelDrag(); return; }
            if (DragPoint(c, sel, out var g1))
            {
                var d = g1 - lastGround;
                d.y = 0;
                if (d.sqrMagnitude > 1e-6f)
                {
                    sel.Translate(d);
                    lastGround = g1;
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
            if (mode == Mode.Body && multiSel.Count > 0)
            {
                foreach (var kv in multiBefore)
                    EntityManager.Get(kv.Key)?.Restore(kv.Value);
                multiBefore.Clear();
                mode = Mode.Idle;
                return;
            }
            var sel = Selected;
            if ((mode == Mode.Body || mode == Mode.Handle) && sel && before != null) sel.Restore(before);
            mode = Mode.Idle;
        }

        void AddMulti(int id)
        {
            if (id == 0 || multiSel.Contains(id)) return;
            var e = EntityManager.Get(id);
            if (!e || Clipboard.IsGlobal(e)) return;
            multiSel.Add(id);
            e.SetTint(SelectTint);
        }

        void ToggleMulti(int id)
        {
            if (id == 0) return;
            if (multiSel.Contains(id))
            {
                multiSel.Remove(id);
                EntityManager.Get(id)?.SetTint(null);
            }
            else AddMulti(id);
        }

        void ClearMulti()
        {
            foreach (int id in multiSel)
                EntityManager.Get(id)?.SetTint(null);
            multiSel.Clear();
            multiBefore.Clear();
            pendingClickId = 0;
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
            if (old && !(hovId == selId && hovPart == selPart) && !multiSel.Contains(hovId)) old.SetPartTint(hovPart, null);
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
