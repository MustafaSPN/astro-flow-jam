using System.Collections;
using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Warp (solucan deliği) jokeri: oyuncu tahtada bir blok seçer; bloğun altında bir solucan deliği açılır ve blok sıvı
    /// gibi burularak içine akar. Aynı anda yörünge sayacının (5/5) altında ikinci bir delik açılır; blok oradan sıvı gibi
    /// taşarak çıkar, kapıdan çıkmış gibi roketlerine ayrılıp yörüngeye girer. Yol ve kapı aranmaz; donuk blok da alınır
    /// (buz ve kilit kırılır), katmanlıda yalnız dış katman geçer. Bant ve slotlarda yer yoksa kullanılamaz (TryExit kuralı).
    /// Delik görselleri yer tutucudur.
    /// </summary>
    public sealed partial class LevelController
    {
        bool warpPicking;
        const float WarpOpenSeconds = 0.35f, WarpSinkSeconds = 0.55f, WarpRiseSeconds = 0.5f, WarpCloseSeconds = 0.3f;
        const float WarpFloorDepth = 0.2f;     // tahta zemininin hemen önünde (blokların altında kalır)
        const float PortalSize = 1.25f;        // sprite parıltısıyla: dolu disk ~1 hücre
        const float PortalSpin = 300f;         // derece / sn

        // Hedefi biraz aşıp geri gelen yumuşak eğri (açılışta yaylanma).
        static float BackOut(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            const float s = 1.9f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        void ToggleWarpPick()
        {
            if (ufoBusy) return;
            ufoPicking = false;
            if (!warpPicking && !HasBooster(BoosterWarp)) return;
            warpPicking = !warpPicking;
            input.CancelDrag();
            if (warpPicking) hud.ShowBoosterPrompt(BoosterWarp, "Wormhole", "Select a block", tutorialBooster >= 0 ? null : CancelBoosterPick);
            else hud.HideBoosterPrompt();
        }

        bool TryWarp(int index)
        {
            var spec = Board.GetRocket(index);
            if (spec.isBlocker) return true;
            var shellSpec = spec.layered ? spec.OuterShell() : spec;
            // Parçalar önce bandın boş yerine, kalanlar boş slotlara gider; ikisi birlikte yetmezse kullanılamaz.
            var toBelt = Mathf.Min(Orbit.FreeUnits, shellSpec.OrbitUnits);
            if (runner.FreeSlotCount < shellSpec.OrbitUnits - toBelt)
            {
                ShowMessage("No room on the orbit");
                return true;
            }
            warpPicking = false;
            hud.HideBoosterPrompt();
            SpendBooster(BoosterWarp);
            Sfx.Play(Sfx.Warp);
            Orbit.TryReserve(toBelt);
            var pieceSlots = new List<bool>();
            for (var i = 0; i < shellSpec.OrbitUnits; i++) pieceSlots.Add(i >= toBelt && runner.ReserveSlot());

            var view = rocketViews[index];
            var aligned = BoardView.CellToWorld(RocketView.CenterOf(spec.origin, spec.size));
            Board.RemoveByBooster(index);
            RocketView traveler;
            if (spec.layered)
            {
                traveler = new GameObject($"Rocket {spec.id} shell").AddComponent<RocketView>();
                traveler.transform.SetParent(levelRoot, false);
                traveler.Build(-1, shellSpec, palette, models);
                traveler.transform.position = aligned;
                SettleOnBoard(view, aligned);
                view.HideOuterLayer();
                peeling.Add(index);
            }
            else
            {
                rocketViews.Remove(index);
                view.RemoveBoardPhysics();
                traveler = view;
            }
            Haptics.Select();
            StartCoroutine(Warp(traveler, shellSpec, aligned, pieceSlots, spec.layered ? index : -1));
            return true;
        }

        IEnumerator Warp(RocketView view, RocketSpec spec, Vector3 aligned, List<bool> pieceSlots, int coreIndex)
        {
            var units = spec.OrbitUnits;
            passing++;
            passingUnits += units;
            view.RemoveBoardPhysics();

            // İki delik: bloğun altında (tahta zemininde) ve yörünge sayacının altında.
            // Portal 1×1 blok boyunda (parıltısı hücreden biraz taşar), giriş ve çıkışta aynı.
            var holeSize = PortalSize;
            // Dikdörtgen olmayan bloklarda (L, T) giriş, bloğun işaret hücresinin (sayı yazan hücre) altında açılır.
            var rectangular = System.Linq.Enumerable.Count(spec.LocalCells) == spec.size.x * spec.size.y;
            var mouth = rectangular ? aligned : aligned + (Vector3)view.MarkerOffset;
            var entry = SceneArt.Place(WarpArt.Portal(), "Warp In", levelRoot, new Vector3(mouth.x, mouth.y, WarpFloorDepth), 2);
            // Çıkış: yörünge sayacının (5/5) dikey hizası ile bekleme sırasının yatay hizasının kesiştiği nokta.
            var exitAt = new Vector3(track.CounterPoint.x, track.SlotPositions[0].y, 0f);
            exitAt.z = -0.4f;
            var exit = SceneArt.Place(WarpArt.Portal(), "Warp Out", levelRoot, exitAt, 2);
            var exitSize = PortalSize;
            // Açılış: dönerek büyür, hafifçe taşıp yerine oturur (geri yaylanma).
            for (var t = 0f; t < WarpOpenSeconds; t += Time.deltaTime)
            {
                var k = BackOut(t / WarpOpenSeconds);
                SetHole(entry, holeSize * k, t);
                SetHole(exit, exitSize * k, t);
                yield return null;
            }

            // Blok deliğe akar: burulur, enine incelir, dalgalanarak küçülür ve zemine doğru batar.
            var spin = 0f;
            var startScale = view.transform.localScale;
            for (var t = 0f; t < WarpSinkSeconds; t += Time.deltaTime)
            {
                var p = t / WarpSinkSeconds;
                spin += Time.deltaTime * 420f * (0.3f + p);
                var wobble = 1f + 0.1f * Mathf.Sin(t * 36f) * (1f - p);
                var shrink = 1f - p * p;
                view.transform.localScale = Vector3.Scale(startScale, new Vector3(shrink * wobble, shrink / wobble, shrink));
                view.transform.rotation = Quaternion.Euler(0f, 0f, -spin * p);
                view.transform.position = Vector3.Lerp(aligned, mouth, p) + new Vector3(0f, 0f, 0.4f * p);   // ağza doğru akar
                SetHole(entry, holeSize, t + WarpOpenSeconds);
                SetHole(exit, exitSize, t + WarpOpenSeconds);
                yield return null;
            }
            if (coreIndex >= 0)
            {
                var core = rocketViews[coreIndex];
                core.BecomeInner(Board.GetRocket(coreIndex), palette, models);
                peeling.Remove(coreIndex);
            }

            // Çıkış deliğinden taşar: küçükten büyür, yukarı uzayıp toparlanır (sönen sallanma).
            view.transform.rotation = Quaternion.identity;
            var rise = exitAt + Vector3.up * (0.2f * track.Scale) + Vector3.back * ExitLayer;
            var close = 0f;
            for (var t = 0f; t < WarpRiseSeconds; t += Time.deltaTime)
            {
                var p = t / WarpRiseSeconds;
                var grow = Tween.Evaluate(Tween.Ease.OutQuad, Mathf.Clamp01(p * 1.4f));
                var jiggle = 0.25f * Mathf.Sin(p * Mathf.PI * 3f) * (1f - p);
                view.transform.localScale = Vector3.Scale(startScale, new Vector3(grow * (1f - jiggle), grow * (1f + jiggle), grow));
                view.transform.position = Vector3.Lerp(exitAt, rise, Tween.Evaluate(Tween.Ease.OutQuad, p));
                close = Mathf.Clamp01((p - 0.5f) / 0.5f);
                SetHole(entry, holeSize * (1f - close * close), t, 1f + 2f * close);   // kapanırken hızlanarak dönüp küçülür
                SetHole(exit, exitSize, t);
                yield return null;
            }
            view.transform.localScale = startScale;
            view.transform.position = rise;
            Destroy(entry.gameObject);
            StartCoroutine(CloseHole(exit, exitSize));

            yield return LaunchPieces(view, spec, rise, pieceSlots, units);
        }

        static void SetHole(SpriteRenderer hole, float size, float time, float spinBoost = 1f)
        {
            if (!hole) return;
            hole.transform.localScale = Vector3.one * Mathf.Max(0f, size);
            // Kendi etrafında sürekli döner (saat yönünde, sarmal kolların açıldığı yöne ters); kapanışta hızlanır.
            hole.transform.localRotation *= Quaternion.Euler(0f, 0f, -PortalSpin * spinBoost * Time.deltaTime);
        }

        IEnumerator CloseHole(SpriteRenderer hole, float size)
        {
            for (var t = 0f; t < WarpCloseSeconds && hole; t += Time.deltaTime)
            {
                var p = t / WarpCloseSeconds;
                SetHole(hole, size * (1f - p * p), t, 1f + 2f * p);
                yield return null;
            }
            if (hole) Destroy(hole.gameObject);
        }
    }
}
