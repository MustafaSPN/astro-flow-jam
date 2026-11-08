namespace AstroFlowJam
{
    public sealed partial class GameHud
    {
        /// <summary>Bölümde geçen süre, dakika:saniye (süre sınırı olmayan bölüm).</summary>
        public void SetTime(float seconds) => timePanel.SetTime(seconds);

        /// <summary>Kalan süre, dakika:saniye; saniye aşağı yuvarlanır (son saniyede 00:00 görünür).</summary>
        public void SetTimeLeft(float remaining) => timePanel.SetTimeLeft(remaining);

        /// <summary>Kalan süreyle her tam saniyede çağrılır: 60 sn altında küçülme, 30/20/10 sn'de neon uyarı.</summary>
        public void TimeWarningAt(float remaining) => timePanel.TimeWarningAt(remaining);

        /// <summary>Yeni bölüm: uyarılar sıfırlanır.</summary>
        public void ResetTimer() => timePanel.Reset();

        /// <summary>Zaman dondurma göstergesi: kalan pay (0 kapalı) ve saniye.</summary>
        public void SetFreeze(float share, float secondsLeft) => timePanel.SetFreeze(share, secondsLeft);
    }
}
