using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;
using Object = UnityEngine.Object;

namespace AstroFlowJam
{
    /// <summary>
    /// Ana ekranın yol sayfası: logo, gezegen yolu (en altta oynanacak bölüm, üstünde sonraki bölümler) ve Play butonu. Yol
    /// elastik olarak kaydırılabilir, bırakınca yerine döner; logonun altına giren gezegenler yavaşça kaybolur.
    /// </summary>
    public sealed class MapPage
    {
        const float PlayY = 400f, FirstPlanetY = 680f, PlanetGap = 245f, PlanetSize = 200f, CurrentSize = 240f;
        const int PathCount = 6;            // oynanacak bölüm + üstünde 5 bölüm (en üsttekiler logonun altında kaybolur)

        readonly RectTransform page;
        readonly Func<int, bool> isHard;
        readonly Action onPlay;
        readonly RectTransform pathRoot, playButton;
        readonly Text playLevel;
        readonly ElasticDrag drag;
        RectTransform glow, ring, currentPlanet;
        Image glowImage;

        public MapPage(RectTransform page, Action onPlay, Func<int, bool> isHard)
        {
            this.page = page;
            this.isHard = isHard;
            this.onPlay = onPlay;
            var hit = page.gameObject.AddComponent<Image>();
            hit.color = Color.clear;                                  // kaydırma her yerden tutulur
            pathRoot = NewRect("Path", page);
            Stretch(pathRoot);
            drag = page.gameObject.AddComponent<ElasticDrag>();
            drag.target = pathRoot;

            var logo = Img(page, "Logo", UiArt.Asset("Home/Logo"), Vector2.zero, new Vector2(640f, 260f));
            logo.anchorMin = logo.anchorMax = new Vector2(0.5f, 1f);
            logo.anchoredPosition = new Vector2(0f, -150f);
            logo.GetComponent<Image>().preserveAspect = true;

            var play = playButton = SlicedButton(page, "Play", PlayButtonSprite, new Vector2(0f, 0f), new Vector2(720f, 220f), onPlay);
            play.anchorMin = play.anchorMax = new Vector2(0.5f, 0f);
            play.anchoredPosition = new Vector2(0f, PlayY - (GameHud.HomeBarHeight - 20f));
            var title = NewText("Title", play, 86, UiArt.Navy);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(700f, 100f));
            title.text = "Play";
            playLevel = NewText("Level", play, 44, UiArt.Navy);
            Place(playLevel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(700f, 60f));
        }

        static Sprite PlayButtonSprite => UiArt.Asset("Home/Play_Button", new Vector4(150f, 150f, 150f, 150f));

        // Gezegen yolu: en altta oynanacak bölüm, üstünde sonraki 5 bölüm; aralarında kesik çizgili yörünge. Yukarıdakiler
        // logonun altına girdikçe yavaşça kaybolur (UpdateHome).
        readonly List<(RectTransform rect, CanvasGroup group, float fadeY)> items = new();   // fadeY: saydamlığın ölçüldüğü yükseklik

