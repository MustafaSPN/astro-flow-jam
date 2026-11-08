using System.Collections;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// UFO jokerinin görseli: UFO modeli (AFJ_UFO) sahnenin uzağında kendi kamerasıyla saydam bir dokuya çizilir; doku
    /// arayüzde HUD'un üstünde gösterilir. Model yavaşça kendi ekseninde döner, kamera hafif üstten bakar (kubbe görünür).
    /// </summary>
    public sealed class UfoStage
    {
        static readonly Color[] Lights =
        {
            new(1f, 0.25f, 0.6f), new(0.2f, 0.95f, 1f), new(1f, 0.85f, 0.2f), new(0.55f, 1f, 0.3f), new(0.75f, 0.35f, 1f),
            new(1f, 0.5f, 0.15f), new(0.25f, 0.55f, 1f), new(1f, 0.35f, 0.35f), new(0.3f, 1f, 0.75f), new(1f, 0.45f, 0.95f),
        };
        const float SpinSpeed = 160f;      // derece / sn: kendi ekseninde dönüş (renkli ışıklar dönerek görünür)

        readonly Transform root;

        public RenderTexture Texture { get; }

        public UfoStage(ModelLibrary models, MonoBehaviour host)
        {
            root = new GameObject("UFO Stage").transform;
            root.position = new Vector3(5000f, 5000f, 0f);
            Texture = new RenderTexture(512, 256, 16, RenderTextureFormat.ARGB32) { name = "UFO" };
            var cam = new GameObject("UFO Camera").AddComponent<Camera>();
            cam.transform.SetParent(root, false);
            // Model (çap 2 birim) dokuyu yatayda neredeyse tam doldurur.
            cam.transform.localPosition = new Vector3(0f, 0.62f, -2.45f);
            cam.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            cam.fieldOfView = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cam.allowHDR = false;
            cam.targetTexture = Texture;
            var data = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            var pivot = new GameObject("UFO Pivot").transform;
            pivot.SetParent(root, false);
            pivot.localRotation = Quaternion.Euler(90f, 0f, 0f);          // modelin yüksekliği (sahnede −z) yukarı bakar
            if (!models.ufo) return;
            var model = ModelKit.Spawn(models.ufo, pivot, Vector3.zero, Quaternion.identity, Color.white);
            // Çekim ışığı yayıcısı ve kenar ışıkları parlak (neon); her kenar ışığı farklı renkte. Gövde ve kubbe modelin
            // kendi renklerinde.
            var glow = ConveyorTrack.Neon(models.neonMaterial, new Color(0.3f, 0.95f, 1f));
            // Gövde metalik gümüş: modelin renginden biraz koyu, parlak ve yansımalı (ClippableLit _Metallic).
            var metal = new Material(ModelKit.Lit("AFJ UFO Metal", new Color(0.62f, 0.72f, 0.84f) * 0.88f, 0.85f)) { name = "AFJ UFO Metal" };
            metal.SetFloat(ShaderIds.Metallic, 0.85f);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var rim = r.name.StartsWith("Rim_Light_") && int.TryParse(r.name.Substring(10, 2), out var k) ? k : -1;
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    if (mats[i].name.StartsWith("UFO_Emitter")) mats[i] = glow;
                    else if (mats[i].name.StartsWith("UFO_Silver")) mats[i] = metal;
                    else if (rim >= 0) mats[i] = ConveyorTrack.Neon(models.neonMaterial, Lights[rim % Lights.Length]);
                }
                r.sharedMaterials = mats;
            }
            host.StartCoroutine(Spin(model));
        }

        public void Dispose()
        {
            if (root) Object.Destroy(root.gameObject);
            Texture.Release();
            Object.Destroy(Texture);
        }

        static IEnumerator Spin(Transform model)
        {
            var baseRotation = model.localRotation;
            for (var t = 0f; model; t += Time.deltaTime)
            {
                model.localRotation = Quaternion.AngleAxis(t * SpinSpeed, Vector3.back) * baseRotation;   // kendi dikey ekseninde
                yield return null;
            }
        }
    }
}
