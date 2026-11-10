using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Mọi vật xây được (tháp, tường, sau này nhà, ao, cây...).
    /// Quy tắc: chỉ lưu THAM SỐ; hình (Visual) luôn được sinh lại từ tham số bằng <see cref="Rebuild"/>.
    /// </summary>
    public abstract class Entity : MonoBehaviour
    {
        public int Id;
        protected GameObject visual;
        Color? tint;
        static MaterialPropertyBlock mpb;

        public abstract string TypeName { get; }
        public virtual string Describe => TypeName;

        /// <summary>Tham số → JSON (dùng cho hoàn tác và lưu game).</summary>
        public abstract string Capture();
        /// <summary>JSON → tham số, rồi sinh lại hình.</summary>
        public abstract void Restore(string json);
        /// <summary>Xoá hình cũ, sinh hình mới từ tham số hiện tại.</summary>
        public abstract void Rebuild();

        public abstract void Translate(Vector3 delta);
        public virtual void Rotate(float degrees) { }
        /// <summary>Vật có nằm trong vùng (x, z) này không — để biết cần sinh lại khi địa hình đổi.</summary>
        public abstract bool AffectedBy(Rect worldXZ);

        /// <summary>Gọi sau khi vật bị gỡ khỏi EntityManager (để dọn những thứ nó ảnh hưởng: đất ao, cây bị che...).</summary>
        public virtual void OnRemoved() { }

        /// <summary>Địa hình đổi trong vùng r. light = đang kéo cọ (chỉ cập nhật phần nhẹ).</summary>
        public virtual void TerrainChangedIn(Rect r, bool light)
        {
            if (!light) Rebuild();
        }

        // ---- Phần con (part) — ví dụ 1 nút hay 1 đoạn trong đồ thị tường. -1 = cả vật ----
        /// <summary>Phần đang được chọn / thao tác (công cụ đặt trước khi gọi các hàm khác).</summary>
        [NonSerialized] public int ActivePart = -1;
        public virtual int PartOf(Collider c) => -1;
        /// <summary>Phần trúng tia chuột (mặc định theo collider; khối tự do dùng chỉ số tam giác).</summary>
        public virtual int PartOfHit(RaycastHit h) => PartOf(h.collider);
        public virtual bool HasPart(int part) => true;
        /// <summary>Xoá 1 phần (true = đã xử lý, hoàn tác bằng ModifyEntityCommand). false = xoá cả vật.</summary>
        public virtual bool DeletePart(int part) => false;
        public virtual bool CanDuplicate => true;
        public virtual bool CanRotate => true;
        /// <summary>Gọi khi thả chuột sau khi kéo vật / kéo tay nắm (để gộp nút, dọn dẹp...).</summary>
        public virtual void EndEdit() { }
        /// <summary>Lệnh phụ đã thực hiện trong EndEdit (vd. ao gộp → xoá ao kia). Công cụ gộp chúng với lệnh sửa thành 1 bước hoàn tác.</summary>
        public virtual List<ICommand> TakeSideEffects() => null;
        public virtual void SetPartTint(int part, Color? c) => SetTint(c);
        /// <summary>true = khi kéo phần đang chọn, chiếu chuột lên mặt phẳng ngang độ cao y (thay vì mặt đất) — ví dụ đồ đạc tầng 2.</summary>
        public virtual bool DragPlaneY(out float y) { y = 0f; return false; }
        /// <summary>Khung toạ độ của vật (dây leo bám theo khung này). Mặc định = thế giới.</summary>
        public virtual Matrix4x4 Frame => Matrix4x4.identity;

        // ---- Cọ màu: mỗi chỗ bấm thuộc 1 "nhóm" (tầng nhà, mái, cửa, đoạn tường...) có N lựa chọn ----
        public virtual string PaintCategory(Collider c) => null;
        public virtual int PaintCount(string category) => 0;
        public virtual int PaintGet(Collider c) => 0;
        /// <summary>Đặt lựa chọn v cho chỗ bấm c rồi sinh lại hình.</summary>
        public virtual void PaintSet(Collider c, int v) { }

        // ---- Tay nắm ----
        public virtual void GetHandles(List<HandleDef> list) { }
        public virtual void BeginHandle(string key) { }
        public virtual void DragHandle(string key, HandleInput input) { }
        public virtual void ClickHandle(string key) { }

        public virtual void PlayPop(float delay)
        {
            if (visual) PopIn.Add(visual, delay);
        }

        // ---- Tô màu khi rê / chọn ----
        /// <summary>rgb = màu pha vào, a = mức pha (0..1). null = bỏ tô.</summary>
        public void SetTint(Color? c)
        {
            if (Nullable.Equals(tint, c)) return;
            tint = c;
            ApplyTint();
        }

        protected void ApplyTint()
        {
            if (!visual) return;
            mpb ??= new MaterialPropertyBlock();
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
            {
                if (tint == null) { r.SetPropertyBlock(null); continue; }
                var t = tint.Value;
                var m = r.sharedMaterial;
                var baseCol = m ? (m.HasProperty("_Color") ? m.GetColor("_Color") : m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white) : Color.white;
                var c = Color.Lerp(baseCol, new Color(t.r, t.g, t.b, 1f), t.a);
                mpb.Clear();
                mpb.SetColor("_Color", c);
                mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(mpb);
            }
        }

        // ---- Tiện ích dựng hình ----
        protected GameObject NewVisual()
        {
            if (visual)
            {
                visual.SetActive(false);
                Destroy(visual);
            }
            visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            return visual;
        }

        protected static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale,
                                         Color color, bool keepCollider = false, Quaternion? localRot = null, string name = null)
        {
            var g = GameObject.CreatePrimitive(type);
            if (name != null) g.name = name;
            if (!keepCollider)
            {
                var col = g.GetComponent<Collider>();
                col.enabled = false;
                Destroy(col);
            }
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = localRot ?? Quaternion.identity;
            g.transform.localScale = localScale;
            g.GetComponent<Renderer>().sharedMaterial = Palette.Mat(color);
            return g;
        }


        protected static GameObject MeshObj(string name, Mesh mesh, Transform parent, Vector3 localPos, Vector3 localScale,
                                            Color color, Quaternion? localRot = null)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = localRot ?? Quaternion.identity;
            g.transform.localScale = localScale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = Palette.Mat(color);
            return g;
        }

        /// <summary>Hướng từ vật về phía camera (trên mặt phẳng ngang) — để đặt tay nắm dễ thấy.</summary>
        protected static Vector3 TowardCamera(Vector3 from)
        {
            var cam = Camera.main;
            if (!cam) return Vector3.back;
            var d = cam.transform.position - from;
            d.y = 0;
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.back;
        }

        public static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                            Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
    }

    /// <summary>Entity có tham số kiểu T (lớp [Serializable] để JsonUtility đọc/ghi được).</summary>
    public abstract class Entity<T> : Entity where T : class, new()
    {
        public T P = new T();

        public override string Capture() => JsonUtility.ToJson(P);

        public override void Restore(string json)
        {
            P = JsonUtility.FromJson<T>(json) ?? new T();
            Rebuild();
        }
    }
}
