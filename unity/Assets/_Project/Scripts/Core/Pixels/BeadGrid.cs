using System;
using System.Collections.Generic;

namespace AstroFlowJam.Core
{
    public enum BeadState { Available, Reserved, Cleared }

    /// <summary>Yörüngenin hangi düz kenarından bakıldığı.</summary>
    public enum GridSide { Top, Bottom, Left, Right }

    /// <summary>
    /// Gizli resmin üzerindeki boncuk ızgarası. Satır 0 en üsttedir. Boncuklar temizlendikten sonra kaymaz.
    /// "Dıştan erişilebilir": bakılan kenardan, arada temizlenmemiş başka boncuk olmayan ilk hücre.
    /// Rezerve boncuk hedefe ulaşana kadar arkasındakileri engellemeyi sürdürür.
    /// </summary>
    public sealed class BeadGrid
    {
        public int Columns { get; }
        public int Rows { get; }
        public int Total { get; }
        public int Remaining { get; private set; }

        /// <summary>Henüz hedeflenmemiş (rezerve edilmemiş) boncuk sayısı; 0 ise kalanların hepsine mermi yoldadır.</summary>
        public int Unreserved => Remaining - reserved;

        int reserved;

        readonly GameColor[] colors;
        readonly BeadState[] states;
        readonly Dictionary<GameColor, int> initialCounts = new();

        /// <param name="layout">Üst satırdan başlayarak satır satır renk harfleri: R, B, G, Y; '.' boş hücre.</param>
        public BeadGrid(int columns, int rows, string layout)
        {
            if (layout == null || layout.Length != columns * rows)
                throw new ArgumentException($"Boncuk düzeni {columns}×{rows} = {columns * rows} karakter olmalı.");
            Columns = columns;
            Rows = rows;
            colors = new GameColor[layout.Length];
            states = new BeadState[layout.Length];
            for (var i = 0; i < layout.Length; i++)
            {
                if (layout[i] == '.')
                {
                    states[i] = BeadState.Cleared;
                    continue;
                }
                if (!TryParseColor(layout[i], out colors[i]))
                    throw new ArgumentException($"Bilinmeyen boncuk rengi '{layout[i]}' ({i % columns}, {i / columns}).");
                Total++;
                initialCounts.TryGetValue(colors[i], out var n);
                initialCounts[colors[i]] = n + 1;
            }
            Remaining = Total;
        }

        public static bool TryParseColor(char c, out GameColor color)
        {
            // Piksel paleti: rakamlar ve küçük harfler (0–9, a–x); eski prototip renkleri büyük R, B, G, Y.
            var pf = GameColors.PfCodes.IndexOf(c);
            if (pf >= 0)
            {
                color = GameColors.FromPf(pf);
                return true;
            }
            switch (c)
            {
                case 'R': color = GameColor.Red; return true;
                case 'B': color = GameColor.Blue; return true;
                case 'G': color = GameColor.Green; return true;
                case 'Y': color = GameColor.Yellow; return true;
                default: color = default; return false;
            }
        }

        public static char ToChar(GameColor color) => GameColors.IsPaletteColor(color) ? GameColors.PfCodes[GameColors.PfIndex(color)] : color switch
        {
            GameColor.Red => 'R',
            GameColor.Blue => 'B',
            GameColor.Green => 'G',
            _ => 'Y',
        };

        public int Index(int column, int row) => row * Columns + column;
        public int ColumnOf(int index) => index % Columns;
        public int RowOf(int index) => index / Columns;
        public GameColor ColorAt(int index) => colors[index];
        public BeadState StateAt(int index) => states[index];

        /// <summary>Başlangıçtaki renk sayıları (boş hücreler hariç).</summary>
        public IReadOnlyDictionary<GameColor, int> InitialCounts => initialCounts;

        /// <summary>Bu kenardan bu satır/sütuna bakıldığında görüş hattındaki ilk temizlenmemiş hücre; yoksa -1.</summary>
        /// <param name="line">Top/Bottom için sütun, Left/Right için satır (0 = üst).</param>
        public int FirstInLine(GridSide side, int line)
        {
            var vertical = side == GridSide.Top || side == GridSide.Bottom;
            var count = vertical ? Rows : Columns;
            if (line < 0 || line >= (vertical ? Columns : Rows)) return -1;
            for (var k = 0; k < count; k++)
            {
                var index = side switch
                {
                    GridSide.Top => Index(line, k),
                    GridSide.Bottom => Index(line, Rows - 1 - k),
                    GridSide.Left => Index(k, line),
                    _ => Index(Columns - 1 - k, line),
                };
                if (states[index] != BeadState.Cleared) return index;
            }
            return -1;
        }

        public bool TryReserve(int index)
        {
            if (states[index] != BeadState.Available) return false;
            states[index] = BeadState.Reserved;
            reserved++;
            return true;
        }

        public void Clear(int index)
        {
            if (states[index] != BeadState.Reserved)
                throw new InvalidOperationException("Yalnızca rezerve boncuk temizlenebilir.");
            states[index] = BeadState.Cleared;
            reserved--;
            Remaining--;
        }
    }
}
