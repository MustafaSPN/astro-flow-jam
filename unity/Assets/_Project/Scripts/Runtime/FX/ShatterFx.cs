using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Patlamada savrulan parçalar (asteroit, blok kilidi, gizli resim kilidi): her parça dışa ve kameraya doğru savrulur,
    /// rastgele eksende döner, hızı söner ve küçülerek kaybolur. Parçalar bu nesnenin altındadır; patlayan nesne hemen
    /// yok edilebilir, efekt kendi kendine biter.
    /// </summary>
    public sealed class ShatterFx : MonoBehaviour
    {
        const float Drag = 2.5f;

        struct Chunk
        {
            public Transform t;
            public Vector3 velocity, axis, scale;
            public float spin, life;
        }

        readonly List<Chunk> chunks = new();
        float time;
        System.Random rng;

        public static ShatterFx Create(Transform parent)
        {
            var fx = new GameObject("Shatter").AddComponent<ShatterFx>();
            fx.transform.SetParent(parent, false);
            fx.rng = new System.Random(fx.GetInstanceID());
            return fx;
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>Hazır bir parçayı (dünya konumunda) savurur: center'dan dışa, speed hızında.</summary>
        public void Throw(Transform chunk, Vector3 center, float speed, float life)
        {
            chunk.SetParent(transform, true);
            var dir = (Vector2)(chunk.position - center);
            if (dir.sqrMagnitude < 1e-4f) dir = Random.insideUnitCircle;
            dir = (dir.normalized + Random.insideUnitCircle * 0.6f).normalized;
            chunks.Add(new Chunk
            {
                t = chunk, scale = chunk.localScale, axis = Random.onUnitSphere, spin = R(360f, 900f), life = life * R(0.75f, 1.25f),
                velocity = new Vector3(dir.x, dir.y, 0f) * speed * R(0.6f, 1.2f) + Vector3.back * speed * R(0.3f, 0.7f),
            });
        }

        /// <summary>
        /// Küçük yüzeyli kırık parçalar (kutu) center çevresinde, radius içinde: malzemeler sırayla dağıtılır.
        /// size parçanın kenarı, speed savrulma hızı (dünya birimi).
        /// </summary>
        public void Shards(Vector3 center, float radius, int count, Material[] materials, float size, float speed, float life = 0.6f)
        {
            for (var i = 0; i < count; i++)
            {
                // CreatePrimitive 3B çarpıştırıcı ekler (cihazda 3B fizik yok); Primitives çarpıştırıcısız kurar.
                var go = Primitives.Create(PrimitiveType.Cube, "Shard", null, materials[i % materials.Length], Vector3.zero, Vector3.one);
                go.transform.position = center + (Vector3)(Random.insideUnitCircle * radius);
                go.transform.rotation = Random.rotation;
                go.transform.localScale = new Vector3(R(0.6f, 1.2f), R(0.4f, 0.9f), R(0.3f, 0.7f)) * size;
                Throw(go.transform, center, speed, life);
            }
        }

        void Update()
        {
            var dt = Time.deltaTime;
            time += dt;
            var damp = Mathf.Exp(-Drag * time);
            var alive = false;
            foreach (var c in chunks)
            {
                if (!c.t) continue;
                var k = Mathf.Clamp01(time / c.life);
                c.t.position += c.velocity * (damp * dt);
                c.t.rotation = Quaternion.AngleAxis(c.spin * dt, c.axis) * c.t.rotation;
                c.t.localScale = c.scale * (1f - k * k);
                alive |= k < 1f;
            }
            if (!alive && time > 0.2f) Destroy(gameObject);
        }
    }
}
