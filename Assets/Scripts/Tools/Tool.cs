using System;
using UnityEngine;

namespace LangGio
{
    /// <summary>Thông tin chung mỗi frame cho công cụ.</summary>
    public struct ToolContext
    {
        public Camera cam;
        public bool overUI, shift, ctrl, alt;

        public Ray Ray => cam.ScreenPointToRay(Input.mousePosition);

        public bool Ground(out Vector3 p) => HeightField.I.Raycast(Ray, out p);
    }

    /// <summary>Một công cụ trên thanh công cụ (Bàn tay, Tường, Tháp, Địa hình, Xóa...).</summary>
    public abstract class Tool
    {
        public abstract string Name { get; }
        public abstract string Key { get; }
        public virtual string Hint => "";
        /// <summary>Chữ nhỏ hiện cạnh chuột (null = không hiện).</summary>
        public virtual string Status => null;
        /// <summary>Đang vẽ / kéo dở → không đổi công cụ, không xoay camera bằng chuột phải.</summary>
        public virtual bool Busy => false;

        public virtual void Enter() { }
        public virtual void Exit() { }
        public abstract void Update(ToolContext c);
    }

    /// <summary>Chọn vật dưới chuột. Ưu tiên tay nắm; vật bị đồi che thì không chọn được.</summary>
    public static class Picker
    {
        /// <summary>Chi tiết tổ hợp dưới chuột (khi không trúng vật nào).</summary>
        /// <summary>Khoảng cách tới vật vừa chọn được ở lần Pick gần nhất.</summary>
        public static float LastEntityDistance = float.MaxValue;

        /// <summary>Chi tiết tổ hợp nếu nó nằm GẦN camera hơn vật (vd. cổng trên tường, cầu trên đường).</summary>
        public static ComboTag PickComboOver(Camera cam, Entity entity)
        {
            var tag = PickCombo(cam, out float d);
            if (!tag) return null;
            return !entity || d < LastEntityDistance - 0.02f ? tag : null;
        }

        public static ComboTag PickCombo(Camera cam) => PickCombo(cam, out _);

        public static ComboTag PickCombo(Camera cam, out float dist)
        {
            dist = float.MaxValue;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            float groundDist = HeightField.I.Raycast(ray, out var gp) ? Vector3.Distance(ray.origin, gp) : float.MaxValue;
            var hits = Physics.RaycastAll(ray, 1000f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.distance > groundDist + 0.3f) break;
                var tag = h.collider.GetComponent<ComboTag>();
                if (tag) { dist = h.distance; return tag; }
            }
            return null;
        }

        public static void Pick(Camera cam, out Entity entity, out HandleView handle) =>
            Pick(cam, out entity, out _, out handle);

        public static void Pick(Camera cam, out Entity entity, out int part, out HandleView handle) =>
            Pick(cam, out entity, out part, out handle, out _);

        public static void Pick(Camera cam, out Entity entity, out int part, out HandleView handle, out Collider hitCollider)
        {
            entity = null;
            handle = null;
            part = -1;
            hitCollider = null;
            LastEntityDistance = float.MaxValue;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            float groundDist = HeightField.I.Raycast(ray, out var gp) ? Vector3.Distance(ray.origin, gp) : float.MaxValue;

            var hits = Physics.RaycastAll(ray, 1000f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                var hv = h.collider.GetComponent<HandleView>();
                if (hv && hv.gameObject.activeInHierarchy) { handle = hv; return; }
            }
            foreach (var h in hits)
            {
                if (h.distance > groundDist + 0.3f) break;
                var e = h.collider.GetComponentInParent<Entity>();
                if (e) { entity = e; part = e.PartOfHit(h); hitCollider = h.collider; LastEntityDistance = h.distance; return; }
            }
        }
    }
}
