using System;
using UnityEngine;

namespace AstroFlowJam.Core
{
    /// <summary>
    /// Renkler: ilk dördü eski prototip renkleri (bölüm düzeninde R, B, G, Y); ardından 34 renklik piksel paleti
    /// (Pf00–Pf33, düzende 0–9 ve a–x; tasarlayıcı siyahı Pf02 kullanmaz). Değerler <see cref="GameColors"/>'ta.
    /// </summary>
    public enum GameColor
    {
        Red, Blue, Green, Yellow,
        Pf00, Pf01, Pf02, Pf03, Pf04, Pf05, Pf06, Pf07, Pf08, Pf09, Pf10, Pf11, Pf12, Pf13, Pf14, Pf15, Pf16,
        Pf17, Pf18, Pf19, Pf20, Pf21, Pf22, Pf23, Pf24, Pf25, Pf26, Pf27, Pf28, Pf29, Pf30, Pf31, Pf32, Pf33,
    }

    /// <summary>Piksel paletinin renk değerleri ve düzen kodları.</summary>
    public static class GameColors
    {
        public const string PfCodes = "0123456789abcdefghijklmnopqrstuvwx";

        static readonly string[] PfHex =
        {
            "0073FF", "0E39CC", "2F2F2F", "3FAC1D", "FF6200", "C04983", "7829E1", "A00D0D", "2FAACF", "D2A626",
            "CCCCCC", "613E23", "046000", "1D6553", "BF83E2", "959D67", "C37B7C", "878EC0", "8D404D", "C4AA70",
            "B2544A", "888795", "474652", "39205D", "AA1D62", "CF8AB2", "D2914F", "6F0F0F", "7B730A", "5C8782",
            "9F6A44", "7B4827", "9328A7", "666476",
        };

        public static bool IsPaletteColor(GameColor c) => c >= GameColor.Pf00;
        public static int PfIndex(GameColor c) => c - GameColor.Pf00;
        public static GameColor FromPf(int index) => GameColor.Pf00 + index;

        /// <summary>Palet renginin değeri (sRGB).</summary>
        public static Color PfColor(GameColor c)
        {
            var hex = PfHex[PfIndex(c)];
            return new Color(System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f,
                             System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f,
                             System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f);
        }
    }

    public enum Dir { Up, Down, Left, Right }

    /// <summary>Tek eksenli blok: yalnız yatay ya da yalnız dikey kayar ve o yönden çıkar.</summary>
    public enum MoveAxis { Free, Horizontal, Vertical }

    public enum Edge { Top, Bottom, Left, Right }

    /// <summary>Asteroit şekilleri (Blender: Asteroid_Obstacles, pivot (0,0) hücresi).</summary>
    public enum AsteroidShape { Single, Double, L, T }

    /// <summary>
    /// Asteroit engeli: tahtada hiç oynamayan, hücrelerini dolduran kaya. count 0 ise kalıcıdır; değilse tahtadan count kadar
    /// blok çıkınca kırılır ve hücreleri boşalır.
    /// </summary>
    [Serializable]
    public struct AsteroidSpec
    {
        public AsteroidShape shape;
        /// <summary>Pivot hücresi (modelin (0,0) hücresi).</summary>
        public Vector2Int origin;
        /// <summary>Saat yönünün tersine 90° adım (0–3).</summary>
        public int rotation;
        public int count;

