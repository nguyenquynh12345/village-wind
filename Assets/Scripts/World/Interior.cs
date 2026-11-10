using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Chế độ "xem trong nhà" (C.10): cắt lớp 1 nhà, camera zoom vào, đổi tầng, sửa đồ đạc.
    /// Chỉ 1 nhà được xem tại 1 thời điểm. Không lưu vào file save.
    /// </summary>
    public static class Interior
    {
        public static int HouseId { get; private set; }
        public static int Floor { get; private set; }
        public static bool Active => HouseId != 0;

        static bool hasCam;
        static float camYaw, camPitch, camDist;
        static Vector3 camTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            HouseId = 0;
            Floor = 0;
            hasCam = false;
        }

        public static bool IsViewing(int id) => id != 0 && id == HouseId;
        public static HouseEntity House => Active ? EntityManager.Get(HouseId) as HouseEntity : null;

        static CameraRig Rig => Camera.main ? Camera.main.GetComponent<CameraRig>() : null;

        public static void Enter(HouseEntity h)
        {
            if (!h || h.Id == 0) return;
            if (IsViewing(h.Id)) return;
            var old = House;
            HouseId = 0;
            if (old) old.Rebuild();

            var rig = Rig;
            if (rig && !hasCam)
            {
                rig.GetState(out camYaw, out camPitch, out camDist, out camTarget);
                hasCam = true;
            }
            HouseId = h.Id;
            Floor = 0;
            h.Rebuild();
            h.PopInterior();
            Focus(h, true);
            GameUI.Toast("Xem trong nhà · PageUp/PageDown đổi tầng · I hoặc Esc để ra");
        }

        /// <summary>Thoát xem trong. restoreCam = đưa camera về chỗ cũ.</summary>
        public static void Exit(bool restoreCam = true)
        {
            if (!Active) return;
            var h = House;
            HouseId = 0;
            Floor = 0;
            if (h) h.Rebuild();
            var rig = Rig;
            if (rig)
            {
                rig.lockY = float.NaN;
                if (restoreCam && hasCam) rig.SetState(camYaw, camPitch, camDist, camTarget);
            }
            hasCam = false;
        }

        public static void SetFloor(int f)
        {
            var h = House;
            if (!h) return;
            f = Mathf.Clamp(f, 0, h.Floors - 1);
            if (f == Floor) return;
            Floor = f;
            h.Rebuild();
            h.PopInterior();
            Focus(h, false);
        }

        /// <summary>Nhà bớt tầng → tầng đang xem lùi xuống (gọi từ Rebuild, không sinh lại).</summary>
        public static void ClampFloor(int floors)
        {
            if (Floor > floors - 1) Floor = Mathf.Max(0, floors - 1);
        }

        static void Focus(HouseEntity h, bool zoom)
        {
            var rig = Rig;
            if (!rig) return;
            float y = h.BaseY + Floor * GameConfig.FloorHeight;
            rig.lockY = y;
            rig.GetState(out var yaw, out var pitch, out var dist, out _);
            if (zoom)
            {
                dist = Mathf.Max(h.W, h.L) * 1.7f + 5f;
                pitch = Mathf.Max(pitch, 52f);
            }
            var center = h.LocalToWorld(Vector3.zero);
            rig.SetState(yaw, pitch, dist, new Vector3(center.x, y, center.z));
        }

        /// <summary>Gọi mỗi frame từ ToolManager: nhà bị xoá (hoàn tác...) → thoát.</summary>
        public static void Tick()
        {
            if (Active && !House) Exit(true);
        }
    }
}
