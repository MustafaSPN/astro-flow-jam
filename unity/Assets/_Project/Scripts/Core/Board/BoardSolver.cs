using System.Collections.Generic;
using System.Text;

namespace AstroFlowJam.Core
{
    /// <summary>
    /// Bölüm doğrulaması: tahtadaki tüm roketler bir sırayla çıkarılabiliyor mu? Engeller tahtada kalır ama sürüklenebilir.
    /// Yalnızca jam tahtasını sınar; boncuk sırası ve yörünge kapasitesi sonraki aşamada eklenecek.
    /// Mekanikler: delikler tahtaya aktarılır; her çıkıştan sonra kalan blokların buz sayacı bir azalır ve çıkan bloğun
    /// anahtar grubundaki kilitler açılır (durum blok tanımlarında tutulur, <see cref="AfterExit"/>). Asteroitler blok
    /// durumunun dışında tutulur: her çıkışta sayaçlıların sayacı bir azalır, sıfıra inen kırılıp listeden düşer.
    /// </summary>
    /// <remarks>
    /// Kaydırmalar geri alınabilir olduğundan bir durumdan ulaşılabilen tüm dizilimler tek bir bileşen oluşturur.
    /// Bir roketi çıkarmak yalnızca yer açar; eski bileşendeki her dizilim çıkıştan sonra da ulaşılabilir kalır.
    /// Bu yüzden bileşen içinde en yakın çıkışı bulup uygulamak ve aramayı kalan tahtada yeniden başlatmak eksiksizdir.
    /// </remarks>
    public static class BoardSolver
    {
        public static bool CanClearBoard(int width, int height, IReadOnlyList<RocketSpec> rockets, IReadOnlyList<ExitSpec> exits,
            int maxStatesPerStep = 500000, IReadOnlyList<UnityEngine.Vector2Int> holes = null, IReadOnlyList<AsteroidSpec> asteroids = null)
        {
            var current = new List<RocketSpec>(rockets);
            var rocks = asteroids != null ? new List<AsteroidSpec>(asteroids) : new List<AsteroidSpec>();
            while (current.Exists(r => !r.isBlocker))
            {
                var next = FindStateAfterNearestExit(width, height, current, exits, maxStatesPerStep, holes, rocks);
                if (next == null) return false;
                current = next;
                rocks = AsteroidsAfterExit(rocks);
            }
            return true;
        }

        /// <summary>Bir çıkıştan sonra asteroitler: sayaçlıların sayacı bir azalır, sıfıra inen kırılır (listeden çıkar).</summary>
        public static List<AsteroidSpec> AsteroidsAfterExit(List<AsteroidSpec> rocks)
        {
            var after = new List<AsteroidSpec>(rocks.Count);
            foreach (var a in rocks)
            {
                if (a.count == 0) { after.Add(a); continue; }
                var b = a; b.count--;
                if (b.count > 0) after.Add(b);
            }
            return after;
        }

        /// <summary>
        /// i'inci blok çıktıktan sonraki tahta: kalan blokların buzu bir azalır, anahtarın grubundaki kilitler açılır.
        /// Katmanlı blokta yalnız dış katman çıkar; blok iç renkte yerinde kalır.
        /// </summary>
        public static List<RocketSpec> AfterExit(List<RocketSpec> state, int i)
        {
            var peeled = state[i].layered;
            var key = peeled ? 0 : state[i].keyGroup;
            var after = new List<RocketSpec>(state.Count);
            for (var j = 0; j < state.Count; j++)
            {
                if (j == i)
                {
                    if (peeled) after.Add(state[i].AfterOuterLayer());
                    continue;
                }
                var r = state[j];
                if (r.ice > 0) r.ice--;
                if (key > 0 && r.lockGroup == key) r.lockGroup = 0;
                after.Add(r);
            }
            return after;
        }

