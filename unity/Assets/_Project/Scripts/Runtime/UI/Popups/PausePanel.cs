using System;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Duraklatma paneli: ayarlar penceresinin tasarımı (aynı panel, başlık boşluğu, ses/titreşim anahtarları), altında
    /// Restart ve Home. Resume butonu yok: karartmaya dokunmak oyuna döner.
    /// </summary>
    public sealed class PausePanel
    {
        const float Width = 860f, Height = 1108f;

        readonly GameObject root;

        public PausePanel(Transform parent, int layer, Action onResume, Action onRestart, Action onHome)
        {
            root = NewRect("Pause Panel", parent).gameObject;
            var rect = (RectTransform)root.transform;
            Stretch(rect);
            Layer(root, layer, true);
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.02f, 0.06f, 0.75f);
            var dimButton = root.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => onResume());
            var panel = NewRect("Panel", rect);
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height));
            var frame = Sliced(panel, "Frame", PopupPanel, Vector2.zero, new Vector2(Width, Height), 1100f * Height / 1300f);
            frame.GetComponent<Image>().raycastTarget = true;            // panel dokunmayı tutar, karartma oyuna döndürür
            frame.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var hint = NewText("Tap Hint", rect, 38, UiArt.Navy);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -Height / 2f - 60f), new Vector2(800f, 60f));
            hint.text = "Tap to resume";
            hint.color = new Color(0.62f, 0.9f, 1f);
            var title = NewText("Title", panel, 80, UiArt.Navy);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, Height / 2f - 215f), new Vector2(700f, 110f));
            title.text = "Paused";
            var y = Height / 2f - 360f;
            foreach (var (label, key) in new[] { ("Sound", SaveKeys.Sound), ("Vibration", SaveKeys.Vibration) })
            {
                SettingToggle.Create(panel, label, key, y);
                y -= 130f;
            }
            y -= 60f;                                                     // anahtarlar ile butonlar arası
            foreach (var (label, action) in new (string, Action)[] { ("Restart", onRestart), ("Home", onHome) })
            {
                var b = SlicedButton(panel, label, DefaultButton, new Vector2(0f, y), new Vector2(600f, 180f), () => action());
                var l = NewText("Label", b, 52, UiArt.Navy);
                Place(l.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero);
                l.text = label;
                y -= 192f;
            }
            root.SetActive(false);
        }

        public void SetVisible(bool on) => root.SetActive(on);
    }
}
