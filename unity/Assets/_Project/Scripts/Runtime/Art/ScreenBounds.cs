using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>Dünya ile ekran arasında küçük dönüşümler (rehber kesitleri, joker odağı, UFO ağzı).</summary>
    public static class ScreenBounds
    {
        /// <summary>Nesnenin görünen parçalarının ekrandaki sınır dikdörtgeni (piksel, biraz paylı); görünür parça yoksa boş.</summary>
        public static Rect Of(Camera camera, GameObject root)
        {
            if (!root || !camera) return default;
            var any = false;
            var b = new Bounds();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return default;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var s = camera.WorldToScreenPoint(corner);
                xMin = Mathf.Min(xMin, s.x); xMax = Mathf.Max(xMax, s.x);
                yMin = Mathf.Min(yMin, s.y); yMax = Mathf.Max(yMax, s.y);
            }
            const float pad = 18f;
            return Rect.MinMaxRect(xMin - pad, yMin - pad, xMax + pad, yMax + pad);
        }

        /// <summary>Ekran noktasından, kameraya doğru verilen derinlikteki (z) düzleme.</summary>
        public static Vector3 ToPlane(Camera camera, Vector2 screen, float z)
        {
            var ray = camera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return ray.origin;
            var d = (z - ray.origin.z) / ray.direction.z;
            return ray.origin + ray.direction * d;
        }
    }
}
