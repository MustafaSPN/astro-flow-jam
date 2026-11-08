using System.Collections;
using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// UFO jokeri: oyuncu tahtadan bir blok seçer (rengi seçilir; katmanlıda dış renk). Ekranın üstünden bir UFO süzülerek
    /// gelir, coin göstergesinin üstünde durur ve o renkteki her şeyi sıvı gibi uzatıp incelterek içine çeker: tahtadaki
    /// bloklar (katmanlıda yalnız dış katman), yörüngedeki ve slotlardaki roketler, resimdeki pikseller. Tahtadan alınan her
    /// blok çıkış sayılır (buz ve asteroit sayaçları azalır, anahtar kilidi açar). İç katmanı bu renkte olan katmanlı
    /// bloklar tek katmana iner (o renk artık resimde yok). Gösteri sürerken giriş kapalı, süre durur.
    /// UFO görseli yer tutucudur (arayüzde kodla çizilen sprite); tasarım sonra.
    /// </summary>
    public sealed partial class LevelController
    {
        bool ufoPicking, ufoBusy;
        const float UfoFlyInSeconds = 0.6f, UfoFlyOutSeconds = 0.45f, UfoHoverAfter = 0.25f;
        const float SuckStagger = 0.45f;
        const float UfoDepth = -1.5f;          // çekilenler kameraya doğru yükselerek akar
        const float BeamOpenSeconds = 0.25f;

        void ToggleUfoPick()
        {
            if (ufoBusy) return;
            warpPicking = false;
            if (!ufoPicking && !HasBooster(BoosterUfo)) return;
            ufoPicking = !ufoPicking;
            input.CancelDrag();
            if (ufoPicking) hud.ShowBoosterPrompt(BoosterUfo, "UFO Attack", "Select a color", tutorialBooster >= 0 ? null : CancelBoosterPick);
            else hud.HideBoosterPrompt();
        }

        /// <summary>Joker hedef seçimi bekleniyor (UFO: renk seçimi).</summary>
        public bool IsPickingForBooster => ufoPicking || warpPicking;

        /// <summary>BoardInput: dokunulan blok jokerin hedefi olduysa true (blok tutulmaz).</summary>
        public bool TryPickForBooster(int index)
        {
            if (warpPicking) return TryWarp(index);
            if (!ufoPicking) return false;
            var spec = Board.GetRocket(index);
            if (spec.isBlocker) return true;
            return PickUfoColor(spec.color);
        }

        /// <summary>
        /// Joker seçimi sırasında tahta dışına dokunma: UFO resimdeki bir pikselin rengini de seçebilir (dokunulan noktadaki
        /// henüz temizlenmemiş piksel). Seçim başka hiçbir dokunuşla iptal olmaz; yalnız panelin kapatma butonuyla.
        /// </summary>
        public void PickOutsideBoard(Vector2 screen)
        {
            if (!ufoPicking || beads == null || !field) return;
            var ray = worldCamera.ScreenPointToRay(screen);
            var planeZ = field.CellWorld(0).z;
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return;
            var hit = ray.origin + ray.direction * ((planeZ - ray.origin.z) / ray.direction.z);
            var g = field.WorldToGrid(hit);
            int col = Mathf.FloorToInt(g.x), row = Mathf.FloorToInt(g.y);
            if (col < 0 || row < 0 || col >= beads.Columns || row >= beads.Rows) return;
            var i = beads.Index(col, row);
            if (beads.StateAt(i) == BeadState.Cleared) return;
            PickUfoColor(beads.ColorAt(i));
        }

        /// <summary>Panelin kapatma butonu ya da jokere tekrar basma: seçim iptal, joker harcanmaz.</summary>
        void CancelBoosterPick()
        {
            ufoPicking = warpPicking = false;
            hud.HideBoosterPrompt();
        }

        bool PickUfoColor(GameColor color)
        {
            ufoPicking = false;
            hud.HideBoosterPrompt();
            SpendBooster(BoosterUfo);
            Sfx.Play(Sfx.Ufo);
            Haptics.Select();
            StartCoroutine(UfoAttack(color));
            return true;
        }

        IEnumerator UfoAttack(GameColor color)
        {
            ufoBusy = true;
            InputEnabled = false;
            input.CancelDrag();

            // UFO (arayüz katmanında, HUD'un üstünde) ekranın üstünden iner, coin göstergesinin üstünde durur, çekim ışığını açar.
            var stage = new UfoStage(models, this);
            var ufo = hud.CreateUfo(stage.Texture, out var beam);
            var rest = hud.UfoRestPoint;
            var above = rest + Vector2.up * (hud.CanvasHeight * 0.35f);
            for (var t = 0f; t < UfoFlyInSeconds; t += Time.deltaTime)
            {
                ufo.anchoredPosition = Vector2.LerpUnclamped(above, rest, Tween.Evaluate(Tween.Ease.OutExpo, t / UfoFlyInSeconds));
                yield return null;
            }
            ufo.anchoredPosition = rest;
            var bob = StartCoroutine(UfoFx.Hover(ufo, rest));
            for (var t = 0f; t < BeamOpenSeconds; t += Time.deltaTime)
            {
                beam.localScale = new Vector3(1f, Tween.Evaluate(Tween.Ease.OutQuad, t / BeamOpenSeconds), 1f);
                yield return null;
            }
            beam.localScale = Vector3.one;
            // Kapıdan geçmekte olan roket bu renkteyse yörüngeye girmesi beklenir (sonra o da çekilir).
            while (passing > 0) yield return null;
            yield return new WaitForSeconds(UfoHoverAfter);

            // Çekilen her şey UFO'nun ağzına (ışığın üst ucu) akar.
            var mouth = ScreenBounds.ToPlane(worldCamera, hud.CanvasToScreen(rest + new Vector2(0f, -60f)), UfoDepth);
            var jobs = new List<Coroutine>();

            // Tahtadaki bloklar.
            for (var i = 0; i < Board.RocketCount; i++)
            {
                if (!Board.IsOnBoard(i)) continue;
                var spec = Board.GetRocket(i);
                if (spec.isBlocker || !rocketViews.TryGetValue(i, out var view)) continue;
                if (spec.color == color)
                {
                    Board.RemoveByBooster(i);
                    if (spec.layered)
                    {
                        // Dış katman kopyası çekilir; blok yerinde iç renge döner.
                        var shell = new GameObject($"Rocket {spec.id} shell").AddComponent<RocketView>();
                        shell.transform.SetParent(levelRoot, false);
                        shell.Build(-1, spec.OuterShell(), palette, models);
                        shell.transform.position = view.transform.position;
                        view.HideOuterLayer();
                        peeling.Add(i);
                        var index = i;
                        jobs.Add(StartCoroutine(UfoFx.Suck(shell.transform, levelRoot, mouth, Random.value * SuckStagger, () =>
                        {
                            view.BecomeInner(Board.GetRocket(index), palette, models);
                            peeling.Remove(index);
                        })));
                    }
                    else
                    {
                        rocketViews.Remove(i);
                        view.RemoveBoardPhysics();
                        jobs.Add(StartCoroutine(UfoFx.Suck(view.transform, levelRoot, mouth, Random.value * SuckStagger)));
                    }
                }
                else if (spec.layered && spec.innerColor == color)
                {
                    // İç katmanın rengi oyundan çıktı: blok tek katmana iner (dış renk kalır).
                    Board.StripInnerLayer(i);
                    view.BecomeInner(Board.GetRocket(i), palette, models);
                }
            }

            // Yörüngedeki, rampadaki ve slotlardaki roketler.
            foreach (var rocket in runner.TakeColor(color))
                jobs.Add(StartCoroutine(UfoFx.Suck(rocket.transform, levelRoot, mouth, Random.value * SuckStagger)));

            // Resimdeki pikseller (yolda mermisi olanlar mermiyle temizlenir).
            if (beads != null)
                for (var i = 0; i < beads.Columns * beads.Rows; i++)
                {
                    if (beads.ColorAt(i) != color || !beads.TryReserve(i)) continue;
                    beads.Clear(i);
                    var bead = field.TakeBead(i);
                    if (bead) jobs.Add(StartCoroutine(UfoFx.Suck(bead, levelRoot, mouth, Random.value * SuckStagger)));
                }

            foreach (var job in jobs) yield return job;
            yield return new WaitForSeconds(0.15f);

            // Işık kapanır, UFO yukarı çekilip gider.
            for (var t = 0f; t < BeamOpenSeconds; t += Time.deltaTime)
            {
                beam.localScale = new Vector3(1f, 1f - t / BeamOpenSeconds, 1f);
                yield return null;
            }
            StopCoroutine(bob);
            var at = ufo.anchoredPosition;
            for (var t = 0f; t < UfoFlyOutSeconds; t += Time.deltaTime)
            {
                ufo.anchoredPosition = Vector2.Lerp(at, above, Tween.Evaluate(Tween.Ease.InQuad, t / UfoFlyOutSeconds));
                yield return null;
            }
            Destroy(ufo.gameObject);
            stage.Dispose();

            ufoBusy = false;
            if (!finishing) InputEnabled = true;
            CheckFinished();
        }

        // Joker hedef seçimi odağı: Wormhole'da tahta, UFO'da tahta ve resim açık kalır, ekranın geri kalanı kararır;
        // hedef olabilecekler (Wormhole: bloklar, UFO: bloklar ve pikseller) hafifçe nabız atarak parlar.
        static readonly int GlowBlocksId = Shader.PropertyToID("_AfjGlowBlocks"), GlowPixelsId = Shader.PropertyToID("_AfjGlowPixels");
        const float PickGlowBase = 0.18f, PickGlowPulse = 0.14f, PickGlowSpeed = 4f;
        int focusedBooster = -1;

        void UpdatePickFocus()
        {
            var booster = ufoPicking ? BoosterUfo : warpPicking ? BoosterWarp : -1;
            if (booster != focusedBooster)
            {
                focusedBooster = booster;
                if (booster < 0) hud.HidePickDim();
                else
                {
                    var holes = new List<Rect> { ScreenRect(BoardView ? BoardView.gameObject : null) };
                    if (booster == BoosterUfo) holes.Add(ScreenRect(field ? field.gameObject : null));
                    holes.RemoveAll(r => r.width <= 0f);
                    hud.ShowPickDim(booster, holes);
                }
            }
            var glow = booster < 0 ? 0f : PickGlowBase + PickGlowPulse * Mathf.Sin(Time.unscaledTime * PickGlowSpeed);
            Shader.SetGlobalFloat(GlowBlocksId, glow);
            Shader.SetGlobalFloat(GlowPixelsId, booster == BoosterUfo ? glow : 0f);
        }

        Rect ScreenRect(GameObject root) => ScreenBounds.Of(worldCamera, root);
    }
}
