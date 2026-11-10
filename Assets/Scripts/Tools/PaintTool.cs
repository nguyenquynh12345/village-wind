using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Cọ màu: click vào 1 chỗ (tầng nhà, mái, cửa, đoạn tường, mái tháp, cây, đường) → đổi sang lựa chọn kế tiếp.
    /// Giữ chuột và quét qua chỗ khác cùng nhóm → áp cùng lựa chọn. Cả lần quét là 1 bước hoàn tác.
    /// </summary>
    public class PaintTool : Tool
    {
        static readonly Color HoverTint = new Color(1f, 0.85f, 0.4f, 0.35f);

        bool painting;
        string category;
        int value;
        readonly Dictionary<int, string> before = new();
        readonly HashSet<Collider> done = new();
        int hoverId, hoverPart = -1;
        string status;

        public override string Name => "Cọ màu";
        public override string Key => "P";
        public override bool Busy => painting;
        public override string Hint => "Click: đổi màu / chất liệu chỗ đó  ·  Giữ chuột và quét: áp cùng màu cho các chỗ cùng loại";
        public override string Status => status;

        public override void Exit()
        {
            if (painting) End();
            SetHover(0, -1);
        }

        public override void Update(ToolContext c)
        {
            status = null;
            Entity e = null;
            int part = -1;
            Collider col = null;
            if (!c.overUI) Picker.Pick(c.cam, out e, out part, out _, out col);
            string cat = e ? e.PaintCategory(col) : null;
            SetHover(cat != null ? e.Id : 0, part);
            if (cat != null) status = CategoryName(cat);

            if (!painting)
            {
                if (!Input.GetMouseButtonDown(0) || cat == null) return;
                painting = true;
                category = cat;
                value = (e.PaintGet(col) + 1) % Mathf.Max(1, e.PaintCount(cat));
                Apply(e, col);
                return;
            }

            if (cat == category && !done.Contains(col)) Apply(e, col);
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) End();
        }

        void Apply(Entity e, Collider col)
        {
            done.Add(col);
            if (e.PaintGet(col) == value) return;
            if (!before.ContainsKey(e.Id)) before[e.Id] = e.Capture();
            e.PaintSet(col, value);
        }

        void End()
        {
            painting = false;
            var cmd = new CompositeCommand();
            foreach (var kv in before)
            {
                var e = EntityManager.Get(kv.Key);
                if (!e) continue;
                var after = e.Capture();
                if (after != kv.Value) cmd.Add(new ModifyEntityCommand(kv.Key, kv.Value, after));
            }
            if (cmd.Count > 0) History.Push(cmd);
            before.Clear();
            done.Clear();
        }

        void SetHover(int id, int part)
        {
            if (id == hoverId && part == hoverPart) return;
            var old = EntityManager.Get(hoverId);
            if (old) old.SetPartTint(hoverPart, null);
            hoverId = id;
            hoverPart = part;
            var now = EntityManager.Get(hoverId);
            if (now) now.SetPartTint(hoverPart, HoverTint);
        }

        static string CategoryName(string cat) => cat switch
        {
            "houseFloor" => "Tầng nhà: Vữa · Gỗ · Đá · Gạch",
            "houseRoof" => "Mái: Ngói đỏ · Ngói xám · Rơm · Rêu",
            "houseDoor" => "Cửa: Nâu · Xanh lá · Xanh dương · Đỏ",
            "wallStyle" => "Tường: Đá lỗ châu mai · Đá trơn · Hàng rào gỗ · Bờ rào thấp",
            "towerRoof" => "Mái tháp: Nón · Bằng răng cưa · Chòi gỗ",
            "treeType" => "Cây: Tròn · Thông · Bụi · Hoa",
            "pathStyle" => "Đường: Đất · Đá cuội · Ván gỗ",
            _ => cat
        };
    }
}
