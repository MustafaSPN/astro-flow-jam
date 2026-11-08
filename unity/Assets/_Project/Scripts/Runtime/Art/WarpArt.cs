using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Solucan deliği (Warp jokeri, yer tutucu): dönen sarmal kollu mor-camgöbeği girdap, ortada karanlık olay ufku ve
    /// parlak iç halka, dışa doğru sönen kenar. Sprite 1 birim genişliğinde; çağıran ölçekler ve döndürür.
    /// </summary>
    public static class WarpArt
    {
        const int Size = 256;
        static Sprite wormhole;

        /// <summary>Portal tasarımı (Resources/UI/WarpPortal.png); yoksa kodla çizilen girdap. Her ikisi de 1 birim genişliğinde.</summary>
        public static Sprite Portal()
        {
            if (portal) return portal;
            var tex = Resources.Load<Texture2D>("UI/WarpPortal");
            if (!tex) return Wormhole();
            return portal = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }

        static Sprite portal;

        public static Sprite Wormhole()
        {
            if (wormhole) return wormhole;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[Size * Size];
            var deep = new Color(0.16f, 0.04f, 0.35f);
            var violet = new Color(0.62f, 0.25f, 1f);
            var cyan = new Color(0.4f, 0.95f, 1f);
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var p = new Vector2((x + 0.5f) / Size - 0.5f, (y + 0.5f) / Size - 0.5f) * 2f;   // -1..1
                var r = p.magnitude;
                if (r > 1f) { pixels[y * Size + x] = Color.clear; continue; }
                var a = Mathf.Atan2(p.y, p.x);
                // Sarmal kollar: açı yarıçapla burulur.
                var arms = 0.5f + 0.5f * Mathf.Sin(a * 4f + r * 11f);
                var c = Color.Lerp(deep, violet, arms * (1f - r) * 1.4f);
                c = Color.Lerp(c, cyan, Mathf.Pow(arms, 3f) * Mathf.Clamp01(1f - Mathf.Abs(r - 0.45f) * 3f));
                // Parlak iç halka ve karanlık merkez.
                var ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.24f) / 0.06f);
                c = Color.Lerp(c, new Color(0.85f, 0.95f, 1f), ring * 0.9f);
                if (r < 0.2f) c = Color.Lerp(new Color(0.02f, 0f, 0.06f), c, Mathf.Clamp01((r - 0.12f) / 0.08f));
                // Kenar söner.
                c.a = Mathf.Clamp01((1f - r) / 0.25f);
                pixels[y * Size + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return wormhole = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
