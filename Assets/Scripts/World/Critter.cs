using UnityEngine;

namespace LangGio
{
    /// <summary>Con vật đi lang thang quanh 1 ngôi nhà (cừu, gà). Ban đêm đứng yên.</summary>
    public class Critter : MonoBehaviour
    {
        public int houseId;
        public float minDist = 3f, radius = 10f, speed = 0.6f, hopHeight = 0.05f, hopRate = 8f;
        public Transform body;

        Vector3 target;
        float wait, t;
        bool hasTarget;

        void Update()
        {
            var h = EntityManager.Get(houseId) as HouseEntity;
            if (!h) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            t += dt;
            var hf = HeightField.I;

            if (DayCycle.IsNight) { if (body) body.localPosition = Vector3.zero; return; }

            if (!hasTarget || wait > 0f)
            {
                wait -= dt;
                // mổ / gặm cỏ: cúi nhẹ
                if (body) body.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(t * 3f)) * 12f, 0, 0);
                if (wait <= 0f) hasTarget = PickTarget(h);
                return;
            }

            var pos = transform.position;
            var d = target - pos;
            d.y = 0;
            if (d.magnitude < 0.25f)
            {
                hasTarget = false;
                wait = Random.Range(2f, 8f);
                return;
            }
            var dir = d.normalized;
            var next = pos + dir * speed * dt;
            if (!Fauna.Walkable(new Vector2(next.x, next.z))) { hasTarget = false; wait = 1f; return; }
            next.y = hf.HeightAt(next.x, next.z);
            transform.position = next;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-6f * dt));
            if (body)
            {
                body.localRotation = Quaternion.identity;
                body.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(t * hopRate)) * hopHeight;
            }
        }

        bool PickTarget(HouseEntity h)
        {
            var c = h.LocalToWorld(Vector3.zero);
            for (int i = 0; i < 10; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(minDist, radius);
                var p = new Vector2(c.x + Mathf.Cos(a) * r, c.z + Mathf.Sin(a) * r);
                // ưu tiên điểm không quá xa chỗ đang đứng
                if (Vector2.Distance(p, new Vector2(transform.position.x, transform.position.z)) > radius) continue;
                if (!Fauna.Walkable(p)) continue;
                target = new Vector3(p.x, 0, p.y);
                return true;
            }
            wait = 2f;
            return false;
        }
    }
}
