using System.Collections;
using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class LevelController : MonoBehaviour
    {
        // Geliştirici kısayolu: kalan tüm pikselleri patlatır; bölüm sonu (kilit, kapı, kamera) hemen başlar.
        void DebugClearBeads()
        {
            if (beads == null || finishing || !InputEnabled) return;
            for (var i = 0; i < beads.Columns * beads.Rows; i++)
            {
                if (!beads.TryReserve(i)) continue;   // temizlenmiş ya da mermisi yolda olanlar atlanır
                beads.Clear(i);
                field.Pop(i);
            }
            CheckFinished();
        }

        // Tek piksel kalınca kamera yörüngeye yaklaşır: son atış ve roketin kilide çarpması yakından izlenir.
        void ZoomOnLastPixel()
        {
            if (!zoomed && beads != null && beads.Remaining == 1) StartCoroutine(ZoomToPicture());
        }

        void CheckFinished()
        {
            if (finishing || nextLevelTimer > 0f || beads == null || beads.Remaining > 0) return;
            finishing = true;
            InputEnabled = false;
            StartCoroutine(Finale());
        }

        // Bölüm sonu: son roket kilide çarpar, küçük patlama, kilit fırlar, kapı açılır ve kamera resme yaklaşır.
        IEnumerator Finale()
        {
            // Yakınlaşma henüz yapılmadıysa (pikseller birden bittiyse) önce yaklaşılır, sonra roket kilide uçar.
            if (!zoomed)
            {
                StartCoroutine(ZoomToPicture());
                yield return new WaitForSeconds(ZoomBeforeHeadbutt);
            }
            var target = door ? door.LockPoint : track.transform.position;
            var impacted = false;
            var direction = Vector3.up;
            if (!runner.Headbutt(target, d => { impacted = true; direction = d; })) impacted = true;
            while (!impacted) yield return null;

            Sfx.Play(Sfx.LockBreak);
            if (models.flameMaterial) RewardDoor.Explosion(target, models.flameMaterial, new Color(1f, 0.6f, 0.2f), levelRoot);
            Haptics.GateExit(this);
            if (door) door.Break(direction, models.flameMaterial);
            yield return new WaitForSeconds(0.9f);
            // Resim açıldı: yörüngenin çevresinde havai fişekler; resim bir süre ekranda kalır.
            if (models.flameMaterial)
                StartCoroutine(Fireworks.Show(track.transform.position, OrbitDesign.OuterExtent * OrbitDesign.ScaleFor(PictureSize),
                    FireworksSeconds, models.flameMaterial, levelRoot));
            // Resim bir süre izlenir, sonra bölüm sonu popup'ı: ödül toplanınca coinler göstergeye uçar ve sonraki bölüm açılır.
            yield return new WaitForSeconds(PictureShowSeconds);
            Progress = Mathf.Max(Progress, levelIndex + 1);
            Telemetry.LevelEnd(LevelNumber, levelVersion, "win", levelTime, 0, 100, continuesUsed, boostersUsed);
            Telemetry.SetPlayer(Progress, ShopCatalog.AdsRemoved);
            Sfx.Play(Sfx.LevelWin);
            hud.ShowLevelComplete(levelIndex + 1, CoinWallet.LevelReward, () => Ads.ShowInterstitialAfterWin(levelIndex + 1, GoHome));
        }

        // Kamera açısı korunur; resim (kapı çerçevesi) ekranın ortasına gelip ekranı dolduracak kadar yaklaşılır.
        IEnumerator ZoomToPicture()
        {
            if (zoomed) yield break;
            zoomed = true;
            var half = OrbitDesign.OuterExtent * OrbitDesign.ScaleFor(PictureSize) * 1.04f;   // yörüngenin dış rayı ekrana sığar
            yield return levelCamera.ZoomTo(track.transform.position, half, cameraFov, FinaleZoomSeconds);
        }

        // Jokerler; sıra GameHud'daki butonlarla aynı. Adetler, kilitler ve fiyatlar BoosterInventory'de (kalıcı).
        static readonly string[] BoosterTitles = { "Station Expansion", "Wormhole", "Time Freeze", "UFO Attack" };
        const int BoosterHangar = 0, BoosterWarp = 1, BoosterTimeFreeze = 2, BoosterUfo = 3;
        // Adetler ve kilitler BoosterInventory'de (kalıcı); açılış bölümünden önce kilitli görünür.
        readonly bool[] boosterLocked = new bool[BoosterInventory.Count];
        int unlockedNow = -1;                  // bu bölümde açılan joker (hediye verildi)
    }
}
