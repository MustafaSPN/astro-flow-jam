using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam.Core
{
    public enum ExitCheck
    {
        Ok,
        /// <summary>Roket kenara ulaşamıyor; yolda başka parça var.</summary>
        Blocked,
        /// <summary>Kenarda roketin karşısında kapı yok, düz duvar.</summary>
        NoGate,
        /// <summary>Karşıdaki kapı başka renkte.</summary>
        WrongColor,
        /// <summary>Aynı renk kapı var ama açıklığı roketin enine genişliğini kapsamıyor.</summary>
        TooNarrow,
    }

    /// <summary>
    /// Kaydırmalı jam tahtasının saf mantığı. Görselden ve Unity sahnesinden bağımsızdır;
    /// hücreler ayrık hesaplanır, parçalar birbirinin üzerinden geçemez.
    /// Mekanikler: delik hücreler kalıcı doludur; tek eksenli blok yalnız kendi ekseninde kayar ve o
    /// eksendeki kenarlardan çıkar; buzlu blok tahtadan sayacı kadar blok çıkana dek, kilitli blok aynı gruptaki anahtarlı
    /// blok çıkana dek donuktur (hiç oynamaz). Asteroit hücreleri delik gibi doludur; sayaçlı asteroit tahtadan sayacı kadar
    /// blok çıkınca kırılır ve hücreleri boşalır. Katmanlı blokta dış katman çıkınca blok yerinde kalır, iç renge döner
    /// (bu da bir çıkış sayılır: sayaçlar azalır; anahtar ise blok tamamen çıkınca açar).
    /// </summary>
    public sealed class BoardModel
    {
        const int Empty = -1;
        const int Hole = -2;
        const int AsteroidBase = -10;          // k'inci asteroidin hücreleri AsteroidBase - k

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<ExitSpec> Exits => exits;

        readonly int[,] cells;
        readonly List<ExitSpec> exits;
        readonly RocketSpec[] rockets;
        readonly bool[] onBoard;
        readonly int[] iceLeft;
        readonly bool[] locked;
        readonly AsteroidSpec[] asteroids;
        readonly int[] asteroidLeft;           // kalan sayaç; kalıcıda 0, kırılmışta -1

        /// <summary>Bir çıkıştan sonra buz sayacı azalan ya da kilidi açılan bloklar (görünüm güncellemesi için).</summary>
        public event Action<int> FrozenChanged;

        /// <summary>Bir çıkıştan sonra sayacı azalan (ya da sıfıra inip kırılan) asteroit.</summary>
        public event Action<int> AsteroidChanged;

        public BoardModel(int width, int height, IEnumerable<RocketSpec> rocketSpecs, IEnumerable<ExitSpec> exitSpecs,
            IEnumerable<Vector2Int> holes = null, IEnumerable<AsteroidSpec> asteroidSpecs = null)
        {
            Width = width;
            Height = height;
            cells = new int[width, height];
            for (var x = 0; x < width; x++)
                for (var y = 0; y < height; y++)
                    cells[x, y] = Empty;

            if (holes != null)
                foreach (var h in holes)
                    if (InBounds(h)) cells[h.x, h.y] = Hole;

            asteroids = asteroidSpecs != null ? new List<AsteroidSpec>(asteroidSpecs).ToArray() : Array.Empty<AsteroidSpec>();
            asteroidLeft = new int[asteroids.Length];
            for (var k = 0; k < asteroids.Length; k++)
            {
                asteroidLeft[k] = Mathf.Max(0, asteroids[k].count);
                foreach (var c in asteroids[k].Cells)
                {
                    if (!InBounds(c) || cells[c.x, c.y] != Empty)
                        throw new ArgumentException($"Asteroit {k} tahta dışına ya da dolu hücreye taşıyor: {c}");
                    cells[c.x, c.y] = AsteroidBase - k;
                }
            }

            exits = new List<ExitSpec>(exitSpecs);
            rockets = new List<RocketSpec>(rocketSpecs).ToArray();
            onBoard = new bool[rockets.Length];
            iceLeft = new int[rockets.Length];
            locked = new bool[rockets.Length];

            for (var i = 0; i < rockets.Length; i++)
            {
                var r = rockets[i];
                ForEachCell(r, r.origin, c =>
                {
                    if (!InBounds(c))
                        throw new ArgumentException($"Roket {r.id} tahta dışına taşıyor: {c}");
                    if (cells[c.x, c.y] == Hole)
                        throw new ArgumentException($"Roket {r.id} bir deliğe denk geliyor: {c}");
                    if (cells[c.x, c.y] <= AsteroidBase)
                        throw new ArgumentException($"Roket {r.id} bir asteroide denk geliyor: {c}");
                    if (cells[c.x, c.y] != Empty)
                        throw new ArgumentException($"Roket {r.id}, {rockets[cells[c.x, c.y]].id} ile çakışıyor: {c}");
                    cells[c.x, c.y] = i;
                });
                onBoard[i] = true;
                iceLeft[i] = Mathf.Max(0, r.ice);
                locked[i] = r.lockGroup > 0;
            }
        }

        public int RocketCount => rockets.Length;
        public RocketSpec GetRocket(int index) => rockets[index];
        public bool IsOnBoard(int index) => onBoard[index];

        /// <summary>Tahtada kalan, çıkması gereken roket sayısı (engeller sayılmaz; onlar hiç çıkmaz).</summary>
        public int RemainingCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < onBoard.Length; i++)
                    if (onBoard[i] && !rockets[i].isBlocker) n++;
                return n;
            }
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        /// <summary>Delik hücre: tahtada yok sayılır, hiçbir blok giremez.</summary>
        public bool IsHole(Vector2Int c) => InBounds(c) && cells[c.x, c.y] == Hole;

        HashSet<Vector2Int> interiorHoles;

        /// <summary>
        /// Tahtanın içindeki delik: kenara (delik hücreleri üzerinden) bağlı değil. Duvarla çevrilir, ama tahtanın arkasındaki
        /// panelin kenar çizgisi içine girmez (yalnız dış sınır çizilir). Oynanışta kenar delikleri gibi geçilmez.
        /// </summary>
        public bool IsInteriorHole(Vector2Int c)
        {
            if (interiorHoles == null)
            {
                interiorHoles = new HashSet<Vector2Int>();
                var outer = new HashSet<Vector2Int>();
                var stack = new Stack<Vector2Int>();
                for (var x = 0; x < Width; x++)
                    for (var y = 0; y < Height; y++)
                        if ((x == 0 || y == 0 || x == Width - 1 || y == Height - 1) && cells[x, y] == Hole && outer.Add(new Vector2Int(x, y)))
                            stack.Push(new Vector2Int(x, y));
                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        var q = p + d;
                        if (InBounds(q) && cells[q.x, q.y] == Hole && outer.Add(q)) stack.Push(q);
                    }
                }
                for (var x = 0; x < Width; x++)
                    for (var y = 0; y < Height; y++)
                        if (cells[x, y] == Hole && !outer.Contains(new Vector2Int(x, y))) interiorHoles.Add(new Vector2Int(x, y));
            }
            return interiorHoles.Contains(c);
        }

        public int AsteroidCount => asteroids.Length;
        public AsteroidSpec GetAsteroid(int k) => asteroids[k];

        /// <summary>Asteroidin kalan sayacı: 0 kalıcı, -1 kırıldı.</summary>
        public int AsteroidLeft(int k) => asteroidLeft[k];

        /// <summary>Hücrede duran (kırılmamış) asteroit.</summary>
        public bool IsAsteroid(Vector2Int c) => InBounds(c) && cells[c.x, c.y] <= AsteroidBase;

        /// <summary>Kalan buz sayacı (0: buz yok ya da eridi).</summary>
        public int IceLeft(int index) => iceLeft[index];

        /// <summary>Anahtarı henüz çıkmamış kilitli blok.</summary>
        public bool IsLocked(int index) => locked[index];

        /// <summary>Donuk blok hiç oynamaz: buzlu (sayaç bitmemiş) ya da kilitli.</summary>
        public bool IsFrozen(int index) => iceLeft[index] > 0 || locked[index];

        /// <summary>Blok bu yönde kayabilir mi: donuk değil ve tek eksen kısıtı izin veriyor.</summary>
        public bool CanMove(int index, Dir dir) => !IsFrozen(index) && rockets[index].Allows(dir);

        public bool TryGetRocketAt(Vector2Int cell, out int index)
        {
            index = InBounds(cell) ? cells[cell.x, cell.y] : Empty;
            return index >= 0;
        }

        /// <summary>Roketin bu yönde kaç boş hücre ilerleyebileceği (engel veya kenar dahil değil).</summary>
        public int FreeDistance(int index, Dir dir)
        {
            if (!CanMove(index, dir)) return 0;
            var r = rockets[index];
            var step = dir.ToVector();
            var distance = 0;
            while (true)
            {
                var next = r.origin + step * (distance + 1);
                var blocked = false;
                ForEachCell(r, next, c =>
                {
                    if (!InBounds(c) || (cells[c.x, c.y] != Empty && cells[c.x, c.y] != index))
                        blocked = true;
                });
                if (blocked) return distance;
                distance++;
            }
        }

        /// <summary>Roketin ön yüzü ile bu yöndeki tahta kenarı arasındaki hücre sayısı.</summary>
        public int DistanceToEdge(int index, Dir dir)
        {
            var r = rockets[index];
            return dir switch
            {
                Dir.Up => Height - (r.origin.y + r.size.y),
                Dir.Down => r.origin.y,
                Dir.Left => r.origin.x,
                _ => Width - (r.origin.x + r.size.x),
            };
        }

        /// <summary>Hücre tahtanın içinde ve boş mu (ya da zaten bu rokete mi ait)?</summary>
        public bool IsFreeFor(int index, Vector2Int cell) =>
            InBounds(cell) && (cells[cell.x, cell.y] == Empty || cells[cell.x, cell.y] == index);

        /// <summary>
        /// Sürekli sürüklemede roketi, yol boyunca hiç engel kesmeden ulaştığı yeni hücreye taşır.
        /// Yolun kendisini çağıran taraf garanti eder; burada yalnızca hedef hücrelerin boş olduğu doğrulanır.
        /// </summary>
        public void Relocate(int index, Vector2Int origin)
        {
            var r = rockets[index];
            if (origin == r.origin) return;
            var valid = true;
            ForEachCell(r, origin, c => valid &= IsFreeFor(index, c));
            if (!valid) throw new InvalidOperationException($"Geçersiz konum: {r.id} → {origin}");

            ForEachCell(r, r.origin, c => cells[c.x, c.y] = Empty);
            r.origin = origin;
            rockets[index] = r;
            ForEachCell(r, r.origin, c => cells[c.x, c.y] = index);
        }

        public void Move(int index, Dir dir, int steps)
        {
            if (steps == 0) return;
            if (steps < 0 || steps > FreeDistance(index, dir))
                throw new InvalidOperationException($"Geçersiz hareket: {rockets[index].id} {dir} {steps}");

            var r = rockets[index];
            ForEachCell(r, r.origin, c => cells[c.x, c.y] = Empty);
            r.origin += dir.ToVector() * steps;
            rockets[index] = r;
            ForEachCell(r, r.origin, c => cells[c.x, c.y] = index);
        }

        /// <summary>Roket bu yöne kaydırılırsa tahtadan çıkabilir mi? Yörünge kapasitesi burada değil, OrbitModel'de kontrol edilir.</summary>
        public ExitCheck CheckExit(int index, Dir dir) => CheckExit(index, dir, out _);

        public ExitCheck CheckExit(int index, Dir dir, out int exitIndex)
        {
            exitIndex = -1;
            if (!CanMove(index, dir) || FreeDistance(index, dir) < DistanceToEdge(index, dir) || !PathClear(index, dir))
                return ExitCheck.Blocked;
            return CheckGate(index, rockets[index].origin, dir, out exitIndex);
        }

        /// <summary>
        /// Yalnızca kapı uygunluğu: roket verilen konumdayken bu yöndeki kenarda ona uyan kapı var mı?
        /// Yol kontrolü yapmaz; sürükleme sırasında kesişim noktalarındaki seçenekleri değerlendirmek için kullanılır.
        /// </summary>
        public ExitCheck CheckGate(int index, Vector2Int origin, Dir dir) => CheckGate(index, origin, dir, out _);

        ExitCheck CheckGate(int index, Vector2Int origin, Dir dir, out int exitIndex)
        {
            exitIndex = -1;
            var r = rockets[index];
            if (r.isBlocker) return ExitCheck.NoGate;
            var edge = dir.ToEdge();
            var vertical = edge == Edge.Top || edge == Edge.Bottom;
            var spanStart = vertical ? origin.x : origin.y;
            var spanEnd = spanStart + (vertical ? r.size.x : r.size.y);

            var result = ExitCheck.NoGate;
            for (var i = 0; i < exits.Count; i++)
            {
                var e = exits[i];
                if (e.edge != edge) continue;
                var overlaps = e.start < spanEnd && spanStart < e.start + e.width;
                if (!overlaps) continue;

                if (e.color != r.color)
                {
                    if (result == ExitCheck.NoGate) result = ExitCheck.WrongColor;
                    continue;
                }

                var covers = e.start <= spanStart && spanEnd <= e.start + e.width;
                if (covers)
                {
                    exitIndex = i;
                    return ExitCheck.Ok;
                }
                result = ExitCheck.TooNarrow;
            }
            return result;
        }

        /// <summary>Çıkışı kesinleştirir: roket tahtadan kaldırılır, hücreleri boşalır. Çıkış geri alınamaz.</summary>
        public void RemoveForExit(int index, Dir dir)
        {
            if (CheckExit(index, dir) != ExitCheck.Ok)
                throw new InvalidOperationException($"{rockets[index].id} {dir} yönünden çıkamaz.");
            ApplyExit(index);
        }

        /// <summary>
        /// Joker (UFO): blok yol ve kapı aranmadan tahtadan alınır; çıkış gibi sayılır (katmanlıda dış katman soyulur, buz ve
        /// asteroit sayaçları azalır, anahtar kilidi açar). Donuk (buzlu, kilitli) blok da alınabilir.
        /// </summary>
        public void RemoveByBooster(int index)
        {
            if (!onBoard[index] || rockets[index].isBlocker)
                throw new InvalidOperationException($"{rockets[index].id} tahtadan alınamaz.");
            ApplyExit(index);
        }

        /// <summary>Joker (UFO) iç katmanın rengini sildi: katmanlı blok tek katmanlı (dış renk) olur.</summary>
        public void StripInnerLayer(int index)
        {
            var r = rockets[index];
            if (!r.layered) return;
            r.layered = false;
            rockets[index] = r;
        }

        void ApplyExit(int index)
        {
            var r = rockets[index];
            var peeled = r.layered;
            if (peeled) rockets[index] = r.AfterOuterLayer();
            else
            {
                ForEachCell(r, r.origin, c => cells[c.x, c.y] = Empty);
                onBoard[index] = false;
            }

            // Her çıkış buz sayaçlarını bir azaltır; anahtarlı blok kendi grubundaki kilitleri açar.
            for (var i = 0; i < rockets.Length; i++)
            {
                if (!onBoard[i] || i == index) continue;
                var changed = false;
                if (iceLeft[i] > 0) { iceLeft[i]--; changed = true; }
                if (!peeled && locked[i] && r.keyGroup > 0 && rockets[i].lockGroup == r.keyGroup) { locked[i] = false; changed = true; }
                if (changed) FrozenChanged?.Invoke(i);
            }

            // Sayaçlı asteroitler de her çıkışta bir azalır; sıfırda kırılır, hücreleri boşalır.
            for (var k = 0; k < asteroids.Length; k++)
            {
                if (asteroidLeft[k] <= 0) continue;
                if (--asteroidLeft[k] == 0)
                {
                    asteroidLeft[k] = -1;
                    foreach (var c in asteroids[k].Cells) cells[c.x, c.y] = Empty;
                }
                AsteroidChanged?.Invoke(k);
            }
        }

        static void ForEachCell(RocketSpec r, Vector2Int origin, Action<Vector2Int> action)
        {
            foreach (var c in r.LocalCells) action(origin + c);
        }

        // Yol kuralı: her hücreden bu yöndeki kenara giden düz hatta başka bir bloğa ait
        // hücre olmamalı. Dikdörtgen olmayan bloklarda (L, T) kendi boşluklarının içinde duran bloklar da çıkışı keser.
        bool PathClear(int index, Dir dir)
        {
            var r = rockets[index];
            var step = dir.ToVector();
            foreach (var local in r.LocalCells)
                for (var c = r.origin + local + step; InBounds(c); c += step)
                    if (cells[c.x, c.y] != Empty && cells[c.x, c.y] != index) return false;
            return true;
        }
    }
}
