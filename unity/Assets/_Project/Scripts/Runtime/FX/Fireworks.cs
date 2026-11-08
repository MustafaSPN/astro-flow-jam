using System.Collections;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Bölüm sonu havai fişekleri: yörüngenin üstünde, altında ve iki yanında sırayla patlayan renkli kıvılcım halkaları.
    /// Kıvılcımlar ışıltılı iz bırakır, hafif yerçekimiyle süzülür ve söner; renkler HDR olduğu için bloom ile parlar.
    /// </summary>
    public static class Fireworks
    {
        static readonly int[] Colors = { 0xFF3DF2, 0x39F3FF, 0x7CFF4F, 0xFFE84A, 0xFF8A3D, 0xA66BFF, 0xFF4B5C };
        const float Intensity = 1.25f;   // bloom eşiğini biraz aşar; renk beyaza doymaz

        /// <summary>Yörüngenin çevresinde 'seconds' boyunca aralıklarla patlar. center: yörünge merkezi, half: dış yarı genişlik.</summary>
        public static IEnumerator Show(Vector3 center, float half, float seconds, Material material, Transform parent)
        {
            var rng = new System.Random();
            var t = 0f;
            var side = 0;
            while (t < seconds)
            {
                // Sırayla üst, alt, sol, sağ; her birinde rastgele bir nokta (yanlar ekrandan taşmasın diye rayın üstüne yakın).
                var r = (float)rng.NextDouble() * 2f - 1f;
                var offset = (side++ % 4) switch
                {
                    0 => new Vector3(r * half * 0.8f, half + 0.9f + (float)rng.NextDouble() * 1.2f, 0f),
                    1 => new Vector3(r * half * 0.8f, -half - 0.9f - (float)rng.NextDouble() * 1.2f, 0f),
                    2 => new Vector3(-half * 0.92f, r * half * 0.9f, 0f),
                    _ => new Vector3(half * 0.92f, r * half * 0.9f, 0f),
                };
                var color = SceneArt.Hex(Colors[rng.Next(Colors.Length)]);
                Burst(center + offset + Vector3.back * 1.2f, color, material, parent);
                Haptics.Shoot();
                var wait = 0.22f + (float)rng.NextDouble() * 0.18f;
                t += wait;
                yield return new WaitForSeconds(wait);
            }
        }

        public static void Burst(Vector3 position, Color color, Material material, Transform parent)
        {
            Sfx.Play(Sfx.Fireworks);
            var go = new GameObject("Firework");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(color * Intensity, Color.Lerp(color, Color.white, 0.5f) * Intensity);
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });

            // Ekran düzleminde halka: kıvılcımlar dışa doğru eşit dağılır.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;
            shape.radiusThickness = 0f;

            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.6f;
            drag.dampen = 0f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var trails = ps.trails;
            trails.enabled = true;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.25f);
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            trails.inheritParticleColor = true;
            trails.dieWithParticles = true;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.trailMaterial = material;
            ps.Play();
        }
    }
}
