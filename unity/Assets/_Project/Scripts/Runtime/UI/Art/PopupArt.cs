using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>Pencerelerin ortak görselleri (Resources/UI/Popup): 9 dilimli panel ve butonlar, renkler, ödül rozeti.</summary>
    public static class PopupArt
    {
        public static readonly Color TitleYellow = new(1f, 0.82f, 0.2f);
        public static readonly Color Gold = new(1f, 0.66f, 0.04f);   // koyu altın (joker tanıtımı üst başlığı)
        public static Sprite PopupPanel => UiArt.Asset("Popup/panel", new Vector4(160f, 160f, 160f, 160f));
        public static Sprite GreenButton => UiArt.Asset("Popup/green_button", new Vector4(140f, 140f, 140f, 140f));
        public static Sprite RedButton => UiArt.Asset("Popup/red_button", new Vector4(140f, 140f, 140f, 140f));
        public static Sprite DefaultButton => UiArt.Asset("Popup/default_button", new Vector4(170f, 170f, 170f, 170f));

        /// <summary>
        /// Ödül rozeti: koyu lacivert hap, mavi neon kenar (kodla çizilir, istenen her en boyda). Üstünde coin simgesi ve
        /// miktar.
        /// </summary>
        public static RectTransform RewardBadge(RectTransform parent, Vector2 position, Vector2 size)
        {
            var w = Mathf.RoundToInt(size.x); var h = Mathf.RoundToInt(size.y);
            var glow = Img(parent, "Reward Badge Glow", UiArt.Glow(w + 70, h + 70, h / 2f, 35f, new Color(0.2f, 0.55f, 1f, 0.25f)), position, new Vector2(w + 70, h + 70));
            glow.GetComponent<Image>().preserveAspect = false;
            var badge = Img(parent, "Reward Badge", UiArt.Glass(w, h, h / 2f, new Color(0.07f, 0.14f, 0.38f), new Color(0.03f, 0.06f, 0.2f), new Color(0.2f, 0.55f, 1f), 7f), position, size);
            badge.GetComponent<Image>().preserveAspect = false;
            return badge;
        }
    }
}
