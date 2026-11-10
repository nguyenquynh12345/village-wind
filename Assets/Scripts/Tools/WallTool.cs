using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Kéo chuột vẽ tường. Thả chuột → làm mượt, tìm góc, thêm vào đồ thị tường:
    /// hút vào tháp / thân tường có sẵn, cắt ngang tường cũ thì tạo ngã tư.
    /// </summary>
    public class WallTool : Tool
    {
        public static int HeightIdx = GameConfig.WallHeightDefault;
        public static int Style;

        readonly List<Vector3> raw = new();
        Vector3 cursor;
        bool drawing;
        LineRenderer line;
        readonly GroundRing snapRing = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            HeightIdx = GameConfig.WallHeightDefault;
            Style = 0;
        }

        float H => Style == 3 ? GameConfig.LowWallHeight : GameConfig.WallHeights[HeightIdx];

        public override string Name => "Tường";
        public override string Key => "G";
        public override bool Busy => drawing;
        public override string Hint =>
            "Kéo chuột trái: vẽ tường  ·  Shift: thẳng  ·  1–4: kiểu tường  ·  [ / ]: chiều cao  ·  Kéo về điểm đầu: khép vòng  ·  Chuột phải / Esc: hủy";
        public override string Status => drawing
            ? $"{WallGraph.StyleNames[Style]} · {PreviewLength():0.0} m · cao {H:0.0} m"
            : $"{WallGraph.StyleNames[Style]} · cao {H:0.0} m";

        public override void Exit()
        {
            Cancel();
            snapRing.Hide();
        }

        void EnsureLine()
        {
            if (line) return;
            var go = new GameObject("WallPreview");
            line = go.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.35f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = Palette.Cream;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.positionCount = 0;
        }

        public override void Update(ToolContext c)
        {
            EnsureLine();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) HeightIdx = Mathf.Max(0, HeightIdx - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket)) HeightIdx = Mathf.Min(GameConfig.WallHeights.Length - 1, HeightIdx + 1);
            for (int i = 0; i < 4; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Style = i;

            var gr = WallGraph.I;
            bool has = c.Ground(out var p);

            // Vòng sáng ở chỗ sẽ hút vào (tháp / chỗ nối / thân tường)
            if (has && !c.overUI && gr)
            {
                var p2 = new Vector2(p.x, p.z);
                var n = gr.NearestNode(p2, GameConfig.TowerSnap);
                if (n != null) snapRing.Show(new Vector3(n.x, 0, n.z), WallGraph.TowerRadius(Mathf.Max(1, gr.LevelOf(n))) + 0.5f, Palette.Cream);
                else if (gr.NearestSeg(p2, GameConfig.SegmentSnap, out _, out var q)) snapRing.Show(new Vector3(q.x, 0, q.y), 0.8f, Palette.Cream);
                else snapRing.Hide();
            }
            else snapRing.Hide();

            if (!drawing)
            {
                if (Input.GetMouseButtonDown(0) && !c.overUI && has && InBuildArea(p))
                {
                    drawing = true;
                    raw.Clear();
                    var s = new Vector3(p.x, 0, p.z);
                    raw.Add(s);
                    cursor = s;
                }
                return;
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }

            if (has && InBuildArea(p))
            {
                p.y = 0;
                if (c.shift)
                {
                    if (raw.Count > 1) raw.RemoveRange(1, raw.Count - 1);
                }
                else if (PathUtil.Flat(p - raw[raw.Count - 1]).magnitude > GameConfig.SampleDist)
                {
                    raw.Add(p);
                }
                cursor = p;
            }

            var hf = HeightField.I;
            line.positionCount = raw.Count + 1;
            for (int i = 0; i < raw.Count; i++) line.SetPosition(i, hf.OnGround(raw[i]) + Vector3.up * 0.15f);
            line.SetPosition(raw.Count, hf.OnGround(cursor) + Vector3.up * 0.15f);

            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) Finish();
        }

        float PreviewLength() =>
            raw.Count == 0 ? 0f : PathUtil.Length(raw) + PathUtil.Flat(cursor - raw[raw.Count - 1]).magnitude;

        void Finish()
        {
            var pts = new List<Vector3>(raw);
            if (pts.Count > 0 && PathUtil.Flat(cursor - pts[pts.Count - 1]).magnitude > 0.1f) pts.Add(cursor);
            Cancel();
            var gr = WallGraph.I;
            if (!gr || pts.Count < 2 || PathUtil.Length(pts) < GameConfig.MinWallLength) return;

            bool closed = pts.Count > 4
                          && PathUtil.Length(pts) > GameConfig.CloseLoopMinLength
                          && PathUtil.Flat(pts[0] - pts[pts.Count - 1]).magnitude < GameConfig.CloseLoopDist;
            if (closed) pts[pts.Count - 1] = pts[0];

            var path = WallGraph.ProcessStroke(pts, out var corners);
            if (path.Count < 2) return;

            var before = gr.Capture();
            gr.AddStroke(path, corners, closed, HeightIdx, Style);
            History.Push(new ModifyEntityCommand(gr.Id, before, gr.Capture()));
        }

        void Cancel()
        {
            drawing = false;
            raw.Clear();
            if (line) line.positionCount = 0;
        }

        public static bool InBuildArea(Vector3 p) =>
            p.x * p.x + p.z * p.z < GameConfig.BuildRadius * GameConfig.BuildRadius;
    }
}
