using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class RocketView : MonoBehaviour
    {
        /// <summary>DOPunchScale: ölçeğe azalan, yön değiştiren bir itme ekler.</summary>
        public void PunchScale(Vector3 amount, float duration, int vibrato, float elasticity)
        {
            punchAmount = amount;
            punchDuration = duration;
            punchVibrato = vibrato;
            punchElasticity = elasticity;
            punchTime = 0f;
        }

        /// <summary>DOShakeScale: ölçeği sönümlenen bir sarsıntıyla titretir.</summary>
        public void ShakeScale(float duration, float strength, int vibrato)
        {
            shakeDuration = duration;
            shakeStrength = Vector3.one * strength;
            shakeVibrato = vibrato;
            shakeTime = 0f;
        }

        /// <summary>İniş ezilmesi: 0,08 sn'de (1,15; 0,85) oranına OutQuad, 0,06 sn'de geri.</summary>
        public void LandSquash() => squashTime = 0f;

        // Seçim: blok tutulduğu sürece hafifçe kameraya doğru kalkar; altında kendi renginde yumuşak, nefes alan bir hale yayar.
        const float SelectLift = 0.38f;
        const float SelectGrow = 0.08f;
        const float HaloAlpha = 0.65f;   // açık zeminde kapının (koyu duvar üstündeki) halesiyle aynı şiddette görünür
        bool selected, selectRefresh;
        float selectScale = 1f;
        float selectAmount;
        readonly System.Collections.Generic.List<SpriteRenderer> halos = new();

        public void SetSelected(bool on)
        {
            selected = on;
            if (on && halos.Count == 0 && Spec.LocalCells != null && !Spec.isBlocker) BuildHalo();
            selectAmount = on ? 1f : 0f;   // geçiş yok: dokununca anında kalkar, bırakınca anında iner
            selectRefresh = true;
        }

        /// <summary>
        /// Kapıya itme: gövde duvarda durur, görünen model kapının içine doğru kayar (dünya biriminde ofset).
        /// Sıfır verilince yerine döner.
        /// </summary>
        public void SetDoorPush(Vector2 worldOffset)
        {
            if (!model) return;
            var local = transform.InverseTransformVector(worldOffset);
            model.localPosition = new Vector3(local.x, local.y, model.localPosition.z);
        }

        /// <summary>Kapıya itilen modeli köke taşır: kök itilen yere gelir, model ofseti sıfırlanır (görüntü yerinde kalır).</summary>
        public void CommitDoorPush()
        {
            if (!model) return;
            var world = transform.TransformVector(new Vector3(model.localPosition.x, model.localPosition.y, 0f));
            transform.position += world;
            model.localPosition = new Vector3(0f, 0f, model.localPosition.z);
        }

        void BuildHalo()
        {
            var sprite = UiArt.Glow(128, 128, 30f, 40f, Color.white);
            foreach (var c in Spec.LocalCells)
            {
                var center = new Vector3(c.x + 0.5f - Spec.size.x / 2f, c.y + 0.5f - Spec.size.y / 2f, 0.12f);   // zemin karolarının hemen önünde
                var halo = SceneArt.Place(sprite, "Select Halo", transform, center, 3);
                // Halenin dolu iç kısmı hücre kadar (48/128 piksel), yumuşak kenarı blok kenarından ~0,8 birim dışarı taşar.
                halo.transform.localScale = Vector3.one * (0.95f * 128f / 48f / sprite.bounds.size.x);
                var col = tint;
                col.a = 0f;
                halo.color = col;
                halo.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;   // tahta dışına (duvar arkasına) taşmaz
                halos.Add(halo);
            }
        }

        void UpdateSelection(float dt)
        {
            if (!selected && !selectRefresh) return;
            selectRefresh = false;
            var k = selectAmount;
            if (model)
            {
                // Kalkış: kameraya doğru yükselir ve hafifçe büyür (üstten bakan kamerada yükseklik böyle okunur).
                model.localPosition = new Vector3(model.localPosition.x, model.localPosition.y, -SelectLift * k);
                selectScale = 1f + SelectGrow * k;   // LateUpdate sonunda ezilme ölçeğiyle çarpılır
            }
            foreach (var h in halos)
            {
                var col = tint;
                col.a = HaloAlpha * k;   // sabit parlaklık (nefes yok)
                h.color = col;
                h.enabled = k > 0.001f;
            }
        }
    }
}
