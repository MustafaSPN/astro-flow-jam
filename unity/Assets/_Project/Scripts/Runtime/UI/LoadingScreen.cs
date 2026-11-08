using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Açılış yükleme ekranı (Loading_UI/Loading_Screen_Concept tasarımı): lacivert degrade zemin, üstte logo, ortada
    /// roketli bloklar, altta camgöbeği çerçeveli dolan çubuk ve "Loading..." yazısı. Zemin, logo ve roket tek görselde
    /// (UI/Loading/Background, iOS açılış ekranıyla aynı) olduğundan açılıştan bu ekrana geçişte hiçbir şey zıplamaz.
    /// Görsel telefon oranında bir sütuna sığdırılır (iPad'de kenarlar zemin rengiyle dolar). En az 5 sn görünür, sonra
    /// yumuşakça kaybolur ve kendini siler; görünürken dokunmaları tutar.
    /// </summary>
    public sealed class LoadingScreen
    {
        const int SortLayer = 300;                               // her şeyin üstünde
        const float MinSeconds = 5f, FadeSeconds = 0.4f;
        const float ImageAspect = 1080f / 2340f;                 // UI/Loading/Background
        // Taslaktan ölçülen yerler (sütun içinde, alttan oran): çubuk ve yazı.
        const float BarLeft = 0.092f, BarRight = 0.908f, BarCenterY = 0.149f, BarAspect = 10.5f;
        const float TextBottom = 0.088f, TextTop = 0.114f;
        static readonly Color Base = new(2f / 255f, 6f / 255f, 60f / 255f);   // görselin kenar rengi ve iOS açılış zemini
        static readonly Color Border = new(1f / 255f, 248f / 255f, 1f);
        static readonly Color Track = new(2f / 255f, 16f / 255f, 82f / 255f);

        readonly GameObject root;
        readonly CanvasGroup group;
        readonly RectTransform fill;
        float time, fadeTime = -1f;

        public LoadingScreen(Transform canvas)
        {
            var rect = NewRect("Loading Screen", canvas);
            Stretch(rect);
            root = rect.gameObject;
            Layer(root, SortLayer, true);
            var bg = root.AddComponent<Image>();
            bg.color = Base;                                     // dokunmaları da tutar
            group = root.AddComponent<CanvasGroup>();

            // Telefon oranlı sütun: görsel tam sığar, iPad'de ortada durur.
            var column = NewRect("Column", rect);
            Stretch(column);
            var fit = column.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = ImageAspect;
            var image = column.gameObject.AddComponent<Image>();
            image.sprite = UiArt.Asset("Loading/Background");
            image.raycastTarget = false;

            // Çubuk: koyu iç, kalın camgöbeği çerçeve; içte açık camgöbeği dolgu.
            var bar = NewRect("Bar", column);
            bar.anchorMin = new Vector2(BarLeft, BarCenterY);
            bar.anchorMax = new Vector2(BarRight, BarCenterY);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = Vector2.zero;
            var barFit = bar.gameObject.AddComponent<AspectRatioFitter>();
            barFit.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            barFit.aspectRatio = BarAspect;
            var track = bar.gameObject.AddComponent<Image>();
            track.sprite = Capsule(256, 66, Track, Track, Border, 6f);
            track.type = Image.Type.Sliced;
            track.raycastTarget = false;
            var inner = NewRect("Inner", bar);
            inner.anchorMin = new Vector2(0f, 0.2f);
            inner.anchorMax = new Vector2(1f, 0.8f);
            inner.offsetMin = Vector2.zero;
            inner.offsetMax = Vector2.zero;
            inner.gameObject.AddComponent<InsetByHeight>().Share = 0.2f;   // yuvarlak uçlar çerçeveye değmesin
            fill = NewRect("Fill", inner);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = Capsule(256, 40, new Color(0.45f, 0.9f, 1f), new Color(0f, 0.66f, 0.99f), Color.clear, 0f);
            fillImage.type = Image.Type.Sliced;
            fillImage.raycastTarget = false;

            var text = NewText("Loading", column, 60, UiArt.Navy);
            text.rectTransform.anchorMin = new Vector2(0.2f, TextBottom);
            text.rectTransform.anchorMax = new Vector2(0.8f, TextTop);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 200;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            if (text.TryGetComponent<Outline>(out var outline)) Object.Destroy(outline);
            text.color = Color.white;
            text.text = "Loading...";
        }

        /// <summary>Her kare (gerçek zamanla): çubuk dolar, sonra ekran kaybolur. Bitince false döner.</summary>
        public bool Tick()
        {
            if (!root) return false;
            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // ilk karelerin uzun süresi çubuğu bir anda doldurmasın
            time += dt;
            var p = Mathf.Clamp01(time / MinSeconds);
            var shown = 1f - (1f - p) * (1f - p);
            fill.anchorMax = new Vector2(shown, 1f);
            fill.gameObject.SetActive(shown > 0.04f);            // çok kısa dolgu yuvarlak uçlarla bozulmasın
            if (p < 1f) return true;
            if (fadeTime < 0f) fadeTime = 0f;
            fadeTime += dt;
            group.alpha = 1f - Mathf.Clamp01(fadeTime / FadeSeconds);
            if (fadeTime < FadeSeconds) return true;
            Object.Destroy(root);
            return false;
        }

        // Yatay hap: üstten alta degrade, isteğe bağlı kenar çizgisi; uçlar yuvarlak kalsın diye dilimli (9-slice).
        static Sprite Capsule(int w, int h, Color top, Color bottom, Color edge, float edgeWidth)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            var r = h / 2f;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var px = x + 0.5f;
                var py = y + 0.5f;
                var cx = Mathf.Clamp(px, r, w - r);
                var d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, r)) - r;   // kenara uzaklık (içeride negatif)
                var c = edgeWidth > 0f && d > -edgeWidth ? edge : Color.Lerp(bottom, top, py / h);
                c.a *= Mathf.Clamp01(0.5f - d);
                pixels[y * w + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        /// <summary>Çubuğun iç alanını, çubuk yüksekliğinin bir oranı kadar soldan ve sağdan içeri çeker.</summary>
        sealed class InsetByHeight : MonoBehaviour
        {
            public float Share;

            void LateUpdate()
            {
                var rect = (RectTransform)transform;
                var inset = ((RectTransform)rect.parent).rect.height * Share;
                rect.offsetMin = new Vector2(inset, 0f);
                rect.offsetMax = new Vector2(-inset, 0f);
            }
        }
    }
}
