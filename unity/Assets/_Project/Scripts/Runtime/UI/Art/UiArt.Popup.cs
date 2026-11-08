using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Bölüm sonu popup'ının görselleri: kodla çizilen ışık hüzmeleri ve parıltı yıldızı, Resources/UI altındaki hazır
    /// görseller (çerçeve, başlık plakası, ödül kutusu, kanatlı kupa).
    /// </summary>
    public static partial class UiArt
    {
        static readonly Color GoldLight = new(1f, 0.9f, 0.42f);

        /// <summary>Merkezden dışa doğru sönen sarı ışık hüzmeleri (döndürülerek kullanılır).</summary>
        public static Sprite Rays(int size = 512, int count = 12)
        {
            var key = $"rays{size}{count}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            return Cache[key] = Draw(size, size, (x, y) =>
            {
                var p = new Vector2(x - size / 2f, y - size / 2f);
                var r = p.magnitude / (size / 2f);
                if (r > 1f) return Color.clear;
                var a = Mathf.Atan2(p.y, p.x) / (Mathf.PI * 2f) * count;
                var ray = Mathf.Clamp01((0.5f - Mathf.Abs(Mathf.Repeat(a, 1f) - 0.5f)) * 4f - 0.6f);
                var fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);
                var c = Color.Lerp(GoldLight, Color.white, 0.3f);
                c.a = ray * fade * 0.85f + Mathf.Clamp01(1f - r * 1.6f) * 0.35f;
                return c;
            });
        }

        /// <summary>Dört köşeli parıltı yıldızı.</summary>
        public static Sprite Sparkle(int size = 64)
        {
            var key = $"sparkle{size}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            return Cache[key] = Draw(size, size, (x, y) =>
            {
                var p = new Vector2(Mathf.Abs(x - size / 2f), Mathf.Abs(y - size / 2f)) / (size / 2f);
                var d = Mathf.Pow(p.x, 0.5f) + Mathf.Pow(p.y, 0.5f);
                var a = Mathf.Clamp01((1f - d) * 3f);
                var c = Color.Lerp(GoldLight, Color.white, 0.5f);
                c.a = a;
                return c;
            });
        }

        /// <summary>
        /// Resources/UI altındaki hazır görsel (ör. popup_frame). border: 9-slice kenarları (piksel; sol, alt, sağ, üst),
        /// köşeleri bozmadan farklı oranlara gerilebilsin diye. Yoksa null.
        /// </summary>
        public static Sprite Asset(string name, Vector4 border = default)
        {
            var key = $"asset{name}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>("UI/" + name);
            if (!tex) return null;
            return Cache[key] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
        }

    }
}
