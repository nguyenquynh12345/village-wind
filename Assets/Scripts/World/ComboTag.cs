using UnityEngine;

namespace LangGio
{
    /// <summary>Gắn trên 1 chi tiết tổ hợp (cổng, cầu, giếng...) để chọn / tắt được nó.</summary>
    public class ComboTag : MonoBehaviour
    {
        public int combo;
        public string key;
        static MaterialPropertyBlock mpb;

        public string Label => $"{Combos.Names[Mathf.Clamp(combo, 0, Combos.Count - 1)]} (chi tiết tự mọc)";

        public void SetTint(Color? c)
        {
            mpb ??= new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (c == null) { r.SetPropertyBlock(null); continue; }
                var t = c.Value;
                var col = Color.Lerp(r.sharedMaterial.color, new Color(t.r, t.g, t.b, 1f), t.a);
                mpb.Clear();
                mpb.SetColor("_Color", col);
                mpb.SetColor("_BaseColor", col);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Thêm hộp va chạm bao quanh các khối con để click trúng được.</summary>
        public void FitCollider()
        {
            var rs = GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            var bc = gameObject.AddComponent<BoxCollider>();
            bc.center = transform.InverseTransformPoint(b.center);
            bc.size = Vector3.Max(b.size, Vector3.one * 0.4f);
        }
    }

    /// <summary>Tắt / bật lại chi tiết tổ hợp (hoàn tác được, lưu trong file làng).</summary>
    public class ComboSuppressCommand : ICommand
    {
        readonly string[] keys;
        readonly bool hide;

        public ComboSuppressCommand(bool hide, params string[] keys)
        {
            this.hide = hide;
            this.keys = keys;
        }

        public void Do() => Apply(hide);
        public void Undo() => Apply(!hide);

        void Apply(bool h)
        {
            if (!Combos.I) return;
            foreach (var k in keys) Combos.I.SetSuppressed(k, h);
        }
    }
}
