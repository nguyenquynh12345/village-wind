using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>Vẽ tạm các ô của mặt nạ ao (màu trong suốt) bám mặt đất — xem trước khi quét cọ nước.</summary>
    public class MaskPreview
    {
        GameObject go;
        Mesh mesh;
        Material mat;

        public void Show(PondMask m, Color color)
        {
            if (m == null) { Hide(); return; }
            if (!go)
            {
                go = new GameObject("MaskPreview");
                mesh = new Mesh { name = "MaskPreview" };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mat = new Material(Shader.Find("Sprites/Default"));
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            go.SetActive(true);
            mat.color = color;

            var hf = HeightField.I;
            const float C = PondMask.C;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int gz = m.z0; gz < m.z0 + m.h; gz++)
                for (int gx = m.x0; gx < m.x0 + m.w; gx++)
                {
                    if (!m.Get(gx, gz)) continue;
                    int i = verts.Count;
                    float x = gx * C, z = gz * C;
                    verts.Add(new Vector3(x, hf.HeightAt(x, z) + 0.08f, z));
                    verts.Add(new Vector3(x + C, hf.HeightAt(x + C, z) + 0.08f, z));
                    verts.Add(new Vector3(x, hf.HeightAt(x, z + C) + 0.08f, z + C));
                    verts.Add(new Vector3(x + C, hf.HeightAt(x + C, z + C) + 0.08f, z + C));
                    tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                    tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);
                }
            mesh.Clear();
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        public void Hide()
        {
            if (go) go.SetActive(false);
        }
    }
}
