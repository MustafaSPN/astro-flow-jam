using System;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Geliştirici menüsü (test için): sol altta sönük "DEV" butonu; açılınca coin ±100, her joker ±1, süre ±10 sn ve joker
    /// tanıtımlarını sıfırlama.
    /// </summary>
    public sealed partial class GameHud
    {
        public event Action<int> DevCoinsPressed;
        public event Action<int, int> DevBoosterPressed;
        public event Action<float> DevTimePressed;
        public event Action DevResetTutorialsPressed;
        public event Action<int> DevLivesPressed;
        public event Action<int> DevLevelPressed;
        RectTransform devToggle, devPanel;
        const int DevLayer = 200;                // ana ekranın ve pencerelerin de üstünde

        GameObject devMenu;
        const float DevRowHeight = 92f, DevRowGap = 14f, DevMenuWidth = 560f;

        void BuildDevMenu()
        {
            var faint = UiArt.Box(150, 76, 22f, new Color(1f, 1f, 1f, 0.22f), new Color(1f, 1f, 1f, 0.12f), 0f);
            var toggle = ImageButton(safeArea, "Dev Menu Button", faint, Vector2.zero, new Vector2(30f + 75f, 20f + 38f), new Vector2(150f, 76f),
                () => devMenu.SetActive(!devMenu.activeSelf));
            Label(toggle, "DEV", 38);
            devToggle = toggle;
            Layer(toggle.gameObject, DevLayer, true);

            string[] rows = { "Coins", BoosterNames[0], BoosterNames[1], BoosterNames[2], BoosterNames[3], "Time", "Lives", "Level" };
            var height = (rows.Length + 1) * (DevRowHeight + DevRowGap) + DevRowGap;   // son satır: tanıtımları sıfırla
            var panel = NewRect("Dev Menu", safeArea);
            Place(panel, Vector2.zero, Vector2.zero, new Vector2(30f + DevMenuWidth / 2f, 20f + 76f + 16f), new Vector2(DevMenuWidth, height));
            var bg = panel.gameObject.AddComponent<Image>();
            bg.sprite = UiArt.Glass((int)DevMenuWidth, (int)height, 30f, new Color(0.1f, 0.13f, 0.27f), new Color(0.05f, 0.07f, 0.16f), UiArt.Cyan);
            devMenu = panel.gameObject;
            devPanel = panel;
            Layer(devMenu, DevLayer, true);

            var button = UiArt.Box(130, 80, 22f, new Color(0.17f, 0.42f, 0.88f), new Color(0.07f, 0.16f, 0.47f), 0f);
            for (var i = 0; i < rows.Length; i++)
            {
                var row = i;
                var y = -DevRowGap - DevRowHeight / 2f - i * (DevRowHeight + DevRowGap);
                var name = NewText("Name", panel, 40);
                Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f, y), new Vector2(220f, DevRowHeight));
                name.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                name.rectTransform.anchoredPosition = new Vector2(130f, y);
                name.text = rows[i];
                string minus, plus;
                if (row == 0) { minus = "-100"; plus = "+100"; }
                else if (row == 5) { minus = "-10s"; plus = "+10s"; }
                else { minus = "-1"; plus = "+1"; }
                Label(ImageButton(panel, "Minus", button, new Vector2(1f, 1f), new Vector2(-230f, y), new Vector2(130f, 80f), () => DevStep(row, -1)), minus, 36);
                Label(ImageButton(panel, "Plus", button, new Vector2(1f, 1f), new Vector2(-85f, y), new Vector2(130f, 80f), () => DevStep(row, +1)), plus, 36);
            }
            var resetY = -DevRowGap - DevRowHeight / 2f - rows.Length * (DevRowHeight + DevRowGap);
            var wide = UiArt.Box((int)(DevMenuWidth - 60f), 80, 22f, new Color(0.95f, 0.45f, 0.2f), new Color(0.7f, 0.25f, 0.08f), 0f);
            Label(ImageButton(panel, "Reset Tutorials", wide, new Vector2(0.5f, 1f), new Vector2(0f, resetY), new Vector2(DevMenuWidth - 60f, 80f),
                () => { devMenu.SetActive(false); DevResetTutorialsPressed?.Invoke(); }), "Reset Tutorials", 36);
            devMenu.SetActive(false);
        }

        void DevStep(int row, int sign)
        {
            if (row == 0) DevCoinsPressed?.Invoke(100 * sign);
            else if (row <= BoosterInventory.Count) DevBoosterPressed?.Invoke(row - 1, sign);
            else if (row == 5) DevTimePressed?.Invoke(10f * sign);
            else if (row == 6) DevLivesPressed?.Invoke(sign);
            else DevLevelPressed?.Invoke(sign);
        }

        // Ana ekranda alt sekmelerin üstüne çıkar.
        void PlaceDevMenu()
        {
            if (!devToggle) return;
            var lift = HomeVisible ? HomeBarHeight : 0f;
            devToggle.anchoredPosition = new Vector2(30f + 75f, 20f + 38f + lift);
            devPanel.anchoredPosition = new Vector2(30f + DevMenuWidth / 2f, 20f + 76f + 16f + lift);
        }
    }
}
