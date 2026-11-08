using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Asteroit engelinin görünümü (Blender: AsteroidObstacles). Tahtanın içinde, kapladığı hücrelerde katı 2B kutular;
    /// bloklar çarpıp durur. Sayaçlı asteroitte üstte sayaç durur; her çıkışta azalıp sallanır, sıfırda asteroit patlar:
    /// kaya bir an şişip kaybolur, turuncu alev ve lila kristal kıvılcımları saçılır, kaya parçaları dönerek dışa savrulup
    /// küçülür; kutular kalkar.
    /// </summary>
    public sealed class AsteroidView : MonoBehaviour
    {
        const float BaseDepth = 0.26f;          // modelin tabanı (Blender z 0) blok tabanı ve zemin seviyesinde
        const float CounterDepth = -0.78f;      // sayaç kayanın tepesinin (Blender z 0,69) önünde
        const float SwellSeconds = 0.08f;       // patlamadan önce kayanın şiştiği süre
        const int ChunksPerCell = 4;
        static readonly Color Fire = new(1f, 0.55f, 0.2f);
        // Blender malzemeleri doğrusal renkte; oyunda sRGB karşılıkları (kaya koyu kurşuni-lacivert, kristal lila neon).
        static readonly Color Rock = new(0.29f, 0.34f, 0.43f), Crystal = new(0.72f, 0.5f, 0.92f);

        Transform model;
        UnityEngine.UI.Text counter;
        GameObject counterRoot;
        float breakTime = -1f, punchTime = -1f;
        Vector3 baseScale;
        ModelLibrary library;
        readonly System.Collections.Generic.List<Vector2Int> cells = new();

        public void Build(AsteroidSpec spec, ModelLibrary models)
        {
            library = models;
            var prefab = models ? models.Asteroid(spec.shape) : null;
            var pivot = new Vector3(spec.origin.x + 0.5f, spec.origin.y + 0.5f, BaseDepth);
            if (prefab)
            {
                model = ModelKit.Spawn(prefab, transform, pivot, Quaternion.Euler(0f, 0f, 90f * spec.rotation), Color.white);
                baseScale = model.localScale;
                Paint(model, models);
            }

            // Katı hücreler: bloklar çarpar, içine giremez.
            cells.AddRange(spec.Cells);
            foreach (var c in cells)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.offset = new Vector2(c.x + 0.5f, c.y + 0.5f);
                box.size = Vector2.one;
            }

            if (spec.count <= 0) return;
            // Sayaç: 1x2'de tam ortada (iki hücrenin arasında), L ve T'de ağırlık merkezine en yakın hücrede.
            var centroid = Vector2.zero;
            foreach (var c in cells) centroid += (Vector2)c;
            centroid /= cells.Count;
            var best = cells[0];
            foreach (var c in cells) if (((Vector2)c - centroid).sqrMagnitude < ((Vector2)best - centroid).sqrMagnitude) best = c;
            counterRoot = new GameObject("Asteroid Counter", typeof(RectTransform), typeof(Canvas));
            counterRoot.transform.SetParent(transform, false);
            var at = spec.shape == AsteroidShape.Double ? centroid : (Vector2)best;
            counterRoot.transform.localPosition = new Vector3(at.x + 0.5f, at.y + 0.5f, CounterDepth);
            counterRoot.transform.localScale = Vector3.one * RocketView.CounterScale * 1.9f;
            var canvas = counterRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 8;
            ((RectTransform)counterRoot.transform).sizeDelta = new Vector2(160f, 100f);
            counter = RocketView.NewText(counterRoot.transform);
            counter.text = spec.count.ToString();
        }

        static void Paint(Transform root, ModelLibrary models)
        {
            var crystal = models.neonMaterial ? ConveyorTrack.Neon(models.neonMaterial, Crystal) : ModelKit.Lit("Asteroid Crystal", Crystal, 0.7f);
            var rock = ModelKit.Lit("Asteroid Rock", Rock, 0.25f);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                    if (mats[i]) mats[i] = mats[i].name.StartsWith("Asteroid_Lilac") ? crystal : rock;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Sayaç azaldı (left > 0) ya da asteroit kırıldı (left &lt; 0).</summary>
        public void UpdateCount(int left)
        {
            if (left > 0)
            {
                if (counter) counter.text = left.ToString();
                punchTime = 0f;
                return;
            }
            if (breakTime >= 0f) return;
            breakTime = 0f;
            foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
            if (counterRoot) counterRoot.SetActive(false);
            Haptics.GateExit(this);
        }

        // Patlama: alev ve kristal kıvılcımları, hücre başına birkaç kaya parçası.
        void Explode()
        {
            var centroid = Vector2.zero;
            foreach (var c in cells) centroid += (Vector2)c + new Vector2(0.5f, 0.5f);
            centroid /= cells.Count;
            var parent = transform.parent ? transform.parent : transform;
            if (library && library.flameMaterial)
            {
                RewardDoor.Explosion(transform.TransformPoint(new Vector3(centroid.x, centroid.y, -0.6f)), library.flameMaterial, Fire, parent);
                foreach (var c in cells)
                    RewardDoor.Explosion(transform.TransformPoint(new Vector3(c.x + 0.5f, c.y + 0.5f, -0.7f)), library.flameMaterial, Crystal, parent);
            }
            if (!library || !library.asteroid1x1) return;
            // Kaya parçaları: küçük 1x1 asteroitler hücrelerden dışa savrulur (ShatterFx tahtada kalır, bu nesne silinir).
            var fx = ShatterFx.Create(parent);
            var center = transform.TransformPoint(new Vector3(centroid.x, centroid.y, BaseDepth - 0.3f));
            foreach (var c in cells)
                for (var i = 0; i < ChunksPerCell; i++)
                {
                    var start = new Vector3(c.x + 0.5f + Random.Range(-0.3f, 0.3f), c.y + 0.5f + Random.Range(-0.3f, 0.3f), BaseDepth - 0.3f);
                    var chunk = ModelKit.Spawn(library.asteroid1x1, transform, start, Random.rotation, Color.white);
                    Paint(chunk, library);
                    chunk.localScale *= Random.Range(0.18f, 0.34f);
                    fx.Throw(chunk, center, 5f, 0.6f);
                }
        }

        void Update()
        {
            var dt = Time.deltaTime;
            if (punchTime >= 0f && model)
            {
                punchTime += dt;
                var k = Mathf.Clamp01(punchTime / 0.25f);
                model.localScale = baseScale * (1f + 0.12f * Mathf.Sin(k * Mathf.PI));
                if (counterRoot) counterRoot.transform.localScale = Vector3.one * RocketView.CounterScale * 1.9f * (1f + 0.3f * Mathf.Sin(k * Mathf.PI));
                if (k >= 1f) punchTime = -1f;
            }
            if (breakTime < 0f) return;
            var before = breakTime;
            breakTime += dt;
            // 1) Kaya bir an şişer, 2) patlar: model kaybolur, parçalar savrulur.
            if (model && breakTime < SwellSeconds) model.localScale = baseScale * (1f + 0.25f * breakTime / SwellSeconds);
            if (before < SwellSeconds && breakTime >= SwellSeconds)
            {
                if (model) model.gameObject.SetActive(false);
                Explode();
            }
            if (breakTime >= SwellSeconds) Destroy(gameObject);
        }
    }
}
