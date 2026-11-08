using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Gizli resmin önündeki ödül kapısı (AFJ_Simple_Reward_Door: iki sürgülü kapak, kırık ek yeri ve ortada manyetik toka
    /// kilit; eski modellerde çerçeve de olabilir). Katmanlar
    /// kameraya doğru: yörünge tabanı → gizli resim → kapı → kilit → pikseller. Tüm pikseller bitince son roket kilide
    /// çarpar: küçük bir patlama, kilit dönerek ekrandan fırlar, kapaklar yanlara kayarak çerçevenin altına girer
    /// (çerçeve dışına taşan kısım kesme düzlemiyle gizlenir).
    /// </summary>
    public sealed class RewardDoor : MonoBehaviour
    {
        // Ölçüler modelden okunur (çerçeve dış genişliği, kapak genişliği); farklı boyutta bir kapı da oturur.
        const float DepthScale = 0.5f;          // derinlik yarıya basılır: kilit piksellerin arkasında kalsın
        const float OpenSeconds = 0.9f;
        static readonly string[] EnergyMaterials = { "SpaceLock_Energy", "SimpleLock_Cyan" };
        static readonly string[] LockNames = { "Reward_Space_Padlock", "Reward_Lock", "Simple_Padlock", "Padlock" };

        // Tasarımdaki tonlar (FBX'ten açık gelen renkler yerine): koyu lacivert gövde, lacivert zırh, parlak metal kenar.
        static readonly (string name, int color, float smoothness)[] RecolorTable =
        {
            ("SpaceLock_Body", 0x0A1328, 0.7f),
            ("SpaceLock_Panel", 0x17264A, 0.75f),
            ("Lock_Shackle_Metal", 0x7F8FB0, 0.95f),
            ("Lock_Dark", 0x060A16, 0.6f),
            ("SimpleLock_Metal", 0x8C9CBE, 0.95f),
            ("SimpleDoor_Panel", 0x46557A, 0.92f),   // kapak: açık, parlak metal
            ("SimpleDoor_Navy", 0x0A1228, 0.7f),
            // Manyetik kapı V2 (Blender'daki doğrusal renklerin sRGB karşılığı): arduvaz kapaklar, koyu ek yeri, lacivert toka.
            ("AFJ_MagneticDoor_Slate", 0x465675, 0.9f),
            ("AFJ_MagneticDoor_Seam", 0x18233A, 0.6f),
            ("AFJ_MagneticLock_Navy", 0x182640, 0.75f),
            ("AFJ_MagneticLock_Inset", 0x102139, 0.6f),
            ("AFJ_MagneticLock_Tabs", 0x627597, 0.9f),
        };

        static readonly int ClipPlaneId = ShaderIds.ClipPlane;

        static readonly Vector3 LockOffset = new(0f, -0.1f, -0.38f);   // eski kapıdaki yeri (Blender z 0,38 → sahne −z)
        const float LockScale = 0.85f;         // eski kilitle aynı boy 0,64; manyetik toka kilit biraz daha büyük
        const float SeamHalfWidth = 0.045f;    // birleşme neonunun her kapaktaki yarısı (model birimi)
        const float NeonBoost = 2.2f;

        Transform root, doorLeft, doorRight, lockPart;
        Vector3 leftClosed, rightClosed;
        float scale, slideDistance;
        float openTime = -1f;
        Material lockNeon;
        readonly List<Renderer> leftRenderers = new(), rightRenderers = new();
        MaterialPropertyBlock block;

        /// <summary>Kilidin dünya konumu (roketin çarpacağı nokta).</summary>
        public Vector3 LockPoint => lockPart ? lockPart.position : transform.position;

        /// <summary>Kapıyı yerleştirir. frameWidth: çerçevenin dış genişliği (dünya); kapı bu yerin merkezindedir.</summary>
        public void Build(GameObject prefab, GameObject padlock, Material neon, float frameWidth)
        {
            lockNeon = neon;
            root = new GameObject("Door Scale").transform;
            root.SetParent(transform, false);
            var model = ModelKit.Spawn(prefab, root, Vector3.zero, Quaternion.identity, Color.white);

            // Model birimiyle ölç: çerçeve (yoksa tüm model) dış genişliği ve sol kapağın genişliği.
            // Çerçevesiz kapıda dış ölçü tüm modeldir. Adlar önekle aranır (Blender ".002" gibi ek koyabilir).
            var frameBounds = Bounds(FindPrefix(model, "Door_Frame") ?? model);
            var leftBounds = Bounds(FindPrefix(model, "Door_Left") ?? model);
            var modelFrame = Mathf.Max(frameBounds.size.x, 0.01f);
            scale = frameWidth / modelFrame;
            slideDistance = leftBounds.size.x * scale;
            var inner = (transform.position.x - leftBounds.min.x) * scale;   // kapağın dış kenarı = çerçevenin iç kenarı
            root.localScale = new Vector3(scale, scale, scale * DepthScale);

            var copies = new Dictionary<Material, Material>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var m = 0; m < mats.Length; m++)
                {
                    if (!mats[m]) continue;
                    if (System.Array.Exists(EnergyMaterials, e => mats[m].name.StartsWith(e))) { mats[m] = neon; continue; }
                    foreach (var (name, color, smoothness) in RecolorTable)
                    {
                        if (!mats[m].name.StartsWith(name)) continue;
                        if (!copies.TryGetValue(mats[m], out var copy))
                        {
                            copy = new Material(mats[m]);
                            copy.SetColor(ShaderIds.BaseColor, SceneArt.Hex(color));
                            copy.SetFloat(ShaderIds.Smoothness, smoothness);
                            copies[mats[m]] = copy;
                        }
                        mats[m] = copy;
                        break;
                    }
                }
                r.sharedMaterials = mats;
            }

            doorLeft = FindPrefix(model, "Door_Left");
            doorRight = FindPrefix(model, "Door_Right");
            foreach (var name in LockNames)
                if (!lockPart) lockPart = FindPrefix(model, name);
            // Uzay kilidi (eski kapının kilidi): modeldeki sade kilidin yerine, kapakların önünde.
            if (padlock)
            {
                if (lockPart) lockPart.gameObject.SetActive(false);
                lockPart = ModelKit.Spawn(padlock, root, LockOffset, Quaternion.identity, Color.white);
                lockPart.localScale = Vector3.one * LockScale;
                Recolor(lockPart, neon);
            }

            // Kapak neonları: küçük girintiler (birleşme yerinin metal şeritleri modelde hazır gelir).
            // Kapakla birlikte kayar ve kesilir; bu yüzden kesme düzlemli malzemenin HDR parlak kopyasıdır.
            var panelNeon = ClippableNeon(doorLeft);
            var panelMaterial = doorLeft ? doorLeft.GetComponentInChildren<Renderer>().sharedMaterial : null;
            if (panelNeon && panelMaterial)
            {
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Small_Panel_Inset") && t.TryGetComponent<Renderer>(out var inset))
                    {
                        var mats = inset.sharedMaterials;
                        for (var m = 0; m < mats.Length; m++) mats[m] = panelNeon;
                        inset.sharedMaterials = mats;
                    }
            }
            if (doorLeft) { leftClosed = doorLeft.position; leftRenderers.AddRange(doorLeft.GetComponentsInChildren<Renderer>(true)); }
            if (doorRight) { rightClosed = doorRight.position; rightRenderers.AddRange(doorRight.GetComponentsInChildren<Renderer>(true)); }
            block = new MaterialPropertyBlock();

            // Kapaklar çerçevenin iç kenarından dışarı çizilmez (açılırken çerçevenin altına girer gibi görünür).
            Clip(leftRenderers, Vector3.left, transform.position + Vector3.left * inner);
            Clip(rightRenderers, Vector3.right, transform.position + Vector3.right * inner);
        }

        // Kapak malzemesinin (kesme düzlemli) HDR camgöbeği kopyası.
        Material ClippableNeon(Transform panel)
        {
            if (!panel) return null;
            var r = panel.GetComponentInChildren<Renderer>();
            if (!r || !r.sharedMaterial) return null;
            var m = new Material(r.sharedMaterial) { name = "Door Neon" };
            m.SetColor(ShaderIds.BaseColor, SceneArt.Cyan * NeonBoost);
            m.SetFloat(ShaderIds.Smoothness, 0f);
            return m;
        }

        // Kilidin enerji parçaları neon, diğerleri kapı tonlarında.
        void Recolor(Transform t, Material neon)
        {
            var copies = new Dictionary<Material, Material>();
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var m = 0; m < mats.Length; m++)
                {
                    if (!mats[m]) continue;
                    if (System.Array.Exists(EnergyMaterials, e => mats[m].name.StartsWith(e))) { mats[m] = neon; continue; }
                    foreach (var (name, color, smoothness) in RecolorTable)
                    {
                        if (!mats[m].name.StartsWith(name)) continue;
                        if (!copies.TryGetValue(mats[m], out var copy))
                        {
                            copy = new Material(mats[m]);
                            copy.SetColor(ShaderIds.BaseColor, SceneArt.Hex(color));
                            copy.SetFloat(ShaderIds.Smoothness, smoothness);
                            copies[mats[m]] = copy;
                        }
                        mats[m] = copy;
                        break;
                    }
                }
                r.sharedMaterials = mats;
            }
        }

        static Transform FindPrefix(Transform root, string prefix)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == prefix || t.name.StartsWith(prefix + ".")) return t;
            return null;
        }

        // Alt nesnelerin dünya sınırları (ölçeklenmeden önce çağrılır: model birimi verir).
        static Bounds Bounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(t.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        void Clip(List<Renderer> renderers, Vector3 normal, Vector3 point)
        {
            block.Clear();
            block.SetVector(ClipPlaneId, new Vector4(normal.x, normal.y, normal.z, Vector3.Dot(normal, point)));
            foreach (var r in renderers) r.SetPropertyBlock(block);
        }

        /// <summary>
        /// Kilit vurulur: kilit patlar (asteroit gibi: neon ve beyaz kıvılcım, lacivert/metal/neon kırıklar vuruş yönüne
        /// biraz daha çok savrulur) ve kapaklar açılır.
        /// </summary>
        public void Break(Vector3 hitDirection, Material sparks = null)
        {
            if (lockPart)
            {
                var bounds = new Bounds(lockPart.position, Vector3.zero);
                foreach (var r in lockPart.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                var size = Mathf.Max(bounds.extents.x, bounds.extents.y);
                var away = ((Vector2)hitDirection).sqrMagnitude > 1e-4f ? ((Vector2)hitDirection).normalized : Vector2.up;
                var at = bounds.center + new Vector3(0f, 0f, -0.2f);
                var parent = transform.parent ? transform.parent : transform;   // kapının basık derinlik ölçeği parçaları bozmasın
                if (sparks)
                {
                    Explosion(at, sparks, SceneArt.Cyan, parent, 1.2f);
                    Explosion(at, sparks, Color.white, parent, 0.7f);
                }
                var mats = new[]
                {
                    ModelKit.Lit("AFJ_MagneticLock_Navy", SceneArt.Hex(0x182640), 0.75f),
                    ModelKit.Lit("AFJ_MagneticLock_Tabs", SceneArt.Hex(0x627597), 0.9f),
                    lockNeon ? lockNeon : ModelKit.Lit("Feature_Cyan", SceneArt.Cyan),
                };
                // Kırıklar vuruş yönünde biraz ileride doğar: dışa savrulurken çoğu ileri gider.
                ShatterFx.Create(parent).Shards(at + (Vector3)(away * size * 0.3f), size * 0.6f, 22, mats, size * 0.22f, 9f, 0.75f);
                Destroy(lockPart.gameObject);
                lockPart = null;
            }
            openTime = 0f;
        }

        void Update()
        {
            var dt = Time.deltaTime;
            if (openTime >= 0f)
            {
                openTime += dt;
                var k = Tween.Evaluate(Tween.Ease.InOutSine, openTime / OpenSeconds);
                var shift = slideDistance * k;
                if (doorLeft) doorLeft.position = leftClosed + Vector3.left * shift;
                if (doorRight) doorRight.position = rightClosed + Vector3.right * shift;
                if (openTime >= OpenSeconds) openTime = -1f;
            }
        }

        /// <summary>Kilit çarpmasında küçük patlama: parlak kıvılcım ve duman benzeri hızlı parçacık patlaması.</summary>
        public static void Explosion(Vector3 position, Material material, Color color, Transform parent, float scale = 1f)
        {
            var go = new GameObject("Lock Explosion");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 9f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * scale, 0.6f * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, color * 2.2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 46) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f * scale;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.4f), new GradientColorKey(color * 0.5f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            ps.Play();
        }
    }
}
