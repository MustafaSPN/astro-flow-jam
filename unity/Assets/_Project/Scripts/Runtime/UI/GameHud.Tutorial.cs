using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Joker tanıtımı (zorunlu ilk kullanım): ekran kararır, yalnız tanıtılan joker butonu karartmanın üstünde ve basılabilir
    /// kalır; solunda sallanan ok ve "Tap to use!" yazısı. Karartma alttaki her dokunuşu yutar.
    /// </summary>
    public sealed partial class GameHud
    {
        GameObject tutorial;
        RectTransform tutorialArrow;
        Text tutorialText;
        int tutorialBooster = -1;
        float tutorialTime;
        const int TutorialLayer = 118;          // diyaloğun (112) üstünde, joker seçim panelinin (125) altında

        void BuildTutorial()
        {
            tutorial = NewRect("Booster Tutorial", transform).gameObject;
            var root = (RectTransform)tutorial.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            Layer(tutorial, TutorialLayer, true);
            var dim = tutorial.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.02f, 0.06f, 0.78f);
            tutorialArrow = Img(root, "Arrow", UiArt.Icon("next"), Vector2.zero, new Vector2(110f, 110f));
            tutorialArrow.anchorMin = tutorialArrow.anchorMax = new Vector2(0.5f, 0.5f);
            tutorialArrow.GetComponent<Image>().color = TitleYellow;
            tutorialText = NewText("Text", root, 58, UiArt.Navy);
            tutorialText.alignment = TextAnchor.MiddleRight;
            tutorialText.rectTransform.anchorMin = tutorialText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            tutorialText.rectTransform.pivot = new Vector2(1f, 0.5f);
            tutorialText.rectTransform.sizeDelta = new Vector2(600f, 90f);
            tutorialText.text = "Tap to use!";
            tutorial.SetActive(false);
        }

        /// <summary>Tanıtılan joker butonu dışında ekranı kilitler.</summary>
        public void ShowBoosterTutorial(int booster)
        {
            if (!tutorial) BuildTutorial();
            EndBoosterTutorial();
            tutorialBooster = booster;
            Layer(boosterImages[booster].gameObject, TutorialLayer + 1, true);
            tutorial.SetActive(true);
            tutorialTime = 0f;
            UpdateTutorial();
        }

        public void EndBoosterTutorial()
        {
            if (tutorialBooster >= 0)
            {
                if (boosterImages[tutorialBooster].TryGetComponent<Canvas>(out var canvas)) canvas.overrideSorting = false;
                boosterImages[tutorialBooster].rectTransform.localScale = Vector3.one;
            }
            tutorialBooster = -1;
            if (tutorial) tutorial.SetActive(false);
        }

        // Ok, butonun solunda ona doğru sallanır; yazı okun solunda.
        void UpdateTutorial()
        {
            if (tutorialBooster < 0 || !tutorial) return;
            tutorialTime += Time.unscaledDeltaTime;
            var root = (RectTransform)tutorial.transform;
            var target = (Vector2)root.InverseTransformPoint(boosterImages[tutorialBooster].rectTransform.position);
            var nudge = Mathf.Abs(Mathf.Sin(tutorialTime * 5f)) * 26f;
            var arrowX = target.x - BoosterSize * 0.5f - 75f - nudge;
            tutorialArrow.anchoredPosition = new Vector2(arrowX, target.y);
            tutorialText.rectTransform.anchoredPosition = new Vector2(arrowX - 70f, target.y);
            boosterImages[tutorialBooster].rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(tutorialTime * 6f));
        }
    }
}
