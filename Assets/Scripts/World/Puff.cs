using UnityEngine;

namespace LangGio
{
    /// <summary>Một cụm khói: bay lên, nở ra, rồi tan.</summary>
    public class Puff : MonoBehaviour
    {
        float t;
        Vector3 drift;

        void Start()
        {
            drift = new Vector3(Random.Range(-0.2f, 0.2f), 1.2f, Random.Range(-0.2f, 0.2f));
            transform.localScale = Vector3.one * 0.3f;
        }

        void Update()
        {
            t += Time.deltaTime;
            transform.position += drift * Time.deltaTime;
            float k = t / 3f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.1f, k) * (1f - Mathf.Clamp01((k - 0.7f) / 0.3f));
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
