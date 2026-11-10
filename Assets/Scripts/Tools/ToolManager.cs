using UnityEngine;

namespace LangGio
{
    /// <summary>Giữ công cụ hiện tại, đọc phím tắt chung (đổi công cụ, hoàn tác, tạm dừng) và gọi công cụ mỗi frame.</summary>
    public class ToolManager : MonoBehaviour
    {
        public static ToolManager I;

        public HandTool Hand { get; private set; }
        public TerrainTool Terrain { get; private set; }
        public BlockTool Block { get; private set; }
        public Tool[] Tools { get; private set; }
        public Tool Current { get; private set; }

        Tool house, wall, tower, path, pond, tree, paint, erase, ground, prop, window, ivy;
        Camera cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        public static bool IsBusy => I && I.Current != null && I.Current.Busy;

        void Awake()
        {
            I = this;
            cam = Camera.main;
            Hand = new HandTool();
            Terrain = new TerrainTool();
            house = new HouseTool();
            wall = new WallTool();
            tower = new TowerTool();
            path = new PathTool();
            pond = new PondTool();
            tree = new TreeTool();
            paint = new PaintTool();
            erase = new EraseTool();
            Block = new BlockTool();
            ground = new GroundPaintTool();
            prop = new PropTool();
            window = new WindowTool();
            ivy = new IvyTool();
            Tools = new Tool[] { Hand, house, window, wall, tower, path, pond, tree, prop, ivy, Block, paint, ground, Terrain, erase };
            Current = Hand;
            Current.Enter();
        }

        public void Set(Tool t)
        {
            if (t == null || t == Current || Current.Busy) return;
            if (t != Hand) Interior.Exit();
            Sfx.Play(SfxId.Click, 1f, 0.4f);
            Current.Exit();
            Current = t;
            Current.Enter();
        }

        /// <summary>Về Bàn tay, bỏ mọi trạng thái dở dang (trước khi tải / làng mới / mở menu).</summary>
        public void ResetTools()
        {
            Interior.Exit(false);
            Current.Exit();
            Current = Hand;
            Current.Enter();
        }

        void Update()
        {
            if (GameUI.Blocking || GameUI.PhotoMode || WalkMode.Active) return;
            Interior.Tick();

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (!Current.Busy)
            {
                // Esc: (1) công cụ khác → Bàn tay; (2) Bàn tay đang chọn → bỏ chọn (HandTool xử lý); (3) không chọn gì → tạm dừng
                if (Input.GetKeyDown(KeyCode.Escape) && Time.frameCount != GameUI.EscConsumedFrame)
                {
                    if (Current != Hand) { Set(Hand); return; }
                    if (!Hand.HasSelection)
                    {
                        if (Interior.Active) Interior.Exit(); else GameUI.OpenPause();
                        return;
                    }
                }

                // Xem trong nhà: I bật/tắt (cần chọn 1 nhà), PageUp/PageDown đổi tầng
                if (Input.GetKeyDown(KeyCode.I))
                {
                    if (Interior.Active) Interior.Exit();
                    else
                    {
                        if (Current != Hand) Set(Hand);
                        if (Hand.Selected is HouseEntity h) Interior.Enter(h);
                        else GameUI.Toast("Chọn 1 ngôi nhà (Bàn tay) rồi nhấn I để xem bên trong");
                    }
                }
                if (Interior.Active)
                {
                    if (Input.GetKeyDown(KeyCode.PageUp)) Interior.SetFloor(Interior.Floor + 1);
                    if (Input.GetKeyDown(KeyCode.PageDown)) Interior.SetFloor(Interior.Floor - 1);
                }

                if (!ctrl)
                {
                    if (Input.GetKeyDown(KeyCode.V)) Set(Hand);
                    if (Input.GetKeyDown(KeyCode.B)) Set(house);
                    if (Input.GetKeyDown(KeyCode.G)) Set(wall);
                    if (Input.GetKeyDown(KeyCode.K)) Set(tower);
                    if (Input.GetKeyDown(KeyCode.H)) Set(path);
                    if (Input.GetKeyDown(KeyCode.O)) Set(pond);
                    if (Input.GetKeyDown(KeyCode.T)) Set(tree);
                    if (Input.GetKeyDown(KeyCode.P)) Set(paint);
                    if (Input.GetKeyDown(KeyCode.U)) Set(Terrain);
                    if (Input.GetKeyDown(KeyCode.X)) Set(erase);
                    if (Input.GetKeyDown(KeyCode.N)) Set(Block);
                    if (Input.GetKeyDown(KeyCode.L)) Set(ground);
                    if (Input.GetKeyDown(KeyCode.D)) Set(prop);
                    if (Input.GetKeyDown(KeyCode.W)) Set(window);
                    if (Input.GetKeyDown(KeyCode.A)) Set(ivy);
                }
                else if (Input.GetKeyDown(KeyCode.S))
                {
                    if (AutoSaver.I) AutoSaver.I.SaveNow(true);
                }
                else if (Input.GetKeyDown(KeyCode.C))
                {
                    Hand.CopySelection();
                }
                else if (Input.GetKeyDown(KeyCode.V))
                {
                    if (Clipboard.HasData)
                    {
                        if (Current != Hand) Set(Hand);
                        Hand.StartPaste();
                    }
                }

                if (Input.GetKeyDown(KeyCode.Z))
                {
                    if (ctrl && shift) History.Redo(); else History.Undo();
                }
                if (Input.GetKeyDown(KeyCode.Y)) History.Redo();
            }

            Current.Update(new ToolContext
            {
                cam = cam,
                overUI = GameUI.MouseOverUI,
                shift = shift,
                ctrl = ctrl,
                alt = alt
            });
        }
    }
}
