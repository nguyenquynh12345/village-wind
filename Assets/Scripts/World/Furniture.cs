using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>1 món đồ trong nhà. Toạ độ x, z theo khung cục bộ của nhà; f = tầng; r = xoay (×90°).</summary>
    [Serializable]
    public class Furn
    {
        public int t, v, f, r;
        public float x, z;
        public Furn Clone() => (Furn)MemberwiseClone();
    }

    /// <summary>Danh mục đồ đạc: tên, kích thước, cách dựng hình (khối xám). Mặt trước = +z của món đồ.</summary>
    public static class Furniture
    {
        public const int Table = 0, Chair = 1, Bed = 2, Cabinet = 3, Lamp = 4, Plant = 5, Stove = 6, Chest = 7;
        public const int Variants = 3;

        public static readonly string[] Names = { "Bàn", "Ghế", "Giường", "Tủ", "Đèn", "Chậu cây", "Bếp lò", "Rương" };
        /// <summary>Những món thêm tay được từ thanh nội thất.</summary>
        public static readonly int[] Addable = { Table, Chair, Bed, Cabinet, Lamp, Plant, Chest };

        static readonly Vector2[] Sizes =
        {
            new(1.2f, 0.8f), new(0.45f, 0.45f), new(1.0f, 2.0f), new(1.0f, 0.5f),
            new(0.35f, 0.35f), new(0.45f, 0.45f), new(0.9f, 0.7f), new(0.8f, 0.5f)
        };
        static readonly float[] Heights = { 0.78f, 0.95f, 0.9f, 1.8f, 1.5f, 0.9f, 1.0f, 0.6f };

        static readonly Color[] Woods = { Palette.Hex("#C49A6C"), Palette.Hex("#7A5236"), Palette.Hex("#E2D3B8") };
        static readonly Color[] Cloth = { Palette.Hex("#C8664A"), Palette.Hex("#4F7FA6"), Palette.Hex("#6E8B4E") };
        static readonly Color Metal = Palette.Hex("#5A5A5E");
        static readonly Color Linen = Palette.Hex("#F4EFE6");
        static readonly Color Terracotta = Palette.Hex("#B7684F");

        public static int Clamp(int t) => Mathf.Clamp(t, 0, Sizes.Length - 1);
        public static Vector2 Size(int t) => Sizes[Clamp(t)];
        public static float Height(int t) => Heights[Clamp(t)];
        public static string Name(int t) => Names[Clamp(t)];

        /// <summary>Nửa kích thước chiếm chỗ theo trục cục bộ của nhà (đã tính xoay).</summary>
        public static Vector2 Half(Furn f)
        {
            var s = Size(f.t);
            return (f.r & 1) == 0 ? s * 0.5f : new Vector2(s.y, s.x) * 0.5f;
        }

        public static Rect Footprint(Furn f)
        {
            var h = Half(f);
            return new Rect(f.x - h.x, f.z - h.y, 2 * h.x, 2 * h.y);
        }

        /// <summary>Xoay để mặt trước quay vào trong phòng, lưng tựa tường có pháp tuyến n (cục bộ).</summary>
        public static int RotFacingAway(Vector3 wallNormal)
        {
            if (Mathf.Abs(wallNormal.x) > Mathf.Abs(wallNormal.z)) return wallNormal.x > 0 ? 3 : 1;
            return wallNormal.z > 0 ? 2 : 0;
        }

        // ---------- Dựng hình ----------
        public static GameObject Build(Furn fu, Transform parent, float floorY, int part)
        {
            var root = new GameObject("Furn" + part);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(fu.x, floorY, fu.z);
            root.transform.localRotation = Quaternion.Euler(0, (fu.r & 3) * 90f, 0);
            root.AddComponent<PartTag>().part = part;

            var s = Size(fu.t);
            float h = Height(fu.t);
            var bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, h * 0.5f, 0);
            bc.size = new Vector3(s.x, h, s.y);

            int v = Mathf.Abs(fu.v) % Variants;
            var t = root.transform;
            var wood = Woods[v];
            switch (Clamp(fu.t))
            {
                case Table:
                    if (v == 2)
                        Box(t, PrimitiveType.Cylinder, new Vector3(0, 0.74f, 0), new Vector3(1.15f, 0.03f, 0.78f), Woods[0]);
                    else
                        Box(t, PrimitiveType.Cube, new Vector3(0, 0.74f, 0), new Vector3(1.2f, 0.06f, 0.8f), wood);
                    for (int i = 0; i < 4; i++)
                        Box(t, PrimitiveType.Cube, new Vector3((i & 1) == 0 ? -0.5f : 0.5f, 0.36f, (i & 2) == 0 ? -0.32f : 0.32f),
                            new Vector3(0.07f, 0.72f, 0.07f), wood);
                    break;

                case Chair:
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f), wood);
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.72f, -0.2f), new Vector3(0.45f, 0.5f, 0.05f), wood);
                    for (int i = 0; i < 4; i++)
                        Box(t, PrimitiveType.Cube, new Vector3((i & 1) == 0 ? -0.18f : 0.18f, 0.22f, (i & 2) == 0 ? -0.18f : 0.18f),
                            new Vector3(0.05f, 0.44f, 0.05f), wood);
                    if (v == 1) Box(t, PrimitiveType.Cube, new Vector3(0, 0.49f, 0.02f), new Vector3(0.4f, 0.04f, 0.38f), Cloth[1]);
                    break;

                case Bed:
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.2f, 0), new Vector3(1.0f, 0.3f, 2.0f), Woods[1]);
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.41f, 0), new Vector3(0.92f, 0.14f, 1.9f), Linen);
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.5f, -0.72f), new Vector3(0.6f, 0.1f, 0.3f), Linen);
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.5f, 0.3f), new Vector3(0.96f, 0.1f, 1.3f), Cloth[v]);
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.45f, -0.97f), new Vector3(1.0f, 0.9f, 0.06f), Woods[1]);
                    break;

                case Cabinet:
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.9f, 0), new Vector3(1.0f, 1.8f, 0.5f), wood);
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.9f, 0.255f), new Vector3(0.02f, 1.6f, 0.01f), Palette.Ink);
                    Box(t, PrimitiveType.Cube, new Vector3(-0.08f, 0.95f, 0.27f), new Vector3(0.04f, 0.12f, 0.03f), Metal);
                    Box(t, PrimitiveType.Cube, new Vector3(0.08f, 0.95f, 0.27f), new Vector3(0.04f, 0.12f, 0.03f), Metal);
                    if (v == 2) Box(t, PrimitiveType.Cube, new Vector3(0, 1.84f, 0), new Vector3(1.06f, 0.08f, 0.56f), Woods[1]);
                    break;

                case Lamp:
                {
                    float ph = v == 1 ? 1.0f : 1.35f;
                    Box(t, PrimitiveType.Cylinder, new Vector3(0, 0.03f, 0), new Vector3(0.3f, 0.03f, 0.3f), Metal);
                    Box(t, PrimitiveType.Cylinder, new Vector3(0, ph * 0.5f, 0), new Vector3(0.04f, ph * 0.5f, 0.04f), v == 2 ? wood : Metal);
                    var shade = Box(t, PrimitiveType.Cylinder, new Vector3(0, ph + 0.08f, 0), new Vector3(0.34f, 0.12f, 0.34f), Linen);
                    shade.GetComponent<Renderer>().sharedMaterial = Palette.LanternMat();
                    break;
                }

                case Plant:
                {
                    float k = v == 1 ? 1.3f : 1f;
                    Box(t, PrimitiveType.Cylinder, new Vector3(0, 0.17f, 0), new Vector3(0.36f, 0.17f, 0.36f), Terracotta);
                    Box(t, PrimitiveType.Sphere, new Vector3(0, 0.36f + 0.25f * k, 0), new Vector3(0.5f, 0.55f, 0.5f) * k, Palette.Moss);
                    if (v == 2)
                        for (int i = 0; i < 3; i++)
                        {
                            float a = i * 2.1f;
                            Box(t, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.17f, 0.75f, Mathf.Sin(a) * 0.17f),
                                Vector3.one * 0.1f, Palette.Hex("#E59AB0"));
                        }
                    break;
                }

                case Stove:
                {
                    var body = v == 1 ? Palette.Hex("#8C8C90") : v == 2 ? Terracotta : Palette.StoneDark;
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.4f, 0), new Vector3(0.9f, 0.8f, 0.7f), body);
                    var fire = Box(t, PrimitiveType.Cube, new Vector3(0, 0.3f, 0.355f), new Vector3(0.4f, 0.3f, 0.02f), Palette.Ink);
                    fire.GetComponent<Renderer>().sharedMaterial = Palette.LanternMat();
                    Box(t, PrimitiveType.Cylinder, new Vector3(0, 0.8f + 1.0f, -0.15f), new Vector3(0.18f, 1.0f, 0.18f), Metal);
                    break;
                }

                case Chest:
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.22f, 0), new Vector3(0.8f, 0.44f, 0.5f), wood);
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.5f, 0), new Vector3(0.84f, 0.14f, 0.54f), v == 1 ? Cloth[0] : wood);
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.3f, 0), new Vector3(0.86f, 0.05f, 0.56f), Metal);
                    Box(t, PrimitiveType.Cube, new Vector3(0, 0.42f, 0.27f), new Vector3(0.1f, 0.12f, 0.03f), Metal);
                    break;
            }
            return root;
        }

        static GameObject Box(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color c)
        {
            var g = GameObject.CreatePrimitive(type);
            var col = g.GetComponent<Collider>();
            col.enabled = false;
            UnityEngine.Object.Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = Palette.Mat(c);
            r.shadowCastingMode = ShadowCastingMode.On;
            return g;
        }
    }
}
