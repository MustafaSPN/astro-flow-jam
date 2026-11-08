using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;
using Object = UnityEngine.Object;

namespace AstroFlowJam
{
    /// <summary>
    /// Bölüm sonu penceresi: ödül rozeti, reklamlı "Claim x2" ve "Continue"; toplanınca coinler kavisli yollarla coin
    /// göstergesine uçar ve sayaç her varışta artar.
    /// </summary>
    public sealed class LevelCompletePopup
    {
        const int FlyingCoins = 10;
        const float CoinFlySeconds = 0.8f;
        const float CoinFlyStagger = 0.06f;
        const int PopupLayer = 110, FlyingCoinLayer = 130;
        static readonly Color ContinueTop = new(0.38f, 0.93f, 0.45f);
        static readonly Color ContinueBottom = new(0.1f, 0.68f, 0.26f);

        readonly RectTransform canvas;
        readonly MonoBehaviour host;
        readonly CoinWidget coins;
        readonly Func<RectTransform> safeArea;

        GameObject popup;
        Image popupDim;
        RectTransform popupPanel, popupCoin, popupCoinGlow, popupRays, flyLayer;
        Text popupLevel, popupReward;
        RectTransform claimButton;
        Action popupDone;
        int popupAmount;
        bool claiming;
        float popupTime = -1f;
        float popupFit = 1f;
        RectTransform popupStars, popupRocket, popupSparkles, popupPlanet;
        readonly System.Collections.Generic.List<(RectTransform glow, float phase)> starGlows = new();
        readonly System.Collections.Generic.List<(RectTransform spark, Vector2 at, float phase)> starTwinkles = new();
        Text popupCompleted;
        RectTransform claimOnce;

        public LevelCompletePopup(RectTransform canvas, MonoBehaviour host, CoinWidget coins, Func<RectTransform> safeArea)
        {
            this.canvas = canvas;
            this.host = host;
            this.coins = coins;
            this.safeArea = safeArea;
            Build();
        }

        // Ekrana sığdırma: popup (ışık hüzmeleri ve kupa dahil) her ekran oranında güvenli alana sığacak kadar küçülür;
        // telefonlarda (referans 1080×2340 ve daha uzun) ölçek 1'dir, kısa/geniş ekranlarda (iPad, eski iPhone) küçülür.
        float FitScale()
        {
            if (!popupPanel) return 1f;
            var area = safeArea().rect;
            // Panelin merkezine göre içerik sınırları; ışık hüzmeleri ve haleler kenardan taşabilir, hesaba katılmaz.
            var extent = new Rect(0f, 0f, 0f, 0f);
            foreach (RectTransform t in popupPanel)
            {
                if (t.name.Contains("Rays") || t.name.Contains("Glow")) continue;
                var r = new Rect(t.anchoredPosition - t.sizeDelta * 0.5f, t.sizeDelta);
                extent = Rect.MinMaxRect(Mathf.Min(extent.xMin, r.xMin), Mathf.Min(extent.yMin, r.yMin), Mathf.Max(extent.xMax, r.xMax), Mathf.Max(extent.yMax, r.yMax));
            }
            var center = popupPanel.anchoredPosition.y;
            var halfH = Mathf.Max(extent.yMax + center, -(extent.yMin + center)) + 40f;
            var halfW = Mathf.Max(extent.xMax, -extent.xMin) + 20f;
            return Mathf.Min(1f, area.height * 0.5f / halfH, area.width * 0.5f / halfW);
        }

        void Build()
        {
            // Yeni tasarım koddan kurulur.
            BuildPanel();

            // Uçan coinler her şeyin üstünde.
            flyLayer = NewRect("Flying Coins", canvas);
            flyLayer.anchorMin = Vector2.zero;
            flyLayer.anchorMax = Vector2.one;
            flyLayer.offsetMin = flyLayer.offsetMax = Vector2.zero;
            Layer(flyLayer.gameObject, FlyingCoinLayer, false);
            popup.SetActive(false);
        }

