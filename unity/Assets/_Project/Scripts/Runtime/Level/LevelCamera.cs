using System.Collections;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Bölüm kamerası: hafif alttan bakan perspektif kamera içeriğin merkezine bakar; mesafe tüm içerik ortadaki sütuna
    /// sığacak şekilde aranır. Uzay arka planı kameraya bağlıdır ve görüş alanını her en boy oranında tam kaplar.
    /// </summary>
    public sealed class LevelCamera
    {
        readonly Camera camera;
        readonly SpriteRenderer sky;
        readonly Vector3[] corners = new Vector3[4];
        Rect content;
        Vector3 fittedFor;

        public LevelCamera(Camera camera, SpriteRenderer sky)
        {
            this.camera = camera;
            this.sky = sky;
        }

        /// <summary>Sığdırılacak içerik (dünya düzleminde); sonraki <see cref="Fit"/> yeniden hesaplar.</summary>
        public void SetContent(Rect bounds)
        {
            content = bounds;
            fittedFor = Vector3.zero;
        }

        /// <summary>Kamerayı içeriğe sığdırır. En boy oranı ve açılar değişmedikçe hesaplamaz; yeniden sığdırdıysa true.</summary>
        public bool Fit(float tilt, float fov)
        {
            if (!camera || content.width <= 0f) return false;
            var key = new Vector3(camera.aspect, tilt, fov);
            if (key == fittedFor) return false;
            fittedFor = key;

            camera.orthographic = false;
            camera.fieldOfView = fov;
            var rotation = Quaternion.Euler(-tilt, 0f, 0f);
            var forward = rotation * Vector3.forward;
            var center = (Vector3)content.center;
            camera.transform.rotation = rotation;

            corners[0] = new Vector3(content.xMin, content.yMin);
            corners[1] = new Vector3(content.xMax, content.yMin);
            corners[2] = new Vector3(content.xMin, content.yMax);
            corners[3] = new Vector3(content.xMax, content.yMax);
            // İçerik ortadaki sütuna sığar: geniş ekranda (iPad) tahta telefondaki oranıyla görünür, telefonda sütun tüm ekrandır.
            var side = (1f - ScreenFit.ColumnFraction) / 2f;
            float near = 1f, far = 500f;
            for (var i = 0; i < 40; i++)
            {
                var distance = (near + far) / 2f;
                camera.transform.position = center - forward * distance;
                var fits = true;
                foreach (var c in corners)
                {
                    var v = camera.WorldToViewportPoint(c);
                    if (v.z <= 0f || v.x < side || v.x > 1f - side || v.y < 0f || v.y > 1f) { fits = false; break; }
                }
                if (fits) far = distance; else near = distance;
            }
            camera.transform.position = center - forward * far;
            camera.farClipPlane = far * 2f + 50f;
            CoverSky(far + 30f, fov);
            return true;
        }

        /// <summary>Açı korunarak merkeze yaklaşılır; halfExtent yarıçaplı alan ekranı doldurur.</summary>
        public IEnumerator ZoomTo(Vector3 center, float halfExtent, float fov, float seconds)
        {
            var from = camera.transform.position;
            var forward = camera.transform.forward;
            var tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            var distance = Mathf.Max(halfExtent / (tan * camera.aspect), halfExtent / tan);
            var to = center - forward * distance;
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                camera.transform.position = Vector3.Lerp(from, to, Tween.Evaluate(Tween.Ease.InOutSine, t / seconds));
                yield return null;
            }
            camera.transform.position = to;
        }

        // Taşan kısım kırpılır.
        void CoverSky(float depth, float fov)
        {
            var height = 2f * depth * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            var size = sky.sprite.bounds.size;
            var scale = Mathf.Max(height / size.y, height * camera.aspect / size.x);
            sky.transform.localPosition = new Vector3(0f, 0f, depth);
            sky.transform.localRotation = Quaternion.identity;
            sky.transform.localScale = Vector3.one * scale;
        }
    }
}
