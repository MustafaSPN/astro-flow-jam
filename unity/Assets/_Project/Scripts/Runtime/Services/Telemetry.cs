using System;
using System.Collections.Generic;
using UnityEngine;
#if !UNITY_EDITOR
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
#endif

namespace AstroFlowJam
{
    /// <summary>
    /// Oyun ölçümü (Firebase Analytics) ve çökme raporu (Crashlytics). Bütün olaylar buradan geçer; amaç bölüm zorluğunu
    /// gerçek oyuncu verisiyle ayarlamak. Kişisel veri (isim, e-posta) ve reklam kimliği gönderilmez; Firebase reklam için
    /// kullanılmaz. Firebase hazır olmadan gelen olaylar sıraya alınır. Editörde ve yapılandırma dosyası
    /// (GoogleService-Info.plist) yoksa hiçbir şey gönderilmez, oyun etkilenmez.
    /// </summary>
    public static class Telemetry
    {
        const int MaxQueued = 64;

        static readonly List<(string name, Dictionary<string, object> args)> queue = new();
        static bool ready, started;

        /// <summary>Oyun açılışında bir kez.</summary>
        public static void Initialize()
        {
#if !UNITY_EDITOR
            if (started) return;
            started = true;
            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.Result != DependencyStatus.Available)
                    {
                        Debug.LogWarning($"[Telemetry] Firebase unavailable: {(task.IsFaulted ? task.Exception?.Message : task.Result.ToString())}");
                        return;
                    }
                    try
                    {
                        _ = FirebaseApp.DefaultInstance;                     // yapılandırma yoksa burada hata verir
                        Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                        FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                        // Ölçüm reklam için kullanılmaz: reklam izinleri kapalı, yalnız analiz deposu açık.
                        FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
                        {
                            { ConsentType.AnalyticsStorage, ConsentStatus.Granted },
                            { ConsentType.AdStorage, ConsentStatus.Denied },
                            { ConsentType.AdUserData, ConsentStatus.Denied },
                            { ConsentType.AdPersonalization, ConsentStatus.Denied },
                        });
                        ready = true;
                        foreach (var (name, args) in queue) Send(name, args);
                        queue.Clear();
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Telemetry] Firebase not configured: {e.Message}");
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Telemetry] Firebase failed: {e.Message}");
            }