        /// <summary>
        /// Bölüm sonu popup'ı (Level_Win_Concept): 9 dilimli panelde üstte "Level N / Complete!", parlayan üç yıldız, gezegenin
        /// önünde süzülen roket ve parıltılar; altında ödül rozeti (coin + miktar), reklamlı "Claim x2" yeşil butonu ve
        /// reklamsız "Claim" butonu. Koddan kurulur (editördeki prefab üreticisi de bunu kullanır).
        /// </summary>
        GameObject BuildPanel()
        {
            popup = NewRect("Level Complete", canvas).gameObject;
            var root = (RectTransform)popup.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            Layer(popup, PopupLayer, true);
            popupDim = popup.AddComponent<Image>();
            popupDim.color = new Color(0.01f, 0.02f, 0.06f, 0.72f);

            popupPanel = NewRect("Panel", root);
            Place(popupPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(PopupWidth, PopupHeight));
            Sliced(popupPanel, "Frame", PopupPanel, Vector2.zero, new Vector2(PopupWidth, PopupHeight), 1100f);

            // Başlık: iki satır, beyaz, kalın lacivert kenar.
            popupLevel = NewText("Level", popupPanel, 96, UiArt.Navy);
            Place(popupLevel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 530f), new Vector2(760f, 110f));
            popupCompleted = NewText("Completed", popupPanel, 96, UiArt.Navy);
            Place(popupCompleted.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(760f, 110f));
            popupCompleted.text = "Complete!";

            // Gezegen, parıltılar, roket; üstte ışık hüzmeleri ve haleli yıldızlar.
            popupSparkles = Img(popupPanel, "Sparkles", UiArt.Asset("Popup/sparkles"), new Vector2(0f, 70f), new Vector2(770f, 670f));   // panelin iç çizgilerine kadar
            popupPlanet = Img(popupPanel, "Planet", UiArt.Asset("Popup/planet"), new Vector2(0f, 10f), new Vector2(540f, 514f));
            popupRocket = Img(popupPanel, "Rocket", UiArt.Asset("Popup/rocket"), new Vector2(-10f, 0f), new Vector2(400f, 286f));
            popupRays = Img(popupPanel, "Rays", UiArt.Rays(), new Vector2(0f, 290f), new Vector2(460f, 460f));
            popupRays.GetComponent<Image>().color = new Color(1f, 0.92f, 0.55f, 0.3f);
            starGlows.Clear();
            foreach (var (x, y, d, ph) in new[] { (-190f, 245f, 180f, 0.9f), (0f, 290f, 240f, 0f), (190f, 245f, 180f, 1.8f) })
                starGlows.Add((Img(popupPanel, "Star Glow", UiArt.Glow((int)d, (int)d, d / 2f, d * 0.3f, new Color(1f, 0.85f, 0.3f, 0.35f)), new Vector2(x, y), new Vector2(d, d)), ph));
            popupStars = Img(popupPanel, "Stars", UiArt.Asset("Popup/stars"), new Vector2(0f, 262f), new Vector2(560f, 227f));
            starTwinkles.Clear();
            foreach (var (x, y, d, ph) in new[] { (-300f, 300f, 46f, 0f), (-130f, 360f, 36f, 1.3f), (120f, 380f, 44f, 2.4f), (300f, 290f, 50f, 0.7f), (-280f, 150f, 34f, 3.1f), (290f, 160f, 40f, 1.9f) })
                starTwinkles.Add((Img(popupPanel, "Twinkle", UiArt.Sparkle(), new Vector2(x, y), new Vector2(d, d)), new Vector2(x, y), ph));

            // Ödül rozeti: coin ve miktar.
            RewardBadge(popupPanel, new Vector2(0f, -250f), new Vector2(460f, 150f));
            popupCoinGlow = Img(popupPanel, "Coin Glow", UiArt.Glow(220, 220, 110f, 60f, new Color(1f, 0.82f, 0.3f, 0.3f)), new Vector2(-105f, -250f), new Vector2(190f, 190f));
            popupCoin = Img(popupPanel, "Coin", UiArt.CoinIcon(), new Vector2(-105f, -250f), new Vector2(112f, 112f));
            popupReward = NewText("Reward", popupPanel, 84, UiArt.Navy);
            Place(popupReward.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(55f, -248f), new Vector2(240f, 100f));
            popupReward.color = TitleYellow;

            // Reklamlı 2 kat ödül (yeşil, büyük) ve reklamsız 1 kat ödül.
            claimButton = SlicedButton(popupPanel, "Claim x2", GreenButton, new Vector2(0f, -440f), new Vector2(660f, 140f), ClaimWithAd);
            var ad = Img(claimButton, "Ad Icon", UiArt.Asset("Popup/ad_icon"), new Vector2(-210f, 6f), new Vector2(100f, 85f));
            ad.anchorMin = ad.anchorMax = new Vector2(0.5f, 0.5f);
            var x2 = NewText("Label", claimButton, 70, UiArt.Navy);
            Place(x2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(55f, 6f), new Vector2(420f, 120f));
            x2.text = "Claim x2";
            claimLabel = x2;
            claimOnce = SlicedButton(popupPanel, "Claim", DefaultButton, new Vector2(0f, -585f), new Vector2(440f, 126f), () => Claim(1));
            var once = NewText("Label", claimOnce, 56, UiArt.Navy);
            Place(once.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero);
            once.text = "Continue";

