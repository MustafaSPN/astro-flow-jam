using UnityEngine;
using UnityEngine.EventSystems;

namespace AstroFlowJam
{
    /// <summary>
    /// Hafif kaydırma: içerik parmakla birlikte dikeyde biraz kayar (uzaklaştıkça direnç artar), bırakınca yayla eski
    /// yerine döner. Ana ekrandaki gezegen yolu için.
    /// </summary>
    public sealed class ElasticDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform target;
        public float limit = 260f;             // en çok kayma (tuval birimi)
        public float returnSharpness = 10f;

        float offset;
        bool dragging;
        Vector2 basePosition;
        bool hasBase;

        public bool Dragging => dragging;

        void LateUpdate()
        {
            if (!target) return;
            if (!hasBase) { basePosition = target.anchoredPosition; hasBase = true; }
            if (!dragging) offset = Mathf.Lerp(offset, 0f, 1f - Mathf.Exp(-returnSharpness * Time.unscaledDeltaTime));
            target.anchoredPosition = basePosition + new Vector2(0f, offset);
        }

        /// <summary>İçeriğin kayma olmadan durduğu yer (yeniden kurulunca).</summary>
        public void ResetBase(Vector2 position)
        {
            basePosition = position;
            hasBase = true;
            offset = 0f;
        }

        public void OnBeginDrag(PointerEventData eventData) => dragging = true;

        public void OnDrag(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>();
            var scale = canvas ? canvas.rootCanvas.scaleFactor : 1f;
            var delta = eventData.delta.y / Mathf.Max(0.01f, scale);
            // Kenara yaklaştıkça direnç: uzaklık arttıkça aynı parmak hareketi daha az kaydırır.
            var resistance = 1f - Mathf.Clamp01(Mathf.Abs(offset) / limit);
            offset = Mathf.Clamp(offset + delta * (0.15f + 0.6f * resistance), -limit, limit);
        }

        public void OnEndDrag(PointerEventData eventData) => dragging = false;
    }
}
