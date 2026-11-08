using UnityEngine;
using UnityEngine.EventSystems;

namespace AstroFlowJam
{
    /// <summary>
    /// Arayüz butonu dokunma tepkisi: basılınca hızla küçülür, bırakınca hafif taşarak yaylanıp eski boyuna döner.
    /// Oyun duraklatılmışken de çalışsın diye ölçeksiz zaman kullanır.
    /// </summary>
    public sealed class ButtonPunch : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        const float PressedScale = 0.86f;
        const float PressSeconds = 0.06f;
        const float ReleaseSeconds = 0.32f;
        const float Overshoot = 0.12f;

        Vector3 baseScale;
        float time = -1f;
        bool pressed;
        float from = 1f;

        void Awake() => baseScale = transform.localScale;

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            from = transform.localScale.x / Mathf.Max(baseScale.x, 1e-4f);
            time = 0f;
            Haptics.Select();
            Sfx.Play(Sfx.Button);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            from = transform.localScale.x / Mathf.Max(baseScale.x, 1e-4f);
            time = 0f;
        }

        void Update()
        {
            if (time < 0f) return;
            time += Time.unscaledDeltaTime;
            float k;
            if (pressed)
            {
                k = Mathf.Lerp(from, PressedScale, Tween.Evaluate(Tween.Ease.OutQuad, time / PressSeconds));
            }
            else
            {
                // Sönümlü yay: hedef 1, ilk anda biraz taşar.
                var p = Mathf.Clamp01(time / ReleaseSeconds);
                k = 1f + (from - 1f) * (1f - p) + Overshoot * Mathf.Sin(p * Mathf.PI) * (1f - p);
                if (p >= 1f) { k = 1f; time = -1f; }
            }
            transform.localScale = baseScale * k;
        }

        void OnDisable()
        {
            transform.localScale = baseScale;
            time = -1f;
            pressed = false;
        }
    }
}
