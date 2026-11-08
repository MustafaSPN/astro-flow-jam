using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Oyuncunun coinleri (cihazda kalıcı). Ekonomi tür araştırmasına göre ayarlandı:
    /// bölüm başı 40 coin, başlangıçta 400 coin.
    /// </summary>
    public static class CoinWallet
    {
        public const int InitialCoins = 500;   // başlangıç coini
        public const int LevelReward = 40;

        public static int Coins
        {
            get => GameSave.GetInt(SaveKeys.Coins, InitialCoins);
            private set
            {
                GameSave.SetInt(SaveKeys.Coins, Mathf.Max(0, value));
                GameSave.Save();
            }
        }

        public static void Add(int amount) => Coins += amount;

        /// <summary>
        /// HUD için kısa tutar (aşağı yuvarlanır, gereksiz sıfırlar atılır): 1000 altı olduğu gibi (999), 1000–9999 iki
        /// ondalıklı (1k, 1.4k, 1.42k), 10 000–99 999 tek ondalıklı (10k, 10.4k), 100 000 ve üstü tam sayı (100k, 134k).
        /// </summary>
        public static string Short(int value)
        {
            if (value < 1000) return value.ToString();
            // Milyon ve üstü: 1M, 1.5M, 16.4M, 120M (aşağı yuvarlanır).
            if (value >= 1000000)
            {
                if (value >= 100000000) return value / 1000000 + "M";
                var tenth = value % 1000000 / 100000;
                return value / 1000000 + (tenth > 0 ? "." + tenth : "") + "M";
            }
            if (value >= 100000) return value / 1000 + "k";
            var tenths = value >= 10000;
            var fraction = tenths ? value % 1000 / 100 : value % 1000 / 10;
            var digits = fraction.ToString(tenths ? "0" : "00").TrimEnd('0');
            return value / 1000 + (digits.Length > 0 ? "." + digits : "") + "k";
        }

        /// <summary>Yeterli coin varsa düşer ve true döner.</summary>
        public static bool TrySpend(int amount)
        {
            if (Coins < amount) return false;
            Coins -= amount;
            return true;
        }
    }
}
