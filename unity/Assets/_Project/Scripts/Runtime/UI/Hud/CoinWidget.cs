using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Üst bardaki coin göstergesi: haleli cam hap, coin simgesi, miktar ve "+". Bölüm sonunda uçan coinler buraya iner;
    /// o sırada gösterge karartmanın üstüne çıkar.
    /// </summary>
    public sealed class CoinWidget
    {
        /// <summary>Bölüm sonu ve coin pencerelerinin karartmasının üstü.</summary>
        public const int TopLayer = 120;

        readonly Text amount;
        readonly RectTransform icon, glow;

        public RectTransform Pill { get; }
        public int Shown { get; private set; }
        public Vector3 IconPosition => icon.position;

        public CoinWidget(RectTransform bar, Vector2 center, float width, float height)
        {
            var glowImage = NewRect("Coins Glow", bar).gameObject.AddComponent<Image>();
            glow = glowImage.rectTransform;
            glowImage.sprite = UiArt.Glow(Mathf.RoundToInt(width + 44f), Mathf.RoundToInt(height + 44f), height / 2f, 22f,
                new Color(UiArt.Cyan.r, UiArt.Cyan.g, UiArt.Cyan.b, 0.2f));
            glowImage.raycastTarget = false;
            Place(glow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), center, new Vector2(width + 44f, height + 44f));
            glow.pivot = new Vector2(0.5f, 0.5f);
            glow.anchoredPosition = center;

            Pill = NewRect("Coins", bar);
            Place(Pill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), center, new Vector2(width, height));
            Pill.pivot = new Vector2(0.5f, 0.5f);
            Pill.anchoredPosition = center;
            var body = Pill.gameObject.AddComponent<Image>();
            body.sprite = UiArt.Glass(Mathf.RoundToInt(width), Mathf.RoundToInt(height), height / 2f,
                Color.Lerp(ButtonTop, ButtonBottom, 0.35f), ButtonBottom, UiArt.Cyan, 4f);
            // Göstergenin tamamı tek buton: neresine dokunulursa bütün gösterge basılma tepkisi verir (şimdilik işlevsiz).
            var button = Pill.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            button.onClick.AddListener(() => { });
            Pill.gameObject.AddComponent<ButtonPunch>();

            var coin = NewRect("Coin", Pill).gameObject.AddComponent<Image>();
            icon = coin.rectTransform;
            coin.sprite = UiArt.CoinIcon();
            coin.raycastTarget = false;
            coin.preserveAspect = true;
            Place(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(height * 0.55f, 0f), new Vector2(height * 0.92f, height * 0.92f));

            amount = NewText("Amount", Pill, 42, UiArt.Navy);
            Place(amount.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-height * 1.9f, 0f));
            Set(CoinWallet.Coins);

            // "+" yalnızca görsel; dokunuş göstergenin kendisine gider.
            var plus = ThemeButton(Pill, "Add Coins", new Vector2(-height * 0.42f, 0f), height * 0.66f, () => { });
            Object.Destroy(plus.GetComponent<ButtonPunch>());
            Object.Destroy(plus.GetComponent<Button>());
            plus.GetComponent<Image>().raycastTarget = false;
            AddNeonIcon(plus, "plus", height * 0.52f);
        }

        public void Set(int value)
        {
            Shown = value;
            amount.text = CoinWallet.Short(value);
        }

        /// <summary>Gösterge karartmanın üstüne çıkar ya da yerine döner; hale göstergenin bir kat altında kalır.</summary>
        public void SetOnTop(bool on)
        {
            const int layer = TopLayer;
            foreach (var rect in new[] { glow, Pill })
            {
                if (on) Layer(rect.gameObject, rect == Pill ? layer : layer - 1, rect == Pill);
                else if (rect.TryGetComponent<Canvas>(out var canvas)) canvas.overrideSorting = false;
            }
        }
    }
}
