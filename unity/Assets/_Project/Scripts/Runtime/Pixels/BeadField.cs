using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Gizli resim ve üzerindeki boncukların görünümü. Katmanlar: en altta resim, her hücrede resmi boncuk aralarından
    /// sızdırmayan koyu örtü, en üstte yuvarlatılmış küp boncuklar. Vurulan boncuk
    /// patlar: 0,05 sn'de 1,1 kata şişer, 0,1 sn'de küçülerek kaybolur ve bu sırada kameraya doğru hafifçe
    /// zıplar; örtüsü kalkar, resmin o hücresi açılır.
    /// </summary>
    public sealed class BeadField : MonoBehaviour
    {
        const float CoverFadeSeconds = OrbitTuning.PixelPopSeconds + OrbitTuning.PixelShrinkSeconds;
        const float DimFactor = 0.45f;        // şeffaf siyah filtre: resim bu oranda parlak görünür (%55 karartma)
        const float RevealSeconds = 0.8f;
        const float PixelModelSize = 0.94f;   // AFJ_Pixel_1x1 kenarı (model birimi)
        const float PixelFill = 0.92f;        // piksel hücrenin bu kadarını kaplar
        const float PixelHeight = 4f;         // piksel kalınlığı (kameraya doğru) hücre boyunun bu katı; taban resimde sabit
        // Katmanlar (kameraya doğru, −z): yörünge tabanı 0,72 → gizli resim → yol kanalı ve raylar → ödül kapısı → kilit
        // → pikseller. Resmin taşan kenarları yolun ve rayın altında kalır.
        public const float PictureDepth = 0.8f;   // en altta: yörüngenin de arkasında (taban plakası gizlenir)
        public const float BeadDepth = -0.25f;
        // Uyarlamalı piksel boyutu: resim yörüngenin içindeki alana sığacak kadar küçülür (uzun kenar alanı doldurur);
        // küçük ızgaralarda piksel en çok 15 sütunluk resmin pikseli kadar olur (1. bölümün boyutu).
        const int FullSizeCells = 15;

        BeadGrid grid;
        PrototypePalette palette;
        Transform[] beads;
        Transform[] covers;
        float pitch;
        Material pictureMaterial;
        float revealTime = -1f;
        Vector2 topLeft;                      // ızgaranın sol üst köşesi, boncuk kökünün yerel uzayında
        Transform beadRoot;                   // boncuklar: perspektif kaymasını telafi etmek için ayrı kök

        readonly List<(int index, float time)> popping = new();
        float hop;                            // zıplama mesafesi, sahne birimine çevrilmiş

        public float Pitch => pitch;

        /// <param name="size">Resim alanının kenar uzunluğu (yerel birim).</param>
        /// <param name="pictureSpan">Gizli resmin kenar uzunluğu (0 ise boncuk ızgarası kadar). Resim kare olacak şekilde
        /// ortadan kırpılır; köşeleri yörünge rayının altında kalır.</param>
        public void Build(BeadGrid beadGrid, Texture2D hiddenImage, float size, PrototypePalette prototypePalette, float pictureSpan = 0f, GameObject pixelModel = null)
        {
            grid = beadGrid;
            palette = prototypePalette;
            pitch = size / Mathf.Max(FullSizeCells, Mathf.Max(grid.Columns, grid.Rows));
            hop = OrbitTuning.PixelHop * OrbitTuning.Scale(size);
            var width = pitch * grid.Columns;
            var height = pitch * grid.Rows;
            // Dolu piksellerin sınır kutusu yörüngenin (resmin) merkezine gelir: resim ızgarada bir yana kaymış olsa da
            // pikseller ortalı görünür. Hedefleme de aynı topLeft'i kullandığı için atış hizası korunur.
            topLeft = new Vector2(-width / 2f, height / 2f) + CenteringOffset();

            // Gizli resim
            beadRoot = new GameObject("Bead Root").transform;
            beadRoot.SetParent(transform, false);
            var picture = Primitives.Create(PrimitiveType.Quad, "Hidden Image", transform, palette.pictureUnlit,
                new Vector3(0f, 0f, PictureDepth), pictureSpan > 0f ? new Vector3(pictureSpan, pictureSpan, 1f) : new Vector3(width, height, 1f));
            // Resim, açılan hücrelerde de şeffaf siyah bir filtreyle karartılmış görünür; bölüm bitince filtre kalkar.
            pictureMaterial = new Material(palette.pictureUnlit) { name = "Hidden Image (runtime)" };
            if (hiddenImage != null)
            {
                pictureMaterial.SetTexture(ShaderIds.BaseMap, hiddenImage);
                pictureMaterial.mainTexture = hiddenImage;
                if (pictureSpan > 0f)
                {
                    // Kare kırpma: uzun kenarın ortasındaki kare alınır.
                    var aspect = hiddenImage.width / (float)hiddenImage.height;
                    var scale = aspect >= 1f ? new Vector2(1f / aspect, 1f) : new Vector2(1f, aspect);
                    var offset = (Vector2.one - scale) / 2f;
                    pictureMaterial.SetTextureScale("_BaseMap", scale);
                    pictureMaterial.SetTextureOffset("_BaseMap", offset);
                    pictureMaterial.mainTextureScale = scale;
                    pictureMaterial.mainTextureOffset = offset;
                }
            }
            SetPictureBrightness(1f);   // resim ödül kapısının arkasında saklıdır; karartma gerekmez
            picture.GetComponent<MeshRenderer>().sharedMaterial = pictureMaterial;

            // Piksel modeli (AFJ_Pixel_1x1) varsa onun mesh'i, yoksa yuvarlak kutu. Malzeme roketlerin gövde malzemesinin
            // kendisi: aynı renkteki roket ve piksel birebir aynı renk, parlaklık ve ışıkla görünür.
            Mesh beadMesh;
            Quaternion meshRotation = Quaternion.identity;
            Vector3 meshScale = Vector3.one;
            var modelFilter = pixelModel ? pixelModel.GetComponentInChildren<MeshFilter>() : null;
            if (modelFilter)
            {
                beadMesh = modelFilter.sharedMesh;
                var mt = modelFilter.transform;
                meshRotation = ModelKit.BlenderToScene * mt.localRotation;
                var fit = pitch * PixelFill / PixelModelSize;
                meshScale = Vector3.Scale(mt.localScale, Vector3.one * fit);
            }
            else beadMesh = RoundedBox.Get(pitch * 0.9f, pitch * 0.9f, pitch * 0.55f, pitch * 0.18f);
            beads = new Transform[grid.Columns * grid.Rows];
            covers = new Transform[beads.Length];
            for (var i = 0; i < beads.Length; i++)
            {
                if (grid.StateAt(i) == BeadState.Cleared) continue;
                var center = CellLocal(i);

                var bead = new GameObject("Bead");
                bead.transform.SetParent(beadRoot, false);
                bead.transform.localPosition = center + new Vector3(0f, 0f, BeadDepth);
                var meshObject = bead;
                if (modelFilter)
                {
                    // Yükseklik: piksel kameraya doğru uzar (arka yüzü yerinde kalır), en ve boy aynı.
                    var raise = new GameObject("Height").transform;
                    raise.SetParent(bead.transform, false);
                    raise.localScale = new Vector3(1f, 1f, PixelHeight);
                    raise.localPosition = new Vector3(0f, 0f, -0.5f * pitch * PixelFill * (PixelHeight - 1f));
                    meshObject = new GameObject("Mesh");
                    meshObject.transform.SetParent(raise, false);
                    meshObject.transform.localRotation = meshRotation;
                    meshObject.transform.localScale = meshScale;
                }
                meshObject.AddComponent<MeshFilter>().sharedMesh = beadMesh;
                var renderer = meshObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ModelKit.PixelMaterial(ModelKit.ColorOf(palette, grid.ColorAt(i)));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beads[i] = bead.transform;
            }
        }

        // Izgara merkezi ile dolu piksellerin sınır kutusu merkezi arasındaki fark (yerel, tam piksel adımında değil).
        Vector2 CenteringOffset()
        {
            int minC = int.MaxValue, maxC = int.MinValue, minR = int.MaxValue, maxR = int.MinValue;
            for (var i = 0; i < grid.Columns * grid.Rows; i++)
            {
                if (grid.StateAt(i) == BeadState.Cleared) continue;
                var c = grid.ColumnOf(i);
                var r = grid.RowOf(i);
                minC = Mathf.Min(minC, c); maxC = Mathf.Max(maxC, c);
                minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r);
            }
            if (minC > maxC) return Vector2.zero;
            var dx = (grid.Columns - 1) / 2f - (minC + maxC) / 2f;     // sütun: sağa pozitif
            var dy = (minR + maxR) / 2f - (grid.Rows - 1) / 2f;        // satır aşağı doğru artar; yukarı pozitif
            return new Vector2(dx, dy) * pitch;
        }

        Vector3 CellLocal(int index) => new(
            topLeft.x + (grid.ColumnOf(index) + 0.5f) * pitch,
            topLeft.y - (grid.RowOf(index) + 0.5f) * pitch,
            0f);

        public Vector3 CellWorld(int index) => beadRoot.TransformPoint(CellLocal(index) + new Vector3(0f, 0f, BeadDepth));

        /// <summary>
        /// Perspektif telafisi: boncuklar yörüngeden kameraya daha yakın olduğu için eğik kamerada kayık görünür. Kameradan
        /// 'reference' noktasına giden ışının boncuk düzlemini kestiği yere kaydırılır; böylece ekranda tam ortalı durur.
        /// </summary>
        public void AlignToView(Camera cam, Vector3 reference)
        {
            if (!cam || !beadRoot) return;
            beadRoot.localPosition = Vector3.zero;
            var planeZ = transform.TransformPoint(new Vector3(0f, 0f, BeadDepth)).z;
            var c = cam.transform.position;
            var d = reference - c;
            if (Mathf.Abs(d.z) < 1e-4f) return;
            var hit = c + d * ((planeZ - c.z) / d.z);
            var shift = hit - new Vector3(reference.x, reference.y, planeZ);
            beadRoot.localPosition = transform.InverseTransformVector(new Vector3(shift.x, shift.y, 0f));
        }

        /// <summary>Dünya konumunun ızgara uzayındaki karşılığı (hücre birimi; x soldan sağa, y üstten aşağı).</summary>
        public Vector2 WorldToGrid(Vector3 world)
        {
            var p = beadRoot.InverseTransformPoint(world);
            return new Vector2((p.x - topLeft.x) / pitch, (topLeft.y - p.y) / pitch);
        }

        /// <summary>Tüm boncuklar kalkınca karartma filtresini yumuşakça kaldırır.</summary>
        public void RevealFully() => revealTime = 0f;

        void SetPictureBrightness(float value) =>
            pictureMaterial.SetColor(ShaderIds.BaseColor, new Color(value, value, value, 1f));

        /// <summary>
        /// UFO jokeri: boncuk ızgaradan ayrılıp çağırana verilir (kendisi çekilme animasyonunu yapar ve yok eder); altındaki
        /// resim hücresi patlamadaki gibi açılır.
        /// </summary>
        public Transform TakeBead(int index)
        {
            var bead = beads[index];
            beads[index] = null;
            popping.Add((index, 0f));
            return bead;
        }

        /// <summary>Vurulan boncuğu patlatır ve altındaki resim hücresini açar.</summary>
        public void Pop(int index) => popping.Add((index, 0f));

        void Update()
        {
            var dt = Time.deltaTime;

            if (revealTime >= 0f && revealTime < RevealSeconds)
            {
                revealTime += dt;
                var k = Mathf.Clamp01(revealTime / RevealSeconds);
                SetPictureBrightness(Mathf.Lerp(DimFactor, 1f, k * k * (3f - 2f * k)));
            }

            for (var i = popping.Count - 1; i >= 0; i--)
            {
                var (index, time) = popping[i];
                time += dt;
                // PixelAnimationController: önce pop (1 → 1,1), sonra küçülme (1,1 → 0); zıplama tüm süreye yayılır.
                // Ease değerleri ayarlarda yok; DOTween varsayılanı (OutQuad) kullanıldı.
                const float pop = OrbitTuning.PixelPopSeconds;
                const float total = OrbitTuning.PixelPopSeconds + OrbitTuning.PixelShrinkSeconds;
                var scale = time < pop
                    ? Mathf.Lerp(1f, OrbitTuning.PixelPopScale, Tween.Evaluate(Tween.Ease.OutQuad, time / pop))
                    : Mathf.Lerp(OrbitTuning.PixelPopScale, 0f, Tween.Evaluate(Tween.Ease.OutQuad, (time - pop) / OrbitTuning.PixelShrinkSeconds));
                if (beads[index])
                {
                    beads[index].localScale = Vector3.one * Mathf.Max(0f, scale);
                    beads[index].localPosition = CellLocal(index) + new Vector3(0f, 0f, BeadDepth - hop * Mathf.Clamp01(time / total));
                }
                var cover = Mathf.Clamp01(time / CoverFadeSeconds);
                if (covers[index]) covers[index].localScale = new Vector3(pitch * 1.02f * (1f - cover), pitch * 1.02f * (1f - cover), 0.05f);

                if (time >= total)
                {
                    if (beads[index]) Destroy(beads[index].gameObject);
                    if (covers[index]) Destroy(covers[index].gameObject);
                    popping.RemoveAt(i);
                }
                else popping[i] = (index, time);
            }
        }
    }
}
