using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class GameHud
    {
        PausePanel pausePanel;

        void BuildPausePanel() =>
            pausePanel = new PausePanel(transform, DialogPopup.DefaultLayer, TogglePause, () => RestartPressed?.Invoke(), () => HomePressed?.Invoke());

        /// <summary>Duraklatma açıksa kapatır (ana ekrana dönerken, yeniden başlatırken).</summary>
        public void Unpause()
        {
            if (Paused) TogglePause();
        }

        void TogglePause()
        {
            Paused = !Paused;
            Time.timeScale = Paused ? 0f : 1f;
            pausePanel.SetVisible(Paused);
        }
    }
}
