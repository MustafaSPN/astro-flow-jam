using AstroFlowJam.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroFlowJam
{
    /// <summary>
    /// Fizikli sürükleme girdisi:
    /// - Dokunulan noktaya 7 mm içindeki en yakın roket seçilir; parmak ile roket merkezi arasındaki fark korunur.
    /// - Seçili roketin gövdesi dinamik olur ve hem Update hem FixedUpdate'te hızı (hedef − konum) × 40 yapılır;
    ///   çarpışmaları fizik motoru çözer, roket engellerin kenarı boyunca kayar.
    /// - Roket her karede en yakın boş hücreye mantıksal olarak taşınır. Kapı eşiğine gelip kapı uyduğunda (renk, açıklık,
    ///   yol) kapı açılır ama blok kendiliğinden çıkmaz: oyuncu kapıya doğru itmeye devam eder, görünen model kapının içine
    ///   kayar; bloğun ExitPushShare kadarı (yarısı) kapıdan geçince çıkar. Blok eşikten ayrılınca kapı yeniden kapanır.
    /// - Bırakıldığında kapıya itme geri alınır, hız sıfırlanır, gövde kinematik olur ve roket hücresine anında oturur.
    /// Seçili roketle aynı renkteki kapılar parlar. Yörünge (bant) kapasitesi çıkış kararında LevelController'da denetlenir.
    /// Mekanikler: donuk blok (buzlu/kilitli) seçilmez, sarsılır; tek eksenli blokta diğer eksen fizikte kilitlenir.
    /// </summary>
    public sealed class BoardInput : MonoBehaviour
    {
        LevelController controller;
        Camera cam;

        /// <summary>Bloğun kapı yönündeki boyunun bu kadarı kapıdan geçince blok çıkar.</summary>
        const float ExitPushShare = 0.5f;

        int active = -1;
        RocketView view;
        GateView heldGate;                      // seçili blok eşiğinde olduğu için açık tutulan kapı
        Vector2 offset;
        Vector2 target;
        Vector2Int lastFeedbackOrigin = new(int.MinValue, 0);

        public void Init(LevelController levelController, Camera worldCamera)
        {
            controller = levelController;
            cam = worldCamera;
            active = -1;
            view = null;
            heldGate = null;
            Time.fixedDeltaTime = BoardTuning.FixedTimestep;
            Physics2D.defaultContactOffset = BoardTuning.ContactOffset;
        }

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || controller == null) return;
            if (!controller.InputEnabled)
            {
                if (active >= 0) Drop();
                return;
            }

            var world = ScreenToWorld(pointer.position.ReadValue());
            if (pointer.press.wasPressedThisFrame && !UiBuild.IsPointerOverUi()) Begin(world, pointer.position.ReadValue());
            if (active < 0) return;

            target = (Vector2)world + offset;
            Steer();
            SyncLogicalCell();
            if (TryExit()) return;
            if (pointer.press.wasReleasedThisFrame) Drop();
        }

        void FixedUpdate()
        {
            if (active >= 0) Steer();
        }

        void Steer()
        {
            var body = view.Body;
            body.linearVelocity = (target - body.position) * BoardTuning.MoveSpeed;
        }

        // Eğik perspektif kamerada ekran noktası, ışının tahta düzlemiyle (z = 0) kesiştiği yerdir.
        Vector3 ScreenToWorld(Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return ray.origin;
            var t = -ray.origin.z / ray.direction.z;
            var p = ray.origin + ray.direction * t;
            p.z = 0f;
            return p;
        }

        // FindClosestSelectableCollider: tahta düzleminde, ekranda 7 mm'ye karşılık gelen yarıçap içindeki en yakın roket.
        void Begin(Vector3 world, Vector2 screen)
        {
            // Joker hedef seçimi: yalnız dokunulan hücredeki blok (yakındaki blok yakalanmaz; boşluk seçim yapmaz).
            if (controller.IsPickingForBooster)
            {
                var cell = controller.BoardView.WorldToCellSpace(world);
                var c = new Vector2Int(Mathf.FloorToInt(cell.x), Mathf.FloorToInt(cell.y));
                if (controller.Board.TryGetRocketAt(c, out var picked) && controller.HasRocketView(picked)) controller.TryPickForBooster(picked);
                else if (!controller.Board.InBounds(c)) controller.PickOutsideBoard(screen);
                return;
            }

            var dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            var pixels = BoardTuning.PredictiveInputRadiusMm / 25.4f * dpi;
            var radius = Vector3.Distance(world, ScreenToWorld(screen + new Vector2(pixels, 0f)));

            RocketView best = null;
            var bestDistance = float.MaxValue;
            foreach (var hit in Physics2D.OverlapCircleAll(world, radius))
            {
                var rocket = hit.GetComponent<RocketView>();
                if (rocket == null || !controller.Board.IsOnBoard(rocket.Index) || !controller.HasRocketView(rocket.Index)) continue;
                var d = Vector2.Distance(hit.ClosestPoint(world), world);
                if (d < bestDistance) { bestDistance = d; best = rocket; }
            }

            if (best == null)
            {
                controller.TapOutsideBoard(world);
                return;
            }

            // Donuk blok (buzlu ya da kilitli) ya da rehberin göstermediği blok seçilmez: sarsılır.
            if (controller.Board.IsFrozen(best.Index) || !controller.GuideAllowsBlock(best.Index))
            {
                best.FrozenTap();
                Sfx.Play(Sfx.Wrong);
                Haptics.Deselect();
                return;
            }

            active = best.Index;
            view = best;
            controller.NotifyMove();                // süre ilk hamleyle başlar
            Sfx.Play(Sfx.BlockPick);
            var body = view.Body;
            body.bodyType = RigidbodyType2D.Dynamic;
            // Tek eksenli blok: diğer eksendeki hareket fizikte kilitlenir (engellerin kenarından da kaymaz).
            body.constraints = RigidbodyConstraints2D.FreezeRotation | view.Spec.axis switch
            {
                MoveAxis.Horizontal => RigidbodyConstraints2D.FreezePositionY,
                MoveAxis.Vertical => RigidbodyConstraints2D.FreezePositionX,
                _ => RigidbodyConstraints2D.None,
            };
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            offset = body.position - (Vector2)world;
            target = body.position;
            lastFeedbackOrigin = new Vector2Int(int.MinValue, 0);
            controller.SetGateGlow(view.Spec.color, true, view.Spec.isBlocker);
            view.SetSelected(true);
            Haptics.Select();
        }

        // Roketin mantıksal hücresi, gövdenin en yakın tam hücre konumudur; o hücreler boşsa güncellenir.
        void SyncLogicalCell()
        {
            var board = controller.Board;
            var spec = board.GetRocket(active);
            var cell = controller.BoardView.WorldToCellSpace(view.Body.position) - (Vector2)spec.size / 2f;
            var origin = new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(cell.x), 0, board.Width - spec.size.x),
                Mathf.Clamp(Mathf.RoundToInt(cell.y), 0, board.Height - spec.size.y));
            foreach (var c in spec.LocalCells)
                if (!board.IsFreeFor(active, origin + c)) return;
            board.Relocate(active, origin);
        }

        // CheckDoor: roket bir kenara dayanmışsa o kenardaki kapı denenir. Uyan kapıda parmağın kapıya doğru ne kadar
        // ittiği ölçülür: model o kadar kapının içine kayar, bloğun yarısı geçince çıkar. Uymayan kapı için geri bildirim
        // roket aynı hücrede kaldıkça bir kez verilir.
        bool TryExit()
        {
            var board = controller.Board;
            var spec = board.GetRocket(active);
            var boardView = controller.BoardView;
            var center = boardView.CellToWorld(RocketView.CenterOf(spec.origin, spec.size));
            var bestPush = 0f;
            var bestDir = Dir.Up;
            var bestGate = -1;
            var bestOffset = Vector2.zero;
            var found = false;
            foreach (Dir dir in System.Enum.GetValues(typeof(Dir)))
            {
                if (board.DistanceToEdge(active, dir) != 0) continue;
                var check = board.CheckExit(active, dir, out var gateIndex);
                if (check == ExitCheck.NoGate || check == ExitCheck.Blocked) continue;
                if (check == ExitCheck.Ok && !controller.HasRoomFor(active))
                {
                    // Yörüngede ve slotlarda yer yok: kapı açılmaz, blok kapıya itilemez; sayaç bir kez uyarır.
                    if (lastFeedbackOrigin != spec.origin)
                    {
                        lastFeedbackOrigin = spec.origin;
                        controller.NoRoomFeedback();
                    }
                    continue;
                }
                if (check == ExitCheck.Ok)
                {
                    // Bir hücrelik dünya vektörü: itme hücre biriminde ölçülür.
                    var step = (Vector2)(boardView.CellToWorld(RocketView.CenterOf(spec.origin + dir.ToVector(), spec.size)) - center);
                    var depth = dir is Dir.Up or Dir.Down ? spec.size.y : spec.size.x;
                    var push = Vector2.Dot(target - (Vector2)center, step) / step.sqrMagnitude / depth;
                    if (!found || push > bestPush)
                    {
                        found = true;
                        bestPush = push;
                        bestDir = dir;
                        bestGate = gateIndex;
                        bestOffset = step * (Mathf.Clamp(push, 0f, ExitPushShare) * depth);
                    }
                    continue;
                }
                if (lastFeedbackOrigin != spec.origin)
                {
                    lastFeedbackOrigin = spec.origin;
                    controller.ExitRejected(active, dir, check);
                }
            }

            view.SetDoorPush(found ? bestOffset : Vector2.zero);
            HoldGate(found ? boardView.Gates[bestGate] : null);
            if (!found || bestPush < ExitPushShare) return false;
            view.CommitDoorPush();
            if (!controller.TryExit(active, bestDir))
            {
                // Bant ve slotlar dolu: blok kapıdan geri çekilir, oyuncu tutmaya devam eder.
                var body = view.Body;
                view.transform.position = new Vector3(body.position.x, body.position.y, view.transform.position.z);
                return false;
            }
            HoldGate(null);                     // geçiş kapıyı kendisi açık tutar, bitince kapatır
            view.SetSelected(false);
            controller.SetGateGlow(spec.color, false);
            active = -1;
            view = null;
            return true;
        }

        // Eşikteki kapıyı aç; başka kapıya geçilince ya da eşikten ayrılınca öncekini kapat (kapılar sayaçlı açılır).
        void HoldGate(GateView gate)
        {
            if (gate == heldGate) return;
            if (heldGate) heldGate.Close();
            heldGate = gate;
            if (heldGate) heldGate.Open();
        }

        /// <summary>Tutulan blok varsa bırakılır (kapıdan çıkmadan hücresine oturur).</summary>
        public void CancelDrag()
        {
            if (active >= 0) Drop();
        }

        // Bırakma: hız sıfır, gövde kinematik, roket hücresine anında oturur.
        void Drop()
        {
            var spec = controller.Board.GetRocket(active);
            view.SetDoorPush(Vector2.zero);
            HoldGate(null);
            var body = view.Body;
            body.linearVelocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None;
            var snapped = controller.BoardView.CellToWorld(RocketView.CenterOf(spec.origin, spec.size));
            body.position = snapped;
            view.transform.position = snapped;
            controller.SetGateGlow(spec.color, false);
            view.SetSelected(false);
            Haptics.Deselect();
            Sfx.Play(Sfx.BlockDrop);
            active = -1;
            view = null;
        }
    }
}
