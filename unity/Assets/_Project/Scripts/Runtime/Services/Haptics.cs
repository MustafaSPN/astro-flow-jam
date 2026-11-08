using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Referans oyunlardaki titreşimler (iOS'ta UIImpactFeedbackGenerator; editörde ve diğer platformlarda sessiz):
    /// - Tahta: blok seçilince ve bırakılınca orta şiddette tek vuruş; kapıdan çıkışta 0,04 sn
    ///   aralıklı kısa vuruş dizisi (GateExitHaptic, genlik 0,5).
    /// - Atış: her atışta hafif vuruş, aralarında en az 0,04 sn.
    /// </summary>
    public static class Haptics
    {
        const float GateExitInterval = 0.04f;
        const int GateExitHits = 3;                 // Vuruş sayısı derlenmiş sabitten okunamadı; kısa bir dizi varsayıldı.
        const float GateExitAmplitude = 0.5f;
        const float ShootCooldown = 0.04f;

        enum Style { Light, Medium, Heavy }

        static float lastShoot = -1f;

        public static bool Enabled { get; set; } = true;

        public static void Select() => Impact(Style.Medium, 1f);

        public static void Deselect() => Impact(Style.Medium, 1f);

        public static void Shoot()
        {
            if (Time.unscaledTime - lastShoot < ShootCooldown) return;
            lastShoot = Time.unscaledTime;
            Impact(Style.Light, 0.7f);
        }

        /// <summary>Son saniyeler: her saniye kısa, güçlü bir vuruş (10 sn altında).</summary>
        public static void TimeWarning() => Impact(Style.Heavy, 1f);

        public static void GateExit(MonoBehaviour host) => host.StartCoroutine(GateExitPattern());

        static IEnumerator GateExitPattern()
        {
            for (var i = 0; i < GateExitHits; i++)
            {
                Impact(Style.Medium, GateExitAmplitude);
                yield return new WaitForSecondsRealtime(GateExitInterval);
            }
        }

        static void Impact(Style style, float intensity)
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            AFJ_HapticImpact((int)style, intensity);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void AFJ_HapticImpact(int style, float intensity);
#endif
    }
}
