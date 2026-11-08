using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Sahnedeki 2D katmanlar (SpriteRenderer, kodla çizilen dokular): uzay arka planı, bandın metal çerçevesi ve
    /// ışıkları, bekleme yeri paneli ve kesik çizgili boş yerler, tahtanın arkasındaki koyu zemin. Renkler tasarımdaki
    /// lacivert / metal / camgöbeği paletidir. Dokular dünya ölçüsüne göre üretilir ve önbelleğe alınır.
    /// </summary>
    public static class SceneArt
    {
        const float PixelsPerUnit = 90f;

        static readonly Color SkyTop = Hex(0x0F1846);
        static readonly Color SkyBottom = Hex(0x080D2B);
        static readonly Color RailDark = Hex(0x0C1129);
        static readonly Color RailBody = Hex(0x3A4467);
        static readonly Color RailLight = Hex(0x7482AE);
        static readonly Color RailGroove = Hex(0x1C2340);
        static readonly Color Screen = Hex(0x020309);   // resmin (karartılmış) zeminiyle aynı ton
        static readonly Color Clamp = Hex(0x5B6584);
        public static readonly Color Cyan = Hex(0x55DDFF);
        static readonly Color PanelFill = Hex(0x141C45);
        static readonly Color PanelEdge = Hex(0x2C3974);
        static readonly Color DashColor = Hex(0x34437F);
        static readonly Color BoardBack = Hex(0x111838);

        static readonly Dictionary<string, Sprite> Cache = new();

        public static Color Hex(int rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        /// <summary>Verilen sprite'ı 'parent' altına yerleştirir; sprite dünya ölçüsünde üretildiği için ölçek 1'dir.</summary>
        public static SpriteRenderer Place(Sprite sprite, string name, Transform parent, Vector3 localPosition, int order = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>
        /// Bandın çerçevesi: yuvarlak köşeli dikdörtgen raylar (orta hat yarı genişlik hw, yarı yükseklik hh, köşe r),
        /// içte koyu ekran, raylarda camgöbeği ışıklar ve gri kelepçeler, dışta yumuşak gölge.
        /// </summary>
        public static Sprite Frame(float hw, float hh, float r, float rail)
        {
            var key = $"frame{hw:F2}{hh:F2}{r:F2}{rail:F2}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var pad = rail / 2f + 0.35f;
            var w = Mathf.CeilToInt((hw + pad) * 2f * PixelsPerUnit);
            var h = Mathf.CeilToInt((hh + pad) * 2f * PixelsPerUnit);
            var half = rail / 2f;
            var lights = new List<(Vector2 center, Vector2 half)>();
            foreach (var sx in new[] { -1f, 1f })
            {
                var inner = -half * 0.55f;
                lights.Add((new Vector2(sx * hw * 0.4f, hh + inner), new Vector2(0.17f, 0.045f)));
                lights.Add((new Vector2(sx * hw * 0.4f, -hh - inner), new Vector2(0.17f, 0.045f)));
                lights.Add((new Vector2(sx * (hw + inner), hh * 0.55f), new Vector2(0.045f, 0.17f)));
                lights.Add((new Vector2(sx * (hw + inner), -hh * 0.55f), new Vector2(0.045f, 0.17f)));
            }
            var clamps = new List<(Vector2 center, Vector2 half)>
            {
                (new Vector2(-hw, 0f), new Vector2(half * 1.15f, 0.2f)),
                (new Vector2(hw, 0f), new Vector2(half * 1.15f, 0.2f)),
            };
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sy in new[] { -1f, 1f })
                clamps.Add((new Vector2(sx * (hw - r * 0.35f), sy * (hh + 0.0f)), new Vector2(0.14f, half * 1.15f)));

            return Cache[key] = Draw(w, h, p =>
            {
                var d = RoundedBox(p, new Vector2(hw, hh), r);
                Color c;
                if (d < -half) c = Screen;
                else if (d <= half)
                {
                    var t = d / half;                                   // −1 iç kenar, +1 dış kenar
                    if (Mathf.Abs(t) > 0.86f) c = RailDark;
                    else if (Mathf.Abs(t + 0.05f) < 0.13f) c = RailGroove;
                    else
                    {
                        c = Color.Lerp(RailBody, RailLight, Mathf.Clamp01(1f - Mathf.Abs(t + 0.45f) * 3f) * 0.8f);
                        c = Color.Lerp(c, RailDark, Mathf.Clamp01(t - 0.4f) * 0.7f);
                    }
                    foreach (var (center, size) in clamps)
                        if (Box(p - center, size, 0.05f) < 0f)
                            c = Box(p - center, size, 0.05f) > -0.03f ? RailDark : Color.Lerp(Clamp, RailLight, Mathf.Clamp01((p.y - center.y) / size.y + 0.3f) * 0.5f);
                }
                else
                {
                    // Dış gölge.
                    var a = Mathf.Clamp01(1f - (d - half) / 0.3f) * 0.5f;
                    c = new Color(0f, 0f, 0.02f, a * a);
                }
                // Işıklar: çekirdek beyaza yakın camgöbeği, çevresinde hale.
                foreach (var (center, size) in lights)
                {
                    var ld = Box(p - center, size, Mathf.Min(size.x, size.y));
                    if (ld < 0f) c = Color.Lerp(Cyan, Color.white, 0.35f);
                    else if (ld < 0.22f && d > -half - 0.3f)
                    {
                        var g = Mathf.Pow(1f - ld / 0.22f, 2f) * 0.75f;
                        c = new Color(Mathf.Lerp(c.r, Cyan.r, g), Mathf.Lerp(c.g, Cyan.g, g), Mathf.Lerp(c.b, Cyan.b, g), Mathf.Max(c.a, g));
                    }
                }
                return c;
            });
        }

        /// <summary>Bekleme yerlerinin oturduğu koyu, ince kenarlı panel.</summary>
        public static Sprite Panel(float width, float height, float radius)
        {
            var key = $"panel{width:F2}{height:F2}{radius:F2}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var pad = 0.25f;
            var size = new Vector2(width / 2f, height / 2f);
            return Cache[key] = Draw(Mathf.CeilToInt((width + pad * 2f) * PixelsPerUnit), Mathf.CeilToInt((height + pad * 2f) * PixelsPerUnit), p =>
            {
                var d = Box(p, size, radius);
                if (d > 0f)
                {
                    var a = Mathf.Clamp01(1f - d / pad) * 0.45f;
                    return new Color(0f, 0f, 0.02f, a * a);
                }
                if (d > -0.045f) return PanelEdge;
                var t = Mathf.Clamp01((p.y + size.y) / (2f * size.y));
                var c = Color.Lerp(PanelFill * 0.85f, PanelFill * 1.15f, t);
                c.a = 0.96f;
                return c;
            });
        }

        /// <summary>Boş bekleme yeri: kesik çizgili yuvarlak kare.</summary>
        public static Sprite DashedSlot(float size)
        {
            var key = $"slot{size:F2}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var half = new Vector2(size / 2f, size / 2f);
            var px = Mathf.CeilToInt((size + 0.1f) * PixelsPerUnit);
            return Cache[key] = Draw(px, px, p =>
            {
                var d = Box(p, half - Vector2.one * 0.02f, size * 0.14f);
                var line = Mathf.Abs(d) - 0.012f;
                if (line > 0.01f) return d < 0f ? new Color(0.05f, 0.07f, 0.18f, 0.55f) : Color.clear;
                // Kenar boyunca kesikler: kenarın uzun ekseni boyunca konum.
                var along = Mathf.Abs(p.x) > Mathf.Abs(p.y) ? p.y : p.x;
                var dash = Mathf.Repeat(along * 9f + 0.25f, 1f) < 0.55f;
                if (!dash) return d < 0f ? new Color(0.05f, 0.07f, 0.18f, 0.55f) : Color.clear;
                var c = DashColor;
                c.a = Mathf.Clamp01(0.5f - line * PixelsPerUnit);
                return c;
            });
        }

        /// <summary>Tahtanın arkasındaki koyu zemin (çerçeveden biraz taşar, yumuşak gölgeli).</summary>
        /// <summary>
        /// Tahtanın arkasındaki koyu zemin ve çerçeve çizgisi; tahtanın şeklini izler: her oynanabilir hücre 'margin' kadar
        /// büyütülür (dış köşeler 0,3 yarıçapla yuvarlak), delikli köşe ve çentiklerde zemin de duvar gibi içe girer.
        /// Sprite'ın merkezi tahtanın merkezidir.
        /// </summary>
        public static Sprite BoardBackdrop(AstroFlowJam.Core.BoardModel board, float margin)
        {
            int W = board.Width, H = board.Height;
            var sb = new System.Text.StringBuilder($"boardback{W}x{H}:{margin:F2}:");
            for (var x = 0; x < W; x++) for (var y = 0; y < H; y++) if (board.IsHole(new Vector2Int(x, y))) sb.Append(x).Append(',').Append(y).Append(';');
            var key = sb.ToString();
            if (Cache.TryGetValue(key, out var cached)) return cached;
            const float pad = 0.35f, radius = 0.3f;
            var grow = margin - radius;
            var reach = Mathf.CeilToInt(margin + pad) + 1;
            // İç delikler panelin içinde sayılır: kenar çizgisi yalnız tahtanın en dış sınırını izler, iç deliklerin içine çizilmez.
            bool Open(int x, int y) => x >= 0 && y >= 0 && x < W && y < H &&
                                       (!board.IsHole(new Vector2Int(x, y)) || board.IsInteriorHole(new Vector2Int(x, y)));
            return Cache[key] = Draw(Mathf.CeilToInt((W + (margin + pad) * 2f) * PixelsPerUnit), Mathf.CeilToInt((H + (margin + pad) * 2f) * PixelsPerUnit), p =>
            {
                // Tahta yerel koordinatı (sol alt köşe 0,0); yalnız yakındaki hücrelere bakılır.
                var q = p + new Vector2(W / 2f, H / 2f);
                var d = float.MaxValue;
                int cx = Mathf.FloorToInt(q.x), cy = Mathf.FloorToInt(q.y);
                for (var x = cx - reach; x <= cx + reach; x++)
                    for (var y = cy - reach; y <= cy + reach; y++)
                        if (Open(x, y)) d = Mathf.Min(d, Box(q - new Vector2(x + 0.5f, y + 0.5f), new Vector2(0.5f + grow, 0.5f + grow), 0f));
                d -= radius;
                // İçe dönük köşeler de dış köşelerle aynı yarıçapla yuvarlanır: köşe noktasını çevreleyen dört hücreden üçü
                // açıksa, eksik hücre yönündeki köşe karesine (yarıçap kadar) içbükey dolgu eklenir.
                for (var vx = cx - 1; vx <= cx + 2; vx++)
                    for (var vy = cy - 1; vy <= cy + 2; vy++)
                    {
                        bool bl = Open(vx - 1, vy - 1), br = Open(vx, vy - 1), tl = Open(vx - 1, vy), tr = Open(vx, vy);
                        if ((bl ? 1 : 0) + (br ? 1 : 0) + (tl ? 1 : 0) + (tr ? 1 : 0) != 3) continue;
                        var diag = !bl ? new Vector2(-1f, -1f) : !br ? new Vector2(1f, -1f) : !tl ? new Vector2(-1f, 1f) : new Vector2(1f, 1f);
                        var corner = new Vector2(vx, vy) + diag * margin;
                        var local = Vector2.Scale(q - corner, diag);   // köşe karesinde iki bileşen de 0..yarıçap
                        // Kare, eski keskin köşenin kenar çizgisi şeridini de kapsar: o çizgi kaybolur, yalnız yay kalır.
                        if (local.x < -0.06f || local.y < -0.06f || local.x > radius || local.y > radius) continue;
                        d = Mathf.Min(d, radius - (local - new Vector2(radius, radius)).magnitude);
                    }
                if (d > 0f)
                {
                    var a = Mathf.Clamp01(1f - d / pad) * 0.55f;
                    return new Color(0f, 0f, 0.02f, a * a);
                }
                return d > -0.04f ? PanelEdge : BoardBack;
            });
        }

        /// <summary>Uzay arka planı: dikey degrade, bulutsu lekeler, yıldızlar, parıltılar ve iki gezegen. Dikey oran 1:2,17.</summary>
        public static Sprite Sky()
        {
            const string key = "sky";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            const int w = 540, h = 1170;
            var rng = new System.Random(7);
            var nebulae = new[]
            {
                (new Vector2(0.15f, 0.35f), 0.35f, Hex(0x3A2C86), 0.45f),
                (new Vector2(0.85f, 0.55f), 0.30f, Hex(0x1E3D97), 0.40f),
                (new Vector2(0.75f, 0.12f), 0.28f, Hex(0x34287A), 0.35f),
                (new Vector2(0.25f, 0.85f), 0.25f, Hex(0x1F3488), 0.35f),
            };
            var stars = new List<(Vector2, float, float)>();
            for (var i = 0; i < 260; i++)
                stars.Add((new Vector2((float)rng.NextDouble() * w, (float)rng.NextDouble() * h), 0.6f + (float)rng.NextDouble() * 1.1f, 0.35f + (float)rng.NextDouble() * 0.65f));
            var sparkles = new List<(Vector2, float)>();
            for (var i = 0; i < 9; i++)
                sparkles.Add((new Vector2((float)rng.NextDouble() * w, (float)rng.NextDouble() * h), 7f + (float)rng.NextDouble() * 6f));
            var bigPlanet = (center: new Vector2(-0.02f * w, 0.79f * h), radius: 0.13f * w);
            var smallPlanet = (center: new Vector2(0.95f * w, 0.88f * h), radius: 0.035f * w);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var uv = new Vector2((float)x / w, (float)y / h);
                var c = Color.Lerp(SkyBottom, SkyTop, uv.y);
                foreach (var (center, radius, color, strength) in nebulae)
                {
                    var q = new Vector2((uv.x - center.x), (uv.y - center.y) * h / w);
                    var g = Mathf.Exp(-q.sqrMagnitude / (radius * radius)) * strength;
                    c = Color.Lerp(c, color, g);
                }
                pixels[y * w + x] = c;
            }
            void Blend(int x, int y, Color color, float a)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || a <= 0f) return;
                var i = y * w + x;
                pixels[i] = Color.Lerp(pixels[i], color, Mathf.Clamp01(a));
            }
            foreach (var (p, radius, brightness) in stars)
                for (var dy = -2; dy <= 2; dy++)
                for (var dx = -2; dx <= 2; dx++)
                {
                    var dist = new Vector2(dx + 0.5f - Mathf.Repeat(p.x, 1f), dy + 0.5f - Mathf.Repeat(p.y, 1f)).magnitude;
                    Blend((int)p.x + dx, (int)p.y + dy, Color.white, (radius - dist + 0.5f) * brightness);
                }
            foreach (var (p, size) in sparkles)
                for (var dy = -(int)size; dy <= size; dy++)
                for (var dx = -(int)size; dx <= size; dx++)
                {
                    var ax = Mathf.Abs(dx) / size;
                    var ay = Mathf.Abs(dy) / size;
                    var a = Mathf.Max(Mathf.Clamp01(1f - ax) * Mathf.Clamp01(1f - ay * 6f), Mathf.Clamp01(1f - ay) * Mathf.Clamp01(1f - ax * 6f));
                    a = Mathf.Max(a, Mathf.Clamp01(1f - new Vector2(dx, dy).magnitude / (size * 0.35f)));
                    Blend((int)p.x + dx, (int)p.y + dy, Hex(0xFFF2D0), a);
                }
            foreach (var (center, radius) in new[] { bigPlanet, smallPlanet })
            {
                var r = Mathf.CeilToInt(radius) + 2;
                for (var dy = -r; dy <= r; dy++)
                for (var dx = -r; dx <= r; dx++)
                {
                    var q = new Vector2(dx, dy);
                    var d = q.magnitude - radius;
                    if (d > 1f) continue;
                    // Sağ üstten ışık: aydınlık tarafa doğru açılan mor ton, kenarda ince parlak hilal.
                    var lit = Mathf.Clamp01(Vector2.Dot(q / radius, new Vector2(0.6f, 0.5f)) * 0.8f + 0.4f);
                    var col = Color.Lerp(Hex(0x241A55), Hex(0x6A54C2), lit);
                    var band = Mathf.Sin((dy / radius) * 9f + dx / radius * 2f) * 0.5f + 0.5f;
                    col = Color.Lerp(col, Hex(0x4A3A9A), band * 0.25f);
                    if (d > -2.5f) col = Color.Lerp(col, Hex(0x9D8CFF), lit * 0.8f);
                    Blend((int)center.x + dx, (int)center.y + dy, col, Mathf.Clamp01(0.5f - d) * 0.9f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Cache[key] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static float RoundedBox(Vector2 p, Vector2 half, float r) => Box(p, half, r);

        // Yuvarlak köşeli kutuya işaretli uzaklık (dünya birimi, içeride negatif). half: yarı boyutlar, r: köşe yarıçapı.
        static float Box(Vector2 p, Vector2 half, float r)
        {
            r = Mathf.Min(r, Mathf.Min(half.x, half.y));
            var q = new Vector2(Mathf.Abs(p.x) - (half.x - r), Mathf.Abs(p.y) - (half.y - r));
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        // Doku merkezli dünya koordinatıyla çizer; kenarlar piksel boyutunda yumuşatılmaz (renk fonksiyonu kendi geçişini yapar).
        static Sprite Draw(int w, int h, System.Func<Vector2, Color> pixel)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                pixels[y * w + x] = pixel(new Vector2((x + 0.5f - w / 2f) / PixelsPerUnit, (y + 0.5f - h / 2f) / PixelsPerUnit));
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }
    }
}
