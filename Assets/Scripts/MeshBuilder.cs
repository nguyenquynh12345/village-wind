using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Ghép mesh từ các tam giác / tứ giác. Mỗi mặt có đỉnh riêng → shading phẳng (low-poly).
    /// Truyền vào hướng pháp tuyến mong muốn để không phải nhớ chiều kim đồng hồ.
    /// </summary>
    public class MeshBuilder
    {
        readonly List<Vector3> verts = new();
        readonly List<int> tris = new();

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 want)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) >= 0) { tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); }
            else { tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); }
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) >= 0)
            {
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            else
            {
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }
        }

        public Mesh ToMesh(string name = "Generated")
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
