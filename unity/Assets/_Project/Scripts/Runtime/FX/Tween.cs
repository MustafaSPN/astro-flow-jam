using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Referans oyunlardaki DOTween ayarlarını aynen çalıştırmak için gereken küçük alt küme: ease eğrileri,
    /// DOPunch (azalan, yön değiştiren itme) ve DOShake (azalan sarsıntı). Değerler 0..1 ilerlemeyle hesaplanır.
    /// </summary>
    public static class Tween
    {
        public enum Ease { Linear, InQuad, OutQuad, OutExpo, InOutSine }

        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            return ease switch
            {
                Ease.InQuad => t * t,
                Ease.OutQuad => -t * (t - 2f),
                Ease.OutExpo => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t),
                Ease.InOutSine => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f,
                _ => t,
            };
        }

        /// <summary>
        /// DOPunch ofseti: 'vibrato × süre' kadar ara hedef (en az 2), her biri bir öncekinden kısa sürer;
        /// ilk hedef itmenin kendisi, sonrakiler esneklik oranında ters yönde ve giderek küçülür, son hedef sıfırdır.
        /// Ara geçişler OutQuad ile yapılır.
        /// </summary>
        public static Vector3 Punch(Vector3 punch, float duration, int vibrato, float elasticity, float time)
        {
            if (time <= 0f || time >= duration) return Vector3.zero;
            var count = Mathf.Max(2, (int)(vibrato * duration));
            var magnitude = punch.magnitude;
            var decay = magnitude / count;

            // Süreler: i'inci adım (i+1)/count oranında uzun; toplam süreye ölçeklenir.
            var weights = 0f;
            for (var i = 0; i < count; i++) weights += (i + 1f) / count;

            var from = Vector3.zero;
            var start = 0f;
            var current = magnitude;
            for (var i = 0; i < count; i++)
            {
                var span = duration * ((i + 1f) / count) / weights;
                Vector3 to;
                if (i == count - 1) to = Vector3.zero;
                else if (i == 0) to = punch;
                else if (i % 2 != 0) to = -Vector3.ClampMagnitude(punch, current * elasticity);
                else to = Vector3.ClampMagnitude(punch, current);
                if (i > 0) current -= decay;

                if (time <= start + span) return Vector3.LerpUnclamped(from, to, Evaluate(Ease.OutQuad, (time - start) / span));
                from = to;
                start += span;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// DOShake ofseti (randomness = 0, fadeOut = true): yön her adımda tersine döner, genlik doğrusal sönümlenir.
        /// </summary>
        public static Vector3 Shake(Vector3 strength, float duration, int vibrato, float time)
        {
            if (time <= 0f || time >= duration) return Vector3.zero;
            var count = Mathf.Max(2, (int)(vibrato * duration));
            var span = duration / count;
            var step = Mathf.Min(count - 1, (int)(time / span));
            Vector3 Target(int i) => i >= count - 1 ? Vector3.zero : strength * ((i % 2 == 0 ? 1f : -1f) * (1f - (float)i / count));
            var from = step == 0 ? Vector3.zero : Target(step - 1);
            return Vector3.LerpUnclamped(from, Target(step), Evaluate(Ease.OutQuad, (time - step * span) / span));
        }

        /// <summary>DOJump: yatayda doğrusal ilerleme, 'peak' anında tepe yüksekliğine çıkan iki parçalı parabol.</summary>
        public static float JumpHeight(float height, float peak, float t)
        {
            t = Mathf.Clamp01(t);
            peak = Mathf.Clamp(peak, 0.05f, 0.95f);
            return t <= peak
                ? height * Evaluate(Ease.OutQuad, t / peak)
                : height * (1f - Evaluate(Ease.InQuad, (t - peak) / (1f - peak)));
        }
    }
}
