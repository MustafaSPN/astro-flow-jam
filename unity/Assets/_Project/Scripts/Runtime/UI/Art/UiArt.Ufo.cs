using UnityEngine;

namespace AstroFlowJam
{
    public static partial class UiArt
    {
        /// <summary>
        /// Yer tutucu UFO (yandan, hafif üstten): metal disk (üstte açık, altta koyu degrade, lacivert kenar), cam kubbe
        /// (camgöbeği, parlama lekesi), disk üzerinde renkli ışık sırası ve altta parlayan çekim ağzı.
        /// </summary>
        public static Sprite Ufo(int width, int height)
        {
            var key = $"ufo{width}x{height}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var lights = new[] { new Color(1f, 0.36f, 0.66f), new Color(0.36f, 0.95f, 1f), new Color(1f, 0.9f, 0.36f) };
            float W = width, H = height;
            return Cache[key] = Draw(width, height, (x, y) =>
            {
                var c = Color.clear;
                // Çekim ağzı (diskin altında).
                var m = Ellipse(x, y, W * 0.5f, H * 0.2f, W * 0.15f, H * 0.07f);
                if (m < 1f) c = Color.Lerp(new Color(0.6f, 1f, 0.95f), new Color(0.2f, 0.75f, 0.9f), m);
                // Disk.
                var d = Ellipse(x, y, W * 0.5f, H * 0.36f, W * 0.48f, H * 0.16f);
                if (d < 1f)
                {
                    var t = Mathf.Clamp01((y - H * 0.2f) / (H * 0.32f));
                    c = Color.Lerp(new Color(0.32f, 0.36f, 0.5f), new Color(0.85f, 0.88f, 0.96f), t);
                    if (d > 0.86f) c = Ink;                                                     // kenar
                    else if (Mathf.Abs(y - H * 0.35f) < H * 0.012f) c = Color.Lerp(c, Ink, 0.5f); // bant çizgisi
                    // Işık sırası.
                    for (var i = 0; i < 7; i++)
                    {
                        var lx = W * (0.2f + 0.1f * i);
                        var ly = H * 0.3f;
                        var r = new Vector2(x - lx, y - ly).magnitude;
                        if (r < H * 0.035f) c = lights[i % lights.Length];
                        else if (r < H * 0.05f) c = Color.Lerp(c, lights[i % lights.Length], 0.5f);
                    }
                }
                // Kubbe (diskin üstünde).
                var k = Ellipse(x, y, W * 0.5f, H * 0.5f, W * 0.2f, H * 0.3f);
                if (k < 1f && y > H * 0.44f)
                {
                    var t = Mathf.Clamp01((y - H * 0.44f) / (H * 0.36f));
                    c = Color.Lerp(new Color(0.2f, 0.6f, 0.85f), new Color(0.6f, 0.95f, 1f), t);
                    if (k > 0.86f) c = Ink;
                    var g = Ellipse(x, y, W * 0.44f, H * 0.66f, W * 0.05f, H * 0.06f);       // parlama lekesi
                    if (g < 1f) c = Color.Lerp(c, Color.white, 0.8f * (1f - g));
                }
                return c;
            });
        }

        /// <summary>Çekim ışığı: yukarıda dar, aşağıda geniş; üstte parlak, aşağı doğru sönen camgöbeği koni.</summary>
        public static Sprite TractorBeam(int width, int height)
        {
            var key = $"beam{width}x{height}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            return Cache[key] = Draw(width, height, (x, y) =>
            {
                var t = 1f - y / height;                                   // 0 üstte, 1 altta
                var half = Mathf.Lerp(width * 0.18f, width * 0.5f, t);
                var dx = Mathf.Abs(x - width / 2f);
                if (dx > half) return Color.clear;
                var edge = Mathf.Clamp01((half - dx) / (width * 0.08f));
                var a = (1f - t) * 0.55f * edge + 0.08f * edge;
                var stripe = 0.5f + 0.5f * Mathf.Sin(y * 0.12f);
                return new Color(0.55f, 1f, 0.95f, a * (0.75f + 0.25f * stripe));
            });
        }

        // Elips uzaklığı: 1 kenar, <1 içeride.
        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            var dx = (x - cx) / rx; var dy = (y - cy) / ry;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
