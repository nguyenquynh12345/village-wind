using UnityEngine;

namespace LangGio
{
    /// <summary>Chim bay vòng trên làng ban ngày (vỗ cánh / lượn), tối thì ẩn.</summary>
    public class Bird : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 20f, height = 20f, speed = 0.25f, phase;
        public Transform wingL, wingR, body;

        float t;

        void Update()
        {
            bool show = !DayCycle.IsNight;
            if (body) body.gameObject.SetActive(show);
            if (!show) return;
            t += Time.deltaTime;
            float a = phase + t * speed;
            float r = radius + Mathf.Sin(t * 0.3f + phase) * 4f;
            float y = height + Mathf.Sin(t * 0.7f + phase * 2f) * 2f;
            var p = center + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            var tangent = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0, 0, -12f);
            float flap = Mathf.Sin(t * 14f + phase * 5f);
            bool glide = Mathf.Sin(t * 0.9f + phase) > 0.3f;
            float ang = glide ? 5f : flap * 35f;
            if (wingL) wingL.localRotation = Quaternion.Euler(0, 0, ang);
            if (wingR) wingR.localRotation = Quaternion.Euler(0, 0, -ang);
        }
    }
}