#endif
        }

        // ------------------------------------------------------------------ bölüm

        /// <summary>
        /// Bölüm açıldı; deneme sayısı aynı bölüm için art arda kaçıncı açılış olduğunu söyler. version bölüm içeriğinin
        /// kısa özeti: bölümler yeniden üretilince eski ve yeni veri ayrılır.
        /// </summary>
        public static void LevelStart(int level, string version, int attempt) =>
            Log("level_start", ("level", level), ("version", version), ("attempt", attempt));

        /// <summary>
        /// Bölüm bitti. result: win, give_up_time, give_up_space, quit, restart. Süre saniye, kalan piksel, ilerleme
        /// yüzdesi (temizlenen piksel), kullanılan devam ve joker sayısıyla.
        /// </summary>
        public static void LevelEnd(int level, string version, string result, float seconds, int pixelsLeft, int progress, int continues, int boosters) =>
            Log("level_end", ("level", level), ("version", version), ("result", result), ("seconds", Mathf.RoundToInt(seconds)),
                ("pixels_left", pixelsLeft), ("progress", progress), ("continues", continues), ("boosters", boosters));

        /// <summary>Kayıp penceresi açıldı (time: süre bitti, space: yer kalmadı).</summary>
        public static void LevelFail(int level, string version, string kind, float seconds, int pixelsLeft, int progress) =>
            Log("level_fail", ("level", level), ("version", version), ("kind", kind), ("seconds", Mathf.RoundToInt(seconds)),
                ("pixels_left", pixelsLeft), ("progress", progress));

        /// <summary>Bölüm içeriğinin kısa, kararlı özeti (8 hex): içerik değişince değişir.</summary>
        public static string VersionOf(UnityEngine.Object levelAsset)
        {
            var json = JsonUtility.ToJson(levelAsset);
            var hash = 2166136261u;
            foreach (var c in json) { hash ^= c; hash *= 16777619u; }
            return hash.ToString("x8");
        }

        /// <summary>Devam satın alındı (price 0: bedava devam).</summary>
        public static void ContinueUsed(int level, string kind, int price) =>
            Log("continue_used", ("level", level), ("kind", kind), ("price", price));

        // ------------------------------------------------------------------ ekonomi

        public static void BoosterUsed(int level, string booster) =>
            Log("booster_used", ("level", level), ("booster", booster));

        public static void BoosterBought(int level, string booster, int price) =>
            Log("booster_bought", ("level", level), ("booster", booster), ("price", price));

        /// <summary>Coin harcandı (Firebase'in standart sanal para olayı).</summary>
        public static void CoinsSpent(string item, int amount) =>
            Log("spend_virtual_currency", ("virtual_currency_name", "coins"), ("item_name", item), ("value", amount));

        /// <summary>Oyuncu Play'e bastı ama canı yoktu.</summary>
        public static void OutOfLives(int level) => Log("out_of_lives", ("level", level));

        // ------------------------------------------------------------------ reklam ve satın alma

        /// <summary>Ödüllü reklam sonuna kadar izlendi (placement: life, claim_x2).</summary>
        public static void RewardedAd(string placement, int level) =>
            Log("ad_rewarded", ("placement", placement), ("level", level));

        public static void InterstitialAd(int level) => Log("ad_interstitial", ("level", level));

        /// <summary>AdMob'un bildirdiği reklam geliri (Firebase'in standart ad_impression olayı; değer mikro birimden çevrilir).</summary>
        public static void AdRevenue(string format, long valueMicros, string currency) =>
            Log("ad_impression", ("ad_platform", "AdMob"), ("ad_format", format), ("value", valueMicros / 1_000_000d), ("currency", currency ?? "USD"));

        public static void Purchase(string productId, int level) =>
            Log("iap_purchase", ("product", productId), ("level", level));

        /// <summary>Oyuncu özellikleri: raporları "en yüksek bölüm" ve "reklamsız" diye ayırmak için.</summary>
        public static void SetPlayer(int maxLevel, bool adsRemoved)
        {
#if !UNITY_EDITOR
            if (!ready) return;
            try
            {
                FirebaseAnalytics.SetUserProperty("max_level", maxLevel.ToString());
                FirebaseAnalytics.SetUserProperty("ads_removed", adsRemoved ? "yes" : "no");
            }
            catch (Exception e) { Debug.LogWarning($"[Telemetry] {e.Message}"); }
#endif
        }

        // ------------------------------------------------------------------ gönderim

        static void Log(string name, params (string key, object value)[] args)
        {
            var dict = new Dictionary<string, object>(args.Length);
            foreach (var (key, value) in args) dict[key] = value;
            if (!ready)
            {
                if (started && queue.Count < MaxQueued) queue.Add((name, dict));
                return;
            }
            Send(name, dict);
        }

        static void Send(string name, Dictionary<string, object> args)
        {
#if !UNITY_EDITOR
            try
            {
                var parameters = new List<Parameter>(args.Count);
                foreach (var (key, value) in args)
                    parameters.Add(value switch
                    {
                        int i => new Parameter(key, i),
                        long l => new Parameter(key, l),
                        float f => new Parameter(key, f),
                        double d => new Parameter(key, d),
                        _ => new Parameter(key, value?.ToString() ?? ""),
                    });
                FirebaseAnalytics.LogEvent(name, parameters.ToArray());
            }
            catch (Exception e) { Debug.LogWarning($"[Telemetry] {name}: {e.Message}"); }
#endif
        }
    }
}
