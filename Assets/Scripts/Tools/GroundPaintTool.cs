using UnityEngine;

namespace LangGio
{
    /// <summary>1 lần quét cọ tô đất (lưu phần chênh lệch của vùng bị tô).</summary>
    public class GroundPaintCommand : ICommand
    {
        readonly RectInt r;
        readonly byte[] before, after;

        public GroundPaintCommand(RectInt r, byte[] before, byte[] after)
        {
            this.r = r;
            this.before = before;
            this.after = after;
        }

        public void Do() => HeightField.I.SetPaintRegion(r, after);
        public void Undo() => HeightField.I.SetPaintRegion(r, before);
    }

    /// <summary>
    /// Cọ tô mặt đất: hoa dại (cỏ hoa dày, có bướm) · sỏi · cát · đất · cỏ (xoá tô).
    /// Giữ + kéo để tô · 1–5: loại · [ / ]: cỡ cọ.
    /// </summary>
    public class GroundPaintTool : Tool
    {
        static readonly Color[] RingColors =
        {
            new(0.55f, 0.8f, 0.45f, 0.6f), new(0.95f, 0.75f, 0.85f, 0.6f), new(0.8f, 0.78f, 0.72f, 0.6f),
            new(0.95f, 0.88f, 0.65f, 0.6f), new(0.65f, 0.5f, 0.38f, 0.6f)
        };

        public static int Mode = 1;
        float radius = 3f;
        bool painting;
        byte[] snapshot;
        RectInt touched;
        bool hasTouched;
        readonly GroundRing ring = new();

        public override string Name => "Cọ đất";
        public override string Key => "L";
        public override bool Busy => painting;
        public override string Hint => "Giữ + kéo: tô mặt đất  ·  1 Cỏ (xoá) · 2 Hoa dại · 3 Sỏi · 4 Cát · 5 Đất  ·  [ / ]: cỡ cọ";
        public override string Status => $"{HeightField.PaintNames[Mode]} · cọ {radius:0.#} m";

        public override void Exit()
        {
            Finish();
            ring.Hide();
        }

        public override void Update(ToolContext c)
        {
            for (int i = 0; i < 5; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Mode = i;
            if (Input.GetKeyDown(KeyCode.LeftBracket)) radius = Mathf.Max(1f, radius - 0.5f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) radius = Mathf.Min(10f, radius + 0.5f);

            bool has = c.Ground(out var p);
            if (has && (!c.overUI || painting)) ring.Show(p, radius, RingColors[Mode]); else ring.Hide();

            var hf = HeightField.I;
            if (!painting)
            {
                if (Input.GetMouseButtonDown(0) && has && !c.overUI)
                {
                    painting = true;
                    snapshot = hf.PaintData();
                    hasTouched = false;
                }
                else return;
            }

            if (has)
            {
                var r = hf.PaintBrush(p, radius, (byte)Mode);
                touched = hasTouched ? HeightField.Union(touched, r) : r;
                hasTouched = true;
            }
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) Finish();
        }

        void Finish()
        {
            if (!painting) return;
            painting = false;
            var hf = HeightField.I;
            if (hasTouched && snapshot != null)
            {
                var before = hf.ExtractPaint(snapshot, touched);
                var after = hf.ExtractPaint(hf.PaintData(), touched);
                if (!System.Linq.Enumerable.SequenceEqual(before, after))
                    History.Push(new GroundPaintCommand(touched, before, after));
            }
            snapshot = null;
        }
    }
}
