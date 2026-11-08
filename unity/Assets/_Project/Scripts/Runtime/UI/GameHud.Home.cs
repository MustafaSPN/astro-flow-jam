using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static AstroFlowJam.PopupArt;
using static AstroFlowJam.UiBuild;

namespace AstroFlowJam
{
    /// <summary>
    /// Ana ekran: üstte avatar, can ve coin göstergeleri (oyundaki coin
    /// göstergesinin tasarımı), ayarlar; ortada logo ve gezegen yolu (en altta oynanacak bölüm, neon nefes alan halesiyle;
    /// üstünde kilitli sonraki bölümler); "Play" butonu; altta Mağaza / Ana Ekran / Koleksiyon sekmeleri. Yol hafifçe
    /// kaydırılabilir, bırakınca yerine döner. Koleksiyon: açılan gizli resimlerin sergisi (dokununca büyür). Mağaza:
    /// coin paketleri ve can doldurma. Ayarlar: ses, müzik, titreşim ve bağlantılar.
    /// </summary>
    public sealed partial class GameHud
    {
        public event Action HomePlayPressed;

        GameObject home;
        RectTransform homeSafe;
        RectTransform[] homePages = new RectTransform[3];
        RectTransform[] homeTabs = new RectTransform[3];
        Image[] homeTabHighlights = new Image[3];
        Text homeLivesText, homeLivesTimer, homeCoinText, homeToast;
        MapPage homeMap;
        ShopPage homeShop;
        CollectionPage homeCollection;
        float homeToastTime, homeTime;
        int homePage = 1, homeLevel = 1, homeTotal = 1;
        bool homeAllDone;                       // tüm bölümler bitti: Play yeni bölümleri bekler
        Func<int, string> homePictureName;
        Func<int, bool> homeHard;
        const int HomeLayer = 150, HomePopupLayer = 175;
        internal const float HomeBarHeight = 175f;
        const float HomePillHeight = 104f;
        static readonly string[] HomeTabNames = { "Shop", "Home", "Collection" };
        static readonly string[] HomeTabIcons = { "Home/Shop_Icon", "Home/Home_Rocket_Icon", "Home/Collection_Icon" };

        public bool HomeVisible => home && home.activeSelf;

        int UnlockedPictures => homeAllDone ? homeTotal : homeLevel - 1;

        /// <summary>Koleksiyon ve yol için bölüm bilgisi (gizli resim, zor bölüm).</summary>
        public void SetupHome(int totalLevels, Func<int, string> pictureNameOf, Func<int, bool> isHard)
        {
            homeTotal = Mathf.Max(1, totalLevels);
            homePictureName = pictureNameOf;
            homeHard = isHard;
        }

        /// <summary>Ana ekranı açar; level: oynanacak bölüm (1'den).</summary>
        public void ShowHome(int level)
        {
            if (!home) BuildHome();
            homeLevel = Mathf.Clamp(level, 1, homeTotal);
            homeAllDone = level > homeTotal;
            home.SetActive(true);
            homeTime = 0f;
            SelectHomePage(1);
            RefreshHome();
        }

        public void HideHome()
        {
            if (home) home.SetActive(false);
            homeCollection?.Hide();
            settingsPanel?.Hide();
        }

        /// <summary>Yol, Play butonu, göstergeler ve koleksiyon yeniden çizilir (bölüm ya da can değişince).</summary>
        public void RefreshHome()
        {
            homeInfoTimer = 0f;
            if (!home) return;
            homeMap.Rebuild(homeLevel, homeTotal, homeAllDone);
            homeCoinText.text = CoinWallet.Short(CoinWallet.Coins);
            if (homePage == 2) homeCollection.Show(UnlockedPictures, homeTotal);
        }

        public void SetHomeLevel(int level)
        {
            homeLevel = Mathf.Clamp(level, 1, homeTotal);
            homeAllDone = level > homeTotal;
            RefreshHome();
        }

        public void HomeToast(string text, float seconds = 1.8f)
        {
            if (!homeToast) return;
            homeToast.text = text;
            homeToastTime = seconds;
        }


