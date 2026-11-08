using System.Collections.Generic;
using System.IO;
using AstroFlowJam.Core;
using UnityEditor;
using UnityEngine;

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// Öğretici eğriye göre G01–G06 bölümlerini üretir (LevelGenerator + LevelSimulator), her birine basit bir gizli resim
    /// çizer ve bölüm sırasını günceller. Eğri tür araştırmasından çıkarıldı:
    /// seyrek tahta, 2–4 renk, engel yok; banda giren roket sayısı ilk bölümlerde 7'den 18'e çıkar
    /// (bloklar kapıda hücrelerine bölünür), kilit açma anları yavaş yavaş artar, tuzak son bölümde gelir.
    /// Komut satırından: -executeMethod AstroFlowJam.Editor.LevelGenerationSetup.Run
    /// </summary>
    public static class LevelGenerationSetup
    {
        const string LevelsDir = "Assets/_Project/Data/Levels";
        const string ArtDir = "Assets/_Project/Resources/LevelPictures";
        const string DatabasePath = "Assets/_Project/Data/LevelDatabase.asset";
        const float PictureWidth = 6.0f;

        static List<LevelGenerator.Recipe> Recipes() => new()
        {
            new() { id = "G01", name = "Bölüm 1 – İlk Uçuş", mechanic = "drag-exit-shoot", boardWidth = 4, boardHeight = 5,
                colors = new[] { GameColor.Blue, GameColor.Yellow }, small = new[] { 2, 1 }, big = new[] { 1, 1 },
                shape = LevelGenerator.Shape.Circle, seed = 11, minUsefulAtStart = 0.5f },
            new() { id = "G02", name = "Bölüm 2 – Büyük Roket", mechanic = "big-rocket", boardWidth = 5, boardHeight = 5,
                colors = new[] { GameColor.Red, GameColor.Blue }, small = new[] { 2, 2 }, big = new[] { 1, 2 },
                shape = LevelGenerator.Shape.RoundedSquare, seed = 22, minUnlocks = 1 },
            new() { id = "G03", name = "Bölüm 3 – Üç Renk", mechanic = "three-colors", boardWidth = 5, boardHeight = 6,
                colors = new[] { GameColor.Green, GameColor.Yellow, GameColor.Blue }, small = new[] { 2, 2, 1 }, big = new[] { 1, 1, 1 },
                shape = LevelGenerator.Shape.Diamond, seed = 33, minUnlocks = 1 },
            new() { id = "G04", name = "Bölüm 4 – Yol Aç", mechanic = "unlock-behind", boardWidth = 5, boardHeight = 6,
                colors = new[] { GameColor.Yellow, GameColor.Red, GameColor.Green }, small = new[] { 2, 2, 2 }, big = new[] { 1, 2, 1 },
                shape = LevelGenerator.Shape.Circle, seed = 44, minUnlocks = 2 },
            new() { id = "G05", name = "Bölüm 5 – Dış Katman", mechanic = "outer-first", boardWidth = 6, boardHeight = 6,
                colors = new[] { GameColor.Blue, GameColor.Green, GameColor.Red }, small = new[] { 3, 2, 2 }, big = new[] { 2, 2, 1 },
                shape = LevelGenerator.Shape.Rocket, seed = 55, minUnlocks = 3 },
            new() { id = "G06", name = "Bölüm 6 – Slotu Kullan", mechanic = "slot-buffer", boardWidth = 6, boardHeight = 6,
                colors = new[] { GameColor.Red, GameColor.Yellow, GameColor.Blue, GameColor.Green }, small = new[] { 2, 2, 2, 2 }, big = new[] { 1, 1, 2, 1 },
                shape = LevelGenerator.Shape.RoundedSquare, seed = 66, minUnlocks = 3, maxForced = 1 },
        };

        [MenuItem("Astro Flow Jam/Seviyeleri Üret")]
        public static void Run()
        {
            Directory.CreateDirectory(ArtDir);
            var levels = new List<LevelData>();
            var report = new System.Text.StringBuilder();
            var palette = new[] { new Color(0.95f, 0.55f, 0.35f), new Color(0.45f, 0.75f, 1f), new Color(0.7f, 0.5f, 0.95f),
                new Color(0.4f, 0.85f, 0.6f), new Color(1f, 0.8f, 0.4f), new Color(0.9f, 0.45f, 0.6f) };

            var index = 0;
            foreach (var recipe in Recipes())
            {
                var started = System.DateTime.Now;
                var generated = LevelGenerator.Generate(recipe, PictureWidth);
                Debug.Log($"[LevelGeneration] {recipe.id} {(System.DateTime.Now - started).TotalSeconds:0} sn'de üretildi: {generated.result}");
                var texture = DrawPlanet(palette[index % palette.Length], recipe.seed);
                var texturePath = $"{ArtDir}/{recipe.id}_Hidden.png";
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);

                var path = $"{LevelsDir}/{recipe.id}.asset";
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelData>();
                    AssetDatabase.CreateAsset(level, path);
                }
                level.levelId = recipe.id;
                level.displayName = recipe.name;
                level.introducesMechanic = recipe.mechanic;
                level.isHard = recipe.maxForced > 0;
                level.boardWidth = recipe.boardWidth;
                level.boardHeight = recipe.boardHeight;
                level.rockets = generated.rockets;
                level.exits = generated.exits;
                // Elle atanmış gizli resim (ör. 2. bölümün kedisi) korunur; yalnızca üretilmiş resim yenilenir.
                var generatedPicture = Path.GetFileNameWithoutExtension(texturePath);
                if (string.IsNullOrEmpty(level.pictureName) || level.pictureName == generatedPicture)
                    level.pictureName = generatedPicture;
                level.beadColumns = generated.beadColumns;
                level.beadRows = generated.beadRows;
                level.beadLayout = generated.beadLayout;
                level.orbitCapacity = 5;
                level.slotCount = 5;
                EditorUtility.SetDirty(level);
                levels.Add(level);

                report.AppendLine($"{recipe.id} {recipe.boardWidth}×{recipe.boardHeight}, {generated.rockets.Count} roket, " +
                                  $"{generated.beadColumns}×{generated.beadRows} ızgara: {generated.result}");
                index++;
            }

            // Eski yoğun L01, eğrinin sonunda zor bir örnek olarak kalır.
            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            var old = AssetDatabase.LoadAssetAtPath<LevelData>($"{LevelsDir}/L01.asset");
            if (old != null) levels.Add(old);
            levels.AddRange(PixelArtLevelSetup.Existing());   // hazır piksel tasarımlı bölümler sırada kalır
            database.levels = levels;
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log("[LevelGeneration]\n" + report);
        }

        // Basit bir gizli resim: koyu uzay, yıldızlar, gölgeli gezegen ve eğik halka.
        static Texture2D DrawPlanet(Color planet, int seed)
        {
            const int size = 256;
            var rng = new System.Random(seed);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var t = y / (float)size;
                    pixels[y * size + x] = Color.Lerp(new Color(0.04f, 0.05f, 0.12f), new Color(0.12f, 0.08f, 0.22f), t);
                }
            for (var i = 0; i < 140; i++)
            {
                var p = rng.Next(size * size);
                pixels[p] = Color.Lerp(pixels[p], Color.white, 0.5f + (float)rng.NextDouble() * 0.5f);
            }
            var center = new Vector2(size * 0.5f, size * 0.52f);
            var radius = size * 0.28f;
            var light = new Vector2(-0.5f, 0.6f).normalized;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = (new Vector2(x, y) - center) / radius;
                    // Halka: eğik elips bandı; gezegenin arkasında kalan kısmı çizilmez.
                    var ring = new Vector2(d.x * 0.62f, (d.y + d.x * 0.25f) * 2.6f).magnitude;
                    var inPlanet = d.sqrMagnitude <= 1f;
                    var ringFront = ring > 1.05f && ring < 1.35f && (!inPlanet || d.y + d.x * 0.25f < 0f);
                    if (inPlanet)
                    {
                        var z = Mathf.Sqrt(1f - d.sqrMagnitude);
                        var shade = Mathf.Clamp01(Vector3.Dot(new Vector3(d.x, d.y, z), new Vector3(light.x, light.y, 0.6f).normalized));
                        var band = 0.85f + 0.15f * Mathf.Sin(d.y * 9f + seed);
                        pixels[y * size + x] = planet * (0.25f + 0.85f * shade) * band;
                    }
                    if (ringFront) pixels[y * size + x] = Color.Lerp(pixels[y * size + x], new Color(0.95f, 0.9f, 0.8f), 0.75f);
                    pixels[y * size + x].a = 1f;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
