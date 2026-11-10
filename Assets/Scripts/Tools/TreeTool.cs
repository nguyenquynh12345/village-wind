using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Cây: click = 1 cây · giữ + kéo = cọ trồng (1 cây / 6 m², cách nhau ≥ 2.2 m) · Alt + kéo = cọ xoá.
    /// 1–4: loại cây · [ / ]: cỡ cọ. Cả 1 lần nhấn–thả là 1 bước hoàn tác.
    /// </summary>
    public class TreeTool : Tool
    {
        static readonly Color PlantColor = Palette.Hex("#A3C47E");
        static readonly Color EraseColor = new Color(0.95f, 0.35f, 0.3f);

        public static int Type;
        float radius = GameConfig.TreeBrushDefault;
        bool stroking, erasing, brushing;
        Vector3 pressPoint;
        CompositeCommand cmd;
        readonly GroundRing ring = new();
        string status;

        public override string Name => "Cây";
        public override string Key => "T";
        public override bool Busy => stroking;
        public override string Hint =>
            "Click: trồng 1 cây  ·  Giữ + kéo: cọ trồng cả khóm  ·  Alt + kéo: cọ xoá cây  ·  1 Tròn · 2 Thông · 3 Bụi · 4 Hoa  ·  [ / ]: cỡ cọ";
        public override string Status => status ?? $"{TreeEntity.TypeNames[Type]} · cọ {radius:0} m";

        public override void Exit()
        {
            if (stroking) End();
            ring.Hide();
        }

        public override void Update(ToolContext c)
        {
            status = null;
            for (int i = 0; i < 4; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Type = i;
            if (Input.GetKeyDown(KeyCode.LeftBracket)) radius = Mathf.Max(1f, radius - 1f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) radius = Mathf.Min(10f, radius + 1f);

            bool has = c.Ground(out var p);
            bool showBrush = c.alt || brushing || erasing;
            if (has && (!c.overUI || stroking))
                ring.Show(p, showBrush ? radius : 0.8f, (c.alt || erasing) ? EraseColor : PlantColor);
            else ring.Hide();

            if (!stroking)
            {
                if (has && !c.overUI)
                {
                    var why = c.alt ? null : TreeEntity.BlockedReason(p.x, p.z);
                    if (why != null) status = why;
                }
                if (!Input.GetMouseButtonDown(0) || c.overUI || !has || !WallTool.InBuildArea(p)) return;
                stroking = true;
                erasing = c.alt;
                brushing = false;
                pressPoint = p;
                cmd = new CompositeCommand();
                if (!erasing) TryPlant(new Vector2(p.x, p.z), 1.0f);
                else EraseAround(p);
                return;
            }

            if (has)
            {
                if (!brushing && PathUtil.Flat(p - pressPoint).magnitude > 0.6f) brushing = true;
                if (erasing) EraseAround(p);
                else if (brushing) BrushPlant(p);
            }

            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) End();
        }

        void End()
        {
            stroking = false;
            brushing = false;
            erasing = false;
            if (cmd != null && cmd.Count > 0) History.Push(cmd);
            cmd = null;
        }

        bool TryPlant(Vector2 q, float minGap)
        {
            if (!WallTool.InBuildArea(new Vector3(q.x, 0, q.y))) return false;
            if (TreeEntity.BlockedReason(q.x, q.y) != null) return false;
            foreach (var t in EntityManager.OfType<TreeEntity>())
                if (!t.Hidden && Vector2.Distance(t.XZ, q) < minGap) return false;

            var tp = new TreeParams
            {
                x = q.x, z = q.y, type = Type,
                size = Random.Range(0.85f, 1.25f),
                rot = Random.Range(0f, 360f)
            };
            var e = EntityManager.Spawn("Tree", JsonUtility.ToJson(tp), 0, Random.Range(0f, 0.15f));
            cmd.Add(new CreateEntityCommand("Tree", e.Capture(), -1f, e.Id));
            return true;
        }

        void BrushPlant(Vector3 p)
        {
            var c = new Vector2(p.x, p.z);
            float area = Mathf.PI * radius * radius;
            int count = 0;
            foreach (var t in EntityManager.OfType<TreeEntity>())
                if (!t.Hidden && Vector2.Distance(t.XZ, c) < radius) count++;
            int allowed = Mathf.FloorToInt(area / GameConfig.TreeDensity);
            for (int k = 0; k < 3 && count < allowed; k++)
            {
                var q = c + Random.insideUnitCircle * radius;
                if (TryPlant(q, GameConfig.TreeSpacing)) count++;
            }
        }

        void EraseAround(Vector3 p)
        {
            var c = new Vector2(p.x, p.z);
            foreach (var t in new List<TreeEntity>(EntityManager.OfType<TreeEntity>()))
            {
                if (Vector2.Distance(t.XZ, c) > radius) continue;
                var del = new DeleteEntityCommand(t);
                del.Do();
                cmd.Add(del);
            }
        }
    }
}
