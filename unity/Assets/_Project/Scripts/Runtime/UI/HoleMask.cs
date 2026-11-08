using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Ekran boyutunda (dörtte bir çözünürlük) karartma dokusu: verilen ekran dikdörtgenleri yuvarlak köşeli, yumuşak
    /// kenarlı delik olarak açık kalır. Doku, sprite ve piksel dizisi tekrar kullanılır.
    /// </summary>
    public sealed class HoleMask
    {
        const int Downscale = 4;                          // yumuşak kenar zaten bulanık
        const float Radius = 34f, Feather = 14f;          // ekran pikseli
        static readonly Color Dim = new(0.01f, 0.02f, 0.06f, 1f);

        Texture2D texture;
        Color32[] pixels;

        public Sprite Sprite { get; private set; }

        /// <param name="holes">Açık kalacak alanlar, ekran pikseli (sol alt köken).</param>
        public Sprite Paint(IReadOnlyList<Rect> holes, float alpha)
        {
            int w = Mathf.Max(1, Screen.width / Downscale), h = Mathf.Max(1, Screen.height / Downscale);
            if (!texture || texture.width != w || texture.height != h)
            {
                if (texture) Object.Destroy(texture);
                if (Sprite) Object.Destroy(Sprite);
                texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                Sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
                pixels = new Color32[w * h];
            }
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var p = new Vector2((x + 0.5f) * Downscale, (y + 0.5f) * Downscale);   // ekran pikseli
                var open = 0f;
                foreach (var r in holes)
                {
                    var half = r.size / 2f;
                    var q = new Vector2(Mathf.Abs(p.x - r.center.x), Mathf.Abs(p.y - r.center.y)) - half + Vector2.one * Radius;
                    var d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - Radius;
                    open = Mathf.Max(open, Mathf.Clamp01(0.5f - d / Feather));
                }
                pixels[y * w + x] = new Color(Dim.r, Dim.g, Dim.b, alpha * (1f - open));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return Sprite;
        }
    }
}
