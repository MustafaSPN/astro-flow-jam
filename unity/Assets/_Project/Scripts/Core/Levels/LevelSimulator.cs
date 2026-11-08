using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam.Core
{
    /// <summary>
    /// Bölümün oyundaki kurallarla (ConveyorRunner, BoardInput, LevelController) kare kare simülasyonu ve "makul oyuncu"
    /// politikası. Bölüm tasarımını ölçmek içindir: çözülebilirlik, süre, başlangıçta işe yarayan hamle oranı, kilit açma
    /// anları ve zorunlu kötü hamleler.
    /// Oyuncu: bantta yer varken, tahtadan çıkabilen ya da slotta bekleyen roketlerden dışta hedefi en çok açık olanı
    /// gönderir. Hiçbiri işe yaramıyorsa bekler; bant boş ve bekleyecek bir şey yoksa en çok boncuğu kalan rengi göndermek
    /// zorunda kalır (tuzak).
    /// </summary>
    public sealed class LevelSimulator
    {
        public sealed class Input
        {
            public int boardWidth, boardHeight;
            public List<RocketSpec> rockets;
            public List<ExitSpec> exits;
            public int beadColumns, beadRows;
            public string beadLayout;
            public float pictureWidth = 6.0f;
            public int beltCapacity = 5;
            public int slotCount = 5;
        }

        public sealed class Result
        {
            public bool solved;
            public string failReason;
            public float seconds;
            /// <summary>Başlangıçta tahtadan çıkabilen roketlerin kaçının rengi dışta hedef bulabiliyor (0..1).</summary>
            public float usefulAtStart;
            public int exitableAtStart;
            /// <summary>En az bir kaydırma gerektiren çıkış sayısı (kilit açma anları).</summary>
            public int unlockExits;
            /// <summary>İşe yaramayan bir roketi göndermek zorunda kalma sayısı.</summary>
            public int forcedSends;
            public int maxSlotsUsed;
            public int boardExits;
            public bool autoFinisherUsed;

            public override string ToString() =>
                $"{(solved ? "çözüldü" : "ÇÖZÜLEMEDİ (" + failReason + ")")} {seconds:0}sn, başta yararlı {usefulAtStart:P0} ({exitableAtStart} çıkış), " +
                $"kilit açma {unlockExits}, zorunlu {forcedSends}, en çok slot {maxSlotsUsed}";
        }

        const float Dt = 1f / 60f;
        const float MaxSeconds = 300f;
        const float StallSeconds = 60f;     // bu kadar süre hiç boncuk temizlenmezse çözülemedi sayılır
        const float DecisionCooldown = 0.35f;
        const float DragSecondsPerMove = 0.45f;
        const float DoorAndJumpSeconds = BoardTuning.ExitAlignSeconds + BoardTuning.ExitPassSeconds + OrbitTuning.JumpSeconds;
        const float SlotTapSeconds = 0.25f;

        enum State { Entering, Riding, Waiting }

        sealed class Item
        {
            public RocketSpec spec;
            public int units, ammo;
            public State state;
            public float timer, distance, nextFire, length;
            public int slot = -1;
            public readonly PassLines pass = new();
        }

        readonly Input input;
        readonly BeltPath belt;
        readonly BeadGrid grid;
        readonly float pitch, tolerance;
        readonly List<Item> items = new();
        readonly List<(int bead, float time)> flights = new();
        readonly Item[] slots;
        List<RocketSpec> board;
        List<BoardSolver.ExitOption> options;
        int usedUnits;
        bool autoFinisher;
        float time, decisionAt;
        int lastRemaining = -1;
        float lastProgress;
        float shotMultiplier;
        readonly Result result = new();

        public LevelSimulator(Input levelInput)
        {
            input = levelInput;
            belt = new BeltPath(input.pictureWidth);
            grid = new BeadGrid(input.beadColumns, input.beadRows, input.beadLayout);
            pitch = input.pictureWidth / Mathf.Max(input.beadColumns, input.beadRows);
            tolerance = OrbitTuning.AlignTolerance * belt.Scale / pitch;
            slots = new Item[input.slotCount];
            board = new List<RocketSpec>(input.rockets);

            var pixelSize = pitch / belt.Scale;
            var t = Mathf.Clamp01((pixelSize - OrbitTuning.ShootSpeedPixelSizeStart) / (OrbitTuning.ShootSpeedPixelSizeEnd - OrbitTuning.ShootSpeedPixelSizeStart));
            shotMultiplier = OrbitTuning.ScaleShootSpeedByPixelSize ? Mathf.Lerp(OrbitTuning.ShootSpeedStart, OrbitTuning.ShootSpeedEnd, t) : 1f;
        }

        public static Result Run(Input input) => new LevelSimulator(input).Run();

        Vector2 ToGrid(Vector2 local) => new(
            (local.x + input.beadColumns * pitch / 2f) / pitch,
            (input.beadRows * pitch / 2f - local.y) / pitch);

        Result Run()
        {
            var start = Options();
            result.exitableAtStart = start.Count;
            var useful = 0;
            foreach (var o in start) if (Useful(o.rocket.color)) useful++;
            result.usefulAtStart = start.Count > 0 ? useful / (float)start.Count : 0f;

            while (time < MaxSeconds)
            {
                if (grid.Remaining == 0)
                {
                    result.solved = true;
                    break;
                }
                if (result.failReason != null) break;
                if (grid.Remaining != lastRemaining) { lastRemaining = grid.Remaining; lastProgress = time; }
                else if (time - lastProgress > StallSeconds) { result.failReason = "ilerleme yok"; break; }
                if (time >= decisionAt) Decide();
                Step();
                time += Dt;
            }
            if (!result.solved && result.failReason == null) result.failReason = "zaman aşımı";
            result.seconds = time;
            result.autoFinisherUsed = autoFinisher;
            return result;
        }

        // Bu renkteki bir roket banttan şu an bir boncuğu vurabilir mi?
        bool Useful(GameColor color) => ShooterTargeting.CanHitFromOrbit(grid, color);

        int ExposedCount(GameColor color)
        {
            var n = 0;
            foreach (GridSide side in System.Enum.GetValues(typeof(GridSide)))
            {
                var lines = side is GridSide.Top or GridSide.Bottom ? grid.Columns : grid.Rows;
                for (var l = 0; l < lines; l++)
                {
                    var b = grid.FirstInLine(side, l);
                    if (b >= 0 && grid.StateAt(b) == BeadState.Available && grid.ColorAt(b) == color) n++;
                }
            }
            return n;
        }

        void Decide()
        {
            decisionAt = time + DecisionCooldown;
            var free = input.beltCapacity - usedUnits;
            // Bant doluyken kapıdan çıkan parçalar boş slotlara iner; tahtadan çıkış için ikisi birlikte sayılır.
            var room = free + FreeSlots();
            if (room <= 0) return;

            // Adaylar: slottaki roketler ve tahtadan şimdi çıkabilenler.
            Item bestSlot = null;
            var bestSlotScore = 0;
            foreach (var it in items)
            {
                if (free <= 0 || it.state != State.Waiting || it.units > free) continue;
                var score = ExposedCount(it.spec.color);
                if (score > bestSlotScore) { bestSlotScore = score; bestSlot = it; }
            }

            BoardSolver.ExitOption bestExit = null;
            var bestExitScore = 0;
            var exitOptions = Options();
            foreach (var o in exitOptions)
            {
                if (o.rocket.OrbitUnits > room) continue;
                var score = ExposedCount(o.rocket.color) * 10 - o.moves;
                if (score > bestExitScore) { bestExitScore = score; bestExit = o; }
            }

            if (bestSlot != null && bestSlotScore * 10 >= bestExitScore) { TapSlot(bestSlot); return; }
            if (bestExit != null) { TakeExit(bestExit); return; }

            // İşe yarar hamle yok: bantta bir şey dönüyorsa ya da mermi uçuyorsa bekle.
            foreach (var it in items) if (it.state != State.Waiting) return;
            if (flights.Count > 0) return;

            // Zorunlu hamle: en çok boncuğu kalan rengin roketi.
            BoardSolver.ExitOption forced = null;
            var forcedScore = -1;
            foreach (var o in exitOptions)
            {
                if (o.rocket.OrbitUnits > room) continue;
                var remaining = RemainingOfColor(o.rocket.color) * 10 - o.moves;
                if (remaining > forcedScore) { forcedScore = remaining; forced = o; }
            }
            if (forced != null) { result.forcedSends++; TakeExit(forced); return; }
            foreach (var it in items)
                if (free > 0 && it.state == State.Waiting && it.units <= free) { result.forcedSends++; TapSlot(it); return; }
            result.failReason = "hamle yok";
        }

        int RemainingOfColor(GameColor color)
        {
            var n = 0;
            for (var i = 0; i < grid.Columns * grid.Rows; i++)
                if (grid.StateAt(i) == BeadState.Available && grid.ColorAt(i) == color) n++;
            return n;
        }

        // Tahtadaki çıkış seçenekleri yalnızca tahta değişince (bir roket çıkınca) yeniden hesaplanır.
        List<BoardSolver.ExitOption> Options() =>
            options ??= BoardSolver.ExitOptions(input.boardWidth, input.boardHeight, board, input.exits);

        void TakeExit(BoardSolver.ExitOption o)
        {
            board = o.after;
            options = null;
            result.boardExits++;
            if (o.moves > 0) result.unlockExits++;
            var toBelt = Mathf.Min(input.beltCapacity - usedUnits, o.rocket.Cells);
            usedUnits += toBelt;
            var delay = o.moves * DragSecondsPerMove + DoorAndJumpSeconds;
            // Blok kapıda hücrelerine bölünür; parçalar sırayla banda girer, bantta yer kalmazsa slotlara iner.
            for (var i = 0; i < o.rocket.Cells; i++)
            {
                var piece = new Item
                {
                    spec = o.rocket.Piece(i), units = 1, ammo = o.rocket.CapacityPerCell, state = State.Entering,
                    timer = delay + i * OrbitTuning.PieceLaunchInterval, length = BeltPath.RocketWidth,
                };
                if (i >= toBelt)
                {
                    piece.slot = FindSlot(1);
                    slots[piece.slot] = piece;
                    piece.state = State.Waiting;
                }
                items.Add(piece);
            }
            decisionAt = time + delay * 0.6f;
        }

        void TapSlot(Item it)
        {
            for (var u = 0; u < it.units; u++) slots[it.slot + u] = null;
            it.slot = -1;
            usedUnits += it.units;
            it.state = State.Entering;
            it.timer = SlotTapSeconds + OrbitTuning.JumpSeconds;
        }

        void Step()
        {
            // Mermiler
            for (var i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                f.time -= Dt;
                if (f.time <= 0f)
                {
                    grid.Clear(f.bead);
                    flights.RemoveAt(i);
                }
                else flights[i] = f;
            }

            UpdateAutoFinisher();
            var speed = OrbitTuning.ConveyorSpeed * belt.Scale * (autoFinisher ? OrbitTuning.AutoFinisherConveyorSpeed : 1f);
            var interval = 1f / (OrbitTuning.ShotsPerSecond * Mathf.Max(OrbitTuning.ShootSpeedMin, shotMultiplier + (autoFinisher ? OrbitTuning.AutoFinisherShootBonus : 0f)));

            for (var i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                if (it.state == State.Entering)
                {
                    it.timer -= Dt;
                    if (it.timer <= 0f) { it.state = State.Riding; it.distance = 0f; it.pass.Reset(); it.nextFire = time; }
                }
                else if (it.state == State.Riding) it.distance += speed * Dt;
            }

            // Aralık koruma
            var riders = items.FindAll(x => x.state == State.Riding);
            riders.Sort((a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < riders.Count - 1; i++)
            {
                var need = OrbitTuning.MinSpacing * belt.Scale + (riders[i].length + riders[i + 1].length) / 2f;
                if (riders[i + 1].distance - riders[i].distance < need) riders[i + 1].distance = riders[i].distance + need;
            }

            foreach (var it in riders)
            {
                if (it.distance >= belt.ExitDistance) { ReachEnd(it); continue; }
                Fire(it, interval);
            }
            items.RemoveAll(x => x.state == State.Riding && x.ammo <= 0);

            var used = 0;
            foreach (var s in slots) if (s != null) used++;
            result.maxSlotsUsed = Mathf.Max(result.maxSlotsUsed, used);
        }

        void Fire(Item it, float interval)
        {
            if (it.ammo <= 0 || time < it.nextFire) return;
            var shots = Mathf.Clamp(Mathf.FloorToInt((Dt + 0.0001f) / interval), 1, OrbitTuning.MaxShotsPerFrame);
            var local = belt.Point(it.distance);
            var from = ToGrid(local);
            for (var n = 0; n < shots && it.ammo > 0; n++)
            {
                var bead = ShooterTargeting.Pick(grid, from, it.spec.color, tolerance, it.pass);
                if (bead < 0) return;
                grid.TryReserve(bead);
                it.ammo--;
                var cell = new Vector2(grid.ColumnOf(bead) + 0.5f, grid.RowOf(bead) + 0.5f);
                var distance = (cell - from).magnitude * pitch;
                flights.Add((bead, distance / (OrbitTuning.ProjectileSpeed * belt.Scale)));
                it.nextFire = Mathf.Max(it.nextFire + interval, time - interval);
                if (it.ammo == 0) usedUnits -= it.units;
            }
        }

        void ReachEnd(Item it)
        {
            if (autoFinisher) { it.distance = 0f; it.pass.Reset(); return; }
            var free = FindSlot(it.units);
            if (free < 0)
            {
                it.distance = 0f;
                it.pass.Reset();
                if (!AnyRiderCanShoot() && flights.Count == 0 && !AnyRiderFits()) result.failReason = "yer kalmadı";
                return;
            }
            for (var u = 0; u < it.units; u++) slots[free + u] = it;
            it.slot = free;
            it.state = State.Waiting;
            usedUnits -= it.units;
        }

        int FreeSlots()
        {
            var n = 0;
            foreach (var s in slots) if (s == null) n++;
            return n;
        }

        int FindSlot(int units)
        {
            for (var i = 0; i + units <= slots.Length; i++)
            {
                var ok = true;
                for (var u = 0; u < units; u++) ok &= slots[i + u] == null;
                if (ok) return i;
            }
            return -1;
        }

        bool AnyRiderFits()
        {
            foreach (var it in items) if (it.state == State.Riding && it.ammo > 0 && FindSlot(it.units) >= 0) return true;
            return false;
        }

        bool AnyRiderCanShoot()
        {
            foreach (var it in items)
                if (it.state == State.Riding && it.ammo > 0 && Useful(it.spec.color)) return true;
            return false;
        }

        void UpdateAutoFinisher()
        {
            if (autoFinisher) return;
            foreach (var r in board) if (!r.isBlocker) return;   // tahtada blok kaldıkça açılmaz (oyundaki kuralla aynı)
            var units = 0;
            foreach (var it in items) if (it.ammo > 0) units += it.units;
            if (units <= input.beltCapacity) autoFinisher = true;
        }
    }
}
