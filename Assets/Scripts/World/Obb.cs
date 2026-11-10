using UnityEngine;

namespace LangGio
{
    /// <summary>Hình chữ nhật xoay trên mặt đất (x, z) — dùng kiểm tra nền nhà chồng lên vật khác.</summary>
    public struct Obb
    {
        public Vector2 c, ax, az;   // tâm, trục x và trục z (đơn vị)
        public float hx, hz;        // nửa rộng, nửa dài

        public static Obb From(float x, float z, float angleDeg, float w, float l)
        {
            var r = Quaternion.Euler(0, angleDeg, 0);
            var X = r * Vector3.right;
            var Z = r * Vector3.forward;
            return new Obb
            {
                c = new Vector2(x, z),
                ax = new Vector2(X.x, X.z),
                az = new Vector2(Z.x, Z.z),
                hx = w * 0.5f,
                hz = l * 0.5f
            };
        }

        public bool Contains(Vector2 p, float margin)
        {
            var d = p - c;
            return Mathf.Abs(Vector2.Dot(d, ax)) <= hx + margin && Mathf.Abs(Vector2.Dot(d, az)) <= hz + margin;
        }

        public Vector2 Corner(int i)
        {
            float sx = (i == 0 || i == 3) ? -1f : 1f;
            float sz = i < 2 ? -1f : 1f;
            return c + ax * (hx * sx) + az * (hz * sz);
        }

        /// <summary>Định lý trục tách (SAT) cho 2 hình chữ nhật.</summary>
        public bool Overlaps(Obb o, float margin = 0f)
        {
            return !Separated(this, o, ax, margin) && !Separated(this, o, az, margin)
                && !Separated(this, o, o.ax, margin) && !Separated(this, o, o.az, margin);
        }

        static bool Separated(Obb a, Obb b, Vector2 axis, float margin)
        {
            float ra = a.hx * Mathf.Abs(Vector2.Dot(a.ax, axis)) + a.hz * Mathf.Abs(Vector2.Dot(a.az, axis));
            float rb = b.hx * Mathf.Abs(Vector2.Dot(b.ax, axis)) + b.hz * Mathf.Abs(Vector2.Dot(b.az, axis));
            float dist = Mathf.Abs(Vector2.Dot(b.c - a.c, axis));
            return dist > ra + rb + margin;
        }
    }
}
