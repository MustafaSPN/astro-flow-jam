using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    public sealed partial class GameHud
    {
        // ------------------------------------------------------------------ ayarlar
        SettingsPanel settingsPanel;

        void OpenSettings()
        {
            settingsPanel ??= new SettingsPanel(transform, HomePopupLayer, this, ConfirmDeleteAccount, text => HomeToast(text));
            settingsPanel.Open();
        }

        public event Action DeleteAccountConfirmed;

        void ConfirmDeleteAccount()
        {
            ShowDialog("Delete Account?", Avatars.Sprite(Avatars.Selected), "All your progress, coins and boosters will be lost.",
                "Keep Account", -1, "Delete", HideDialog,
                () =>
                {
                    HideDialog();
                    GameSave.DeleteAll();
                    GameSave.Save();
                    if (homeAvatar) homeAvatar.sprite = Avatars.Sprite(0);
                    GameSettings.NotifyReset();
                    DeleteAccountConfirmed?.Invoke();
                    HomeToast("Account deleted");
                },
                HideDialog, 220f, secondaryRed: true, tapToClose: true, extraBottom: 70f);
        }
    }
}