        /// <summary>Yolu oynanacak bölümden başlayarak yeniden kurar ve Play yazısını günceller.</summary>
        public void Rebuild(int level, int total, bool allDone)
        {
            var hardHere = isHard != null && isHard(level);
            playLevel.text = allDone ? "New levels soon!" : hardHere ? $"Level {level} - Hard" : $"Level {level}";
            foreach (Transform child in pathRoot) Object.Destroy(child.gameObject);
            items.Clear();
            var bottom = FirstPlanetY - (GameHud.HomeBarHeight - 20f);
            var count = Mathf.Max(1, Mathf.Min(total - level + 1, PathCount));
            for (var k = 0; k < count - 1; k++)
            {
                // Yörünge iki gezegenin kenarları arasında tam ortada (alttaki oynanacak gezegen daha büyük).
                var y = bottom + k * PlanetGap;
                var lower = (k == 0 ? CurrentSize : PlanetSize) / 2f;
                var upper = PlanetSize / 2f;
                var length = PlanetGap - (lower + upper) * 0.55f;   // uçları gezegenlerin altına girer
                var seg = Img(pathRoot, "Orbit", UiArt.Asset("Home/Orbit_Segment"), Vector2.zero, new Vector2(72f, length));
                seg.anchorMin = seg.anchorMax = new Vector2(0.5f, 0f);
                seg.GetComponent<Image>().preserveAspect = false;
                seg.anchoredPosition = new Vector2(0f, y + (PlanetGap + lower - upper) / 2f);
                // Yörünge üstteki gezegenle birlikte kaybolur (saydam gezegenin içinden görünmez).
                items.Add((seg, seg.gameObject.AddComponent<CanvasGroup>(), y + PlanetGap));
            }
            for (var k = count - 1; k >= 0; k--)
            {
                var n = level + k;
                var y = bottom + k * PlanetGap;
                var current = k == 0;
                var size = current ? CurrentSize : PlanetSize;
                var holder = NewRect($"Level {n}", pathRoot);
                Place(holder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(size, size));
                holder.pivot = new Vector2(0.5f, 0.5f);
                holder.anchoredPosition = new Vector2(0f, y);
                items.Add((holder, holder.gameObject.AddComponent<CanvasGroup>(), y));
                if (current)
                {
                    glow = Img(holder, "Glow", UiArt.Glow(480, 480, 240f, 130f, new Color(0.25f, 0.85f, 1f, 0.75f)), Vector2.zero, new Vector2(480f, 480f));
                }
                var planet = ImageButton(holder, "Planet", UiArt.Asset("Home/Planet"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size),
                    current ? onPlay : () => { });
                var image = planet.GetComponent<Image>();
                image.preserveAspect = true;
                var hard = isHard != null && isHard(n);
                image.color = current ? Color.white : new Color(0.8f, 0.85f, 0.95f);
                if (current)
                {
                    currentPlanet = planet;
                    ring = Img(holder, "Ring", UiArt.Asset("Home/Selection_Ring"), Vector2.zero, new Vector2(size + 50f, size + 48f));
                }
                var label = NewText("Number", planet, current ? 84 : 70, UiArt.Navy);
                Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -4f), Vector2.zero);
                label.text = n.ToString();
                if (hard)
                {
                    // Zor bölüm: gezegenin sol üst kenarında kurukafa rozeti.
                    // Seçim halkasının da önünde: gezegen kökünde en son çizilir.
                    var skull = Img(holder, "Hard", UiArt.Asset("Home/Hard_Skull"), Vector2.zero, Vector2.one * (current ? 96f : 84f));
                    skull.anchorMin = skull.anchorMax = new Vector2(0.5f, 0.5f);
                    skull.anchoredPosition = new Vector2(-size * (current ? 0.4f : 0.33f), size * (current ? 0.4f : 0.33f));
                    skull.SetAsLastSibling();
                }
                if (!current)
                {
                    // Kilit gezegenin sağ üst kenarına oturur (kenar çizgisinin üstünde, 45° noktası).
                    var lockIcon = Img(planet, "Lock", UiArt.Asset("Home/Lock"), Vector2.zero, new Vector2(96f, 86f));
                    lockIcon.anchorMin = lockIcon.anchorMax = new Vector2(0.5f, 0.5f);
                    lockIcon.anchoredPosition = new Vector2(size * 0.33f, size * 0.33f);
                }
            }
            drag.ResetBase(Vector2.zero);
            Fade();
        }

        // Logonun altına giren yol elemanları yukarı doğru yavaşça saydamlaşır.
        void Fade()
        {
            if (items.Count == 0) return;
            var pageHeight = page.rect.height;
            float solid = pageHeight - 580f, gone = pageHeight - 40f;
            var offset = pathRoot.anchoredPosition.y;
            foreach (var (rect, group, fadeY) in items)
            {
                if (!rect) continue;
                var y = fadeY + offset;
                var a = 1f - Mathf.Clamp01((y - solid) / (gone - solid));
                a = a * a * (3f - 2f * a);
                group.alpha = rect.name == "Orbit" ? a * a : a;   // yörünge gezegenden önce silinir
                group.blocksRaycasts = a > 0.5f;
            }
        }

        /// <summary>Her kare: yol soldurulur; oynanacak gezegen ve Play butonu nefes alır.</summary>
        public void Tick(float time)
        {
            Fade();
            if (glow)
            {
                var b = 0.5f + 0.5f * Mathf.Sin(time * 2.4f);
                glow.localScale = Vector3.one * (0.88f + 0.22f * b);
                if (!glowImage) glowImage = glow.GetComponent<Image>();
                glowImage.color = new Color(1f, 1f, 1f, 0.45f + 0.55f * b);
                ring.localScale = Vector3.one * (1f + 0.035f * b);
                currentPlanet.localScale = Vector3.one * (1f + 0.02f * b);
            }
            if (playButton) playButton.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(time * 3f));
        }
    }
}
