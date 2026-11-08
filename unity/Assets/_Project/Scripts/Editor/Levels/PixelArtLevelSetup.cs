using System.Collections.Generic;
using System.IO;
using AstroFlowJam.Core;
using UnityEditor;
using UnityEngine;

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// Hazır piksel tasarımından bölüm üretir (ör. yapay zekaya çizdirilip ızgaraya oturtulmuş piksel kedi). Düzen dosyası
    /// iki satırdır: ızgara boyu ve satır satır renk harfleri (R, B, G, Y; '.' boş). Roket hücreleri renk başına boncuk
    /// sayısından hesaplanır (hücre başına ~20 mermi), tahta ve kapılar <see cref="LevelGenerator"/> ile üretilir,
    /// <see cref="LevelSimulator"/> ile çözülebilirlik sınanır ve bölüm sıraya eklenir.
    /// Komut satırından: -executeMethod AstroFlowJam.Editor.PixelArtLevelSetup.Run
    /// </summary>
    public static class PixelArtLevelSetup
    {
        const string LevelsDir = "Assets/_Project/Data/Levels";
        const string DatabasePath = "Assets/_Project/Data/LevelDatabase.asset";
        const float PictureWidth = 6.0f;
        const int AmmoPerCell = 30;           // 625 boncukta 20 ile 31 roket hücresi tahtaya sığmıyor, slotlar tıkanıyordu
        const float BigCellShare = 0.4f;         // roket hücrelerinin yaklaşık bu kadarı iki hücreli bloklarda

        sealed class Entry
        {
            public string id, name, layoutFile, hiddenImage;
            public int index, boardWidth, boardHeight, seed, minUnlocks, maxForced;
            /// <summary>Dıştan içe (önce açılan renk önce): tahtada kapıya yakın roketler bu sırayla dizilir.</summary>
            public GameColor[] colors;
        }

        static readonly Entry[] Entries =
        {
            new()
            {
                id = "L08", name = "Bölüm 8 – Piksel Kedi", layoutFile = "L08_Cat.txt", index = 7,
                hiddenImage = "L01_Hidden",
                boardWidth = 7, boardHeight = 7, seed = 88, minUnlocks = 3, maxForced = 1,
                colors = new[] { GameColor.Blue, GameColor.Yellow, GameColor.Red, GameColor.Green },
            },
        };

        [MenuItem("Astro Flow Jam/Piksel Bölümlerini Üret")]
        public static void Run()
        {
            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            foreach (var entry in Entries)
            {
                var lines = File.ReadAllLines($"{LevelsDir}/{entry.layoutFile}");
                var side = int.Parse(lines[0].Trim());
                var layout = lines[1].Trim();
                var counts = LevelGenerator.BeadCounts(layout);

                var small = new int[entry.colors.Length];
                var big = new int[entry.colors.Length];
                for (var i = 0; i < entry.colors.Length; i++)
                {
                    counts.TryGetValue(entry.colors[i], out var beads);
                    var cells = Mathf.Max(1, Mathf.RoundToInt(beads / (float)AmmoPerCell));
                    big[i] = Mathf.RoundToInt(cells * BigCellShare / 2f);
                    small[i] = cells - big[i] * 2;
                }

                var recipe = new LevelGenerator.Recipe
                {
                    id = entry.id, name = entry.name, mechanic = "pixel-art", boardWidth = entry.boardWidth,
                    boardHeight = entry.boardHeight, colors = entry.colors, small = small, big = big, seed = entry.seed,
                    minUnlocks = entry.minUnlocks, maxForced = entry.maxForced,
                    fixedLayout = layout, fixedColumns = side, fixedRows = side, candidates = 12,
                };
                var started = System.DateTime.Now;
                var generated = LevelGenerator.Generate(recipe, PictureWidth);
                Debug.Log($"[PixelArtLevel] {entry.id} {(System.DateTime.Now - started).TotalSeconds:0} sn: {generated.rockets.Count} blok, " +
                          $"{side}×{side} ızgara, boncuklar {string.Join(", ", counts)}: {generated.result}");

                var path = $"{LevelsDir}/{entry.id}.asset";
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelData>();
                    AssetDatabase.CreateAsset(level, path);
                }
                level.levelId = entry.id;
                level.displayName = entry.name;
                level.introducesMechanic = recipe.mechanic;
                level.isHard = entry.maxForced > 0;
                level.boardWidth = entry.boardWidth;
                level.boardHeight = entry.boardHeight;
                level.rockets = generated.rockets;
                level.exits = generated.exits;
                level.pictureName = entry.hiddenImage;
                level.beadColumns = side;
                level.beadRows = side;
                level.beadLayout = layout;
                level.orbitCapacity = 5;
                level.slotCount = 5;
                EditorUtility.SetDirty(level);

                database.levels.Remove(level);
                database.levels.Insert(Mathf.Min(entry.index, database.levels.Count), level);
            }
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Var olan piksel bölümleri (sıralı): seviyeler yeniden üretilince sıradan düşmesinler.</summary>
        public static IEnumerable<LevelData> Existing()
        {
            foreach (var entry in Entries)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelData>($"{LevelsDir}/{entry.id}.asset");
                if (level != null) yield return level;
            }
        }
    }
}
