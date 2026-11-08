using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>Oyunun malzeme özellikleri; metin yerine kimlikle erişilir.</summary>
    public static class ShaderIds
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        public static readonly int Metallic = Shader.PropertyToID("_Metallic");
        public static readonly int Gloss = Shader.PropertyToID("_Gloss");
        public static readonly int GlossKeepDark = Shader.PropertyToID("_GlossKeepDark");
        public static readonly int GlowGroup = Shader.PropertyToID("_GlowGroup");
        public static readonly int ClipPlane = Shader.PropertyToID("_ClipPlane");
    }
}
