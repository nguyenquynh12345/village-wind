using System.Collections.Generic;
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
            int oldFloor = Floor;
            HouseId = 0;
            if (old) { old.Rebuild(); StartRestore(old, oldFloor); }

            var rig = Rig;
            if (rig && !hasCam)
            {
                rig.GetState(out camYaw, out camPitch, out camDist, out camTarget);
                hasCam = true;
            }
            StartCutaway(h, 0);
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
            int prevFloor = Floor;
            HouseId = 0;
            Floor = 0;
            if (h)
            {
                ClearCutaway(h);
                h.Rebuild();
                StartRestore(h, prevFloor);
            }
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
            int prev = Floor;
            if (f < prev) StartCutaway(h, f);
            else ClearCutaway(h);
            Floor = f;
            h.Rebuild();
            if (f > prev) StartRestore(h, prev);
            h.PopInterior();
            Focus(h, false);
        }

        static void ClearCutaway(HouseEntity h)
        {
            if (!h) return;
            for (int i = h.transform.childCount - 1; i >= 0; i--)
            {
                var ch = h.transform.GetChild(i);
                if (ch.name == "InteriorCutaway")
                {
                    ch.gameObject.SetActive(false);
                    Object.Destroy(ch.gameObject);
                }
            }
        }

        /// <summary>Thu nhỏ localScale.y của mái và các tầng trên về 0 trong 0.3 s khi cắt lớp vào nhà.</summary>
        static void StartCutaway(HouseEntity h, int targetFloor)
        {
            if (!h) return;
            ClearCutaway(h);
            var vis = h.transform.Find("Visual");
            if (!vis) return;

            float cutY = (targetFloor + 1) * GameConfig.FloorHeight;
            var cutGo = new GameObject("InteriorCutaway");
            cutGo.transform.SetParent(h.transform, false);
            cutGo.transform.SetLocalPositionAndRotation(new Vector3(0, h.BaseY + cutY, 0), vis.localRotation);

            var move = new List<Transform>();
            for (int i = 0; i < vis.childCount; i++)
            {
                var ch = vis.GetChild(i);
                string n = ch.name;
                if (n == "Roof") move.Add(ch);
                else if (n.StartsWith("Floor") && int.TryParse(n.Substring(5), out int fl) && fl > targetFloor) move.Add(ch);
                else if ((n == "Trim" || n == "Balcony" || n == "Window" || n == "WinFrame" || n == "Sill" || n == "Shutters" || n == "FlowerBox")
                         && ch.localPosition.y >= cutY - 0.1f) move.Add(ch);
                else if (n.StartsWith("Wing"))
                {
                    for (int j = ch.childCount - 1; j >= 0; j--)
                    {
                        var wch = ch.GetChild(j);
                        if (wch.name == "Roof" || (wch.name.StartsWith("Floor") && int.TryParse(wch.name.Substring(5), out int wfl) && wfl > targetFloor)
                            || (wch.localPosition.y >= cutY - 0.1f && wch.name != "Plinth" && wch.name != "IFloor"))
                            move.Add(wch);
                    }
                }
            }

            if (move.Count == 0)
            {
                Object.Destroy(cutGo);
                return;
            }

            foreach (var tr in move)
                tr.SetParent(cutGo.transform, true);
            foreach (var col in cutGo.GetComponentsInChildren<Collider>())
                col.enabled = false;

            var anim = cutGo.AddComponent<InteriorCutAnim>();
            anim.shrink = true;
            anim.duration = 0.3f;
        }

        /// <summary>Mọc lại mái và các tầng trên trong 0.3 s khi thoát xem trong nhà (hoặc lên tầng).</summary>
        static void StartRestore(HouseEntity h, int prevFloor)
        {
            if (!h) return;
            var vis = h.transform.Find("Visual");
            if (!vis) return;
            float cutY = (prevFloor + 1) * GameConfig.FloorHeight;
            for (int i = 0; i < vis.childCount; i++)
            {
                var ch = vis.GetChild(i);
                string n = ch.name;
                bool animate = n == "Roof"
                    || (n.StartsWith("Floor") && int.TryParse(n.Substring(5), out int fl) && fl > prevFloor)
                    || ((n == "Trim" || n == "Balcony") && ch.localPosition.y >= cutY - 0.1f);
                if (animate)
                {
                    var bs = ch.localScale;
                    ch.localScale = new Vector3(bs.x, 0.001f, bs.z);
                    var anim = ch.gameObject.AddComponent<InteriorCutAnim>();
                    anim.baseScale = bs;
                    anim.shrink = false;
                    anim.duration = 0.3f;
                }
                else if (n.StartsWith("Wing"))
                {
                    var wRoof = ch.Find("Roof");
                    if (wRoof)
                    {
                        var bs = wRoof.localScale;
                        wRoof.localScale = new Vector3(bs.x, 0.001f, bs.z);
                        var anim = wRoof.gameObject.AddComponent<InteriorCutAnim>();
                        anim.baseScale = bs;
                        anim.shrink = false;
                        anim.duration = 0.3f;
                    }
                }
            }
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

    /// <summary>Thu hoặc mở localScale.y trong 0.3 s khi vào / ra chế độ xem trong nhà.</summary>
    public class InteriorCutAnim : MonoBehaviour
    {
        public bool shrink = true;
        public float duration = 0.3f;
        public Vector3 baseScale;
        float t;

        void Start()
        {
            if (baseScale == Vector3.zero)
                baseScale = transform.localScale;
            if (!shrink)
                transform.localScale = new Vector3(baseScale.x, 0.001f, baseScale.z);
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.05f, duration));
            float s = shrink ? Mathf.SmoothStep(1f, 0f, k) : Mathf.SmoothStep(0f, 1f, k);
            transform.localScale = new Vector3(baseScale.x, Mathf.Max(0.001f, baseScale.y * s), baseScale.z);
            if (k >= 1f)
            {
                if (shrink) Destroy(gameObject);
                else
                {
                    transform.localScale = baseScale;
                    Destroy(this);
                }
            }
        }
    }
}
