namespace AstroFlowJam
{
    /// <summary>
    /// Oyuncu kaydının tüm anahtarları. Metinler yayındaki kayıtlarla aynı kalmalıdır: değişen anahtar ilerlemeyi siler.
    /// </summary>
    public static class SaveKeys
    {
        public const string Progress = "afj_progress";                // oynanacak bölüm (0'dan)
        public const string Coins = "afj_coins";
        public const string Lives = "afj_lives", LivesStamp = "afj_lives_stamp";
        public const string LivesInfiniteUntil = "afj_lives_infinite", StarterBundleBought = "afj_shop_starter";
        public const string FreeContinueUsed = "afj_free_continue_used";
        public const string AdsRemoved = "afj_ads_removed";            // "Remove Ads" satın alındı
        public const string IapTransactions = "afj_iap_txn";           // içeriği verilmiş son satın alma işlemleri
        public const string LevelAttempt = "afj_attempt";              // ölçüm: "bölüm|art arda deneme"
        public const string Avatar = "afj_avatar";
        public const string Sound = "afj_set_sound", Music = "afj_set_music", Vibration = "afj_set_vibration";
        public const string GuideBasics = "afj_guide_basics", GuideSlot = "afj_guide_slot", GuideSpeed = "afj_guide_speed";

        public static string Guide(string mechanic) => $"afj_guide_{mechanic}";
        public static string BoosterCount(int booster) => $"afj_booster_{booster}";
        public static string BoosterGranted(int booster) => $"afj_booster_granted_{booster}";
        public static string BoosterTutorial(int booster) => $"afj_booster_tutorial_{booster}";

        // Yalnız geliştirme menüsü.
        public const string DevStartLevel = "afj_dev_level";
        public const string DevTimeLimit = "afj_dev_time";            // tüm bölümlerde süre sınırını bu değerle değiştirir
        public const string DevFail = "afj_dev_fail";
    }
}
