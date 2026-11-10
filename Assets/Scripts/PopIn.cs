using UnityEngine;

namespace LangGio
{
    /// <summary>Hiệu ứng "nảy" khi vật thể mọc lên (easeOutBack).</summary>
    public class PopIn : MonoBehaviour
    {
        public float delay, duration = 0.35f;
        public bool vertical;
        Vector3 target;
        float t;
        bool sounded;

        public static void Add(GameObject go, float delay, bool vertical = false, float duration = 0.35f)
        {
            var p = go.AddComponent<PopIn>();
            p.delay = delay;
            p.vertical = vertical;
            p.duration = duration;
        }

        void Start()
        {
            target = transform.localScale;
            transform.localScale = vertical ? new Vector3(target.x, 0.0001f, target.z) : target * 0.0001f;
            t = -delay;
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t < 0) return;
            if (!sounded) { sounded = true; Sfx.Pop(); }
            float k = Mathf.Clamp01(t / duration);
            float e = Mathf.Max(0.0001f, EaseOutBack(k));
            transform.localScale = vertical ? new Vector3(target.x, target.y * e, target.z) : target * e;
            if (k >= 1f)
            {
                transform.localScale = target;
                Destroy(this);
            }
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
        }
    }
}
