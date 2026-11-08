using System.Collections;
using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Prototip bölüm denetleyicisi. Tahta (fizikli sürükleme, kapı eşiğinde çıkış, kapı animasyonu),
    /// bant, slotlar ve atış (ConveyorRunner) burada birleşir. Bölümü kurar, çıkış isteklerini kurallara göre
    /// karara bağlar, ekranı yerleştirir ve geliştirici HUD'unu çizer. Şimdilik süre sınırı yoktur.
    /// </summary>
    public sealed partial class LevelController : MonoBehaviour
    {
        const float PictureSize = 6.0f;        // gizli resim ve boncuk ızgarası kare
        const float BoardToSlots = 1.35f;      // tahta üst kenarı ile slot sırası arasındaki boşluk
        const float HudHeight = 1.7f;          // üst bar için bırakılan pay
        const float BottomMargin = 0.6f;
        const float SideMargin = 0.6f;
        const float OrbitEdgeMargin = 0.08f;   // kartuş deposu ekran kenarına neredeyse değer
        const float ExitClearance = 0.55f;      // blok kapıyı geçince kapı çizgisinden bu kadar uzakta durur
        const float SeparationSpread = 0.35f;   // ayrılırken hücreler arasına açılan boşluk (hücre aralığı oranı)
        const float SeparationSeconds = 0.1f;
        const float ExitLayer = 0.6f;          // kapıdan çıkan blok/roket kameraya doğru bu kadar yükselir: bekleme alanındaki roketlerin önünde görünür
        const float UnfoldSeconds = 0.15f;
        const float LiquidPassSeconds = 0.6f, LiquidSettleSeconds = 0.25f;   // katmanlı bloğun dış katmanı kapıdan akar
        const float MorphSeconds = 0.22f;      // blok → roket gövdesi geçişi (açılmayla birlikte dönüşüm ~0,3 sn)

        [SerializeField] LevelDatabase database;
        [SerializeField] PrototypePalette palette;
        [SerializeField] ModelLibrary models;
        [SerializeField] Camera worldCamera;
        [Tooltip("Kameranın alttan bakış açısı (derece). 0 tam üstten 2D görünümdür.")]
        [SerializeField, Range(0f, 35f)] float cameraTilt = 16f;
        [Tooltip("Dar görüş açısı perspektif bozulmasını azaltır.")]
        [SerializeField, Range(15f, 60f)] float cameraFov = 30f;

        public BoardModel Board { get; private set; }
        /// <summary>Bant kapasitesi (birim): küçük roket 1, büyük roket 2.</summary>
        public OrbitModel Orbit { get; private set; }
        public BoardView BoardView { get; private set; }
        public bool InputEnabled { get; private set; }
        /// <summary>Bölümün boncuk ızgarası; boncuksuz bölümlerde null.</summary>
        public BeadGrid Beads => beads;
        public ConveyorRunner Runner => runner;

        readonly Dictionary<int, RocketView> rocketViews = new();
        readonly HashSet<int> peeling = new();   // dış katmanı kapıdan akan katmanlı bloklar: çekirdek büyüyene kadar tutulamaz
        ConveyorTrack track;
        ConveyorRunner runner;
        BeadGrid beads;
        BoardInput input;
        Transform levelRoot;
        LevelData level;
        int levelIndex;
        SpriteRenderer sky;
        RewardDoor door;
        float doorWidth;
        bool finishing;                        // bölüm sonu gösterisi sürüyor
        bool zoomed;                           // kamera resme yaklaştı (son piksel kalınca başlar)
        const float ZoomBeforeHeadbutt = 0.9f; // yakınlaşma bitmeden roket kilide uçmasın
        const float DoorDepth = 0.3f;          // kapı kökü: kapaklar resmin (0,535) önünde, kilit piksellerin (−0,25) arkasında
        const float FinaleZoomSeconds = 1.1f;
        const float PictureShowSeconds = 1.2f; // açılan resim popup'tan önce bu kadar izlenir
        const float FireworksSeconds = 4.2f;
        const float PictureUnderRail = 0.3f;   // resim iç rayın altına bu kadar taşar: kenarda boşluk kalmaz
        float levelTime;                       // bölümde geçen süre (süresiz bölümde gösterilir)
        // Süre sınırı: kalan süre 0,25 sn'lik adımlarla azalır; her tam saniyede
        // (dört adımda bir) uyarılar denetlenir. Son saniyeler yavaşlamaz. Duraklatma (timeScale 0) sayacı durdurur.
        const float TimerTick = 0.25f;
        float timeLeft, tickClock;
        int ticks;
        GameHud hud;
        BeadField field;
        PhysicsMaterial2D blockMaterial;
        float nextLevelTimer = -1f;
        int passing;                           // kapıdan geçmekte olan roket sayısı
        int passingUnits;                      // ... ve birim toplamı

        void Start()
        {
            Application.targetFrameRate = 60;
            // URP Rendering Debugger çok parmaklı dokunuşla açılıyor (yörüngede/ana ekranda kaydırırken): kapalı.
            UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI = false;
            // Block Physics Material 2D: sürtünme ve sekme yok.
            blockMaterial = new PhysicsMaterial2D("Block") { friction = 0f, bounciness = 0f };
            ModelKit.Init(palette.red.shader);
            input = gameObject.AddComponent<BoardInput>();
            worldCamera.backgroundColor = SceneArt.Hex(0x080D2B);
            // Oyun içi arka plan: ana ekranın yıldızlı düz arka planı (ana ekran kodla çizilen bulutsulu gökyüzünü kullanır).
            var bgTex = Resources.Load<Texture2D>("UI/Home/Background_Simple");
            var bgSprite = bgTex ? Sprite.Create(bgTex, new Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f), 100f) : SceneArt.Sky();
            sky = SceneArt.Place(bgSprite, "Sky", worldCamera.transform, Vector3.zero, -100);
            levelCamera = new LevelCamera(worldCamera, sky);
            SetupBloom();
            hud = GameHud.Create();
            hud.PreviousPressed += () => LoadLevel(levelIndex - 1);
            hud.RestartPressed += () => LeaveLevel(toHome: false);
            hud.NextPressed += () => LoadLevel(levelIndex + 1);
            if (DevTools.LevelSkip) hud.BadgePressed += DebugClearBeads;   // rozete dokununca bitir: yalnız geliştirme derlemesinde
            hud.BoosterPressed += UseBooster;
            hud.DevCoinsPressed += amount => { CoinWallet.Add(amount); hud.SetCoins(CoinWallet.Coins); };
            hud.DevBoosterPressed += (b, delta) => { BoosterInventory.DevAdd(b, delta); RefreshBoosterCount(b); };
            hud.DevTimePressed += DevAddTime;
            hud.DevResetTutorialsPressed += DevResetTutorials;
            // Geliştirme: 'afj_dev_level' (1'den başlar) varsa o bölümle açılır; tek seferlik, okunduktan sonra silinir.
            Ads.Initialize();
            Store.Initialize();
            Telemetry.Initialize();
            SetupHome();
            var dev = GameSave.GetInt(SaveKeys.DevStartLevel, 0);
            GameSave.DeleteKey(SaveKeys.DevStartLevel);
            if (dev > 0) LoadLevel(dev - 1);
            else GoHome();
        }

        // Neon ışıklar (HDR renkli Unlit malzemeler) çevrelerine ışık saçsın diye kameraya bloom eklenir.
        void SetupBloom()
        {
            var data = worldCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (!data) data = worldCamera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            worldCamera.allowHDR = true;
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.55f);
            bloom.highQualityFiltering.Override(false);
            var volume = new GameObject("Post Processing").AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // Bölümün kendi gizli resmi varsa o; yoksa 7. bölümün gizli resmi (astronot kedi) kullanılır.
        const int SharedPictureLevel = 6;
        string activePicture;

        string PictureNameOf(LevelData data) => !string.IsNullOrEmpty(data.pictureName) ? data.pictureName
            : database.levels.Count > SharedPictureLevel ? database.levels[SharedPictureLevel].pictureName : null;

        // Yeni resim alınır, önceki bırakılır: iki bölüm aynı resmi kullanıyorsa yeniden yüklenmez.
        Texture2D SwitchPicture(string name)
        {
            var texture = LevelPictures.Acquire(name);
            LevelPictures.Release(activePicture);
            activePicture = texture ? name : null;
            return texture;
        }

        public void LoadLevel(int index)
        {
            levelIndex = (index % database.levels.Count + database.levels.Count) % database.levels.Count;
            level = database.levels[levelIndex];

            StopAllCoroutines();
            if (levelRoot) Destroy(levelRoot.gameObject);
            levelRoot = new GameObject("Level").transform;
            rocketViews.Clear();
            peeling.Clear();
            nextLevelTimer = -1f;
            timerStarted = false;
            hud.HideHome();
            passing = 0;
            passingUnits = 0;
            levelTime = 0f;
            timeLeft = TimeLimit;
            tickClock = 0f;
            ticks = 0;
            hud.ResetTimer();
            freezeLeft = 0f;
            hud.SetFreeze(0f, 0f);
            hangarUses = 0;
            hud.HideBoosterPrompt();
            hud.HideDialog();
            dialogOpen = false;
            ufoPicking = false;
            ufoBusy = false;
            warpPicking = false;
            continuesUsed = 0;
            boostersUsed = 0;
            levelVersion = Telemetry.VersionOf(level);
            Telemetry.LevelStart(levelIndex + 1, levelVersion, NextAttempt(levelIndex + 1));
            for (var b = 0; b < BoosterInventory.Count; b++)
            {
                boosterLocked[b] = !BoosterInventory.IsUnlocked(b, levelIndex + 1);
                if (BoosterInventory.GrantUnlockGift(b, levelIndex + 1)) unlockedNow = b;
                hud.SetBoosterLocked(b, boosterLocked[b] ? BoosterInventory.UnlockLevel[b] : 0);
                RefreshBoosterCount(b);
            }
            unlockedNow = -1;
            tutorialBooster = -1;
            hud.EndBoosterTutorial();
            ResetGuides();
            if (TimeLimit > 0) hud.TimeWarningAt(timeLeft);   // yüklenince başlangıç süresiyle bir kez
            finishing = false;
            zoomed = false;
            door = null;

            Board = new BoardModel(level.boardWidth, level.boardHeight, level.rockets, level.exits, level.holes, level.asteroids);
            // Buz sayacı azalan / kilidi açılan blokların görünümü güncellenir.
            Board.FrozenChanged += i =>
            {
                if (rocketViews.TryGetValue(i, out var v)) v.UpdateFrozen(Board.IceLeft(i), Board.IsLocked(i));
                // Kilit açıldı ya da buz çözüldü (sayaç azalınca ses yok, yalnız açılışta).
                var r = Board.GetRocket(i);
                if (r.lockGroup > 0 && !Board.IsLocked(i)) Sfx.Play(Sfx.Unlock);
                else if (r.lockGroup == 0 && Board.IceLeft(i) == 0) Sfx.Play(Sfx.IceBreak);
            };
            // Sayaçlı asteroit: sayaç azalır, sıfırda kırılır.
            Board.AsteroidChanged += k =>
            {
                if (BoardView && k < BoardView.Asteroids.Count && BoardView.Asteroids[k]) BoardView.Asteroids[k].UpdateCount(Board.AsteroidLeft(k));
                if (Board.AsteroidLeft(k) < 0) Sfx.Play(Sfx.AsteroidBreak);   // kırılan sayaçlı asteroit -1 olur
            };
            Orbit = new OrbitModel(level.orbitCapacity);

            BoardView = new GameObject("Board").AddComponent<BoardView>();
            BoardView.transform.SetParent(levelRoot, false);
            BoardView.transform.position = new Vector3(-level.boardWidth / 2f, 0f, 0f);
            BoardView.Build(Board, palette, models);
            // Tahtanın arkasında, çerçeveden biraz taşan koyu zemin; tahtanın (deliklerin) şeklini izler.
            SceneArt.Place(SceneArt.BoardBackdrop(Board, BoardView.FrameThickness + 0.25f), "Board Backdrop", levelRoot,
                new Vector3(0f, level.boardHeight / 2f, ModelKit.GroundOffset + 0.25f));

            for (var i = 0; i < Board.RocketCount; i++)
            {
                var spec = Board.GetRocket(i);
                var view = new GameObject($"Rocket {spec.id}").AddComponent<RocketView>();
                view.transform.SetParent(levelRoot, false);
                view.Build(i, spec, palette, models);
                view.transform.position = BoardView.CellToWorld(RocketView.CenterOf(spec.origin, spec.size));
                view.AddBoardPhysics(blockMaterial);
                rocketViews[i] = view;
            }

            // Bant ve slotlar: slot sırası tahtanın üstünde, bant ve resim onun üstünde.
            var k = OrbitTuning.Scale(PictureSize);
            var (beltHalfWidth, beltHalfHeight, beltCenter) = BeltPath.Extents(PictureSize);
            var modelScale = OrbitDesign.ScaleFor(PictureSize);
            var slotRowFromCenter = -beltCenter.y + beltHalfHeight + OrbitDesign.SlotRowBelowOrbit * k;
            var pictureCenter = new Vector3(0f, level.boardHeight + BoardView.FrameThickness + BoardToSlots + slotRowFromCenter, 0f);
            track = new GameObject("Conveyor").AddComponent<ConveyorTrack>();
            track.transform.SetParent(levelRoot, false);
            track.transform.position = pictureCenter;
            track.Build(PictureSize, level.slotCount, level.orbitCapacity, palette, models);

            field = null;
            beads = null;
            startPixels = 0;
            if (level.HasBeads)
            {
                beads = new BeadGrid(level.beadColumns, level.beadRows, level.beadLayout);
                startPixels = beads.Remaining;
                field = new GameObject("Beads").AddComponent<BeadField>();
                field.transform.SetParent(track.transform, false);
                // Ödül kapısı ve gizli resim yörüngenin iç rayına kadar uzanır (resmin köşeleri rayın altında kalır).
                doorWidth = 2f * OrbitDesign.InnerRail * OrbitDesign.ScaleFor(PictureSize);
                field.Build(beads, SwitchPicture(PictureNameOf(level)), PictureSize, palette, doorWidth + PictureUnderRail, models.pixel);
                door = new GameObject("Reward Door").AddComponent<RewardDoor>();
                door.transform.SetParent(track.transform, false);
                door.transform.localPosition = new Vector3(0f, 0f, DoorDepth);
                // Kapı: AFJ_Simple_Reward_Door; kilit: ayrı manyetik toka kilit (AFJ_Space_Padlock), kapının gömülü kilidinin yerine.
                door.Build(models.rewardDoor, models.padlock, ConveyorTrack.Neon(models.neonMaterial, SceneArt.Cyan), doorWidth);
            }
            else
            {
                Primitives.Create(PrimitiveType.Cube, "Picture", track.transform, palette.picture,
                    new Vector3(0f, 0f, 0.5f), new Vector3(PictureSize, PictureSize, 0.1f));
            }

            runner = levelRoot.gameObject.AddComponent<ConveyorRunner>();
            runner.Init(track, Orbit, beads, field, palette);
            runner.BeadCleared += CheckFinished;
            runner.BeadCleared += ZoomOnLastPixel;
            runner.OutOfSpace += () => Fail(FailKind.Space);
            runner.SlotLaunched += GuideOnSlotLaunched;
            runner.MoreRocketsComing = MoreRocketsComing;

            // Altta ana ekran göstergesine (home indicator) pay bırakılır.
            var bottom = -BoardView.FrameThickness - BottomMargin;
            var top = pictureCenter.y + beltCenter.y + beltHalfHeight + HudHeight;
            // Yörünge yatayda ortalıdır; soldan taşan kartuş deposu ekran kenarına değmesin diye iki yana aynı pay bırakılır.
            var boardHalf = level.boardWidth / 2f + BoardView.FrameThickness;
            var halfWidth = Mathf.Max(boardHalf + SideMargin, OrbitDesign.LeftExtent * modelScale + OrbitEdgeMargin);
            levelCamera.SetContent(Rect.MinMaxRect(-halfWidth, bottom, halfWidth, top));
            FitCamera();

            input.Init(this, worldCamera);
            InputEnabled = true;
            StartCoroutine(DevFail());
            StartCoroutine(LevelIntro());
        }

        public bool HasRocketView(int index) => rocketViews.ContainsKey(index) && !peeling.Contains(index);

        public void SetGateGlow(GameColor color, bool on) => SetGateGlow(color, on, false);

        /// <summary>Tutulan blokla aynı renkteki kapılar parlar; renksiz engel blok hiçbir kapıyı parlatmaz.</summary>
        public void SetGateGlow(GameColor color, bool on, bool isBlocker)
        {
            if (isBlocker && on) return;
            foreach (var g in BoardView.Gates)
                if (g.Spec.color == color) g.SetGlow(on);
        }

        /// <summary>Tahta dışına dokunma: slottaki roketi banda gönderir.</summary>
        public void TapOutsideBoard(Vector3 world)
        {
            if (ufoPicking || warpPicking) return;   // seçimde tahta dışı: BoardInput PickOutsideBoard'u çağırır
            if (!GuideAllowsSlot) return;
            NotifyMove();
            // Yörünge doluysa roket itme animasyonuyla reddedilir; ekranda yazı çıkmaz.
            if (runner.TryTapSlot(world, out var rejected) && rejected) NoRoomFeedback();
        }

        /// <summary>Bloğun parçaları şu an yörüngeye ve boş slotlara sığar mı (TryExit ile aynı yer koşulu)?</summary>
        public bool HasRoomFor(int index)
        {
            var units = Board.GetRocket(index).OrbitUnits;
            var toBelt = Mathf.Min(Orbit.FreeUnits, units);
            return runner.FreeSlotCount >= units - toBelt;
        }

        /// <summary>Yer yok: yörünge sayacı kırmızı yanıp söner.</summary>
        public void NoRoomFeedback()
        {
            hud.PulseBeltCounter();
            Sfx.Play(Sfx.Wrong);
        }

        void ShowMessage(string text, float seconds = 1.8f) => hud.ShowMessage(text, seconds);

        void RefreshHud()
        {
            if (level == null) return;
            hud.SetLevel(levelIndex + 1);
            if (TimeLimit > 0) hud.SetTimeLeft(timeLeft);
            else hud.SetTime(levelTime);
            hud.SetBeltCounter(worldCamera, track.CounterPoint, Orbit.FreeUnits, Orbit.Capacity);
        }

        // Kapıdan geçen roket varsa ya da tahtadaki bir blok (engeller hariç) yörüngedeki ve slotlardaki boş yere sığıyorsa
        // kaybedilmez (TryExit ile aynı yer koşulu). Bloğun kapıya ulaşıp ulaşamayacağına bakılmaz: her durumda çıkarılabilir.
        // Yörünge ve slotlar doluysa blok çıkamaz; o zaman tahtadaki bloklar kaybı engellemez.
        bool MoreRocketsComing()
        {
            if (passing > 0) return true;
            var room = Orbit.FreeUnits + runner.FreeSlotCount;
            for (var i = 0; i < Board.RocketCount; i++)
                if (Board.IsOnBoard(i) && !Board.GetRocket(i).isBlocker && Board.GetRocket(i).OrbitUnits <= room) return true;
            return false;
        }

        // Otomatik bitirici kuralı: tahtada blok (engeller hariç) kaldıkça otomatik bitirici açılmaz;
        // tahta boşalınca kapıda, bantta ve slotlarda kalan roketlerin hepsi banda sığıyorsa açılır.
        void UpdateAutoFinisher()
        {
            if (runner.AutoFinisherActive || beads == null) return;
            for (var i = 0; i < Board.RocketCount; i++)
                if (Board.IsOnBoard(i) && !Board.GetRocket(i).isBlocker) return;
            if (passingUnits + runner.RemainingUnits > Orbit.Capacity) return;
            runner.ActivateAutoFinisher();
            Sfx.Play(Sfx.SpeedUp);
            GuideOnAutoFinisher();
        }

        float TimeLimit => GameSave.GetInt(SaveKeys.DevTimeLimit, 0) > 0 ? GameSave.GetInt(SaveKeys.DevTimeLimit) : level.timeLimit;

        void TickTimer()
        {
            tickClock += Time.deltaTime;
            while (tickClock >= TimerTick && timeLeft > 0f)
            {
                tickClock -= TimerTick;
                timeLeft -= TimerTick;
                if (++ticks % 4 == 0 && timeLeft > 0f) hud.TimeWarningAt(timeLeft);
            }
            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                hud.SetTimeLeft(0f);
                Fail(FailKind.Time);
            }
        }

        // Tahtada oyuncunun çıkaracağı blok kalmadı mı (engeller sayılmaz)?
        bool BoardCleared()
        {
            if (Board == null) return false;
            for (var i = 0; i < Board.RocketCount; i++)
                if (Board.IsOnBoard(i) && !Board.GetRocket(i).isBlocker) return false;
            return true;
        }

        void Update()
        {
            if (!levelRoot) return;                     // ana ekran: bölüm yok
            UpdatePickFocus();
            if (runner != null && InputEnabled)
            {
                UpdateAutoFinisher();
                UpdateSlotGuide();
            }
            // Süre, oyuncunun yapacak işi kaldıkça akar: tahta boşalınca (kalan roketler kendiliğinden atar) ve bölüm bitişinde durur.
            if (InputEnabled && timerStarted && !dialogOpen && nextLevelTimer < 0f && !finishing && !BoardCleared())
            {
                levelTime += Time.deltaTime;
                if (freezeLeft > 0f)
                {
                    freezeLeft = Mathf.Max(0f, freezeLeft - Time.deltaTime);
                    hud.SetFreeze(freezeLeft / TimeFreezeSeconds, freezeLeft);
                }
                else if (TimeLimit > 0) TickTimer();
            }
            RefreshHud();
            // Boncuksuz prototip bölümleri: tahta boşalıp tüm roketler turunu bitirince sonraki bölüme geçilir.
            if (beads == null && nextLevelTimer < 0f && InputEnabled && Board.RemainingCount == 0 && passing == 0 && runner.ActiveCount == 0)
            {
                ShowMessage("Board cleared!");
                InputEnabled = false;
                nextLevelTimer = 1.5f;
            }
            if (nextLevelTimer > 0f)
            {
                nextLevelTimer -= Time.deltaTime;
                if (nextLevelTimer <= 0f) LoadLevel(levelIndex + 1);
            }
            FitCamera();
        }
    }
}
