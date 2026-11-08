using System;
using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Bant ve atış mantığı:
    /// - Tahtadan çıkan veya slottan dokunulan roket banda zıplar (0,25 sn, tepe %50) ve inişte ezilir.
    /// - Bant sabit hızla akar; öğeler arasında en az "aralık + boyların yarısı" kadar mesafe korunur, gerekirse öndeki itilir.
    /// - Bantta her roket saniyede 20,7 atış hızıyla, kenarına hizalı hatlardaki dıştaki ilk boncuk kendi rengindeyse ateş eder.
    ///   Boncuk atış anında rezerve edilir, mermi çarpınca temizlenir; mermi biten roket dönerek yükselip kaybolur.
    /// - Turu biten roket mermisi kaldıysa boş slota zıplar; büyük roket yan yana iki slot kaplar. Yer yoksa banttan
    ///   çıkamaz ve yeni bir tur atar;
    ///   o anda bantta hiçbir roket hiçbir boncuğu vuramıyorsa bölüm "yer kalmadı" ile kaybedilir.
    /// Bant kapasitesi birimle (OrbitModel) tutulur: birim çıkış kararında ayrılır, roket banttan ayrılınca serbest kalır.
    /// </summary>
    public sealed partial class ConveyorRunner : MonoBehaviour
    {
        const float ProjectileSize = 0.16f;
        const float FlightSpeed = 9f;           // birim/sn (referans ölçeğinde); uçuş süresi mesafeden çıkar
        const float MinFlightSeconds = 0.35f, MaxFlightSeconds = 0.8f;
        const float FlightLift = 1.6f;          // uçuşun ortasında kameraya doğru yükselme

        const float PickSeconds = 0.18f;        // kartuş depodan rokete
        const float FlyOffSeconds = 1.1f;
        const float FlyOffSpeed = 6f;           // başlangıç hızı (ölçekli); süre boyunca ivmelenir
        static readonly Vector3 CartridgeOffset = new(0f, -0.05f, -0.1f);    // rokete giriş noktası (roket yerel, ölçekli)


        readonly List<Item> items = new();
        readonly List<Projectile> projectiles = new();
        readonly Stack<Projectile> projectilePool = new();
        Item[] slots;
        int reservedSlots;                      // kapıdan geçmekte olan, slota inecek parçalar için ayrılan yer sayısı
        // Fırlatma rampası tek roketliktir: rampaya uçan / kartuş alan roket sahibidir; diğerleri sırayla bekler.
        Item rampOwner;
        readonly List<Item> rampQueue = new();
        ConveyorTrack track;
        OrbitModel belt;
        BeadGrid grid;
        BeadField field;
        PrototypePalette palette;
        float failCheckAt = -1f;
        Item lastShooter;
        Action<Vector3> headbuttImpact;
        const float HeadbuttSeconds = 0.55f;

        /// <summary>Bir boncuk patlayıp resmin bir hücresi açıldığında.</summary>
        public event Action BeadCleared;

        /// <summary>Turu biten rokete slotta yer yokken hiçbir roket vuramadığında (yer kalmadı).</summary>
        public event Action OutOfSpace;

        /// <summary>Slottaki roket dokunuşla yörüngeye döndü.</summary>
        public event Action SlotLaunched;

        /// <summary>Slotta bekleyen roketin merkezi (slotta roket yoksa false).</summary>
        public bool TryGetSlotCenter(int slot, out Vector3 center)
        {
            center = default;
            if (slot < 0 || slot >= slots.Length) return false;
            var item = slots[slot];
            if (item == null || item.slot != slot || item.queued) return false;
            center = SlotCenter(slot, item.units);
            return true;
        }

        /// <summary>Slota inmiş ve yörüngede yeri olan (dokununca kalkacak) ilk roketin slotu; yoksa -1.</summary>
        public int FirstTappableSlot()
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var item = slots[i];
                if (item != null && item.state == State.Waiting && item.slot == i && !item.queued && belt.CanReserve(item.units)) return i;
            }
            return -1;
        }

        /// <summary>
        /// Bizim sapmamız: tahtadan yörüngeye hâlâ roket gelebilir mi (kapıdan geçen roket ya da boş yere sığan blok)?
        /// Gelebiliyorsa kaybedilmez; yeni roket resmi açıp başkalarına hedef bırakabilir.
        /// </summary>
        public Func<bool> MoreRocketsComing;

        /// <summary>
        /// Otomatik bitirici: kalan roketlerin hepsi banda sığınca açılır ve bölüm sonuna kadar açık kalır.
        /// Açıkken bant 2 kat hızlı akar, atış çarpanına 2,5 eklenir ve bant sonsuz tura geçer (tur biten roket slota inmez).
        /// </summary>
        public bool AutoFinisherActive { get; private set; }

        public void ActivateAutoFinisher() => AutoFinisherActive = true;

        /// <summary>Bantta, bandın yolunda veya slotta olup henüz tükenmemiş roketlerin birim toplamı.</summary>
        public int RemainingUnits
        {
            get
            {
                var n = 0;
                foreach (var item in items) if (item.state != State.Disappearing) n += item.units;
                return n;
            }
        }

        /// <summary>Depodaki kartuş sayısı (görsel; mantık OrbitModel'dedir).</summary>
        public int DepotCount => track.Depot.Count;

        /// <summary>Bant ya da slotta roket var mı?</summary>
        public int ActiveCount => items.Count;

        public int OccupiedSlots
        {
            get
            {
                var n = 0;
                foreach (var s in slots) if (s != null) n++;
                return n + reservedSlots;
            }
        }

        public int SlotCount => slots.Length;

        /// <param name="beadGrid">Boncuksuz prototip bölümlerinde null; roketler bir tur atıp kaybolur.</param>
        public void Init(ConveyorTrack conveyor, OrbitModel beltCapacity, BeadGrid beadGrid, BeadField beadField, PrototypePalette prototypePalette)
        {
            track = conveyor;
            belt = beltCapacity;
            grid = beadGrid;
            field = beadField;
            palette = prototypePalette;
            slots = new Item[track.SlotPositions.Count];
            reservedSlots = 0;
            rampOwner = null;
            rampQueue.Clear();
        }

        /// <summary>
        /// Hangar jokeri: bekleme sırasına bir yer eklenir. Sıra yeniden ortalandığı için bekleyen roketler yeni yerlerine
        /// kısa bir sıçrayışla geçer, yolda olanların hedefi güncellenir.
        /// </summary>
        public void AddSlot()
        {
            track.AddSlot();
            Array.Resize(ref slots, track.SlotPositions.Count);
            foreach (var item in items)
            {
                if (item.slot < 0 || item.queued) continue;
                if (item.state == State.Waiting) StartJump(item, item.view.transform.position, false);
                else if (item.state == State.Jumping && !item.jumpToBelt) item.jumpTo = SlotCenter(item.slot, item.units);
            }
        }

        /// <summary>
        /// UFO jokeri: bu renkteki bütün roketler (bantta, rampa sırasında, slotta ya da yolda) oyundan çekilir; tuttukları
        /// bant birimi, slot ve kartuş serbest kalır. Görünümleri çağırana verilir (UFO'ya çekilme animasyonu için).
        /// </summary>
        public List<RocketView> TakeColor(GameColor color)
        {
            var taken = new List<RocketView>();
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                if (item.view.Spec.color != color || item.state == State.Disappearing || item.state == State.Headbutt) continue;
                // Bant birimini tutanlar: bantta dönen, rampa sırasında bekleyen ve rampaya (banda) uçan roketler.
                if (item.state == State.Riding || item.queued || item == rampOwner) belt.Release(item.units);
                if (item.slot >= 0)
                {
                    for (var u = 0; u < item.units; u++) slots[item.slot + u] = null;
                    item.slot = -1;
                }
                rampQueue.Remove(item);
                ReleaseRamp(item);
                ReleaseCartridge(item);
                if (lastShooter == item) lastShooter = null;
                item.view.SetFlame(false);
                items.RemoveAt(i);
                taken.Add(item.view);
            }
            CompactSlots();
            return taken;
        }

        /// <summary>Ne dolu ne de ayrılmış slot sayısı.</summary>
        public int FreeSlotCount => slots.Length - OccupiedSlots;

        /// <summary>Bant doluyken kapıdan çıkacak parça için bir slot yeri atomik olarak ayırır.</summary>
        public bool ReserveSlot()
        {
            if (FreeSlotCount <= 0) return false;
            reservedSlots++;
            return true;
        }

        /// <summary>
        /// Kapıdan çıkan tek hücrelik roket. Bant birimi ya da slot yeri çağıran tarafından ayrılmıştır: toSlot ise roket
        /// banda değil en soldaki boş slota uçar (bant doluyken tahtada beklemek gerekmez).
        /// </summary>
        /// <param name="delay">Bölünen blokun parçaları sırayla uçar.</param>
        public void Enter(RocketView view, Vector3 from, float delay, bool toSlot = false)
        {
            view.ClearClip();
            var item = new Item
            {
                view = view, units = 1, ammo = view.Spec.capacity,
                length = RocketView.ColliderSize,
            };
            items.Add(item);
            if (toSlot)
            {
                reservedSlots--;
                item.slot = FindSlot(1);
                slots[item.slot] = item;
            }
            if (toSlot) StartJump(item, from, false);
            else
            {
                // Rampaya sırayla: rampa boşsa doğrudan oraya, değilse rampanın önündeki sıraya uçar.
                item.queued = true;
                rampQueue.Add(item);
                StartJump(item, from, false, track.QueueSpot(rampQueue.Count - 1));
            }
            item.time = -delay;
            item.launchSound = true;
        }

        // Rampa boşsa sıradaki roket (gecikmesi dolmuşsa) rampaya uçar; slottan gelen roket slotunu o an boşaltır.
        void ServeRampQueue()
        {
            if (rampOwner != null || rampQueue.Count == 0) return;
            var item = rampQueue[0];
            if (item.time < 0f) return;
            rampQueue.RemoveAt(0);
            item.queued = false;
            rampOwner = item;
            StartJump(item, item.view.transform.position, true);
            // Arkadakiler birer yer öne kayar.
            for (var i = 0; i < rampQueue.Count; i++)
            {
                var q = rampQueue[i];
                var spot = track.QueueSpot(i);
                if (q.state == State.Jumping) q.jumpTo = spot;
                else if (q.state == State.Waiting) StartJump(q, q.view.transform.position, false, spot);
            }
        }

        void ReleaseRamp(Item item)
        {
            if (rampOwner == item) rampOwner = null;
        }

        void StartHop(Item item)
        {
            item.state = State.Jumping;
            item.hop = true;
            item.time = 0f;
            item.jumpFrom = item.view.transform.position;
            item.jumpToBelt = false;
            item.jumpTo = SlotCenter(item.slot, 1);
            item.flightSeconds = OrbitTuning.JumpSeconds;
        }

        // Roket hedefe alev atarak uçar: kameraya doğru hafif yükselen bir eğri, burun gidiş yönünde. Bant hedefi süre
        // boyunca biraz ilerlediği için varış noktası bandın başıdır; slota inen roketin alevi iniş anında söner.
        void StartJump(Item item, Vector3 from, bool toBelt, Vector3? target = null)
        {
            item.state = State.Jumping;
            item.hop = false;
            item.time = 0f;
            item.jumpFrom = from;
            item.jumpToBelt = toBelt;
            // Yörüngeye giden roket rampada durmaz: rampanın üstünden doğrudan katılma noktasına uçar, kartuş yolda yetişir.
            item.jumpTo = target ?? (toBelt ? track.Evaluate(0f, out _) : SlotCenter(item.slot, item.units));
            item.pickStarted = false;
            var distance = Vector3.Distance(from, item.jumpTo);
            item.flightSeconds = Mathf.Clamp(distance / (FlightSpeed * track.Scale), MinFlightSeconds, MaxFlightSeconds);
            item.view.SetFlame(true);
        }

        void Update()
        {
            if (track == null) return;
            var dt = Time.deltaTime;
            var k = track.Scale;

            UpdateProjectiles(dt, k);
            ServeRampQueue();

            for (var i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                item.time += dt;
                var gone = false;
                switch (item.state)
                {
                    case State.Jumping: UpdateJump(item, k); break;
                    case State.Headbutt: gone = UpdateHeadbutt(item, dt, k); break;
                    case State.Riding:
                        item.distance += OrbitTuning.ConveyorSpeed * k * dt * (AutoFinisherActive ? OrbitTuning.AutoFinisherConveyorSpeed : 1f);
                        break;
                    case State.Disappearing: gone = UpdateFlyOff(item, dt, k); break;
                }
                if (!gone) continue;
                Destroy(item.view.gameObject);
                items.RemoveAt(i);
            }

            EnforceSpacing(k);

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.state != State.Riding) continue;
                var t = item.view.transform;
                t.position = track.Evaluate(item.distance, out var tangent);
                t.rotation = RotationFor(item.view, tangent);
                if (item.distance >= track.ExitDistance) ReachEnd(item);
                else if (grid != null) Fire(item, k);
            }

            if (failCheckAt >= 0f && Time.time >= failCheckAt)
            {
                failCheckAt = -1f;
                // Yolda mermi varsa çarpınca yeni boncuklar açılabilir; karar mermiler inene kadar ertelenir.
                if (projectiles.Count > 0) failCheckAt = Time.time + OrbitTuning.CannotShootFailDelay;
                else if (!AnyRiderFitsSlots() && !AnyRiderCanShoot() && !AnyJoining() && !AnySlotRocketCanHelp() && !(MoreRocketsComing?.Invoke() ?? false))
                    OutOfSpace?.Invoke();
            }
        }

        // Aralık koruma: bant sırasına göre ardışık iki öğe arasında en az
        // "minSpacing + (boyA + boyB) / 2" bırakılır; yakın kalan öndeki öğe ileri itilir.
        readonly List<Item> riders = new();
        static readonly Comparison<Item> ByDistance = (a, b) => a.distance.CompareTo(b.distance);

        void EnforceSpacing(float k)
        {
            riders.Clear();
            foreach (var item in items)
                if (item.state == State.Riding) riders.Add(item);
            riders.Sort(ByDistance);
            for (var i = 0; i < riders.Count - 1; i++)
            {
                var behind = riders[i];
                var ahead = riders[i + 1];
                var need = OrbitTuning.MinSpacing * k + (behind.length + ahead.length) / 2f;
                if (ahead.distance - behind.distance < need) ahead.distance = behind.distance + need;
            }
        }

        // MarkDepleted: mermisi biten roket banttan ayrılır, yeri hemen boşalır.
        void StartDisappear(Item item)
        {
            if (item.state == State.Riding) belt.Release(item.units);
            ReleaseCartridge(item);
            item.view.SetFlame(true);
            item.state = State.Disappearing;
            item.time = 0f;
            var t = item.view.transform;
            item.jumpFrom = t.position;
            // Gidiş yönü: burnun yönü, resmin merkezinden dışarı doğru biraz açılarak.
            var nose = (Vector2)(t.rotation * Vector3.up);
            var outward = ((Vector2)(t.position - track.transform.position)).normalized;
            var dir = (nose + outward * 0.6f).normalized;
            item.flyDirection = new Vector3(dir.x, dir.y, 0f);
        }

        /// <summary>
        /// Bölüm sonu: son atışı yapan roket (yoksa yörüngedeki herhangi biri) kilide uçup çarpar. Çarpma anında onImpact
        /// gidiş yönüyle çağrılır. Uygun roket yoksa false döner.
        /// </summary>
        public bool Headbutt(Vector3 target, Action<Vector3> onImpact)
        {
            var item = lastShooter != null && items.Contains(lastShooter) ? lastShooter : null;
            if (item == null) item = items.Find(x => x.state == State.Riding);
            if (item == null) item = items.Find(x => x.state != State.Headbutt);
            if (item == null) return false;
            if (item.state == State.Riding) belt.Release(item.units);
            if (item.slot >= 0)
            {
                for (var u = 0; u < item.units; u++) slots[item.slot + u] = null;
                item.slot = -1;
            }
            rampQueue.Remove(item);
            ReleaseRamp(item);
            ReleaseCartridge(item);
            item.queued = false;
            item.state = State.Headbutt;
            item.time = 0f;
            item.jumpFrom = item.view.transform.position;
            item.jumpTo = target;
            item.view.transform.localScale = Vector3.one;
            item.view.SetFlame(true);
            headbuttImpact = onImpact;
            return true;
        }

        // Slot bulamayan (ya da otomatik bitiricideki) roket durmadan yeni tura geçer: tur katılma noktasında kapanır.
        static void StartTransit(Item item)
        {
            item.distance = 0f;
            item.pass.Reset();
        }

        Vector3 CartridgeSeat(Item item)
        {
            var t = item.view.transform;
            return t.position + t.rotation * (CartridgeOffset * track.Scale);
        }

        Quaternion CartridgeRotation => track.Depot.transform.rotation * track.Depot.SocketRotation;

        // Bant sonu: boncuksuz bölümde roket kaybolur; mermisi kalan roket boş slota zıplar, slot yoksa yeni tur atar.
        void ReachEnd(Item item)
        {
            if (grid == null)
            {
                StartDisappear(item);
                return;
            }
            // Otomatik bitirici: bant sonsuz turdadır, roket rampa boyunca başa döner.
            if (AutoFinisherActive)
            {
                StartTransit(item);
                return;
            }
            var free = FreeSlotCount > 0 ? FindSlot(item.units) : -1;
            if (free < 0)
            {
                StartTransit(item);
                if (!AnyRiderCanShoot()) failCheckAt = Time.time + OrbitTuning.CannotShootFailDelay;
                return;
            }
            belt.Release(item.units);
            for (var u = 0; u < item.units; u++) slots[free + u] = item;
            item.slot = free;
            // Durmadan: kartuş bırakma noktasında depoya döner, roket aynı anda slotuna uçar.
            ReleaseCartridge(item);
            StartJump(item, item.view.transform.position, false);
        }

        // Atış çarpanı: piksel boyutu (referans birimiyle) küçüldükçe atış hızlanır; otomatik bitirici ekler.
        float ShootSpeedMultiplier(float k)
        {
            var pixelSize = field.Pitch / k;
            var t = Mathf.Clamp01((pixelSize - OrbitTuning.ShootSpeedPixelSizeStart) /
                                  (OrbitTuning.ShootSpeedPixelSizeEnd - OrbitTuning.ShootSpeedPixelSizeStart));
            var m = OrbitTuning.ScaleShootSpeedByPixelSize ? Mathf.Lerp(OrbitTuning.ShootSpeedStart, OrbitTuning.ShootSpeedEnd, t) : 1f;
            if (AutoFinisherActive) m += OrbitTuning.AutoFinisherShootBonus;
            return Mathf.Max(OrbitTuning.ShootSpeedMin, m);
        }

        // Yörüngede sayılır: dönen roket.
        static bool OnOrbit(Item item) => item.state == State.Riding;

        // Roket modelinin burnu yerel +y'dedir. Yörüngede burun akış yönüne bakar, alev arkadan çıkar; mermiler gövdeden
        // resme doğru atılır. Slotta burun yukarı bakar.
        static Quaternion RotationFor(RocketView view, Vector3 tangent) => Nose(tangent);

        static Quaternion Nose(Vector2 direction) =>
            Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
    }
}
