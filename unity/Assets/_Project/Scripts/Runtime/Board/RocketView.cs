using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Bloğun ve roketin görünümü ile tahtadaki fizik gövdesi. Tahtada blok, her hücresi için bir 1×1 blok modelinden kurulur;
    /// aynı bloktaki komşu hücreler arasında bağlantı parçaları açıktır. Kapıdan geçince her hücre ayrı parçalı roket
    /// modeline dönüşür ve burun, kanatlar ve motor gövdenin içinden açılır (Unfold).
    /// Her hücre ayrı bir yuvarlak köşeli kutu çarpıştırıcısıdır (hücrenin %90'ı, köşe yarıçapı
    /// 0,2 hücre); gövde dururken kinematik, sürüklenirken dinamiktir. Ölçek animasyonları (atış itmesi, iniş ezilmesi,
    /// sarsıntı) model kabına, konum ve dönüş kök nesneye uygulanır.
    /// </summary>
    public sealed partial class RocketView : MonoBehaviour
    {
        public const float ColliderSize = BeltPath.RocketWidth;   // 1×1 kutu + 0,4 kenar yarıçapı, 2 birimlik hücrede = 0,9 hücre
        public const float ColliderRadius = 0.2f;
        /// <summary>Roket modelinde namlunun gövde merkezine uzaklığı (Muzzle, burun ucunun önü).</summary>
        public const float MuzzleDistance = 0.91f;
        const float CounterDepth = -0.34f;                         // sayaç yazısı modelin üst yüzünün hemen önünde

        public int Index { get; private set; }
        public RocketSpec Spec { get; private set; }
        public Rigidbody2D Body { get; private set; }

        static readonly int ClipPlaneId = ShaderIds.ClipPlane;
        static readonly Vector4 NoClip = new(0f, 0f, 0f, 100000f);
        static readonly string[] UnfoldParts = { "Nose", "Fin_Left", "Fin_Right", "Engine" };
        Transform model;
        Renderer[] renderers;
        readonly System.Collections.Generic.List<Renderer> tinted = new();
        Color tint;
        MaterialPropertyBlock block;
        UnityEngine.UI.Text counter;
        internal const float CounterScale = 0.0062f;   // dünya birimi / UI pikseli
        Canvas counterCanvas;
        Vector4 clipPlane = NoClip;

        // Üst üste binebilen ölçek animasyonları; her biri temel ölçeğe eklenir ya da çarpılır.
        float punchTime = -1f, punchDuration;
        Vector3 punchAmount;
        int punchVibrato;
        float punchElasticity;
        float shakeTime = -1f, shakeDuration;
        Vector3 shakeStrength;
        int shakeVibrato;
        float squashTime = -1f;

        // Roket parçalarının açılması
        readonly System.Collections.Generic.List<(Transform part, Vector3 position, Vector3 scale)> parts = new();
        ParticleSystem flame;
        const float EngineDistance = 0.62f;       // motor ağzının gövde merkezine uzaklığı (yerel −y)
        float unfoldTime = -1f, unfoldDuration;

        /// <param name="asRocket">true: kapıdan çıkmış tek hücrelik roket; false: tahtadaki blok.</param>
        public void Build(int index, RocketSpec spec, PrototypePalette palette, ModelLibrary models, bool asRocket = false)
        {
            Index = index;
            Spec = spec;
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            tint = spec.isBlocker ? new Color(0.45f, 0.47f, 0.52f) : ModelKit.ColorOf(palette, spec.color);

            if (asRocket)
            {
                var root = ModelKit.Spawn(models.rocket, model, Vector3.zero, Quaternion.identity, tint, tinted);
                body = ModelKit.Find(root, "Body");
                if (body) bodyScale = body.localScale;
                foreach (var name in UnfoldParts)
                {
                    var part = ModelKit.Find(root, name);
                    if (part) parts.Add((part, part.localPosition, part.localScale));
                }
                // Ortadaki krem arma roketlerde gösterilmez.
                var badge = ModelKit.Find(root, "Badge");
                if (badge) badge.gameObject.SetActive(false);
                if (models.flameMaterial) flame = CreateFlame(models.flameMaterial, tint);
            }
            else
            {
                var cells = new System.Collections.Generic.HashSet<Vector2Int>(spec.LocalCells);
                // Kesintisiz blok kiti: her hücreye 1×1 gövde, komşu iki hücrenin ortasına bağlantı parçası (uzun ekseni
                // birleşme yönünde; dikey çiftlerde 90° döner). Gövde ve bağlantı aynı malzemede.
                Vector3 Center(Vector2 c) => new(c.x + 0.5f - spec.size.x / 2f, c.y + 0.5f - spec.size.y / 2f, 0f);
                foreach (var c in cells)
                {
                    ModelKit.Spawn(models.block, model, Center(c), Quaternion.identity, tint, tinted);
                    if (!models.blockConnector) continue;
                    if (cells.Contains(c + Vector2Int.right))
                        ModelKit.Spawn(models.blockConnector, model, Center(c + new Vector2(0.5f, 0f)), Quaternion.identity, tint, tinted);
                    if (cells.Contains(c + Vector2Int.up))
                        ModelKit.Spawn(models.blockConnector, model, Center(c + new Vector2(0f, 0.5f)), Quaternion.Euler(0f, 0f, 90f), tint, tinted);
                }
                if (spec.layered) BuildCore(cells, Center, palette, models);
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            if (!asRocket) BuildMechanics(models);
            // Tahtadaki bloklar sayı göstermez; mermi sayısı yalnızca kapıdan çıkıp rokete dönüşünce yazılır.
            if (spec.isBlocker || !asRocket) return;

            // Kalan mermi sayacı: her atışta bir azalır. Beyaz yazı, ince siyah kenar. Dünya uzayında
            // küçük bir uGUI tuvali: kenar ve yazı tek mesh içinde sırayla çizilir (ayrı TextMesh kopyalarında beyazın
            // kenarın arkasında kalıp yazının siyah görünmesi cihazda oluyordu). Yazı her zaman dik durur.
            var text = new GameObject("Counter", typeof(RectTransform), typeof(Canvas));
            text.transform.SetParent(model, false);
            text.transform.localPosition = new Vector3(0f, 0f, CounterDepth);
            text.transform.localScale = Vector3.one * CounterScale;
            counterCanvas = text.GetComponent<Canvas>();
            counterCanvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)text.transform).sizeDelta = new Vector2(160f, 100f);
            counter = NewText(text.transform);
            SetCount(spec.capacity);
        }

        // Katmanlı blok: iç katman, dış bloğun üst yüzünde duran tek parça çekirdek (CoreMesh): hücrelerin birleşiminden
        // CoreInset kadar içeride, düz üstlü, kenarları yuvarlak; blokla aynı malzemede iç renkte. Her şekilde (1x1, 2'li, L, T)
        // kesintisiz ve hücrelerle hizalı. Dış katman geçince yeni blok çekirdek boyundan tam boya büyür.
        public const float CoreScale = 0.6f, CoreHeight = 0.8f, CoreLift = -0.16f;
        const float CoreInset = 0.2f, CoreRound = 0.1f, CoreBevel = 0.08f, CoreTop = 0.13f;
        const float BlockTop = -0.275f;         // blok gövdesinin üst yüzü (model kabında, kameraya doğru −z)
        static Vector3 CoreBlockScale => new(CoreScale, CoreHeight, CoreScale);
        Transform coreRoot;
        float growTime = -1f;
        const float GrowSeconds = 0.35f;
        readonly System.Collections.Generic.List<(Transform part, Vector3 from, Vector3 to)> growParts = new();

        void BuildCore(System.Collections.Generic.HashSet<Vector2Int> cells, System.Func<Vector2, Vector3> center, PrototypePalette palette, ModelLibrary models)
        {
            var go = new GameObject("Core", typeof(MeshFilter), typeof(MeshRenderer));
            coreRoot = go.transform;
            coreRoot.SetParent(model, false);
            coreRoot.localPosition = new Vector3(0f, 0f, BlockTop - 0.002f);
            go.GetComponent<MeshFilter>().sharedMesh = CoreMesh.Build(cells, Spec.size, CoreInset, CoreRound, CoreBevel, CoreTop);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = ModelKit.BlockMaterial(ModelKit.ColorOf(palette, Spec.innerColor));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Katmanlı blokta dış katman kapıya akarken tahtada yalnız çekirdek görünür.</summary>
        public void HideOuterLayer()
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                if (!coreRoot || !r.transform.IsChildOf(coreRoot)) r.enabled = false;
        }

        /// <summary>
        /// Dış katman geçti: blok iç renkte normal blok olarak yeniden kurulur ve çekirdek boyundan hafif taşarak tam boya
        /// büyür (yükseltilmiş çekirdek tahtaya iner).
        /// </summary>
        public void BecomeInner(RocketSpec spec, PrototypePalette palette, ModelLibrary models)
        {
            if (model) Destroy(model.gameObject);
            tinted.Clear();
            features.Clear();
            coreRoot = null;
            Build(Index, spec, palette, models);
            // Yeni bloğun her parçası çekirdekteki boyundan tam boya büyür (hücre ortaları yerinde kalır).
            growParts.Clear();
            foreach (Transform part in model)
            {
                var connector = models.blockConnector && part.name == models.blockConnector.name;
                if (!connector && part.name != models.block.name) continue;   // gövde ve bağlantılar
                growParts.Add((part, Vector3.Scale(part.localScale, CoreBlockScale), part.localScale));
            }
            growTime = 0f;
        }

        /// <summary>Tahtadaki fizik gövdesini kurar: hücre başına bir yuvarlak köşeli kutu, kinematik başlar.</summary>
        public void AddBoardPhysics(PhysicsMaterial2D material)
        {
            Body = gameObject.AddComponent<Rigidbody2D>();
            Body.bodyType = RigidbodyType2D.Kinematic;
            Body.gravityScale = 0f;
            Body.mass = 1f;
            Body.linearDamping = 0f;
            Body.angularDamping = 0.05f;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation2D.None;
            Body.sharedMaterial = material;

            foreach (var cell in Spec.LocalCells)
            {
                var c = gameObject.AddComponent<BoxCollider2D>();
                c.offset = new Vector2(cell.x + 0.5f - Spec.size.x / 2f, cell.y + 0.5f - Spec.size.y / 2f);
                c.size = Vector2.one * (ColliderSize - ColliderRadius * 2f);
                c.edgeRadius = ColliderRadius;
                c.sharedMaterial = material;
            }
        }

        /// <summary>Tahtadan ayrılırken fizik gövdesi kaldırılır; yörüngede roket yalnızca animasyonla hareket eder.</summary>
        public void RemoveBoardPhysics()
        {
            HideMechanics();
            foreach (var c in GetComponents<Collider2D>()) Destroy(c);
            if (Body) Destroy(Body);
            Body = null;
        }

        public void SetCount(int value)
        {
            if (!counter) return;
            counter.text = NumberText.Of(value);
        }

        internal static UnityEngine.UI.Text NewText(Transform parent)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var t = go.AddComponent<UnityEngine.UI.Text>();
            t.font = UiArt.Font;
            t.fontSize = 60;
            t.fontStyle = UiArt.IsBoldFace ? FontStyle.Normal : FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = Color.white;
            t.raycastTarget = false;
            var outline = go.AddComponent<ThickOutline>();
            outline.distance = 3.5f;
            outline.shadow = Vector2.zero;
            return t;
        }

        /// <summary>Düzlemin 'normal' tarafında kalan kısım çizilmez (kapıdan geçen kısım görünmez olur).</summary>
        public void SetClipPlane(Vector3 normal, Vector3 pointOnPlane) =>
            ApplyClip(new Vector4(normal.x, normal.y, normal.z, Vector3.Dot(normal, pointOnPlane)));

        public void ClearClip() => ApplyClip(NoClip);

        void ApplyClip(Vector4 plane)
        {
            clipPlane = plane;
            ApplyBlock();
        }

        // Kesme düzlemi tüm parçalara uygulanır.
        void ApplyBlock()
        {
            foreach (var r in renderers)
            {
                if (r is not MeshRenderer) continue;
                block.Clear();
                block.SetVector(ClipPlaneId, clipPlane);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>Hücre uzayındaki roket merkezi; origin sol alt hücredir.</summary>
        public static Vector2 CenterOf(Vector2Int origin, Vector2Int size) => origin + (Vector2)size / 2f;

        void LateUpdate()
        {
            var dt = Time.deltaTime;
            UpdateSelection(dt);
            UpdateMechanics(dt);
            var scale = Vector3.one;

            if (squashTime >= 0f)
            {
                squashTime += dt;
                // Ezilen eksen kameraya bakan z eksenidir.
                var squash = new Vector3(OrbitTuning.LandSquash.x, OrbitTuning.LandSquash.x, OrbitTuning.LandSquash.y);
                float k;
                if (squashTime < OrbitTuning.LandSquashSeconds) k = Tween.Evaluate(Tween.Ease.OutQuad, squashTime / OrbitTuning.LandSquashSeconds);
                else if (squashTime < OrbitTuning.LandSquashSeconds + OrbitTuning.LandSettleSeconds)
                    k = 1f - Tween.Evaluate(Tween.Ease.OutQuad, (squashTime - OrbitTuning.LandSquashSeconds) / OrbitTuning.LandSettleSeconds);
                else { k = 0f; squashTime = -1f; }
                scale = Vector3.Scale(scale, Vector3.LerpUnclamped(Vector3.one, squash, k));
            }
            if (punchTime >= 0f)
            {
                punchTime += dt;
                var p = Tween.Punch(punchAmount, punchDuration, punchVibrato, punchElasticity, punchTime);
                scale += new Vector3(p.x, p.z, p.y);
                if (punchTime >= punchDuration) punchTime = -1f;
            }
            if (shakeTime >= 0f)
            {
                shakeTime += dt;
                scale += Tween.Shake(shakeStrength, shakeDuration, shakeVibrato, shakeTime);
                if (shakeTime >= shakeDuration) shakeTime = -1f;
            }
            if (growTime >= 0f && model)
            {
                // Çekirdekten tam bloğa: hafif taşmalı büyüme, yükseltilmiş çekirdek tahta seviyesine iner.
                growTime += dt;
                var g = Mathf.Clamp01(growTime / GrowSeconds);
                var over = 1f + 0.12f * Mathf.Sin(g * Mathf.PI);
                var e = Tween.Evaluate(Tween.Ease.OutQuad, g);
                foreach (var (part, from, to) in growParts) if (part) part.localScale = Vector3.Lerp(from, to, e) * over;
                if (!selected) model.localPosition = new Vector3(model.localPosition.x, model.localPosition.y, Mathf.Lerp(CoreLift, 0f, g));
                if (g >= 1f) growTime = -1f;
            }
            if (model) model.localScale = scale * selectScale;

            UpdateMorph(dt);
            if (unfoldTime >= 0f)
            {
                unfoldTime += dt;
                var k = Tween.Evaluate(Tween.Ease.OutQuad, unfoldTime / unfoldDuration);
                foreach (var (part, position, partScale) in parts)
                {
                    part.localPosition = Vector3.LerpUnclamped(Vector3.zero, position, k);
                    part.localScale = Vector3.LerpUnclamped(partScale * 0.01f, partScale, k);
                }
                if (unfoldTime >= unfoldDuration) unfoldTime = -1f;
            }

            if (!counter) return;
            counter.transform.parent.rotation = Quaternion.identity;
            var cp = counter.transform.position;
            var visible = Vector3.Dot(clipPlane, new Vector4(cp.x, cp.y, cp.z, 0f)) <= clipPlane.w && transform.localScale.x > 0.05f;
            if (counterCanvas.enabled != visible) counterCanvas.enabled = visible;
        }
    }
}
