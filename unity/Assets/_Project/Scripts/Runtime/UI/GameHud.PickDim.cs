using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Joker hedef seçimi karartması (Wormhole: tahta, UFO: tahta ve resim): ekranın geri kalanı hafifçe kararır, verilen
    /// ekran dikdörtgenleri yuvarlak köşeli, yumuşak kenarlı delik olarak açık kalır. Dokunmaları yutmaz; seçilen joker
    /// butonu karartmanın üstünde kalır (yeniden basınca seçim iptal edilir).
    /// </summary>
    public sealed partial class GameHud
    {
        GameObject pickDim;
        Image pickDimImage;
        readonly HoleMask pickDimMask = new();
        int pickDimBooster = -1;
        const int PickDimLayer = 110;           // HUD'un üstünde, diyaloğun (112) ve seçim panelinin (125) altında
        const float PickDimAlpha = 0.82f;

        /// <param name="holes">Açık kalacak alanlar, ekran pikseli (sol alt köken).</param>
        public void ShowPickDim(int booster, IReadOnlyList<Rect> holes)
        {
            if (!pickDim)
            {
                pickDim = NewRect("Pick Dim", transform).gameObject;
                var root = (RectTransform)pickDim.transform;
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
                Layer(pickDim, PickDimLayer, false);
                pickDimImage = pickDim.AddComponent<Image>();
                pickDimImage.raycastTarget = false;
            }
            pickDimImage.sprite = pickDimMask.Paint(holes, PickDimAlpha);
            pickDim.SetActive(true);
            RaiseBooster(booster);
        }

        public void HidePickDim()
        {
            if (pickDim) pickDim.SetActive(false);
            RaiseBooster(-1);
        }

        // Seçimdeki joker butonu karartmanın üstüne çıkar; önceki eski yerine iner.
        void RaiseBooster(int booster)
        {
            if (pickDimBooster >= 0 && pickDimBooster != tutorialBooster && boosterImages[pickDimBooster].TryGetComponent<Canvas>(out var old))
                old.overrideSorting = false;
            pickDimBooster = booster;
            if (booster >= 0) Layer(boosterImages[booster].gameObject, PickDimLayer + 1, true);
        }
    }
}
