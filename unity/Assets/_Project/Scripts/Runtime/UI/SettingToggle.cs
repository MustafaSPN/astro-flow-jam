using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Ayar satırı: ad solda, sağda açık (yeşil) / kapalı (gri) anahtar. Ayar başka bir yerden değişince de güncellenir.
    /// </summary>
    public static class SettingToggle
    {
        public static void Create(RectTransform panel, string label, string key, float y)
        {
            var name = NewText(label, panel, 54, UiArt.Navy);
            // Satır butonlarla aynı genişlikte (±300): yazı sol kenarda, anahtarın sağ kenarı butonların sağ kenarında.
            Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, y), new Vector2(380f, 90f));
            name.alignment = TextAnchor.MiddleLeft;
            name.text = label;
            var onSprite = UiArt.Box(220, 96, 48f, new Color(0.45f, 0.95f, 0.35f), new Color(0.12f, 0.65f, 0.2f), 5f, UiArt.Navy);
            var offSprite = UiArt.Box(220, 96, 48f, new Color(0.55f, 0.6f, 0.72f), new Color(0.3f, 0.34f, 0.45f), 5f, UiArt.Navy);
            var toggle = ImageButton(panel, label + " Toggle", onSprite, new Vector2(0.5f, 0.5f), new Vector2(190f, y), new Vector2(220f, 96f), () =>
            {
                GameSettings.Toggle(key);
                Haptics.Select();
            });
            var state = NewText("State", toggle, 36, UiArt.Navy);
            Place(state.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 60f));
            var knob = Img(toggle, "Knob", UiArt.Circle(80, Color.white, new Color(0.82f, 0.86f, 0.95f), 4f, UiArt.Navy), Vector2.zero, new Vector2(78f, 78f));
            knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
            var image = toggle.GetComponent<Image>();

            void Refresh()
            {
                if (!toggle) { GameSettings.Changed -= Refresh; return; }   // arayüz yok edildi
                var on = GameSettings.IsOn(key);
                image.sprite = on ? onSprite : offSprite;
                knob.anchoredPosition = new Vector2(on ? 62f : -62f, 0f);
                state.text = on ? "ON" : "OFF";
                state.rectTransform.anchoredPosition = new Vector2(on ? -36f : 36f, 2f);
            }
            Refresh();
            GameSettings.Changed += Refresh;
        }
    }
}
