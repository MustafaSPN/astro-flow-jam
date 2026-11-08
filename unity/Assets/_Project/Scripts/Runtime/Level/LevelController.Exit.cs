using System.Collections;
using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class LevelController : MonoBehaviour
    {
        /// <summary>
        /// Kapı eşiğindeki uyan roketin çıkışı. Bantta yer varsa birimler atomik olarak ayrılır, roket tahtadan kalkar ve
        /// kapı animasyonu başlar; yoksa çıkış olmaz.
        /// </summary>
        public bool TryExit(int index, Dir dir)
        {
            var spec = Board.GetRocket(index);
            if (Board.CheckExit(index, dir, out var exitIndex) != ExitCheck.Ok) return false;
            // Parçalar önce bandın boş yerine, kalanlar boş slotlara gider; ikisi birlikte yetmezse blok çıkamaz.
            var toBelt = Mathf.Min(Orbit.FreeUnits, spec.OrbitUnits);
            var toSlots = spec.OrbitUnits - toBelt;
            if (runner.FreeSlotCount < toSlots) return false;
            Orbit.TryReserve(toBelt);
            var pieceSlots = new List<bool>();
            for (var i = 0; i < spec.OrbitUnits; i++) pieceSlots.Add(i >= toBelt && runner.ReserveSlot());

            var view = rocketViews[index];
            var aligned = BoardView.CellToWorld(RocketView.CenterOf(spec.origin, spec.size));
            Board.RemoveForExit(index, dir);
            if (spec.layered)
            {
                // Katmanlı blok: dış katmanın kopyası kapıdan sıvı gibi akar; blok yerinde kalır, çekirdeği iç renkte büyür.
                var shellSpec = spec.OuterShell();
                var shell = new GameObject($"Rocket {spec.id} shell").AddComponent<RocketView>();
                shell.transform.SetParent(levelRoot, false);
                shell.Build(-1, shellSpec, palette, models);
                shell.transform.position = view.transform.position;
                SettleOnBoard(view, aligned);
                view.HideOuterLayer();
                peeling.Add(index);
                StartCoroutine(PassThroughDoor(shell, shellSpec, BoardView.Gates[exitIndex], aligned, pieceSlots, liquid: true,
                    onShellGone: () =>
                    {
                        view.BecomeInner(Board.GetRocket(index), palette, models);
                        peeling.Remove(index);
                    }));
            }
            else
            {
                rocketViews.Remove(index);
                StartCoroutine(PassThroughDoor(view, spec, BoardView.Gates[exitIndex], aligned, pieceSlots));
            }
            Haptics.GateExit(this);
            Sfx.Play(Sfx.GateExit);
            GuideOnExit(index);
            return true;
        }

        // Tahtada kalan blok (katmanlı bloğun çekirdeği) hücresine oturur, sürükleme hızı sıfırlanır.
        static void SettleOnBoard(RocketView view, Vector3 aligned)
        {
            view.SetSelected(false);
            var body = view.Body;
            if (body)
            {
                body.linearVelocity = Vector2.zero;
                body.bodyType = RigidbodyType2D.Kinematic;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.interpolation = RigidbodyInterpolation2D.None;
                body.position = aligned;
            }
            view.transform.position = aligned;
        }

        /// <summary>Kapı eşiğindeki roket çıkamadı: kapı ve blok sarsılır (yazılı uyarı yok).</summary>
        public void ExitRejected(int index, Dir dir, ExitCheck check)
        {
            Sfx.Play(Sfx.Wrong);
            var spec = Board.GetRocket(index);
            switch (check)
            {
                case ExitCheck.WrongColor:
                    BoardView.ShakeGate(dir, spec.origin, spec.size);
                    rocketViews[index].ShakeScale(BoardTuning.ShakeSeconds, BoardTuning.ShakeStrength, BoardTuning.ShakeVibrato);
                    break;
                case ExitCheck.TooNarrow:
                    BoardView.ShakeGate(dir, spec.origin, spec.size);
                    rocketViews[index].ShakeScale(BoardTuning.ShakeSeconds, BoardTuning.ShakeStrength, BoardTuning.ShakeVibrato);
                    break;
                case ExitCheck.Ok:
                    break;   // yörünge ve slotlar dolu: blok tahtada kalır
            }
        }

        // Kapıdan çıkış: kapı açılır, blok 0,1 sn'de hücre hizasına oturur ve 0,5 sn'de açık kapıdan geçip kapının dışına
        // çıkar (görünür kalır; maskeleme yok); ardından kapı kapanır. Çok hücreli blok önce hücrelerine ayrılır, her
        // hücrenin içinden burun, kanatlar ve motor çıkarak roket oluşur ve roketler alev atarak sırayla yörüngeye uçar.
        IEnumerator PassThroughDoor(RocketView view, RocketSpec spec, GateView gate, Vector3 aligned, List<bool> pieceSlots,
            bool liquid = false, System.Action onShellGone = null)
        {
            var units = spec.OrbitUnits;
            passing++;
            passingUnits += units;
            view.RemoveBoardPhysics();
            gate.Open();

            // Hizalama yalnız kapıya dik eksende: oyuncunun kapıya ittiği kısım geri çekilmez, geçiş oradan sürer.
            var from = view.transform.position;
            var pushed = Mathf.Max(0f, Vector3.Dot(from - aligned, gate.Outward));
            var alignTo = aligned + gate.Outward * pushed;
            for (var t = 0f; t < BoardTuning.ExitAlignSeconds; t += Time.deltaTime)
            {
                view.transform.position = Vector3.LerpUnclamped(from, alignTo, Tween.Evaluate(Tween.Ease.OutQuad, t / BoardTuning.ExitAlignSeconds));
                yield return null;
            }
            view.transform.position = alignTo;

            // Blok, arka yüzü kapıyı geçene kadar dışarı kayar.
            var halfDepth = (Mathf.Abs(gate.Outward.x) * spec.size.x + Mathf.Abs(gate.Outward.y) * spec.size.y) / 2f;
            var passTo = gate.LinePoint + gate.Outward * (halfDepth + ExitClearance)
                         + Vector3.ProjectOnPlane(aligned - gate.LinePoint, gate.Outward);
            passTo.z = aligned.z - ExitLayer;   // geçerken kameraya doğru yükselir (bekleme alanındaki roketlerin üst katmanı)
            if (liquid)
            {
                // Sıvı katman: kabuk kapıya doğru uzar, enine daralır, yüzeyi dalgalanır. Kabuk kapıyı geçince toparlanır
                // (sönen bir sallanmayla) ve çekirdek tahtada büyür.
                var horizontal = Mathf.Abs(gate.Outward.x) > 0.5f;
                for (var t = 0f; t < LiquidPassSeconds; t += Time.deltaTime)
                {
                    var p = t / LiquidPassSeconds;
                    var flow = Mathf.Sin(p * Mathf.PI);
                    var wobble = 1f + 0.07f * Mathf.Sin(t * 38f) * (1f - p);
                    var along = 1f + 0.75f * flow;
                    var across = (1f - 0.32f * flow) * wobble;
                    view.transform.localScale = horizontal ? new Vector3(along, across, 1f - 0.3f * flow) : new Vector3(across, along, 1f - 0.3f * flow);
                    view.transform.position = Vector3.Lerp(alignTo, passTo, Tween.Evaluate(Tween.Ease.InOutSine, p));
                    yield return null;
                }
                view.transform.position = passTo;
                onShellGone?.Invoke();
                for (var t = 0f; t < LiquidSettleSeconds; t += Time.deltaTime)
                {
                    var p = t / LiquidSettleSeconds;
                    var jiggle = 0.12f * Mathf.Sin(p * Mathf.PI * 3f) * (1f - p);
                    view.transform.localScale = horizontal ? new Vector3(1f - jiggle, 1f + jiggle, 1f) : new Vector3(1f + jiggle, 1f - jiggle, 1f);
                    yield return null;
                }
                view.transform.localScale = Vector3.one;
            }
            else
            {
                for (var t = 0f; t < BoardTuning.ExitPassSeconds; t += Time.deltaTime)
                {
                    view.transform.position = Vector3.Lerp(alignTo, passTo, t / BoardTuning.ExitPassSeconds);
                    yield return null;
                }
                view.transform.position = passTo;
            }
            gate.Close();
            yield return LaunchPieces(view, spec, passTo, pieceSlots, units);
        }

        // Bloğun her hücresinin yerine, parçaları gövdenin içine toplanmış bir roket konur; blok görünümü kalkar, roketler
        // açılır ve sırayla yörüngeye (ya da ayrılmış slota) uçar. Çağıran passing / passingUnits'i önceden artırmıştır.
        IEnumerator LaunchPieces(RocketView view, RocketSpec spec, Vector3 passTo, List<bool> pieceSlots, int units)
        {
            // Her hücrenin yerine, parçaları gövdenin içine toplanmış bir roket konur; blok görünümü kalkar.
            var pieces = new List<(RocketView view, Vector3 start, Vector3 spread)>();
            var index = 0;
            foreach (var c in spec.LocalCells)
            {
                var offset = new Vector3(c.x + 0.5f - spec.size.x / 2f, c.y + 0.5f - spec.size.y / 2f, 0f);
                var piece = new GameObject($"Rocket {spec.id}.{index}").AddComponent<RocketView>();
                piece.transform.SetParent(levelRoot, false);
                piece.Build(-1, spec.Piece(index), palette, models, asRocket: true);
                piece.Collapse();
                piece.MorphFromBlock(models.block, MorphSeconds);
                var start = passTo + offset;
                piece.transform.position = start;
                pieces.Add((piece, start, offset * SeparationSpread));
                index++;
            }
            Destroy(view.gameObject);

            // Ayrılma: çok hücreli blokta hücreler blok merkezinden dışarı doğru açılır.
            if (pieces.Count > 1)
                for (var t = 0f; t < SeparationSeconds; t += Time.deltaTime)
                {
                    var k = Tween.Evaluate(Tween.Ease.OutQuad, t / SeparationSeconds);
                    foreach (var p in pieces) p.view.transform.position = p.start + p.spread * k;
                    yield return null;
                }

            // Gövdeye dönüşüm bitince kanatlar ve motor gövdenin içinden açılır.
            var morphLeft = MorphSeconds - (pieces.Count > 1 ? SeparationSeconds : 0f);
            if (morphLeft > 0f) yield return new WaitForSeconds(morphLeft * 0.7f);
            foreach (var p in pieces) p.view.Unfold(UnfoldSeconds);
            yield return new WaitForSeconds(UnfoldSeconds);

            passing--;
            passingUnits -= units;
            for (var i = 0; i < pieces.Count; i++)
                runner.Enter(pieces[i].view, pieces[i].view.transform.position, i * OrbitTuning.PieceLaunchInterval, pieceSlots[i]);
        }
    }
}
