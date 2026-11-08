using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Ekonomi akışları: adedi biten jokere basınca 3'lü paket satın alma; bölüm kaybedilince devam teklifi.
    /// Devam: ilk kez bedava (bir kerelik), sonra bölüm içinde coin merdiveni 900/1900/2900/3900/4900. Süre
    /// bitince +30 sn, yer kalmayınca +1 bekleme yuvası. Vazgeçince bölüm yeniden başlar. Diyaloglar açıkken süre durur.
    /// </summary>
    public sealed partial class LevelController
    {
        enum FailKind { Time, Space }

        static readonly int[] ContinuePrices = { 900, 1900, 2900, 3900, 4900 };
        const int ContinueSeconds = 30;
        static readonly string[] BoosterDescriptions =
        {
            "Adds one extra waiting slot and orbit space for this level.",
            "Warps any block out through a wormhole.",
            "Stops the clock for 10 seconds.",
            "Beams up every block and pixel of one color.",
        };

        int continuesUsed, boostersUsed, startPixels;
        string levelVersion = "";

        int ProgressPercent => startPixels > 0 ? Mathf.RoundToInt(100f * (startPixels - PixelsLeft) / startPixels) : 0;

        int LevelNumber => levelIndex + 1;
        int PixelsLeft => beads != null ? beads.Remaining : 0;
        string FailName => failKind == FailKind.Time ? "time" : "space";

        // Aynı bölüm art arda kaçıncı kez açılıyor (bölüm değişince 1'e döner); ölçüm için.
        static int NextAttempt(int level)
        {
            var saved = GameSave.GetString(SaveKeys.LevelAttempt, "").Split('|');
            var count = saved.Length == 2 && saved[0] == level.ToString() && int.TryParse(saved[1], out var n) ? n + 1 : 1;
            GameSave.SetString(SaveKeys.LevelAttempt, $"{level}|{count}");
            return count;
        }
        FailKind failKind;
        bool dialogOpen;

        void OpenBuy(int booster)
        {
            var price = BoosterInventory.PackPrice[booster];
            dialogOpen = true;
            input.CancelDrag();
            hud.ShowDialog(BoosterTitles[booster], GameHud.BoosterIcon(booster), BoosterDescriptions[booster],
                "", price, null,
                () =>
                {
                    if (!BoosterInventory.TryBuyPack(booster)) { hud.DialogDenied(); ShowMessage("Not enough coins"); return; }
                    RefreshBoosterCount(booster);
                    Telemetry.BoosterBought(LevelNumber, BoosterTitles[booster], price);
                    Telemetry.CoinsSpent("booster_pack", price);
                    Haptics.GateExit(this);
                    CloseDialog();
                },
                null, CloseDialog, caption: $"Buy x{BoosterInventory.PackSize}");
        }

        void CloseDialog()
        {
            dialogOpen = false;
            hud.HideDialog();
        }

        void Fail(FailKind kind)
        {
            if (!InputEnabled) return;
            InputEnabled = false;
            input.CancelDrag();
            CancelBoosterPick();
            Sfx.Play(Sfx.LevelFail);
            failKind = kind;
            dialogOpen = true;
            Telemetry.LevelFail(LevelNumber, levelVersion, FailName, levelTime, PixelsLeft, ProgressPercent);
            var free = GameSave.GetInt(SaveKeys.FreeContinueUsed, 0) == 0;
            var price = free ? -1 : ContinuePrices[Mathf.Min(continuesUsed, ContinuePrices.Length - 1)];
            var title = kind == FailKind.Time ? "Out of Time!" : "Out of Space!";
            var body = kind == FailKind.Time ? $"Get +{ContinueSeconds} seconds and keep playing." : "Get one extra slot and keep playing.";
            var icon = kind == FailKind.Time ? GameHud.TimeoutIcon : GameHud.BoosterIcon(BoosterHangar);
            hud.ShowDialog(title, icon, body, free ? "Free Continue" : "Continue", price, "Give Up",
                () => TryContinue(free, price), GiveUp, null, kind == FailKind.Time ? 270f : 230f, "You Lose!", secondaryRed: true);
        }

        void TryContinue(bool free, int price)
        {
            if (free) GameSave.SetInt(SaveKeys.FreeContinueUsed, 1);
            else if (!CoinWallet.TrySpend(price)) { hud.DialogDenied(); ShowMessage("Not enough coins"); return; }
            GameSave.Save();
            Telemetry.ContinueUsed(LevelNumber, FailName, free ? 0 : price);
            if (!free) Telemetry.CoinsSpent("continue", price);
            if (!free) continuesUsed++;            // bedava devam merdiveni ilerletmez: ilk ücretli devam 900
            CloseDialog();
            if (failKind == FailKind.Time) { timeLeft += ContinueSeconds; tickClock = 0f; hud.ResetTimer(); }
            else runner.AddSlot();
            InputEnabled = true;
            Haptics.GateExit(this);
        }

        /// <summary>Geliştirme: 'afj_dev_fail' 1 (süre) ya da 2 (yer) ise bölüm açıldıktan biraz sonra o kayıp penceresi açılır (tek seferlik).</summary>
        System.Collections.IEnumerator DevFail()
        {
            var kind = GameSave.GetInt(SaveKeys.DevFail, 0);
            if (kind == 0) yield break;
            GameSave.DeleteKey(SaveKeys.DevFail);
            yield return new WaitForSeconds(2f);
            Fail(kind == 1 ? FailKind.Time : FailKind.Space);
        }

        // Vazgeç: 1 can gider, ana ekrana dönülür.
        void GiveUp()
        {
            Telemetry.LevelEnd(LevelNumber, levelVersion, "give_up_" + FailName, levelTime, PixelsLeft, ProgressPercent, continuesUsed, boostersUsed);
            CloseDialog();
            Lives.Lose();
            GoHome();
        }

        // Geliştirici menüsü: kalan süreyi ±10 sn değiştirir (süreli bölümde; en az 1 sn kalır).
        void DevAddTime(float seconds)
        {
            if (TimeLimit <= 0 || level == null) { ShowMessage("No time limit"); return; }
            timeLeft = Mathf.Max(1f, timeLeft + seconds);
            hud.ResetTimer();
            hud.SetTimeLeft(timeLeft);
            hud.TimeWarningAt(timeLeft);
        }

        // Joker tanıtımı: joker açıldığı bölümde ilk kez tanıtılır ve oyuncuya bir kez zorla kullandırılır.
        // Tanıtım bitene kadar süre durur; bölüm yeniden başlarsa tanıtım tekrar gelir.
        int tutorialBooster = -1;
        float tutorialShownAt;
        const float TutorialTapGuard = 0.35f;
        const float IntroDelay = 0.6f;

        System.Collections.IEnumerator BoosterIntro()
        {
            var b = -1;
            for (var i = 0; i < BoosterInventory.Count; i++)
                if (levelIndex + 1 == BoosterInventory.UnlockLevel[i] && GameSave.GetInt(SaveKeys.BoosterTutorial(i), 0) == 0) { b = i; break; }
            if (b < 0) yield break;
            dialogOpen = true;                                 // süre tanıtım boyunca durur
            yield return new WaitForSeconds(IntroDelay);
            if (BoosterInventory.Get(b) <= 0) { BoosterInventory.DevAdd(b, 1); RefreshBoosterCount(b); }   // zorunlu kullanım için en az bir adet
            input.CancelDrag();
            // Süresiz bölümde Time Freeze denenemez: yalnız tanıtılır.
            var canTry = b != BoosterTimeFreeze || TimeLimit > 0;
            var free = BoosterInventory.FreeOnUnlock;
            hud.ShowDialog(BoosterTitles[b], GameHud.BoosterIcon(b), BoosterDescriptions[b], canTry ? "Try it!" : "Got it!", -1, null,
                () =>
                {
                    hud.HideDialog();
                    if (!canTry) { FinishBoosterTutorial(); return; }
                    tutorialBooster = b;
                    tutorialShownAt = Time.unscaledTime;
                    hud.ShowBoosterTutorial(b);
                },
                null, null, 250f, "New Booster!", subtitleColor: PopupArt.Gold, caption: $"+{free} free");
        }

        // Geliştirici menüsü: joker tanıtımları sıfırlanır, bölüm yeniden başlar (açılış bölümündeyse tanıtım hemen gelir).
        void DevResetTutorials()
        {
            for (var i = 0; i < BoosterInventory.Count; i++) GameSave.DeleteKey(SaveKeys.BoosterTutorial(i));
            ResetGuidePrefs();
            GameSave.Save();
            LoadLevel(levelIndex);
            ShowMessage("Tutorials reset");
        }

        void FinishBoosterTutorial()
        {
            for (var i = 0; i < BoosterInventory.Count; i++)
                if (levelIndex + 1 == BoosterInventory.UnlockLevel[i]) GameSave.SetInt(SaveKeys.BoosterTutorial(i), 1);
            GameSave.Save();
            tutorialBooster = -1;
            hud.EndBoosterTutorial();
            dialogOpen = false;
        }
    }
}
