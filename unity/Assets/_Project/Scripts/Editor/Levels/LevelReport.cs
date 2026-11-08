using AstroFlowJam.Core;
using UnityEditor;
using UnityEngine;

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// Sıradaki tüm bölümleri <see cref="LevelSimulator"/> ile oynatır ve sonuçları yazar (çözüldü mü, süre, kilit açma,
    /// zorunlu hamle, en çok slot). Komut satırından: -executeMethod AstroFlowJam.Editor.LevelReport.Run
    /// </summary>
    public static class LevelReport
    {
        [MenuItem("Astro Flow Jam/Bölümleri Simüle Et")]
        public static void Run()
        {
            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/_Project/Data/LevelDatabase.asset");
            var report = new System.Text.StringBuilder();
            for (var i = 0; i < database.levels.Count; i++)
            {
                var l = database.levels[i];
                var result = LevelSimulator.Run(new LevelSimulator.Input
                {
                    boardWidth = l.boardWidth, boardHeight = l.boardHeight, rockets = l.rockets, exits = l.exits,
                    beadColumns = l.beadColumns, beadRows = l.beadRows, beadLayout = l.beadLayout,
                    pictureWidth = 6.0f, beltCapacity = l.orbitCapacity, slotCount = l.slotCount,
                });
                report.AppendLine($"{i + 1}. {l.levelId} ({l.beadColumns}×{l.beadRows}, {l.rockets.Count} blok): {result}");
            }
            Debug.Log("[LevelReport]\n" + report);
        }
    }
}
