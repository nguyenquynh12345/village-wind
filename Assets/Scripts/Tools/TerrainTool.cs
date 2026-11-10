using UnityEngine;

namespace LangGio
{
    /// <summary>Cọ địa hình: 1 Nâng · 2 Hạ · 3 San phẳng · 4 Làm mượt. [ / ]: cỡ cọ. Shift: mạnh gấp đôi.</summary>
    public class TerrainTool : Tool
    {
        public static readonly string[] ModeNames = { "Nâng", "Hạ", "San phẳng", "Làm mượt" };
        static readonly Color[] ModeColors =
        {
            Palette.Hex("#E9B44C"), Palette.Hex("#7FB7C9"), Palette.Cream, Palette.Hex("#A3C47E")
        };

        public TerrainMode Mode = TerrainMode.Raise;
        public float Radius = GameConfig.BrushDefault;

        readonly GroundRing ring = new();
        bool stroking;
        float[] before;
        RectInt dirty;
        bool hasDirty;
        float flatHeight;

        public override string Name => "Địa hình";
        public override string Key => "U";
        public override bool Busy => stroking;
        public override string Hint =>
            $"Giữ chuột trái: {ModeNames[(int)Mode]}  ·  1 Nâng · 2 Hạ · 3 San phẳng · 4 Làm mượt  ·  [ / ]: cỡ cọ  ·  Shift: mạnh";
        public override string Status => $"{ModeNames[(int)Mode]} · cọ {Radius:0} m";

        public override void Exit()
        {
            if (stroking) EndStroke();
            ring.Hide();
        }

        public override void Update(ToolContext c)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) Mode = TerrainMode.Raise;
            if (Input.GetKeyDown(KeyCode.Alpha2)) Mode = TerrainMode.Lower;
            if (Input.GetKeyDown(KeyCode.Alpha3)) Mode = TerrainMode.Flatten;
            if (Input.GetKeyDown(KeyCode.Alpha4)) Mode = TerrainMode.Smooth;
            if (Input.GetKeyDown(KeyCode.LeftBracket)) Radius = Mathf.Max(GameConfig.BrushMin, Radius - 1f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) Radius = Mathf.Min(GameConfig.BrushMax, Radius + 1f);

            bool has = c.Ground(out var p);
            if (has && (!c.overUI || stroking)) ring.Show(p, Radius, ModeColors[(int)Mode]);
            else ring.Hide();

            var hf = HeightField.I;

            if (!stroking)
            {
                if (Input.GetMouseButtonDown(0) && !c.overUI && has)
                {
                    stroking = true;
                    before = hf.Snapshot();
                    hasDirty = false;
                    flatHeight = p.y;
                }
                return;
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                // Huỷ nét cọ đang kéo: trả đất về như cũ
                if (hasDirty) hf.SetRegion(dirty, hf.Extract(before, dirty));
                stroking = false;
                before = null;
                return;
            }

            if (Input.GetMouseButton(0) && has)
            {
                var r = hf.Brush(p, Radius, Mode, c.shift ? 2f : 1f, Time.deltaTime, flatHeight);
                dirty = hasDirty ? HeightField.Union(dirty, r) : r;
                hasDirty = true;
                hf.RebuildDirty();
                EntityManager.TerrainChanged(hf.GridToWorld(r), true);
            }

            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) EndStroke();
        }

        void EndStroke()
        {
            stroking = false;
            var hf = HeightField.I;
            if (hasDirty)
            {
                History.Push(new TerrainCommand(dirty, hf.Extract(before, dirty), hf.GetRegion(dirty)));
                EntityManager.TerrainChanged(hf.GridToWorld(dirty), false);
            }
            before = null;
            hasDirty = false;
        }
    }
}
