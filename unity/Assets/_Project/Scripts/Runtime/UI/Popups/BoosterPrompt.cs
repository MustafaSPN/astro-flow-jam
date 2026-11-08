using System;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Joker hedef seçimi paneli (UFO: renk, Warp: blok): ortak popup paneli, solda jokerin simgesi, ortada başlık ve
    /// açıklama, sağ üst köşede kapatma butonu (jokeri iptal eder). Ekranın üstünde, üst barın yerinde durur; karartma yoktur,
    /// tahta ve resim dokunulabilir kalır (yalnız panel dokunmayı tutar).
    /// </summary>
    public sealed class BoosterPrompt
    {
        const int Layer125 = 125;                                     // HUD, coin göstergesi ve duraklatmanın üstünde
        const float Width = 700f, Height = 210f, IconSize = 136f, Top = 0f;
        const float TextX = (Height - 20f) / 2f - 30f;               // simgenin sağında kalan alanın ortası, kapatma butonundan uzak

        readonly RectTransform parent;
        GameObject root, closeButton;
        Image icon;
        Text title, text;
        Action onClose;
        float time = -1f;

        public BoosterPrompt(RectTransform parent) => this.parent = parent;

        /// <summary>Paneli açar; kapatma butonu onClose'u çağırır (panel çağıran tarafından kapanır).</summary>
        public void Show(Sprite boosterIcon, string titleText, string body, Action close)
        {
            if (!root) Build();
            icon.sprite = boosterIcon;
            title.text = titleText;
            text.text = body;
            onClose = close;
            closeButton.SetActive(close != null);                     // joker tanıtımında seçim iptal edilemez
            root.SetActive(true);
            time = 0f;
        }

        public void Hide()
        {
            if (root) root.SetActive(false);
            time = -1f;
        }

        /// <summary>Her kare: panel yukarıdan kayarak gelir ve hafifçe taşar; simge nabız atar.</summary>
        public void Tick()
        {
            if (time < 0f || !root) return;
            time += Time.unscaledDeltaTime;
            var p = Mathf.Clamp01(time / 0.35f);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            var e = 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
            ((RectTransform)root.transform).anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(Height + 40f, Top, e));
            icon.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(time * 4f));
        }

        void Build()
        {
            var rect = NewRect("Booster Prompt", parent);
            Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, Top), new Vector2(Width, Height));
            rect.pivot = new Vector2(0.5f, 1f);
            Layer(rect.gameObject, Layer125, true);
            var frame = Sliced(rect, "Frame", PopupPanel, Vector2.zero, new Vector2(Width, Height), 1100f * Height / 520f);
            frame.GetComponent<Image>().raycastTarget = true;          // panelin altındaki dokunmalar geçmez
            // Simge panelin sol tarafında, iç çerçevenin yukarı-aşağı ve soldan tam ortasında (kenarlardan eşit pay).
            var iconX = -Width / 2f + Height / 2f;
            icon = Img(rect, "Icon", null, new Vector2(iconX, -8f), new Vector2(IconSize, IconSize)).GetComponent<Image>();
            title = NewText("Title", rect, 54, UiArt.Navy);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(TextX, 32f), new Vector2(440f, 70f));
            text = NewText("Text", rect, 40, UiArt.Navy);
            Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(TextX, -34f), new Vector2(440f, 60f));
            text.color = new Color(0.55f, 0.88f, 1f);
            // Kapatma: panelin sağ üst köşesine taşan kırmızı yuvarlak buton, beyaz çarpı.
            var close = ImageButton(rect, "Close", UiArt.Box(80, 80, 40f, new Color(1f, 0.4f, 0.42f), new Color(0.82f, 0.14f, 0.2f), 5f, UiArt.Navy),
                new Vector2(1f, 1f), new Vector2(-48f, -48f), new Vector2(80f, 80f), () => onClose?.Invoke());   // iç çerçevenin köşesinde
            closeButton = close.gameObject;
            var x = Img(close, "X", UiArt.Icon("close"), Vector2.zero, new Vector2(62f, 62f));
            x.anchorMin = x.anchorMax = new Vector2(0.5f, 0.5f);
            root = rect.gameObject;
            root.SetActive(false);
        }
    }
}
