using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Geniş ekran (iPad) yerleşimi: telefon düzeni ekranın ortasındaki 1080×2340 sütuna aynen yerleşir, arka plan tüm
    /// ekranı kaplar. Telefonların hepsi eşiğin altındadır (en geniş 16:9 = 0,5625); onlarda hiçbir şey değişmez.
    /// </summary>
    public static class ScreenFit
    {
        public const float ColumnWidth = 1080f, ColumnHeight = 2340f;
        const float WideAspect = 0.6f;
        // Çentikli iPhone'un güvenli alan payları (tuval birimi): sütunda da aynı boşluklar kalır, üst bar ve alt şerit kaymaz.
        const float PhoneTopInset = 166f, PhoneBottomInset = 91f;

        static float Aspect => Screen.height > 0 ? (float)Screen.width / Screen.height : 0f;

        public static bool IsWide => Aspect > WideAspect;

        /// <summary>Sütunun ekran genişliğine oranı (telefonda 1).</summary>
        public static float ColumnFraction => IsWide ? ColumnWidth / ColumnHeight / Aspect : 1f;

        /// <summary>Güvenli alanın alt kenarı (0..1): geniş ekranda en az telefon payı kadar.</summary>
        public static float SafeBottom(float safeBottom) => IsWide ? Mathf.Max(safeBottom, PhoneBottomInset / ColumnHeight) : safeBottom;

        /// <summary>Güvenli alanın üst kenarı (0..1): geniş ekranda en az telefon payı kadar aşağıda.</summary>
        public static float SafeTop(float safeTop) => IsWide ? Mathf.Min(safeTop, 1f - PhoneTopInset / ColumnHeight) : safeTop;

        /// <summary>Sütunun bir yanında kalan boşluk, tuval biriminde (telefonda 0).</summary>
        public static float SideMargin => IsWide ? (ColumnHeight * Aspect - ColumnWidth) / 2f : 0f;
    }
}
