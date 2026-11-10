using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>Hình hiển thị của 1 tay nắm + collider để chuột bấm trúng.</summary>
    public class HandleView : MonoBehaviour
    {
        public string key;
        public HandleKind kind;
        GameObject shape;
        bool built;

        static readonly Color PointColor = Palette.Hex("#5BA8D9");
        static readonly Color ArrowColor = Palette.Hex("#E8B34E");
        static readonly Color CycleColor = Palette.Hex("#D9735A");

        public static HandleView Create(Transform parent)
        {
            var go = new GameObject("Handle");
            go.transform.SetParent(parent, false);
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.6f;
            return go.AddComponent<HandleView>();
        }

        public void Setup(string k, HandleKind kd)
        {
            key = k;
            if (built && kd == kind) return;
            kind = kd;
            built = true;
            if (shape) Destroy(shape);
            shape = new GameObject("Shape");
            shape.transform.SetParent(transform, false);

            // kiểu dáng thống nhất: lõi màu mềm + viền trắng + đĩa bóng mờ dưới chân, nhịp thở nhẹ
            switch (kind)
            {
                case HandleKind.Point:
                    Part(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.46f, Quaternion.identity, PointColor, true);
                    Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.54f, 0.025f, 0.54f), Quaternion.identity, White);   // vòng đai trắng
                    Part(PrimitiveType.Cylinder, new Vector3(0, -0.26f, 0), new Vector3(0.66f, 0.01f, 0.66f), Quaternion.identity, White);
                    break;
                case HandleKind.Arrow:
                    Cone(new Vector3(0, 0.12f, 0), new Vector3(0.3f, 0.36f, 0.3f), Quaternion.identity, ArrowColor);
                    Cone(new Vector3(0, -0.12f, 0), new Vector3(0.3f, 0.36f, 0.3f), Quaternion.Euler(180f, 0, 0), ArrowColor);
                    Part(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.26f, Quaternion.identity, White);
                    break;
                case HandleKind.Cycle:
                    Part(PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.34f, Quaternion.Euler(45f, 0, 45f), CycleColor, true);
                    Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.5f, 0.02f, 0.5f), Quaternion.identity, White);
                    break;
            }
        }

        static readonly Color White = Palette.Hex("#FBF7F0");

        void Update()
        {
            if (!shape) return;
            float t = Time.unscaledTime * 2.4f + key.GetHashCode() * 0.001f;
            shape.transform.localScale = Vector3.one * (1f + Mathf.Sin(t) * 0.05f);
            if (kind == HandleKind.Cycle) shape.transform.localRotation = Quaternion.Euler(0, Time.unscaledTime * 40f, 0);
        }

        void Part(PrimitiveType t, Vector3 pos, Vector3 scale, Quaternion rot, Color c, bool glow = false)
        {
            var g = GameObject.CreatePrimitive(t);
            var col = g.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);
            Finish(g, pos, scale, rot, c, glow);
        }

        void Cone(Vector3 pos, Vector3 scale, Quaternion rot, Color c)
        {
            var g = new GameObject("Cone");
            g.AddComponent<MeshFilter>().sharedMesh = MeshLib.Cone(12);
            g.AddComponent<MeshRenderer>();
            Finish(g, pos, scale, rot, c, true);
        }

        static readonly System.Collections.Generic.Dictionary<Color, Material> glowMats = new();

        void Finish(GameObject g, Vector3 pos, Vector3 scale, Quaternion rot, Color c, bool glow = false)
        {
            g.transform.SetParent(shape.transform, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            if (glow)
            {
                if (!glowMats.TryGetValue(c, out var m) || !m)
                {
                    m = new Material(Palette.Mat(c)) { name = "HandleGlow" };
                    Palette.Glow(m, c, 0.45f);   // sáng nhẹ, nổi trên nền cỏ / mái
                    glowMats[c] = m;
                }
                r.sharedMaterial = m;
            }
            else r.sharedMaterial = Palette.Mat(c);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
