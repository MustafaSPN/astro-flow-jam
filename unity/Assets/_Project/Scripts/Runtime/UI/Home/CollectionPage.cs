using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Ana ekranın koleksiyon sayfası: açılan gizli resimler üçlü ızgarada, sıradaki birkaç bölüm kilitli kart. Yalnız
    /// ekrana yakın kartların resmi bellekte tutulur; karta dokununca resim büyük görüntülenir.
    /// </summary>
    public sealed class CollectionPage
    {
        const float Cell = 320f, Gap = 24f;
        const int PictureLoadsPerFrame = 3;
        const float ViewerSize = 980f, ViewerInset = 70f;    // panelin iç neon çizgisine kadar

        sealed class Card
        {
            public RectTransform rect;
            public RawImage image;
            public string name;
            public bool loaded;
        }

        readonly MonoBehaviour host;
        readonly Transform overlayParent;
        readonly int overlayLayer;
        readonly Func<int, string> pictureName;
        readonly RectTransform content;
        readonly Text count;
        readonly List<Card> cards = new();
        float lastScroll = float.NaN;
        bool loadPending;

        GameObject viewer;
        RawImage viewerImage;
        string viewerPicture;

        public CollectionPage(RectTransform page, Transform overlayParent, int overlayLayer, MonoBehaviour host, Func<int, string> pictureName)
        {
            this.host = host;
            this.overlayParent = overlayParent;
            this.overlayLayer = overlayLayer;
            this.pictureName = pictureName;
            // Kaydırılan resimler başlığın altına girdikçe yavaşça kaybolur; başlık ve sayı en üstte çizilir.
            content = ScrollContent(page, Vector2.zero);
            PageFade(page);
            var title = NewText("Title", page, 84, UiArt.Navy);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(900f, 110f));
            title.text = "Collection";
            count = NewText("Count", page, 46, UiArt.Navy);
            Place(count.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(900f, 60f));
            count.color = PopupArt.TitleYellow;
        }

        /// <summary>Açılanlar + bulunduğu satırı tamamlayıp sonraki iki satırı dolduran kilitli kartlar.</summary>
        public void Show(int unlocked, int total)
        {
            Hide();
            cards.Clear();
            lastScroll = float.NaN;
            foreach (Transform child in content) UnityEngine.Object.Destroy(child.gameObject);
            var shown = Mathf.Min(total, (unlocked + 2) / 3 * 3 + 6);
            count.text = NumberText.Of(unlocked);
            for (var i = 0; i < shown; i++)
            {
                var n = i + 1;
                var center = new Vector2((i % 3 - 1) * (Cell + Gap), -PageContentTop - Cell / 2f - (i / 3) * (Cell + Gap));
                var card = ShopCard(content, center, new Vector2(Cell, Cell));
                if (n <= unlocked)
                {
                    var picture = RoundedPicture(card, null, 12f, 30f);
                    picture.enabled = false;
                    cards.Add(new Card { rect = card, image = picture, name = pictureName(n) });
                    var button = card.gameObject.AddComponent<Button>();
                    button.targetGraphic = card.GetComponent<Image>();
                    button.onClick.AddListener(() => OpenViewer(n));
                    card.gameObject.AddComponent<ButtonPunch>();
                }
                else
                {
                    card.GetComponent<Image>().color = new Color(0.55f, 0.6f, 0.75f);
                    var lockIcon = Img(card, "Lock", UiArt.Asset("Home/Lock"), Vector2.zero, new Vector2(120f, 108f));
                    lockIcon.anchorMin = lockIcon.anchorMax = new Vector2(0.5f, 0.5f);
                }
            }
            content.sizeDelta = new Vector2(0f, PageContentTop + (shown + 2) / 3 * (Cell + Gap) + 80f);
        }

        /// <summary>Sayfa kapanınca resimler ve açık görüntüleyici bırakılır.</summary>
        public void Hide()
        {
            foreach (var card in cards)
                if (card.loaded) Unload(card);
            CloseViewer();
        }

        /// <summary>Her kare: kaydırma değiştiyse kartları soldurur ve yakındaki resimleri yükler.</summary>
        public void Tick()
        {
            var scroll = content.anchoredPosition.y;
            if (scroll == lastScroll && !loadPending) return;
            lastScroll = scroll;
            UpdatePictures(scroll);
            FadeUnderTitle(content, scroll);
        }

        void UpdatePictures(float scroll)
        {
            var viewHeight = ((RectTransform)content.parent).rect.height;
            var budget = PictureLoadsPerFrame;
            loadPending = false;
            foreach (var card in cards)
            {
                var size = card.rect.sizeDelta.y;
                var top = card.rect.anchoredPosition.y + size / 2f + scroll;
                var near = top - size < size && top > -viewHeight - size;   // görünüm ve bir kart payı
                if (near && !card.loaded)
                {
                    if (budget <= 0) { loadPending = true; continue; }
                    budget--;
                    var texture = LevelPictures.Acquire(card.name);
                    if (!texture) continue;
                    card.loaded = true;
                    card.image.texture = texture;
                    card.image.uvRect = SquareUv(texture);
                    card.image.enabled = true;
                }
                else if (!near && card.loaded) Unload(card);
            }
        }

        static void Unload(Card card)
        {
            card.loaded = false;
            card.image.texture = null;
            card.image.enabled = false;
            LevelPictures.Release(card.name);
        }

        // Resim büyür: kare panel, resim çerçeveye yuvarlak köşelerle tam oturur.
        void OpenViewer(int level)
        {
            if (!viewer) BuildViewer();
            CloseViewer();
            viewerPicture = pictureName(level);
            var texture = LevelPictures.Acquire(viewerPicture);
            viewerImage.texture = texture;
            if (texture) viewerImage.uvRect = SquareUv(texture);
            viewer.SetActive(true);
            host.StartCoroutine(PopIn((RectTransform)viewer.transform.GetChild(0)));
        }

        void CloseViewer()
        {
            if (viewer) viewer.SetActive(false);
            if (viewerImage) viewerImage.texture = null;
            LevelPictures.Release(viewerPicture);
            viewerPicture = null;
        }

        void BuildViewer()
        {
            viewer = NewRect("Viewer", overlayParent).gameObject;
            var root = (RectTransform)viewer.transform;
            Stretch(root);
            Layer(viewer, overlayLayer, true);
            var dim = viewer.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.02f, 0.06f, 0.85f);
            var close = viewer.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(CloseViewer);
            var frame = NewRect("Frame", root);
            Place(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(ViewerSize, ViewerSize));
            Sliced(frame, "Panel", PopupArt.PopupPanel, Vector2.zero, new Vector2(ViewerSize, ViewerSize), 1100f * ViewerSize / 1300f);
            var holder = NewRect("Holder", frame);
            Stretch(holder);
            viewerImage = RoundedPicture(holder, null, ViewerInset, 40f);
            // Panel görselinin iç neon çizgisi ortada değil (popup/panel.png: sol 54, sağ 82, üst 106, alt 57 px; gölge
            // sağ altta): resim çizginin içine eşit payla oturur.
            var k = 1100f * ViewerSize / 1300f / 1101f;          // görsel pikseli → tuval birimi (Sliced referans yüksekliği)
            const float pad = 12f;
            var mask = (RectTransform)viewerImage.transform.parent;
            mask.offsetMin = new Vector2(54f * k + pad, 57f * k + pad);
            mask.offsetMax = new Vector2(-(82f * k + pad), -(106f * k + pad));
            var hint = NewText("Hint", root, 38, UiArt.Navy);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -540f), new Vector2(800f, 60f));
            hint.text = "Tap to close";
            hint.color = new Color(0.62f, 0.9f, 1f);
        }
    }
}
