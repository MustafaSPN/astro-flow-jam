using System;
using System.Collections.Generic;

namespace AstroFlowJam.Core
{
    /// <summary>
    /// Bölümdeki roket kapasitelerini boncuk sayılarından üretir: bir rengin boncukları o rengin roketlerine
    /// paylaştırılır, büyük roket küçüğün iki katı pay alır. Bölünmeyen artık boncuklar büyük roketlerden
    /// başlayarak birer birer dağıtılır. Böylece her rengin toplam kapasitesi boncuk sayısına tam eşit olur.
    /// </summary>
    public static class CapacityPlanner
    {
        public static void Assign(IList<RocketSpec> rockets, IReadOnlyDictionary<GameColor, int> beadCounts)
        {
            foreach (GameColor color in Enum.GetValues(typeof(GameColor)))
            {
                beadCounts.TryGetValue(color, out var beads);
                var members = new List<int>();
                var shares = 0;
                for (var i = 0; i < rockets.Count; i++)
                {
                    if (rockets[i].isBlocker || rockets[i].color != color) continue;
                    members.Add(i);
                    shares += Share(rockets[i]);
                }

                if (members.Count == 0)
                {
                    if (beads > 0) throw new ArgumentException($"{color} için {beads} boncuk var ama roket yok.");
                    continue;
                }
                if (beads < shares)
                    throw new ArgumentException($"{color}: {beads} boncuk {members.Count} rokete yetmiyor.");

                var unit = beads / shares;
                var leftover = beads - unit * shares;
                members.Sort((a, b) => Share(rockets[b]).CompareTo(Share(rockets[a])));
                foreach (var i in members)
                {
                    var r = rockets[i];
                    r.capacity = unit * Share(r);
                    rockets[i] = r;
                }
                for (var k = 0; leftover > 0; k = (k + 1) % members.Count, leftover--)
                {
                    var r = rockets[members[k]];
                    r.capacity++;
                    rockets[members[k]] = r;
                }
            }
        }

        static int Share(RocketSpec r) => r.OrbitUnits;
    }
}
