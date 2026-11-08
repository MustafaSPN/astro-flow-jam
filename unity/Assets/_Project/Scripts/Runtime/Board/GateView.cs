using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Tahta kenarındaki renkli kapı (AFJ_Simple_Gate_2Units): iki renkli kol, uçlarında metalik gri halkalar. Model 2 hücre
    /// genişliğinde; 1 ve 3 hücrelik kapılar için kollar enine ölçeklenir (her kol açıklığın yarısını kaplar). Kapı fiziksel
    /// olarak her zaman kapalıdır (duvar kesintisizdir); uyan blok kapı eşiğine gelince kollar 0,2 sn'de OutExpo ile duvara
    /// doğru çekilir, blok geçince aynı şekilde kapanır. Seçilen blokla aynı
    /// renkteki kapılar hafifçe parlar.
    /// </summary>
    public sealed class GateView : MonoBehaviour
    {
        const int ModelWidth = 2;
        const float OpenWidth = 0.06f;           // açıkken kolun kapalı genişliğine oranı
        const float ModelInset = BoardView.FrameThickness / 2f;
        static readonly Color RingMetal = new(0.62f, 0.66f, 0.74f);   // kol uçlarındaki halkalar: metalik gri
        const string RingMaterial = "Gate_Joint_Metal";
        const string ArrowMaterial = "Gate_Arrow_Changeable";

        public ExitSpec Spec { get; private set; }

        /// <summary>Kapıdan tahta dışına doğru birim yön (dünya uzayında).</summary>
        public Vector3 Outward { get; private set; }

        /// <summary>Kapı çizgisi (tahta kenarı) üzerindeki orta nokta.</summary>
        public Vector3 LinePoint => transform.position - Outward * (BoardView.FrameThickness / 2f);

        Transform modelRoot;
        readonly List<(Transform door, Vector3 closedPosition, Vector3 closedScale, Vector3 openPosition, Vector3 openScale)> doors = new();
        readonly List<Material> glowMaterials = new();
        readonly List<Color> glowBase = new();
        readonly List<bool> glowIsLight = new();
        SpriteRenderer halo;
        Color gateColor;
        public const float HaloAlpha = 0.18f;         // hale kapının kendi renginde, yumuşak
        const float LightBoost = 0.06f;        // ışık şeritleri yalnızca hafifçe parlar
        const float BreathSeconds = 1.3f;      // nefes ritmi
        const float GlowOffSeconds = 0.5f;     // blok bırakılınca hale azalarak söner
        float breathTime;
        int openCount;
        float slide, slideFrom, slideTo, slideTime = -1f;
        float glow, glowFrom, glowTo, glowTime = -1f, glowDuration;
        float shakeTime = -1f;

        /// <summary>Yeni kitte köşe ayrı modüldür; kapının uçlarında değişiklik gerekmez.</summary>
        public void UseCornerEnd(bool left) { }

        public void Build(ExitSpec spec, int boardWidth, int boardHeight, PrototypePalette palette, ModelLibrary models)
        {
            Spec = spec;
            var first = BoardView.EdgeSegmentCenter(spec.edge, spec.start, boardWidth, boardHeight);
            var last = BoardView.EdgeSegmentCenter(spec.edge, spec.start + spec.width - 1, boardWidth, boardHeight);
            transform.localPosition = (first + last) / 2f;
            Outward = spec.edge switch
            {
                Edge.Top => Vector3.up,
                Edge.Bottom => Vector3.down,
                Edge.Left => Vector3.left,
                _ => Vector3.right,
            };
            // Yerel +y dışarıyı gösterir; modelin açıklığı yerel x boyunca uzanır.
            // Yalnızca Z ekseni etrafında döner; FromToRotation ters yönlerde (alt kenar) X etrafında çevirip kapıyı zeminin
            // altına sokuyordu.
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Outward.y, Outward.x) * Mathf.Rad2Deg - 90f);

            var offset = new Vector3(0f, ModelInset - BoardView.FrameThickness / 2f, ModelKit.GroundOffset);
            modelRoot = ModelKit.Spawn(models.gate, transform, offset, Quaternion.identity, ModelKit.ColorOf(palette, spec.color),
                unique: true, uniqueMaterials: glowMaterials);
            // Renkli kapaklar blokların malzemesiyle aynı (üç boyutlu, aynı yansıma); ışık şeritleri düz parlak.
            var shaded = new Dictionary<Material, Material>();
            foreach (var m in glowMaterials)
            {
                Material copy;
                if (m.name.StartsWith("Gate_Light_Changeable") && models.neonMaterial)
                {
                    copy = new Material(models.neonMaterial) { name = m.name + " (ışık)" };
                    copy.SetColor(ShaderIds.BaseColor, m.GetColor(ShaderIds.BaseColor));
                }
                else
                {
                    // Kapaklar ve renk şeridi blokların malzemesinin kendisi: aynı parlaklık ve yansıma.
                    copy = ModelKit.GateMaterial(ModelKit.ColorOf(palette, spec.color));
                }
                shaded[m] = copy;
            }
            foreach (var r in modelRoot.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                    if (mats[i] && shaded.TryGetValue(mats[i], out var copy)) mats[i] = copy;
                r.sharedMaterials = mats;
            }
            glowMaterials.Clear();
            glowMaterials.AddRange(shaded.Values);
            foreach (var m in glowMaterials)
            {
                glowBase.Add(m.GetColor(ShaderIds.BaseColor));
                glowIsLight.Add(m.name.EndsWith("(ışık)"));
            }
            BuildHalo(ModelKit.ColorOf(palette, spec.color), spec.width);

            // Kol uçlarındaki halkalar metalik gri, parlak.
            var ring = new Material(ModelKit.BlockMaterial(Color.white)) { name = RingMaterial + " (metal)" };
            ring.SetColor(ShaderIds.BaseColor, RingMetal);
            ring.SetFloat(ShaderIds.Smoothness, 0.95f);
            foreach (var r in modelRoot.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                    if (mats[i] && mats[i].name.StartsWith(RingMaterial)) mats[i] = ring;
                r.sharedMaterials = mats;
            }
            // Kolların altındaki yön okları kapının renginin açık tonunda (kollar açılınca görünür).
            var arrow = new Material(ModelKit.BlockMaterial(Color.white)) { name = ArrowMaterial + " (ok)" };
            arrow.SetColor(ShaderIds.BaseColor, Color.Lerp(ModelKit.ColorOf(palette, spec.color), Color.white, 0.45f));
            foreach (var r in modelRoot.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                    if (mats[i] && mats[i].name.StartsWith(ArrowMaterial)) mats[i] = arrow;
                r.sharedMaterials = mats;
            }
            Adapt(Mathf.Clamp(spec.width, 1, 3));
            ArrangeChevrons(spec.width);
        }

        // Oklar hücre başına bir çift: modeldeki iki çift (x = ∓0,5) 'width' hücrenin ortalarına dağıtılır, eksikler kopyalanır.
        void ArrangeChevrons(int width)
        {
            var pairs = new List<List<Transform>>();
            foreach (var t in modelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.Contains("Chevron_")) continue;
                var i = t.name.Contains("Chevron_0_") ? 0 : 1;
                while (pairs.Count <= i) pairs.Add(new List<Transform>());
                pairs[i].Add(t);
            }
            if (pairs.Count == 0) return;
            while (pairs.Count < width)
            {
                var copy = new List<Transform>();
                foreach (var t in pairs[0]) copy.Add(Instantiate(t.gameObject, t.parent).transform);
                pairs.Add(copy);
            }
            for (var p = 0; p < pairs.Count; p++)
            {
                var active = p < width;
                var pairCenter = 0f;
                foreach (var t in pairs[p]) pairCenter += transform.InverseTransformPoint(t.GetComponent<Renderer>().bounds.center).x;
                pairCenter /= pairs[p].Count;
                var dx = p - (width - 1) / 2f - pairCenter;
                foreach (var t in pairs[p])
                {
                    t.gameObject.SetActive(active);
                    if (active) Shift(t, dx);
                }
                if (!active) continue;
                // Modeldeki "V" tahtanın içine (yerel −y) bakar; çift kendi merkezi etrafında 180° döner, ok dışarı bakar.
                var center = Vector3.zero;
                foreach (var t in pairs[p]) center += t.GetComponent<Renderer>().bounds.center;
                center /= pairs[p].Count;
                foreach (var t in pairs[p]) t.RotateAround(center, transform.forward, 180f);
            }
        }

        static Transform FindArm(Transform root, string suffix)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains(suffix)) return t;
            return null;
        }

        // Kolları 'width' hücreye uyarlar: her kol açıklığın yarısını kaplar. Açık: dış kenarı duvarda sabit, daralır.
        void Adapt(int width)
        {
            var ratio = width / (float)ModelWidth;
            // Taban da kapı genişliğine uyar: modelde 2 hücrelik; 1 hücrelik kapıda iki yandan yarım hücre taşıyordu (kenarda
            // duvarın altında kalıyor, köşede dışarıda kare çıkıntı gibi görünüyordu).
            foreach (var t in modelRoot.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith("_Base")) t.localScale = Vector3.Scale(t.localScale, new Vector3(ratio, 1f, 1f));
            foreach (var (name, side) in new[] { ("Arm_Left", -1f), ("Arm_Right", 1f) })
            {
                var arm = FindArm(modelRoot, name);
                if (!arm) continue;
                Shift(arm, side * 0.5f * (ratio - 1f));
                var closedPosition = arm.localPosition;
                var closedScale = Vector3.Scale(arm.localScale, new Vector3(ratio, 1f, 1f));
                arm.localScale = closedScale;
                var openShift = side * (ratio / 2f) * (1f - OpenWidth);
                var openPosition = closedPosition + arm.parent.InverseTransformVector(transform.TransformVector(new Vector3(openShift, 0f, 0f)));
                var openScale = Vector3.Scale(closedScale, new Vector3(OpenWidth, 1f, 1f));
                doors.Add((arm, closedPosition, closedScale, openPosition, openScale));
            }
        }

        // Kapı çevresindeki neon hale: kapı açıklığı boyunca uzanan yumuşak parıltı (seçili blok kapının rengindeyse).
        void BuildHalo(Color color, int width)
        {
            gateColor = color;
            var sprite = UiArt.Glow(320, 120, 30f, 40f, Color.white);
            halo = SceneArt.Place(sprite, "Neon Halo", transform, new Vector3(0f, ModelInset - BoardView.FrameThickness / 2f, 0.12f));   // zemin karolarının önünde (içe bakan yarısı da görünür), kolların arkasında
            var size = sprite.bounds.size;
            halo.transform.localScale = new Vector3((width + 1.1f) / size.x, (BoardView.FrameThickness + 1.1f) / size.y, 1f);
            halo.sortingOrder = 3;
            halo.enabled = false;
        }

        void Shift(Transform t, float dx) =>
            t.position += transform.TransformVector(new Vector3(dx, 0f, 0f));

        /// <summary>Door.Open: geçen her blok için sayaç artar; ilk açılışta kapaklar direklere çekilir.</summary>
        public void Open()
        {
            if (openCount++ == 0) SlideTo(1f);
        }

        /// <summary>Door.Close: son blok geçince kapaklar kapanır.</summary>
        public void Close()
        {
            if (openCount > 0 && --openCount == 0) SlideTo(0f);
        }

        /// <summary>Door.SetColorGlow: seçilen blok bu kapının renginde ise parlar.</summary>
        public void SetGlow(bool on)
        {
            glowFrom = glow;
            glowTo = on ? BoardTuning.DoorGlow : 0f;
            glowDuration = on ? BoardTuning.DoorGlowOnSeconds : GlowOffSeconds;
            glowTime = 0f;
        }

        /// <summary>Yanlış renk veya dar kapı için kısa sarsıntı.</summary>
        public void Shake() => shakeTime = 0f;

        void SlideTo(float target)
        {
            slideFrom = slide;
            slideTo = target;
            slideTime = 0f;
        }

        void Update()
        {
            var dt = Time.deltaTime;
            if (slideTime >= 0f)
            {
                slideTime += dt;
                var k = Tween.Evaluate(Tween.Ease.OutExpo, slideTime / BoardTuning.DoorSlideSeconds);
                slide = Mathf.LerpUnclamped(slideFrom, slideTo, k);
                if (slideTime >= BoardTuning.DoorSlideSeconds) slideTime = -1f;
                foreach (var d in doors)
                {
                    d.door.localPosition = Vector3.LerpUnclamped(d.closedPosition, d.openPosition, slide);
                    d.door.localScale = Vector3.LerpUnclamped(d.closedScale, d.openScale, slide);
                }
            }
            if (glowTime >= 0f)
            {
                glowTime += dt;
                glow = Mathf.Lerp(glowFrom, glowTo, Tween.Evaluate(Tween.Ease.InOutSine, glowTime / glowDuration));
                if (glowTime >= glowDuration) glowTime = -1f;
            }
            if (glow > 0.0001f || (halo && halo.enabled))
            {
                // Kapının rengi değişmez: kapının çevresinde kendi renginde yumuşak hale nefes alır gibi parlayıp söner,
                // ışık şeritleri de aynı ritimle hafifçe parlar.
                breathTime += dt;
                var g = Mathf.Clamp01(glow / BoardTuning.DoorGlow);
                var breath = 0.6f + 0.4f * (0.5f + 0.5f * Mathf.Sin(breathTime * Mathf.PI * 2f / BreathSeconds));
                for (var i = 0; i < glowMaterials.Count; i++)
                    if (glowIsLight[i]) glowMaterials[i].SetColor(ShaderIds.BaseColor, glowBase[i] * (1f + LightBoost * g * breath));
                if (halo)
                {
                    var c = gateColor;
                    c.a = g * HaloAlpha * breath;
                    halo.color = c;
                    halo.enabled = g > 0.001f;
                }
            }
            else breathTime = 0f;

            var offset = Vector3.zero;
            if (shakeTime >= 0f)
            {
                shakeTime += dt;
                offset = Vector3.right * Tween.Shake(Vector3.one * 0.08f, BoardTuning.ShakeSeconds, BoardTuning.ShakeVibrato, shakeTime).x;
                if (shakeTime >= BoardTuning.ShakeSeconds) shakeTime = -1f;
            }
            modelRoot.localPosition = new Vector3(offset.x, ModelInset - BoardView.FrameThickness / 2f, ModelKit.GroundOffset);
        }
    }
}
