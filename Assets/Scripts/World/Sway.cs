using UnityEngine;

namespace LangGio
{
    /// <summary>Cây đung đưa nhẹ (±2°) theo gió.</summary>
    public class Sway : MonoBehaviour
    {
        float phase, speed;
        Quaternion baseRot;

        void Start()
        {
            baseRot = transform.localRotation;
            phase = Mathf.Repeat(transform.position.x * 0.37f + transform.position.z * 0.53f, 6.28f);
            speed = 0.8f + Mathf.Abs(Mathf.Sin(phase)) * 0.6f;
        }

        void Update()
        {
            float t = Time.time * speed + phase;
            transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(t) * 2f, 0, Mathf.Cos(t * 0.7f) * 2f);
        }
    }
}
