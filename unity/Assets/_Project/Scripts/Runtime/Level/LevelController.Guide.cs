using System.Collections;
using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Oyun içi rehberler. İlk bölümde temel tutorial:
    /// oyuncu eli izleyerek tek bir bloğu kendi renkli kapısına sürükler, roketlerin yörüngede kendi rengindeki
    /// pikselleri vurduğu, yörünge kapasitesi ve hedef anlatılır. Bir roket ilk kez bekleme yerine indiğinde ona
    /// dokunmayı öğreten adım gelir. Tahta mekanikleri ilk göründükleri bölümde
    /// kesitle tanıtılır. Her rehber bir kez gösterilir (kayıtlı); rehber sürerken süre durur.
    /// </summary>
    public sealed partial class LevelController
    {
        enum GuideMode { None, Block, Slot }

        GuideMode guideMode;
        int guideBlock = -1;
        bool guideExitDone, guideSlotDone, guideSlotSeen, guideContinued, slotGuideRunning;
        static readonly string[] MechanicGuides = { "oneway", "ice", "lock", "asteroid", "countdown", "layer" };
        const float GuideStartDelay = 0.5f, GuideAfterExit = 1.3f, SlotGuideDelay = 0.7f;

        /// <summary>Rehber bu bloğun tutulmasına izin veriyor mu (temel tutorial'da yalnız gösterilen blok)?</summary>
        public bool GuideAllowsBlock(int index) => guideMode == GuideMode.None || (guideMode == GuideMode.Block && index == guideBlock);

        /// <summary>Rehber slot roketine dokunmaya izin veriyor mu?</summary>
        public bool GuideAllowsSlot => guideMode is GuideMode.None or GuideMode.Slot;

        bool GuideActive => guideMode != GuideMode.None || hud.GuideVisible;

        void ResetGuides()
        {
            guideMode = GuideMode.None;
            guideBlock = -1;
            slotGuideRunning = false;
            guideSlotSeen = GameSave.GetInt(SaveKeys.GuideSlot, 0) == 1;
            hud.HideGuide();
        }

        // Bölüm açılışı: temel tutorial (1. bölüm), mekanik tanıtımları, sonra joker tanıtımı.
        IEnumerator LevelIntro()
        {
            yield return new WaitForSeconds(GuideStartDelay);
            if (levelIndex == 0 && GameSave.GetInt(SaveKeys.GuideBasics, 0) == 0) yield return BasicsGuide();
            yield return MechanicIntros();
            yield return BoosterIntro();
        }

        IEnumerator BasicsGuide()
        {
            var target = PickGuideBlock(out var exitIndex);
            if (target < 0) yield break;
            dialogOpen = true;
            input.CancelDrag();
            guideMode = GuideMode.Block;
            guideBlock = target;
            guideExitDone = false;
            var blockRect = ScreenRect(rocketViews[target].gameObject);
            var gateRect = ScreenRect(BoardView.Gates[exitIndex].gameObject);
            hud.ShowGuide(new List<Rect> { blockRect, gateRect }, "Move the blocks", "Drag the block to the gate of the same color.", null);
            var outward = (gateRect.center - blockRect.center).normalized * 40f;
            hud.SetGuideHand(blockRect.center, gateRect.center + outward);
            while (!guideExitDone) yield return null;
            guideMode = GuideMode.None;
            hud.HideGuide();
            yield return new WaitForSeconds(GuideAfterExit);

            yield return GuideInfo(new List<Rect> { ScreenRect(track.gameObject) }, "Rockets",
                "Rockets fly around the orbit and shoot the pixels of their own color.");
            yield return GuideInfo(new List<Rect> { ScreenRect(track.Depot.gameObject) }, "Orbit space",
                $"The orbit holds {Orbit.Capacity} rockets. The counter shows how many more can launch.");
            yield return GuideInfo(new List<Rect> { ScreenRect(field ? field.gameObject : track.gameObject) }, "Your goal",
                "Clear every pixel to reveal the hidden picture!");
            GameSave.SetInt(SaveKeys.GuideBasics, 1);
            GameSave.Save();
            dialogOpen = false;
        }

        // Bilgi adımı: karartma + kesit + açıklama; dokununca biter.
        IEnumerator GuideInfo(List<Rect> holes, string title, string text)
        {
            holes.RemoveAll(r => r.width <= 0f);
            guideContinued = false;
            hud.ShowGuide(holes, title, text, () => guideContinued = true);
            while (!guideContinued) yield return null;
            hud.HideGuide();
        }

        // İlk bölüm bloğu: düz itişle kendi kapısından hemen çıkabilen, rengi resimde şimdi vurulabilen blok.
        int PickGuideBlock(out int exitIndex)
        {
            exitIndex = -1;
            var fallback = -1;
            var fallbackExit = -1;
            for (var i = 0; i < Board.RocketCount; i++)
            {
                if (!Board.IsOnBoard(i) || Board.GetRocket(i).isBlocker || Board.IsFrozen(i) || !HasRocketView(i) || !HasRoomFor(i)) continue;
                foreach (Dir dir in System.Enum.GetValues(typeof(Dir)))
                {
                    if (Board.CheckExit(i, dir, out var ex) != ExitCheck.Ok) continue;
                    if (beads == null || ShooterTargeting.CanHitFromOrbit(beads, Board.GetRocket(i).color)) { exitIndex = ex; return i; }
                    if (fallback < 0) { fallback = i; fallbackExit = ex; }
                }
            }
            exitIndex = fallbackExit;
            return fallback;
        }

        /// <summary>Denetleyici: başarılı çıkış (temel tutorial'ın sürükleme adımı bunu bekler).</summary>
        void GuideOnExit(int index)
        {
            if (guideMode == GuideMode.Block && index == guideBlock) guideExitDone = true;
        }

        /// <summary>Slottaki rokete başarılı dokunuş (yörüngeye döndü).</summary>
        void GuideOnSlotLaunched()
        {
            if (guideMode == GuideMode.Slot) guideSlotDone = true;
        }

        // Temel tutorial'ın tamamı dahil diyaloglar sürerken dialogOpen açıktır.
        bool SlotGuideBlocked => dialogOpen || finishing || runner.AutoFinisherActive || GuideActive || IsPickingForBooster || tutorialBooster >= 0;

        // Bekleme yerinde dokunulabilir roket varsa ona dokunmak öğretilir (bir kez, hangi bölümde olursa). Her karede
        // bakılır: roket başka bir rehber sürerken indiyse rehber kapanır kapanmaz gösterilir.
        void UpdateSlotGuide()
        {
            if (guideSlotSeen || slotGuideRunning || SlotGuideBlocked || runner.FirstTappableSlot() < 0) return;
            StartCoroutine(SlotGuide());
        }

        IEnumerator SlotGuide()
        {
            slotGuideRunning = true;
            yield return new WaitForSeconds(SlotGuideDelay);
            var slot = runner.FirstTappableSlot();
            if (SlotGuideBlocked || !runner.TryGetSlotCenter(slot, out var center)) { slotGuideRunning = false; yield break; }
            dialogOpen = true;
            input.CancelDrag();
            guideMode = GuideMode.Slot;
            guideSlotDone = false;
            var screen = (Vector2)worldCamera.WorldToScreenPoint(center);
            var size = Mathf.Abs(worldCamera.WorldToScreenPoint(center + Vector3.right * track.Scale).x - screen.x) * 1.4f;
            var hole = new Rect(screen - Vector2.one * size / 2f, Vector2.one * size);
            hud.ShowGuide(new List<Rect> { hole }, "Waiting slots", "This rocket still has ammo. Tap it to send it back to the orbit!", null);
            hud.SetGuideHand(screen, screen);
            while (!guideSlotDone)
            {
                if (!runner.TryGetSlotCenter(slot, out _)) break;   // roket başka yolla ayrıldı
                yield return null;
            }
            guideMode = GuideMode.None;
            hud.HideGuide();
            if (guideSlotDone)                          // roket başka yolla ayrıldıysa öğretilmiş sayılmaz
            {
                guideSlotSeen = true;
                GameSave.SetInt(SaveKeys.GuideSlot, 1);
                GameSave.Save();
            }
            dialogOpen = false;
            slotGuideRunning = false;
        }

        // Tahta mekaniklerinin ilk görünüşü: kesitle tanıtılır.
        IEnumerator MechanicIntros()
        {
            foreach (var mechanic in MechanicGuides)
            {
                if (GameSave.GetInt(SaveKeys.Guide(mechanic), 0) == 1) continue;
                var holes = new List<GameObject>();
                string title = null, text = null;
                for (var i = 0; i < Board.RocketCount && title == null; i++)
                {
                    if (!rocketViews.TryGetValue(i, out var view)) continue;
                    var r = Board.GetRocket(i);
                    switch (mechanic)
                    {
                        case "oneway" when r.axis != MoveAxis.Free:
                            holes.Add(view.gameObject);
                            title = "One-way block";
                            text = $"This block slides only {(r.axis == MoveAxis.Horizontal ? "left and right" : "up and down")}, in the direction of its arrows.";
                            break;
                        case "ice" when Board.IceLeft(i) > 0:
                            holes.Add(view.gameObject);
                            title = "Frozen block";
                            text = $"It can't move yet. It thaws after {Board.IceLeft(i)} more blocks leave the board.";
                            break;
                        case "lock" when r.lockGroup > 0:
                            holes.Add(view.gameObject);
                            for (var j = 0; j < Board.RocketCount; j++)
                                if (j != i && Board.GetRocket(j).keyGroup == r.lockGroup && rocketViews.TryGetValue(j, out var key)) holes.Add(key.gameObject);
                            title = "Locked block";
                            text = "Send the block with the key out through its gate to unlock this one.";
                            break;
                        case "layer" when r.layered:
                            holes.Add(view.gameObject);
                            title = "Layered block";
                            text = "The outer color leaves through its own gate first. Then the inner color stays on the board.";
                            break;
                    }
                }
                for (var k = 0; k < Board.AsteroidCount && (title == null || mechanic is "asteroid" or "countdown"); k++)
                {
                    if (BoardView.Asteroids.Count <= k || !BoardView.Asteroids[k]) continue;
                    var a = Board.GetAsteroid(k);
                    if (mechanic == "asteroid" && a.count == 0)
                    {
                        holes.Add(BoardView.Asteroids[k].gameObject);   // kalıcı asteroitlerin hepsi gösterilir
                        title = "Asteroid";
                        text = "Asteroids never move. Slide your blocks around them.";
                    }
                    else if (mechanic == "countdown" && a.count > 0)
                    {
                        if (title != null) continue;                    // sayaçlı: ilk asteroit ve sayacı anlatılır
                        holes.Add(BoardView.Asteroids[k].gameObject);
                        title = "Countdown asteroid";
                        text = $"This asteroid breaks after {Board.AsteroidLeft(k)} blocks leave the board.";
                    }
                }
                if (title == null) continue;
                dialogOpen = true;
                input.CancelDrag();
                var rects = new List<Rect>();
                foreach (var go in holes) rects.Add(ScreenRect(go));
                yield return GuideInfo(rects, title, text);
                GameSave.SetInt(SaveKeys.Guide(mechanic), 1);
                GameSave.Save();
                dialogOpen = false;
            }
        }

        // 2x hızlanma ilk kez açıldı: kısa bilgi (oyunu durdurmaz).
        void GuideOnAutoFinisher()
        {
            if (GameSave.GetInt(SaveKeys.GuideSpeed, 0) == 1) return;
            GameSave.SetInt(SaveKeys.GuideSpeed, 1);
            GameSave.Save();
            ShowMessage("Speed x2! Every rocket left fits on the orbit", 2.6f);
        }

        static void ResetGuidePrefs()
        {
            GameSave.DeleteKey(SaveKeys.GuideSpeed);
            GameSave.DeleteKey(SaveKeys.GuideBasics);
            GameSave.DeleteKey(SaveKeys.GuideSlot);
            foreach (var m in MechanicGuides) GameSave.DeleteKey(SaveKeys.Guide(m));
        }
    }
}