        static readonly Vector2Int[][] ShapeCells =
        {
            new[] { new Vector2Int(0, 0) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 0) },
            new[] { new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, -1) },
        };

        /// <summary>Pivota göre dönmüş hücreler.</summary>
        public static Vector2Int Rotate(Vector2Int c, int rotation)
        {
            for (var k = 0; k < ((rotation % 4) + 4) % 4; k++) c = new Vector2Int(-c.y, c.x);
            return c;
        }

        /// <summary>Tahtada kapladığı hücreler.</summary>
        public System.Collections.Generic.IEnumerable<Vector2Int> Cells
        {
            get { foreach (var c in ShapeCells[(int)shape]) yield return origin + Rotate(c, rotation); }
        }
    }

    /// <summary>Bölüm verisindeki bir roketin tanımı. Koordinatlar sol alttan başlar (x sağa, y yukarı).</summary>
    [Serializable]
    public struct RocketSpec
    {
        public string id;
        public GameColor color;
        /// <summary>Roketin kapladığı en sol alt hücre.</summary>
        public Vector2Int origin;
        /// <summary>Kapladığı dikdörtgenin genişliği × yüksekliği.</summary>
        public Vector2Int size;
        /// <summary>
        /// Dikdörtgen olmayan şekiller (L, T, Z...) için dolu hücreler, origin'e göre (0..size-1). Boşsa dikdörtgenin
        /// tüm hücreleri doludur. Bloklar dönmez.
        /// </summary>
        public System.Collections.Generic.List<Vector2Int> shape;
        public int capacity;
        /// <summary>Engel: sürüklenebilir ama hiçbir kapıdan çıkamaz, yörüngeye gitmez; yalnızca yer kaplar.</summary>
        public bool isBlocker;
        /// <summary>Tek eksenli blok: yalnız bu eksende kayar ve yalnız bu eksendeki kenarlardan çıkar.</summary>
        public MoveAxis axis;
        /// <summary>Buzlu blok: tahtadan bu kadar blok çıkana kadar donuktur (oynamaz); her çıkışta sayaç bir azalır.</summary>
        public int ice;
        /// <summary>Anahtar: bu blok kapıdan çıkınca kilit grubu aynı olan bloklar açılır (0: anahtar yok).</summary>
        public int keyGroup;
        /// <summary>Kilitli blok: aynı gruptaki anahtarlı blok çıkana kadar oynamaz (0: kilit yok).</summary>
        public int lockGroup;
        /// <summary>
        /// Katmanlı blok: dış katman 'color' ve 'capacity', iç katman 'innerColor' ve 'innerCapacity'.
        /// Dış rengin kapısından geçince dış katman roketlere dönüşür, blok aynı yerde iç renkte normal blok olarak kalır.
        /// </summary>
        public bool layered;
        public GameColor innerColor;
        public int innerCapacity;

        /// <summary>Hücre sayısı. Blok kapıdan geçince her hücre ayrı bir rokete bölünür (birlikte
        /// çıkar); bantta hücre başına bir birim kaplar.</summary>
        public int Cells => shape is { Count: > 0 } ? shape.Count : size.x * size.y;

        /// <summary>Dolu hücreler, origin'e göre.</summary>
        public System.Collections.Generic.IEnumerable<Vector2Int> LocalCells
        {
            get
            {
                if (shape is { Count: > 0 })
                {
                    foreach (var c in shape) yield return c;
                    yield break;
                }
                for (var x = 0; x < size.x; x++)
                    for (var y = 0; y < size.y; y++)
                        yield return new Vector2Int(x, y);
            }
        }

        /// <summary>Verilen hücre listesinden şekilli blok; boyut hücrelerin kapladığı dikdörtgendir.</summary>
        public static RocketSpec Shaped(string id, GameColor color, Vector2Int origin, int capacity, params Vector2Int[] cells)
        {
            var max = Vector2Int.zero;
            foreach (var c in cells) max = Vector2Int.Max(max, c);
            var spec = new RocketSpec(id, color, origin, max + Vector2Int.one, capacity);
            spec.shape = new System.Collections.Generic.List<Vector2Int>(cells);
            return spec;
        }

        /// <summary>Bantta kapladığı birim = bölündüğü roket sayısı.</summary>
        public int OrbitUnits => Cells;

        /// <summary>Bölünen her parçanın mermisi.</summary>
        public int CapacityPerCell => capacity / Cells;

        /// <summary>i'inci parçanın mermisi; bölünmeyen artık ilk parçalara birer birer dağıtılır.</summary>
        public int PieceCapacity(int index) => capacity / Cells + (index < capacity % Cells ? 1 : 0);

        /// <summary>Dış katman: kapıdan geçen kabuk (katmansız, dış renk ve mermi).</summary>
        public RocketSpec OuterShell()
        {
            var shell = this;
            shell.layered = false;
            shell.keyGroup = 0;
            return shell;
        }

        /// <summary>Dış katman geçtikten sonra tahtada kalan blok: iç renk ve mermi, katmansız.</summary>
        public RocketSpec AfterOuterLayer()
        {
            var inner = this;
            inner.layered = false;
            inner.color = innerColor;
            inner.capacity = innerCapacity;
            return inner;
        }

        /// <summary>Kapıdan geçince oluşan tek hücrelik parça.</summary>
        public RocketSpec Piece(int index) => new($"{id}.{index}", color, origin, Vector2Int.one, PieceCapacity(index));

        public RocketSpec(string id, GameColor color, Vector2Int origin, Vector2Int size, int capacity)
        {
            this.id = id;
            this.color = color;
            this.origin = origin;
            this.size = size;
            this.capacity = capacity;
            isBlocker = false;
            shape = null;
            axis = MoveAxis.Free;
            ice = 0;
            keyGroup = 0;
            lockGroup = 0;
            layered = false;
            innerColor = default;
            innerCapacity = 0;
        }

        /// <summary>Blok bu yönde hareket edebilir mi (tek eksen kısıtı)?</summary>
        public bool Allows(Dir dir) => axis == MoveAxis.Free ||
                                       (axis == MoveAxis.Horizontal ? dir is Dir.Left or Dir.Right : dir is Dir.Up or Dir.Down);

        public static RocketSpec Blocker(string id, Vector2Int origin, Vector2Int size)
        {
            var spec = new RocketSpec(id, GameColor.Red, origin, size, 0);
            spec.isBlocker = true;
            return spec;
        }
    }

    /// <summary>Tahta kenarındaki renkli çıkış. start, üst/alt kenarda x, sol/sağ kenarda y hücresidir.</summary>
    [Serializable]
    public struct ExitSpec
    {
        public GameColor color;
        public Edge edge;
        public int start;
        public int width;

        public ExitSpec(GameColor color, Edge edge, int start, int width)
        {
            this.color = color;
            this.edge = edge;
            this.start = start;
            this.width = width;
        }
    }

    public static class DirExtensions
    {
        public static Vector2Int ToVector(this Dir d) => d switch
        {
            Dir.Up => Vector2Int.up,
            Dir.Down => Vector2Int.down,
            Dir.Left => Vector2Int.left,
            _ => Vector2Int.right,
        };

        public static Edge ToEdge(this Dir d) => d switch
        {
            Dir.Up => Edge.Top,
            Dir.Down => Edge.Bottom,
            Dir.Left => Edge.Left,
            _ => Edge.Right,
        };
    }
}
