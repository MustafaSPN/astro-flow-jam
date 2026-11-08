using System;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;
using Object = UnityEngine.Object;

namespace AstroFlowJam
{
    /// <summary>Profil resimleri (Resources/UI/Home).</summary>
    public static class Avatars
    {
        public static readonly string[] Names =
        {
            "Avatar", "Avatars/White_Woman", "Avatars/Black_Man", "Avatars/Black_Woman",
            "Avatars/Cat", "Avatars/Dog", "Avatars/Fox", "Avatars/Elephant",
        };

        public static Sprite Sprite(int i) => UiArt.Asset("Home/" + Names[Mathf.Clamp(i, 0, Names.Length - 1)]);

        public static int Selected => GameSave.GetInt(SaveKeys.Avatar, 0);
    }

    /// <summary>Profil resmi seçimi: avatar ızgarası; dışarı dokununca kapanır, seçince kaydeder.</summary>
    public static class AvatarPicker
    {
        const float Width = 900f, Height = 1300f, Cell = 360f;

        public static GameObject Open(Transform parent, int layer, MonoBehaviour host, Action<int> picked)
        {
            var picker = NewRect("Avatar Picker", parent).gameObject;
            var root = (RectTransform)picker.transform;
            Stretch(root);
            Layer(picker, layer, true);
            var dim = picker.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.02f, 0.06f, 0.75f);
            var dimButton = picker.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => Object.Destroy(picker));
            var panel = NewRect("Panel", root);
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height));
            var frame = Sliced(panel, "Frame", PopupPanel, Vector2.zero, new Vector2(Width, Height), 1100f * Height / 1300f);
            frame.GetComponent<Image>().raycastTarget = true;
            frame.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var title = NewText("Title", panel, 76, UiArt.Navy);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, Height / 2f - 185f), new Vector2(700f, 100f));
            title.text = "Choose Avatar";
            // Ayarlar ve can penceresi gibi: X yok, dışarı dokununca kapanır.
            var hint = NewText("Tap Hint", root, 38, UiArt.Navy);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -Height / 2f - 60f), new Vector2(800f, 60f));
            hint.text = "Tap to close";
            hint.color = new Color(0.62f, 0.9f, 1f);
            var area = NewRect("Area", panel);
            Place(area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -85f), new Vector2(Width - 120f, Height - 400f));
            var content = ScrollContent(area, Vector2.zero);
            for (var i = 0; i < Avatars.Names.Length; i++)
            {
                var index = i;
                var pos = new Vector2((i % 2 == 0 ? -1f : 1f) * Cell * 0.52f, -Cell / 2f - 10f - (i / 2) * Cell);
                var b = ImageButton(content, Avatars.Names[i], Avatars.Sprite(i), new Vector2(0.5f, 1f), pos, new Vector2(Cell - 30f, Cell - 40f), () =>
                {
                    GameSave.SetInt(SaveKeys.Avatar, index);
                    GameSave.Save();
                    Haptics.Select();
                    picked(index);
                    Object.Destroy(picker);
                });
                b.GetComponent<Image>().preserveAspect = true;
            }
            content.sizeDelta = new Vector2(0f, (Avatars.Names.Length + 1) / 2 * Cell + 30f);
            host.StartCoroutine(PopIn(panel));
            return picker;
        }
    }
}
