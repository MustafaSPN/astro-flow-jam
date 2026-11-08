using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Yörüngenin görünümü ve dünya uzayındaki yolu. Geometri <see cref="BeltPath"/>'tedir (seviye simülasyonu da aynısını
    /// kullanır). Görsel AFJ_Orbit_With_Depot modelidir: kare yörünge, sol altta kartuş deposu, altta fırlatma rampası.
    /// Yol ortasındaki ışıklar neon renkte, çevrelerine hale saçarak roketlerden yavaş, yol boyunca döner. Altta bekleme yerleri koyu bir
    /// panelde, kesik çizgili kutular olarak durur.
    /// </summary>
    public sealed class ConveyorTrack : MonoBehaviour
    {
        const float PanelMargin = 0.22f;
        const float PanelHeight = 1.32f;
        const float SlotSize = 0.98f;
        const float OrbitDepth = 0.75f;         // model kökü: taban üstü 0,72, yol kanalı 0,68 — resim (0,70) ikisinin arasında, ray ve yolun altında
        const float PanelDepth = 0.4f;
        const float DecorSpeed = 0.22f;         // neon cisimler, roket hızının bu oranında döner

        static readonly int[] NeonColors = { 0x39F3FF, 0xFF3DF2, 0x7CFF4F, 0xFFE84A, 0xFF8A3D, 0xA66BFF };
        // Yol ortasındaki, yola paralel ışıklar neon renkte döner; yola dik kelepçeler modeldeki gibi metalik ve sabittir.
        static readonly string[] DecorPrefixes = { "Navigation_Light" };
        const float GlowLength = 0.75f, GlowWidth = 0.36f;   // ışık halesi (dünya birimi, model ölçeğiyle çarpılır)
        const string LightsMaterial = "Orbit_Lights";
        public const float NeonIntensity = 1.7f;    // HDR çarpanı: bloom eşiğini aşar, ışık saçar

        // Yol: koyu metalik (koyu lacivert-çelik iç kanal, parlak kenar rayları, koyu taban).
        static readonly (string name, int color, float smoothness)[] OrbitRecolor =
        {
            ("Orbit_Channel", 0x1A2038, 0.85f),
            ("Orbit_Silver", 0x4E5A80, 0.8f),
            ("Orbit_Navy", 0x0C1124, 0.6f),
        };

        BeltPath path;
        readonly List<Vector3> slots = new();
        readonly List<(Transform t, float distance, float angle0, Quaternion rotation0, float z)> decor = new();

        public float Length => path.Length;
        public float ExitDistance => path.ExitDistance;
        public float Scale => path.Scale;

        /// <summary>Bekleme slotlarının dünya konumları, soldan sağa.</summary>
        public IReadOnlyList<Vector3> SlotPositions => slots;

        /// <summary>Kartuş deposu (yörünge kapasitesi).</summary>
        public CartridgeDepot Depot { get; private set; }

        /// <summary>Kartuş sayacının dünya konumu (deponun altı).</summary>
        public Vector3 CounterPoint { get; private set; }

        /// <summary>Rampanın girişi, yükleme noktası (yeni roket kartuş alır), bırakma noktası (tur sonu), kartuş aktarım
        /// noktası (dünya, roket yüksekliğinde).</summary>
        public Vector3 RampEntry { get; private set; }
        public Vector3 RampStop { get; private set; }
        public Vector3 DropPoint { get; private set; }
        public Vector3 Exchange { get; private set; }

        /// <param name="pictureWidth">Resim alanının genişliği; bu nesnenin konumu resmin merkezidir.</param>
        public void Build(float pictureWidth, int slotCount, int capacity, PrototypePalette palette, ModelLibrary models)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            decor.Clear();
            depotFrame.Clear();
            depotSockets.Clear();
            depotTopRail = depotTopSocket = null;
            path = new BeltPath(pictureWidth);
            var k = Scale;
            var s = path.ModelScale;

            var orbit = orbitModel = ModelKit.Spawn(models.orbit, transform, new Vector3(0f, 0f, OrbitDepth), Quaternion.identity, Color.white);
            orbit.localScale = Vector3.one * s;

            // Taban plakası iç alanı tamamen örter; gizli resim en altta durduğu için gizlenir (raylar ve yol kanalı kalır).
            var basePlate = ModelKit.Find(orbit, "Orbit_Base");
            if (basePlate) basePlate.gameObject.SetActive(false);

            // Modeldeki hazır kartuşlar yalnızca yuva konumlarını verir; oynayan kartuşlar ayrı nesnelerdir.
            var socketWorld = new List<Vector3>();
            for (var i = 1; i <= OrbitDesign.DepotSockets; i++)
            {
                var c = ModelKit.Find(orbit, $"Cartridge_{i:00}");
                if (!c) continue;
                socketWorld.Add(c.position);
                c.gameObject.SetActive(false);
            }

            var neon = Neon(models.neonMaterial, NeonColor(0));
            var recolored = new Dictionary<Material, Material>();
            foreach (var r in orbit.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var m = 0; m < mats.Length; m++)
                {
                    if (!mats[m]) continue;
                    if (mats[m].name.StartsWith(LightsMaterial)) { mats[m] = neon; continue; }
                    foreach (var (name, color, smoothness) in OrbitRecolor)
                    {
                        if (!mats[m].name.StartsWith(name)) continue;
                        if (!recolored.TryGetValue(mats[m], out var copy))
                        {
                            copy = new Material(mats[m]);
                            copy.SetColor(ShaderIds.BaseColor, SceneArt.Hex(color));
                            copy.SetFloat(ShaderIds.Smoothness, smoothness);
                            recolored[mats[m]] = copy;
                        }
                        mats[m] = copy;
                        break;
                    }
                }
                r.sharedMaterials = mats;
            }
            BuildDecor(orbit, models.neonMaterial);

            var depotRoot = new GameObject("Cartridge Depot").transform;
            depotRoot.SetParent(transform, false);
            Depot = depotRoot.gameObject.AddComponent<CartridgeDepot>();
            var localSockets = new List<Vector3>();
            foreach (var w in socketWorld) localSockets.Add(depotRoot.InverseTransformPoint(w));
            Depot.Build(models.cartridge, localSockets, capacity, s, Neon(models.neonMaterial, NeonColor(0)));

            RampEntry = transform.TransformPoint(path.FromModel(OrbitDesign.RampEntry));
            RampStop = transform.TransformPoint(path.FromModel(OrbitDesign.LoadPoint));
            DropPoint = transform.TransformPoint(path.FromModel(OrbitDesign.DropPoint));
            Exchange = transform.TransformPoint((Vector3)path.FromModel(OrbitDesign.Exchange) + new Vector3(0f, 0f, -0.1f));
            var depotBottom = OrbitDesign.Depot + new Vector2(0f, -OrbitDesign.DepotSpacing * 2f - 0.6f);
            CounterPoint = transform.TransformPoint(path.FromModel(depotBottom));

            BuildSlots(slotCount);
        }

        readonly List<GameObject> slotArt = new();

        // Bekleme yerleri: kesik çizgili kutular ve onları saran koyu panel. Sayı değişince (Hangar jokeri) yeniden kurulur;
        // sıra ortalı kalır.
        void BuildSlots(int slotCount)
        {
            foreach (var go in slotArt) if (go) Destroy(go);
            slotArt.Clear();
            var k = Scale;
            slots.Clear();
            var locals = path.SlotPositions(slotCount);
            var slotSprite = SceneArt.DashedSlot(SlotSize * k);
            foreach (var local in locals)
            {
                slots.Add(transform.TransformPoint(local));
                slotArt.Add(SceneArt.Place(slotSprite, $"Slot {slots.Count - 1}", transform, (Vector3)local + new Vector3(0f, 0f, PanelDepth - 0.02f), 1).gameObject);
            }
            var left = locals[0].x - SlotSize * k / 2f - PanelMargin * k;
            var right = locals[^1].x + SlotSize * k / 2f + PanelMargin * k;
            slotArt.Add(SceneArt.Place(SceneArt.Panel(right - left, PanelHeight * k, 0.3f * k), "Slot Panel", transform,
                new Vector3((left + right) / 2f, locals[0].y, PanelDepth)).gameObject);
        }

        /// <summary>Hangar jokeri: bekleme sırasına bir yer eklenir (sıra yeniden ortalanır).</summary>
        public void AddSlot() => BuildSlots(slots.Count + 1);

        Transform orbitModel;
        readonly List<(Transform pivot, float height)> depotFrame = new();   // boyu uzayan parçalar (alt ucundan)
        readonly List<GameObject> depotSockets = new();                      // eklenen yuva çukurları
        Transform depotTopRail, depotTopSocket;
        Vector3 depotTopRailRest;

        /// <summary>
        /// Hangar jokeri: depoya bir kartuş daha. Modeldeki depo 5 yuvalık; yuva eklendikçe çerçeve (arka plaka ve yan raylar)
        /// yukarı uzar, üst ray yeni üst yuvanın üstüne kayar, yeni yuvalara çukur eklenir: kartuşlar çerçevenin içinde kalır.
        /// </summary>
        public void AddCartridge()
        {
            Depot.AddCartridge();
            FitDepotFrame(Depot.Capacity - OrbitDesign.DepotSockets);
        }

        void FitDepotFrame(int extra)
        {
            if (extra <= 0 || !orbitModel) return;
            var stepWorld = Depot.transform.TransformVector(Depot.SocketStep);
            var dir = stepWorld.normalized;
            var grow = stepWorld.magnitude * extra;
            if (depotFrame.Count == 0 && !depotTopRail) PrepareDepotFrame(dir);
            foreach (var (pivot, height) in depotFrame) pivot.localScale = new Vector3(1f, (height + grow) / height, 1f);
            if (depotTopRail) depotTopRail.position = depotTopRailRest + dir * grow;

            foreach (var go in depotSockets) Destroy(go);
            depotSockets.Clear();
            // Modeldeki en üst yuva bir kez bulunur (Destroy kare sonuna kadar beklediği için eski kopyalar seçilmesin).
            var top = depotTopSocket;
            if (!top) return;
            for (var i = 1; i <= extra; i++)
            {
                var copy = Instantiate(top.gameObject, top.parent);
                copy.transform.position = top.position + stepWorld * i;
                depotSockets.Add(copy);
            }
        }

        // Uzayacak parçalar, alt uçlarında depo yönüne (Y) hizalı bir pivotun altına alınır; pivot Y'de ölçeklenince parça
        // yalnız yukarı uzar.
        void PrepareDepotFrame(Vector3 dir)
        {
            foreach (var t in orbitModel.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Depot_Backplate") && !t.name.StartsWith("Depot_SideRail")) continue;
                var r = t.GetComponent<Renderer>();
                if (!r) continue;
                var b = r.bounds;
                var height = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z))));
                if (height <= 1e-4f) continue;
                var pivot = new GameObject(t.name + " Pivot").transform;
                pivot.SetParent(t.parent, false);
                pivot.SetPositionAndRotation(b.center - dir * (height / 2f), Quaternion.FromToRotation(Vector3.up, dir));
                pivot.localScale = Vector3.one;
                t.SetParent(pivot, true);
                depotFrame.Add((pivot, height));
            }
            depotTopSocket = TopMost(orbitModel, "Socket_", dir);
            depotTopRail = TopMost(orbitModel, "Depot_EndRail", dir);
            if (depotTopRail) depotTopRailRest = depotTopRail.position;
        }

        static Transform TopMost(Transform root, string prefix, Vector3 dir)
        {
            Transform best = null;
            var bestProj = float.MinValue;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(prefix) || !t.GetComponent<Renderer>()) continue;
                var proj = Vector3.Dot(t.GetComponent<Renderer>().bounds.center, dir);
                if (proj > bestProj) { bestProj = proj; best = t; }
            }
            return best;
        }

        // Yörünge üzerindeki ışık ve kelepçeler: neon renk alır, yol üzerindeki yerinden başlayarak döner.
        void BuildDecor(Transform orbit, Material neonSource)
        {
            var index = 0;
            foreach (var t in orbit.GetComponentsInChildren<Transform>(true))
            {
                var isDecor = false;
                foreach (var p in DecorPrefixes) isDecor |= t.name.StartsWith(p);
                if (!isDecor || !t.GetComponent<Renderer>()) continue;
                var neonColor = NeonColor(index++);
                var material = Neon(neonSource, neonColor);
                var r = t.GetComponent<Renderer>();
                var mats = r.sharedMaterials;
                for (var m = 0; m < mats.Length; m++) mats[m] = material;
                r.sharedMaterials = mats;

                var center = r.bounds.center;
                var local = (Vector2)transform.InverseTransformPoint(center);
                var distance = Nearest(local);
                var tangent = path.Tangent(distance);
                // Kökü görünür merkezle aynı yere taşınabilsin diye nesne dönen bir sarmalayıcıya alınır.
                var pivot = new GameObject(t.name + " Pivot").transform;
                pivot.SetParent(transform, false);
                pivot.position = center;
                pivot.rotation = Quaternion.identity;
                t.SetParent(pivot, true);
                // Neon hale: ışığın arkasında, yol yönünde uzanan yumuşak parıltı.
                var s = path.ModelScale;
                var glow = SceneArt.Place(GlowSprite(), "Glow", pivot, new Vector3(0f, 0f, 0.05f), 2);
                glow.color = neonColor;
                glow.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                glow.transform.localScale = new Vector3(GlowLength * s / glow.sprite.bounds.size.x, GlowWidth * s / glow.sprite.bounds.size.y, 1f);
                decor.Add((pivot, distance, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg, pivot.localRotation, pivot.localPosition.z));
            }
        }

        static Sprite glowSprite;

        static Sprite GlowSprite() => glowSprite ? glowSprite : glowSprite = UiArt.Glow(96, 48, 10f, 18f, Color.white);

        /// <summary>HDR parlaklıkta düz renk (bloom ile ışık saçar).</summary>
        public static Material Neon(Material source, Color color)
        {
            var m = new Material(source);
            m.SetColor(ShaderIds.BaseColor, color * NeonIntensity);
            return m;
        }

        static Color NeonColor(int i) => SceneArt.Hex(NeonColors[i % NeonColors.Length]);

        // Yolda verilen noktaya en yakın mesafe (örnekleyerek).
        float Nearest(Vector2 local)
        {
            var best = 0f;
            var bestD = float.MaxValue;
            const int samples = 400;
            for (var i = 0; i < samples; i++)
            {
                var d = path.Length * i / samples;
                var dist = (path.Point(d) - local).sqrMagnitude;
                if (dist < bestD) { bestD = dist; best = d; }
            }
            return best;
        }

        void Update()
        {
            if (path == null) return;
            var step = OrbitTuning.ConveyorSpeed * Scale * DecorSpeed * Time.deltaTime;
            for (var i = 0; i < decor.Count; i++)
            {
                var (t, distance, angle0, rotation0, z) = decor[i];
                distance = Mathf.Repeat(distance + step, LoopLength);
                decor[i] = (t, distance, angle0, rotation0, z);
                var p = LoopPoint(distance, out var tangent);
                t.localPosition = new Vector3(p.x, p.y, z);
                t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - angle0) * rotation0;
            }
        }

        // Işıklar için kapalı döngü: roket yolu istasyonda açıktır (durma noktası → katılma noktası arası); ışıklar bu
        // boşluğu da düz geçer, sıçramaz.
        float LoopLength => path.Length + Gap.magnitude;

        Vector2 Gap => path.Point(0f) - path.Point(path.Length);

        Vector2 LoopPoint(float d, out Vector2 tangent)
        {
            if (d <= path.Length)
            {
                tangent = path.Tangent(d);
                return path.Point(d);
            }
            var gap = Gap;
            tangent = gap.normalized;
            return path.Point(path.Length) + gap * Mathf.Clamp01((d - path.Length) / Mathf.Max(gap.magnitude, 1e-4f));
        }

        /// <summary>Rampa sırasındaki i'inci yerin dünya konumu (0 = rampaya en yakın).</summary>
        public Vector3 QueueSpot(int i)
        {
            var step = Mathf.Min(i, OrbitDesign.QueueSpots - 1) + Mathf.Max(0, i - OrbitDesign.QueueSpots + 1) * 0.15f;
            return transform.TransformPoint(path.FromModel(OrbitDesign.QueueFront + Vector2.up * (OrbitDesign.QueueStep * step)));
        }

        /// <summary>Yol üzerindeki dünya konumu ve yönü.</summary>
        public Vector3 Evaluate(float s, out Vector3 tangent)
        {
            tangent = transform.TransformDirection(path.Tangent(s));
            return transform.TransformPoint(path.Point(s));
        }
    }
}
