using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Katmanlı bloğun çekirdeği: hücrelerin birleşiminden içeri çekilmiş, dış köşeleri yuvarlak, üstü yastık gibi kabarık tek
    /// parça şekil. Yükseklik alanı ızgarası: kenarda yükseklik 0 (dış bloğun üst yüzüyle aynı düzlem, kenar tırtığı
    /// görünmez), içe doğru çeyrek daire profiliyle 'height'e çıkar. Şekil işaretli uzaklıkla tanımlanır: her hücre ve dolu
    /// komşusuna köprü birer kutu; birleşim önce 'round' kadar daraltılıp sonra genişletilir (dış köşeler yuvarlanır).
    /// Mesh model kabının sahne eksenlerindedir: x, y tahta düzlemi, −z kameraya doğru yükseklik.
    /// </summary>
    public static class CoreMesh
    {
        const int PerCell = 24;   // hücre başına ızgara çözünürlüğü

        public static Mesh Build(IEnumerable<Vector2Int> cellList, Vector2Int size, float inset, float round, float bevel, float height)
        {
            var cells = new HashSet<Vector2Int>(cellList);
            var boxes = new List<(Vector2 center, Vector2 half)>();
            var h = 0.5f - inset;
            foreach (var c in cells)
            {
                var center = new Vector2(c.x + 0.5f - size.x / 2f, c.y + 0.5f - size.y / 2f);
                boxes.Add((center, new Vector2(h, h)));
                if (cells.Contains(c + Vector2Int.right)) boxes.Add((center + new Vector2(0.5f, 0f), new Vector2(0.5f, h)));
                if (cells.Contains(c + Vector2Int.up)) boxes.Add((center + new Vector2(0f, 0.5f), new Vector2(h, 0.5f)));
            }

            float Sdf(Vector2 p)
            {
                var d = float.MaxValue;
                foreach (var (center, half) in boxes)
                {
                    var q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - (half - Vector2.one * round);
                    var box = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
                    d = Mathf.Min(d, box);
                }
                return d - round;
            }

            int nx = size.x * PerCell + 1, ny = size.y * PerCell + 1;
            var vertices = new Vector3[nx * ny];
            var inside = new bool[nx * ny];
            for (var j = 0; j < ny; j++)
            for (var i = 0; i < nx; i++)
            {
                var p = new Vector2(i / (float)PerCell - size.x / 2f, j / (float)PerCell - size.y / 2f);
                var d = Sdf(p);
                var t = Mathf.Clamp01(-d / bevel);
                var z = height * Mathf.Sqrt(1f - (1f - t) * (1f - t));
                vertices[j * nx + i] = new Vector3(p.x, p.y, -z);
                inside[j * nx + i] = d <= 0.5f / PerCell;
            }

            var triangles = new List<int>();
            for (var j = 0; j < ny - 1; j++)
            for (var i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                if (!(inside[a] && inside[b] && inside[c] && inside[d])) continue;
                // Kamera −z'den baktığı için saat yönünde sıra (Unity sol el): ön yüz kameraya döner.
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }

            var mesh = new Mesh { name = "Layer Core", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
