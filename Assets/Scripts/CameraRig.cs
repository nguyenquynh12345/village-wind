using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Camera xoay quanh một điểm trên mặt đất (bám theo đồi).
    /// Chuột phải: xoay · Con lăn: zoom · Chuột giữa / phím mũi tên: di chuyển · Q/E: xoay.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public Vector3 target = Vector3.zero;
        public float yaw = 35f, pitch = 42f, distance = 38f;
        /// <summary>Khác NaN: điểm nhìn giữ ở độ cao này (xem trong nhà) thay vì bám mặt đất.</summary>
        [System.NonSerialized] public float lockY = float.NaN;

        float tYaw, tPitch, tDist;

        /// <summary>Chế độ ngắm cảnh: camera tự lượn chậm quanh làng, ẩn giao diện. Bấm chuột / phím bất kỳ để thoát.</summary>
        public static bool Cinematic;
        float cineT;
        Vector3 cineCenter;

        public void StartCinematic()
        {
            Cinematic = true;
            cineT = 0f;
            cineCenter = tTarget;
            GameUI.UiHidden = true;
        }

        public void StopCinematic()
        {
            if (!Cinematic) return;
            Cinematic = false;
            GameUI.UiHidden = false;
        }
        Vector3 tTarget, lastMouse;

        void Awake()
        {
            tYaw = yaw; tPitch = pitch; tDist = distance; tTarget = target;
            Apply();
        }

        public void GetState(out float y, out float p, out float d, out Vector3 t)
        {
            y = tYaw; p = tPitch; d = tDist; t = tTarget;
        }

        public void SetState(float y, float p, float d, Vector3 t)
        {
            yaw = tYaw = y;
            pitch = tPitch = Mathf.Clamp(p, 15f, 85f);
            distance = tDist = Mathf.Clamp(d, 6f, 90f);
            target = tTarget = t;
            Apply();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (GameUI.Blocking)
            {
                // Đang ở menu: camera tự xoay chậm quanh làng
                if (GameUI.Screen == UiScreen.Main) { tYaw += 4f * dt; yaw = tYaw; Apply(); }
                return;
            }
            if (Cinematic)
            {
                if (Input.anyKeyDown || Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) { StopCinematic(); return; }
                cineT += dt;
                tYaw += 5f * dt;
                tPitch = 30f + Mathf.Sin(cineT * 0.11f) * 10f;
                tDist = 46f + Mathf.Sin(cineT * 0.07f + 1f) * 14f;
                tTarget = cineCenter + new Vector3(Mathf.Sin(cineT * 0.05f) * 6f, 0, Mathf.Cos(cineT * 0.04f) * 6f);
                float kc = 1f - Mathf.Exp(-dt * 1.5f);
                yaw = Mathf.Lerp(yaw, tYaw, kc); pitch = Mathf.Lerp(pitch, tPitch, kc);
                distance = Mathf.Lerp(distance, tDist, kc); target = Vector3.Lerp(target, tTarget, kc);
                Apply();
                return;
            }
            if (Input.GetKeyDown(KeyCode.F2) && GameUI.Screen == UiScreen.Play) { StartCinematic(); return; }
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) lastMouse = Input.mousePosition;
            Vector3 delta = Input.mousePosition - lastMouse;
            lastMouse = Input.mousePosition;

            // Xoay
            if (Input.GetMouseButton(1) && !ToolManager.IsBusy)
            {
                tYaw += delta.x * 0.25f;
                tPitch = Mathf.Clamp(tPitch - delta.y * 0.2f, 15f, 85f);
            }
            if (Input.GetKey(KeyCode.Q)) tYaw += 90f * dt;
            if (Input.GetKey(KeyCode.E)) tYaw -= 90f * dt;

            // Di chuyển trên mặt phẳng ngang
            Vector3 right = Quaternion.Euler(0, tYaw, 0) * Vector3.right;
            Vector3 fwd = Quaternion.Euler(0, tYaw, 0) * Vector3.forward;
            if (Input.GetMouseButton(2))
                tTarget -= (right * delta.x + fwd * delta.y) * tDist * 0.0015f;

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.UpArrow)) move += fwd;
            if (Input.GetKey(KeyCode.DownArrow)) move -= fwd;
            if (Input.GetKey(KeyCode.RightArrow)) move += right;
            if (Input.GetKey(KeyCode.LeftArrow)) move -= right;
            tTarget += move.normalized * tDist * 0.9f * dt;

            var flat = Vector3.ClampMagnitude(new Vector3(tTarget.x, 0, tTarget.z), GameConfig.BuildRadius);
            float groundY = !float.IsNaN(lockY) ? lockY : HeightField.I ? HeightField.I.HeightAt(flat.x, flat.z) : 0f;
            tTarget = new Vector3(flat.x, groundY, flat.z);

            // Zoom
            float scroll = Input.mouseScrollDelta.y;
            bool shiftRot = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                            ToolManager.I && ToolManager.I.Current == ToolManager.I.Hand && ToolManager.I.Hand.Selected;
            if (!GameUI.MouseOverUI && !shiftRot && Mathf.Abs(scroll) > 0.01f)
                tDist = Mathf.Clamp(tDist * (1f - scroll * 0.12f), 6f, 90f);

            // Làm mượt
            float k = 1f - Mathf.Exp(-12f * dt);
            yaw = Mathf.LerpAngle(yaw, tYaw, k);
            pitch = Mathf.Lerp(pitch, tPitch, k);
            distance = Mathf.Lerp(distance, tDist, k);
            target = Vector3.Lerp(target, tTarget, k);
            Apply();
        }

        void Apply()
        {
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var pos = target + rot * new Vector3(0, 0, -distance);
            if (HeightField.I)
            {
                float minY = HeightField.I.HeightAt(pos.x, pos.z) + 1.5f;
                if (pos.y < minY) pos.y = minY;   // không chui xuống đồi
            }
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(target - pos);
        }
    }
}
