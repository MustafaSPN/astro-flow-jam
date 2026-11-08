using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Purchasing;

namespace AstroFlowJam
{
    /// <summary>
    /// Mağaza kataloğu: App Store ürün kimlikleri, içerikleri ve mağaza bağlanamazsa
    /// gösterilen yedek fiyatlar. Gerçek fiyat App Store'dan gelir (oyuncunun para birimiyle). Ürünler App Store Connect'te
    /// aynı kimliklerle açılmalıdır: coin paketleri ve paketler "Consumable", reklam kaldırma "Non-Consumable".
    /// </summary>
    public static class ShopCatalog
    {
        const string Prefix = "com.sepen.astroflowjam.";

        public readonly struct Bundle
        {
            public readonly string id, name, price, tag;
            public readonly int coins, boostersEach, infiniteHours;
            public readonly bool oneTime;

            public Bundle(string id, string name, string price, int coins, int boostersEach, int infiniteHours, string tag = null, bool oneTime = false)
            {
                this.id = Prefix + id; this.name = name; this.price = price; this.coins = coins; this.boostersEach = boostersEach;
                this.infiniteHours = infiniteHours; this.tag = tag; this.oneTime = oneTime;
            }
        }

        public readonly struct CoinPack
        {
            public readonly string id, price, tag;
            public readonly int coins;

            public CoinPack(int coins, string price, string tag = null)
            {
                id = $"{Prefix}coins{coins}"; this.coins = coins; this.price = price; this.tag = tag;
            }
        }

        /// <summary>Reklam kaldırma: bölüm aralarındaki geçiş reklamları kalkar, isteğe bağlı ödüllü reklamlar kalır.</summary>
        public const string RemoveAdsId = Prefix + "removeads";
        public const string RemoveAdsPrice = "$3.99";

        /// <summary>Coin paketleri (1000 $1.99 … 100 000 $99.99).</summary>
        public static readonly CoinPack[] CoinPacks =
        {
            new(1000, "$1.99"), new(5000, "$7.99"), new(10000, "$14.99", "Popular"),
            new(25000, "$29.99"), new(50000, "$54.99", "Best Value"), new(100000, "$99.99"),
        };

        /// <summary>Paketler: coin, joker ve sınırsız can; başlangıç paketi bir kez alınır. Reklamları kaldırmaz.</summary>
        public static readonly Bundle[] Bundles =
        {
            new("bundle.starter", "Starter Bundle", "$4.99", 1000, 1, 1, "One Time", true),
            new("bundle.small", "Small Bundle", "$9.99", 5000, 1, 2),
            new("bundle.medium", "Medium Bundle", "$19.99", 10000, 2, 2, "Popular"),
            new("bundle.large", "Large Bundle", "$34.99", 25000, 5, 6),
            new("bundle.mega", "Mega Bundle", "$79.99", 50000, 14, 18),
            new("bundle.ultra", "Ultra Bundle", "$99.99", 65000, 24, 24, "Best Value"),
        };

        public static bool StarterBought => GameSave.GetInt(SaveKeys.StarterBundleBought, 0) == 1;
        public static bool AdsRemoved => GameSave.GetInt(SaveKeys.AdsRemoved, 0) == 1;

        /// <summary>Mağazaya sorulacak ürünler.</summary>
        public static List<ProductDefinition> ProductDefinitions()
        {
            var list = new List<ProductDefinition> { new(RemoveAdsId, ProductType.NonConsumable) };
            foreach (var pack in CoinPacks) list.Add(new ProductDefinition(pack.id, ProductType.Consumable));
            foreach (var bundle in Bundles) list.Add(new ProductDefinition(bundle.id, ProductType.Consumable));
            return list;
        }

        /// <summary>Satın alınan ürünün içeriğini verir; oyuncuya gösterilecek kısa mesajı döndürür (bilinmeyen ürün: null).</summary>
        public static string GrantProduct(string id)
        {
            if (id == RemoveAdsId)
            {
                GameSave.SetInt(SaveKeys.AdsRemoved, 1);
                GameSave.Save();
                return "Ads removed!";
            }
            foreach (var pack in CoinPacks)
            {
                if (pack.id != id) continue;
                CoinWallet.Add(pack.coins);
                GameSave.Save();
                return $"+{pack.coins.ToString("N0", CultureInfo.InvariantCulture)} coins";
            }
            foreach (var bundle in Bundles)
            {
                if (bundle.id != id) continue;
                Grant(bundle);
                return $"{bundle.name} purchased!";
            }
            return null;
        }

        /// <summary>Paketin içeriğini verir (coin, her jokerden adet, sınırsız can).</summary>
        static void Grant(in Bundle bundle)
        {
            CoinWallet.Add(bundle.coins);
            for (var i = 0; i < BoosterInventory.Count; i++) BoosterInventory.Add(i, bundle.boostersEach);
            Lives.AddInfinite(bundle.infiniteHours * 3600);
            if (bundle.oneTime) GameSave.SetInt(SaveKeys.StarterBundleBought, 1);
            GameSave.Save();
        }
    }
}
