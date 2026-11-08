using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>Gri kutu görselleri için yardımcı. Fizik kullanılmadığı için çarpıştırıcılar kaldırılır.</summary>
    public static class Primitives
    {
        public const float SillShade = 0.65f;   // kapı tabanı, kapı renginin bu oranda koyusu

        static readonly System.Collections.Generic.Dictionary<Material, Material> Darker = new();

        /// <summary>Verilen malzemenin aynı rengin koyu tonundaki kopyası; malzeme başına bir kez üretilir.</summary>
        public static Material Darken(Material source, float shade = SillShade)
        {
            if (Darker.TryGetValue(source, out var cached) && cached) return cached;
            var material = new Material(source) { name = source.name + " (koyu)" };
            var c = source.GetColor(ShaderIds.BaseColor);
            material.SetColor(ShaderIds.BaseColor, new Color(c.r * shade, c.g * shade, c.b * shade, c.a));
            Darker[source] = material;
            return material;
        }

        static readonly System.Collections.Generic.Dictionary<PrimitiveType, Mesh> Meshes = new();

        // CreatePrimitive 3D çarpıştırıcı ekler; 3D fizik modülü derlemede olmadığı için cihazda hata verir. Mesh bir kez
        // alınır, nesneler çarpıştırıcısız kurulur.
        static Mesh MeshOf(PrimitiveType type)
        {
            if (Meshes.TryGetValue(type, out var mesh) && mesh) return mesh;
            var name = type switch
            {
                PrimitiveType.Sphere => "Sphere.fbx",
                PrimitiveType.Quad => "Quad.fbx",
                PrimitiveType.Cylinder => "Cylinder.fbx",
                PrimitiveType.Capsule => "Capsule.fbx",
                PrimitiveType.Plane => "Plane.fbx",
                _ => "Cube.fbx",
            };
            mesh = Resources.GetBuiltinResource<Mesh>(name);
            Meshes[type] = mesh;
            return mesh;
        }

        public static GameObject Create(PrimitiveType type, string name, Transform parent, Material material,
            Vector3 localPosition, Vector3 localScale)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = MeshOf(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
