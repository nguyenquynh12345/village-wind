using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    [Serializable]
    public class PropParams
    {
        public int type;
        public float x, z, yaw;
        public bool raised;     // đặt trên mái / khối (y cố định) thay vì bám đất
        public float y;
        public int seed;
    }

    /// <summary>Đồ trang trí nhỏ đặt tay: đèn lồng, ghế băng, thùng gỗ, chậu hoa, đống rơm...</summary>
    public class PropEntity : Entity<PropParams>
    {
        public const int Lantern = 0, Bench = 1, Barrel = 2, Crates = 3, FlowerPot = 4, Haystack = 5,
                         Wheelbarrow = 6, Scarecrow = 7, Signpost = 8, Woodpile = 9;

        public static readonly string[] Names =
        {
            "Đèn lồng", "Ghế băng", "Thùng gỗ", "Thùng hàng", "Chậu hoa", "Đống rơm", "Xe cút kít", "Bù nhìn", "Biển chỉ đường", "Đống củi"
        };

        static readonly Color Wood = Palette.Hex("#9A7352"), WoodDark = Palette.Hex("#6B4F3E"), Iron = Palette.Hex("#4A4A4E");
        static readonly Color Straw = Palette.Hex("#E2C46B"), Terracotta = Palette.Hex("#B7684F");
        static readonly Color[] Blooms = { Palette.Hex("#E59AB0"), Palette.Hex("#F6D55C"), Palette.Hex("#FFFFFF"), Palette.Hex("#B9A3E3") };

        Light lamp;

        public override string TypeName => "Prop";
        public int Type => Mathf.Clamp(P.type, 0, Names.Length - 1);
        public Vector2 XZ => new(P.x, P.z);
        public override string Describe => $"{Names[Type]}  ·  R: xoay  ·  🔶 đổi loại";

        float BaseY => P.raised ? P.y : HeightField.I.HeightAt(P.x, P.z);

        public override void Rebuild()
        {
            var v = NewVisual();
            v.transform.SetPositionAndRotation(new Vector3(P.x, BaseY, P.z), Quaternion.Euler(0, P.yaw, 0));
            var t = v.transform;
            lamp = null;
            var rng = new System.Random(P.seed);

            switch (Type)
            {
                case Lantern:
                    Prim(PrimitiveType.Cube, t, new Vector3(0, 0.05f, 0), new Vector3(0.35f, 0.1f, 0.35f), Palette.StoneDark, true);
                    Prim(PrimitiveType.Cube, t, new Vector3(0, 1.1f, 0), new Vector3(0.1f, 2.1f, 0.1f), Iron, true);
                    Prim(PrimitiveType.Cube, t, new Vector3(0.25f, 2.1f, 0), new Vector3(0.5f, 0.06f, 0.06f), Iron);
                    var glass = Prim(PrimitiveType.Cube, t, new Vector3(0.45f, 1.85f, 0), new Vector3(0.24f, 0.32f, 0.24f), Palette.Cream);
                    glass.GetComponent<Renderer>().sharedMaterial = Palette.LanternMat();
                    {
                        // khung 4 góc + mái chóp + móc treo
                        var fr = new DetailMesh(P.seed + 17);
                        foreach (var (sx, sz) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) })
                            fr.Box(new Vector3(0.45f + sx * 0.125f, 1.85f, sz * 0.125f), new Vector3(0.03f, 0.36f, 0.03f), Quaternion.identity, Iron);
                        fr.Box(new Vector3(0.45f, 1.67f, 0), new Vector3(0.28f, 0.04f, 0.28f), Quaternion.identity, Iron);
                        fr.Taper(new Vector3(0.45f, 2.03f, 0), new Vector3(0.45f, 2.22f, 0), 0.22f, 0.025f, 4, Iron, Iron);
                        fr.Taper(new Vector3(0.45f, 2.2f, 0), new Vector3(0.45f, 2.1f, 0) + Vector3.up * 0.02f, 0.015f, 0.015f, 4, Iron, Iron);
                        fr.Build("LampFrame", t, true, Palette.VertexColorMat());
                    }
                    var lg = new GameObject("Lamp");
                    lg.transform.SetParent(t, false);
                    lg.transform.localPosition = new Vector3(0.45f, 1.8f, 0);
                    lamp = lg.AddComponent<Light>();
                    lamp.type = LightType.Point;
                    lamp.range = 7f;
                    lamp.intensity = 1.3f;
                    lamp.color = Palette.Hex("#FFC27A");
                    lamp.shadows = LightShadows.None;
                    lamp.enabled = DayCycle.IsEvening;
                    break;

                case Bench:
                    Prim(PrimitiveType.Cube, t, new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.08f, 0.45f), Wood, true);
                    Prim(PrimitiveType.Cube, t, new Vector3(0, 0.75f, -0.2f), new Vector3(1.6f, 0.35f, 0.06f), Wood);
                    foreach (var sx in new[] { -0.7f, 0.7f })
                        Prim(PrimitiveType.Cube, t, new Vector3(sx, 0.22f, 0), new Vector3(0.08f, 0.44f, 0.4f), WoodDark);
                    break;

                case Barrel:
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.45f, 0.6f), Wood, true);
                    foreach (var y in new[] { 0.2f, 0.7f })
                        Prim(PrimitiveType.Cylinder, t, new Vector3(0, y, 0), new Vector3(0.63f, 0.03f, 0.63f), Iron);
                    if (rng.Next(2) == 0)
                        Prim(PrimitiveType.Cylinder, t, new Vector3(0.55f, 0.35f, 0.2f), new Vector3(0.5f, 0.35f, 0.5f), Wood, true);
                    break;

                case Crates:
                    Prim(PrimitiveType.Cube, t, new Vector3(0, 0.3f, 0), Vector3.one * 0.6f, Wood, true);
                    Prim(PrimitiveType.Cube, t, new Vector3(0.62f, 0.25f, 0.1f), Vector3.one * 0.5f, WoodDark, true,
                         Quaternion.Euler(0, 12f, 0));
                    Prim(PrimitiveType.Cube, t, new Vector3(0.1f, 0.82f, 0.05f), Vector3.one * 0.45f, Wood, true,
                         Quaternion.Euler(0, -18f, 0));
                    break;

                case FlowerPot:
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.2f, 0), new Vector3(0.45f, 0.2f, 0.45f), Terracotta, true);
                    Prim(PrimitiveType.Sphere, t, new Vector3(0, 0.5f, 0), new Vector3(0.5f, 0.35f, 0.5f), Palette.Moss);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 1.26f;
                        Prim(PrimitiveType.Sphere, t, new Vector3(Mathf.Cos(a) * 0.15f, 0.62f, Mathf.Sin(a) * 0.15f), Vector3.one * 0.11f,
                             Blooms[rng.Next(Blooms.Length)]);
                    }
                    break;

                case Haystack:
                {
                    // đống rơm: các lớp bó chồng lên nhau, lởm chởm, hơi xoã ở chân + dây buộc
                    Prim(PrimitiveType.Sphere, t, new Vector3(0, 0.5f, 0), new Vector3(1.5f, 1.1f, 1.5f), Straw, true).GetComponent<Renderer>().enabled = false;   // giữ va chạm
                    var hs = new DetailMesh(P.seed * 7 + 3);
                    var light = Palette.Hex("#E2C46A"); var dark = Palette.Hex("#B8913E");
                    hs.Blob(new Vector3(0, 0.42f, 0), new Vector3(0.82f, 0.48f, 0.82f), hs.Vary(Straw, 0.04f), dark, 0.09f, 12, 7);
                    hs.Blob(new Vector3(0, 0.78f, 0), new Vector3(0.66f, 0.42f, 0.66f), hs.Vary(light, 0.04f), Straw, 0.09f, 12, 7);
                    hs.Blob(new Vector3(0.03f, 1.1f, 0), new Vector3(0.42f, 0.32f, 0.42f), hs.Vary(light, 0.05f), Straw, 0.1f, 10, 6);
                    for (int k = 0; k < 14; k++)   // nắm rơm xoã quanh chân
                    {
                        float a = k / 14f * Mathf.PI * 2f + hs.Range(-0.2f, 0.2f);
                        var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        hs.Blob(d * hs.Range(0.78f, 0.9f) + Vector3.up * 0.08f, new Vector3(0.18f, 0.09f, 0.18f), hs.Vary(light, 0.06f), dark, 0.25f, 6, 4);
                    }
                    hs.Taper(new Vector3(0, 0.6f, 0) + Vector3.left * 0.0f, new Vector3(0, 0.66f, 0), 0.8f, 0.79f, 14, Palette.Hex("#8A6A3A"), Palette.Hex("#8A6A3A"));   // dây buộc
                    hs.Build("Hay", t, true, Palette.VertexColorMat(Palette.PatWood));
                    break;
                }

                case Wheelbarrow:
                    Prim(PrimitiveType.Cube, t, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.3f, 0.9f), Wood, true, Quaternion.Euler(-8f, 0, 0));
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.22f, 0.55f), new Vector3(0.44f, 0.04f, 0.44f), WoodDark, false, Quaternion.Euler(0, 0, 90f));
                    foreach (var sx in new[] { -0.22f, 0.22f })
                    {
                        Prim(PrimitiveType.Cube, t, new Vector3(sx, 0.5f, -0.65f), new Vector3(0.05f, 0.05f, 0.6f), WoodDark);
                        Prim(PrimitiveType.Cube, t, new Vector3(sx, 0.2f, -0.25f), new Vector3(0.05f, 0.4f, 0.05f), WoodDark);
                    }
                    break;

                case Scarecrow:
                    Prim(PrimitiveType.Cube, t, new Vector3(0, 0.9f, 0), new Vector3(0.08f, 1.8f, 0.08f), WoodDark, true);
                    Prim(PrimitiveType.Cube, t, new Vector3(0, 1.35f, 0), new Vector3(1.2f, 0.07f, 0.07f), WoodDark);
                    Prim(PrimitiveType.Cube, t, new Vector3(0, 1.15f, 0), new Vector3(0.45f, 0.6f, 0.25f), Palette.Hex("#6F8FB0"));
                    Prim(PrimitiveType.Sphere, t, new Vector3(0, 1.7f, 0), Vector3.one * 0.32f, Straw);
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0, 1.88f, 0), new Vector3(0.5f, 0.02f, 0.5f), Palette.Hex("#8A6A4F"));
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0, 1.96f, 0), new Vector3(0.26f, 0.08f, 0.26f), Palette.Hex("#8A6A4F"));
                    {
                        // rơm lòi ra ở cổ tay, gấu áo, dưới mũ; mắt, miếng vá áo
                        var sc = new DetailMesh(P.seed + 29);
                        var straw2 = Palette.Hex("#D9B85C");
                        foreach (var sx in new[] { -1f, 1f })
                            for (int k = 0; k < 4; k++)
                                sc.Taper(new Vector3(sx * 0.58f, 1.35f, 0), new Vector3(sx * (0.72f + sc.Range(0f, 0.06f)), 1.35f + sc.Range(-0.12f, 0.06f), sc.Range(-0.06f, 0.06f)), 0.02f, 0.006f, 3, straw2, Straw);
                        for (int k = 0; k < 9; k++)
                        {
                            float x = -0.2f + k * 0.05f;
                            sc.Taper(new Vector3(x, 0.86f, 0), new Vector3(x + sc.Range(-0.03f, 0.03f), 0.72f + sc.Range(-0.05f, 0.03f), sc.Range(-0.05f, 0.05f)), 0.02f, 0.006f, 3, straw2, Straw);
                        }
                        for (int k = 0; k < 10; k++)
                        {
                            float a = k / 10f * Mathf.PI * 2f;
                            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                            sc.Taper(new Vector3(0, 1.86f, 0) + d * 0.12f, new Vector3(0, 1.78f, 0) + d * 0.26f, 0.015f, 0.005f, 3, straw2, Straw);
                        }
                        foreach (var ex in new[] { -0.06f, 0.06f })
                            sc.Blob(new Vector3(ex, 1.73f, 0.155f), Vector3.one * 0.025f, Palette.Ink, Palette.Ink, 0f, 5, 3);
                        sc.Box(new Vector3(0.1f, 1.05f, 0.13f), new Vector3(0.14f, 0.13f, 0.01f), Quaternion.Euler(0, 0, 8f), Palette.Hex("#C9604A"));
                        sc.Build("Straw", t, true, Palette.VertexColorMat());
                    }
                    break;

                case Signpost:
                    Prim(PrimitiveType.Cube, t, new Vector3(0, 0.8f, 0), new Vector3(0.1f, 1.6f, 0.1f), WoodDark, true);
                    Prim(PrimitiveType.Cube, t, new Vector3(0.3f, 1.35f, 0), new Vector3(0.6f, 0.2f, 0.04f), Wood, false, Quaternion.Euler(0, 0, 4f));
                    Prim(PrimitiveType.Cube, t, new Vector3(-0.25f, 1.05f, 0), new Vector3(0.5f, 0.18f, 0.04f), Wood, false, Quaternion.Euler(0, 25f, -3f));
                    break;

                case Woodpile:
                    for (int row = 0; row < 3; row++)
                        for (int i = 0; i < 4 - row; i++)
                            Prim(PrimitiveType.Cylinder, t, new Vector3((i - (3 - row) * 0.5f) * 0.24f, 0.11f + row * 0.2f, 0),
                                 new Vector3(0.22f, 0.45f, 0.22f), i % 2 == 0 ? Wood : Palette.Hex("#B98A5E"), row == 0,
                                 Quaternion.Euler(90f, 0, 0));
                    break;
            }
            ApplyTint();
        }

        void Update()
        {
            if (lamp) lamp.enabled = DayCycle.IsEvening;
        }

        public override void Translate(Vector3 d)
        {
            P.x += d.x;
            P.z += d.z;
            if (P.raised) P.raised = false;   // kéo đi → về lại mặt đất
            Rebuild();
        }

        public override void Rotate(float degrees)
        {
            P.yaw = Mathf.Repeat(P.yaw + degrees, 360f);
            Rebuild();
        }

        public override bool AffectedBy(Rect r) => !P.raised && r.Contains(new Vector2(P.x, P.z));

        public override void GetHandles(List<HandleDef> list)
        {
            list.Add(new HandleDef("type", HandleKind.Cycle, new Vector3(P.x, BaseY + 2.4f, P.z)));
        }

        public override void ClickHandle(string key)
        {
            if (key != "type") return;
            P.type = (Type + 1) % Names.Length;
            Rebuild();
        }

        public override void PlayPop(float delay)
        {
            if (visual) PopIn.Add(visual, delay, false, 0.3f);
        }
    }
}
