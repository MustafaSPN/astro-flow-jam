using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Üst bardaki süre paneli: "Time" başlığı ve süre kutusu; son saniyelerde neon uyarı,
    /// zaman dondurmada buz kaplaması ve boşalan çubuk. Bölüm rozetinin halesi de burada çizilir (panel halesiyle aynı katman
    /// sırasında kalsın diye).
    /// </summary>
    public sealed class TimePanel
    {
        const int PanelWidth = 420;            // Time paneli tek sıra sağ bloğa yer açmak için daraltıldı (eskiden 560)
        // Panelin solu rozetin altında kalır; başlık ve süre kutusu panelin görünen kısmında ortalanır.
        const float PanelContentShift = 52f;

        readonly Text timer;
        int shownSeconds = -1;
        readonly Image freezeCover, freezeFill;
        readonly GameObject freezeBar, freezePill;
        readonly Text freezeSeconds;
        const float FreezeTotal = 10f;        // dondurma süresi (LevelController.TimeFreezeSeconds ile aynı)
        const float FreezeBlinkFrom = 3f;      // 10 sn'lik dondurmanın 7. saniyesinde bitiş uyarısı
        // Neon: süre kutusunun içinde, yazının arkasında kırmızı bir parıltı yanar; yazı beyaz kalır, dış hattı koyu kırmızıya
        // döner ve parıltının önünde net okunur.
        readonly Image timeWarning;
        readonly ThickOutline timerOutline;
        static readonly Color NeonGlow = new(1f, 0.12f, 0.22f);
        static readonly Color NeonEdge = new(0.45f, 0.02f, 0.08f);
        bool warned30, warned20;
        float flashTime = -1f, punchTime = -1f;
        // Süre uyarısı: 60 sn altında yazı her saniye 1,2 kattan küçülür (0,9 sn); 30 ve 20 sn'de
        // uyarı katmanı bir kez yanıp söner (0,5 sn yanar, 0,5 sn söner); 10 sn altında her saniye yanıp söner ve titreşir.
        const int PunchBelow = 60, FlashOnce1 = 30, FlashOnce2 = 20, FlashEveryBelow = 10;
        const float PunchScale = 1.2f, PunchSeconds = 0.9f, FlashHalfSeconds = 0.5f;

        public TimePanel(RectTransform bar)
        {
            // Haleler kardeş olarak önce çizilir; panelin ve rozetin arkasında kalır.
            var panelGlow = NewRect("Panel Glow", bar).gameObject.AddComponent<Image>();
            panelGlow.sprite = UiArt.Glow(PanelWidth + 80, 250, 40f, 40f, new Color(UiArt.Cyan.r, UiArt.Cyan.g, UiArt.Cyan.b, 0.25f));
            panelGlow.raycastTarget = false;
            Place(panelGlow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f + PanelWidth / 2f, 0f), new Vector2(PanelWidth + 80f, 250f));
            var badgeGlow = NewRect("Badge Glow", bar).gameObject.AddComponent<Image>();
            badgeGlow.sprite = UiArt.Glow(330, 330, 165f, 40f, new Color(UiArt.Cyan.r, UiArt.Cyan.g, UiArt.Cyan.b, 0.3f));
            badgeGlow.raycastTarget = false;
            Place(badgeGlow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(130f, 0f), new Vector2(330f, 330f));

            var panel = NewRect("Time Panel", bar);
            Place(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f + PanelWidth / 2f, 0f), new Vector2(PanelWidth, 170f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = UiArt.Glass(PanelWidth, 170, 40f, UiArt.GlassTop, UiArt.GlassBottom, UiArt.GlassEdge, 6f);
            panelImage.raycastTarget = false;

            var header = NewRect("Header", panel);
            Place(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(PanelContentShift, -14f), new Vector2(300f, 56f));
            var clock = NewRect("Clock", header).gameObject.AddComponent<Image>();
            clock.sprite = UiArt.Icon("clock");
            clock.raycastTarget = false;
            Place(clock.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-72f, 0f), new Vector2(60f, 60f));
            var headerText = NewText("Label", header, 44, UiArt.Navy);
            Place(headerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(28f, 2f), new Vector2(160f, 56f));
            headerText.text = "Time";

            var timeBox = NewRect("Time Box", panel);
            Place(timeBox, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(PanelContentShift, 18f), new Vector2(280f, 82f));
            var timeImage = timeBox.gameObject.AddComponent<Image>();
            timeImage.sprite = UiArt.Glass(280, 82, 41f, new Color(0.04f, 0.07f, 0.2f), new Color(0.02f, 0.04f, 0.12f), new Color(0.2f, 0.32f, 0.66f), 4f);
            timeImage.raycastTarget = false;
            timeWarning = NewRect("Time Neon Glow", timeBox).gameObject.AddComponent<Image>();
            timeWarning.sprite = UiArt.Glow(300, 130, 34f, 46f, NeonGlow);
            timeWarning.raycastTarget = false;
            timeWarning.color = new Color(1f, 1f, 1f, 0f);
            Place(timeWarning.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(300f, 130f));
            // Zaman dondurma (yer tutucu): kutuyu buz mavisi cam kaplar; altında 10 sn'de boşalan çubuk.
            freezeCover = NewRect("Time Freeze Cover", timeBox).gameObject.AddComponent<Image>();
            freezeCover.sprite = UiArt.Frost(280, 82, 41f);
            freezeCover.raycastTarget = false;
            Place(freezeCover.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var barBack = NewRect("Time Freeze Bar", panel).gameObject.AddComponent<Image>();
            barBack.sprite = UiArt.Box(240, 14, 7f, new Color(0.05f, 0.1f, 0.25f), new Color(0.05f, 0.1f, 0.25f), 0f);
            barBack.raycastTarget = false;
            Place(barBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(PanelContentShift, 4f), new Vector2(240f, 14f));
            freezeFill = NewRect("Fill", barBack.rectTransform).gameObject.AddComponent<Image>();
            freezeFill.sprite = UiArt.Box(240, 14, 7f, new Color(0.75f, 0.97f, 1f), new Color(0.35f, 0.75f, 1f), 0f);
            freezeFill.type = Image.Type.Filled;
            freezeFill.fillMethod = Image.FillMethod.Horizontal;
            freezeFill.raycastTarget = false;
            Place(freezeFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            freezeBar = barBack.gameObject;
            // Kalan donma süresi: kutunun sağ alt köşesinde küçük buz hapı.
            var pill = NewRect("Time Freeze Seconds", timeBox);
            Place(pill, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -6f), new Vector2(60f, 46f));
            var pillImage = pill.gameObject.AddComponent<Image>();
            pillImage.sprite = UiArt.Glass(60, 46, 23f, new Color(0.7f, 0.95f, 1f), new Color(0.3f, 0.66f, 0.95f), Color.white, 3f);
            pillImage.raycastTarget = false;
            freezeSeconds = NewText("Value", pill, 30, UiArt.Navy);
            Place(freezeSeconds.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero);
            freezePill = pill.gameObject;
            SetFreeze(0f, 0f);
            timer = NewText("Time", timeBox, 60, UiArt.Navy);
            Place(timer.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero);
            timerOutline = timer.GetComponent<ThickOutline>();
        }

        /// <summary>
        /// Zaman dondurma göstergesi: share 0..1 kalan pay (çubuk), secondsLeft kalan saniye; 0'da gizlenir. Son 3 saniyede
        /// buz kaplaması yanıp söner.
        /// </summary>
        public void SetFreeze(float share, float secondsLeft)
        {
            var on = share > 0f;
            freezeBar.SetActive(on);
            freezeCover.gameObject.SetActive(on);
            freezePill.SetActive(on);
            if (!on) return;
            freezeFill.fillAmount = share;
            freezeSeconds.text = Mathf.CeilToInt(secondsLeft).ToString();
            // Başlangıç: buz 0,3 sn'de büyüyerek kutuyu kaplar.
            var grow = Mathf.Clamp01((1f - share) * FreezeTotal / 0.3f);
            freezeCover.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, 1f - (1f - grow) * (1f - grow));
            var a = Mathf.Clamp01(grow * 1.4f);
            if (secondsLeft <= FreezeBlinkFrom) a *= 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Cos(secondsLeft * Mathf.PI * 4f));
            freezeCover.color = new Color(1f, 1f, 1f, a);
        }

        /// <summary>Bölümde geçen süre, dakika:saniye (süre sınırı olmayan bölüm).</summary>
        public void SetTime(float seconds)
        {
            var whole = Mathf.FloorToInt(seconds);
            if (whole == shownSeconds) return;
            shownSeconds = whole;
            timer.text = $"{whole / 60:00}:{whole % 60:00}";
            timer.color = Color.white;
        }

        /// <summary>Kalan süre, dakika:saniye; saniye aşağı yuvarlanır (son saniyede 00:00 görünür).</summary>
        public void SetTimeLeft(float remaining)
        {
            var whole = Mathf.Max(0, Mathf.FloorToInt(remaining));
            if (whole == shownSeconds) return;
            shownSeconds = whole;
            timer.text = $"{whole / 60:00}:{whole % 60:00}";
        }

        /// <summary>
        /// Süre uyarısı, her tam saniyede kalan süreyle çağrılır: 60 sn ve altında yazı 1,2 kattan
        /// küçülür (0,9 sn); 30 sn'de bir kez, 20 sn'de bir kez kırmızı katman yanıp söner (0,5 sn yanar, 0,5 sn söner);
        /// 10 sn ve altında her saniye yanıp söner ve titreşir.
        /// </summary>
        public void TimeWarningAt(float remaining)
        {
            if (remaining <= PunchBelow) punchTime = 0f;
            if (remaining <= 0f) { ResetTimerEffects(); return; }
            if (remaining <= FlashEveryBelow) { flashTime = 0f; Haptics.TimeWarning(); Sfx.Play(Sfx.TimerWarning); }
            else if (remaining <= FlashOnce2) { if (!warned20) { warned20 = true; flashTime = 0f; } }
            else if (remaining <= FlashOnce1 && !warned30) { warned30 = true; flashTime = 0f; }
        }

        void ResetTimerEffects()
        {
            warned30 = warned20 = false;
            flashTime = punchTime = -1f;
            if (timer) { timer.rectTransform.localScale = Vector3.one; SetNeon(0f); }
        }

        // Neon parlaması: yazının arkasındaki kırmızı parıltı yanar, yazı beyaz kalır, dış hattı koyu kırmızıya döner.
        void SetNeon(float amount)
        {
            if (timeWarning) timeWarning.color = new Color(1f, 1f, 1f, amount);
            timer.color = Color.white;
            if (timerOutline)
            {
                timerOutline.color = Color.Lerp(UiArt.Navy, NeonEdge, amount);
                timer.SetVerticesDirty();
            }
        }

        /// <summary>Yeni bölüm: uyarılar sıfırlanır.</summary>
        public void Reset()
        {
            shownSeconds = -1;
            ResetTimerEffects();
            if (timer) timer.color = Color.white;
        }

        /// <summary>Her kare: uyarı parlaması ve küçülme animasyonu.</summary>
        public void Tick()
        {
            var dt = Time.unscaledDeltaTime;
            if (flashTime >= 0f)
            {
                flashTime += dt;
                var p = flashTime / FlashHalfSeconds;                        // 0..1 yanar, 1..2 söner (InOutSine)
                var a = p < 1f ? 0.5f - 0.5f * Mathf.Cos(p * Mathf.PI) : 0.5f + 0.5f * Mathf.Cos((p - 1f) * Mathf.PI);
                if (p >= 2f) { a = 0f; flashTime = -1f; }
                SetNeon(a);
            }
            if (punchTime >= 0f)
            {
                punchTime += dt;
                var p = Mathf.Clamp01(punchTime / PunchSeconds);
                var ease = 1f - (1f - p) * (1f - p) * (1f - p);                 // 1,2 kattan küçülerek (OutCubic)
                timer.rectTransform.localScale = Vector3.one * Mathf.Lerp(PunchScale, 1f, ease);
                if (p >= 1f) punchTime = -1f;
            }
        }
    }
}
