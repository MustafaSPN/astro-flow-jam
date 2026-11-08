#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace AstroFlowJam
{
    /// <summary>
    /// Reklam takibi izni (iOS App Tracking Transparency). Oyuncu 3. bölümü bitirip ana ekrana döndüğünde bir kez
    /// sorulur: ilk açılışta sormak "hayır" oranını artırır, geçiş reklamları da zaten 20. bölümde başlar. Oyuncu karar
    /// verdiyse iOS bir daha sormaz; pencere bir sebeple çıkmadıysa (karar yok) sonraki ana ekranda yeniden denenir.
    /// "Hayır" denirse oyun aynen sürer, reklamlar kişiselleştirilmeden gösterilir.
    /// </summary>
    public static class Tracking
    {
        public const int AskAfterLevel = 3;
        const int NotDetermined = 0;

        static bool askedThisSession;

        /// <summary>Kazanılan bölüm sayısı yeterliyse ve karar verilmemişse sistem izin penceresini açar.</summary>
        public static void MaybeAsk(int levelsCompleted)
        {
            if (askedThisSession || levelsCompleted < AskAfterLevel) return;
#if UNITY_IOS && !UNITY_EDITOR
            if (AFJ_TrackingStatus() != NotDetermined) return;
            askedThisSession = true;
            AFJ_RequestTracking();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int AFJ_TrackingStatus();

        [DllImport("__Internal")]
        static extern void AFJ_RequestTracking();
#endif
    }
}
