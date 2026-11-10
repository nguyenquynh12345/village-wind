using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>Vòng tròn vẽ bám mặt đất (cọ địa hình, chỗ đặt tháp, điểm hút).</summary>
    public class GroundRing
    {
        const int Segments = 48;
        LineRenderer lr;

        void Ensure()
        {
            if (lr) return;
            var go = new GameObject("GroundRing");
            lr = go.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.positionCount = Segments;
            lr.widthMultiplier = 0.14f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        public void Show(Vector3 center, float radius, Color color)
        {
            Ensure();
            lr.gameObject.SetActive(true);
            lr.startColor = lr.endColor = color;
            var hf = HeightField.I;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                float x = center.x + Mathf.Cos(a) * radius, z = center.z + Mathf.Sin(a) * radius;
                lr.SetPosition(i, new Vector3(x, hf.HeightAt(x, z) + 0.12f, z));
            }
        }

        public void Hide()
        {
            if (lr) lr.gameObject.SetActive(false);
        }
    }
}
