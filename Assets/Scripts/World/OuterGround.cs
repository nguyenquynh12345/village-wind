using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Vành đất cỏ bao quanh vùng xây (±WorldHalf) trải ra rất xa, khép vào sương —
    /// nhìn ngang không còn thấy mép mặt đất hay khoảng trống xám ở góc khung hình.
    /// Vành có lỗ vuông đúng bằng vùng xây nên không che ao / chỗ hạ đất.
    /// </summary>
    public class OuterGround : MonoBehaviour
    {
        void Start()
        {
            float a = GameConfig.WorldHalf - 0.05f, b = 900f;
            int rings = 6;
            var grass = Palette.Hex("#6C9A4B");
            var far = Palette.Hex("#86AA5C");
            var dm = new System.Collections.Generic.List<Vector3>();
            var cols = new System.Collections.Generic.List<Color>();
            var tris = new System.Collections.Generic.List<int>();
            var hf = HeightField.I;

            // các vòng vuông đồng tâm từ a → b (khoảng cách tăng dần), mỗi vòng 4 cạnh × 8 đoạn
            float[] radii = new float[rings + 1];
            for (int r = 0; r <= rings; r++) radii[r] = Mathf.Lerp(a, b, Mathf.Pow(r / (float)rings, 2.2f));
            const int seg = 8;
            int per = seg * 4;
            for (int r = 0; r <= rings; r++)
            {
                float R = radii[r];
                for (int k = 0; k < per; k++)
                {
                    int side = k / seg;
                    float t = (k % seg) / (float)seg * 2f - 1f;
                    Vector2 p = side switch
                    {
                        0 => new Vector2(t * R, -R),
                        1 => new Vector2(R, t * R),
                        2 => new Vector2(-t * R, R),
                        _ => new Vector2(-R, -t * R),
                    };
                    // vòng trong cùng bám đúng độ cao mép địa hình; ra xa nhấp nhô nhẹ như đồi thoải
                    float y = r == 0 && hf ? hf.HeightAt(Mathf.Clamp(p.x, -a, a), Mathf.Clamp(p.y, -a, a)) - 0.02f
                                           : Mathf.PerlinNoise(p.x * 0.006f + 3f, p.y * 0.006f) * Mathf.Min(r, 3) * 2.2f - 0.02f;
                    dm.Add(new Vector3(p.x, y, p.y));
                    cols.Add(Color.Lerp(grass, far, (r / (float)rings) * 0.8f + Mathf.PerlinNoise(p.x * 0.02f, p.y * 0.02f) * 0.2f));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int k = 0; k < per; k++)
                {
                    int i0 = r * per + k, i1 = r * per + (k + 1) % per;
                    int j0 = i0 + per, j1 = i1 + per;
                    tris.Add(i0); tris.Add(i1); tris.Add(j0);
                    tris.Add(i1); tris.Add(j1); tris.Add(j0);
                }

            var mesh = new Mesh { name = "OuterGround" };
            mesh.SetVertices(dm);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Palette.VertexColorMat(Palette.PatGrass);
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
