using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Köşeleri yuvarlatılmış dikdörtgen prizma ağı. Ön yüz kameraya (-z) bakar; yan yüzler yaylarda yumuşak,
    /// düz kenarlarda keskin gölgelenir. Aynı ölçüler için ağ bir kez üretilip paylaşılır.
    /// </summary>
    public static class RoundedBox
    {
        const int CornerSegments = 6;
        static readonly Dictionary<Vector4, Mesh> Cache = new();

        public static Mesh Get(float width, float height, float depth, float radius)
        {
            radius = Mathf.Min(radius, width / 2f, height / 2f);
            var key = new Vector4(width, height, depth, radius);
            if (Cache.TryGetValue(key, out var cached) && cached) return cached;

            var outline = new List<Vector2>();
            var normals2D = new List<Vector2>();
            var hw = width / 2f - radius;
            var hh = height / 2f - radius;
            var centers = new[] { new Vector2(hw, hh), new Vector2(-hw, hh), new Vector2(-hw, -hh), new Vector2(hw, -hh) };
            for (var c = 0; c < 4; c++)
                for (var i = 0; i <= CornerSegments; i++)
                {
                    var a = (c * 90f + 90f * i / CornerSegments) * Mathf.Deg2Rad;
                    var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    outline.Add(centers[c] + n * radius);
                    normals2D.Add(n);
                }

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            var zf = -depth / 2f;
            var zb = depth / 2f;

            // Ön ve arka yüz: merkezden yelpaze (şekil dışbükey).
            AddCap(outline, zf, Vector3.back, verts, norms, tris);
            AddCap(outline, zb, Vector3.forward, verts, norms, tris);

            // Yan yüzler
            for (var i = 0; i < outline.Count; i++)
            {
                var j = (i + 1) % outline.Count;
                var start = verts.Count;
                verts.Add(new Vector3(outline[i].x, outline[i].y, zf));
                verts.Add(new Vector3(outline[j].x, outline[j].y, zf));
                verts.Add(new Vector3(outline[j].x, outline[j].y, zb));
                verts.Add(new Vector3(outline[i].x, outline[i].y, zb));
                norms.Add(normals2D[i]);
                norms.Add(normals2D[j]);
                norms.Add(normals2D[j]);
                norms.Add(normals2D[i]);
                var outward = ((Vector3)(normals2D[i] + normals2D[j])).normalized;
                AddTri(verts, tris, start, start + 1, start + 2, outward);
                AddTri(verts, tris, start, start + 2, start + 3, outward);
            }

            var mesh = new Mesh { name = $"RoundedBox {width}x{height}" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        static void AddCap(List<Vector2> outline, float z, Vector3 normal, List<Vector3> verts, List<Vector3> norms, List<int> tris)
        {
            var center = verts.Count;
            verts.Add(new Vector3(0f, 0f, z));
            norms.Add(normal);
            for (var i = 0; i < outline.Count; i++)
            {
                verts.Add(new Vector3(outline[i].x, outline[i].y, z));
                norms.Add(normal);
            }
            for (var i = 0; i < outline.Count; i++)
                AddTri(verts, tris, center, center + 1 + i, center + 1 + (i + 1) % outline.Count, normal);
        }

        // Unity'de ön yüz, izleyiciye göre saat yönündedir; çapraz çarpım istenen normale bakmıyorsa sıra çevrilir.
        static void AddTri(List<Vector3> verts, List<int> tris, int a, int b, int c, Vector3 normal)
        {
            var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, normal) < 0f) (b, c) = (c, b);
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }
    }
}
