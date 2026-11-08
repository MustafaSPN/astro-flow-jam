using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Kodla çizilen mekanik görselleri: buz kabuğu (Blender'dan buz modeli gelmedi) ve tek eksen oku.
    /// Buz: bloğu saran yarı saydam cam kabuk, kenarlarda parlak kenar, köşelerde yüzeyli kırağı parçaları ve buz sayacı
    /// azaldıkça artan çatlaklar (4 aşama). Çok hücreli blokta kabuk hücreler arasında kesilmez: her hücre komşu maskesine göre
    /// yalnız dış kenarlarında kenarlıdır. Sprite'lar hücre boyunda üretilir ve önbelleğe alınır.
    /// </summary>
    public static class MechanicArt
    {
        const int Size = 128;                                  // hücre başı piksel
        public const int IceStages = 4;                        // çatlak aşamaları: 0 en az, 3 en çok
        static readonly Dictionary<string, Sprite> Cache = new();

        /// <summary>Buz sayacından çatlak aşaması: başta az çatlak, sayaç azaldıkça artar (son sayıda en çok).</summary>
        public static int IceStage(int initial, int left)
        {
            if (initial <= 1) return initial == 1 ? 1 : 0;
            var progress = (initial - Mathf.Clamp(left, 1, initial)) / (float)(initial - 1);   // 0 başta, sayaç 1'de 1
            return Mathf.Clamp(Mathf.RoundToInt(progress * (IceStages - 1)), 0, IceStages - 1);
        }

        /// <summary>
        /// Buz kabuğu hücresi. connected: komşu dolu hücreler (1 sol, 2 sağ, 4 alt, 8 üst) – o yönde kenar yok, kabuk sürer.
        /// variant 0–3: kırağı ve çatlakların yönü (hücreler birbirinin aynısı görünmesin).
        /// </summary>
        public static Sprite Ice(int stage, int connected, int variant) =>
            Get($"ice{stage}_{connected}_{variant}", () => Draw(p => IcePixel(p, stage, connected, variant)));

        // ------------------------------------------------------------ buz
        static readonly Vector2[][] Cracks =
        {
            // aşama 0: üst kenardan tek kısa çatlak
            new[] { new Vector2(0.12f, 0.5f), new Vector2(0.06f, 0.36f), new Vector2(0.1f, 0.26f) },
            // aşama 1: sağ kenardan inen çatlak ve dalı
            new[] { new Vector2(0.5f, 0.18f), new Vector2(0.36f, 0.1f), new Vector2(0.3f, -0.04f), new Vector2(0.18f, -0.1f) },
            new[] { new Vector2(0.36f, 0.1f), new Vector2(0.34f, 0.24f) },
            // aşama 2: ilk çatlak uzar, alttan yeni çatlak
            new[] { new Vector2(0.1f, 0.26f), new Vector2(-0.02f, 0.12f), new Vector2(-0.06f, 0.02f) },
            new[] { new Vector2(-0.22f, -0.5f), new Vector2(-0.16f, -0.34f), new Vector2(-0.24f, -0.2f), new Vector2(-0.14f, -0.08f) },
            // aşama 3: soldan ve merkezden ağ gibi
            new[] { new Vector2(-0.5f, 0.06f), new Vector2(-0.34f, 0.1f), new Vector2(-0.22f, 0.02f), new Vector2(-0.06f, 0.02f) },
            new[] { new Vector2(-0.06f, 0.02f), new Vector2(0.06f, -0.12f), new Vector2(0.18f, -0.1f) },
            new[] { new Vector2(-0.14f, -0.08f), new Vector2(-0.02f, -0.28f), new Vector2(0.1f, -0.36f), new Vector2(0.14f, -0.5f) },
            new[] { new Vector2(-0.34f, 0.1f), new Vector2(-0.36f, 0.3f), new Vector2(-0.28f, 0.42f) },
        };
        static readonly int[] CracksPerStage = { 1, 3, 5, 9 };

        static Color IcePixel(Vector2 p, int stage, int connected, int variant)
        {
            // Dış kenarlar hücrenin hafif dışına taşar (kabuk bloktan biraz büyük); bağlı yönlerde kabuk komşu hücreye uzar,
            // kenar parlaması iç sınırlara düşmez: blok tek parça buz gibi görünür.
            var ext = new Vector4(
                (connected & 1) != 0 ? 2f : 0.49f, (connected & 2) != 0 ? 2f : 0.49f,
                (connected & 4) != 0 ? 2f : 0.49f, (connected & 8) != 0 ? 2f : 0.49f);
            var d = ShellDistance(p, ext, connected);
            const float aa = 1.5f / Size;
            if (d > aa || Mathf.Abs(p.x) > 0.5f || Mathf.Abs(p.y) > 0.5f) return Color.clear;

            // Çatlaklar varyanta göre döner (hücreler birbirinin aynısı görünmesin).
            var q = Rotate(p, variant * 90f);

            // Cam gövde: ince, hafif camgöbeği; blok rengi altından canlı görünür.
            var c = Color.Lerp(new Color(0.62f, 0.9f, 1f, 0.15f), new Color(0.5f, 0.82f, 1f, 0.11f), 0.5f - p.y);

            // Kalın cam kenarı: sol üstte parlak beyaz, sağ altta koyu camgöbeği (ışık sol üstten) + içte ince parıltı çizgisi.
            var edge = -d;
            if (edge < 0.09f)
            {
                var k = 1f - edge / 0.09f;
                var side = Mathf.Clamp01(0.5f + (p.x - p.y) * 1.1f);
                var rim = Color.Lerp(new Color(0.95f, 1f, 1f, 0.9f), new Color(0.42f, 0.74f, 0.95f, 0.85f), side);
                c = Color.Lerp(c, rim, k * k);
            }
            if (Mathf.Abs(edge - 0.105f) < 0.007f) c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.6f), 0.55f);

            // Köşe kırağısı: yalnız iki dış kenarın buluştuğu köşelerde, çapraz iki köşede yüzeyli beyazımsı parça.
            var corners = variant % 2 == 0 ? new[] { new Vector2(-1f, 1f), new Vector2(1f, -1f) } : new[] { new Vector2(1f, 1f), new Vector2(-1f, -1f) };
            foreach (var corner in corners)
            {
                var outerX = (connected & (corner.x < 0 ? 1 : 2)) == 0;
                var outerY = (connected & (corner.y < 0 ? 4 : 8)) == 0;
                if (outerX && outerY) c = Frost(c, p, corner, 0.47f);
            }

            // Yansıma: sol üstten çapraz geniş, soluk parlama.
            var sheen = Mathf.Abs(p.x + p.y - 0.18f);
            if (sheen < 0.06f) c = Color.Lerp(c, new Color(1f, 1f, 1f, c.a + 0.1f), (1f - sheen / 0.06f) * 0.3f);

            var count = CracksPerStage[Mathf.Clamp(stage, 0, IceStages - 1)];
            var line = 1f; var shadow = 1f;
            for (var i = 0; i < count; i++)
            {
                var pts = Cracks[i];
                for (var j = 0; j + 1 < pts.Length; j++)
                {
                    line = Mathf.Min(line, Segment(q, pts[j], pts[j + 1]));
                    shadow = Mathf.Min(shadow, Segment(q - new Vector2(0.012f, -0.012f), pts[j], pts[j + 1]));
                }
            }
            // Çatlaklar: beyaz çizgi, bir yanında koyu gölge (kabuğun derinliği).
            if (shadow < 0.016f) c = Color.Lerp(c, new Color(0.2f, 0.42f, 0.6f, 0.6f), 1f - shadow / 0.016f);
            if (line < 0.012f) c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.97f), Mathf.Clamp01(1.4f - line / 0.012f));

            c.a *= Mathf.Clamp01((aa - d) / (2f * aa));
            return c;
        }

        // Köşe kırağısı: köşeye bakan çapraz yönde eşik ötesinde, üç yüzeyli açık tonlu parça.
        static Color Frost(Color c, Vector2 q, Vector2 corner, float threshold)
        {
            var t = Vector2.Dot(q, corner.normalized);
            if (t < threshold) return c;
            var across = Vector2.Dot(q, new Vector2(-corner.y, corner.x).normalized);
            var facet = across > 0.04f ? 0.88f : across < -0.06f ? 0.74f : 0.96f;   // üç yüzey farklı tonda
            var f = new Color(facet * 0.9f, facet, 1f, 0.78f);
            if (Mathf.Abs(t - threshold) < 0.01f) f = new Color(1f, 1f, 1f, 0.9f);   // parçanın parlak kenarı
            return Color.Lerp(c, f, 0.85f);
        }

        // Kabuk alanına işaretli uzaklık: dikdörtgen; yalnız iki dış kenarın buluştuğu köşeler yuvarlak.
        static float ShellDistance(Vector2 p, Vector4 ext, int connected)
        {
            var left = p.x < 0f; var down = p.y < 0f;
            var hx = left ? ext.x : ext.y;
            var hy = down ? ext.z : ext.w;
            var outerX = (connected & (left ? 1 : 2)) == 0;
            var outerY = (connected & (down ? 4 : 8)) == 0;
            var r = outerX && outerY ? 0.16f : 0f;
            var q = new Vector2(Mathf.Abs(p.x) - (hx - r), Mathf.Abs(p.y) - (hy - r));
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
            return (p - (a + ab * t)).magnitude;
        }

        // ------------------------------------------------------------ ok
        // Arayüzle aynı dil (GameHud panelleri): lacivert gövde, açık camgöbeği kenar, dışta yumuşak neon hale; çift başlı,
        // yuvarlatılmış köşeli. Boy hücre cinsinden; uçlar ve kalınlık sabit, gövde uzar.
        const float ArrowHeadLength = 0.27f, ArrowHeadHalf = 0.25f, ArrowShaftHalf = 0.1f;
        const float ArrowRound = 0.065f, ArrowRim = 0.04f, ArrowGlow = 0.07f;
        static readonly Color ArrowTop = SceneArt.Hex(0x2A4596), ArrowBottom = SceneArt.Hex(0x16245A),
                              ArrowRimColor = SceneArt.Hex(0xC8F6FF), ArrowGlowColor = SceneArt.Hex(0x55DDFF);

        /// <summary>Tek eksen oku, toplam boyu 'length' hücre (yatay; dikey için 90° döndürülür).</summary>
        public static Sprite Arrow(float length)
        {
            length = Mathf.Round(length * 50f) / 50f;
            return Get($"arrow_{length:0.00}", () =>
            {
                var pad = ArrowGlow + 0.02f;
                var half = new Vector2(length / 2f + pad, ArrowHeadHalf + pad);
                return DrawRect(half, p => ArrowPixel(p, length / 2f));
            });
        }

        static Color ArrowPixel(Vector2 p, float half)
        {
            // Köşeleri yuvarlatmak için şekil ArrowRound kadar içeriden çizilip o kadar dışa büyütülür.
            var tip = half - ArrowRound;
            var neck = half - ArrowHeadLength;
            var w = ArrowHeadHalf - ArrowRound; var t = ArrowShaftHalf - ArrowRound;
            var poly = new[]
            {
                new Vector2(tip, 0f), new Vector2(neck, w), new Vector2(neck, t), new Vector2(-neck, t), new Vector2(-neck, w),
                new Vector2(-tip, 0f), new Vector2(-neck, -w), new Vector2(-neck, -t), new Vector2(neck, -t), new Vector2(neck, -w),
            };
            var d = PolygonDistance(p, poly) - ArrowRound;
            const float aa = 1.2f / Size;
            if (d > 0f)
            {
                var g = Mathf.Clamp01(1f - d / ArrowGlow);
                var glow = ArrowGlowColor; glow.a = 0.5f * g * g;
                var edge = ArrowRimColor; edge.a = Mathf.Clamp01(1f - d / aa);
                return glow.a > edge.a ? glow : edge;
            }
            if (-d < ArrowRim) return ArrowRimColor;
            var body = Color.Lerp(ArrowBottom, ArrowTop, Mathf.Clamp01(0.5f + p.y / (2f * ArrowHeadHalf)));
            if (-d < ArrowRim + aa) body = Color.Lerp(ArrowRimColor, body, (-d - ArrowRim) / aa);
            return body;
        }

        // İşaretli uzaklık: dışta artı, içte eksi (Inigo Quilez, çokgen).
        static float PolygonDistance(Vector2 p, Vector2[] v)
        {
            var d = Vector2.Dot(p - v[0], p - v[0]);
            var s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i]; var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                var c1 = p.y >= v[i].y; var c2 = p.y < v[j].y; var c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        // ------------------------------------------------------------ yardımcılar
        static Sprite Get(string key, System.Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out var s) && s) return s;
            return Cache[key] = make();
        }

        static Sprite Draw(System.Func<Vector2, Color> pixel)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
                pixels[y * Size + x] = pixel(new Vector2((x + 0.5f) / Size - 0.5f, (y + 0.5f) / Size - 0.5f));
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        // Dikdörtgen sprite: half yarı boyutları (birim = hücre), Size piksel/birim.
        static Sprite DrawRect(Vector2 half, System.Func<Vector2, Color> pixel)
        {
            int w = Mathf.CeilToInt(half.x * 2f * Size), h = Mathf.CeilToInt(half.y * 2f * Size);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                pixels[y * w + x] = pixel(new Vector2((x + 0.5f) / Size - w / (2f * Size), (y + 0.5f) / Size - h / (2f * Size)));
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Size);
        }

        static Vector2 Rotate(Vector2 p, float degrees)
        {
            var r = degrees * Mathf.Deg2Rad;
            return new Vector2(p.x * Mathf.Cos(r) - p.y * Mathf.Sin(r), p.x * Mathf.Sin(r) + p.y * Mathf.Cos(r));
        }
    }
}
