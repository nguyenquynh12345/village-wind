using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Đặt / dời / xoá ô cửa trên tường nhà: click tường = đặt (trùng ô cũ → đổi kiểu) · Alt + click = xoá ·
    /// 1–5: Vuông / Vòm / Tròn / Cửa ra vào / Cao.
    /// </summary>
    public class WindowTool : Tool
    {
        static readonly Color OkColor = new Color(0.55f, 0.85f, 0.6f, 0.55f);
        static readonly Color RemoveColor = new Color(0.95f, 0.3f, 0.25f, 0.6f);

        public static int Kind = HouseEntity.WinSquare;
        GameObject ghost;
        Material ghostMat;
        string lastReason;

        public override string Name => "Cửa sổ";
        public override string Key => "W";
        public override string Hint =>
            "Click tường nhà: đặt ô cửa (trùng ô cũ → đổi kiểu)  ·  Alt + click: xoá  ·  1 Vuông · 2 Vòm · 3 Tròn · 4 Cửa ra vào · 5 Cao";
        public override string Status => lastReason ?? HouseEntity.OpeningNames[Kind];

        public override void Exit()
        {
            if (ghost) ghost.SetActive(false);
        }

        public override void Update(ToolContext c)
        {
            if (!c.ctrl)
                for (int i = 0; i < HouseEntity.OpeningNames.Length; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Kind = i;

            EnsureGhost();
            ghost.SetActive(false);
            lastReason = null;
            if (c.overUI) return;

            var ray = c.Ray;
            float gd = HeightField.I.Raycast(ray, out var gp) ? Vector3.Distance(ray.origin, gp) : float.MaxValue;
            if (!Physics.Raycast(ray, out var hit, 1000f) || hit.distance > gd + 0.05f) return;
            var house = hit.collider.GetComponentInParent<HouseEntity>();
            if (!house || house.Id == 0 || !house.WallHit(hit, out int s, out int f, out float along)) return;

            int kind = c.alt ? -1 : Kind;
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(house.OpeningCenter(s, f, along, Mathf.Max(0, kind)), house.OpeningRotation(s));
            ghost.transform.localScale = kind switch
            {
                HouseEntity.WinDoor => new Vector3(1.1f, 2.1f, 0.2f),
                HouseEntity.WinTall => new Vector3(0.9f, 1.8f, 0.2f),
                HouseEntity.WinRound => new Vector3(0.8f, 0.8f, 0.2f),
                _ => new Vector3(0.9f, 1.2f, 0.2f)
            };
            ghostMat.color = c.alt ? RemoveColor : OkColor;

            if (!Input.GetMouseButtonDown(0)) return;
            var before = house.Capture();
            if (house.EditOpening(s, f, along, kind, out var reason))
            {
                History.Push(new ModifyEntityCommand(house.Id, before, house.Capture()));
            }
            else if (reason != null) GameUI.Toast(reason);
        }

        void EnsureGhost()
        {
            if (ghost) return;
            ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.name = "WindowGhost";
            var col = ghost.GetComponent<Collider>();
            col.enabled = false;
            Object.Destroy(col);
            ghostMat = new Material(Shader.Find("Sprites/Default")) { color = OkColor };
            var r = ghost.GetComponent<Renderer>();
            r.sharedMaterial = ghostMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            ghost.SetActive(false);
        }
    }
}
