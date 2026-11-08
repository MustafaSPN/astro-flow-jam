using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>Gri kutu prototipinin malzemeleri. Gerçek varlıklar geldiğinde yalnızca görünüm katmanı değişir.</summary>
    [CreateAssetMenu(menuName = "Astro Flow Jam/Prototype Palette", fileName = "PrototypePalette")]
    public sealed class PrototypePalette : ScriptableObject
    {
        public Material red;
        public Material blue;
        public Material green;
        public Material yellow;
        public Material boardTile;
        public Material boardFrame;
        public Material orbitTrack;
        public Material picture;
        public Material white;
        [Tooltip("Gizli resim için ışıktan etkilenmeyen dokulu malzeme (URP Unlit).")]
        public Material pictureUnlit;

        // Palet renkleri için çalışma anında üretilen malzemeler (kırmızı malzemenin kopyası, rengi değiştirilmiş).
        readonly System.Collections.Generic.Dictionary<GameColor, Material> paletteMaterials = new();

        public Material For(GameColor color)
        {
            if (!GameColors.IsPaletteColor(color))
            {
                return color switch
                {
                    GameColor.Red => red,
                    GameColor.Blue => blue,
                    GameColor.Green => green,
                    _ => yellow,
                };
            }
            if (paletteMaterials.TryGetValue(color, out var cached) && cached) return cached;
            var material = new Material(red) { name = color.ToString() };
            var value = GameColors.PfColor(color);
            if (material.HasProperty(ShaderIds.BaseColor)) material.SetColor(ShaderIds.BaseColor, value);
            if (material.HasProperty(ShaderIds.Color)) material.SetColor(ShaderIds.Color, value);
            return paletteMaterials[color] = material;
        }
    }
}
