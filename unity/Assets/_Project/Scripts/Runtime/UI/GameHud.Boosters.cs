using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    public sealed partial class GameHud : MonoBehaviour
    {
        // Duraklat butonunun hemen altında, onunla aynı sütunda ve aynı boyda; üst bara bağlı olduğu için her bölümde aynı yerde.
        void BuildBoosters()
        {
            var column = NewRect("Boosters", topBar);
            var height = BoosterNames.Length * BoosterSize + (BoosterNames.Length - 1) * BoosterGap;
            // Ekranın sağ kenarına yaslı: adet yuvarlağının sağ kenarı ekranın (güvenli alanın) sağ kenarına teğet. Üst bar
            // güvenli alanın iki yanından BarSideMargin içeride; yuvarlak butonun sağ kenarından BadgeOverhang taşar.
            Place(column, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(BarSideMargin - BadgeOverhang - BoosterEdgeGap - BoosterSize / 2f, -RightButton / 2f - BoosterTopGap - height / 2f), new Vector2(BoosterSize, height));
            for (var i = 0; i < BoosterNames.Length; i++)
            {
                var index = i;
                var y = height / 2f - BoosterSize / 2f - i * (BoosterSize + BoosterGap);
                var icon = UiArt.Asset("Boosters/" + BoosterIcons[i]);
                var px = Mathf.RoundToInt(BoosterSize);
                var button = ImageButton(column, BoosterNames[i], icon ? icon : UiArt.Glass(px, px, BoosterSize * 0.27f, ButtonTop, ButtonBottom, UiArt.Cyan, 5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(BoosterSize, BoosterSize), () => BoosterPressed?.Invoke(index));
                boosterImages[i] = button.GetComponent<Image>();
                boosterImages[i].preserveAspect = true;
                boosterIconSprites[i] = boosterImages[i].sprite;
                if (!icon) Label(button, BoosterNames[i], 25);
                // Kilitli hal: kilit simgesinin alttaki koyu şeridinde açılış bölümü ("Lvl 6").
                boosterLockLabels[i] = NewText("Unlock Level", button, 28, UiArt.Navy);
                Place(boosterLockLabels[i].rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BoosterSize * 0.1f), new Vector2(BoosterSize, 40f));
                boosterLockLabels[i].rectTransform.pivot = new Vector2(0.5f, 0f);
                boosterLockLabels[i].gameObject.SetActive(false);
                var badge = NewRect("Count", button);
                Place(badge, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(BadgeOverhang - BadgeSize / 2f, 8f), new Vector2(BadgeSize, BadgeSize));   // sağ üst köşeye yaslı
                var badgeImage = badge.gameObject.AddComponent<Image>();
                badgeImage.sprite = UiArt.Box((int)BadgeSize, (int)BadgeSize, BadgeSize / 2f, new Color(0.98f, 0.36f, 0.4f), new Color(0.78f, 0.12f, 0.2f), 0f);   // kenarlıksız
                badgeImage.raycastTarget = false;
                boosterCounts[i] = NewText("Value", badge, 30);
                Place(boosterCounts[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero);
                boosterCounts[i].text = "2";
                boosterBadges[i] = badge.gameObject;
            }
        }

        readonly Image[] boosterImages = new Image[4];
        readonly Sprite[] boosterIconSprites = new Sprite[4];
        readonly GameObject[] boosterBadges = new GameObject[4];
        readonly Text[] boosterLockLabels = new Text[4];

        /// <summary>
        /// Joker kilidi: unlockLevel > 0 ise kilit simgesi, altında açılış bölümü ("Lvl 6") ve adet rozeti gizli; 0 ise
        /// jokerin kendi simgesi ve rozeti.
        /// </summary>
        public void SetBoosterLocked(int booster, int unlockLevel)
        {
            var locked = unlockLevel > 0;
            var lockSprite = UiArt.Asset("Boosters/Locked");
            boosterImages[booster].sprite = locked && lockSprite ? lockSprite : boosterIconSprites[booster];
            boosterBadges[booster].SetActive(!locked);
            boosterLockLabels[booster].gameObject.SetActive(locked);
            if (locked) boosterLockLabels[booster].text = $"Lvl {unlockLevel}";
        }


        const float UfoWidth = 400f, UfoHeight = 200f, BeamWidth = 280f, BeamHeight = 560f;

        /// <summary>
        /// UFO jokerinin görseli (yer tutucu): üst barın üstünde çizilen UFO ve altında çekim ışığı. Kök döner; konumu
        /// anchoredPosition ile (tuval merkezine göre) sürülür. Coin göstergesinin tuvaldeki konumu UfoRestPoint.
        /// </summary>
        public RectTransform CreateUfo(Texture model, out RectTransform beam)
        {
            var root = NewRect("UFO", transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(UfoWidth, UfoHeight);
            root.SetSiblingIndex(safeArea.GetSiblingIndex() + 1);         // üst barın üstünde, duraklatma panelinin altında
            beam = NewRect("Beam", root);
            Place(beam, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -UfoHeight * 0.3f - BeamHeight / 2f), new Vector2(BeamWidth, BeamHeight));
            beam.pivot = new Vector2(0.5f, 1f);
            beam.anchoredPosition = new Vector2(0f, -UfoHeight * 0.22f);   // diskin alt yüzünden çıkar
            var beamImage = beam.gameObject.AddComponent<Image>();
            beamImage.sprite = UiArt.TractorBeam(Mathf.RoundToInt(BeamWidth), Mathf.RoundToInt(BeamHeight));
            beamImage.raycastTarget = false;
            beam.localScale = new Vector3(1f, 0f, 1f);
            // UFO modeli (AFJ_UFO) ayrı bir kamerayla dokuya çizilir ve arayüzde gösterilir: HUD'un üstünde durur.
            Graphic body;
            if (model)
            {
                var raw = NewRect("Body", root).gameObject.AddComponent<RawImage>();
                raw.texture = model;
                body = raw;
            }
            else
            {
                var image = NewRect("Body", root).gameObject.AddComponent<Image>();
                image.sprite = UiArt.Ufo(Mathf.RoundToInt(UfoWidth), Mathf.RoundToInt(UfoHeight));
                body = image;
            }
            body.raycastTarget = false;
            Place(body.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return root;
        }

        /// <summary>UFO'nun duracağı yer (tuval merkezine göre): coin göstergesinin üstü.</summary>
        public Vector2 UfoRestPoint => (Vector2)transform.InverseTransformPoint(coins.Pill.position) + new Vector2(0f, 10f);

        /// <summary>Tuval noktası (merkeze göre) → ekran pikseli.</summary>
        public Vector2 CanvasToScreen(Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, transform.TransformPoint(local));

        /// <summary>Tuvalin yüksekliği (referans birim).</summary>
        public float CanvasHeight => canvasRect.rect.height;

        /// <summary>Jokerin kalan adedi (rozette).</summary>
        public void SetBoosterCount(int booster, int count)
        {
            var t = boosterCounts[booster];
            t.text = count > 0 ? count.ToString() : "+";
            t.fontSize = count > 0 ? 30 : 52;                     // "+" rakamdan ince: büyük yazılır
            t.rectTransform.anchoredPosition = new Vector2(0f, count > 0 ? 2f : 4f);
        }
    }
}
