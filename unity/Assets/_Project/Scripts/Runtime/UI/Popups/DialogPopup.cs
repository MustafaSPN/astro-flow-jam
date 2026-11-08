using System;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Ortak diyalog (joker satın alma, kaybedince devam, can, onaylar): ortak 9 dilimli popup paneli, başlık, büyük simge,
    /// açıklama, coin fiyatlı yeşil ana buton, varsayılan ikinci buton ve sağ üstte kapatma. Arkada karartma; coin göstergesi
    /// karartmanın üstünde kalır (oyuncu bakiyesini görür). Tek örnek kurulur, her açılışta içerik değişir.
    /// </summary>
    public sealed class DialogPopup
    {
        readonly RectTransform canvas;
        readonly MonoBehaviour host;
        readonly CoinWidget coins;

        public DialogPopup(RectTransform canvas, MonoBehaviour host, CoinWidget coins)
        {
            this.canvas = canvas;
            this.host = host;
            this.coins = coins;
        }

        public bool Visible => dialog && dialog.activeSelf;

        /// <summary>Açık diyalog bu başlığı taşıyorsa açıklamasını günceller (ör. can sayacı).</summary>
        public bool TryRefresh(string titlePart, string body, string iconText)
        {
            if (!Visible || !dialogTitle.text.Contains(titlePart)) return false;
            dialogBody.text = body;
            dialogIconText.text = iconText;
            return true;
        }

        GameObject dialog;
        Image dialogDim, dialogIcon, dialogCoin, dialogSecondaryIcon;
        RectTransform dialogIconGlow, dialogFrame;
        RectTransform dialogPanel, dialogPrimary, dialogSecondary, dialogClose;
        Text dialogTapHint, dialogIconText, dialogTitle, dialogSubtitle, dialogBody, dialogCaption, dialogPrimaryLabel, dialogPrice, dialogSecondaryLabel;
        Action dialogOnPrimary, dialogOnSecondary, dialogOnClose;
        float dialogTime = -1f;
        bool dialogTapToClose;
        Color dialogPriceColor;
        public const int DefaultLayer = 112;
        const float DialogWidth = 820f, DialogHeight = 1000f;

        void BuildDialog()
        {
            dialog = NewRect("Dialog", canvas).gameObject;
            var root = (RectTransform)dialog.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            Layer(dialog, DefaultLayer, true);
            dialogDim = dialog.AddComponent<Image>();
            dialogDim.color = new Color(0.01f, 0.02f, 0.06f, 0.72f);

            dialogPanel = NewRect("Panel", root);
            Place(dialogPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(DialogWidth, DialogHeight));
            dialogFrame = Sliced(dialogPanel, "Frame", PopupPanel, Vector2.zero, new Vector2(DialogWidth, DialogHeight), 1100f * DialogHeight / 1300f);
            // "Tap to close": karartmaya dokunmak kapatır; panelin kendisi dokunmayı tutar (karartmaya geçmez).
            dialogFrame.GetComponent<Image>().raycastTarget = true;
            var block = dialogFrame.gameObject.AddComponent<Button>();
            block.transition = Selectable.Transition.None;
            var dimButton = dialog.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => { if (dialogTapToClose && dialogTime > 0.3f) dialogOnClose?.Invoke(); });
            dialogTapHint = NewText("Tap Hint", root, 38, UiArt.Navy);
            dialogTapHint.rectTransform.anchorMin = dialogTapHint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dialogTapHint.rectTransform.sizeDelta = new Vector2(800f, 60f);
            dialogTapHint.color = new Color(0.62f, 0.9f, 1f);
            dialogTapHint.text = "Tap to close";

            dialogTitle = NewText("Title", dialogPanel, 72, UiArt.Navy);
            Place(dialogTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 345f), new Vector2(680f, 110f));
            // Üst başlık (kayıpta "You Lose!"): başlığın üstünde, büyük, kırmızı, kalın beyaz kenarlı.
            dialogSubtitle = NewText("Subtitle", dialogPanel, 100, Color.white);
            Place(dialogSubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 372f), new Vector2(740f, 130f));
            dialogSubtitle.color = new Color(0.95f, 0.16f, 0.2f);
            var subtitleEdge = dialogSubtitle.GetComponent<ThickOutline>();
            subtitleEdge.distance = 7f;
            subtitleEdge.shadow = new Vector2(0f, -6f);
            dialogIconGlow = Img(dialogPanel, "Icon Glow", UiArt.Glow(380, 380, 190f, 110f, new Color(0.35f, 0.75f, 1f, 0.35f)), new Vector2(0f, 120f), new Vector2(380f, 380f));
            dialogIcon = Img(dialogPanel, "Icon", null, new Vector2(0f, 120f), new Vector2(250f, 250f)).GetComponent<Image>();
            // Simgenin üstünde yazı (can penceresinde kalbin içinde can sayısı); varsayılan gizli.
            dialogIconText = NewText("Icon Text", dialogIcon.rectTransform, 110, UiArt.Navy);
            Place(dialogIconText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 8f), Vector2.zero);
            dialogBody = NewText("Body", dialogPanel, 44, UiArt.Navy);
            Place(dialogBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(640f, 130f));
            dialogBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogBody.color = new Color(0.62f, 0.9f, 1f);

            // Ana butonun üstündeki kısa yazı (satın almada "Buy x3"): butonda yalnız coin ve fiyat kalır.
            dialogCaption = NewText("Caption", dialogPanel, 54, UiArt.Navy);
            Place(dialogCaption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(640f, 80f));
            dialogCaption.color = TitleYellow;

            // Ana buton: etiket + coin + fiyat (fiyat yoksa yalnız ortalanmış etiket).
            dialogPrimary = SlicedButton(dialogPanel, "Primary", GreenButton, new Vector2(0f, -255f), new Vector2(620f, 140f), () => dialogOnPrimary?.Invoke());
            dialogPrimaryLabel = NewText("Label", dialogPrimary, 62, UiArt.Navy);
            Place(dialogPrimaryLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-105f, 6f), new Vector2(360f, 110f));
            dialogCoin = Img(dialogPrimary, "Coin", UiArt.CoinIcon(), new Vector2(98f, 6f), new Vector2(70f, 70f)).GetComponent<Image>();
            dialogCoin.rectTransform.anchorMin = dialogCoin.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dialogPrice = NewText("Price", dialogPrimary, 56, UiArt.Navy);
            Place(dialogPrice.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190f, 6f), new Vector2(150f, 110f));
            dialogPrice.color = TitleYellow;
            // Fiyat coinin hemen sağından başlar (sola yaslı): dört basamak da coinin üstüne binmez.
            dialogPrice.alignment = TextAnchor.MiddleLeft;
            dialogPrice.rectTransform.pivot = new Vector2(0f, 0.5f);

            dialogSecondary = SlicedButton(dialogPanel, "Secondary", DefaultButton, new Vector2(0f, -395f), new Vector2(440f, 116f), () => dialogOnSecondary?.Invoke());
            dialogSecondaryLabel = NewText("Label", dialogSecondary, 52, UiArt.Navy);
            Place(dialogSecondaryLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero);
            dialogSecondaryIcon = Img(dialogSecondary, "Icon", null, new Vector2(-150f, 4f), new Vector2(84f, 72f)).GetComponent<Image>();

            var close = dialogClose = ImageButton(dialogPanel, "Close", UiArt.Box(92, 92, 46f, new Color(1f, 0.4f, 0.42f), new Color(0.82f, 0.14f, 0.2f), 5f, UiArt.Navy),
                new Vector2(0.5f, 0.5f), new Vector2(DialogWidth / 2f - 70f, DialogHeight / 2f - 70f), new Vector2(92f, 92f), () => dialogOnClose?.Invoke());
            var x = Img(close, "X", UiArt.Icon("close"), Vector2.zero, new Vector2(70f, 70f));
            x.anchorMin = x.anchorMax = new Vector2(0.5f, 0.5f);
            dialog.SetActive(false);
        }

        /// <summary>
        /// Diyaloğu açar. price &lt; 0 ise ana butonda yalnız etiket (ör. "Free"); secondary null ise ikinci buton gizli.
        /// Kapatma butonu onClose'u çağırır. Pencereyi kapatmak çağıranın işi (HideDialog).
        /// </summary>
        public void Show(int layer, string title, Sprite icon, string body, string primary, int price, string secondary,
            Action onPrimary, Action onSecondary, Action onClose, float iconSize, string subtitle, bool secondaryRed,
            string caption, Color? subtitleColor, Sprite secondaryIcon, bool tapToClose, float extraBottom)
        {
            if (!dialog) BuildDialog();
            Layer(dialog, layer, true);
            dialogTitle.text = title;
            // Alt başlık varsa başlık aşağı kayar; kapatma butonu yalnız onClose verilince görünür.
            dialogSubtitle.gameObject.SetActive(subtitle != null);
            dialogSubtitle.text = subtitle ?? "";
            dialogSubtitle.color = subtitleColor ?? new Color(0.95f, 0.16f, 0.2f);
            dialogSubtitle.fontSize = subtitle != null && subtitle.Length > 10 ? 86 : 100;   // uzun üst başlık panele sığsın
            // Üst başlıklı (kayıp) diyalog daha uzun: panel her iki yönde uzar, içerik ferah dağılır.
            var tall = subtitle != null;
            // Üst başlıklı ama ikinci butonsuz (joker tanıtımı): panel daha az uzar, içerik ortalanmak için biraz aşağı iner.
            var single = tall && secondary == null;
            var dy = single ? -45f : 0f;
            // extraBottom: panel yalnız aşağı doğru uzar (üst boşluk aynı, alttaki butonla kenar arası açılır).
            dialogFrame.sizeDelta = new Vector2(DialogWidth, (tall ? DialogHeight + (single ? 130f : 260f) : DialogHeight) + extraBottom);
            dialogFrame.anchoredPosition = new Vector2(0f, -extraBottom / 2f);
            dialogSubtitle.rectTransform.anchoredPosition = new Vector2(0f, 408f + dy);
            dialogTitle.rectTransform.anchoredPosition = new Vector2(0f, tall ? 295f + dy : 345f);
            dialogIcon.rectTransform.anchoredPosition = dialogIconGlow.anchoredPosition = new Vector2(0f, tall ? 100f + dy : caption != null && secondary != null ? 165f : 120f);
            var both = !tall && caption != null && secondary != null;   // açıklama + üst yazı + iki buton: içerik yukarı toplanır
            dialogBody.rectTransform.anchoredPosition = new Vector2(0f, tall ? -100f + dy : both ? 20f : caption != null ? -60f : -80f);
            dialogPrimary.anchoredPosition = new Vector2(0f, tall ? (single ? -330f : -305f) + dy : both ? -215f : caption != null ? -285f : -255f);
            dialogCaption.gameObject.SetActive(caption != null);
            dialogCaption.text = caption ?? "";
            dialogCaption.rectTransform.anchoredPosition = new Vector2(0f, tall ? (single ? -212f : -195f) + dy : both ? -95f : -170f);
            dialogSecondary.anchoredPosition = new Vector2(0f, tall ? -480f : both ? -375f : -395f);
            // Dışarı dokunarak kapanan diyalogda X yok; altta "Tap to close" yazar.
            dialogTapToClose = tapToClose && onClose != null;
            dialogClose.gameObject.SetActive(onClose != null && !dialogTapToClose);
            dialogTapHint.gameObject.SetActive(dialogTapToClose);
            dialogTapHint.rectTransform.anchoredPosition = new Vector2(0f, dialogPanel.anchoredPosition.y + dialogFrame.anchoredPosition.y - dialogFrame.sizeDelta.y / 2f - 60f);
            dialogSecondary.GetComponent<Image>().sprite = secondaryRed ? RedButton : DefaultButton;
            dialogIcon.sprite = icon;
            dialogIconText.gameObject.SetActive(false);
            dialogIcon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            dialogIcon.enabled = icon;
            dialogBody.text = body;
            dialogPrimaryLabel.text = primary;
            var priced = price >= 0;
            dialogCoin.gameObject.SetActive(priced);
            dialogPrice.gameObject.SetActive(priced);
            dialogPrice.text = priced ? price.ToString() : "";
            // Bakiye yetmiyorsa fiyat kırmızı.
            dialogPriceColor = priced && CoinWallet.Coins < price ? new Color(1f, 0.3f, 0.32f) : TitleYellow;
            dialogPrice.color = dialogPriceColor;
            dialogPrimaryLabel.rectTransform.anchoredPosition = new Vector2(priced ? -115f : 0f, 6f);
            // Etiketsiz fiyatlı buton: coin ve fiyat ortalanır, fiyat büyür.
            var priceOnly = priced && string.IsNullOrEmpty(primary);
            dialogCoin.rectTransform.anchoredPosition = new Vector2(priceOnly ? -85f : 88f, 0f);   // rakamların görünen ortasıyla aynı hizada (ölçüldü)
            dialogCoin.rectTransform.sizeDelta = Vector2.one * (priceOnly ? 86f : 70f);
            dialogPrice.rectTransform.anchoredPosition = new Vector2(priceOnly ? -32f : 133f, 6f);
            dialogPrice.rectTransform.sizeDelta = new Vector2(220f, 110f);
            dialogPrice.fontSize = priceOnly ? 68 : 56;
            dialogSecondary.gameObject.SetActive(secondary != null);
            if (secondary != null) dialogSecondaryLabel.text = secondary;
            // İkinci butonda simge (ör. reklam): etiket sağa kayar, buton genişler.
            dialogSecondaryIcon.sprite = secondaryIcon;
            dialogSecondaryIcon.gameObject.SetActive(secondaryIcon);
            // İki büyük buton (can penceresi): ikinci buton ana butonla aynı boyda.
            dialogSecondary.sizeDelta = both ? new Vector2(620f, 140f) : new Vector2(secondaryIcon ? 540f : 440f, 116f);
            dialogSecondary.GetComponent<Image>().pixelsPerUnitMultiplier = dialogSecondary.GetComponent<Image>().sprite.rect.height / dialogSecondary.sizeDelta.y;
            dialogSecondaryLabel.fontSize = both ? 60 : 52;
            dialogSecondaryIcon.rectTransform.anchoredPosition = new Vector2(both ? -220f : -190f, 4f);
            dialogSecondaryIcon.rectTransform.sizeDelta = both ? new Vector2(100f, 85f) : new Vector2(84f, 72f);
            dialogSecondaryLabel.rectTransform.anchoredPosition = new Vector2(secondaryIcon ? 40f : 0f, 4f);
            dialogOnPrimary = onPrimary;
            dialogOnSecondary = onSecondary;
            dialogOnClose = onClose;
            dialog.SetActive(true);
            coins.SetOnTop(true);
            coins.Set(CoinWallet.Coins);
            dialogTime = 0f;
        }

        /// <summary>Açık diyaloğun simgesinin üstüne yazı (ör. kalbin içinde can sayısı).</summary>
        public void SetIconText(string text)
        {
            if (!dialog) return;
            dialogIconText.text = text;
            dialogIconText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void Hide()
        {
            if (!dialog) return;
            dialog.SetActive(false);
            coins.SetOnTop(false);
            coins.Set(CoinWallet.Coins);
            dialogTime = -1f;
        }

        /// <summary>Yetersiz coin: ana buton sarsılır, fiyat kırmızı yanıp söner.</summary>
        public void Denied()
        {
            if (dialogPrimary) host.StartCoroutine(Shake(dialogPrimary));
            Haptics.Deselect();
        }

        System.Collections.IEnumerator Shake(RectTransform target)
        {
            var basePos = target.anchoredPosition;
            for (var t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                target.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 70f) * 14f * (1f - t / 0.35f), 0f);
                if (dialogPrice) dialogPrice.color = Color.Lerp(dialogPriceColor, new Color(1f, 0.3f, 0.3f), Mathf.Sin(t / 0.35f * Mathf.PI));
                yield return null;
            }
            target.anchoredPosition = basePos;
            if (dialogPrice) dialogPrice.color = dialogPriceColor;
        }

        // Açılış: karartma belirir, panel taşarak büyür; simge hafifçe nabız atar.
        /// <summary>Her kare: açılış animasyonu ve simge nabzı.</summary>
        public void Tick()
        {
            if (dialogTime < 0f || !dialog || !dialog.activeSelf) return;
            dialogTime += Time.unscaledDeltaTime;
            var p = Mathf.Clamp01(dialogTime / 0.4f);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            var s = Mathf.LerpUnclamped(0.6f, 1f, 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f));
            dialogPanel.localScale = Vector3.one * s;
            dialogDim.color = new Color(0.01f, 0.02f, 0.06f, 0.72f * Mathf.Clamp01(dialogTime / 0.2f));
            dialogIcon.rectTransform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(dialogTime * 3.2f));
        }
    }
}
