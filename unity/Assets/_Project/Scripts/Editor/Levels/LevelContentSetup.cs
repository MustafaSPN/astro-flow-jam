using System.Collections.Generic;
using System.IO;
using System.Text;
using AstroFlowJam.Core;
using UnityEditor;
using UnityEngine;

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// Bölüm içeriğini görsellerden kurar: boncuk PNG'sindeki her piksel bir boncuktur ve en yakın oyun rengine eşlenir;
    /// roket kapasiteleri boncuk sayılarından CapacityPlanner ile hesaplanır. Proje açıldığında veya kod derlendiğinde
    /// eski içerik sürümündeki bölümleri kendiliğinden günceller; elle "Astro Flow Jam/Bölüm İçeriğini Yenile" ile de çalışır.
    /// </summary>
    [InitializeOnLoad]
    static class LevelContentSetup
    {
        const int ContentVersion = 5;
        const string ArtDir = "Assets/_Project/Art/Levels";
        const string LevelsDir = "Assets/_Project/Data/Levels";
        const string PictureMaterialPath = "Assets/_Project/Materials/HiddenImage.mat";

        static readonly (GameColor color, Color32 rgb)[] Palette =
        {
            (GameColor.Red, new Color32(0xE5, 0x48, 0x4D, 255)),
            (GameColor.Blue, new Color32(0x3E, 0x7B, 0xFA, 255)),
            (GameColor.Green, new Color32(0x3C, 0xC4, 0x6B, 255)),
            (GameColor.Yellow, new Color32(0xF5, 0xC8, 0x42, 255)),
        };

        static LevelContentSetup() => EditorApplication.delayCall += () => Run(force: false);

        [MenuItem("Astro Flow Jam/Bölüm İçeriğini Yenile")]
        static void RunFromMenu() => Run(force: true);

        /// <summary>Komut satırından: -executeMethod AstroFlowJam.Editor.LevelContentSetup.Apply</summary>
        public static void Apply() => Run(force: false);

        static void Run(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsurePictureMaterial();
            SetupLevel01(force);
            AssetDatabase.SaveAssets();
        }

        static void EnsurePictureMaterial()
        {
            var palette = AssetDatabase.LoadAssetAtPath<PrototypePalette>("Assets/_Project/Data/PrototypePalette.asset");
            if (palette == null || palette.pictureUnlit != null) return;

            var material = AssetDatabase.LoadAssetAtPath<Material>(PictureMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(material, PictureMaterialPath);
            }
            palette.pictureUnlit = material;
            EditorUtility.SetDirty(palette);
        }

        static void SetupLevel01(bool force)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>($"{LevelsDir}/L01.asset");
            if (level == null || (!force && level.contentVersion >= ContentVersion)) return;

            var layout = ReadBeadLayout($"{ArtDir}/L01_Beads.png", out var columns, out var rows);
            if (layout == null) return;

            level.displayName = "Bölüm 1 – Uzay Kedisi";
            level.pictureName = "L01_Hidden";
            level.beadColumns = columns;
            level.beadRows = rows;
            level.beadLayout = layout;
            level.boardWidth = 6;
            level.boardHeight = 6;
            level.timeLimit = 180f;
            level.orbitCapacity = 5;
            level.slotCount = 5;
            level.introducesMechanic = "drag-and-orbit";

            // Yoğun tahta: 12 roket ve 7 sürüklenebilir engel, 36 hücrenin 28'i dolu. Engeller hiçbir kapıdan çıkmaz;
            // roketlerin yolunu açmak için kenara çekilmeleri gerekir. Kapasiteler küçük, roket sayısı fazla.
            var rockets = new List<RocketSpec>
            {
                new("Y1", GameColor.Yellow, new Vector2Int(0, 5), new Vector2Int(2, 1), 0),
                new("Y2", GameColor.Yellow, new Vector2Int(1, 0), new Vector2Int(2, 1), 0),
                new("y1", GameColor.Yellow, new Vector2Int(0, 4), new Vector2Int(1, 1), 0),
                new("y2", GameColor.Yellow, new Vector2Int(1, 2), new Vector2Int(1, 1), 0),
                new("G1", GameColor.Green, new Vector2Int(0, 2), new Vector2Int(1, 2), 0),
                new("G2", GameColor.Green, new Vector2Int(2, 4), new Vector2Int(2, 1), 0),
                new("g1", GameColor.Green, new Vector2Int(3, 1), new Vector2Int(1, 1), 0),
                new("B1", GameColor.Blue, new Vector2Int(1, 1), new Vector2Int(2, 1), 0),
                new("b1", GameColor.Blue, new Vector2Int(3, 5), new Vector2Int(1, 1), 0),
                new("b2", GameColor.Blue, new Vector2Int(4, 3), new Vector2Int(1, 1), 0),
                new("R1", GameColor.Red, new Vector2Int(1, 3), new Vector2Int(2, 1), 0),
                new("r1", GameColor.Red, new Vector2Int(5, 4), new Vector2Int(1, 1), 0),
                RocketSpec.Blocker("X1", new Vector2Int(2, 5), new Vector2Int(1, 1)),
                RocketSpec.Blocker("X2", new Vector2Int(4, 5), new Vector2Int(1, 1)),
                RocketSpec.Blocker("X3", new Vector2Int(1, 4), new Vector2Int(1, 1)),
                RocketSpec.Blocker("X4", new Vector2Int(2, 2), new Vector2Int(2, 1)),
                RocketSpec.Blocker("X5", new Vector2Int(0, 0), new Vector2Int(1, 2)),
                RocketSpec.Blocker("X6", new Vector2Int(4, 0), new Vector2Int(2, 1)),
                RocketSpec.Blocker("X7", new Vector2Int(5, 2), new Vector2Int(1, 1)),
            };
            CapacityPlanner.Assign(rockets, new BeadGrid(columns, rows, layout).InitialCounts);
            level.rockets = rockets;
            level.exits = new List<ExitSpec>
            {
                new(GameColor.Yellow, Edge.Top, 0, 2),
                new(GameColor.Yellow, Edge.Bottom, 1, 2),
                new(GameColor.Blue, Edge.Top, 3, 2),
                new(GameColor.Green, Edge.Right, 3, 2),
                new(GameColor.Red, Edge.Left, 2, 2),
            };

            level.contentVersion = ContentVersion;
            EditorUtility.SetDirty(level);
            Debug.Log($"Bölüm 1 içeriği kuruldu: {columns}×{rows} boncuk, kapasiteler " +
                      string.Join(", ", rockets.FindAll(r => !r.isBlocker).ConvertAll(r => $"{r.id}={r.capacity}")));
        }

        /// <summary>PNG'yi ham baytlarından okur (içe aktarma ayarlarından bağımsız). Satırlar üstten başlar.</summary>
        static string ReadBeadLayout(string path, out int columns, out int rows)
        {
            columns = rows = 0;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Boncuk görseli bulunamadı: {path}");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            columns = texture.width;
            rows = texture.height;
            var pixels = texture.GetPixels32();
            Object.DestroyImmediate(texture);

            var sb = new StringBuilder(columns * rows);
            for (var row = 0; row < rows; row++)
                for (var col = 0; col < columns; col++)
                {
                    // Texture2D'de y = 0 alt satırdır; düzen üstten başlar.
                    var p = pixels[(rows - 1 - row) * columns + col];
                    sb.Append(p.a < 128 ? '.' : BeadGrid.ToChar(Nearest(p)));
                }
            return sb.ToString();
        }

        static GameColor Nearest(Color32 c)
        {
            var best = GameColor.Red;
            var bestDistance = int.MaxValue;
            foreach (var (color, rgb) in Palette)
            {
                int dr = c.r - rgb.r, dg = c.g - rgb.g, db = c.b - rgb.b;
                var d = dr * dr + dg * dg + db * db;
                if (d < bestDistance) { bestDistance = d; best = color; }
            }
            return best;
        }
    }
}
