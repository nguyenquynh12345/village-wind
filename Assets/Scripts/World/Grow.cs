using UnityEngine;

namespace LangGio
{
    /// <summary>Cây mới trồng: nhỏ 60% rồi lớn dần tới đủ cỡ.</summary>
    public class Grow : MonoBehaviour
    {
        public float duration = GameConfig.TreeGrowTime;
        float t;

        void Start() => transform.localScale = Vector3.one * 0.6f;

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, k * (2f - k));
            if (k >= 1f) Destroy(this);
        }
    }
}