        // ------------------------------------------------------------------ kurulum
        void BuildHome()
        {
            GameSettings.Apply();
            home = NewRect("Home", transform).gameObject;
            var root = (RectTransform)home.transform;
            Stretch(root);
            Layer(home, HomeLayer, true);
            var bg = home.AddComponent<Image>();
            bg.color = SceneArt.Hex(0x080D2B);
            var skySprite = SceneArt.Sky();
            var sky = Img(root, "Background", skySprite, Vector2.zero, Vector2.zero);
            Stretch(sky);
            sky.GetComponent<Image>().preserveAspect = false;
            var fitter = sky.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = skySprite.rect.width / skySprite.rect.height;

            homeSafe = NewRect("Safe", root);
            homeSafe.anchorMin = safeArea.anchorMin;
            homeSafe.anchorMax = safeArea.anchorMax;
            homeSafe.offsetMin = homeSafe.offsetMax = Vector2.zero;

            for (var i = 0; i < 3; i++)
            {
                homePages[i] = NewRect(HomeTabNames[i], homeSafe);
                Stretch(homePages[i]);
                homePages[i].offsetMin = new Vector2(0f, HomeBarHeight - 20f);   // alt sekmelerin üstünde
                homePages[i].offsetMax = new Vector2(0f, -190f);                // üst göstergelerin altında
            }
            homeMap = new MapPage(homePages[1], () => HomePlayPressed?.Invoke(), n => homeHard != null && homeHard(n));
            homeShop = new ShopPage(homePages[0], this, text => HomeToast(text), RefreshHome);
            homeCollection = new CollectionPage(homePages[2], transform, HomePopupLayer, this, n => homePictureName?.Invoke(n));
            BuildHomeTopBar();
            BuildHomeTabs();

            homeToast = NewText("Toast", root, 46, UiArt.Navy);
            homeToast.rectTransform.anchorMin = homeToast.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            homeToast.rectTransform.sizeDelta = new Vector2(1000f, 90f);
            homeToast.rectTransform.anchoredPosition = new Vector2(0f, -130f);   // gezegen ve rozetin üstünde, boş alanda
            homeToast.text = "";
        }

        // Oyundaki coin göstergesiyle aynı tasarım (biraz büyük): cam hap, solda taşan simge, ortada değer, sağda "+".
        // iconText verilirse simgenin içine yazılır (kalbin içinde can sayısı).
        RectTransform HomePill(RectTransform parent, string name, Sprite icon, Vector2 position, float width, out Text value, Action onClick,
            Text iconTextOut = null)
        {
            const float h = HomePillHeight;
            var glow = NewRect(name + " Glow", parent).gameObject.AddComponent<Image>();
            glow.sprite = UiArt.Glow(Mathf.RoundToInt(width + 50f), Mathf.RoundToInt(h + 50f), h / 2f, 25f, new Color(UiArt.Cyan.r, UiArt.Cyan.g, UiArt.Cyan.b, 0.2f));
            glow.raycastTarget = false;
            Place(glow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(width + 50f, h + 50f));
            glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glow.rectTransform.anchoredPosition = position;

            var pill = NewRect(name, parent);
            Place(pill, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(width, h));
            pill.pivot = new Vector2(0.5f, 0.5f);
            pill.anchoredPosition = position;
            var body = pill.gameObject.AddComponent<Image>();
            body.sprite = UiArt.Glass(Mathf.RoundToInt(width), Mathf.RoundToInt(h), h / 2f, Color.Lerp(ButtonTop, ButtonBottom, 0.35f), ButtonBottom, UiArt.Cyan, 4f);
            var button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            button.onClick.AddListener(() => onClick?.Invoke());
            pill.gameObject.AddComponent<ButtonPunch>();

            // Oyun içi coin göstergesinin oranları (CoinHeight 88, BuildCoins) h / CoinHeight ile ölçeklenir.
            var k = h / CoinHeight;
            var image = NewRect("Icon", pill).gameObject.AddComponent<Image>();
            image.sprite = icon;
            image.raycastTarget = false;
            image.preserveAspect = true;
            Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(h * 0.55f, 0f), new Vector2(h * 0.92f, h * 0.92f));

            value = NewText("Value", pill, Mathf.RoundToInt(42 * k), UiArt.Navy);
            Place(value.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f * k, 2f * k), new Vector2(-h * 1.9f, 0f));

