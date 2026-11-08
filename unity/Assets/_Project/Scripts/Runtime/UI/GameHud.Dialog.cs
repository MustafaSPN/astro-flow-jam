using System;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class GameHud
    {
        DialogPopup dialogPopup;
        DialogPopup Dialog => dialogPopup ??= new DialogPopup((RectTransform)transform, this, coins);

        /// <summary>
        /// Diyaloğu açar. price &lt; 0 ise ana butonda yalnız etiket (ör. "Free"); secondary null ise ikinci buton gizli.
        /// Kapatma butonu onClose'u çağırır. Pencereyi kapatmak çağıranın işi (HideDialog).
        /// </summary>
        public void ShowDialog(string title, Sprite icon, string body, string primary, int price, string secondary,
            Action onPrimary, Action onSecondary, Action onClose, float iconSize = 250f, string subtitle = null, bool secondaryRed = false,
            string caption = null, Color? subtitleColor = null, Sprite secondaryIcon = null, bool tapToClose = false, float extraBottom = 0f) =>
            Dialog.Show(HomeVisible ? HomePopupLayer : DialogPopup.DefaultLayer,   // ana ekranda da görünür
                title, icon, body, primary, price, secondary, onPrimary, onSecondary, onClose, iconSize, subtitle, secondaryRed,
                caption, subtitleColor, secondaryIcon, tapToClose, extraBottom);

        /// <summary>Açık diyaloğun simgesinin üstüne yazı (ör. kalbin içinde can sayısı).</summary>
        public void SetDialogIconText(string text) => dialogPopup?.SetIconText(text);

        public void HideDialog() => dialogPopup?.Hide();

        /// <summary>Yetersiz coin: ana buton sarsılır, fiyat kırmızı yanıp söner.</summary>
        public void DialogDenied() => dialogPopup?.Denied();

        /// <summary>Joker simgesi (Resources/UI/Boosters).</summary>
        public static Sprite BoosterIcon(int booster) => UiArt.Asset("Boosters/" + BoosterIcons[booster]);

        /// <summary>Süre bitti görseli (+30 sn saat, Resources/UI/Popup/timeout).</summary>
        public static Sprite TimeoutIcon => UiArt.Asset("Popup/timeout");
    }
}
