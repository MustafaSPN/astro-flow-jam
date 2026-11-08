using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AstroFlowJam.Core
{
    /// <summary>
    /// İki referans oyunun seviye verisinden çıkan tasarım kurallarıyla bölüm üretir:
    /// - Resim katman katman açılır: renkler dıştan içe soğan katmanlarıdır (sınırlar hafif gürültülü),
    ///   her rengin boncuk sayısı o rengin roket kapasitelerinin toplamına eşittir.
    /// - Tahta bir kuyruk gibi çalışır: dış katmanın roketleri kapılarına yakın ("kuyruğun önü"), iç katmanların
    ///   roketleri uzakta ve öndekilerin arkasında durur ("kuyruğun derini"); öndekini çıkarmak hem resmi açar hem yolu açar.
    /// - İlk bölümler seyrek başlar; engel yoktur.
    /// Her bölüm için birçok aday üretilir, <see cref="LevelSimulator"/> ile ölçülür ve hedeflere en yakını seçilir.
    /// </summary>
    public static class LevelGenerator
    {
        public enum Shape { Circle, RoundedSquare, Diamond, Rocket }

        public sealed class Recipe
        {
            public string id, name, mechanic;
            public int boardWidth, boardHeight;
            /// <summary>Dıştan içe katman sırasıyla renkler.</summary>
            public GameColor[] colors;
            /// <summary>
            /// Renk başına tek hücreli ve iki hücreli (1×2/2×1) blok sayıları, renk sırasıyla. Blok kapıda hücrelerine
            /// bölünür; her hücre 10 ya da 20 mermili bir rokettir.
            /// </summary>
            public int[] small, big;
            public Shape shape;
            public int seed;
            /// <summary>Hedefler: başlangıçta yararlı çıkış oranı, kilit açma çıkışları ve zorunlu kötü hamle sayısı.</summary>
            public float minUsefulAtStart = 0.6f;
            public int minUnlocks, maxForced;
            public int candidates = 30;
            /// <summary>
            /// Hazır piksel tasarımı (ör. içe alınmış piksel kedi): verilirse boncuklar şekilden üretilmez, bu düzen kullanılır;
            /// roket kapasiteleri her rengin boncuk sayısına göre <see cref="CapacityPlanner"/> ile dağıtılır.
            /// </summary>
            public string fixedLayout;
            public int fixedColumns, fixedRows;
        }

        public sealed class Level
        {
            public Recipe recipe;
            public int beadColumns, beadRows;
            public string beadLayout;
            public List<RocketSpec> rockets;
            public List<ExitSpec> exits;
            public LevelSimulator.Result result;
        }

        const float TenAmmoShare = 0.4f;

        public static Level Generate(Recipe recipe, float pictureWidth = 6.0f, int beltCapacity = 5, int slotCount = 5)
        {
            Level best = null;
            var bestScore = float.MaxValue;
            for (var c = 0; c < recipe.candidates; c++)
            {
                var rng = new System.Random(recipe.seed * 1000 + c);
                var level = Candidate(recipe, rng);
                if (level == null) continue;
                level.result = LevelSimulator.Run(new LevelSimulator.Input
                {
                    boardWidth = recipe.boardWidth, boardHeight = recipe.boardHeight, rockets = level.rockets, exits = level.exits,
                    beadColumns = level.beadColumns, beadRows = level.beadRows, beadLayout = level.beadLayout,
                    pictureWidth = pictureWidth, beltCapacity = beltCapacity, slotCount = slotCount,
                });
                var score = Score(recipe, level.result);
                if (score < bestScore) { bestScore = score; best = level; }
            }
            return best;
        }

        // Düşük daha iyi; çözülemeyen aday hiçbir zaman seçilmez (çözülebilir aday yoksa en azından biri döner).
        static float Score(Recipe r, LevelSimulator.Result res)
        {
            var s = res.solved ? 0f : 1000f;
            s += Mathf.Max(0f, r.minUsefulAtStart - res.usefulAtStart) * 50f;
            s += Mathf.Max(0, r.minUnlocks - res.unlockExits) * 8f;
            s += Mathf.Max(0, res.forcedSends - r.maxForced) * 15f;
            s += res.seconds * 0.01f;
            return s;
        }

        static Level Candidate(Recipe recipe, System.Random rng)
        {
            var counts = new int[recipe.colors.Length];
            var rocketsPerColor = new List<(bool big, int ammoPerCell)>[recipe.colors.Length];
            for (var i = 0; i < recipe.colors.Length; i++)
            {
                rocketsPerColor[i] = new List<(bool, int)>();
                for (var k = 0; k < recipe.big[i]; k++) rocketsPerColor[i].Add((true, rng.NextDouble() < TenAmmoShare ? 10 : 20));
                for (var k = 0; k < recipe.small[i]; k++) rocketsPerColor[i].Add((false, rng.NextDouble() < TenAmmoShare ? 10 : 20));
                foreach (var (big, ammo) in rocketsPerColor[i]) counts[i] += ammo * (big ? 2 : 1);
            }

            var (cols, rows, layout) = recipe.fixedLayout != null
                ? (recipe.fixedColumns, recipe.fixedRows, recipe.fixedLayout)
                : BuildBeads(recipe, counts, rng);
            var exits = BuildExits(recipe, rng);
            var rockets = PlaceRockets(recipe, rocketsPerColor, exits, rng);
            if (rockets == null) return null;
            if (recipe.fixedLayout != null) CapacityPlanner.Assign(rockets, BeadCounts(layout));
            return new Level { recipe = recipe, beadColumns = cols, beadRows = rows, beadLayout = layout, rockets = rockets, exits = exits };
        }

        /// <summary>Düzendeki renk başına boncuk sayısı ('.' boş hücre).</summary>
        public static Dictionary<GameColor, int> BeadCounts(string layout)
        {
            var counts = new Dictionary<GameColor, int>();
            foreach (var ch in layout)
            {
                if (!BeadGrid.TryParseColor(ch, out var color)) continue;
                counts.TryGetValue(color, out var n);
                counts[color] = n + 1;
            }
            return counts;
        }

        // Şekil içinden tam 'toplam' hücre seçilir; her hücrenin dış kenara uzaklığı (dört yönden en kısa) katmanı belirler.
        static (int cols, int rows, string layout) BuildBeads(Recipe recipe, int[] counts, System.Random rng)
        {
            var total = 0;
            foreach (var c in counts) total += c;
            var side = Mathf.CeilToInt(Mathf.Sqrt(total / 0.62f));
            var cells = new List<(int x, int y, float metric)>();
            for (var y = 0; y < side; y++)
                for (var x = 0; x < side; x++)
                {
                    var u = (x + 0.5f) / side * 2f - 1f;
                    var v = (y + 0.5f) / side * 2f - 1f;
                    cells.Add((x, y, ShapeMetric(recipe.shape, u, v) + (float)rng.NextDouble() * 0.04f));
                }
            cells.Sort((a, b) => a.metric.CompareTo(b.metric));
            var filled = new bool[side, side];
            for (var i = 0; i < total; i++) filled[cells[i].x, cells[i].y] = true;

            var order = new List<(int x, int y, float depth)>();
            for (var y = 0; y < side; y++)
                for (var x = 0; x < side; x++)
                {
                    if (!filled[x, y]) continue;
                    int l = 0, r = 0, t = 0, b = 0;
                    for (var i = x - 1; i >= 0; i--) if (filled[i, y]) l++;
                    for (var i = x + 1; i < side; i++) if (filled[i, y]) r++;
                    for (var i = y - 1; i >= 0; i--) if (filled[x, i]) t++;
                    for (var i = y + 1; i < side; i++) if (filled[x, i]) b++;
                    var depth = Mathf.Min(Mathf.Min(l, r), Mathf.Min(t, b));
                    order.Add((x, y, depth + (float)rng.NextDouble() * 1.6f));
                }
            order.Sort((a, b) => a.depth.CompareTo(b.depth));

            var grid = new char[side, side];
            for (var y = 0; y < side; y++) for (var x = 0; x < side; x++) grid[x, y] = '.';
            var k = 0;
            for (var c = 0; c < counts.Length; c++)
                for (var n = 0; n < counts[c]; n++, k++)
                    grid[order[k].x, order[k].y] = BeadGrid.ToChar(recipe.colors[c]);

            var sb = new StringBuilder(side * side);
            for (var y = 0; y < side; y++) for (var x = 0; x < side; x++) sb.Append(grid[x, y]);
            return (side, side, sb.ToString());
        }

        static float ShapeMetric(Shape shape, float u, float v)
        {
            switch (shape)
            {
                case Shape.RoundedSquare:
                    return Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4f) + Mathf.Pow(Mathf.Abs(v), 4f), 0.25f);
                case Shape.Diamond:
                    return Mathf.Abs(u) + Mathf.Abs(v) * 0.9f;
                case Shape.Rocket:
                    // Dikey gövde, üstte sivri burun.
                    return Mathf.Max(Mathf.Abs(u) * 1.6f + Mathf.Max(0f, -v - 0.2f) * 1.2f, Mathf.Abs(v) * 0.85f);
                default:
                    return Mathf.Sqrt(u * u + v * v);
            }
        }

        // Renk başına bir kapı (2 hücre genişliğinde); kenarlar karışık sırayla dağıtılır, aynı kenarda çakışmaz.
        static List<ExitSpec> BuildExits(Recipe recipe, System.Random rng)
        {
            var edges = new List<Edge> { Edge.Top, Edge.Left, Edge.Right, Edge.Bottom };
            Shuffle(edges, rng);
            var used = new Dictionary<Edge, List<int>>();
            var exits = new List<ExitSpec>();
            for (var i = 0; i < recipe.colors.Length; i++)
            {
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    var edge = edges[(i + attempt) % edges.Count];
                    var length = edge is Edge.Top or Edge.Bottom ? recipe.boardWidth : recipe.boardHeight;
                    var start = rng.Next(0, length - 1);
                    if (!used.TryGetValue(edge, out var list)) used[edge] = list = new List<int>();
                    if (list.Exists(s => Math.Abs(s - start) < 3)) continue;
                    list.Add(start);
                    exits.Add(new ExitSpec(recipe.colors[i], edge, start, 2));
                    break;
                }
            }
            return exits.Count == recipe.colors.Length ? exits : BuildExits(recipe, rng);
        }

        // Roketler katman sırasıyla yerleşir: katman ne kadar içteyse kapısından o kadar uzak bir hücre hedeflenir.
        static List<RocketSpec> PlaceRockets(Recipe recipe, List<(bool big, int ammoPerCell)>[] perColor, List<ExitSpec> exits, System.Random rng)
        {
            int w = recipe.boardWidth, h = recipe.boardHeight;
            var occupied = new bool[w, h];
            var rockets = new List<RocketSpec>();
            var layers = perColor.Length;
            var maxDistance = w + h;
            var id = 0;

            for (var c = 0; c < layers; c++)
            {
                var exit = exits[c];
                var depthFraction = layers == 1 ? 0f : c / (float)(layers - 1);
                foreach (var (big, ammoPerCell) in perColor[c])
                {
                    var options = new List<(Vector2Int origin, Vector2Int size, float weight)>();
                    var sizes = big ? new[] { new Vector2Int(1, 2), new Vector2Int(2, 1) } : new[] { Vector2Int.one };
                    foreach (var size in sizes)
                        for (var x = 0; x + size.x <= w; x++)
                            for (var y = 0; y + size.y <= h; y++)
                            {
                                if (!Free(occupied, x, y, size)) continue;
                                var d = DistanceToExit(exit, x, y, size, w, h);
                                var desired = depthFraction * maxDistance * 0.6f;
                                var weight = Mathf.Exp(-Mathf.Abs(d - desired) * 1.3f) + 0.01f;
                                options.Add((new Vector2Int(x, y), size, weight));
                            }
                    if (options.Count == 0) return null;
                    var pick = WeightedPick(options, rng);
                    for (var x = 0; x < pick.size.x; x++)
                        for (var y = 0; y < pick.size.y; y++)
                            occupied[pick.origin.x + x, pick.origin.y + y] = true;
                    var capacity = ammoPerCell * pick.size.x * pick.size.y;
                    rockets.Add(new RocketSpec($"{BeadGrid.ToChar(recipe.colors[c])}{++id}", recipe.colors[c], pick.origin, pick.size, capacity));
                }
            }
            return rockets;
        }

        static bool Free(bool[,] occupied, int x, int y, Vector2Int size)
        {
            for (var dx = 0; dx < size.x; dx++)
                for (var dy = 0; dy < size.y; dy++)
                    if (occupied[x + dx, y + dy]) return false;
            return true;
        }

        // Roketin kapının önüne gelmesi için gereken hücre mesafesi (kapıya dik eksende kenara, paralel eksende açıklığa).
        static float DistanceToExit(ExitSpec e, int x, int y, Vector2Int size, int w, int h)
        {
            float along, across;
            switch (e.edge)
            {
                case Edge.Top: across = h - (y + size.y); along = Gap(x, size.x, e.start, e.width); break;
                case Edge.Bottom: across = y; along = Gap(x, size.x, e.start, e.width); break;
                case Edge.Left: across = x; along = Gap(y, size.y, e.start, e.width); break;
                default: across = w - (x + size.x); along = Gap(y, size.y, e.start, e.width); break;
            }
            return across + along;
        }

        static float Gap(int start, int length, int gateStart, int gateWidth)
        {
            if (start >= gateStart && start + length <= gateStart + gateWidth) return 0f;
            return Mathf.Min(Mathf.Abs(start - gateStart), Mathf.Abs(start + length - (gateStart + gateWidth))) + 0.5f;
        }

        static (Vector2Int origin, Vector2Int size, float weight) WeightedPick(List<(Vector2Int origin, Vector2Int size, float weight)> options, System.Random rng)
        {
            var total = 0f;
            foreach (var o in options) total += o.weight;
            var r = (float)rng.NextDouble() * total;
            foreach (var o in options)
            {
                r -= o.weight;
                if (r <= 0f) return o;
            }
            return options[options.Count - 1];
        }

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
