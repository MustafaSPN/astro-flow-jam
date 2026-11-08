using System.Collections.Generic;
using AstroFlowJam.Core;
using NUnit.Framework;
using UnityEditor;

namespace AstroFlowJam.Tests
{
    /// <summary>Projedeki her bölüm dosyası kurulabilir ve jam tahtası tamamen temizlenebilir olmalı.</summary>
    public class LevelValidationTests
    {
        static IEnumerable<TestCaseData> AllLevels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(LevelData)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                yield return new TestCaseData(path).SetName("Bölüm: " + path);
            }
        }

        [TestCaseSource(nameof(AllLevels))]
        public void Level_IsValidAndBoardClearable(string path)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            Assert.DoesNotThrow(() => new BoardModel(level.boardWidth, level.boardHeight, level.rockets, level.exits, level.holes, level.asteroids),
                "Roket çakışması, deliğe/asteroide ya da tahta dışına taşan hücre var.");
            Assert.IsTrue(BoardSolver.CanClearBoard(level.boardWidth, level.boardHeight, level.rockets, level.exits, holes: level.holes, asteroids: level.asteroids),
                "Tahtadaki tüm roketler çıkarılamıyor.");

            // Her kilitli bloğun aynı renkte ve aynı grupta anahtarlı bir bloğu olmalı (oyuncu anahtarı blok renginden tanır).
            foreach (var r in level.rockets)
            {
                if (r.lockGroup <= 0) continue;
                var hasKey = level.rockets.Exists(k => k.keyGroup == r.lockGroup && k.color == r.color);
                Assert.IsTrue(hasKey, $"{r.id}: kilitli ({r.color}, grup {r.lockGroup}) ama aynı renkte anahtarı yok.");
            }

            if (!level.HasBeads) return;
            // GDD: her rengin boncuk sayısı o renkteki roket kapasitelerinin toplamına eşit olmalı.
            var counts = new BeadGrid(level.beadColumns, level.beadRows, level.beadLayout).InitialCounts;
            foreach (GameColor color in System.Enum.GetValues(typeof(GameColor)))
            {
                counts.TryGetValue(color, out var beads);
                var capacity = 0;
                foreach (var r in level.rockets)
                {
                    if (r.isBlocker) continue;
                    if (r.color == color) capacity += r.capacity;
                    if (r.layered && r.innerColor == color) capacity += r.innerCapacity;   // katmanlı bloğun iç katmanı
                }
                Assert.AreEqual(beads, capacity, $"{color}: boncuk {beads}, roket kapasitesi {capacity}");
            }
            Assert.IsFalse(string.IsNullOrEmpty(level.pictureName), "Gizli resim atanmamış.");
        }

        [Test]
        public void Levels_Exist()
        {
            Assert.IsNotEmpty(AssetDatabase.FindAssets("t:" + nameof(LevelData)));
        }
    }
}