            var plus = ThemeButton(pill, "Plus", new Vector2(-h * 0.42f, 0f), h * 0.66f, () => { });
            Destroy(plus.GetComponent<ButtonPunch>());
            Destroy(plus.GetComponent<Button>());
            plus.GetComponent<Image>().raycastTarget = false;
            AddNeonIcon(plus, "plus", h * 0.52f);
            return pill;
        }

        Image homeAvatar;
        Text homeLivesCount;
        Image homeLivesInfinity;
        GameObject homeAvatarPicker;

        void BuildHomeTopBar()
        {
            var bar = NewRect("Top", homeSafe);
            Place(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -20f), new Vector2(0f, 170f));
            bar.pivot = new Vector2(0.5f, 1f);
            const float y = -85f;
            var avatarGlow = Img(bar, "Avatar Glow", UiArt.Glow(230, 230, 115f, 30f, new Color(UiArt.Cyan.r, UiArt.Cyan.g, UiArt.Cyan.b, 0.25f)), Vector2.zero, new Vector2(230f, 230f));
            avatarGlow.anchorMin = avatarGlow.anchorMax = new Vector2(0f, 1f);
            avatarGlow.anchoredPosition = new Vector2(105f, y);
            var avatar = ImageButton(bar, "Avatar", Avatars.Sprite(Avatars.Selected), new Vector2(0f, 1f), new Vector2(105f, y), new Vector2(165f, 158f), OpenAvatarPicker);
            homeAvatar = avatar.GetComponent<Image>();
            homeAvatar.preserveAspect = true;

            // Can: kalbin içinde sayı, hapın içinde "Full" ya da sonraki cana kalan süre.
            HomePill(bar, "Lives", UiArt.Asset("Home/Heart"), new Vector2(378f, y), 336f, out homeLivesTimer, ShowLivesInfo);
            var heart = (RectTransform)bar.Find("Lives/Icon");
            heart.sizeDelta = Vector2.one * HomePillHeight * 1.38f;          // kalp biraz büyük, hapın dışına taşar
            // Can penceresiyle aynı oran: yazı kalbin yarısı boyunda (220 kalp / 110 yazı).
            homeLivesCount = NewText("Count", heart, Mathf.RoundToInt(heart.sizeDelta.y * 0.5f), UiArt.Navy);
            Place(homeLivesCount.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, heart.sizeDelta.y * 8f / 220f), Vector2.zero);
            homeLivesText = homeLivesCount;
            homeLivesInfinity = InfinityMark(heart, heart.sizeDelta.y * 0.62f);
            homeLivesInfinity.gameObject.SetActive(false);
            homeLivesTimer.fontSize = 44;
            homeLivesTimer.rectTransform.offsetMin = new Vector2(HomePillHeight * 1.2f, 0f);   // kalple üst üste binmez   // kalpten plus'a kadar ortalı
            homeLivesTimer.rectTransform.offsetMax = new Vector2(-HomePillHeight * 0.9f, 0f);

            HomePill(bar, "Coins", UiArt.CoinIcon(), new Vector2(735f, y), CoinWidth * HomePillHeight / CoinHeight, out homeCoinText, () => SelectHomePage(0));

            var settings = ImageButton(bar, "Settings", UiArt.Asset("Home/Settings"), new Vector2(1f, 1f), new Vector2(-100f, y), new Vector2(150f, 132f), OpenSettings);
            settings.GetComponent<Image>().preserveAspect = true;
        }

        // Alt sekmeler: cam panel ekranın iki yanına sıfır, altına kadar iner (üst kenarı neon). Sekmeler ekranın altına göre
        // dizilir: yazılar alt kenara yakın; yalnız seçili sekmenin yazısı görünür ve simgesi büyüyüp panelin üstünden taşar.
        RectTransform homeTabRow, homeTabBar;
        readonly RectTransform[] homeTabIcons = new RectTransform[3];
        readonly Text[] homeTabLabels = new Text[3];
        const float TabPanelTrim = 30f;   // panel üst kenarı sekme düzeninden bu kadar aşağıda (simgeler yerinde kalır)
        const float TabLabelY = 90f, TabIconY = 150f, TabIconSize = 118f, TabSelectedSize = 200f, TabOverflow = 42f;

        void BuildHomeTabs()
        {
            var bar = homeTabBar = NewRect("Tabs", homeSafe);
            Place(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, HomeBarHeight));
            var shade = Img(bar, "Shade", UiArt.Glow(1080, 300, 0f, 120f, new Color(0.01f, 0.02f, 0.08f, 0.9f)), Vector2.zero, Vector2.zero);
            Stretch(shade);
            shade.GetComponent<Image>().preserveAspect = false;
            var sideMargin = ScreenFit.SideMargin;                         // geniş ekranda şerit sütundan ekran kenarlarına taşar
            shade.offsetMin = new Vector2(-sideMargin, -260f);
            shade.offsetMax = new Vector2(sideMargin, 120f);
            var panel = NewRect("Panel", bar);
            Stretch(panel);
            panel.offsetMin = new Vector2(-sideMargin, -260f);             // yanlar ekran kenarında, alt güvenli alanın altına kadar
            panel.offsetMax = new Vector2(sideMargin, -TabPanelTrim);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = UiArt.Glass(Mathf.RoundToInt(1080f + 2f * sideMargin), Mathf.RoundToInt(HomeBarHeight + 260f), 70f, new Color(0.1f, 0.2f, 0.5f), new Color(0.03f, 0.06f, 0.2f), UiArt.Cyan, 5f);
            panelImage.raycastTarget = true;

            homeTabRow = NewRect("Tab Row", home.transform);              // ekranın altına göre (güvenli alan değil)
            Place(homeTabRow, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 0f));
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var x = (i - 1) * 355f;
                var tab = NewRect(HomeTabNames[i], homeTabRow);
                Place(tab, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 0f), new Vector2(330f, 320f));
                var hit = tab.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var hl = Img(tab, "Selected", UiArt.Asset("Home/Selected_Tab", new Vector4(110f, 110f, 110f, 110f)), Vector2.zero, new Vector2(300f, 120f)).GetComponent<Image>();
                hl.rectTransform.anchorMin = hl.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                hl.rectTransform.anchoredPosition = new Vector2(0f, TabLabelY + 6f);
                hl.type = Image.Type.Sliced;
                hl.preserveAspect = false;
                hl.pixelsPerUnitMultiplier = 279f / 120f;
                homeTabHighlights[i] = hl;
                var button = tab.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => SelectHomePage(index));
                tab.gameObject.AddComponent<ButtonPunch>();
                var icon = Img(tab, "Icon", UiArt.Asset(HomeTabIcons[i]), Vector2.zero, Vector2.one * TabIconSize);
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0f);
                icon.anchoredPosition = new Vector2(0f, TabIconY);
                homeTabIcons[i] = icon;
                var label = NewText("Label", tab, 42, UiArt.Navy);
                Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, TabLabelY), new Vector2(330f, 54f));
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchoredPosition = new Vector2(0f, TabLabelY);
                label.text = HomeTabNames[i];
                homeTabLabels[i] = label;
                homeTabs[i] = tab;
            }
        }

        // Seçili sekme simgesi panelin üst kenarından TabOverflow kadar taşar; geçiş yumuşak.
        void UpdateTabs()
        {
            if (!homeTabRow) return;
            var rootHeight = ((RectTransform)home.transform).rect.height;
            var panelTop = homeSafe.anchorMin.y * rootHeight + HomeBarHeight;   // panelin üst kenarı, ekranın altından
            var k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            for (var i = 0; i < 3; i++)
            {
                var on = i == homePage;
                var size = on ? TabSelectedSize : TabIconSize;
                var y = on ? panelTop + TabOverflow - TabSelectedSize / 2f : TabIconY;
                var icon = homeTabIcons[i];
                // Seçili kutucuk panelin alt kenarından (ekranın altı) üst kenarına kadar uzanır.
                var hl = homeTabHighlights[i].rectTransform;
                hl.pivot = new Vector2(0.5f, 0f);
                hl.sizeDelta = new Vector2(300f, panelTop - TabPanelTrim - 14f);
                hl.anchoredPosition = new Vector2(0f, 6f);
                homeTabHighlights[i].pixelsPerUnitMultiplier = 279f / 150f;   // köşeler hap yarıçapında kalsın
                icon.sizeDelta = Vector2.Lerp(icon.sizeDelta, Vector2.one * size, k);
                icon.anchoredPosition = Vector2.Lerp(icon.anchoredPosition, new Vector2(0f, y), k);
            }
        }

        void OpenAvatarPicker()
        {
            if (homeAvatarPicker) Destroy(homeAvatarPicker);
            homeAvatarPicker = AvatarPicker.Open(transform, HomePopupLayer, this, index => homeAvatar.sprite = Avatars.Sprite(index));
        }

        void SelectHomePage(int page)
        {
            homePage = page;
            for (var i = 0; i < 3; i++)
            {
                homePages[i].gameObject.SetActive(i == page);
                homeTabHighlights[i].gameObject.SetActive(i == page);
                homeTabLabels[i].gameObject.SetActive(i == page);       // yalnız seçili sekmenin yazısı
                homeTabIcons[i].GetComponent<Image>().color = new Color(1f, 1f, 1f, i == page ? 1f : 0.75f);
            }
            homeCollection.Hide();
            if (page == 2) homeCollection.Show(UnlockedPictures, homeTotal);
            if (page == 1) homeTime = 0f;
        }

        // ------------------------------------------------------------------ canlar
        void ShowLivesInfo()
        {
            if (Lives.IsFull) return;                // can dolu: hiçbir şey olmaz
            ShowNoLives(false);
        }

        /// <summary>Can bilgisi / can bitti penceresi: coinle doldurma ya da bekleme.</summary>
        public void ShowNoLives(bool empty = true)
        {
            ShowDialog(empty ? "Out of Lives" : "Lives", UiArt.Asset("Home/Heart"),
                $"Next life in {Lives.NextText}", "Refill", Lives.RefillCost, "+1 Life",
                () =>
                {
                    if (!Lives.TryRefill()) { DialogDenied(); HomeToast("Not enough coins"); return; }
                    Telemetry.CoinsSpent("refill_lives", Lives.RefillCost);
                    HideDialog();
                    Haptics.GateExit(this);
                    HomeToast("Lives refilled!");
                    RefreshHome();
                },
                () =>
                {
                    if (Lives.IsFull) { HideDialog(); return; }
                    Ads.ShowRewarded(() =>
                    {
                        Telemetry.RewardedAd("life", homeLevel);
                        if (!Lives.AddOne()) { HideDialog(); return; }
                        Haptics.Select();
                        HomeToast("+1 life!");
                        if (Lives.IsFull) HideDialog();
                    }, () => { DialogDenied(); HomeToast("Ad not ready, try again"); });
                },
                HideDialog, 220f, caption: $"Refill all {Lives.Max} lives", secondaryIcon: UiArt.Asset("Popup/ad_icon"), tapToClose: true);
            SetDialogIconText(Lives.Count.ToString());
        }

        const float HomeInfoInterval = 0.25f;
        float homeInfoTimer;

        // Can, sayaç ve coin metinleri: geri sayım saniyelik olduğundan her karede yenilenmez.
        void RefreshHomeInfo()
        {
            homeInfoTimer = HomeInfoInterval;
            var lives = Lives.Count;
            // Sınırsız can: kalpte sayı yerine ∞.
            homeLivesCount.gameObject.SetActive(!Lives.IsInfinite);
            homeLivesInfinity.gameObject.SetActive(Lives.IsInfinite);
            homeLivesCount.text = NumberText.Of(lives);
            homeLivesTimer.text = Lives.IsInfinite ? Lives.InfiniteText : lives >= Lives.Max ? "Full" : Lives.NextText;
            homeCoinText.text = CoinWallet.Short(CoinWallet.Coins);
            if (dialogPopup != null && dialogPopup.Visible)
                dialogPopup.TryRefresh("Lives", $"Next life in {Lives.NextText}", NumberText.Of(lives));   // +1 Life sonrası da güncel
        }

        void UpdateHome()
        {
            PlaceDevMenu();
            if (!HomeVisible) return;
            UpdateTabs();
            homeTime += Time.unscaledDeltaTime;
            homeSafe.anchorMin = safeArea.anchorMin;
            homeSafe.anchorMax = safeArea.anchorMax;
            homeMap.Tick(homeTime);
            if (homePage == 0) homeShop.Tick();
            else if (homePage == 2) homeCollection.Tick();
            homeInfoTimer -= Time.unscaledDeltaTime;
            if (homeInfoTimer <= 0f) RefreshHomeInfo();
            if (homeToastTime > 0f)
            {
                homeToastTime -= Time.unscaledDeltaTime;
                homeToast.color = new Color(1f, 1f, 1f, Mathf.Clamp01(homeToastTime / 0.3f));
                if (homeToastTime <= 0f) homeToast.text = "";
            }
        }
    }
}
