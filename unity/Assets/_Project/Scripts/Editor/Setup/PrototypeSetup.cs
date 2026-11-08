using System.Collections.Generic;
using System.IO;
using AstroFlowJam.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// Gri kutu prototipini tek adımda kurar: malzemeler, örnek bölümler, bölüm sırası, sahne ve iOS oyuncu ayarları.
    /// Mevcut bölüm dosyalarının üzerine yazmaz; yalnızca eksik olanları oluşturur.
    /// Komut satırından: -executeMethod AstroFlowJam.Editor.PrototypeSetup.Run
    /// </summary>
    public static class PrototypeSetup
    {
        const string Root = "Assets/_Project";
        const string MaterialsDir = Root + "/Materials";
        const string DataDir = Root + "/Data";
        const string LevelsDir = DataDir + "/Levels";
        const string ScenePath = Root + "/Scenes/Prototype.unity";

        [MenuItem("Astro Flow Jam/Prototipi Kur")]
        public static void Run()
        {
            foreach (var dir in new[] { MaterialsDir, LevelsDir, Root + "/Scenes" })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            var palette = CreatePalette();
            MaterialMigration.Migrate();
            var database = CreateLevels();
            CreateScene(palette, database);
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("Astro Flow Jam prototipi kuruldu: " + ScenePath);
        }

        static PrototypePalette CreatePalette()
        {
            var palette = LoadOrCreate<PrototypePalette>(DataDir + "/PrototypePalette.asset");
            palette.red = Mat("Red", "E5484D");
            palette.blue = Mat("Blue", "3E7BFA");
            palette.green = Mat("Green", "3CC46B");
            palette.yellow = Mat("Yellow", "F5C842");
            palette.boardTile = Mat("BoardTile", "2A3152", 0.2f);
            palette.boardFrame = Mat("BoardFrame", "4A5378", 0.3f);
            palette.orbitTrack = Mat("OrbitTrack", "5B6694", 0.3f);
            palette.picture = Mat("Picture", "1C2140", 0.1f);
            palette.white = Mat("White", "F2F2F2");
            EditorUtility.SetDirty(palette);
            return palette;
        }

        static Material Mat(string name, string hex, float smoothness = 0.55f)
        {
            var path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static LevelDatabase CreateLevels()
        {
            var levels = new List<LevelData>
            {
                Level("L01", "Bölüm 1 – Kalibrasyon", 180f, "drag-and-orbit",
                    new[]
                    {
                        Rocket("R1", GameColor.Red, 0, 1, 1, 1),
                        Rocket("R2", GameColor.Blue, 1, 2, 1, 1),
                        Rocket("R3", GameColor.Green, 4, 1, 1, 1),
                        Rocket("R4", GameColor.Yellow, 5, 2, 1, 1),
                    },
                    new[]
                    {
                        new ExitSpec(GameColor.Red, Edge.Top, 0, 1),
                        new ExitSpec(GameColor.Blue, Edge.Top, 1, 1),
                        new ExitSpec(GameColor.Green, Edge.Top, 4, 1),
                        new ExitSpec(GameColor.Yellow, Edge.Top, 5, 1),
                    }),

                Level("P02", "Prototip – Jam testi", 150f, "big-rocket",
                    new[]
                    {
                        Rocket("R1", GameColor.Red, 0, 4, 2, 1),
                        Rocket("Y1", GameColor.Yellow, 1, 5, 1, 1),
                        Rocket("B1", GameColor.Blue, 4, 3, 1, 2),
                        Rocket("G1", GameColor.Green, 2, 3, 1, 1),
                        Rocket("Y2", GameColor.Yellow, 4, 1, 1, 2),
                        Rocket("B2", GameColor.Blue, 1, 1, 2, 1),
                        Rocket("G2", GameColor.Green, 5, 0, 1, 1),
                    },
                    new[]
                    {
                        new ExitSpec(GameColor.Red, Edge.Top, 0, 2),
                        new ExitSpec(GameColor.Blue, Edge.Top, 4, 1),
                        new ExitSpec(GameColor.Green, Edge.Right, 3, 1),
                        new ExitSpec(GameColor.Yellow, Edge.Left, 1, 2),
                        new ExitSpec(GameColor.Blue, Edge.Bottom, 2, 2),
                    }),

                Level("P03", "Prototip – Kapasite testi", 150f, "orbit-capacity",
                    new[]
                    {
                        Rocket("R1", GameColor.Red, 0, 5, 1, 1),
                        Rocket("R2", GameColor.Red, 1, 4, 1, 1),
                        Rocket("B1", GameColor.Blue, 2, 4, 1, 2),
                        Rocket("B2", GameColor.Blue, 3, 5, 1, 1),
                        Rocket("G1", GameColor.Green, 4, 5, 2, 1),
                        Rocket("Y1", GameColor.Yellow, 0, 0, 1, 1),
                        Rocket("Y2", GameColor.Yellow, 2, 0, 2, 1),
                        Rocket("Y3", GameColor.Yellow, 5, 0, 1, 1),
                    },
                    new[]
                    {
                        new ExitSpec(GameColor.Red, Edge.Top, 0, 2),
                        new ExitSpec(GameColor.Blue, Edge.Top, 2, 2),
                        new ExitSpec(GameColor.Green, Edge.Top, 4, 2),
                        new ExitSpec(GameColor.Yellow, Edge.Bottom, 0, 6),
                    }),
            };

            var database = LoadOrCreate<LevelDatabase>(DataDir + "/LevelDatabase.asset");
            database.levels = levels;
            EditorUtility.SetDirty(database);
            return database;
        }

        static LevelData Level(string id, string name, float time, string mechanic, RocketSpec[] rockets, ExitSpec[] exits)
        {
            var path = $"{LevelsDir}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (existing != null) return existing;

            var level = ScriptableObject.CreateInstance<LevelData>();
            level.levelId = id;
            level.displayName = name;
            level.timeLimit = time;
            level.introducesMechanic = mechanic;
            level.rockets = new List<RocketSpec>(rockets);
            level.exits = new List<ExitSpec>(exits);
            AssetDatabase.CreateAsset(level, path);
            return level;
        }

        static RocketSpec Rocket(string id, GameColor color, int x, int y, int w, int h) =>
            new(id, color, new Vector2Int(x, y), new Vector2Int(w, h), w * h > 1 ? 40 : 20);

        static void CreateScene(PrototypePalette palette, LevelDatabase database)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            ColorUtility.TryParseHtmlString("#0F1226", out var bg);
            cam.backgroundColor = bg;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.transform.position = new Vector3(0f, 8f, -20f);
            camGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.6f);

            var controller = new GameObject("Level Controller").AddComponent<LevelController>();
            var so = new SerializedObject(controller);
            so.FindProperty("database").objectReferenceValue = database;
            so.FindProperty("palette").objectReferenceValue = palette;
            so.FindProperty("worldCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
            ModelSetup.Assign(controller, ModelSetup.CreateOrUpdateLibrary());

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Astro Flow Jam";
            PlayerSettings.productName = "Astro Flow Jam";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.sepen.astroflowjam");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
