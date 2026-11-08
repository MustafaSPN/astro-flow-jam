using System;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Ana ekranın ayarlar penceresi: ses ve titreşim anahtarları, bilgi
    /// butonları, hesabı silme ve sürüm. Karartmaya dokununca kapanır.
    /// </summary>
    public sealed class SettingsPanel
    {
        const string SupportMail = "mailto:mustafa@sepen.dev?subject=Astro%20Flow%20Jam%20Support";   // Support: mail uygulaması açılır

        const float Width = 860f, BaseHeight = 1595f, RowStep = 192f;
        const string PrivacyUrl = "https://sepen.dev/astro-flow-jam/privacy/";
        const string TermsUrl = "https://sepen.dev/astro-flow-jam/terms/";

        readonly MonoBehaviour host;
        readonly GameObject root;

        public SettingsPanel(Transform parent, int layer, MonoBehaviour host, Action onDeleteAccount, Action<string> toast)
        {
            this.host = host;
            // AB/Birleşik Krallık oyuncuları onay tercihlerini her zaman değiştirebilmeli: buton yalnız orada görünür.
            var privacyChoices = Ads.PrivacyOptionsRequired;
            var labels = privacyChoices
                ? new[] { "Restore Purchases", "Privacy Policy", "Terms of Service", "Privacy Choices", "Support" }
                : new[] { "Restore Purchases", "Privacy Policy", "Terms of Service", "Support" };
            var height = BaseHeight + (labels.Length - 4) * RowStep;
            root = NewRect("Settings", parent).gameObject;
            var rect = (RectTransform)root.transform;
            Stretch(rect);
            Layer(root, layer, true);
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.02f, 0.06f, 0.75f);
            var panel = NewRect("Panel", rect);
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, height));
            var frame = Sliced(panel, "Frame", PopupPanel, Vector2.zero, new Vector2(Width, height), 1100f * BaseHeight / 1300f);
            frame.GetComponent<Image>().raycastTarget = true;            // panel dokunmayı tutar, karartma kapatır
            frame.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var dimButton = root.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Hide);
            var hint = NewText("Tap Hint", rect, 38, UiArt.Navy);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -height / 2f - 60f), new Vector2(800f, 60f));
            hint.text = "Tap to close";
            hint.color = new Color(0.62f, 0.9f, 1f);
            var title = NewText("Title", panel, 80, UiArt.Navy);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, height / 2f - 215f), new Vector2(700f, 110f));
            title.text = "Settings";   // Choose Avatar başlığıyla aynı üst boşluk
            var y = height / 2f - 360f;
            foreach (var (label, key) in new[] { ("Sound", SaveKeys.Sound), ("Vibration", SaveKeys.Vibration) })
            {
                SettingToggle.Create(panel, label, key, y);
                y -= 130f;
            }
            y -= 20f;
            foreach (var label in labels)
            {
                var text = label;
                Action onClick = text switch
                {
                    "Restore Purchases" => () => Store.Restore(message => toast(message)),
                    "Privacy Policy" => () => Application.OpenURL(PrivacyUrl),
                    "Terms of Service" => () => Application.OpenURL(TermsUrl),
                    "Privacy Choices" => () => Ads.ShowPrivacyOptions(message => toast(message)),
                    _ => () => Application.OpenURL(SupportMail),
                };
                var b = SlicedButton(panel, label, DefaultButton, new Vector2(0f, y), new Vector2(600f, 180f), () => onClick());
                var l = NewText("Label", b, 52, UiArt.Navy);
                Place(l.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero);
                l.text = label;
                y -= RowStep;
            }
            // Hesabı sil (diğerlerinden küçük): onay sorulur, onaylanınca cihazdaki tüm ilerleme silinir.
            var delete = SlicedButton(panel, "Delete Account", DefaultButton, new Vector2(0f, y + 10f), new Vector2(420f, 130f), () =>
            {
                Hide();
                onDeleteAccount();
            });
            var dl = NewText("Label", delete, 42, UiArt.Navy);
            Place(dl.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero);
            dl.text = "Delete Account";
            var version = NewText("Version", panel, 32, UiArt.Navy);
            Place(version.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y + 10f - 65f - 12f - 25f), new Vector2(700f, 50f));   // buton
            version.text = $"Version {Application.version}";
            version.color = new Color(0.62f, 0.9f, 1f);
            root.SetActive(false);
        }

        public void Open()
        {
            root.SetActive(true);
            host.StartCoroutine(PopIn((RectTransform)root.transform.GetChild(0)));
        }

        public void Hide() => root.SetActive(false);
    }
}
