using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Kéo chuột vẽ đường đi. Đầu đường gần cửa nhà → hút vào trước cửa; gần đường khác → nối vào.
    /// 1–3: kiểu (Đất / Đá cuội / Ván gỗ) · [ / ]: bề rộng · Shift: thẳng.
    /// </summary>
    public class PathTool : Tool
    {
        public static int Style;
        public static float Width = 2f;
        readonly List<Vector3> raw = new();
        Vector3 cursor;
        bool drawing;
        LineRenderer line;
        readonly GroundRing snapRing = new();

        public override string Name => "Đường";
        public override string Key => "H";
        public override bool Busy => drawing;
        public override string Hint =>
            "Kéo chuột: vẽ đường  ·  Shift: thẳng  ·  1 Đất · 2 Đá cuội · 3 Ván gỗ  ·  [ / ]: bề rộng  ·  Đầu đường gần cửa nhà / đường khác: tự nối";
        public override string Status => $"{PathEntity.StyleNames[Style]} · rộng {Width:0.#} m" +
                                         (drawing ? $" · {PreviewLength():0.0} m" : "");

        public override void Exit()
        {
            Cancel();
            snapRing.Hide();
        }

        void EnsureLine()
        {
            if (line) return;
            var go = new GameObject("PathPreview");
            line = go.AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = new Color(0.91f, 0.84f, 0.68f, 0.9f);
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.positionCount = 0;
        }

        public override void Update(ToolContext c)
        {
            EnsureLine();
            for (int i = 0; i < 3; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Style = i;
            if (Input.GetKeyDown(KeyCode.LeftBracket)) Width = Mathf.Max(1f, Width - 0.5f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) Width = Mathf.Min(4f, Width + 0.5f);
            line.widthMultiplier = Width;

            bool has = c.Ground(out var p);
            if (has && !c.overUI && Snap(new Vector2(p.x, p.z), out var sp, out _))
                snapRing.Show(new Vector3(sp.x, 0, sp.y), 0.9f, Palette.Cream);
            else snapRing.Hide();

            if (!drawing)
            {
                if (Input.GetMouseButtonDown(0) && !c.overUI && has && WallTool.InBuildArea(p))
                {
                    drawing = true;
                    raw.Clear();
                    raw.Add(new Vector3(p.x, 0, p.z));
                    cursor = raw[0];
                }
                return;
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }

            if (has && WallTool.InBuildArea(p))
            {
                p.y = 0;
                if (c.shift) { if (raw.Count > 1) raw.RemoveRange(1, raw.Count - 1); }
                else if (PathUtil.Flat(p - raw[raw.Count - 1]).magnitude > GameConfig.SampleDist) raw.Add(p);
                cursor = p;
            }

            var hf = HeightField.I;
            line.positionCount = raw.Count + 1;
            for (int i = 0; i < raw.Count; i++) line.SetPosition(i, hf.OnGround(raw[i]) + Vector3.up * 0.12f);
            line.SetPosition(raw.Count, hf.OnGround(cursor) + Vector3.up * 0.12f);

            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) Finish();
        }

        float PreviewLength() =>
            raw.Count == 0 ? 0f : PathUtil.Length(raw) + PathUtil.Flat(cursor - raw[raw.Count - 1]).magnitude;

        /// <summary>Hút vào trước cửa nhà (2.5 m) hoặc vào thân đường khác (1.5 m).</summary>
        static bool Snap(Vector2 p, out Vector2 q, out bool toDoor)
        {
            q = p; toDoor = false;
            float best = 2.5f;
            foreach (var h in EntityManager.OfType<HouseEntity>())
            {
                var d = Geo.XZ(h.DoorPoint);
                float dist = Vector2.Distance(d, p);
                if (dist < best) { best = dist; q = d; toDoor = true; }
            }
            if (toDoor) return true;
            best = 1.5f;
            bool found = false;
            foreach (var path in EntityManager.OfType<PathEntity>())
            {
                if (path.Points.Count < 2) continue;
                Geo.Closest(path.Points, p, out _, out var c, out float dist);
                if (dist < best) { best = dist; q = c; found = true; }
            }
            return found;
        }

        void Finish()
        {
            var pts = new List<Vector3>(raw);
            if (pts.Count > 0 && PathUtil.Flat(cursor - pts[pts.Count - 1]).magnitude > 0.1f) pts.Add(cursor);
            Cancel();
            if (pts.Count < 2 || PathUtil.Length(pts) < 1.5f) return;

            if (Snap(Geo.XZ(pts[0]), out var s0, out _)) pts[0] = Geo.X0Z(s0);
            if (Snap(Geo.XZ(pts[pts.Count - 1]), out var s1, out _)) pts[pts.Count - 1] = Geo.X0Z(s1);

            var smooth = PathUtil.Resample(PathUtil.Chaikin(pts, 3), 0.8f);
            var pp = new PathParams { width = Width, style = Style };
            foreach (var q in smooth) pp.pts.Add(new Vector2(q.x, q.z));
            History.Execute(new CreateEntityCommand("Path", JsonUtility.ToJson(pp), -1f));
        }

        void Cancel()
        {
            drawing = false;
            raw.Clear();
            if (line) line.positionCount = 0;
        }
    }
}
