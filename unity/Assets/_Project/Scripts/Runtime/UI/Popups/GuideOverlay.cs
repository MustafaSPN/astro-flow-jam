using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Oyun içi rehber (karartma + kesit + açıklama + el): ekran kararır, anlatılan yerler yumuşak kenarlı delik olarak açık
    /// kalır; açıklama paneli deliklerden uzak yere yerleşir. El, sürükleme yolunu (bloktan kapıya) ya da dokunulacak yeri
    /// gösterir. "Tap to continue" adımında karartma her dokunuşu yutar ve adımı bitirir; etkileşimli adımda dokunmalar
    /// oyuna geçer (neye izin verildiğini denetleyici belirler).
    /// </summary>
    public sealed class GuideOverlay
    {
        readonly RectTransform canvas;

        public GuideOverlay(RectTransform canvas) => this.canvas = canvas;

        GameObject guide;
        Image guideDim;
        readonly HoleMask mask = new();
        RectTransform guidePanel, guideHand;
        Text guideTitle, guideText, guideContinue;
        Action guideOnContinue;
        Vector2 handFrom, handTo;
        bool handVisible, handDrag;
        float guideTime;
        const int GuideLayer = 116;            // HUD ve seçim karartmasının üstünde, diyaloğun (112) üstünde, tanıtımın (118) altında
        const float GuideAlpha = 0.72f;
        const float GuidePanelWidth = 900f, GuidePanelHeight = 310f;
        const float HandSize = 190f;
        const float DragSeconds = 1.3f, DragPause = 0.45f;   // el: basılı tutup sürükler, bırakır, kısa ara

        void BuildGuide()
        {
            guide = NewRect("Guide", canvas).gameObject;
            var root = (RectTransform)guide.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            Layer(guide, GuideLayer, true);
            guideDim = guide.AddComponent<Image>();
            var tap = guide.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() =>
            {
                if (guideOnContinue == null || guideTime < 0.35f) return;   // açılış dokunuşu adımı atlamasın
                var next = guideOnContinue;
                guideOnContinue = null;
                next();
            });

            guidePanel = NewRect("Panel", root);
            Place(guidePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(GuidePanelWidth, GuidePanelHeight));
            Sliced(guidePanel, "Frame", PopupPanel, Vector2.zero, new Vector2(GuidePanelWidth, GuidePanelHeight), 1100f * GuidePanelHeight / 520f);
            guideTitle = NewText("Title", guidePanel, 56, UiArt.Navy);
            Place(guideTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(800f, 70f));
            guideTitle.color = TitleYellow;
            guideText = NewText("Text", guidePanel, 44, UiArt.Navy);
            Place(guideText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(780f, 140f));
            guideText.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Uzun açıklama panele sığsın: en çok iki-üç satır, gerekirse küçülür.
            guideText.verticalOverflow = VerticalWrapMode.Truncate;
            guideText.resizeTextForBestFit = true;
            guideText.resizeTextMinSize = 30;
            guideText.resizeTextMaxSize = 44;
            guideText.color = new Color(0.62f, 0.9f, 1f);
            guideContinue = NewText("Continue", root, 40, UiArt.Navy);
            guideContinue.rectTransform.anchorMin = guideContinue.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            guideContinue.rectTransform.sizeDelta = new Vector2(700f, 60f);
            guideContinue.text = "Tap to continue";

            guideHand = Img(root, "Hand", UiArt.Asset("Tutorial/hand"), Vector2.zero, new Vector2(HandSize, HandSize));
            guideHand.anchorMin = guideHand.anchorMax = new Vector2(0.5f, 0.5f);
            guideHand.pivot = new Vector2(0.46f, 0.93f);   // işaret parmağının ucu
            guide.SetActive(false);
        }

        /// <param name="holes">Açık kalacak alanlar (ekran pikseli).</param>
        /// <param name="onContinue">Verilirse "Tap to continue" adımı: dokunuş adımı bitirir. Yoksa dokunmalar oyuna geçer.</param>
        public void Show(IReadOnlyList<Rect> holes, string title, string text, Action onContinue)
        {
            if (!guide) BuildGuide();
            guideDim.sprite = mask.Paint(holes, GuideAlpha);
            guideDim.raycastTarget = onContinue != null;
            guideOnContinue = onContinue;
            guideTitle.text = title ?? "";
            guideTitle.gameObject.SetActive(!string.IsNullOrEmpty(title));
            guideText.text = text;
            guideText.rectTransform.anchoredPosition = new Vector2(0f, string.IsNullOrEmpty(title) ? 0f : -28f);
            guideContinue.gameObject.SetActive(onContinue != null);
            PlaceGuidePanel(holes);
            handVisible = false;
            guideHand.gameObject.SetActive(false);
            guide.SetActive(true);
            guideTime = 0f;
        }

        /// <summary>El: from ≠ to ise sürükleme yolunu, eşitse dokunmayı gösterir (ekran pikseli).</summary>
        public void SetHand(Vector2 fromScreen, Vector2 toScreen)
        {
            if (!guide) return;
            handFrom = ScreenToCanvas(fromScreen);
            handTo = ScreenToCanvas(toScreen);
            handDrag = (toScreen - fromScreen).sqrMagnitude > 4f;
            handVisible = true;
            guideHand.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (guide) guide.SetActive(false);
            guideOnContinue = null;
            handVisible = false;
        }

        public bool Visible => guide && guide.activeSelf;

        Vector2 ScreenToCanvas(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)guide.transform, screen, null, out var local);
            return local;
        }

        // Panel deliklerle en az çakışan yere: ekranın üst, orta ya da alt bandı (güvenli alan içinde).
        void PlaceGuidePanel(IReadOnlyList<Rect> holes)
        {
            var root = (RectTransform)guide.transform;
            var size = root.rect.size;
            var best = 0f;
            var bestCost = float.MaxValue;
            foreach (var f in new[] { 0.74f, 0.5f, 0.26f, 0.84f, 0.16f })
            {
                var y = (f - 0.5f) * size.y;
                var screenY = f * Screen.height;
                var halfH = (GuidePanelHeight / 2f + 70f) * Screen.height / Mathf.Max(1f, size.y);
                var cost = 0f;
                foreach (var h in holes)
                    cost += Mathf.Max(0f, Mathf.Min(h.yMax, screenY + halfH) - Mathf.Max(h.yMin, screenY - halfH));
                cost += Mathf.Abs(f - 0.5f) * 40f;   // eşitlikte ortaya yakın
                if (cost < bestCost) { bestCost = cost; best = y; }
            }
            guidePanel.anchoredPosition = new Vector2(0f, best);
            guideContinue.rectTransform.anchoredPosition = new Vector2(0f, best - GuidePanelHeight / 2f - 50f);
        }

        /// <summary>Her kare: "Tap to continue" yanıp söner, el sürükleme ya da dokunmayı gösterir.</summary>
        public void Tick()
        {
            if (!guide || !guide.activeSelf) return;
            guideTime += Time.unscaledDeltaTime;
            var a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(guideTime * 2.6f));
            guideContinue.color = new Color(1f, 1f, 1f, a);
            if (!handVisible) return;
            var image = guideHand.GetComponent<Image>();
            if (handDrag)
            {
                // Bas (küçül), yolu izle, bırak (büyü), kaybol; döngü.
                var cycle = DragSeconds + DragPause;
                var t = guideTime % cycle;
                var p = Mathf.Clamp01((t - 0.2f) / (DragSeconds - 0.4f));
                var e = p * p * (3f - 2f * p);
                guideHand.anchoredPosition = Vector2.Lerp(handFrom, handTo, e);
                var pressed = t > 0.15f && t < DragSeconds - 0.15f;
                guideHand.localScale = Vector3.one * (pressed ? 0.88f : 1f);
                image.color = new Color(1f, 1f, 1f, t < DragSeconds ? 1f : 1f - (t - DragSeconds) / DragPause);
            }
            else
            {
                var t = guideTime % 1.1f;
                var press = t < 0.25f ? t / 0.25f : t < 0.5f ? 1f - (t - 0.25f) / 0.25f : 0f;
                guideHand.anchoredPosition = handTo + new Vector2(0f, -12f * press);
                guideHand.localScale = Vector3.one * (1f - 0.12f * press);
                image.color = Color.white;
            }
        }
    }
}
