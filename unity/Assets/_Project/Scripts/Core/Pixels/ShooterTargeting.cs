using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam.Core
{
    /// <summary>
    /// Roketin hedef seçimi:
    /// 1. Atıcının kenarı, ızgara merkezine göre baskın eksenden bulunur.
    /// 2. Merkezi atıcıya en fazla 'tolerans' uzaklıkta olan tüm sütunlar/satırlar aday olur.
    /// 3. Her hatta o kenardan bakınca ilk dolu hücre alınır; temizlenmemiş her boncuk doludur.
    /// 4. Aday, rezerve edilmemişse ve rengi roketle aynıysa geçerlidir.
    /// 5. En küçük "uzaklık² + 0,01 · hat sapması" skoru kazanır.
    /// Koordinatlar hücre birimindedir: x soldan sağa sütun, y üstten aşağı satır; (0,0) ızgaranın sol üst köşesi.
    /// </summary>
    public static class ShooterTargeting
    {
        const float LineOffsetWeight = 0.01f;

        /// <summary>Atıcının baktığı kenar: dikey sapma yatayı aşıyorsa üst/alt, aksi halde sol/sağ.</summary>
        public static GridSide SideOf(BeadGrid grid, Vector2 shooter)
        {
            var dx = shooter.x - grid.Columns / 2f;
            var dy = shooter.y - grid.Rows / 2f;
            if (Mathf.Abs(dy) > Mathf.Abs(dx)) return dy < 0f ? GridSide.Top : GridSide.Bottom;
            return dx < 0f ? GridSide.Left : GridSide.Right;
        }

        /// <summary>Atıcı için en iyi boncuk; yoksa -1.</summary>
        /// <param name="shooter">Atıcının ızgara uzayındaki konumu (hücre birimi).</param>
        /// <param name="tolerance">Hat merkezinin atıcıya en fazla uzaklığı (hücre birimi).</param>
        /// <param name="pass">
        /// Verilirse bu geçişte zaten ateş edilmiş hatlar atlanır ve seçilen hat işaretlenir: roket bir kenardan geçerken
        /// her hatta yalnızca öndeki boncuğu vurur, arkasındakini aynı geçişte vuramaz.
        /// </param>
        public static int Pick(BeadGrid grid, Vector2 shooter, GameColor color, float tolerance, PassLines pass = null)
        {
            var side = SideOf(grid, shooter);
            var vertical = side == GridSide.Top || side == GridSide.Bottom;
            var along = vertical ? shooter.x : shooter.y;
            var lineCount = vertical ? grid.Columns : grid.Rows;

            var used = pass?.On(side);
            var best = -1;
            var bestLine = -1;
            var bestScore = float.MaxValue;
            foreach (var line in AlignedLines(along, lineCount, tolerance))
            {
                if (used != null && used.Contains(line)) continue;
                var bead = grid.FirstInLine(side, line);
                if (bead < 0) continue;
                if (grid.StateAt(bead) != BeadState.Available || grid.ColorAt(bead) != color) continue;

                var cell = new Vector2(grid.ColumnOf(bead) + 0.5f, grid.RowOf(bead) + 0.5f);
                var score = (cell - shooter).sqrMagnitude + Mathf.Abs(line + 0.5f - along) * LineOffsetWeight;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = bead;
                    bestLine = line;
                }
            }
            if (best >= 0) used?.Add(bestLine);
            return best;
        }

        /// <summary>Merkezi 'along' noktasına tolerans içinde kalan hatlar, yakınlığa göre sıralı.</summary>
        public static List<int> AlignedLines(float along, int lineCount, float tolerance)
        {
            var lines = new List<int>();
            for (var i = 0; i < lineCount; i++)
                if (Mathf.Abs(i + 0.5f - along) <= tolerance) lines.Add(i);
            lines.Sort((a, b) => Math.Abs(a + 0.5f - along).CompareTo(Math.Abs(b + 0.5f - along)));
            return lines;
        }

        /// <summary>
        /// Bu renkteki atıcı yörüngenin herhangi bir yerinden bir boncuğu vurabilir mi? Yörünge resmin dört kenarını tam
        /// çevrelediği için her kenardaki her hat bir noktadan hizalanır; bu yüzden örnek noktalar yerine tüm hatlara bakılır
        /// (seyrek örnekler büyük ızgaralarda hat atlayıp yanlış "vuramaz" kararı verebiliyordu).
        /// </summary>
        public static bool CanHitFromOrbit(BeadGrid grid, GameColor color)
        {
            foreach (GridSide side in Enum.GetValues(typeof(GridSide)))
            {
                var lineCount = side == GridSide.Top || side == GridSide.Bottom ? grid.Columns : grid.Rows;
                for (var line = 0; line < lineCount; line++)
                {
                    var bead = grid.FirstInLine(side, line);
                    if (bead >= 0 && grid.StateAt(bead) == BeadState.Available && grid.ColorAt(bead) == color) return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Bizim sapmamız: bir roketin bu geçişte ateş ettiği hatlar. Kenar değişince ya da yeni tur başlayınca sıfırlanır.
    /// Mermi çok hızlı çarptığı için roket aynı hizadayken arkadaki boncuk öne geçip ikinci kez vurulabiliyordu.
    /// </summary>
    public sealed class PassLines
    {
        readonly HashSet<int> lines = new();
        GridSide side;
        bool started;

        /// <summary>Bu kenarda ateş edilmiş hatlar; kenar değiştiyse liste boşalır.</summary>
        public HashSet<int> On(GridSide current)
        {
            if (!started || current != side) lines.Clear();
            side = current;
            started = true;
            return lines;
        }

        /// <summary>Yeni geçiş (tur başı): tüm hatlar yeniden vurulabilir.</summary>
        public void Reset()
        {
            lines.Clear();
            started = false;
        }
    }
}
