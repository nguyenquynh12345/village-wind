using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Ao: kéo vẽ 1 vòng → thả ra thành ao. Shift + kéo: cọ thêm nước. Alt + kéo: cọ bớt nước.
    /// Ao mới chạm ao cũ → tự gộp. 1 / 2: ao nông / sâu. [ / ]: cỡ cọ.
    /// </summary>
    public class PondTool : Tool
    {
        enum Mode { None, Lasso, Add, Remove }

        static readonly Color AddColor = new Color(0.5f, 0.75f, 0.85f, 0.55f);
        static readonly Color RemoveColor = new Color(0.95f, 0.35f, 0.3f, 0.5f);

        Mode mode;
        public static bool Deep;
        float radius = GameConfig.PondBrushDefault;
        readonly List<Vector3> raw = new();
        PondMask stroke;
        LineRenderer line;
        readonly GroundRing ring = new();
        readonly MaskPreview preview = new();

        public override string Name => "Ao";
        public override string Key => "O";
        public override bool Busy => mode != Mode.None;
        public override string Hint =>
            "Kéo vẽ 1 vòng: tạo ao  ·  Shift + kéo: cọ thêm nước  ·  Alt + kéo: cọ bớt nước  ·  1 Nông · 2 Sâu  ·  [ / ]: cỡ cọ";
        public override string Status => $"Ao {(Deep ? "sâu" : "nông")} · cọ {radius:0.#} m";

        public override void Exit() => Cancel();

        void EnsureLine()
        {
            if (line) return;
            var go = new GameObject("PondLasso");
            line = go.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.3f;
            line.loop = true;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = Palette.Hex("#7FB7C9");
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.positionCount = 0;
        }

        public override void Update(ToolContext c)
        {
            EnsureLine();
            if (Input.GetKeyDown(KeyCode.Alpha1)) Deep = false;
            if (Input.GetKeyDown(KeyCode.Alpha2)) Deep = true;
            if (Input.GetKeyDown(KeyCode.LeftBracket)) radius = Mathf.Max(1f, radius - 0.5f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) radius = Mathf.Min(8f, radius + 0.5f);

            bool has = c.Ground(out var p);
            var p2 = new Vector2(p.x, p.z);

            if (has && !c.overUI && (c.shift || c.alt || mode == Mode.Add || mode == Mode.Remove))
                ring.Show(p, radius, (c.alt || mode == Mode.Remove) ? RemoveColor : AddColor);
            else ring.Hide();

            if (mode == Mode.None)
            {
                if (!Input.GetMouseButtonDown(0) || c.overUI || !has || !WallTool.InBuildArea(p)) return;
                if (c.alt) { mode = Mode.Remove; stroke = PondMask.Stamp(null, p2, radius, true); }
                else if (c.shift) { mode = Mode.Add; stroke = PondMask.Stamp(null, p2, radius, true); }
                else { mode = Mode.Lasso; raw.Clear(); raw.Add(new Vector3(p.x, 0, p.z)); }
                return;
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }

            if (has && WallTool.InBuildArea(p))
            {
                if (mode == Mode.Lasso)
                {
                    if (PathUtil.Flat(new Vector3(p.x, 0, p.z) - raw[raw.Count - 1]).magnitude > 0.4f) raw.Add(new Vector3(p.x, 0, p.z));
                    var hf = HeightField.I;
                    line.positionCount = raw.Count;
                    for (int i = 0; i < raw.Count; i++) line.SetPosition(i, hf.OnGround(raw[i]) + Vector3.up * 0.15f);
                }
                else
                {
                    stroke = PondMask.Stamp(stroke, p2, radius, true);
                    preview.Show(stroke, mode == Mode.Remove ? RemoveColor : AddColor);
                }
            }

            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) Finish();
        }

        void Finish()
        {
            var m = mode;
            PondMask shape = null;
            if (m == Mode.Lasso && raw.Count >= 3)
            {
                var poly = new List<Vector2>();
                foreach (var q in raw) poly.Add(new Vector2(q.x, q.z));
                shape = PondMask.Rasterize(poly);
            }
            else if (m != Mode.Lasso) shape = stroke;
            Cancel();
            if (shape == null) return;

            if (m == Mode.Remove) RemoveWater(shape);
            else AddWater(shape);
        }

        void AddWater(PondMask shape)
        {
            // Không đào ao dưới nền nhà
            foreach (var h in EntityManager.OfType<HouseEntity>())
            {
                foreach (var fp in h.Boxes)
                    for (int gz = shape.z0; gz < shape.z0 + shape.h; gz++)
                        for (int gx = shape.x0; gx < shape.x0 + shape.w; gx++)
                            if (shape.Get(gx, gz) && fp.Contains(new Vector2(PondMask.Center(gx), PondMask.Center(gz)), 0.5f))
                                shape.Set(gx, gz, false);
            }
            shape = shape.Trim();
            if (shape == null || shape.Area < GameConfig.PondMinArea) return;

            // Gộp với các ao đang chạm
            var touching = new List<PondEntity>();
            foreach (var pond in EntityManager.OfType<PondEntity>())
                if (pond.Mask != null && shape.Touches(pond.Mask)) touching.Add(pond);

            var cmd = new CompositeCommand();
            if (touching.Count == 0)
            {
                var pp = new PondParams { deep = Deep, seed = Random.Range(0, 100000) };
                shape.WriteTo(pp);
                cmd.Add(new CreateEntityCommand("Pond", JsonUtility.ToJson(pp), 0f));
                History.Execute(cmd);
                return;
            }

            var keep = touching[0];
            var merged = shape;
            foreach (var t in touching) merged = PondMask.Union(merged, t.Mask);
            for (int i = 1; i < touching.Count; i++)
            {
                var del = new DeleteEntityCommand(touching[i]);
                del.Do();
                cmd.Add(del);
            }
            var before = keep.Capture();
            keep.SetMask(merged);
            keep.Rebuild();
            cmd.Add(new ModifyEntityCommand(keep.Id, before, keep.Capture()));
            History.Push(cmd);
        }

        void RemoveWater(PondMask shape)
        {
            var cmd = new CompositeCommand();
            foreach (var pond in new List<PondEntity>(EntityManager.OfType<PondEntity>()))
            {
                if (pond.Mask == null || !shape.Touches(pond.Mask)) continue;
                var rest = PondMask.Subtract(pond.Mask, shape);
                if (rest == null || rest.Area < GameConfig.PondMinArea)
                {
                    var del = new DeleteEntityCommand(pond);
                    del.Do();
                    cmd.Add(del);
                }
                else
                {
                    var before = pond.Capture();
                    pond.SetMask(rest);
                    pond.Rebuild();
                    cmd.Add(new ModifyEntityCommand(pond.Id, before, pond.Capture()));
                }
            }
            if (cmd.Count > 0) History.Push(cmd);
        }

        void Cancel()
        {
            mode = Mode.None;
            raw.Clear();
            stroke = null;
            if (line) line.positionCount = 0;
            preview.Hide();
            ring.Hide();
        }
    }
}
