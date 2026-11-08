using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Joker adetleri ve ekonomisi (cihazda kalıcı). Ekonomi tür araştırmasına göre: joker
    /// açıldığı bölümde 2 adet bedava verilir, sonra coinle 3'lü paket alınır. Sıra: 0 Hangar, 1 Warp, 2 Freeze, 3 UFO.
    /// </summary>
    public static class BoosterInventory
    {
        public const int Count = 4;
        public const int PackSize = 3;
        public const int FreeOnUnlock = 2;
        /// <summary>Açıldığı bölüm.</summary>
        public static readonly int[] UnlockLevel = { 6, 8, 12, 18 };
        /// <summary>3'lü paket fiyatı (coin).</summary>
        public static readonly int[] PackPrice = { 900, 1400, 900, 1900 };

        public static int Get(int booster) => GameSave.GetInt(SaveKeys.BoosterCount(booster), 0);

        static void Set(int booster, int value)
        {
            GameSave.SetInt(SaveKeys.BoosterCount(booster), Mathf.Max(0, value));
            GameSave.Save();
        }

        /// <summary>Geliştirici menüsü: adedi doğrudan değiştirir (0 altına inmez).</summary>
        public static void DevAdd(int booster, int delta) => Set(booster, Get(booster) + delta);

        /// <summary>Mağaza paketi: adet ekler.</summary>
        public static void Add(int booster, int amount) => Set(booster, Get(booster) + amount);

        public static bool IsUnlocked(int booster, int level) => level >= UnlockLevel[booster];

        /// <summary>Joker açıldıysa ve açılış hediyesi verilmediyse 2 adet verir; verildiyse true.</summary>
        public static bool GrantUnlockGift(int booster, int level)
        {
            if (!IsUnlocked(booster, level) || GameSave.GetInt(SaveKeys.BoosterGranted(booster), 0) == 1) return false;
            GameSave.SetInt(SaveKeys.BoosterGranted(booster), 1);
            Set(booster, Get(booster) + FreeOnUnlock);
            return true;
        }

        public static bool TrySpend(int booster)
        {
            var n = Get(booster);
            if (n <= 0) return false;
            Set(booster, n - 1);
            return true;
        }

        /// <summary>Coin yeterse 3'lü paketi alır.</summary>
        public static bool TryBuyPack(int booster)
        {
            if (!CoinWallet.TrySpend(PackPrice[booster])) return false;
            Set(booster, Get(booster) + PackSize);
            return true;
        }
    }
}
