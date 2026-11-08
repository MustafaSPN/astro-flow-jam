using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// Art/Models altındaki FBX'leri oyuna bağlar: içe aktarma ayarlarını düzenler (animasyon, kamera, ışık yok), ModelLibrary
    /// asset'ini kurar ve Prototype sahnesindeki LevelController'a atar.
    /// Komut satırından: -executeMethod AstroFlowJam.Editor.ModelSetup.Run
    /// </summary>
    public static class ModelSetup
    {
        const string ModelsDir = "Assets/_Project/Art/Models";
        public const string LibraryPath = "Assets/_Project/Data/ModelLibrary.asset";
        const string ScenePath = "Assets/_Project/Scenes/Prototype.unity";

        [MenuItem("Astro Flow Jam/Modelleri Bağla")]
        public static void Run()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.addCollider = false;
                importer.isReadable = false;
                // Yuvarlak tahta kiti: normaller keskin kenarlı yeniden hesaplanır; düz yüzeyler aynı tonda ışık alır
                // (kapının iki kapağı farklı tonda görünmesin).
                if (path.Contains("AFJ_Rounded_"))
                {
                    importer.importNormals = ModelImporterNormals.Calculate;
                    importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
                    importer.normalSmoothingAngle = 40f;
                }
                importer.SaveAndReimport();
            }

            CreateOrUpdateLibrary();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Sahne açılınca kullanılmayan asset'ler boşaltılır; kütüphane sahneden sonra yeniden yüklenir.
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(LibraryPath);
            Assign(Object.FindFirstObjectByType<LevelController>(), library);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Modeller bağlandı: " + LibraryPath);
        }

        public static ModelLibrary CreateOrUpdateLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ModelLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.block = Load("AFJ_Block_1x1_Seamless");
            library.blockConnector = Load("AFJ_Block_Connector_Seamless");
            library.rocket = Load("AFJ_Rounded_Rocket_Parts");
            library.pixel = Load("AFJ_Pixel_1x1");
            library.gate = Load("AFJ_Simple_Gate_2Units");
            library.floorTile = Load("AFJ_Floor_Tile_1x1");
            library.wallStraight = Load("AFJ_Simple_Wall_1Unit");
            library.wallCorner = Load("AFJ_Simple_Wall_Corner");
            library.orbit = Load("AFJ_Orbit_With_Depot");
            library.cartridge = Load("AFJ_Energy_Cartridge");
            library.rewardDoor = Load("AFJ_Simple_Reward_Door");
            library.padlock = Load("AFJ_Space_Padlock");
            library.arrowShaft = Load("AFJ_Arrow_Shaft");
            library.arrowHeadLeft = Load("AFJ_Arrow_Head_Left");
            library.arrowHeadRight = Load("AFJ_Arrow_Head_Right");
            library.keyEmblem = Load("AFJ_Key_Emblem");
            library.lockHub = Load("AFJ_Lock_Hub");
            library.lockArm = Load("AFJ_Lock_Arm_1Unit");
            library.lockAnchor = Load("AFJ_Lock_Anchor");
            library.lock1x1 = Load("AFJ_Lock_1x1");
            library.ufo = Load("UfoBooster/AFJ_UFO");
            library.asteroid1x1 = Load("AsteroidObstacles/AFJ_Asteroid_1x1");
            library.asteroid1x2 = Load("AsteroidObstacles/AFJ_Asteroid_1x2");
            library.asteroidL = Load("AsteroidObstacles/AFJ_Asteroid_L");
            library.asteroidT = Load("AsteroidObstacles/AFJ_Asteroid_T");
            library.flameMaterial = CreateFlameMaterial();
            library.neonMaterial = CreateNeonMaterial();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        public static void Assign(LevelController controller, ModelLibrary library)
        {
            if (controller == null)
            {
                Debug.LogError("Sahnede LevelController bulunamadı.");
                return;
            }
            var so = new SerializedObject(controller);
            var property = so.FindProperty("models");
            property.objectReferenceValue = library;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        const string FlameTexturePath = "Assets/_Project/Art/FX/FlameParticle.png";
        const string FlameMaterialPath = "Assets/_Project/Materials/FlameParticle.mat";

        // Yumuşak kenarlı yuvarlak parçacık dokusu ve toplamalı (additive) parçacık malzemesi.
        static Material CreateFlameMaterial()
        {
            System.IO.Directory.CreateDirectory("Assets/_Project/Art/FX");
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                    var a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            System.IO.File.WriteAllBytes(FlameTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(FlameTexturePath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FlameTexturePath);

            var material = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, FlameMaterialPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Yörünge ışıkları ve neon cisimler: ışıktan etkilenmeyen düz renk (URP Unlit). Renk çalışma anında kopyalara verilir.
        static Material CreateNeonMaterial()
        {
            const string path = "Assets/_Project/Materials/Neon.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject Load(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/{name}.fbx");
            if (model == null) Debug.LogError($"Model bulunamadı: {ModelsDir}/{name}.fbx");
            return model;
        }
    }
}