        static List<RocketSpec> FindStateAfterNearestExit(int width, int height, List<RocketSpec> start, IReadOnlyList<ExitSpec> exits, int maxStates,
            IReadOnlyList<UnityEngine.Vector2Int> holes, IReadOnlyList<AsteroidSpec> asteroids)
        {
            var seen = new HashSet<string> { Key(start) };
            var queue = new Queue<List<RocketSpec>>();
            queue.Enqueue(start);

            while (queue.Count > 0 && seen.Count < maxStates)
            {
                var state = queue.Dequeue();
                var board = new BoardModel(width, height, state, exits, holes, asteroids);

                for (var i = 0; i < state.Count; i++)
                    foreach (Dir dir in System.Enum.GetValues(typeof(Dir)))
                        if (board.CheckExit(i, dir) == ExitCheck.Ok)
                            return AfterExit(state, i);

                for (var i = 0; i < state.Count; i++)
                {
                    foreach (Dir dir in System.Enum.GetValues(typeof(Dir)))
                    {
                        var free = board.FreeDistance(i, dir);
                        for (var s = 1; s <= free; s++)
                        {
                            var next = new List<RocketSpec>(state);
                            var r = next[i];
                            r.origin += dir.ToVector() * s;
                            next[i] = r;
                            if (seen.Add(Key(next))) queue.Enqueue(next);
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>Tahtadan çıkabilecek bir roket: en az kaç kaydırmayla ve çıktıktan sonraki dizilim.</summary>
        public sealed class ExitOption
        {
            public string rocketId;
            public RocketSpec rocket;
            public int moves;
            public List<RocketSpec> after;
        }

        /// <summary>
        /// Mevcut dizilimden kaydırmalarla ulaşılabilen her çıkış (roket başına en az kaydırmalı olan). Bir kaydırma,
        /// bir roketin tek yönde bir veya daha çok hücre ilerlemesidir.
        /// </summary>
        public static List<ExitOption> ExitOptions(int width, int height, IReadOnlyList<RocketSpec> rockets, IReadOnlyList<ExitSpec> exits,
            int maxStates = 20000, IReadOnlyList<UnityEngine.Vector2Int> holes = null, IReadOnlyList<AsteroidSpec> asteroids = null)
        {
            var found = new Dictionary<string, ExitOption>();
            var start = new List<RocketSpec>(rockets);
            var seen = new HashSet<string> { Key(start) };
            var queue = new Queue<(List<RocketSpec> state, int moves)>();
            queue.Enqueue((start, 0));

            while (queue.Count > 0 && seen.Count < maxStates)
            {
                var (state, moves) = queue.Dequeue();
                var board = new BoardModel(width, height, state, exits, holes, asteroids);
                for (var i = 0; i < state.Count; i++)
                {
                    if (found.ContainsKey(state[i].id)) continue;
                    foreach (Dir dir in System.Enum.GetValues(typeof(Dir)))
                        if (board.CheckExit(i, dir) == ExitCheck.Ok)
                        {
                            var after = AfterExit(state, i);
                            found[state[i].id] = new ExitOption { rocketId = state[i].id, rocket = state[i], moves = moves, after = after };
                            break;
                        }
                }

                for (var i = 0; i < state.Count; i++)
                    foreach (Dir dir in System.Enum.GetValues(typeof(Dir)))
                    {
                        var free = board.FreeDistance(i, dir);
                        for (var s = 1; s <= free; s++)
                        {
                            var next = new List<RocketSpec>(state);
                            var r = next[i];
                            r.origin += dir.ToVector() * s;
                            next[i] = r;
                            if (seen.Add(Key(next))) queue.Enqueue((next, moves + 1));
                        }
                    }
            }
            return new List<ExitOption>(found.Values);
        }

        static string Key(List<RocketSpec> state)
        {
            var parts = new List<string>(state.Count);
            foreach (var r in state) parts.Add($"{r.id}:{r.origin.x},{r.origin.y}{(r.layered ? "L" : "")}");
            parts.Sort();
            var sb = new StringBuilder();
            foreach (var p in parts) sb.Append(p).Append('|');
            return sb.ToString();
        }
    }
}
