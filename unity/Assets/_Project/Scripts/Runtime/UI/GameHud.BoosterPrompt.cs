using System;

namespace AstroFlowJam
{
    public sealed partial class GameHud
    {
        BoosterPrompt boosterPrompt;

        /// <summary>Joker hedef seçimi panelini açar; kapatma butonu onClose'u çağırır (panel çağıran tarafından kapanır).</summary>
        public void ShowBoosterPrompt(int booster, string title, string text, Action onClose)
        {
            boosterPrompt ??= new BoosterPrompt(safeArea);
            boosterPrompt.Show(BoosterIcon(booster), title, text, onClose);
        }

        public void HideBoosterPrompt() => boosterPrompt?.Hide();
    }
}