            return popup;
        }

        const float PopupWidth = 900f, PopupHeight = 1460f;

        /// <summary>Bölüm sonu popup'ını açar; ödül toplanınca 'done' çağrılır.</summary>
        public void Show(int level, int reward, Action done)
        {
            popupFit = FitScale();
            popupDone = done;
            popupAmount = reward;
            claiming = false;
            popupLevel.text = $"Level {level}";
            shownLevel = level;
            popupReward.text = $"+{reward}";
            popup.SetActive(true);
            coins.SetOnTop(true);
            popupTime = 0f;
        }

        // multiplier 2: reklamlı (reklam sistemi henüz yok; doğrudan verilir), 1: reklamsız. Rozetteki miktar güncellenir.
        Text claimLabel;
        int shownLevel;
        bool adShowing;

        // "Claim x2": ödüllü reklam sonuna kadar izlenirse ödül iki katı; reklam hazır değilse buton kısa süre uyarı gösterir.
        void ClaimWithAd()
        {
            if (claiming || adShowing) return;
            adShowing = true;
            Ads.ShowRewarded(() => { adShowing = false; Telemetry.RewardedAd("claim_x2", shownLevel); Claim(2); }, () =>
            {
                adShowing = false;
                host.StartCoroutine(AdNotReady());
            }, () => adShowing = false);
        }

        IEnumerator AdNotReady()
        {
            claimLabel.text = "Ad not ready";
            yield return new WaitForSecondsRealtime(1.5f);
            claimLabel.text = "Claim x2";
        }

        void Claim(int multiplier)
        {
            if (claiming) return;
            claiming = true;
            popupAmount *= multiplier;
            popupReward.text = $"+{popupAmount}";
            if (multiplier > 1) host.StartCoroutine(Punch(popupReward.rectTransform, 0.35f, 0.3f));
            host.StartCoroutine(FlyCoins());
        }

        // Coinler popup'taki coinden göstergeye kavisli yollarla, kendi ekseninde dönerek uçar; her varışta sayaç artar.
        IEnumerator FlyCoins()
        {
            var start = popupCoin.position;
            var target = coins.IconPosition;
            var from = coins.Shown;
            CoinWallet.Add(popupAmount);
            Sfx.Play(Sfx.Coin);
            var landed = 0;
            var rng = new System.Random();
            for (var i = 0; i < FlyingCoins; i++)
            {
                var side = (float)(rng.NextDouble() * 2.0 - 1.0);
                host.StartCoroutine(FlyOne(start, target, side, i * CoinFlyStagger, () =>
                {
                    landed++;
                    coins.Set(from + Mathf.RoundToInt(popupAmount * landed / (float)FlyingCoins));
                    host.StartCoroutine(Punch(coins.Pill, 0.18f, 0.25f));
                    Haptics.Shoot();
                }));
            }
            while (landed < FlyingCoins) yield return null;
            coins.Set(CoinWallet.Coins);
            yield return new WaitForSecondsRealtime(0.35f);
            // Kapanış: panel küçülür, karartma kalkar.
            for (var t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
            {
                var k = t / 0.22f;
                popupPanel.localScale = Vector3.one * (popupFit * Mathf.Lerp(1f, 0.6f, Tween.Evaluate(Tween.Ease.InQuad, k)));
                popupDim.color = new Color(0.01f, 0.02f, 0.06f, 0.72f * (1f - k));
                yield return null;
            }
            popup.SetActive(false);
            coins.SetOnTop(false);
            popupDim.color = new Color(0.01f, 0.02f, 0.06f, 0.72f);
            popupDone?.Invoke();
        }

        const int CoinLayers = 7;
        const float CoinThickness = 0.24f;   // coin çapına oranla
        static readonly Color CoinEdge = new(0.78f, 0.46f, 0.08f);

        IEnumerator FlyOne(Vector3 start, Vector3 target, float side, float delay, Action arrived)
        {
            yield return new WaitForSecondsRealtime(delay);
            var root = NewRect("Flying Coin", flyLayer);
            root.sizeDelta = new Vector2(120f, 120f);
            var layers = new Image[CoinLayers];
            for (var i = 0; i < CoinLayers; i++)
            {
                var img = NewRect("Layer", root).gameObject.AddComponent<Image>();
                img.sprite = UiArt.CoinIcon();
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = i == CoinLayers - 1 ? Color.white : Color.Lerp(CoinEdge * 0.75f, CoinEdge, i / (float)(CoinLayers - 1));
                Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
                layers[i] = img;
            }
            var scale = canvas.lossyScale.x;
            var control = (start + target) / 2f + new Vector3(side * 380f, 260f, 0f) * scale;
            start += new Vector3(side * 60f, 0f, 0f) * scale;
            var spinOffset = side * Mathf.PI;
            for (var t = 0f; t < CoinFlySeconds; t += Time.unscaledDeltaTime)
            {
                var k = Tween.Evaluate(Tween.Ease.InQuad, t / CoinFlySeconds);
                var a = Vector3.Lerp(start, control, k);
                var b = Vector3.Lerp(control, target, k);
                root.position = Vector3.Lerp(a, b, k);
                root.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.6f, k);
                // Dikey eksende dönme: yüz yatayda daralır, kenar (arka katmanlar) dönüş yönünde yana açılır.
                var angle = spinOffset + t * Mathf.PI * 2f * 2.2f;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                var width = Mathf.Max(0.06f, Mathf.Abs(cos));
                for (var i = 0; i < CoinLayers; i++)
                {
                    var depth = (i - (CoinLayers - 1)) / (float)(CoinLayers - 1);   // −1 arka … 0 ön
                    var rt = layers[i].rectTransform;
                    rt.localScale = new Vector3(width, 1f, 1f);
                    rt.anchoredPosition = new Vector2(depth * CoinThickness * 120f * sin, 0f);
                }
                layers[CoinLayers - 1].color = Color.Lerp(new Color(0.85f, 0.7f, 0.45f), Color.white, Mathf.Abs(cos));
                yield return null;
            }
            Object.Destroy(root.gameObject);
            arrived?.Invoke();
        }

