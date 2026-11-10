using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Point = kéo trên mặt đất (hình cầu xanh) ·
    /// Arrow = kéo lên/xuống (mũi tên vàng) ·
    /// Cycle = click để đổi kiểu (hình thoi cam).
    /// </summary>
    public enum HandleKind { Point, Arrow, Cycle }

    public struct HandleDef
    {
        public string key;
        public HandleKind kind;
        public Vector3 position;

        public HandleDef(string key, HandleKind kind, Vector3 position)
        {
            this.key = key;
            this.kind = kind;
            this.position = position;
        }
    }

    /// <summary>Dữ liệu chuột gửi cho Entity khi đang kéo tay nắm.</summary>
    public struct HandleInput
    {
        public Vector3 ground;      // điểm trên mặt đất dưới chuột
        public bool hasGround;
        public float axisDelta;     // đã kéo lên (+) / xuống (−) bao nhiêu mét theo trục đứng
        public bool shift;
    }

    /// <summary>Quản lý các tay nắm đang hiện của vật được chọn (tái sử dụng GameObject).</summary>
    public class HandleSet
    {
        readonly List<HandleView> pool = new();
        Transform root;

        public void Show(List<HandleDef> defs, Camera cam, string activeKey)
        {
            if (!root) root = new GameObject("Handles").transform;
            while (pool.Count < defs.Count) pool.Add(HandleView.Create(root));

            for (int i = 0; i < pool.Count; i++)
            {
                var v = pool[i];
                if (i >= defs.Count) { v.gameObject.SetActive(false); continue; }
                var d = defs[i];
                v.gameObject.SetActive(true);
                v.Setup(d.key, d.kind);
                v.transform.position = d.position;
                float dist = Vector3.Distance(cam.transform.position, d.position);
                float s = Mathf.Clamp(dist * 0.035f, 0.5f, 4f) * (d.key == activeKey ? 1.25f : 1f);
                v.transform.localScale = Vector3.one * s;
            }
        }

        public void Hide()
        {
            foreach (var v in pool) if (v) v.gameObject.SetActive(false);
        }
    }
}
