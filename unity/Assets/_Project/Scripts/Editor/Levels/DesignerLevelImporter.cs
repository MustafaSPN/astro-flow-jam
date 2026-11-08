using System;
using System.Collections.Generic;
using System.IO;
using AstroFlowJam.Core;
using UnityEditor;
using UnityEngine;

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// Bölüm tasarlayıcının çıktısını (Tools/leveldesigner/out/levels/NNN/level.json) LevelData varlıklarına çevirir
    /// (Data/Levels/Designer/PNNN.asset) ve bölüm sırasını bu bölümlerle değiştirir.
    /// Komut satırından: -executeMethod AstroFlowJam.Editor.DesignerLevelImporter.Run
    /// </summary>
    public static class DesignerLevelImporter
    {
        const string SourceDir = "Tools/leveldesigner/out/levels";
        const string TargetDir = "Assets/_Project/Data/Levels/Designer";
        const string DatabasePath = "Assets/_Project/Data/LevelDatabase.asset";
        const string SharedPicture = "L01_Hidden";
        const string PictureDir = "Assets/_Project/Resources/LevelPictures";
        // Tasarlayıcı gizli resim çizdirmediyse: 2. bölümde astronot kedi, diğerlerinde ortak resim
        static readonly Dictionary<int, string> OwnPictures = new() { { 2, "G02_Reward_AstronautCat" } };

        [Serializable] class Cell { public int x, y; }
        // way: "" serbest, "H" yalnız yatay, "V" yalnız dikey; ice: buz sayacı; key / lock: anahtar ve kilit grubu (0 yok).
        // inner / innerAmmo: katmanlı bloğun iç katmanının rengi ve hücre başına mermisi (boşsa katmansız).
        [Serializable] class JRocket { public string color; public Cell[] cells; public Cell origin; public int[] ammo; public string way; public int ice, key, @lock; public string inner; public int[] innerAmmo; }
        [Serializable] class JExit { public string color; public string edge; public int start; public int width; }
        // Asteroit: shape Single/Double/L/T, pivot (x, y), rot 90° adım, count 0 kalıcı.
        [Serializable] class JAsteroid { public string shape; public int x, y, rot, count; }
        [Serializable]
        class JLevel
        {
            public string levelId, displayName, tier;
            public int boardWidth, boardHeight, beadColumns, beadRows, timeLimit;
            public string beadLayout, hiddenImage;
            public JRocket[] rockets;
            public JExit[] exits;
            public Cell[] holes;
            public JAsteroid[] asteroids;
        }

        [MenuItem("Astro Flow Jam/Tasarlayıcı Bölümlerini İçe Aktar")]
        public static void Run()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var source = Path.Combine(projectRoot, SourceDir);
            Directory.CreateDirectory(TargetDir);
            var levels = new List<LevelData>();
            // Sıra: önce "demo_" deneme bölümleri, sonra numaralı bölümler.
            var dirs = Directory.GetDirectories(source);
            Array.Sort(dirs, (a, b) =>
            {
                bool da = Path.GetFileName(a).StartsWith("demo_"), db = Path.GetFileName(b).StartsWith("demo_");
                return da != db ? (da ? -1 : 1) : StringComparer.Ordinal.Compare(a, b);
            });
            foreach (var dir in dirs)
            {
                var file = Path.Combine(dir, "level.json");
                if (!File.Exists(file)) continue;
                var j = JsonUtility.FromJson<JLevel>(File.ReadAllText(file));
                var number = int.TryParse(Path.GetFileName(dir), out var n) ? n : -1;
                levels.Add(Import(j, number, dir));
            }
            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            database.levels = levels;
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DesignerLevelImporter] {levels.Count} bölüm içe aktarıldı ve sıraya kondu.");
        }

        /// <summary>
        /// Tek bir deneme bölümünü (Tools/leveldesigner/out/demos/&lt;ad&gt;/level.json) içe aktarır ve sıranın başına koyar;
        /// aynı kimlikli eski kopya sıradan çıkarılır. Komut satırı: -executeMethod ...DesignerLevelImporter.RunDemo -demo demo_8x9
        /// </summary>
        public static void RunDemo()
        {
            var args = Environment.GetCommandLineArgs();
            var name = Array.IndexOf(args, "-demo") is var k && k >= 0 && k + 1 < args.Length ? args[k + 1] : "demo_8x9";
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Tools/leveldesigner/out/demos", name);
            var j = JsonUtility.FromJson<JLevel>(File.ReadAllText(Path.Combine(dir, "level.json")));
            Directory.CreateDirectory(TargetDir);
            var level = Import(j, -1, dir);
            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            database.levels.RemoveAll(l => l && l.levelId == level.levelId);
            database.levels.Insert(0, level);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DesignerLevelImporter] deneme bölümü {level.levelId} sıranın başına kondu.");
        }

        // Tasarlayıcının çizdirdiği uzay temalı gizli resim (hidden.png) varsa projeye kopyalanır.
        static string ImportPicture(JLevel j, string dir)
        {
            if (string.IsNullOrEmpty(j.hiddenImage)) return null;
            var source = Path.Combine(dir, j.hiddenImage);
            if (!File.Exists(source)) return null;
            Directory.CreateDirectory(PictureDir);
            var target = $"{PictureDir}/{j.levelId}_Hidden.png";
            File.Copy(source, target, true);
            AssetDatabase.ImportAsset(target);
            return Path.GetFileNameWithoutExtension(target);
        }

        static LevelData Import(JLevel j, int number, string dir)
        {
            var path = $"{TargetDir}/{j.levelId}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, path);
            }
            level.levelId = j.levelId;
            level.displayName = j.displayName;
            level.isHard = j.tier == "Zor" || j.tier == "Çok Zor";
            level.timeLimit = j.timeLimit;
            level.introducesMechanic = "designer";
            level.boardWidth = j.boardWidth;
            level.boardHeight = j.boardHeight;
            level.beadColumns = j.beadColumns;
            level.beadRows = j.beadRows;
            level.beadLayout = j.beadLayout;
            level.orbitCapacity = 5;
            level.slotCount = 5;
            level.pictureName = ImportPicture(j, dir) ?? (OwnPictures.TryGetValue(number, out var own) ? own : SharedPicture);

            level.rockets = new List<RocketSpec>();
            for (var i = 0; i < j.rockets.Length; i++)
            {
                var r = j.rockets[i];
                if (!BeadGrid.TryParseColor(r.color[0], out var color))
                    throw new Exception($"{j.levelId}: bilinmeyen renk '{r.color}'");
                int w = 0, h = 0, capacity = 0;
                foreach (var c in r.cells) { w = Mathf.Max(w, c.x + 1); h = Mathf.Max(h, c.y + 1); }
                foreach (var a in r.ammo) capacity += a;
                var spec = new RocketSpec($"{BeadGrid.ToChar(color)}{i + 1}", color, new Vector2Int(r.origin.x, r.origin.y), new Vector2Int(w, h), capacity);
                if (r.cells.Length != w * h)              // dikdörtgen olmayan şekil (L, T)
                {
                    spec.shape = new List<Vector2Int>();
                    foreach (var c in r.cells) spec.shape.Add(new Vector2Int(c.x, c.y));
                }
                spec.axis = r.way == "H" ? MoveAxis.Horizontal : r.way == "V" ? MoveAxis.Vertical : MoveAxis.Free;
                spec.ice = r.ice;
                spec.keyGroup = r.key;
                spec.lockGroup = r.@lock;
                if (!string.IsNullOrEmpty(r.inner))
                {
                    if (!BeadGrid.TryParseColor(r.inner[0], out var innerColor))
                        throw new Exception($"{j.levelId}: bilinmeyen iç renk '{r.inner}'");
                    spec.layered = true;
                    spec.innerColor = innerColor;
                    spec.innerCapacity = 0;
                    if (r.innerAmmo != null) foreach (var a in r.innerAmmo) spec.innerCapacity += a;
                }
                level.rockets.Add(spec);
            }
            level.holes = new List<Vector2Int>();
            if (j.holes != null) foreach (var h in j.holes) level.holes.Add(new Vector2Int(h.x, h.y));
            level.asteroids = new List<AsteroidSpec>();
            if (j.asteroids != null)
                foreach (var a in j.asteroids)
                    level.asteroids.Add(new AsteroidSpec
                    {
                        shape = (AsteroidShape)Enum.Parse(typeof(AsteroidShape), a.shape), origin = new Vector2Int(a.x, a.y),
                        rotation = a.rot, count = a.count,
                    });
            level.exits = new List<ExitSpec>();
            foreach (var e in j.exits)
            {
                BeadGrid.TryParseColor(e.color[0], out var color);
                var edge = e.edge switch { "top" => Edge.Top, "bottom" => Edge.Bottom, "left" => Edge.Left, _ => Edge.Right };
                level.exits.Add(new ExitSpec(color, edge, e.start, e.width));
            }
            EditorUtility.SetDirty(level);
            return level;
        }
    }
}
