using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Reklamlar (AdMob): bölüm kazanıldıktan sonra geçiş reklamı (Remove Ads alındıysa yok), can penceresinde ödüllü reklam (her zaman). Bütün reklam çağrıları
    /// buradan geçer. Oyun reklamı beklemez: reklam hazır değilse akış sürer, ödüllü reklamda ödül verilmez. Takip izni (ATT)
    /// burada istenmez (bkz. Tracking). AB ve Birleşik Krallık onayı Google UMP ile sorulur; onaydan önce reklam yüklenmez. Gerçek reklam birimleri
    /// yalnız App Store derlemesinde (AFJ_STORE), diğer derlemelerde Google'ın test birimleri.
    /// </summary>
    public static class Ads
    {
#if AFJ_STORE
        // App Store derlemesi (BuildIOS.AppStore): AdMob hesabındaki gerçek reklam birimleri.
        const string InterstitialUnit = "ca-app-pub-1035801224777804/3289453756";
        const string RewardedUnit = "ca-app-pub-1035801224777804/3313504484";
#else
        // Geliştirme ve test derlemeleri: Google'ın test birimleri. Kendi cihazında gerçek reklama tıklamak AdMob
        // hesabını riske atar.
        const string InterstitialUnit = "ca-app-pub-3940256099942544/4411468910";
        const string RewardedUnit = "ca-app-pub-3940256099942544/1712485313";
#endif
        const int InterstitialFirstLevel = 20;
        const float FullscreenGapSeconds = 60f;

        static InterstitialAd interstitial;
        static RewardedAd rewarded;
        static bool started, rewardedLoading, interstitialLoading;
        static int rewardedFails, interstitialFails;
        const float RetryBaseSeconds = 10f, RetryMaxSeconds = 120f;
        static float lastFullscreenAt = float.NegativeInfinity;

        /// <summary>Oyun açılışında bir kez: önce onay akışı, sonra reklam SDK'sı.</summary>
        public static void Initialize()
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.SetiOSAppPauseOnBackground(true);       // tam ekran reklamda oyun ve ses durur
            ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
            {
                if (updateError != null) { StartAds(); return; }
                // Onay penceresi yalnız onayın zorunlu olduğu bölgelerde (AB, Birleşik Krallık, İsviçre) açılır. Böylece
                // AdMob'da reklam takibi açıklama mesajı açık olsa bile Apple izni açılışta sorulmaz; izni Tracking,
                // 3. bölümden sonra kendisi sorar.
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
            // Onay mesajı henüz yayımlanmamışsa UMP hata verir; geliştirme derlemesi test reklamlarıyla yine çalışır.
            if (started) return;
            if (!ConsentInformation.CanRequestAds() && !DevTools.Enabled)
            {
                Debug.LogWarning("[Ads] Consent does not allow ad requests yet");
                return;
            }
            started = true;
            MobileAds.Initialize(_ =>
            {
                LoadInterstitial();
                LoadRewarded();
            });
        }

        static void LoadInterstitial()
        {
            if (!started || interstitial != null || interstitialLoading || ShopCatalog.AdsRemoved) return;
            interstitialLoading = true;
            InterstitialAd.Load(InterstitialUnit, new AdRequest(), (ad, error) =>
            {
                interstitialLoading = false;
                if (error != null)
                {
                    Debug.LogWarning($"[Ads] Interstitial load failed: {error.GetMessage()}");
                    RetryLater(interstitialFails++, LoadInterstitial);
                    return;
                }
                interstitialFails = 0;
                interstitial = ad;
                ad.OnAdPaid += value => Telemetry.AdRevenue("interstitial", value.Value, value.CurrencyCode);
            });
        }

        static void LoadRewarded()
        {
            if (!started || rewarded != null || rewardedLoading) return;
            rewardedLoading = true;
            RewardedAd.Load(RewardedUnit, new AdRequest(), (ad, error) =>
            {
                rewardedLoading = false;
                if (error != null)
                {
                    Debug.LogWarning($"[Ads] Rewarded load failed: {error.GetMessage()}");
                    RetryLater(rewardedFails++, LoadRewarded);
                    return;
                }
                rewardedFails = 0;
                rewarded = ad;
                ad.OnAdPaid += value => Telemetry.AdRevenue("rewarded", value.Value, value.CurrencyCode);
            });
        }

        // Reklam gelmezse (yeni reklam birimlerinde ve düşük doldurmada sık) artan aralıkla yeniden dener: 10, 20, 40 … en çok 120 sn.
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
            if (!due || interstitial == null || !interstitial.CanShowAd())
            {
                LoadInterstitial();
                done();
                return;
            }
            var ad = interstitial;
            interstitial = null;
            void Finish()
            {
                lastFullscreenAt = Time.unscaledTime;
                ad.Destroy();
                LoadInterstitial();
                done();
            }
            ad.OnAdFullScreenContentClosed += Finish;
            ad.OnAdFullScreenContentFailed += _ => Finish();
            Telemetry.InterstitialAd(levelNumber);
            ad.Show();
        }

        /// <summary>
        /// Ödüllü reklam: sonuna kadar izlenirse kapandıktan sonra <paramref name="earned"/>; ödülsüz kapatılırsa
        /// <paramref name="skipped"/>; reklam hazır değilse <paramref name="unavailable"/>.
        /// </summary>
        public static void ShowRewarded(Action earned, Action unavailable, Action skipped = null)
        {
            if (rewarded == null || !rewarded.CanShowAd())
            {
                LoadRewarded();
                unavailable();
                return;
            }
            var ad = rewarded;
            rewarded = null;
            var gotReward = false;
            void Finish()
            {
                lastFullscreenAt = Time.unscaledTime;
                ad.Destroy();
                LoadRewarded();
                if (gotReward) earned();
                else skipped?.Invoke();
            }
            ad.OnAdFullScreenContentClosed += Finish;
            ad.OnAdFullScreenContentFailed += _ => Finish();
            ad.Show(_ => gotReward = true);
        }
    }
}
