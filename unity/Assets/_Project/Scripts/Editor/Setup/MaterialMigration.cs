using UnityEditor;
using UnityEngine;

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// Roket ve kapı renk malzemelerinin kesme düzlemi destekleyen shader'ı kullanmasını sağlar.
    /// Proje açıldığında veya kod derlendiğinde kendiliğinden çalışır; sahneyi ve bölüm verisini değiştirmez.
    /// </summary>
    [InitializeOnLoad]
    static class MaterialMigration
    {
        public const string ShaderName = "AstroFlowJam/ClippableLit";
        static readonly string[] Materials = { "Red", "Blue", "Green", "Yellow", "White" };

        static MaterialMigration() => EditorApplication.delayCall += Migrate;

        public static void Migrate()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) return;

            foreach (var name in Materials)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Materials/{name}.mat");
                if (mat == null || mat.shader == shader) continue;
                // Renk ve parlaklık aynı adlı özelliklerde tutulduğu için shader değişiminde korunur.
                var color = mat.GetColor("_BaseColor");
                var smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.55f;
                mat.shader = shader;
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", smoothness);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
