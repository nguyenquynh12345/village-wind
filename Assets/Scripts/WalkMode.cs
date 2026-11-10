using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Chế độ dạo làng góc nhìn thứ nhất (S5 — bật/tắt bằng F3 hoặc nút trên thanh dưới):
    /// - WASD đi bộ, Shift chạy, chuột xoay đầu, mắt cao 1.6 m.
    /// - Không đi xuyên nhà, tường, tháp, khối xây; không lội nước sâu quá 0.3 m (đi qua cầu gỗ bình thường).
    /// - Đứng cách thuyền ≤ 3 m nhấn E để ngồi thuyền, nhấn E lần nữa để lên bờ gần nhất.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class WalkMode : MonoBehaviour
    {
        public static WalkMode I;
        public static bool Active => I && I.active;
        public static bool IsRidingBoat => I && I.active && I.ridingBoat != null;
        public static bool CanBoardBoat => I && I.active && I.ridingBoat == null && I.nearBoat != null;

        Camera cam;
        CameraRig rig;
        bool active;

        // Trạng thái camera trước khi vào dạo làng
        float savedYaw, savedPitch, savedDist, savedFov;
        Vector3 savedTarget;

        // Trạng thái người đi bộ
        Vector2 posXZ;
        float currentGroundY;
        float yaw, pitch;
        float stepAccum;
        float bobPhase;

        // Ngồi thuyền (S4)
        Boat nearBoat;
        Boat ridingBoat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
            rig = GetComponent<CameraRig>();
        }

        void OnDisable()
        {
            if (active) ExitWalk();
        }

        public static void Toggle()
        {
            if (!I) return;
            if (I.active) I.ExitWalk();
            else if (GameUI.Screen == UiScreen.Play && !GameUI.JournalOpen && !GameUI.PhotoMode && !ToolManager.IsBusy)
                I.EnterWalk();
        }

        public void EnterWalk()
        {
            if (active || !HeightField.I) return;
            if (CameraRig.Cinematic && rig) rig.StopCinematic();
            if (Interior.Active) Interior.Exit(false);

            if (rig)
            {
                rig.GetState(out savedYaw, out savedPitch, out savedDist, out savedTarget);
                rig.enabled = false;
            }
            if (cam)
            {
                savedFov = cam.fieldOfView;
                cam.fieldOfView = 58f;
            }
            if (ToolManager.I && ToolManager.I.Current != null)
                ToolManager.I.Current.Exit();

            // Điểm bắt đầu: trước cửa ngôi nhà gần tâm màn hình nhất; không có nhà → giữa màn hình
            Vector2 centerXZ = new Vector2(savedTarget.x, savedTarget.z);
            HouseEntity bestHouse = null;
            float bestD = float.MaxValue;
            foreach (var h in EntityManager.OfType<HouseEntity>())
            {
                float d = Vector2.Distance(new Vector2(h.P.x, h.P.z), centerXZ);
                if (d < bestD) { bestD = d; bestHouse = h; }
            }

            if (bestHouse != null)
            {
                var dp = bestHouse.DoorPoint;
                var outDir = new Vector2(dp.x - bestHouse.P.x, dp.z - bestHouse.P.z);
                if (outDir.sqrMagnitude < 1e-4f) outDir = Vector2.up;
                else outDir.Normalize();
                var startCand = new Vector2(dp.x, dp.z) + outDir * 0.45f;
                posXZ = CanStandAt(startCand, out _) ? startCand : FindNearestSafeSpot(new Vector2(dp.x, dp.z));
                yaw = Mathf.Atan2(outDir.x, outDir.y) * Mathf.Rad2Deg;
                pitch = 0f;
            }
            else
            {
                posXZ = CanStandAt(centerXZ, out _) ? centerXZ : FindNearestSafeSpot(centerXZ);
                yaw = savedYaw;
                pitch = 0f;
            }

            CanStandAt(posXZ, out currentGroundY);
            ridingBoat = null;
            nearBoat = null;
            stepAccum = 0f;
            bobPhase = 0f;
            active = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ApplyCamera();
        }

        public void ExitWalk()
        {
            if (!active) return;
            active = false;
            ridingBoat = null;
            nearBoat = null;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (cam) cam.fieldOfView = savedFov > 1f ? savedFov : 40f;
            if (rig)
            {
                rig.enabled = true;
                rig.SetState(savedYaw, savedPitch, savedDist, savedTarget);
            }
            if (ToolManager.I && ToolManager.I.Current != null)
                ToolManager.I.Current.Enter();

            GameUI.EscConsumedFrame = Time.frameCount;
        }

        void Update()
        {
            if (!active)
            {
                if (GameUI.Screen == UiScreen.Play && !GameUI.JournalOpen && !GameUI.PhotoMode && !ToolManager.IsBusy && Input.GetKeyDown(KeyCode.F3))
                    EnterWalk();
                return;
            }

            if (GameUI.Screen != UiScreen.Play)
            {
                ExitWalk();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F3) || Input.GetKeyDown(KeyCode.Escape))
            {
                ExitWalk();
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Chuột xoay đầu
            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            yaw = Mathf.Repeat(yaw + mx * 2.2f, 360f);
            pitch = Mathf.Clamp(pitch - my * 2.0f, -80f, 80f);

            // Đang ngồi thuyền
            if (ridingBoat != null)
            {
                if (!ridingBoat || Seasons.Snow >= 0.5f || Input.GetKeyDown(KeyCode.E))
                {
                    Vector2 boatXZ = ridingBoat ? Geo.XZ(ridingBoat.transform.position) : posXZ;
                    ridingBoat = null;
                    posXZ = FindNearestBankSpot(boatXZ);
                    CanStandAt(posXZ, out currentGroundY);
                    PlayStepSound(posXZ);
                }
                else
                {
                    posXZ = Geo.XZ(ridingBoat.transform.position);
                    ApplyCamera();
                    return;
                }
            }

            // Tìm thuyền gần ≤ 3 m để có thể nhấn E lên thuyền
            nearBoat = null;
            if (Seasons.Snow < 0.5f)
            {
                float bestB = 3f;
                foreach (var b in Boat.All)
                {
                    if (!b) continue;
                    float d = Vector2.Distance(posXZ, Geo.XZ(b.transform.position));
                    if (d <= bestB) { bestB = d; nearBoat = b; }
                }
                if (nearBoat != null && Input.GetKeyDown(KeyCode.E))
                {
                    ridingBoat = nearBoat;
                    nearBoat = null;
                    PlayStepSound(posXZ);
                    ApplyCamera();
                    return;
                }
            }

            // Đi bộ WASD / phím mũi tên + Shift chạy
            float ix = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                     - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float iz = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                     - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);

            Vector2 input = new Vector2(ix, iz);
            if (input.sqrMagnitude > 1f) input.Normalize();

            float dt = Time.unscaledDeltaTime;
            bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float speed = run ? GameConfig.RunSpeed : GameConfig.WalkSpeed;

            if (input.sqrMagnitude > 1e-4f)
            {
                float rad = yaw * Mathf.Deg2Rad;
                Vector2 fwd = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                Vector2 right = new Vector2(fwd.y, -fwd.x);
                Vector2 delta = (right * input.x + fwd * input.y) * (speed * dt);

                Vector2 before = posXZ;
                if (TryMove(delta, out float newY))
                {
                    currentGroundY = Mathf.Lerp(currentGroundY, newY, Mathf.Clamp01(dt * 18f));
                    float moved = Vector2.Distance(before, posXZ);
                    stepAccum += moved;
                    bobPhase += moved * (run ? 3.2f : 3.8f);
                    float stride = run ? 1.85f : 1.35f;
                    if (stepAccum >= stride)
                    {
                        stepAccum -= stride;
                        PlayStepSound(posXZ);
                    }
                }
            }
            else
            {
                if (CanStandAt(posXZ, out float gy))
                    currentGroundY = Mathf.Lerp(currentGroundY, gy, Mathf.Clamp01(dt * 14f));
            }

            ApplyCamera();
        }

        void ApplyCamera()
        {
            if (ridingBoat)
            {
                var seat = ridingBoat.SeatWorldPos + Vector3.up * 0.85f;
                transform.position = seat;
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                return;
            }
            float bob = Mathf.Sin(bobPhase) * 0.035f;
            transform.position = new Vector3(posXZ.x, currentGroundY + GameConfig.EyeHeight + bob, posXZ.y);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        bool TryMove(Vector2 delta, out float standY)
        {
            // Chia bước nhỏ nếu đi nhanh để không nhảy xuyên tường mỏng
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.2f));
            Vector2 step = delta / steps;
            if (!CanStandAt(posXZ, out float curY)) curY = currentGroundY;
            float maxTan = Mathf.Tan(GameConfig.WalkMaxSlope * Mathf.Deg2Rad);
            bool any = false;

            bool CanStep(Vector2 to, float stepLen, out float y)
            {
                if (!CanStandAt(to, out y)) return false;
                float extra = OnAnyPath(to) && WaterSurfaceAt(to, out _) ? 0.4f : 0f;
                return y - curY <= stepLen * maxTan + extra;
            }

            for (int i = 0; i < steps; i++)
            {
                var next = posXZ + step;
                if (CanStep(next, step.magnitude, out float y))
                {
                    posXZ = next;
                    curY = y;
                    any = true;
                }
                else
                {
                    // Trượt dọc theo tường / cạnh nhà / chân vách đá
                    var nx = new Vector2(posXZ.x + step.x, posXZ.y);
                    var nz = new Vector2(posXZ.x, posXZ.y + step.y);
                    if (Mathf.Abs(step.x) > 1e-5f && CanStep(nx, Mathf.Abs(step.x), out float yx))
                    {
                        posXZ = nx;
                        curY = yx;
                        any = true;
                    }
                    else if (Mathf.Abs(step.y) > 1e-5f && CanStep(nz, Mathf.Abs(step.y), out float yz))
                    {
                        posXZ = nz;
                        curY = yz;
                        any = true;
                    }
                }
            }
            standY = curY;
            return any;
        }

        // ================= Kiểm tra va chạm & độ sâu nước =================
        static bool CanStandAt(Vector2 p, out float groundY)
        {
            groundY = 0f;
            var hf = HeightField.I;
            if (!hf) return false;

            // Giới hạn trong BuildRadius
            if (p.sqrMagnitude > GameConfig.BuildRadius * GameConfig.BuildRadius) return false;

            groundY = hf.HeightAt(p.x, p.y);

            // Không đi xuyên nền nhà
            foreach (var h in EntityManager.OfType<HouseEntity>())
                if (h.ContainsPoint(p, 0.35f)) return false;

            bool onPath = OnAnyPath(p);

            // Không đi xuyên tường, tháp (trừ khi có cổng đường đi cắt qua)
            var gr = WallGraph.I;
            if (gr)
            {
                foreach (var (tp, tr) in gr.Towers())
                    if (Vector2.Distance(p, tp) < tr + 0.32f) return false;
                if (!onPath && gr.NearestSeg(p, GameConfig.WallThickness * 0.5f + 0.28f, out _, out _))
                    return false;
            }

            // Kiểm tra cầu gỗ (E2) bắc qua ao/sông
            if (onPath && WaterSurfaceAt(p, out float waterLevel))
            {
                groundY = Mathf.Max(groundY, waterLevel + 0.35f);
            }
            else if (Seasons.Snow < 0.5f && WaterSurfaceAt(p, out float wLevel))
            {
                // Không xuống nước sâu hơn 0.3 m (trừ mùa đông mặt băng cứng)
                float depth = wLevel - groundY;
                if (depth > 0.3f) return false;
            }
            else if (Seasons.Snow >= 0.5f && WaterSurfaceAt(p, out float iceLevel))
            {
                groundY = Mathf.Max(groundY, iceLevel);
            }

            // Kiểm tra capsule với collider của Entity (nhà, tường, khối xây, đồ trang trí)
            Vector3 pBot = new Vector3(p.x, groundY + 0.55f, p.y);
            Vector3 pTop = new Vector3(p.x, groundY + GameConfig.EyeHeight - 0.1f, p.y);
            var cols = Physics.OverlapCapsule(pBot, pTop, 0.28f);
            foreach (var c in cols)
            {
                if (!c || c.isTrigger) continue;
                if (c.GetComponentInParent<HeightField>()) continue;
                if (c.GetComponentInParent<WallGraph>()) continue;
                if (c.GetComponentInParent<PathEntity>()) continue;
                if (c.GetComponentInParent<PondEntity>() || c.GetComponentInParent<RiverEntity>()) continue;
                if (c.GetComponentInParent<TreeEntity>()) continue;
                if (c.GetComponentInParent<ComboTag>()) continue;
                return false;
            }

            return true;
        }

        static bool OnAnyPath(Vector2 p)
        {
            foreach (var path in EntityManager.OfType<PathEntity>())
                if (path.Covers(p, 0.1f)) return true;
            return false;
        }

        static bool WaterSurfaceAt(Vector2 p, out float waterY)
        {
            foreach (var pond in EntityManager.OfType<PondEntity>())
            {
                if (pond.Mask != null && pond.WorldRect.Contains(p) && pond.Mask.Coverage(p.x, p.y, 0.5f) > 0f)
                {
                    waterY = pond.WaterLevel;
                    return true;
                }
            }
            foreach (var rv in EntityManager.OfType<RiverEntity>())
            {
                if (rv.Covers(p, 0.2f))
                {
                    waterY = rv.WaterLevelAt(p);
                    return true;
                }
            }
            waterY = 0f;
            return false;
        }

        static Vector2 FindNearestSafeSpot(Vector2 origin)
        {
            if (CanStandAt(origin, out _)) return origin;
            for (float r = 0.8f; r <= 30f; r += 0.8f)
            {
                int n = Mathf.Max(8, Mathf.RoundToInt(r * 4f));
                for (int i = 0; i < n; i++)
                {
                    float a = i * (Mathf.PI * 2f / n);
                    var cand = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (CanStandAt(cand, out _)) return cand;
                }
            }
            return Vector2.zero;
        }

        static Vector2 FindNearestBankSpot(Vector2 boatXZ)
        {
            for (float r = 1.2f; r <= 35f; r += 0.6f)
            {
                int n = Mathf.Max(12, Mathf.RoundToInt(r * 5f));
                for (int i = 0; i < n; i++)
                {
                    float a = i * (Mathf.PI * 2f / n);
                    var cand = boatXZ + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (!WaterSurfaceAt(cand, out _) && CanStandAt(cand, out _))
                        return cand;
                }
            }
            return FindNearestSafeSpot(boatXZ);
        }

        static void PlayStepSound(Vector2 p)
        {
            if (Seasons.Snow > 0.5f)
                Sfx.Play(SfxId.Step, Random.Range(1.35f, 1.55f), 0.28f);   // tuyết lạo xạo
            else if (OnAnyPath(p))
                Sfx.Play(SfxId.Step, Random.Range(1.05f, 1.22f), 0.34f);   // đường / cầu
            else
                Sfx.Play(SfxId.Step, Random.Range(0.75f, 0.92f), 0.25f);   // cỏ / đất
        }
    }
}
