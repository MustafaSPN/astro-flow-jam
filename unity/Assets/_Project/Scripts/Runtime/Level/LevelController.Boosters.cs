using System.Collections;
using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class LevelController : MonoBehaviour
    {
        void RefreshBoosterCount(int b) => hud.SetBoosterCount(b, BoosterInventory.Get(b));

        bool HasBooster(int b) => BoosterInventory.Get(b) > 0;

        void SpendBooster(int b)
        {
            BoosterInventory.TrySpend(b);
            RefreshBoosterCount(b);
            boostersUsed++;
            Telemetry.BoosterUsed(LevelNumber, BoosterTitles[b]);
            if (b == tutorialBooster) FinishBoosterTutorial();
        }
        int hangarUses;                        // Hangar bölümde en çok HangarMaxUses kez: her biri +1 yuva ve +1 yörünge
        const int HangarMaxUses = 3;           // 5 → 8 kapasite
        /// <summary>Freeze: süre 10 sn durur.</summary>
        const float TimeFreezeSeconds = 10f;
        float freezeLeft;

        void UseBooster(int booster)
        {
            if (!InputEnabled || finishing || GuideActive) return;
            NotifyMove();
            // Joker tanıtımı: yalnız tanıtılan joker basılır; seçim modundayken yeniden basmak seçimi iptal etmez.
            // "Try it!" dokunuşu aynı anda alttaki joker butonuna da düşebilir: tanıtım açıldıktan hemen sonraki basış sayılmaz.
            if (tutorialBooster >= 0 && (booster != tutorialBooster || IsPickingForBooster || Time.unscaledTime - tutorialShownAt < TutorialTapGuard)) return;
            if (boosterLocked[booster]) { ShowMessage($"Unlocks at level {BoosterInventory.UnlockLevel[booster]}"); return; }
            // Adet bitti: satın alma penceresi (seçim modundaki joker yeniden basılırsa iptal edilir, pencere açılmaz).
            var picking = (booster == BoosterUfo && ufoPicking) || (booster == BoosterWarp && warpPicking);
            if (!picking && !(booster == BoosterHangar && hangarUses >= HangarMaxUses) && !HasBooster(booster)) { OpenBuy(booster); return; }
            if (booster == BoosterHangar) { UseHangar(); return; }
            if (booster == BoosterUfo) { ToggleUfoPick(); if (tutorialBooster >= 0) hud.EndBoosterTutorial(); return; }
            if (booster == BoosterWarp) { ToggleWarpPick(); if (tutorialBooster >= 0) hud.EndBoosterTutorial(); return; }
            if (booster != BoosterTimeFreeze) { ShowMessage($"{BoosterTitles[booster]}: coming soon"); return; }
            // Süresiz bölümde ya da süre zaten donukken kullanılmaz (dondurma bitene kadar buton kilitli).
            if (TimeLimit <= 0 || freezeLeft > 0f) return;
            SpendBooster(booster);
            freezeLeft = TimeFreezeSeconds;
            Sfx.Play(Sfx.Freeze);
            Haptics.Select();
        }

        // Hangar: bölüm boyunca bekleme sırasına bir yuva ve yörüngeye bir birim (kartuşuyla) eklenir; bölümde en çok 3 kez.
        void UseHangar()
        {
            if (hangarUses >= HangarMaxUses) { ShowMessage("Max capacity reached"); return; }
            SpendBooster(BoosterHangar);
            Sfx.Play(Sfx.Booster);
            hangarUses++;
            Orbit.Expand(1);
            track.AddCartridge();
            runner.AddSlot();
            Haptics.Select();
        }
    }
}
