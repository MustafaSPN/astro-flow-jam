using System;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Can sistemi (türün yaygın değerleri): en çok 5 can, eksik can 30 dakikada bir
    /// dolar, coinle tamamen doldurma 900. Bölüm kaybedilince (vazgeçince) ya da süre başladıktan sonra bölümden çıkılınca
    /// (ana ekran, yeniden başlat) 1 can gider. Can dolumu gerçek zamanla hesaplanır (uygulama kapalıyken de dolar).
    /// </summary>
    public static class Lives
    {
        public const int Max = 5;
        public const int RefillSeconds = 30 * 60;
        public const int RefillCost = 900;

        static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>Şu anki can (geçen sürede dolan canlar eklenir ve kaydedilir).</summary>
        public static int Count
        {
            get
            {
                Regenerate();
                return IsInfinite ? Max : GameSave.GetInt(SaveKeys.Lives, Max);
            }
        }

        public static bool IsFull => Count >= Max;

        /// <summary>Paketlerden gelen sınırsız can sürüyor mu? Sürerken can gitmez; bitince kalan can kaldığı yerden devam eder.</summary>
        public static bool IsInfinite => InfiniteSeconds > 0;

        public static long InfiniteSeconds =>
            Math.Max(0L, ReadTime(SaveKeys.LivesInfiniteUntil, 0L) - Now);

        /// <summary>Sınırsız cana süre ekler (süre doluysa üstüne biner).</summary>
        public static void AddInfinite(int seconds)
        {
            GameSave.SetString(SaveKeys.LivesInfiniteUntil, (Now + InfiniteSeconds + seconds).ToString());
            GameSave.Save();
        }

        /// <summary>Sınırsız can süresi: bir saatten uzunsa "23h 59m", kısaysa "59:57" (can widget'ına sığar).</summary>
        public static string InfiniteText
        {
            get
            {
                var s = InfiniteSeconds;
                return s >= 3600 ? $"{s / 3600}h {s / 60 % 60:00}m" : $"{s / 60:00}:{s % 60:00}";
            }
        }

        /// <summary>Sonraki cana kalan saniye (can doluysa 0).</summary>
        public static int SecondsToNext
        {
            get
            {
                Regenerate();
                if (GameSave.GetInt(SaveKeys.Lives, Max) >= Max) return 0;
                var stamp = ReadTime(SaveKeys.LivesStamp, Now);
                return (int)Mathf.Max(0, RefillSeconds - (Now - stamp));
            }
        }

        /// <summary>"29:59" biçiminde sonraki cana kalan süre.</summary>
        public static string NextText
        {
            get
            {
                var s = SecondsToNext;
                return $"{s / 60:00}:{s % 60:00}";
            }
        }

        /// <summary>Kayıtlı zaman damgası; kayıt bozuksa varsayılan (oyun hata vermez).</summary>
        static long ReadTime(string key, long fallback) =>
            long.TryParse(GameSave.GetString(key, ""), out var value) ? value : fallback;

        static void Regenerate()
        {
            var count = GameSave.GetInt(SaveKeys.Lives, Max);
            if (count >= Max) return;
            var stamp = ReadTime(SaveKeys.LivesStamp, Now);
            var gained = (int)((Now - stamp) / RefillSeconds);
            if (gained <= 0) return;
            count = Mathf.Min(Max, count + gained);
            GameSave.SetInt(SaveKeys.Lives, count);
            GameSave.SetString(SaveKeys.LivesStamp, (stamp + (long)gained * RefillSeconds).ToString());
            GameSave.Save();
        }

        /// <summary>Bir can harcar (kayıp). Can doluyken harcanırsa dolum sayacı şimdi başlar.</summary>
        public static void Lose()
        {
            if (IsInfinite) return;
            var count = Count;
            if (count <= 0) return;
            if (count >= Max) GameSave.SetString(SaveKeys.LivesStamp, Now.ToString());
            GameSave.SetInt(SaveKeys.Lives, count - 1);
            GameSave.Save();
        }

        /// <summary>Geliştirici menüsü: can ekler/çıkarır (0–Max).</summary>
        public static void DevAdd(int delta)
        {
            var count = Count;
            var next = Mathf.Clamp(count + delta, 0, Max);
            if (count >= Max && next < Max) GameSave.SetString(SaveKeys.LivesStamp, Now.ToString());
            GameSave.SetInt(SaveKeys.Lives, next);
            GameSave.Save();
        }

        /// <summary>Reklam ödülü: 1 can (doluysa eklenmez).</summary>
        public static bool AddOne()
        {
            var count = Count;
            if (count >= Max) return false;
            GameSave.SetInt(SaveKeys.Lives, count + 1);
            GameSave.Save();
            return true;
        }

        /// <summary>Coinle canları doldurur; coin yetmezse false.</summary>
        public static bool TryRefill()
        {
            if (IsFull || !CoinWallet.TrySpend(RefillCost)) return false;
            GameSave.SetInt(SaveKeys.Lives, Max);
            GameSave.Save();
            return true;
        }
    }
}
