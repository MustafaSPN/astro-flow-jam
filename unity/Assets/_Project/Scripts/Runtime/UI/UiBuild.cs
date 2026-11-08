using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Kod ile kurulan arayüzün ortak yapı taşları: dikdörtgen, yazı, görsel, düğme, yerleşim ve küçük animasyonlar.
    /// Ekranlar bunları <c>using static AstroFlowJam.UiBuild</c> ile doğrudan çağırır.
    /// </summary>
    public static class UiBuild
    {
        /// <summary>Sınırsız can işareti: kalbin ortasında düz ∞ görseli (lacivert kenarlı beyaz çizgi).</summary>
        public static Image InfinityMark(RectTransform heart, float size)
        {
            var w = size * 0.98f;
            var mark = Img(heart, "Infinity", UiArt.Infinity(), new Vector2(0f, size * 0.05f), new Vector2(w, w * 150f / 256f));
            mark.anchorMin = mark.anchorMax = new Vector2(0.5f, 0.5f);
            var image = mark.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Kaydırılan sayfalarda içeriğin başladığı yükseklik (başlık ve sayının altı).</summary>
        public const float PageContentTop = 170f;

        /// <summary>Kaydırılan kartlar başlığın altına girdikçe yavaşça kaybolur.</summary>
        public static void FadeUnderTitle(RectTransform content, float scroll)
        {
            foreach (Transform child in content)
            {
                var rect = (RectTransform)child;
                var top = rect.anchoredPosition.y + rect.sizeDelta.y / 2f + scroll;   // kartın üst kenarı, görünüm üstüne göre
                var a = 1f - Mathf.Clamp01((top + PageContentTop - 30f) / 260f);
                if (!child.TryGetComponent<CanvasGroup>(out var g)) g = child.gameObject.AddComponent<CanvasGroup>();
                g.alpha = a * a * (3f - 2f * a);
            }
        }

        public static RectTransform ShopCard(RectTransform parent, Vector2 center, Vector2 size)
        {
            var card = NewRect("Card", parent);
            Place(card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), center, size);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = center;
            var image = card.gameObject.AddComponent<Image>();
            image.sprite = UiArt.Glass(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), 40f, new Color(0.11f, 0.2f, 0.45f), new Color(0.05f, 0.09f, 0.24f), UiArt.Cyan, 5f);
            return card;
        }

        public static readonly Color ButtonTop = new(0.17f, 0.42f, 0.88f);       // buton içi: parlak mavi cam
        public static readonly Color ButtonBottom = new(0.07f, 0.16f, 0.47f);

        static Texture2D fadeTexture;
        public static Texture2D VerticalFade()
        {
            if (fadeTexture) return fadeTexture;
            fadeTexture = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var bg = SceneArt.Hex(0x0B1440);
            for (var y = 0; y < 64; y++)
            {
                var t = y / 63f;                                   // 0 alt, 1 üst
                fadeTexture.SetPixel(0, y, new Color(bg.r, bg.g, bg.b, Mathf.SmoothStep(0f, 0.95f, Mathf.Clamp01(t * 1.6f))));
            }
            fadeTexture.Apply();
            return fadeTexture;
        }

        // Yuvarlak köşeli maske içinde resim: karta kenarlarından eşit payla tam oturur.
        public static RawImage RoundedPicture(RectTransform parent, Texture tex, float inset, float radius)
        {
            var mask = NewRect("Mask", parent);
            Stretch(mask);
            mask.offsetMin = Vector2.one * inset;
            mask.offsetMax = -Vector2.one * inset;
            var size = Mathf.RoundToInt(Mathf.Max(64f, parent.rect.width > 0 ? parent.rect.width : 300f));
            var shape = mask.gameObject.AddComponent<Image>();
            shape.sprite = UiArt.Box(size, size, radius, Color.white, Color.white, 0f, Color.white);
            shape.raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var raw = NewRect("Picture", mask).gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            Stretch(raw.rectTransform);
            if (tex) raw.uvRect = SquareUv(tex);
            return raw;
        }

        // Kare olmayan resim ortadan kare kırpılır (oyundaki gizli resim gibi).
        public static Rect SquareUv(Texture tex)
        {
            var aspect = tex.width / (float)tex.height;
            return aspect >= 1f ? new Rect((1f - 1f / aspect) / 2f, 0f, 1f / aspect, 1f) : new Rect(0f, (1f - aspect) / 2f, 1f, aspect);
        }

        public static System.Collections.IEnumerator PopIn(RectTransform target)
        {
            for (var t = 0f; t < 0.28f; t += Time.unscaledDeltaTime)
            {
                var p = t / 0.28f;
                const float c1 = 1.70158f, c3 = c1 + 1f;
                var e = 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
                if (!target) yield break;              // pencere animasyon sürerken kapandı
                target.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, e);
                yield return null;
            }
            if (target) target.localScale = Vector3.one;
        }

        // Dikey kaydırılan içerik (maskeli görünüm alanı).
        public static RectTransform ScrollContent(RectTransform page, Vector2 topOffset)
        {
            var view = NewRect("View", page);
            Stretch(view);
            view.offsetMax = topOffset;
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var content = NewRect("Content", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 2000f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        // Başlığın arkasında yukarıdan aşağı koyulaşan gradyan: altına giren içerik okunurluğu bozmaz. Geniş ekranda sütundan
        // ekran kenarlarına taşar.
        public static void PageFade(RectTransform page)
        {
            var fade = NewRect("Fade", page).gameObject.AddComponent<RawImage>();
            fade.texture = VerticalFade();
            fade.raycastTarget = false;
            Place(fade.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(2f * ScreenFit.SideMargin, 260f));
            fade.rectTransform.pivot = new Vector2(0.5f, 1f);
        }

        public static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            r.pivot = new Vector2(0.5f, 0.5f);
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // Beyaz, kalın siyah kenarlı yazı (türün yazı stili).
        public static Text NewText(string name, Transform parent, int size, Color? edge = null)
        {
            var text = NewRect(name, parent).gameObject.AddComponent<Text>();
            text.font = UiArt.Font;
            text.fontSize = size;
            text.fontStyle = UiArt.IsBoldFace ? FontStyle.Normal : FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.raycastTarget = false;
            var outline = text.gameObject.AddComponent<ThickOutline>();
            outline.color = edge ?? UiArt.Ink;
            outline.distance = Mathf.Max(3f, size * (edge.HasValue ? 0.05f : 0.07f));
            outline.shadow = new Vector2(0f, -size * 0.06f);
            return text;
        }

        public static void Label(RectTransform parent, string label, int size)
        {
            var text = NewText("Label", parent, size);
            Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero);
            text.text = label;
        }

        public static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static RectTransform ImageButton(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position,
            Vector2 size, Action onClick)
        {
            var rect = NewRect(name, parent);
            Place(rect, anchor, anchor, position, size);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            rect.gameObject.AddComponent<ButtonPunch>();
            return rect;
        }

        // Tema butonu: arkada neon hale, lacivert cam gövde, camgöbeği kenar.
        public static RectTransform ThemeButton(Transform parent, string name, Vector2 position, float size, Action onClick)
        {
            var glow = NewRect(name + " Glow", parent).gameObject.AddComponent<Image>();
            var glowSize = Mathf.RoundToInt(size + 56f);
            glow.sprite = UiArt.Glow(glowSize, glowSize, size * 0.27f, 28f, new Color(UiArt.Cyan.r, UiArt.Cyan.g, UiArt.Cyan.b, 0.22f));
            glow.raycastTarget = false;
            Place(glow.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), position, new Vector2(glowSize, glowSize));
            glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glow.rectTransform.anchoredPosition = position;
            var px = Mathf.RoundToInt(size);
            return ImageButton(parent, name, UiArt.Glass(px, px, size * 0.27f, ButtonTop, ButtonBottom, UiArt.Cyan, 5f),
                new Vector2(1f, 0.5f), position, new Vector2(size, size), onClick);
        }

        public static void AddNeonIcon(RectTransform parent, string kind, float size, bool whiteFill = false)
        {
            var icon = NewRect("Icon", parent).gameObject.AddComponent<Image>();
            icon.sprite = UiArt.NeonIcon(kind, whiteFill: whiteFill);
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        }

        public static void AddIcon(RectTransform parent, string kind, float size)
        {
            var icon = NewRect("Icon", parent).gameObject.AddComponent<Image>();
            icon.sprite = UiArt.Icon(kind);
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        }

        /// <summary>Dokunuş bir HUD öğesinin üzerinde mi? Tahta girdisi bu durumda yok sayılır.</summary>
        public static bool IsPointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>
        /// 9 dilimli görsel: köşe/kenar payları yükseklikle orantılı ölçeklenir (yükseklik kaynakla aynı oranda küçülür,
        /// genişlik serbest), yani buton ve panel her en boy oranında bozulmadan kullanılır.
        /// </summary>
        public static RectTransform Sliced(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size, float referenceHeight)
        {
            var rect = Img(parent, name, sprite, position, size);
            var image = rect.GetComponent<Image>();
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            if (sprite) image.pixelsPerUnitMultiplier = sprite.rect.height / referenceHeight;
            return rect;
        }

        /// <summary>9 dilimli buton (yeşil ya da varsayılan): görsel + tıklama + basma tepkisi.</summary>
        public static RectTransform SlicedButton(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Action onClick)
        {
            var rect = Sliced(parent, name, sprite, position, size, size.y);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            rect.gameObject.AddComponent<ButtonPunch>();
            return rect;
        }

        // Ayrı çizim katmanı (iç içe Canvas): popup karartmanın, coin göstergesi ve uçan coinler popup'ın üstünde.
        public static void Layer(GameObject go, int order, bool raycast)
        {
            if (!go.TryGetComponent<Canvas>(out var canvas)) canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
            if (raycast && !go.GetComponent<GraphicRaycaster>()) go.AddComponent<GraphicRaycaster>();
        }

        public static RectTransform Img(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
        {
            var image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            return image.rectTransform;
        }

        public static void NeonBar(RectTransform parent, Vector2 position, Vector2 size, Color color, float angle)
        {
            var w = Mathf.RoundToInt(size.x + 28f);
            var h = Mathf.RoundToInt(size.y + 28f);
            var glow = Img(parent, "Neon Glow", UiArt.Glow(w, h, Mathf.Min(size.x, size.y) / 2f, 14f, new Color(color.r, color.g, color.b, 0.6f)), position, new Vector2(w, h));
            var bar = Img(parent, "Neon", UiArt.Box(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), Mathf.Min(size.x, size.y) / 2f,
                Color.Lerp(color, Color.white, 0.35f), color, 0f), position, size);
            glow.localRotation = bar.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        public static IEnumerator Punch(RectTransform target, float amount, float seconds)
        {
            if (!target) yield break;
            for (var t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                target.localScale = Vector3.one * (1f + amount * Mathf.Sin(t / seconds * Mathf.PI));
                yield return null;
            }
            target.localScale = Vector3.one;
        }
    }
}
