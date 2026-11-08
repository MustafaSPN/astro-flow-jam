using UnityEditor;
using UnityEngine;

namespace AstroFlowJam.Editor
{
    /// <summary>İçe aktarılmış FBX modellerinin hiyerarşisini, yerel dönüşümlerini, sınırlarını ve malzemelerini yazdırır.</summary>
    public static class ModelInspector
    {
        public static void Run()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Project/Art/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var go = Object.Instantiate(prefab);
                var sb = new System.Text.StringBuilder($"===== {path} scale={importer.globalScale} useFileScale={importer.useFileScale} bakeAxis={importer.bakeAxisConversion}\n");
                Dump(go.transform, 0, sb);
                Debug.Log("[Model] " + sb);
                Object.DestroyImmediate(go);
            }
        }

        static void Dump(Transform t, int depth, System.Text.StringBuilder sb)
        {
            var r = t.GetComponent<Renderer>();
            var b = r ? $" bounds min{r.bounds.min:F3} max{r.bounds.max:F3}" : "";
            var mats = r ? string.Join(",", System.Array.ConvertAll(r.sharedMaterials, m => m ? m.name : "null")) : "";
            sb.AppendLine($"{new string(' ', depth * 2)}{t.name} pos{t.localPosition:F3} rot{t.localEulerAngles:F1} scl{t.localScale:F3}{b} {mats}");
            foreach (Transform c in t) Dump(c, depth + 1, sb);
        }
    }
}
