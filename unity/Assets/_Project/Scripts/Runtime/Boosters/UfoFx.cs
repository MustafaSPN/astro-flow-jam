using System;
using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AstroFlowJam
{
    /// <summary>UFO jokerinin animasyonları: sıvı gibi çekilme ve asılı UFO'nun salınması.</summary>
    public static class UfoFx
    {
        public const float SuckSeconds = 0.7f;

        /// <summary>
        /// Sıvı gibi çekilme: nesne UFO'ya doğru uzar, enine incelir, yüzeyi dalgalanır; yaklaştıkça küçülüp ağzında kaybolur.
        /// Nesne önce <paramref name="layer"/> altına alınır (tahtadan ya da yörüngeden kopar).
        /// </summary>
        public static IEnumerator Suck(Transform t, Transform layer, Vector3 mouth, float delay, Action onGone = null)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (!t) { onGone?.Invoke(); yield break; }
            t.SetParent(layer, true);
            var start = t.position;
            var baseScale = t.localScale;
            var control = Vector3.Lerp(start, mouth, 0.5f) + Vector3.back * 1.2f;   // kameraya doğru hafif kavis
            for (var time = 0f; time < SuckSeconds && t; time += Time.deltaTime)
            {
                var p = time / SuckSeconds;
                var e = p * p * (3f - 2f * p) * 0.35f + p * p * 0.65f;               // yavaş kopar, hızlanarak çekilir
                var a = Vector3.Lerp(start, control, e);
                var b = Vector3.Lerp(control, mouth, e);
                var pos = Vector3.Lerp(a, b, e);
                var dir = (Vector2)(mouth - pos);
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up;
                var flow = Mathf.Sin(Mathf.Min(1f, p * 1.3f) * Mathf.PI * 0.5f);
                var wobble = 1f + 0.08f * Mathf.Sin(time * 40f) * (1f - p);
                var along = 1f + 1.1f * flow;
                var across = (1f - 0.55f * flow) * wobble;
                var shrink = 1f - Mathf.Clamp01((p - 0.6f) / 0.4f);
                var sx = Mathf.Lerp(across, along, Mathf.Abs(dir.x)) * shrink;
                var sy = Mathf.Lerp(across, along, Mathf.Abs(dir.y)) * shrink;
                t.position = pos;
                t.localScale = Vector3.Scale(baseScale, new Vector3(sx, sy, shrink));
                yield return null;
            }
            if (t) Object.Destroy(t.gameObject);
            onGone?.Invoke();
        }

        /// <summary>Asılı UFO hafifçe aşağı yukarı salınır (durdurulana kadar).</summary>
        public static IEnumerator Hover(RectTransform ufo, Vector2 rest)
        {
            for (var t = 0f; ; t += Time.deltaTime)
            {
                ufo.anchoredPosition = rest + new Vector2(0f, Mathf.Sin(t * 5f) * 6f);
                ufo.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3.3f) * 2f);
                yield return null;
            }
        }
    }
}
