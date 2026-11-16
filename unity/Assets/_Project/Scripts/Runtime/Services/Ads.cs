using System;
using GoogleMobileAds.Ump.Api;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Reklamlar (Unity LevelPlay): bölüm kazanıldıktan sonra geçiş reklamı (Remove Ads alındıysa yok), can penceresinde ve
    /// ödül ekranında ödüllü reklam. Bütün reklam çağrıları buradan geçer. Oyun reklamı beklemez: reklam hazır değilse akış
    /// sürer, ödüllü reklamda ödül verilmez; ödüllü reklam butonları <see cref="RewardedReady"/> ile yalnız reklam hazırken
    /// görünür. Takip izni (ATT) burada istenmez (bkz. Tracking). AB, Birleşik Krallık ve İsviçre onayı Google UMP ile
    /// sorulur; LevelPlay onayı UMP'nin yazdığı IAB TCF kaydından okur.
    /// </summary>
    public static class Ads
    {
        const string AppKey = "299ada15d";
        const string InterstitialUnit = "in7xe4pfnr4dcs6h";
        const string RewardedUnit = "y11jypsz8999tflg";

        const int InterstitialFirstLevel = 20;
        const float FullscreenGapSeconds = 60f;
        const float RetryBaseSeconds = 10f, RetryMaxSeconds = 120f;
        const int RewardWaitMs = 500;   // ödül olayı kapanmadan sonra gelebilir: kapanınca bu kadar beklenir

        static LevelPlayInterstitialAd interstitial;
        static LevelPlayRewardedAd rewarded;
        static bool initStarted, ready, rewardedLoading, interstitialLoading;
        static int rewardedFails, interstitialFails;
        static float lastFullscreenAt = float.NegativeInfinity;

        /// <summary>Ödüllü reklam gösterilmeye hazır mı (butonlar buna göre görünür).</summary>
        public static bool RewardedReady => ready && rewarded != null && rewarded.IsAdReady();

        /// <summary>Ödüllü reklamın hazır olma durumu değişti (yüklendi ya da gösterildi).</summary>
        public static event Action RewardedReadyChanged;

        /// <summary>Oyun açılışında bir kez: önce onay akışı, sonra reklam SDK'sı.</summary>
        public static void Initialize()
        {
            ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
            {
                if (updateError != null) { StartAds(); return; }
                // Onay penceresi yalnız onayın zorunlu olduğu bölgelerde açılır; Apple takip izni açılışta sorulmaz,
                // Tracking 3. bölümden sonra kendisi sorar.
                if (ConsentInformation.PrivacyOptionsRequirementStatus != PrivacyOptionsRequirementStatus.Required) { StartAds(); return; }
                ConsentForm.LoadAndShowConsentFormIfRequired(_ => StartAds());
            });
        }

        /// <summary>Onayın zorunlu olduğu bölgede mi (Ayarlar'daki "Privacy Choices" butonu yalnız orada görünür).</summary>
        public static bool PrivacyOptionsRequired
        {
            get
            {
                try { return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; }
                catch (Exception) { return false; }
            }
        }

        /// <summary>Oyuncunun reklam onayını yeniden seçebileceği Google formu; açılamazsa mesaj verir.</summary>
        public static void ShowPrivacyOptions(Action<string> failed)
        {
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error != null) failed("Privacy options are not available right now.");
                else StartAds();                              // onay yeni verildiyse reklamlar başlar
            });
        }

        static void StartAds()
        {
            if (initStarted) return;
            initStarted = true;
            LevelPlay.SetMetaData("is_child_directed", "false");   // genel kitle (App Store'da Made for Kids değil)
            if (!IsStoreBuild) LevelPlay.SetMetaData("is_test_suite", "enable");
            LevelPlay.OnInitSuccess += _ =>
            {
                ready = true;
                LevelPlay.SetPauseGame(true);                       // tam ekran reklamda oyun ve ses durur
                CreateAds();
            };
            LevelPlay.OnInitFailed += error =>
            {
                Debug.LogWarning($"[Ads] LevelPlay init failed: {error.ErrorCode} {error.ErrorMessage}");
                initStarted = false;                                // sonra yeniden denenir
                RetryLater(0, StartAds);
            };
            LevelPlay.OnImpressionDataReady += data =>
            {
                if (data.Revenue is double revenue)
                    Telemetry.AdRevenue(data.AdFormat ?? "unknown", (long)Math.Round(revenue * 1_000_000d), "USD");
            };
            LevelPlay.Init(AppKey);
        }

#if AFJ_STORE
        const bool IsStoreBuild = true;
#else
        const bool IsStoreBuild = false;
