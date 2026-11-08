using UnityEngine;

namespace AstroFlowJam
{
    public static partial class UiArt
    {
        /// <summary>
        /// Buz kaplaması (zaman dondurma): yuvarlak kutu biçiminde yarı saydam buz. Kenarlardan içe doğru yoğunlaşan kırağı,
        /// Voronoi hücreleriyle kristal yüzeyler ve parlak yüzey sınırları, kenardan içeri uzanan çatlaklar, sol üstten
        /// çapraz parlama, birkaç yıldız parıltısı ve kalın parlak buz kenarı. Ortası daha saydam: altındaki yazı okunur.
        /// </summary>
        public static Sprite Frost(int width, int height, float radius, int seed = 7)
        {
            var key = $"frost{width}x{height}r{radius}s{seed}";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            // Kristal tohumları (yüzeyler): kenara yakın daha sık.
            var seeds = new Vector2[34];
            var shade = new float[seeds.Length];
            for (var i = 0; i < seeds.Length; i++)
            {
                var edgeBias = R() < 0.65f;
                var x = R() * width;
                var y = edgeBias ? (R() < 0.5f ? R() * height * 0.3f : height - R() * height * 0.3f) : R() * height;
                seeds[i] = new Vector2(x, y);
                shade[i] = R();
            }
            // Çatlaklar: kenardaki bir noktadan içeri kırık çizgiler.
            var cracks = new Vector2[6][];
            for (var i = 0; i < cracks.Length; i++)
            {
                var pts = new Vector2[4];
                var side = i % 4;
                pts[0] = side switch
                {
                    0 => new Vector2(R() * width, 0f), 1 => new Vector2(R() * width, height),
                    2 => new Vector2(0f, R() * height), _ => new Vector2(width, R() * height),
                };
                var dir = (new Vector2(width / 2f, height / 2f) - pts[0]).normalized;
                for (var k = 1; k < pts.Length; k++)
                {
                    var turn = (R() - 0.5f) * 1.4f;
                    var d = new Vector2(dir.x * Mathf.Cos(turn) - dir.y * Mathf.Sin(turn), dir.x * Mathf.Sin(turn) + dir.y * Mathf.Cos(turn));
                    pts[k] = pts[k - 1] + d * (height * (0.16f + 0.14f * R()));
                }
                cracks[i] = pts;
            }
            var sparkles = new Vector3[5];
            for (var i = 0; i < sparkles.Length; i++) sparkles[i] = new Vector3(R() * width, R() * height, 3f + 4f * R());

            const float rim = 4f;
            return Cache[key] = Draw(width, height, (x, y) =>
            {
                var d = RoundedBox(x, y, width, height, radius);
                if (d > 0.5f) return Color.clear;
                var inside = -d;                                         // kenardan uzaklık (piksel)
                var p = new Vector2(x, y);

                // Kırağı yoğunluğu: kenarda tam, ortaya doğru azalır; gürültü tüylü bir sınır verir.
                var n = Noise(x * 0.09f, y * 0.09f) * 0.6f + Noise(x * 0.23f, y * 0.23f) * 0.4f;
                var reach = height * 0.42f;
                var frost = Mathf.Clamp01(1.15f - inside / reach + (n - 0.5f) * 0.9f);

                // Kristal yüzeyler: en yakın iki tohum; sınırları parlak çizgi, her yüzey biraz farklı tonda.
                float d1 = float.MaxValue, d2 = float.MaxValue; var nearest = 0;
                for (var i = 0; i < seeds.Length; i++)
                {
                    var dd = (seeds[i] - p).sqrMagnitude;
                    if (dd < d1) { d2 = d1; d1 = dd; nearest = i; }
                    else if (dd < d2) d2 = dd;
                }
                var facetEdge = Mathf.Sqrt(d2) - Mathf.Sqrt(d1);

                var body = Color.Lerp(new Color(0.55f, 0.85f, 1f), new Color(0.78f, 0.95f, 1f), shade[nearest]);
                var alpha = Mathf.Lerp(0.28f, 0.82f, frost);
                var c = new Color(body.r, body.g, body.b, alpha);
                // Kırağı beyazı.
                c = Color.Lerp(c, new Color(0.96f, 0.99f, 1f, 0.92f), frost * frost * 0.75f);
                // Yüzey sınırları (yalnız kırağılı bölgede belirgin).
                if (facetEdge < 1.6f) c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.95f), (1f - facetEdge / 1.6f) * (0.25f + 0.6f * frost));

                // Çatlaklar: ince beyaz çizgi, yanında koyu camgöbeği gölge.
                var crack = float.MaxValue;
                foreach (var pts in cracks)
                    for (var k = 0; k + 1 < pts.Length; k++) crack = Mathf.Min(crack, Segment(p, pts[k], pts[k + 1]));
                if (crack < 1.2f) c = Color.Lerp(c, new Color(1f, 1f, 1f, 1f), 0.85f);
                else if (crack < 2.6f) c = Color.Lerp(c, new Color(0.25f, 0.55f, 0.85f, 0.9f), 0.35f);

                // Sol üstten çapraz parlama şeridi.
                var sheen = Mathf.Abs((x / width) - (1f - y / height) * 0.55f - 0.12f);
                if (sheen < 0.06f) c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.9f), 0.35f * (1f - sheen / 0.06f));

                // Yıldız parıltıları.
                foreach (var s in sparkles)
                {
                    var q = new Vector2(Mathf.Abs(x - s.x), Mathf.Abs(y - s.y));
                    var star = Mathf.Min(q.x * 3f + q.y, q.y * 3f + q.x);
                    if (star < s.z) c = Color.Lerp(c, Color.white, 1f - star / s.z);
                }

                // Kalın buz kenarı: üstte beyaz, altta camgöbeği; hemen içinde ince koyu çizgi.
                if (inside < rim)
                {
                    var t = Mathf.Clamp01(y / height);
                    c = Color.Lerp(new Color(0.45f, 0.8f, 1f, 1f), new Color(1f, 1f, 1f, 1f), t);
                }
                else if (inside < rim + 2f) c = Color.Lerp(c, new Color(0.2f, 0.45f, 0.75f, 1f), 0.4f);
                return Fade(c, d);
            });
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return (a + ab * t - p).magnitude;
        }

        // Yumuşak değer gürültüsü (0..1).
        static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            float H(int a, int b) { var h = (a * 374761393 + b * 668265263) ^ 0x5bd1e995; h = (h ^ (h >> 13)) * 1274126177; return (h & 0xffff) / 65535f; }
            return Mathf.Lerp(Mathf.Lerp(H(xi, yi), H(xi + 1, yi), u), Mathf.Lerp(H(xi, yi + 1), H(xi + 1, yi + 1), u), v);
        }
    }
}
