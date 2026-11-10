using UnityEngine;

namespace LangGio
{
    /// <summary>Bướm chập chờn quanh 1 điểm trên bãi hoa (ban ngày).</summary>
    public class Butterfly : MonoBehaviour
    {
        public Vector3 home;
        public float phase;
        public Transform wingL, wingR, body;

        float t;

        void Update()
        {
            bool show = !DayCycle.IsEvening && Settings.Season != 3 && Seasons.Rain < 0.3f;   // bướm trốn khi tối, mùa đông, trời mưa
            if (body) body.gameObject.SetActive(show);
            if (!show) return;
            if (!HeightField.I) return;
            t += Time.deltaTime;
            float a = t * 0.9f + phase;
            var off = new Vector3(Mathf.Sin(a) * 1.6f + Mathf.Sin(a * 2.3f) * 0.5f, 0, Mathf.Cos(a * 0.8f) * 1.4f);
            var p = home + off;
            p.y = HeightField.I.HeightAt(p.x, p.z) + 0.6f + Mathf.Abs(Mathf.Sin(t * 2.1f + phase)) * 0.5f;
            var dir = p - transform.position;
            transform.position = p;
            dir.y = 0;
            if (dir.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(dir);
            float flap = Mathf.Abs(Mathf.Sin(t * 18f + phase)) * 70f;
            if (wingL) wingL.localRotation = Quaternion.Euler(0, 0, flap);
            if (wingR) wingR.localRotation = Quaternion.Euler(0, 0, -flap);
        }
    }
}
