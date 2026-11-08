using System;

namespace AstroFlowJam
{
    /// <summary>
    /// Oyuncu ayarları (ses, titreşim): kayıtta açık/kapalı tutulur. Bir ayar değişince <see cref="Changed"/> duyurulur;
    /// ayarlar penceresi ve duraklatma paneli aynı anahtarları gösterdiği için ikisi de kendini günceller.
    /// </summary>
    public static class GameSettings
    {
        public static event Action Changed;

        public static bool IsOn(string key) => GameSave.GetInt(key, 1) == 1;

        public static void Toggle(string key)
        {
            GameSave.SetInt(key, IsOn(key) ? 0 : 1);
            GameSave.Save();
            Apply();
            Changed?.Invoke();
        }

        /// <summary>Kayıttaki ayarları sistemlere uygular (açılışta ve kayıt silinince).</summary>
        public static void Apply() => Haptics.Enabled = IsOn(SaveKeys.Vibration);

        /// <summary>Kayıt dışarıdan değişti (ör. hesap silindi): göstergeler yenilensin.</summary>
        public static void NotifyReset()
        {
            Apply();
            Changed?.Invoke();
        }
    }
}
