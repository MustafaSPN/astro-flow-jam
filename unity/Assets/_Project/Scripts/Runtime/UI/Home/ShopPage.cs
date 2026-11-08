using System;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Ana ekranın mağaza sayfası: reklam kaldırma, can doldurma, paketler ve coin paketleri. Satın almalar <see cref="Store"/>
    /// üzerinden App Store'a gider; fiyatlar mağazadan gelince ya da bir şey satın alınınca sayfa yeniden kurulur.
    /// </summary>
    public sealed class ShopPage
    {
        readonly MonoBehaviour host;
        readonly Action<string> toast;
        readonly Action refresh;
        readonly RectTransform content;
        float lastScroll = float.NaN;

        public ShopPage(RectTransform page, MonoBehaviour host, Action<string> toast, Action refresh)
        {
            this.host = host;
            this.toast = toast;
            this.refresh = refresh;
            // Koleksiyondaki gibi: kartlar başlığın altına girdikçe kaybolur, başlık gradyanın üstünde çizilir.
            content = ScrollContent(page, Vector2.zero);
            PageFade(page);
            var title = NewText("Title", page, 84, UiArt.Navy);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 110f));
            title.text = "Shop";
            Build();
            Store.Changed += Build;
        }

        // Kartları (yeniden) kurar; kaydırma konumu korunur.
        void Build()
        {
            for (var i = content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(content.GetChild(i).gameObject);
            lastScroll = float.NaN;
            var y = -PageContentTop;
            if (!ShopCatalog.AdsRemoved)
            {
                RemoveAdsCard(content, y - 105f);
                y -= 250f;
            }
            // Can doldurma (900 coin).
            var lives = ShopCard(content, new Vector2(0f, y - 105f), new Vector2(960f, 210f));
            var heart = Img(lives, "Heart", UiArt.Asset("Home/Heart"), new Vector2(-350f, 0f), new Vector2(150f, 130f));
            heart.anchorMin = heart.anchorMax = new Vector2(0.5f, 0.5f);
            heart.GetComponent<Image>().preserveAspect = true;
            var livesTitle = NewText("Title", lives, 52, UiArt.Navy);
            Place(livesTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90f, 30f), new Vector2(380f, 70f));
            livesTitle.text = "Refill Lives";
            var livesBody = NewText("Body", lives, 34, UiArt.Navy);
            Place(livesBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90f, -30f), new Vector2(380f, 50f));
            livesBody.text = $"Get all {Lives.Max} lives now";
            livesBody.color = new Color(0.62f, 0.9f, 1f);
            PriceButton(lives, new Vector2(290f, 0f), new Vector2(300f, 120f), Lives.RefillCost.ToString(), true, () =>
            {
                if (Lives.IsFull) { toast("Your lives are already full"); return; }
                if (!Lives.TryRefill()) { toast("Not enough coins"); return; }
                Telemetry.CoinsSpent("refill_lives", Lives.RefillCost);
                Haptics.GateExit(host);
                toast("Lives refilled!");
                refresh();
            });
            y -= 250f;
            // Paketler.
            y = ShopHeader(content, "Bundles", y);
            foreach (var bundle in ShopCatalog.Bundles)
            {
                if (bundle.oneTime && ShopCatalog.StarterBought) continue;
                BundleCard(content, bundle, y - 150f);
                y -= 340f;
            }
            // Coin paketleri.
            y = ShopHeader(content, "Coins", y - 10f);
            for (var i = 0; i < ShopCatalog.CoinPacks.Length; i++)
            {
                var pack = ShopCatalog.CoinPacks[i];
                var col = i % 2;
                var row = i / 2;
                var card = ShopCard(content, new Vector2(col == 0 ? -245f : 245f, y - row * 440f - 200f), new Vector2(470f, 410f));
                var icon = Img(card, "Coin", UiArt.CoinIcon(), new Vector2(0f, 62f), Vector2.one * (120f + 12f * Mathf.Min(i, 5)));
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.GetComponent<Image>().preserveAspect = true;
                var amount = NewText("Amount", card, 60, UiArt.Navy);
                Place(amount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(440f, 80f));
                amount.text = pack.coins.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
                amount.color = TitleYellow;
                PriceButton(card, new Vector2(0f, -145f), new Vector2(330f, 110f), Store.Price(pack.id, pack.price), false, () => Buy(pack.id));
                if (pack.tag != null) ShopTag(card, pack.tag);
            }
            content.sizeDelta = new Vector2(0f, -y + 3 * 440f + 60f);
        }

        /// <summary>Her kare: kaydırma değiştiyse kartlar başlığın altında soldurulur.</summary>
        public void Tick()
        {
            var scroll = content.anchoredPosition.y;
            if (scroll == lastScroll) return;                  // kaydırma durdu
            lastScroll = scroll;
            FadeUnderTitle(content, scroll);
        }

        static float ShopHeader(RectTransform content, string text, float y)
        {
            var header = NewText("Header", content, 58, UiArt.Navy);
            Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 10f), new Vector2(900f, 80f));
            header.text = text;
            return y - 100f;
        }

        // Paket kartı: solda ad, coin ve jokerler; sağda sınırsız can ve fiyat.
        void BundleCard(RectTransform content, ShopCatalog.Bundle bundle, float centerY)
        {
            var card = ShopCard(content, new Vector2(0f, centerY), new Vector2(960f, 300f));
            var name = NewText("Name", card, 52, UiArt.Navy);
            Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-200f, 95f), new Vector2(500f, 70f));
            name.text = bundle.name;
            var coin = Img(card, "Coin", UiArt.CoinIcon(), new Vector2(-390f, 5f), new Vector2(92f, 92f));
            coin.anchorMin = coin.anchorMax = new Vector2(0.5f, 0.5f);
            coin.GetComponent<Image>().preserveAspect = true;
            var coins = NewText("Coins", card, 58, UiArt.Navy);
            Place(coins.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-170f, 5f), new Vector2(300f, 80f));
            coins.text = bundle.coins.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            coins.color = TitleYellow;
            for (var i = 0; i < GameHud.BoosterIcons.Length; i++)
            {
                var booster = Img(card, GameHud.BoosterNames[i], UiArt.Asset("Boosters/" + GameHud.BoosterIcons[i]), new Vector2(-410f + i * 92f, -90f), new Vector2(84f, 84f));
                booster.anchorMin = booster.anchorMax = new Vector2(0.5f, 0.5f);
                booster.GetComponent<Image>().preserveAspect = true;
            }
            var each = NewText("Each", card, 48, UiArt.Navy);
            Place(each.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-10f, -90f), new Vector2(120f, 70f));
            each.text = $"x{bundle.boostersEach}";
            var heart = Img(card, "Heart", UiArt.Asset("Home/Heart"), new Vector2(215f, 55f), new Vector2(90f, 80f));
            heart.anchorMin = heart.anchorMax = new Vector2(0.5f, 0.5f);
            heart.GetComponent<Image>().preserveAspect = true;
            InfinityMark(heart, 66f);                                // süreli sınırsız can
            var hours = NewText("Hours", card, 48, UiArt.Navy);
            Place(hours.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(320f, 55f), new Vector2(130f, 70f));
            hours.text = $"{bundle.infiniteHours}h";
            hours.color = new Color(0.62f, 0.9f, 1f);
            var id = bundle.id;
            PriceButton(card, new Vector2(285f, -75f), new Vector2(310f, 110f), Store.Price(id, bundle.price), false, () => Buy(id));   // tek seferlik paket alınınca kart kalkar
            if (bundle.tag != null) ShopTag(card, bundle.tag);
        }

        // Satın alma: sonuç mesajı gösterilir (iptalde mesaj yok); başarılıysa üst bar yenilenir (sayfayı Store.Changed kurar).
        void Buy(string productId) => Store.Buy(productId, (success, message) =>
        {
            if (message != null) toast(message);
            if (!success) return;
            Haptics.GateExit(host);
            refresh();
        });

        // Reklam kaldırma kartı: solda üstü çizili "AD" simgesi, ortada başlık ve açıklama, sağda fiyat.
        void RemoveAdsCard(RectTransform content, float centerY)
        {
            var card = ShopCard(content, new Vector2(0f, centerY), new Vector2(960f, 210f));
            var badge = Img(card, "No Ads", UiArt.Box(150, 110, 26f, Color.white, new Color(0.8f, 0.86f, 0.95f), 6f, UiArt.Navy), new Vector2(-350f, 0f), new Vector2(150f, 110f));
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0.5f);
            var ad = NewText("AD", badge, 64, Color.white);
            Place(ad.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero);
            ad.text = "AD";
            ad.color = UiArt.Navy;
            var slash = Img(badge, "Slash", UiArt.Box(170, 24, 12f, new Color(1f, 0.35f, 0.38f), new Color(0.82f, 0.12f, 0.2f), 4f, UiArt.Navy), Vector2.zero, new Vector2(170f, 24f));
            slash.anchorMin = slash.anchorMax = new Vector2(0.5f, 0.5f);
            slash.localRotation = Quaternion.Euler(0f, 0f, 35f);
            var title = NewText("Title", card, 52, UiArt.Navy);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90f, 30f), new Vector2(380f, 70f));
            title.text = "Remove Ads";
            var body = NewText("Body", card, 34, UiArt.Navy);
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90f, -30f), new Vector2(380f, 50f));
            body.text = "No ads between levels";
            body.color = new Color(0.62f, 0.9f, 1f);
            PriceButton(card, new Vector2(290f, 0f), new Vector2(300f, 120f), Store.Price(ShopCatalog.RemoveAdsId, ShopCatalog.RemoveAdsPrice), false,
                () => Buy(ShopCatalog.RemoveAdsId));
        }

        // Etiket: kartın sol üst köşesine taşan kırmızı hap.
        static void ShopTag(RectTransform card, string tag)
        {
            var pill = Img(card, "Tag", UiArt.Box(240, 64, 32f, new Color(1f, 0.42f, 0.45f), new Color(0.82f, 0.14f, 0.22f), 4f, UiArt.Navy), Vector2.zero, new Vector2(240f, 64f));
            pill.anchorMin = pill.anchorMax = new Vector2(0f, 1f);
            pill.anchoredPosition = new Vector2(110f, -6f);
            pill.localRotation = Quaternion.Euler(0f, 0f, 8f);
            var t = NewText("Text", pill, 34, UiArt.Navy);
            Place(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero);
            t.text = tag;
        }


        static RectTransform PriceButton(RectTransform parent, Vector2 position, Vector2 size, string text, bool coin, Action onClick)
        {
            var button = SlicedButton(parent, "Buy", GreenButton, position, size, onClick);
            var label = NewText("Label", button, 50, UiArt.Navy);
            Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(coin ? 32f : 0f, 4f), Vector2.zero);
            label.text = text;
            if (coin)
            {
                var icon = Img(button, "Coin", UiArt.CoinIcon(), new Vector2(-size.x / 2f + 62f, 2f), new Vector2(70f, 70f));
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            }
            return button;
        }
    }
}