#endif

        static void CreateAds()
        {
            rewarded = new LevelPlayRewardedAd(RewardedUnit);
            rewarded.OnAdLoaded += _ => { rewardedLoading = false; rewardedFails = 0; RewardedReadyChanged?.Invoke(); };
            rewarded.OnAdLoadFailed += error =>
            {
                rewardedLoading = false;
                Debug.LogWarning($"[Ads] Rewarded load failed: {error.ErrorCode} {error.ErrorMessage}");
                RetryLater(rewardedFails++, LoadRewarded);
            };

            interstitial = new LevelPlayInterstitialAd(InterstitialUnit);
            interstitial.OnAdLoaded += _ => { interstitialLoading = false; interstitialFails = 0; };
            interstitial.OnAdLoadFailed += error =>
            {
                interstitialLoading = false;
                Debug.LogWarning($"[Ads] Interstitial load failed: {error.ErrorCode} {error.ErrorMessage}");
                RetryLater(interstitialFails++, LoadInterstitial);
            };

            LoadRewarded();
            LoadInterstitial();
        }

        static void LoadInterstitial()
        {
            if (!ready || interstitial == null || interstitialLoading || interstitial.IsAdReady() || ShopCatalog.AdsRemoved) return;
            interstitialLoading = true;
            interstitial.LoadAd();
        }

        static void LoadRewarded()
        {
            if (!ready || rewarded == null || rewardedLoading || rewarded.IsAdReady()) return;
            rewardedLoading = true;
            rewarded.LoadAd();
        }

        // Reklam gelmezse artan aralıkla yeniden dener: 10, 20, 40 … en çok 120 sn.
        static async void RetryLater(int fails, Action load)
        {
            var delay = Mathf.Min(RetryBaseSeconds * (1 << Mathf.Min(fails, 4)), RetryMaxSeconds);
            await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(delay));
            load();
        }

        /// <summary>Bölüm kazanıldıktan sonra: kurallar uygunsa geçiş reklamı, ardından <paramref name="done"/>.</summary>
        public static void ShowInterstitialAfterWin(int levelNumber, Action done)
        {
            var due = !ShopCatalog.AdsRemoved && levelNumber >= InterstitialFirstLevel && Time.unscaledTime - lastFullscreenAt >= FullscreenGapSeconds;
            if (!due || interstitial == null || !interstitial.IsAdReady())
            {
                LoadInterstitial();
                done();
                return;
            }
            var finished = false;
            void Finish()
            {
                if (finished) return;
                finished = true;
                interstitial.OnAdClosed -= OnClosed;
                interstitial.OnAdDisplayFailed -= OnFailed;
                lastFullscreenAt = Time.unscaledTime;
                LoadInterstitial();
                done();
            }
            void OnClosed(LevelPlayAdInfo _) => Finish();
            void OnFailed(LevelPlayAdInfo _, LevelPlayAdError error)
            {
                Debug.LogWarning($"[Ads] Interstitial show failed: {error.ErrorCode} {error.ErrorMessage}");
                Finish();
            }
            interstitial.OnAdClosed += OnClosed;
            interstitial.OnAdDisplayFailed += OnFailed;
            Telemetry.InterstitialAd(levelNumber);
            interstitial.ShowAd();
        }

        /// <summary>
        /// Ödüllü reklam: sonuna kadar izlenirse kapandıktan sonra <paramref name="earned"/>; ödülsüz kapatılırsa
        /// <paramref name="skipped"/>; reklam hazır değilse <paramref name="unavailable"/>.
        /// </summary>
        public static void ShowRewarded(Action earned, Action unavailable, Action skipped = null)
        {
            if (!RewardedReady)
            {
                LoadRewarded();
                unavailable();
                return;
            }
            var gotReward = false;
            var finished = false;
            void Finish()
            {
                if (finished) return;
                finished = true;
                rewarded.OnAdRewarded -= OnRewarded;
                rewarded.OnAdClosed -= OnClosed;
                rewarded.OnAdDisplayFailed -= OnFailed;
                lastFullscreenAt = Time.unscaledTime;
                RewardedReadyChanged?.Invoke();
                LoadRewarded();
                if (gotReward) earned();
                else skipped?.Invoke();
            }
            void OnRewarded(LevelPlayAdInfo _, LevelPlayReward __)
            {
                gotReward = true;
            }
            async void OnClosed(LevelPlayAdInfo _)
            {
                if (!gotReward) await System.Threading.Tasks.Task.Delay(RewardWaitMs);   // ödül olayı geç gelebilir
                Finish();
            }
            void OnFailed(LevelPlayAdInfo _, LevelPlayAdError error)
            {
                Debug.LogWarning($"[Ads] Rewarded show failed: {error.ErrorCode} {error.ErrorMessage}");
                Finish();
            }
            rewarded.OnAdRewarded += OnRewarded;
            rewarded.OnAdClosed += OnClosed;
            rewarded.OnAdDisplayFailed += OnFailed;
            rewarded.ShowAd();
            RewardedReadyChanged?.Invoke();
        }
    }
}
