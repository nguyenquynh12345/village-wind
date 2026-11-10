using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>Mesh dùng chung (sinh 1 lần, cache lại).</summary>
    public static class MeshLib
    {
        static readonly Dictionary<int, Mesh> cones = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cones.Clear();

        /// <summary>Hình nón đáy bán kính 1, cao 1, đáy tại y = 0. sides = 4 → kim tự tháp.</summary>
        public static Mesh Cone(int sides = 14)
        {
            if (cones.TryGetValue(sides, out var m) && m) return m;
            var mb = new MeshBuilder();
            var apex = Vector3.up;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                var p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                var outward = ((p0 + p1) * 0.5f).normalized + Vector3.up * 0.6f;
                mb.Tri(p0, p1, apex, outward);
                mb.Tri(p0, p1, Vector3.zero, Vector3.down);
            }
            m = mb.ToMesh("Cone" + sides);
            cones[sides] = m;
            return m;
        }
    }
}
