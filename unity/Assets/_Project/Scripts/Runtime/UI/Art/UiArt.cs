using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AstroFlowJam
{
    /// <summary>
    /// HUD görselleri kodla çizilir (dışarıdan görsel yok): kalın koyu kenarlı,
    /// dikey degradeli, alt kenarında koyu "dudak" ve üstte parlak yansıma şeridi olan yuvarlak kutular ve ikonlar.
    /// Dokular istenen piksel boyutunda üretilir ve önbelleğe alınır.
    /// </summary>
    public static partial class UiArt
    {
        public static readonly Color Ink = new(0.04f, 0.04f, 0.07f);                  // kenar ve yazı kenarı
        public static readonly Color BlueTop = new(0.36f, 0.80f, 1f);
        public static readonly Color BlueBottom = new(0.16f, 0.53f, 0.98f);
        public static readonly Color OrangeTop = new(1f, 0.66f, 0.22f);
        public static readonly Color OrangeBottom = new(0.93f, 0.42f, 0.06f);
        public static readonly Color YellowTop = new(1f, 0.86f, 0.32f);
        public static readonly Color YellowBottom = new(0.98f, 0.66f, 0.12f);
        public static readonly Color Navy = new(0.03f, 0.05f, 0.16f);                 // yazı kenarı (lacivert)
        public static readonly Color GlassTop = new(0.11f, 0.17f, 0.42f);
        public static readonly Color GlassBottom = new(0.05f, 0.08f, 0.24f);
        public static readonly Color GlassEdge = new(0.30f, 0.45f, 0.85f);
        public static readonly Color Cyan = new(0.33f, 0.87f, 1f);

        static readonly Dictionary<string, Sprite> Cache = new();
        static Font font;

        /// <summary>Oyunun yazı tipi: Exo 2 Bold (Resources/Fonts, OFL lisanslı). Bulunamazsa yerleşik yazı tipi; o zaman kalın stil istenir.</summary>
        public static bool IsBoldFace => Font && Font.name.StartsWith("Exo2");

        public static Font Font
        {
            get
            {
                if (font) return font;
                font = Resources.Load<Font>("Fonts/Exo2-Bold");
                return font ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        /// <summary>
        /// Sınırsız can işareti: düz, eşit kalınlıkta beyaz ∞ çizgisi ve lacivert kenar (Bernoulli lemniskatı, biraz
        /// yuvarlatılmış). Yazı tipine bağlı değil; her cihazda aynı görünür.
        /// </summary>
        public static Sprite Infinity()
        {
            const int W = 256, H = 150;
            const float Core = 10f, Edge = 7f, Scale = 106f, Round = 1.3f;
            if (Cache.TryGetValue("infinity", out var cached)) return cached;
            const int N = 240;
            var pts = new Vector2[N + 1];
            for (var i = 0; i <= N; i++)
            {
                var t = i * Mathf.PI * 2f / N;
                var s = Mathf.Sin(t);
                var k = 1f + s * s;
                pts[i] = new Vector2(W / 2f + Scale * Mathf.Cos(t) / k, H / 2f + Scale * Round * s * Mathf.Cos(t) / k);
            }
            return Cache["infinity"] = Draw(W, H, (x, y) =>
            {
                var p = new Vector2(x, y);
                var best = float.MaxValue;
                for (var i = 0; i < N; i++)
                {
                    var a = pts[i];
                    var ab = pts[i + 1] - a;
                    var u = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    best = Mathf.Min(best, (a + ab * u - p).sqrMagnitude);
                }
                var d = Mathf.Sqrt(best);
                if (d > Core + Edge + 1f) return Color.clear;
                var c = Color.Lerp(Color.white, Navy, Mathf.Clamp01(d - Core + 0.5f));
                return Fade(c, d - Core - Edge);
            });
        }

        /// <summary>Yuvarlak köşeli parlak kutu. radius = yükseklik/2 hap (pill) şekli verir.</summary>
        public static Sprite Box(int width, int height, float radius, Color top, Color bottom, float outline = 7f, Color? edge = null)
        {
            var edgeColor = edge ?? Ink;
            var key = $"box{width}x{height}r{radius}{top}{bottom}{outline}{edgeColor}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var lip = height * 0.09f;
            return Cache[key] = Draw(width, height, (x, y) =>
            {
                var d = RoundedBox(x, y, width, height, radius);
                if (d > 0.5f) return Color.clear;
                Color c;
                if (d > -outline) c = edgeColor;
                else
                {
                    var t = Mathf.Clamp01((y - outline) / (height - 2f * outline));
                    c = Color.Lerp(bottom, top, t);
                    // Alt dudak: iç kutunun altında koyu bant.
                    var inner = RoundedBox(x, y - lip, width, height - lip, radius);
                    if (y < height / 2f && inner > -outline) c = Color.Lerp(bottom, Ink, 0.35f);
                    // Üst yansıma: sol üstte yumuşak kenarlı yatay hap.
                    var gloss = new Vector2(Mathf.Max(width * 0.11f, height * 0.08f), height * 0.055f);
                    var g = Capsule(new Vector2(y - (height - outline - height * 0.15f), x - (outline + radius * 0.45f + gloss.x)), new Vector2(gloss.y, gloss.x));
                    c = Color.Lerp(c, Color.white, 0.5f * Mathf.Clamp01(0.5f - g));
                }
                return Fade(c, d);
            });
        }

        /// <summary>Lacivert cam panel (bölüm etiketi, süre): koyu degrade, ince parlak kenar, alt dudak ve yansıma yok.</summary>
        public static Sprite Glass(int width, int height, float radius, Color top, Color bottom, Color edge, float outline = 5f)
        {
            var key = $"glass{width}x{height}r{radius}{top}{bottom}{edge}{outline}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            return Cache[key] = Draw(width, height, (x, y) =>
            {
                var d = RoundedBox(x, y, width, height, radius);
                if (d > 0.5f) return Color.clear;
                var c = d > -outline ? edge : Color.Lerp(bottom, top, Mathf.Clamp01(y / height));
                // Kenarın hemen içinde ince koyu çizgi: cam kalınlığı.
                if (d <= -outline && d > -outline - 3f) c = Color.Lerp(c, Ink, 0.5f);
                return Fade(c, d);
            });
        }

        /// <summary>Kutunun çevresinde yumuşak ışık halesi (dışa doğru sönen renk).</summary>
        public static Sprite Glow(int width, int height, float radius, float spread, Color color)
        {
            var key = $"glow{width}x{height}r{radius}s{spread}{color}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            // İç şekil: doku 'spread' kadar içeri çekilmiş, köşe yarıçapı 'radius' olan kutu (daire için yarıçap = yarı boy).
            // Hale bu şeklin kenarından gerçek uzaklıkla dışa doğru yumuşakça söner; köşeler de yuvarlak kalır.
            var half = new Vector2(width / 2f - spread, height / 2f - spread);
            var r = Mathf.Min(radius, Mathf.Min(half.x, half.y));
            return Cache[key] = Draw(width, height, (x, y) =>
            {
                var q = new Vector2(Mathf.Abs(x - width / 2f) - (half.x - r), Mathf.Abs(y - height / 2f) - (half.y - r));
                var d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
                var k = Mathf.Clamp01(1f - d / spread);
                var c = color;
                c.a *= d <= 0f ? 1f : k * k * k;
                return c;
            });
        }

        /// <summary>Parlak daire (duraklat butonu).</summary>
        public static Sprite Circle(int size, Color top, Color bottom, float outline = 7f, Color? edge = null) =>
            Box(size, size, size / 2f, top, bottom, outline, edge);

        /// <summary>Beyaz ikon, koyu kenarlı: "pause", "restart", "prev", "next".</summary>
        public static Sprite Icon(string kind, int size = 128)
        {
            var key = $"icon{kind}{size}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var edge = size * 0.06f;
            return Cache[key] = Draw(size, size, (x, y) =>
            {
                var p = new Vector2(x / size - 0.5f, y / size - 0.5f) * size;   // piksel, merkez sıfır
                var d = Shape(kind, p, size);
                if (d > edge + 0.5f) return Color.clear;
                return d > 0f ? Fade(Ink, d - edge) : Color.white;
            });
        }

        /// <summary>
        /// Tema ikonu: üstte beyazdan altta camgöbeğine degrade dolgu (whiteFill ile düz beyaz), koyu lacivert kenar ve
        /// dışta ince neon hale.
        /// </summary>
        public static Sprite NeonIcon(string kind, int size = 128, bool whiteFill = false)
        {
            var key = $"neonicon{kind}{size}{whiteFill}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var edge = size * 0.05f;
            var halo = size * 0.09f;
            return Cache[key] = Draw(size, size, (x, y) =>
            {
                var p = new Vector2(x / size - 0.5f, y / size - 0.5f) * size;
                var d = Shape(kind, p, size);
                if (d <= 0f) return whiteFill ? Color.white : Color.Lerp(Cyan, Color.white, Mathf.Clamp01(y / size * 1.3f - 0.1f));
                if (d <= edge) return Fade(Navy, d - edge);
                var k = Mathf.Clamp01(1f - (d - edge) / halo);
                var c = Cyan;
                c.a = k * k * 0.55f;
                return c;
            });
        }

        static Sprite coinIcon;

        /// <summary>Coin simgesi: AFJ_Space_Coin modelinin önden render'ı (Resources/UI/Coin.png); yoksa çizilmiş coin.</summary>
        public static Sprite CoinIcon()
        {
            if (coinIcon) return coinIcon;
            var tex = Resources.Load<Texture2D>("UI/Coin");
            if (!tex) return coinIcon = Coin();
            return coinIcon = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Altın coin: radyal degrade, koyu altın kenar halkası, ortada parlak yıldız ve sol üstte yansıma.</summary>
        public static Sprite Coin(int size = 96)
        {
            var key = $"coin{size}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var gold = new Color(1f, 0.82f, 0.25f);
            var goldDark = new Color(0.86f, 0.52f, 0.08f);
            var rim = new Color(0.62f, 0.33f, 0.04f);
            return Cache[key] = Draw(size, size, (x, y) =>
            {
                var p = new Vector2(x - size / 2f, y - size / 2f);
                var r = p.magnitude;
                var R = size / 2f - 1.5f;
                if (r > R + 0.5f) return Color.clear;
                Color c;
                if (r > R - size * 0.05f) c = Navy;
                else if (r > R - size * 0.13f) c = Color.Lerp(goldDark, rim, 0.4f);
                else
                {
                    c = Color.Lerp(goldDark, gold, Mathf.Clamp01(0.5f + p.y / size + (1f - r / R) * 0.4f));
                    // Ortada beş köşeli yıldız (kabartma).
                    var a = Mathf.Atan2(p.y, p.x) + Mathf.PI / 2f;
                    var sector = Mathf.Repeat(a, 2f * Mathf.PI / 5f) - Mathf.PI / 5f;
                    var starR = Mathf.Lerp(size * 0.13f, size * 0.29f, Mathf.Cos(sector * 2.5f) * 0.5f + 0.5f);
                    if (r < starR) c = Color.Lerp(c, new Color(1f, 0.95f, 0.7f), 0.75f);
                }
                // Sol üst yansıma.
                var g = new Vector2(p.x + size * 0.16f, p.y - size * 0.18f).magnitude;
                if (g < size * 0.12f && r < R - size * 0.13f) c = Color.Lerp(c, Color.white, 0.5f * (1f - g / (size * 0.12f)));
                return Fade(c, r - R);
            });
        }

        // İkon şekline işaretli uzaklık (piksel, içeride negatif).
        static float Shape(string kind, Vector2 p, float s)
        {
            switch (kind)
            {
                case "close":
                {
                    // Çarpı: ±45° döndürülmüş iki kapsül.
                    var bar = new Vector2(s * 0.075f, s * 0.27f);
                    const float c = 0.70710678f;
                    var a = new Vector2(c * p.x + c * p.y, -c * p.x + c * p.y);
                    var b = new Vector2(c * p.x - c * p.y, c * p.x + c * p.y);
                    return Mathf.Min(Capsule(a, bar), Capsule(b, bar));
                }
                case "pause":
                {
                    var bar = new Vector2(s * 0.085f, s * 0.2f);
                    return Mathf.Min(Capsule(p - new Vector2(-s * 0.13f, 0f), bar), Capsule(p - new Vector2(s * 0.13f, 0f), bar));
                }
                case "restart":
                {
                    float radius = s * 0.22f, half = s * 0.055f, gapStart = 30f, gapEnd = 110f;
                    var angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                    if (angle < 0f) angle += 360f;
                    var ring = angle > gapStart && angle < gapEnd ? 1e5f : Mathf.Abs(p.magnitude - radius) - half;
                    var a = gapStart * Mathf.Deg2Rad;
                    var tip = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    var tangent = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                    var normal = tip.normalized;
                    var arrow = Triangle(p, tip + tangent * s * 0.15f, tip + normal * s * 0.12f, tip - normal * s * 0.12f);
                    return Mathf.Min(ring, arrow);
                }
                case "clock":
                {
                    var ring = Mathf.Abs(p.magnitude - s * 0.3f) - s * 0.055f;
                    var hand1 = Capsule(p - new Vector2(0f, s * 0.08f), new Vector2(s * 0.045f, s * 0.12f));
                    var q = p - new Vector2(s * 0.07f, 0f);
                    var hand2 = Capsule(new Vector2(q.y, q.x), new Vector2(s * 0.045f, s * 0.11f));
                    return Mathf.Min(ring, Mathf.Min(hand1, hand2));
                }
                case "plus":
                {
                    var arm = new Vector2(s * 0.07f, s * 0.26f);
                    return Mathf.Min(Capsule(p, arm), Capsule(new Vector2(p.y, p.x), arm));
                }
                case "prev":
                    return Triangle(p, new Vector2(-s * 0.2f, 0f), new Vector2(s * 0.14f, s * 0.2f), new Vector2(s * 0.14f, -s * 0.2f));
                default:
                    return Triangle(p, new Vector2(s * 0.2f, 0f), new Vector2(-s * 0.14f, -s * 0.2f), new Vector2(-s * 0.14f, s * 0.2f));
            }
        }

        static float Capsule(Vector2 p, Vector2 half)
        {
            var r = half.x;
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Max(Mathf.Abs(p.y) - (half.y - r), 0f));
            return q.magnitude - r;
        }

        // Üçgene işaretli uzaklık (içeride negatif).
        static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) < 0f) (b, c) = (c, b);
            float Edge(Vector2 u, Vector2 v)
            {
                var e = v - u;
                return Vector2.Dot(p - u, new Vector2(e.y, -e.x).normalized);
            }
            return Mathf.Max(Edge(a, b), Edge(b, c), Edge(c, a));
        }

        // Kutuya işaretli uzaklık (piksel koordinatı, içeride negatif).
        static float RoundedBox(float x, float y, float w, float h, float r)
        {
            var q = new Vector2(Mathf.Abs(x - w / 2f) - (w / 2f - r), Mathf.Abs(y - h / 2f) - (h / 2f - r));
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static Color Fade(Color c, float d)
        {
            c.a *= Mathf.Clamp01(0.5f - d);
            return c;
        }

        static Sprite Draw(int w, int h, System.Func<float, float, Color> pixel)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                pixels[y * w + x] = pixel(x + 0.5f, y + 0.5f);
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
