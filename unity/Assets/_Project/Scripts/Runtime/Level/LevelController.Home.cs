using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Ana ekran akışı ve can kuralları: oyun ana ekranla açılır; "Play" oynanacak bölümü başlatır
    /// (can yoksa can penceresi). Süre ilk hamleyle başlar. Süre başladıktan sonra ana ekrana dönmek ya da yeniden
    /// başlatmak 1 cana mal olur (onay sorulur); başlamadan çıkmak ücretsizdir. Kaybedip vazgeçmek 1 can götürür ve ana
    /// ekrana döner. Bölüm kazanılınca ilerleme kaydedilir, ödül toplanınca ana ekrana dönülür.
    /// </summary>
    public sealed partial class LevelController
    {
        bool timerStarted;

        /// <summary>Oynanacak bölümün sırası (kazanılan en yüksek bölümün bir sonrası); bölüm sayısına eşitse hepsi bitti.</summary>
        int Progress
        {
            get => Mathf.Clamp(GameSave.GetInt(SaveKeys.Progress, 0), 0, database.levels.Count);
            set
            {
                GameSave.SetInt(SaveKeys.Progress, Mathf.Clamp(value, 0, database.levels.Count));
                GameSave.Save();
            }
        }

        /// <summary>Son bölüm de kazanıldı: yeni bölümler gelene kadar Play bilgi verir.</summary>
        bool AllLevelsDone => Progress >= database.levels.Count;

        void SetupHome()
        {
            hud.SetupHome(database.levels.Count, n => PictureNameOf(database.levels[Mathf.Clamp(n - 1, 0, database.levels.Count - 1)]), n => database.levels[n - 1].isHard);
            hud.HomePlayPressed += PlayFromHome;
            hud.HomePressed += () => LeaveLevel(toHome: true);
            hud.DeleteAccountConfirmed += GoHome;            // ilerleme silindi: ana ekran 1. bölümle yeniden kurulur
            hud.DevLivesPressed += delta => { Lives.DevAdd(delta); hud.RefreshHome(); };
            hud.DevLevelPressed += delta =>
            {
                if (hud.HomeVisible)
                {
                    Progress += delta;
                    hud.SetHomeLevel(Progress + 1);
                }
                else
                {
                    LoadLevel(levelIndex + delta);
                    Progress = levelIndex;
                }
            };
        }

        void PlayFromHome()
        {
            if (AllLevelsDone) { hud.HomeToast("All levels done! New levels soon.", 3f); return; }
            if (Lives.Count <= 0) { Telemetry.OutOfLives(Progress + 1); hud.ShowNoLives(); return; }
            hud.HideHome();
            LoadLevel(Progress);
        }

        /// <summary>İlk hamle (blok tutma, slot roketi, joker): süre başlar.</summary>
        public void NotifyMove() => timerStarted = true;

        // Bölümü bırakma (ana ekran ya da yeniden başlat). Süre başladıysa onay sorulur ve 1 can gider.
        void LeaveLevel(bool toHome)
        {
            hud.Unpause();
            if (!timerStarted || finishing || !levelRoot)
            {
                if (toHome) GoHome(); else LoadLevel(levelIndex);
                return;
            }
            dialogOpen = true;
            input.CancelDrag();
            // Can penceresi düzeni: kalbin içinde kalan can, sarı uyarı, aynı boyda iki buton, dışarı dokununca devam.
            hud.ShowDialog(toHome ? "Quit Level?" : "Restart Level?", UiArt.Asset("Home/Heart"), "Your progress in this level will be lost.",
                "Keep Playing", -1, toHome ? "Quit" : "Restart", CloseDialog,
                () =>
                {
                    CloseDialog();
                    Telemetry.LevelEnd(LevelNumber, levelVersion, toHome ? "quit" : "restart", levelTime, PixelsLeft, ProgressPercent, continuesUsed, boostersUsed);
                    Lives.Lose();
                    if (toHome || Lives.Count <= 0) GoHome(); else LoadLevel(levelIndex);
                },
                CloseDialog, 220f, secondaryRed: true, caption: Lives.IsInfinite ? "Unlimited lives active" : "You will lose 1 life", tapToClose: true, extraBottom: 70f);
            hud.SetDialogIconText(Lives.Count.ToString());
        }

        /// <summary>Bölüm kapanır, ana ekran açılır.</summary>
        void GoHome()
        {
            StopAllCoroutines();
            hud.Unpause();
            hud.HideDialog();
            hud.HideGuide();
            hud.HidePickDim();
            hud.HideBoosterPrompt();
            hud.EndBoosterTutorial();
            dialogOpen = false;
            InputEnabled = false;
            if (levelRoot) Destroy(levelRoot.gameObject);
            levelRoot = null;
            SwitchPicture(null);                        // ana ekranda oyun resmi bellekte kalmaz
            rocketViews.Clear();
            Shader.SetGlobalFloat("_AfjGlowBlocks", 0f);
            Shader.SetGlobalFloat("_AfjGlowPixels", 0f);
            hud.ShowHome(Progress + 1);
            StartCoroutine(AskTrackingSoon());
        }

        // Reklam takibi izni: 3. bölümden sonra ana ekrana dönünce, ekran oturduktan kısa süre sonra (bir kez).
        System.Collections.IEnumerator AskTrackingSoon()
        {
            if (Progress < Tracking.AskAfterLevel) yield break;
            while (hud.LoadingVisible) yield return null;    // açılışta yükleme ekranı bitmeden sorma
            yield return new WaitForSecondsRealtime(0.8f);
            if (hud.HomeVisible) Tracking.MaybeAsk(Progress);
        }
    }
}
