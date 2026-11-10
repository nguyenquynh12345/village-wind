using UnityEngine;

namespace LangGio
{
    /// <summary>Vịt bơi chậm theo vòng quanh tâm ao.</summary>
    public class Duck : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 2f, speed = 0.25f, phase;

        Renderer[] rends;

        void Update()
        {
            // mùa đông ao đóng băng: vịt đi trú
            if (rends == null) rends = GetComponentsInChildren<Renderer>();
            bool show = Seasons.Snow < 0.5f;
            if (rends.Length > 0 && rends[0].enabled != show) foreach (var r in rends) if (r) r.enabled = show;
            if (!show) return;
            float a = phase + Time.time * speed;
            var p = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
            p.y = center.y + Mathf.Sin(Time.time * 2f + phase) * 0.02f;
            var dir = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(dir));
        }
    }
}
