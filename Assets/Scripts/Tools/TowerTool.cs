using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Click đất trống: tháp đứng riêng · click thân tường: chèn tháp vào tường ·
    /// click chỗ nối không tháp: dựng tháp · click tháp có sẵn: thêm tầng (1 → 2 → 3 → 1).
    /// </summary>
    public class TowerTool : Tool
    {
        readonly GroundRing ring = new();
        string status;

        static readonly Color Ok = Palette.Cream;
        static readonly Color Bad = new Color(0.95f, 0.35f, 0.3f);
        static readonly Color Up = Palette.Hex("#E9B44C");

        public override string Name => "Tháp";
        public override string Key => "K";
        public override string Hint => "Click đất trống: đặt tháp  ·  Click thân tường: chèn tháp  ·  Click tháp có sẵn: thêm tầng";
        public override string Status => status;

        public override void Exit() => ring.Hide();

        public override void Update(ToolContext c)
        {
            status = null;
            var gr = WallGraph.I;
            if (c.overUI || !gr || !c.Ground(out var p)) { ring.Hide(); return; }
            var p2 = new Vector2(p.x, p.z);
            var hf = HeightField.I;

            var node = gr.NearestTower(p2, GameConfig.TowerSnap) ?? gr.NearestNode(p2, GameConfig.TowerSnap);
            GSeg seg = null;
            Vector2 q = default;
            bool onSeg = node == null && gr.NearestSeg(p2, GameConfig.SegmentSnap, out seg, out q);
            var spot = onSeg ? q : p2;
            bool tooClose = node == null && gr.NearestTower(spot, GameConfig.TowerMinSpacing) != null;
            bool inArea = WallTool.InBuildArea(p);
            bool onHouse = false;
            foreach (var h in EntityManager.OfType<HouseEntity>())
                if (h.ContainsPoint(spot, GameConfig.TowerRadius[1])) { onHouse = true; break; }

            if (node != null)
            {
                int L = gr.LevelOf(node);
                var np = new Vector3(node.x, hf.HeightAt(node.x, node.z), node.z);
                ring.Show(np, WallGraph.TowerRadius(Mathf.Max(1, L)) + 0.5f, Up);
                status = L > 0 ? $"Thêm tầng ({L} → {L % 3 + 1})" : "Dựng tháp ở chỗ nối";
            }
            else if (tooClose || !inArea || onHouse)
            {
                ring.Show(new Vector3(spot.x, 0, spot.y), GameConfig.TowerRadius[1], Bad);
                status = !inArea ? "Ngoài vùng xây" : onHouse ? "Chồng lên nhà" : "Quá gần tháp khác";
            }
            else
            {
                ring.Show(new Vector3(spot.x, 0, spot.y), GameConfig.TowerRadius[1], Ok);
                status = onSeg ? "Chèn tháp vào tường" : null;
            }

            if (!Input.GetMouseButtonDown(0)) return;

            var before = gr.Capture();
            if (node != null)
            {
                int L = gr.LevelOf(node);
                node.level = L > 0 ? L % 3 + 1 : 1;
                node.manual = true;
            }
            else if (!tooClose && inArea && !onHouse)
            {
                if (onSeg)
                {
                    var n = gr.GetNode(gr.SplitAt(seg, q));
                    n.level = 1;
                    n.manual = true;
                }
                else gr.AddNode(p2, true, -1);
            }
            else return;

            gr.Commit();
            History.Push(new ModifyEntityCommand(gr.Id, before, gr.Capture()));
        }
    }
}