        // Açılış: karartma belirir, panel taşarak büyür; ışık hüzmeleri döner, coin hafifçe nefes alır.
        /// <summary>Her kare: açılış, ışık hüzmeleri, yıldızlar ve roket animasyonu.</summary>
        public void Tick()
        {
            if (!popup || !popup.activeSelf) return;
            popupTime += Time.unscaledDeltaTime;
            if (!claiming)
            {
                var p = Mathf.Clamp01(popupTime / 0.45f);
                const float c1 = 1.70158f, c3 = c1 + 1f;   // ease-out-back: hafif taşıp yerine oturur
                var s = Mathf.LerpUnclamped(0.55f, 1f, 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f));
                popupPanel.localScale = Vector3.one * (s * popupFit);
                popupDim.color = new Color(0.01f, 0.02f, 0.06f, 0.72f * Mathf.Clamp01(popupTime / 0.25f));
            }
            popupRays.localRotation = Quaternion.Euler(0f, 0f, popupTime * 18f);
            popupCoin.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(popupTime * 3.2f));
            popupCoinGlow.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(popupTime * 2.4f));
            if (!popupStars) return;
            // Yıldızlar: açılışta taşarak büyür; arkalarındaki haleler sırayla nabız atar, uçlarında parıltılar yanıp söner.
            var sp = Mathf.Clamp01((popupTime - 0.15f) / 0.5f);
            const float b1 = 1.70158f, b3 = b1 + 1f;
            var pop = sp <= 0f ? 0f : 1f + b3 * Mathf.Pow(sp - 1f, 3f) + b1 * Mathf.Pow(sp - 1f, 2f);
            popupStars.localScale = Vector3.one * (pop * (1f + 0.025f * Mathf.Sin(popupTime * 2.6f)));
            foreach (var (glow, phase) in starGlows)
            {
                var pulse = 0.5f + 0.5f * Mathf.Sin(popupTime * 3.4f + phase * 2.2f);
                glow.localScale = Vector3.one * (pop * (0.9f + 0.25f * pulse));
                glow.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.55f + 0.45f * pulse);
            }
            foreach (var (spark, at, phase) in starTwinkles)
            {
                var k = Mathf.Repeat(popupTime * 0.8f + phase * 0.31f, 1f);
                var flash = k < 0.35f ? Mathf.Sin(k / 0.35f * Mathf.PI) : 0f;
                spark.localScale = Vector3.one * (pop * flash);
                spark.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
            }
            // Roket gezegenin önünde hafifçe süzülür; parıltılar yavaşça nefes alır.
            popupRocket.anchoredPosition = new Vector2(-10f + 8f * Mathf.Sin(popupTime * 1.7f), 14f * Mathf.Sin(popupTime * 2.3f));
            popupRocket.localRotation = Quaternion.Euler(0f, 0f, 3f * Mathf.Sin(popupTime * 1.9f));
            popupSparkles.localScale = Vector3.one * (1f + 0.015f * Mathf.Sin(popupTime * 1.5f));   // nefes alırken çizgileri aşmaz
            popupSparkles.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(popupTime * 2.8f));
        }
    }
}
