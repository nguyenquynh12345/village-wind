using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Kéo chuột vẽ nền nhà: hướng kéo đầu tiên (&gt; 1 m) là chiều dài, xoay làm tròn 15°.
    /// Click không kéo: nhà mặc định 4 × 6 m. Xem trước nhà thật, đỏ nếu không đặt được.
    /// </summary>
    public class HouseTool : Tool
    {
        static readonly Color OkTint = new Color(1f, 1f, 1f, 0.25f);
        static readonly Color BadTint = new Color(0.95f, 0.3f, 0.25f, 0.6f);

        bool dragging, axisLocked, valid;
        Vector3 start;
        float axisAngle, defaultAngle;
        string reason;
        HouseParams cur;
        HouseEntity preview;
        HouseEntity target;        // đang kéo thêm cánh cho nhà này
        Wing wingCur;

        public override string Name => "Nhà";
        public override string Key => "B";
        public override bool Busy => dragging;
        public override string Hint =>
            "Kéo chuột: vẽ nền nhà (hướng kéo đầu = chiều dài)  ·  Click: nhà 4 × 6 m  ·  Kéo từ TRONG nhà cũ ra ngoài: thêm cánh (L / T / U)  ·  Esc: hủy";
        public override string Status => target && dragging
            ? (wingCur != null ? $"Cánh nhà {wingCur.w:0.#} × {wingCur.d:0.#} m" : "Kéo ra ngoài cạnh nhà") + (valid || wingCur == null ? "" : $" · {reason}")
            : dragging && cur != null
            ? $"{Size(cur.w, GameConfig.HouseMaxWidth):0.#} × {Size(cur.l, GameConfig.HouseMaxLength):0.#} m" + (valid ? "" : $" · {reason}")
            : null;

        static float Size(float v, float max) => Mathf.Clamp(v, GameConfig.HouseMinSize, max);

        public override void Exit() => Cancel();

        public override void Update(ToolContext c)
        {
            bool has = c.Ground(out var p);

            if (!dragging)
            {
                if (Input.GetMouseButtonDown(0) && !c.overUI && has && WallTool.InBuildArea(p))
                {
                    // Bắt đầu trong nền 1 nhà có sẵn → kéo thêm cánh
                    var p2 = new Vector2(p.x, p.z);
                    foreach (var h in EntityManager.OfType<HouseEntity>())
                        if (h.Footprint.Contains(p2, 0f))
                        {
                            if (h.P.wings != null && h.P.wings.Count >= HouseEntity.MaxWings)
                            {
                                GameUI.Toast($"Mỗi nhà tối đa {HouseEntity.MaxWings} cánh");
                                return;
                            }
                            dragging = true;
                            target = h;
                            start = new Vector3(p.x, 0, p.z);
                            EnsurePreview();
                            target.SetVisualActive(false);
                            UpdateWing(p);
                            return;
                        }
                    dragging = true;
                    axisLocked = false;
                    start = new Vector3(p.x, 0, p.z);
                    float yaw = c.cam.transform.eulerAngles.y;
                    defaultAngle = Snap(yaw + 90f, GameConfig.HouseAngleSnap);   // cạnh dài quay về camera
                    EnsurePreview();
                    UpdateShape(c, p);
                }
                return;
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }

            if (target)
            {
                if (has) UpdateWing(p);
                if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
                {
                    var h = target;
                    bool ok = valid && wingCur != null;
                    var json = ok ? JsonUtility.ToJson(cur) : null;
                    Cancel();
                    if (ok && h) History.Execute(new ModifyEntityCommand(h.Id, h.Capture(), json));
                }
                return;
            }

            if (has) UpdateShape(c, p);

            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
            {
                var json = JsonUtility.ToJson(cur);
                bool ok = valid;
                Cancel();
                if (ok) History.Execute(new CreateEntityCommand("House", json, 0f));
            }
        }

        void UpdateShape(ToolContext c, Vector3 p)
        {
            var d = new Vector3(p.x - start.x, 0, p.z - start.z);
            if (!axisLocked && d.magnitude > 1f)
            {
                axisAngle = Snap(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, GameConfig.HouseAngleSnap);
                axisLocked = true;
            }

            var hp = new HouseParams { seed = cur != null ? cur.seed : Random.Range(0, 100000) };
            if (!axisLocked)
            {
                hp.angle = defaultAngle;
                hp.w = 4f;
                hp.l = 6f;
                hp.x = start.x;
                hp.z = start.z;
            }
            else
            {
                var rot = Quaternion.Euler(0, axisAngle, 0);
                var local = Quaternion.Inverse(rot) * d;
                float L = Mathf.Clamp(Snap(Mathf.Abs(local.z), GameConfig.HouseSnap), GameConfig.HouseMinSize, GameConfig.HouseMaxLength);
                float W = Mathf.Clamp(Snap(Mathf.Abs(local.x), GameConfig.HouseSnap), GameConfig.HouseMinSize, GameConfig.HouseMaxWidth);
                float sx = local.x < 0 ? -1f : 1f, sz = local.z < 0 ? -1f : 1f;
                var center = start + rot * new Vector3(sx * W * 0.5f, 0, sz * L * 0.5f);
                hp.angle = Mathf.Repeat(axisAngle, 360f);
                hp.w = W;
                hp.l = L;
                hp.x = center.x;
                hp.z = center.z;
            }

            var toCam = c.cam.transform.position - new Vector3(hp.x, 0, hp.z);
            toCam.y = 0;
            hp.door = HouseEntity.SideFacing(hp, toCam, true);
            // Có đường đi gần → cửa quay về phía đường
            float bestD = 12f;
            foreach (var path in EntityManager.OfType<PathEntity>())
            {
                if (path.P.pts == null || path.P.pts.Count < 2) continue;
                Geo.Closest(path.P.pts, new Vector2(hp.x, hp.z), out _, out var q, out float dd);
                if (dd >= bestD) continue;
                bestD = dd;
                hp.door = HouseEntity.SideFacing(hp, new Vector3(q.x - hp.x, 0, q.y - hp.z), true);
            }

            cur = hp;
            valid = HouseEntity.CanPlace(hp, 0, out reason);
            preview.P = hp;
            preview.Rebuild();
            preview.SetTint(valid ? OkTint : BadTint);
        }

        void UpdateWing(Vector3 p)
        {
            if (!target) return;
            var hp = JsonUtility.FromJson<HouseParams>(target.Capture());
            wingCur = HouseEntity.WingFromDrag(hp, start, new Vector3(p.x, 0, p.z));
            if (wingCur != null)
            {
                hp.wings ??= new System.Collections.Generic.List<Wing>();
                hp.wings.Add(wingCur);
                valid = HouseEntity.CanPlace(hp, target.Id, out reason);
            }
            else { valid = false; reason = null; }
            cur = hp;
            preview.P = hp;
            preview.Rebuild();
            preview.SetTint(valid ? OkTint : BadTint);
        }

        void EnsurePreview()
        {
            if (preview) return;
            var go = new GameObject("HousePreview");
            preview = go.AddComponent<HouseEntity>();   // không đăng ký vào EntityManager
        }

        void Cancel()
        {
            dragging = false;
            cur = null;
            wingCur = null;
            if (target) target.SetVisualActive(true);
            target = null;
            if (preview) { preview.gameObject.SetActive(false); Object.Destroy(preview.gameObject); }
            preview = null;
        }

        static float Snap(float v, float step) => Mathf.Round(v / step) * step;
    }
}
