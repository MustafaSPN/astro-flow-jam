using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class GameHud
    {
        GuideOverlay guideOverlay;
        GuideOverlay Guide => guideOverlay ??= new GuideOverlay((RectTransform)transform);

        /// <param name="holes">Açık kalacak alanlar (ekran pikseli).</param>
        /// <param name="onContinue">Verilirse "Tap to continue" adımı: dokunuş adımı bitirir. Yoksa dokunmalar oyuna geçer.</param>
        public void ShowGuide(IReadOnlyList<Rect> holes, string title, string text, Action onContinue) => Guide.Show(holes, title, text, onContinue);

        /// <summary>El: from ≠ to ise sürükleme yolunu, eşitse dokunmayı gösterir (ekran pikseli).</summary>
        public void SetGuideHand(Vector2 fromScreen, Vector2 toScreen) => guideOverlay?.SetHand(fromScreen, toScreen);

        public void HideGuide() => guideOverlay?.Hide();

        public bool GuideVisible => guideOverlay != null && guideOverlay.Visible;
    }
}
