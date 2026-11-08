namespace AstroFlowJam
{
    /// <summary>
    /// Geliştirici araçları (DEV menüsü, bölüm okları, rozete dokununca bitirme, test satın alma) açık mı. Yalnız editörde ve
    /// geliştirme derlemesinde (BuildIOS, AFJ_DEV tanımı) açık. DevTools.Enabled kullanılmaz: iOS yayın derlemesinde de
    /// true dönebiliyor.
    /// </summary>
    public static class DevTools
    {
#if UNITY_EDITOR || AFJ_DEV
        public const bool Enabled = true;
#else
        public const bool Enabled = false;
#endif

        /// <summary>
        /// Rozete (Level yazısı) dokununca bölümü geçme. Geliştirme derlemesinde ve mağaza ekran görüntüsü derlemesinde
        /// (BuildIOS.SimulatorRelease, AFJ_SHOTS) açık; yayın ve App Store derlemesinde kapalı.
        /// </summary>
#if AFJ_SHOTS
        public const bool LevelSkip = true;
#else
        public const bool LevelSkip = Enabled;
#endif
    }
}
