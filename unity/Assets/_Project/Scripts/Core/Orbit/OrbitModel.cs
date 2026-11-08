using System;

namespace AstroFlowJam.Core
{
    /// <summary>
    /// Yörünge kapasitesi birimle ölçülür: küçük roket 1, büyük roket 2 birim.
    /// Transferdeki roket de kapasiteye dahildir; rezervasyon çıkış anında atomik yapılır.
    /// </summary>
    public sealed class OrbitModel
    {
        public int Capacity { get; private set; }
        public int UsedUnits { get; private set; }
        public int FreeUnits => Capacity - UsedUnits;

        public OrbitModel(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        /// <summary>Kapasite artar (Hangar jokeri: bölüm boyunca +1 birim).</summary>
        public void Expand(int units)
        {
            if (units <= 0) throw new ArgumentOutOfRangeException(nameof(units));
            Capacity += units;
        }

        public bool CanReserve(int units) => units <= FreeUnits;

        public bool TryReserve(int units)
        {
            if (!CanReserve(units)) return false;
            UsedUnits += units;
            return true;
        }

        public void Release(int units)
        {
            if (units > UsedUnits) throw new InvalidOperationException("Ayrılmamış birim serbest bırakılamaz.");
            UsedUnits -= units;
        }
    }
}
