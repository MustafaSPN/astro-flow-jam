using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>Tek bir bölümün tanımı. Çalışma anında değiştirilmez; LevelController kendi kopyasını kurar.</summary>
    [CreateAssetMenu(menuName = "Astro Flow Jam/Level Data", fileName = "Level")]
    public sealed class LevelData : ScriptableObject
    {
        [Header("Kimlik")]
        public string levelId = "L00";
        public string displayName = "Bölüm";
        public bool isHard;
        [Tooltip("Bu bölümde tanıtılan mekanik; öğretici ipuçları bu alana bağlanır.")]
        public string introducesMechanic;

        [Header("Jam tahtası")]
        public int boardWidth = 6;
        public int boardHeight = 6;
        public List<RocketSpec> rockets = new();
        public List<ExitSpec> exits = new();
        [Tooltip("Delik hücreler: tahtada yok sayılır, hiçbir blok giremez.")]
        public List<Vector2Int> holes = new();
        [Tooltip("Asteroit engelleri: hiç oynamaz; sayacı olan tahtadan o kadar blok çıkınca kırılır.")]
        public List<AsteroidSpec> asteroids = new();

        [Header("Gizli resim ve boncuklar")]
        [Tooltip("Resources/LevelPictures içindeki gizli resmin adı; bölüm açılınca yüklenir.")]
        public string pictureName;
        public int beadColumns = 20;
        public int beadRows = 20;
        [Tooltip("Üst satırdan başlayarak satır satır renk harfleri: R, B, G, Y; '.' boş hücre. Boşsa bölümde boncuk yoktur.")]
        [TextArea(4, 20)] public string beadLayout = "";
        [Tooltip("İçerik kurulumunun sürümü; editör, eski sürümdeki bölümleri güncel içerikle yeniler.")]
        public int contentVersion;

        public bool HasBeads => !string.IsNullOrEmpty(beadLayout);

        [Header("Bant ve süre")]
        [Tooltip("Süre sınırı (saniye); 0 süresiz. Süre biterse bölüm kaybedilir. Tasarlayıcı her bölüme yazar.")]
        public float timeLimit;
        [Tooltip("Bant kapasitesi (birim): küçük roket 1, büyük roket 2. Varsayılan 5.")]
        public int orbitCapacity = 5;
        [Tooltip("Turu biten roketlerin beklediği slot sayısı. Varsayılan 5.")]
        public int slotCount = 5;
    }
}
