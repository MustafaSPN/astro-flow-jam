using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Yörünge kapasitesinin görünür hali: depoda yuvalara dizili enerji kartuşları.
    /// Rampaya gelen roket en alttaki kartuşu alır, üsttekiler birer yuva aşağı kayar. Turu biten (ya da mermisi biten)
    /// roketin kartuşu deponun üstüne döner ve boş en alt yuvaya iner. Kartuşlar her karede hedef yuvalarına yumuşakça
    /// ilerler.
    /// </summary>
    public sealed class CartridgeDepot : MonoBehaviour
    {
        const float SlideSpeed = 9f;            // yuvaya iniş (birim/sn, ölçekli)
        const float ReturnSeconds = 0.45f;
        const float ReturnLift = 1.2f;

        sealed class Cartridge
        {
            public Transform transform;
            public float returnTime = -1f;      // ≥ 0 iken depoya dönüş uçuşunda
            public Vector3 returnFrom;
            public Quaternion returnRotation;
        }

        readonly List<Vector3> sockets = new();         // yerel, alttan üste
        readonly List<Cartridge> stack = new();         // alttan üste
        readonly List<Cartridge> returning = new();
        Quaternion socketRotation;
        float scale;

        /// <summary>Depodaki (yolda olmayan) kartuş sayısı.</summary>
        public int Count => stack.Count;

        public int Capacity => sockets.Count;

        /// <summary>Ardışık iki yuva arası (yerel, alttan üste).</summary>
        public Vector3 SocketStep => sockets.Count > 1 ? sockets[1] - sockets[0] : Vector3.up * (0.3f * scale);

        /// <summary>Depo yuvaları (yerel konum, alttan üste) ve kartuş prefabı; 'count' kadar kartuşla dolu başlar.</summary>
        public void Build(GameObject prefab, IReadOnlyList<Vector3> localSockets, int count, float modelScale, Material neonCore)
        {
            cartridgePrefab = prefab;
            spawned = 0;
            neon = neonCore;
            scale = modelScale;
            sockets.Clear();
            sockets.AddRange(localSockets);
            socketRotation = Quaternion.identity;
            for (var i = 0; i < count && i < sockets.Count; i++) Spawn(sockets[i]);
        }

        GameObject cartridgePrefab;
        Material neon;
        int spawned;                                 // depoya ait kartuş sayısı (yolda olanlar dahil)

        /// <summary>
        /// Depoya bir kartuş daha (Hangar jokeri: yörünge kapasitesi +1). Modelde yuva yoksa en üst yuvanın bir aralık
        /// üstüne yeni yuva eklenir; kartuş oraya konur.
        /// </summary>
        public void AddCartridge()
        {
            if (!cartridgePrefab || sockets.Count == 0) return;
            if (spawned >= sockets.Count)
            {
                var top = sockets[^1];
                var step = sockets.Count > 1 ? top - sockets[^2] : Vector3.up * (0.3f * scale);
                sockets.Add(top + step);
            }
            Spawn(sockets[Mathf.Min(stack.Count, sockets.Count - 1)]);
        }

        void Spawn(Vector3 socket)
        {
            var t = ModelKit.Spawn(cartridgePrefab, transform, socket, Quaternion.identity, Color.white);
            t.localScale = Vector3.one * scale;
            // Enerji çekirdeği neon: bloom ile ışık saçar.
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var m = 0; m < mats.Length; m++)
                    if (mats[m] && mats[m].name.StartsWith("Orbit_Lights")) mats[m] = neon;
                r.sharedMaterials = mats;
            }
            socketRotation = t.localRotation;          // modelin Blender → sahne dönüşü
            stack.Add(new Cartridge { transform = t });
            spawned++;
        }

        /// <summary>Kartuşun depo yuvasındaki yerel dönüşü (modelin Blender → sahne dönüşü).</summary>
        public Quaternion SocketRotation => socketRotation;

        /// <summary>En alttaki kartuşu verir (depodan ayrılır); depo boşsa dönmekte olanı; yoksa null. Üsttekiler aşağı kayar.</summary>
        public Transform Take()
        {
            if (stack.Count == 0)
            {
                if (returning.Count == 0) return null;
                var r = returning[0];
                returning.RemoveAt(0);
                return r.transform;
            }
            var c = stack[0];
            stack.RemoveAt(0);
            return c.transform;
        }

        /// <summary>Kartuş bulunduğu yerden depoya uçar ve en üstteki boş yuvaya, oradan da aşağıya iner.</summary>
        public void Return(Transform cartridge)
        {
            if (cartridge == null) return;
            cartridge.SetParent(transform, true);
            var c = new Cartridge
            {
                transform = cartridge, returnTime = 0f, returnFrom = cartridge.localPosition,
                returnRotation = cartridge.localRotation,
            };
            returning.Add(c);
        }

        void Update()
        {
            var dt = Time.deltaTime;
            for (var i = returning.Count - 1; i >= 0; i--)
            {
                var c = returning[i];
                c.returnTime += dt;
                var p = Mathf.Clamp01(c.returnTime / ReturnSeconds);
                var top = sockets[sockets.Count - 1] + Vector3.up * (0.3f * scale);
                var e = Tween.Evaluate(Tween.Ease.InOutSine, p);
                c.transform.localPosition = Vector3.Lerp(c.returnFrom, top, e) + Vector3.back * (ReturnLift * scale * Mathf.Sin(p * Mathf.PI));
                c.transform.localRotation = Quaternion.Slerp(c.returnRotation, socketRotation, e);
                c.transform.localScale = Vector3.one * scale;
                if (p < 1f) continue;
                returning.RemoveAt(i);
                c.returnTime = -1f;
                stack.Add(c);
            }

            // Yığındaki her kartuş kendi yuvasına (alttan sırası) kayar.
            var step = SlideSpeed * scale * dt;
            for (var i = 0; i < stack.Count; i++)
            {
                var t = stack[i].transform;
                t.localPosition = Vector3.MoveTowards(t.localPosition, sockets[Mathf.Min(i, sockets.Count - 1)], step);
                t.localRotation = socketRotation;
            }
        }
    }
}
