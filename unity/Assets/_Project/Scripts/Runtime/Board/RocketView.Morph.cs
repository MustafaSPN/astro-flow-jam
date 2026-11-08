using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class RocketView : MonoBehaviour
    {
        /// <summary>Burun, kanatlar ve motor gövdenin içine toplanır; roket blok hücresi gibi görünür.</summary>
        public void Collapse()
        {
            unfoldTime = -1f;
            foreach (var (part, _, scale) in parts)
            {
                part.localPosition = Vector3.zero;
                part.localScale = scale * 0.01f;
            }
        }

        // Bloktan gövdeye geçiş: blok küplüğü içine doğru erirken roket gövdesi küçük başlayıp hafif taşarak büyür.
        Transform body, morphBlock;
        Vector3 bodyScale = Vector3.one;
        float morphTime = -1f, morphDuration;
        const float MorphBodyStart = 0.72f;   // gövde bloğun içinde gizli başlar
        const float MorphBlockEnd = 0.7f;     // blok bu orana küçülünce kaybolur (gövde onu tamamen örter)

        /// <summary>Kapıdan çıkan hücre: aynı yerde blok görünümüyle başlar, 'duration' boyunca roket gövdesine dönüşür.</summary>
        public void MorphFromBlock(GameObject blockPrefab, float duration)
        {
            if (!body || !blockPrefab) return;
            morphBlock = ModelKit.Spawn(blockPrefab, model, Vector3.zero, Quaternion.identity, tint, tinted);
            morphDuration = duration;
            morphTime = 0f;
            body.localScale = bodyScale * MorphBodyStart;
        }

        void UpdateMorph(float dt)
        {
            if (morphTime < 0f) return;
            morphTime += dt;
            var p = Mathf.Clamp01(morphTime / morphDuration);
            const float c1 = 1.4f, c3 = c1 + 1f;   // hafif taşan büyüme (ease-out-back)
            var grow = 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
            body.localScale = bodyScale * Mathf.LerpUnclamped(MorphBodyStart, 1f, grow);
            if (morphBlock)
            {
                var shrink = Mathf.Lerp(1f, MorphBlockEnd, Tween.Evaluate(Tween.Ease.InOutSine, p));
                morphBlock.localScale = Vector3.one * shrink;
                if (p >= 0.85f) { Destroy(morphBlock.gameObject); morphBlock = null; }
            }
            if (p >= 1f) { body.localScale = bodyScale; morphTime = -1f; }
        }

        /// <summary>Burun, kanatlar ve motor gövdenin ortasından küçük başlayıp yerlerine açılır.</summary>
        public void Unfold(float duration)
        {
            unfoldDuration = duration;
            unfoldTime = 0f;
            foreach (var (part, _, scale) in parts)
            {
                part.localPosition = Vector3.zero;
                part.localScale = scale * 0.01f;
            }
        }

        /// <summary>Motor alevi: uçarken ve yörüngede dönerken yanar, slota inince söner.</summary>
        public void SetFlame(bool on)
        {
            if (!flame) return;
            var emission = flame.emission;
            emission.enabled = on;
            if (on && !flame.isPlaying) flame.Play();
        }

        // Motor ağzından geriye (yerel −y) doğru püsküren, dünya uzayında iz bırakan kısa ömürlü parçacıklar.
        ParticleSystem CreateFlame(Material material, Color color)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(model, false);
            go.transform.localPosition = new Vector3(0f, -EngineDistance, 0f);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.back);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.42f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.rateOverTime = 70f;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.08f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            // Alev roketin kendi renginde: motor ağzında açık ton, uçlara doğru koyulaşıp söner.
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.6f), 0f), new GradientColorKey(color, 0.35f), new GradientColorKey(color * 0.55f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// <summary>Namlunun dünya konumu: roket modelinin burun yönünde (yerel +y).</summary>
        public Vector3 MuzzlePosition => transform.position + transform.up * (MuzzleDistance * transform.lossyScale.x);
    }
}
