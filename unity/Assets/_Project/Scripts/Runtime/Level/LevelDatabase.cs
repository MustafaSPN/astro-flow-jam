using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>Bölümlerin oynanma sırası yalnızca bu listede tutulur; bir bölümü taşımak için listede yerini değiştirmek yeterlidir.</summary>
    [CreateAssetMenu(menuName = "Astro Flow Jam/Level Database", fileName = "LevelDatabase")]
    public sealed class LevelDatabase : ScriptableObject
    {
        public List<LevelData> levels = new();
    }
}
