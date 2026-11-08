using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Tahta hissinin ayar değerleri (sürükleme, kapı, çıkış animasyonu). Uzunluklar hücre birimindedir.
    /// </summary>
    public static class BoardTuning
    {
        /// <summary>Sürüklenen gövdenin hızı = (hedef − konum) × bu değer.</summary>
        public const float MoveSpeed = 40f;
        /// <summary>Dokunma bu yarıçap (mm) içindeki en yakın bloğu seçer.</summary>
        public const float PredictiveInputRadiusMm = 7f;
        /// <summary>Fizik adımı: sürükleme 120 Hz'de çözülür.</summary>
        public const float FixedTimestep = 1f / 120f;
        /// <summary>2B fizik temas payı (hücre biriminde).</summary>
        public const float ContactOffset = 0.005f;

        /// <summary>Kapı açılırken kol 0,8 hücre dışarı kayar, 0,2 sn OutExpo.</summary>
        public const float DoorSlide = 0.8f;
        public const float DoorSlideSeconds = 0.2f;
        /// <summary>Seçilen blokla aynı renkteki kapılar 0,25 sn'de 0,2 parlaklığa çıkar, 0,1 sn'de söner.</summary>
        public const float DoorGlow = 0.2f;
        public const float DoorGlowOnSeconds = 0.25f;
        public const float DoorGlowOffSeconds = 0.1f;

        /// <summary>Kapıdan çıkış (toplam 0,4 sn): önce 0,07 sn'de hücre hizasına (OutQuad)...</summary>
        public const float ExitAlignSeconds = 0.07f;
        /// <summary>...sonra kapı ekseninde kapı çizgisinin öbür yanına 0,33 sn, doğrusal.</summary>
        public const float ExitPassSeconds = 0.33f;

        /// <summary>Reddedilen hamlede blok sarsıntısı: ölçek sarsıntısı 0,3 sn, güç 0,1, titreşim 10.</summary>
        public const float ShakeSeconds = 0.3f;
        public const float ShakeStrength = 0.1f;
        public const int ShakeVibrato = 10;
    }

    /// <summary>
    /// Yörünge, atış ve animasyon ayar değerleri. Uzunluklar 6,2 birim genişliğindeki bir referans resme göredir;
    /// sahneye <see cref="Scale"/> ile çevrilir.
    /// </summary>
    public static class OrbitTuning
    {
        public const float ReferencePictureWidth = 6.2f;

        // Atış
        public const float ShotsPerSecond = 20.7f;
        public const int MaxShotsPerFrame = 16;
        public const float AlignTolerance = 0.15f;
        public const float ProjectileSpeed = 30f;

        // Bant: köşeleri yuvarlak dikdörtgen, resim merkezli
        public const float ConveyorSpeed = 5f;
        public const float BaseItemLength = 0.5f;
        public const float MinSpacing = 0.5f;
        public const float ConveyorHalfWidth = 3.246f;
        public const float ConveyorHalfHeight = 3.932f;
        public const float ConveyorCornerRadius = 0.8f;
        /// <summary>Bant merkezi resim merkezinin bu kadar altında.</summary>
        public const float ConveyorCenterOffset = 0.14f;
        /// <summary>Bandın başlangıcı: alt kenarda, sol köşe yayının bitimi (x = −2,44); yol saat yönünün tersinedir.</summary>
        public const float ConveyorStartX = -2.44f;
        /// <summary>Bandın sonu: sol kenarda (y = −3,016); sol alt köşe başlangıç ile son arasındaki boşluktur.</summary>
        public const float ConveyorEndY = -3.016f;
        public const float ClipEnd = 0.995f;

        // Bekleme yerleri: bant alt kenarının 1,95 birim altında, ortalı; 5 slot.
        public const float SlotRowBelowConveyor = 1.95f;
        /// <summary>Slot aralığı: bir roket genişliği.</summary>
        public const float SlotSpacing = 1.05f;

        // Otomatik bitirici: kalan roketlerin hepsi banda sığınca bant hızı ×2, atış çarpanına +2,5, bant sonsuz tur.
        public const float AutoFinisherConveyorSpeed = 2f;
        public const float AutoFinisherShootBonus = 2.5f;
        /// <summary>Atış çarpanı piksel boyutuna göre: 0,3125 birimde ×1, 0,1 birimde ×2,5 (arası doğrusal, en az 1).</summary>
        public const float ShootSpeedPixelSizeStart = 0.3125f;
        public const float ShootSpeedPixelSizeEnd = 0.1f;
        public const float ShootSpeedStart = 1f;
        public const float ShootSpeedEnd = 2.5f;
        public const float ShootSpeedMin = 1f;

        // Kayıp denetimi: "hiçbir roket vuramıyor" kararı bu gecikmeyle verilir.
        public const float CannotShootFailDelay = 0.1f;

        // Roket animasyonları
        public const float JumpSeconds = 0.15f;   // yörüngeye atlayış
        public const float JumpHeight = 3f;
        public const float JumpPeak = 0.5f;
        public static readonly Vector3 ShootPunch = new(0.24f, -0.28f, 0.24f);
        public const float ShootPunchSeconds = 0.07f;
        public const int ShootPunchVibrato = 1;
        public const float ShootPunchElasticity = 0f;

        public static readonly Vector3 TapPunch = new(0.15f, -0.2f, 0.15f);
        public const float TapPunchSeconds = 0.6f;
        public const int TapPunchVibrato = 8;
        public const float TapPunchElasticity = 0.8f;
        /// <summary>İniş ezilmesi: x = yatay eksenlerin çarpanı, y = dikey eksenin çarpanı.</summary>
        public static readonly Vector2 LandSquash = new(1.15f, 0.85f);
        public const float LandSquashSeconds = 0.08f;
        public const float LandSettleSeconds = 0.06f;
        public const float DisappearSeconds = 0.35f;
        public const float DisappearSpinTurns = 1f;
        public const float DisappearRise = 7f;

        // Piksel patlaması
        public const float PixelPopSeconds = 0.05f;
        public const float PixelPopScale = 1.1f;
        public const float PixelShrinkSeconds = 0.1f;
        public const float PixelHop = 0.1f;

        /// <summary>
        /// Atış hızını resmin piksel boyutuna göre ölçekleme (küçük pikselde ×2,5'e kadar). Bizim resimlerimizde roketleri
        /// bant dolmadan boşalttığı için kapalı; otomatik bitirici artışı açık kalır.
        /// </summary>
        public const bool ScaleShootSpeedByPixelSize = false;

        /// <summary>Art arda gönderilen roketlerin banda yerleşme aralığı.</summary>
        public const float PlaceToConveyorInterval = 0.05f;

        /// <summary>
        /// Bölünen bloğun roketleri yörüngeye bu aralıkla uçar. 0,05 sn'de bantta birbirine yapışık gidiyorlardı;
        /// aralarında görünür mesafe kalsın diye uzatıldı.
        /// </summary>
        public const float PieceLaunchInterval = 0.3f;

        /// <summary>Referans uzunlukları sahneye çeviren çarpan (resim genişliğine göre).</summary>
        public static float Scale(float pictureWidth) => pictureWidth / ReferencePictureWidth;
    }
}
