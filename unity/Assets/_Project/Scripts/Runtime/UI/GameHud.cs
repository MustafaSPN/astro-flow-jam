using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Oyun HUD'u (uGUI), oyunun lacivert-camgöbeği temasında: solda daire içinde bölüm
    /// numarası, yanında "Time" paneli, sağda tek sırada coin göstergesi ve duraklat (yeniden başlat duraklatma panelinde); sağ altta küçük geliştirici gezinme
    /// butonları. Kartuş deposunun altındaki "boş/toplam" sayacı da burada, dünya konumunu takip eden kalın kenarlı yazı
    /// olarak çizilir. Duraklatınca ekran
    /// kararır ve "Resume" / "Restart" seçenekleri çıkar.
    /// IMGUI yerine uGUI kullanılır; dokunmatik ekranda, Device Simulator'da ve cihazda butonlar güvenilir çalışır.
    /// </summary>
    public sealed partial class GameHud : MonoBehaviour
    {
        LoadingScreen loading;

        /// <summary>Açılış yükleme ekranı hâlâ görünüyor mu.</summary>
        public bool LoadingVisible => loading != null;
        const float ReferenceWidth = 1080f;
        const float ReferenceHeight = 2340f;
        // Üst barın dikey yeri (PositionTopBar): zaman paneli çentiğin / Dynamic Island'ın PanelGap kadar altında (panelin
        // neon halesi adaya değer); çentiksiz telefonlarda rozetin halesi ekranın üst kenarını geçmez. Ada/çentiğin alt kenarı
        // güvenli alanın bu kadar üstündedir (iPhone 17 Pro'da ölçüldü; çentikli telefonlarda da ~14 pt).
        const float ObstacleAboveSafe = 37f;
        const float PanelGap = 20f;
        const float PanelHalf = 85f, BadgeGlowHalf = 165f;   // zaman paneli ve rozet halesinin yarı yükseklikleri
        const float BarHeight = 270f;
        const float BarScale = 1f;
        static readonly Color FullColor = new(1f, 0.36f, 0.32f);

        Text title;
        TimePanel timePanel;
        Text message;
        GameObject messageBox;
        Text beltLabel;
        RectTransform canvasRect, safeArea, topBar;
        Rect appliedSafeArea;
        float messageTime;
        Camera beltCamera;
        Vector3 beltWorld;

        public event Action PreviousPressed;
        public event Action RestartPressed;
        public event Action HomePressed;
        public event Action NextPressed;
        /// <summary>Geliştirici kısayolu: bölüm rozetine dokunmak kalan pikselleri temizler (bölüm sonu gösterisini dener).</summary>
        public event Action BadgePressed;
        /// <summary>Sağdaki joker sütunundan bir joker seçildi (0 İstasyon, 1 Solucan Deliği, 2 Zaman, 3 UFO).</summary>
        public event Action<int> BoosterPressed;

        public bool Paused { get; private set; }

        public static GameHud Create()
        {
            EnsureEventSystem();
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0f;
            var hud = go.AddComponent<GameHud>();
            hud.Build();
            return hud;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(es);
        }

        void Build()
        {
            canvasRect = (RectTransform)transform;
            safeArea = NewRect("Safe Area", transform);
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            ApplySafeArea();

            // Bant sayacı ("3/5"): kalın siyah kenarlı beyaz yazı; butonların altında kalır.
            beltLabel = NewText("Belt Counter", transform, 52, UiArt.Navy);
            beltLabel.rectTransform.sizeDelta = new Vector2(260f, 100f);
            beltLabel.transform.SetAsFirstSibling();
            beltLabel.gameObject.SetActive(false);

            // Üst bölüm (lacivert-camgöbeği tema): solda yuvarlak içinde bölüm numarası, ona
            // yaslanan panelde "Time" ve süre kutusu; sağda tek sırada coin göstergesi ve duraklat.
            var bar = topBar = NewRect("Top Bar", safeArea);
            Place(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-60f, BarHeight));
            PositionTopBar();
            // Üst bar biraz küçük: içerik bar genişliği içinde ölçeklenir (sağ ve sol bloklar kenarda kalır).
            bar.localScale = Vector3.one * BarScale;
            bar.sizeDelta = new Vector2(-60f / BarScale, BarHeight);

            timePanel = new TimePanel(bar);

            // Bölüm rozeti: panelin soluna taşan büyük daire.
            var badge = NewRect("Level Badge", bar);
            Place(badge, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(130f, 0f), new Vector2(250f, 250f));
            var rim = badge.gameObject.AddComponent<Image>();
            var badgeButton = badge.gameObject.AddComponent<Button>();
            badgeButton.transition = Selectable.Transition.None;
            badgeButton.onClick.AddListener(() => BadgePressed?.Invoke());
            rim.sprite = UiArt.Glass(250, 250, 125f, new Color(0.36f, 0.48f, 0.78f), new Color(0.2f, 0.27f, 0.5f), UiArt.Navy, 6f);
            rim.raycastTarget = true;
            var face = NewRect("Face", badge).gameObject.AddComponent<Image>();
            face.sprite = UiArt.Glass(206, 206, 103f, new Color(0.2f, 0.42f, 0.95f), new Color(0.08f, 0.17f, 0.52f), UiArt.Cyan, 5f);
            face.raycastTarget = false;
            Place(face.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(206f, 206f));
            var levelLabel = NewText("Label", badge, 42, UiArt.Navy);
            Place(levelLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 46f), new Vector2(200f, 50f));
            levelLabel.text = "Level";
            title = NewText("Number", badge, LevelFontSize, UiArt.Navy);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(200f, 110f));

            // Tek sıra: en sağda duraklat (yeniden başlat duraklatma panelinde), solunda coin göstergesi.
            var pause = ThemeButton(bar, "Pause", new Vector2(-RightButton / 2f, 0f), RightButton, TogglePause);
            AddNeonIcon(pause, "pause", RightButton * 0.76f, whiteFill: true);

            // Coin göstergesi (şimdilik yalnızca arayüz): duraklatın solunda; kısa tutar (10.4k gibi beş karakter) sığar.
            BuildCoins(bar);

            // Geliştirici: önceki / sonraki bölüm (sağ alt köşede, sönük); yalnız geliştirme derlemesinde.
            if (DevTools.Enabled)
            {
                var dev = NewRect("Dev Nav", safeArea);
                Place(dev, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f - 95f, 20f), new Vector2(190f, 80f));
                var devBox = UiArt.Box(84, 76, 22f, new Color(1f, 1f, 1f, 0.22f), new Color(1f, 1f, 1f, 0.12f), 0f);
                AddIcon(ImageButton(dev, "Prev", devBox, new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(84f, 76f), () => PreviousPressed?.Invoke()), "prev", 64f);
                AddIcon(ImageButton(dev, "Next", devBox, new Vector2(1f, 0.5f), new Vector2(-42f, 0f), new Vector2(84f, 76f), () => NextPressed?.Invoke()), "next", 64f);
            }

            BuildBoosters();
            if (DevTools.Enabled) BuildDevMenu();          // geliştirici menüsü yalnız geliştirme derlemesinde
            loading = new LoadingScreen(transform);           // açılış: iOS açılış ekranının devamı, kendiliğinden kaybolur

            // Kısa mesajlar: üst sıranın altında koyu hap.
            messageBox = NewRect("Message", topBar).gameObject;   // bar nereye konursa onun hemen altında
            var boxRect = (RectTransform)messageBox.transform;
            Place(boxRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -30f - 48f), new Vector2(900f, 96f));
            var bg = messageBox.AddComponent<Image>();
            bg.sprite = UiArt.Box(900, 96, 48f, new Color(0.1f, 0.1f, 0.16f, 0.85f), new Color(0.06f, 0.06f, 0.1f, 0.85f), 0f);
            bg.raycastTarget = false;
            message = NewText("Text", boxRect, 40);
            Place(message.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-40f, 0f));
            messageBox.SetActive(false);

            BuildPausePanel();
            levelComplete = new LevelCompletePopup((RectTransform)transform, this, coins, () => safeArea);
        }

        // Jokerler (yer tutucu tasarım): ekranın sağ kenarında, tahta ve slot sırasının hizasında dikey sütun. Her butonda
        // kısa ad ve kalan adet rozeti; tasarım sonra eklenecek.
        internal static readonly string[] BoosterNames = { "Hangar", "Warp", "Freeze", "UFO" };
        internal static readonly string[] BoosterIcons = { "Hangar", "Warp", "Freeze", "Ufo" };   // Resources/UI/Boosters
        const float BoosterSize = RightButton * 1.2f, BoosterGap = 24f, BoosterTopGap = 64f, BadgeSize = 54f, BadgeOverhang = 7f;
        const float BarSideMargin = 30f;
        const float BoosterEdgeGap = 14f;      // adet yuvarlağı ile ekranın sağ kenarı arasındaki boşluk      // üst barın güvenli alan kenarına uzaklığı (sizeDelta −60 / BarScale)
        readonly Text[] boosterCounts = new Text[4];

        void OnDestroy()
        {
            if (Paused) Time.timeScale = 1f;
        }

        /// <summary>Rozetteki bölüm numarası.</summary>
        int shownLevel = -1, shownBeltFree = -1, shownBeltCapacity = -1;

        public void SetLevel(int number)
        {
            if (number == shownLevel) return;
            shownLevel = number;
            var text = NumberText.Of(number);
            title.text = text;
            // Rozete sığsın: 3 basamakta biraz, 4 ve üstünde biraz daha küçük.
            title.fontSize = text.Length <= 2 ? LevelFontSize : text.Length == 3 ? 84 : 68;
        }

        const int LevelFontSize = 104;

        public void ShowMessage(string text, float seconds = 1.8f)
        {
            message.text = text;
            messageBox.SetActive(true);
            messageTime = seconds;
        }

        /// <summary>Bant sayacını dünya konumuna yerleştirir ve günceller; bant doluyken kırmızı.</summary>
        public void SetBeltCounter(Camera cam, Vector3 world, int free, int capacity)
        {
            beltCamera = cam;
            beltWorld = world;
            if (free != shownBeltFree || capacity != shownBeltCapacity)
            {
                shownBeltFree = free;
                shownBeltCapacity = capacity;
                beltLabel.text = $"{free}/{capacity}";
                beltLabel.color = free == 0 ? FullColor : Color.white;
            }
            beltLabel.gameObject.SetActive(true);
            PlaceBeltLabel();
        }

        float pulseTime = -1f;
        const float PulseSeconds = 0.45f;

        /// <summary>Yörünge doluyken: sayaç kırmızı parlayarak büyüyüp küçülür ve hafifçe sarsılır.</summary>
        public void PulseBeltCounter()
        {
            pulseTime = 0f;
        }

        void UpdatePulse()
        {
            if (pulseTime < 0f) return;
            pulseTime += Time.unscaledDeltaTime;
            var p = Mathf.Clamp01(pulseTime / PulseSeconds);
            var bump = Mathf.Sin(p * Mathf.PI);
            beltLabel.rectTransform.localScale = Vector3.one * (1f + 0.55f * bump);
            beltLabel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(p * Mathf.PI * 6f) * 7f * (1f - p));
            beltLabel.color = Color.Lerp(FullColor, new Color(1f, 0.15f, 0.12f), bump);
            if (p >= 1f)
            {
                pulseTime = -1f;
                beltLabel.rectTransform.localScale = Vector3.one;
                beltLabel.rectTransform.localRotation = Quaternion.identity;
            }
        }

        void PlaceBeltLabel()
        {
            if (!beltCamera || !beltLabel.gameObject.activeSelf) return;
            var screen = beltCamera.WorldToScreenPoint(beltWorld);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local))
                beltLabel.rectTransform.anchoredPosition = local;
        }

        void Update()
        {
            if (messageTime > 0f)
            {
                messageTime -= Time.unscaledDeltaTime;
                if (messageTime <= 0f) messageBox.SetActive(false);
            }
            if (Screen.safeArea != appliedSafeArea) ApplySafeArea();
            boosterPrompt?.Tick();
            dialogPopup?.Tick();
            levelComplete.Tick();
            if (loading != null && !loading.Tick()) loading = null;
            timePanel.Tick();
            UpdateTutorial();
            guideOverlay?.Tick();
            UpdateHome();
        }

        void LateUpdate()
        {
            PlaceBeltLabel();
            UpdatePulse();
        }

        // Çentik ve Dynamic Island altında kalmaması için içerik güvenli alana yerleşir.
        void ApplySafeArea()
        {
            appliedSafeArea = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            // Geniş ekranda (iPad) tuval yüksekliğe göre ölçeklenir ve içerik ortadaki telefon sütununa yerleşir.
            GetComponent<CanvasScaler>().matchWidthOrHeight = ScreenFit.IsWide ? 1f : 0f;
            var column = ScreenFit.ColumnFraction;
            safeArea.anchorMin = new Vector2(Mathf.Max(appliedSafeArea.xMin / Screen.width, 0.5f - column / 2f), ScreenFit.SafeBottom(appliedSafeArea.yMin / Screen.height));
            safeArea.anchorMax = new Vector2(Mathf.Min(appliedSafeArea.xMax / Screen.width, 0.5f + column / 2f), ScreenFit.SafeTop(appliedSafeArea.yMax / Screen.height));
            PositionTopBar();
        }

        // Bar merkezinin ekranın üstünden uzaklığı (tuval birimi): panel ada/çentiğin PanelGap altında, rozet halesi ekranın
        // üst kenarını geçmez. Bar güvenli alanın üst kenarına bağlı olduğu için konum bu farkla verilir.
        void PositionTopBar()
        {
            if (!topBar || Screen.height <= 0) return;
            // Telefonda tuval genişliğe, geniş ekranda yüksekliğe göre ölçeklenir.
            var canvasHeight = ScreenFit.IsWide ? ReferenceHeight : Screen.height * ReferenceWidth / Screen.width;
            var topInset = (1f - safeArea.anchorMax.y) * canvasHeight;
            var obstacle = Mathf.Max(0f, topInset - ObstacleAboveSafe);
            var center = Mathf.Max(obstacle + PanelGap + PanelHalf, BadgeGlowHalf);
            topBar.anchoredPosition = new Vector2(0f, topInset - (center - BarHeight / 2f));
        }

        CoinWidget coins;
        LevelCompletePopup levelComplete;

        void BuildCoins(RectTransform bar) =>
            coins = new CoinWidget(bar, new Vector2(-RightButton - CoinGap - CoinWidth / 2f, 0f), CoinWidth, CoinHeight);

        /// <summary>Coin göstergesindeki sayı.</summary>
        public void SetCoins(int value) => coins.Set(value);

        /// <summary>Bölüm sonu penceresini açar; ödül toplanınca 'done' çağrılır.</summary>
        public void ShowLevelComplete(int level, int reward, Action done) => levelComplete.Show(level, reward, done);
    }
}
