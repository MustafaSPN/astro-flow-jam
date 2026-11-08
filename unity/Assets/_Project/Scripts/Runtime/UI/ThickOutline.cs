using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AstroFlowJam
{
    /// <summary>uGUI yazısına kalın kenar: köşe noktaları 'samples' yönde kopyalanıp kenar rengine boyanır (Outline yalnız 4 yön çizer).</summary>
    public sealed class ThickOutline : BaseMeshEffect
    {
        public Color color = UiArt.Ink;
        public float distance = 4f;
        public int samples = 12;
        public Vector2 shadow = new(0f, -4f);    // altta ek koyu gölge (kalın alt kenar)

        readonly List<UIVertex> verts = new();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            verts.Clear();
            vh.GetUIVertexStream(verts);
            var original = verts.Count;
            var result = new List<UIVertex>(original * (samples + 2));
            void AddCopy(Vector2 offset)
            {
                for (var i = 0; i < original; i++)
                {
                    var v = verts[i];
                    v.position += (Vector3)offset;
                    var c = color;
                    c.a *= v.color.a / 255f;
                    v.color = c;
                    result.Add(v);
                }
            }
            if (shadow != Vector2.zero) AddCopy(shadow + Vector2.down * distance * 0.5f);
            for (var s = 0; s < samples; s++)
            {
                var a = s * Mathf.PI * 2f / samples;
                AddCopy(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * distance);
            }
            for (var i = 0; i < original; i++) result.Add(verts[i]);
            vh.Clear();
            vh.AddUIVertexTriangleStream(result);
        }
    }
}
