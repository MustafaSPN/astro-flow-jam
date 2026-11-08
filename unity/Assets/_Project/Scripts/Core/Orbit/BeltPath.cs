using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam.Core
{
    /// <summary>
    /// Yörünge bandının yolu, resim merkezli yerel düzlemde. Köşeleri yuvarlatılmış kapalı kare:
    /// sol kenardaki katılma noktasından başlar, tam tur atıp aynı noktada biter. Oyun
    /// (ConveyorTrack) ve seviye simülasyonu (LevelSimulator) aynı yolu kullanır.
    /// </summary>
    /// <summary>
    /// AFJ_Orbit_With_Depot modelinin ölçüleri (Blender XY, model birimi): kare yörünge, sol kenara gömülü servis
    /// istasyonu (rampa) ve hemen solunda kartuş deposu. Kaynak: AFJ_Orbit_Path.json ve modeldeki boş nesneler.
    /// </summary>
    public static class OrbitDesign
    {
        public const float Half = 3f;                 // orta hat yarı genişliği
        public const float CornerRadius = 0.65f;
        public const float InnerRail = 2.81f;
        public static readonly Vector2 Merge = new(-3f, -1.025f);         // Orbit_Merge_Point (istasyonun üst ucu)
        public static readonly Vector2 RampEntry = new(-3f, -2.675f);     // Ramp_Entry_Point (istasyonun alt ucu)
        /// <summary>Rampanın üst yarısı: yeni roket burada kartuş alır ve hemen üstten yörüngeye katılır.</summary>
        public static readonly Vector2 LoadPoint = new(-3f, -1.44f);
        /// <summary>Rampanın alt yarısı: turu biten roket burada durup kartuşunu bırakır (yolun sonu).</summary>
        public static readonly Vector2 DropPoint = new(-3f, -2.26f);
        /// <summary>Rampa sırası: deponun üstünde, yörüngenin solunda yukarı doğru dizilir; en öndeki rampaya en yakındır.</summary>
        public static readonly Vector2 QueueFront = new(-3.78f, -0.55f);
        public const float QueueStep = 0.74f;
        public const int QueueSpots = 6;              // bundan sonrakiler son yerde hafifçe kayarak üst üste bekler
        public static readonly Vector2 Exchange = new(-3.15f, -1.85f);    // Cartridge_Exchange_Point
        public static readonly Vector2 Depot = new(-3.8f, -1.85f);        // Cartridge_Depot
        public const float DepotSpacing = 0.265f;     // kartuşlar arası (Cartridge_01..05: −0,53 … +0,53)
        public const int DepotSockets = 5;
        public const float SlotRowBelowOrbit = 1.7f;  // slot sırası yörünge orta hattının altında (referans ölçeğiyle)
        public const float SlotSpacing = 1.25f;       // bekleme yerleri arası (referans 1,05): roketler birbirine değmez
        public const float SlotRowShift = 0f;         // slot sırasının yatay kayması (referans ölçeğiyle)
        public const float LeftExtent = 4.35f;        // deponun sol kenarı
        public const float OuterExtent = 3.3f;        // yörünge tabanının dış kenarı
        public const float Clearance = 0.6f;          // resim kenarı ile orta hat arası

        /// <summary>Model biriminden dünyaya: orta hat resim kenarından 'Clearance' kadar dışarıda.</summary>
        public static float ScaleFor(float pictureWidth) => (pictureWidth / 2f + Clearance) / Half;
    }

    public sealed class BeltPath
    {
        /// <summary>Roketin bant üzerindeki gövde genişliği (hücre birimi); resimle arasında bırakılan boşluğa eklenir.</summary>
        public const float RocketWidth = 0.9f;
        const int CornerSamples = 16;
        const float TangentWindow = 0.3f;

        readonly List<Vector2> points = new();
        readonly List<float> cumulative = new();

        public float Length { get; private set; }

        /// <summary>Öğenin banttan çıktığı mesafe (clipEnd).</summary>
        public float ExitDistance => Length * OrbitTuning.ClipEnd;

        /// <summary>Referans uzunlukları bu sahneye çeviren çarpan.</summary>
        public float Scale { get; }

        public float HalfWidth { get; }
        public float HalfHeight { get; }
        public Vector2 Center { get; }

        public IReadOnlyList<Vector2> Points => points;

        public BeltPath(float pictureWidth)
        {
            Scale = OrbitTuning.Scale(pictureWidth);
            ModelScale = OrbitDesign.ScaleFor(pictureWidth);
            (HalfWidth, HalfHeight, Center) = Extents(pictureWidth);
            var s = ModelScale;
            float h = OrbitDesign.Half * s, r = OrbitDesign.CornerRadius * s;

            // Kare döngü (AFJ_Orbit modeli), saat yönünde: sol kenardaki servis rampasının üst ucundan (katılma noktası)
            // yukarı çıkar, üst, sağ ve alt kenarı dolaşır, sol kenardan rampanın üstünden geçip katılma noktasında biter.
            // Tur tam kapanır: rampa hizasındaki satırlar da vurulur (eskiden bırakma noktasında bitiyordu ve rampa
            // boyunca ateş edilmediği için o satırlardaki pikseller hiç vurulamıyordu).
            points.Add(OrbitDesign.Merge * s);
            AddArc(new Vector2(-h + r, h - r), r, 180f, 90f);
            AddArc(new Vector2(h - r, h - r), r, 90f, 0f);
            AddArc(new Vector2(h - r, -h + r), r, 0f, -90f);
            AddArc(new Vector2(-h + r, -h + r), r, 270f, 180f);
            points.Add(OrbitDesign.Merge * s);

            cumulative.Add(0f);
            for (var i = 1; i < points.Count; i++)
            {
                Length += (points[i] - points[i - 1]).magnitude;
                cumulative.Add(Length);
            }
        }

        /// <summary>Model biriminden (Blender XY) yörünge düzlemine.</summary>
        public float ModelScale { get; }

        /// <summary>
        /// Yörüngenin orta hattının yarı genişliği, yarı yüksekliği ve merkezi (resim merkezine göre). Kare yörünge resmi
        /// ortalar; roketler resmin üstüne taşmasın diye orta hat resim kenarından roketin yarı genişliği + boşluk kadar
        /// dışarıdadır.
        /// </summary>
        public static (float halfWidth, float halfHeight, Vector2 center) Extents(float pictureWidth)
        {
            var h = OrbitDesign.Half * OrbitDesign.ScaleFor(pictureWidth);
            return (h, h, Vector2.zero);
        }

        /// <summary>Model biriminde verilen noktanın yörünge düzlemindeki yeri.</summary>
        public Vector2 FromModel(Vector2 model) => model * ModelScale;

        void AddArc(Vector2 center, float r, float fromDeg, float toDeg)
        {
            for (var i = 0; i <= CornerSamples; i++)
            {
                var a = Mathf.Lerp(fromDeg, toDeg, i / (float)CornerSamples) * Mathf.Deg2Rad;
                points.Add(new Vector2(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r));
            }
        }

        public Vector2 Point(float s)
        {
            s = Mathf.Clamp(s, 0f, Length);
            var lo = 0;
            var hi = cumulative.Count - 1;
            while (hi - lo > 1)
            {
                var mid = (lo + hi) / 2;
                if (cumulative[mid] <= s) lo = mid; else hi = mid;
            }
            var segLength = cumulative[hi] - cumulative[lo];
            var t = segLength > 0f ? (s - cumulative[lo]) / segLength : 0f;
            return Vector2.Lerp(points[lo], points[hi], t);
        }

        /// <summary>Yumuşatılmış yön: köşelerde sıçramadan döner.</summary>
        public Vector2 Tangent(float s) =>
            (Point(Mathf.Min(s + TangentWindow, Length)) - Point(Mathf.Max(s - TangentWindow, 0f))).normalized;

        /// <summary>Bekleme slotlarının yerel konumları, soldan sağa (bandın altında, ortalı).</summary>
        public List<Vector2> SlotPositions(int count)
        {
            var list = new List<Vector2>();
            var y = Center.y - HalfHeight - OrbitDesign.SlotRowBelowOrbit * Scale;
            var x0 = OrbitDesign.SlotRowShift * Scale;
            for (var i = 0; i < count; i++) list.Add(new Vector2(x0 + (i - (count - 1) / 2f) * OrbitDesign.SlotSpacing * Scale, y));
            return list;
        }
    }
}
