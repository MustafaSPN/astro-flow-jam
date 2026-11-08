using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class ConveyorRunner : MonoBehaviour
    {
        // Kartuş: roket rampaya çıkarken depodan bir enerji hücresi alır, turu bitince depoya geri verir.
        // Uçuşun son PickSeconds'ında depodaki en alt kartuş (depo boşsa dönmekte olan) rokete uçar ve içine girer.
        void UpdatePick(Item item, float k)
        {
            if (!item.pickStarted && item.time >= item.flightSeconds - PickSeconds)
            {
                item.pickStarted = true;
                item.pickStart = item.time;
                item.cartridge = track.Depot.Take();
                item.cartridgeAttached = false;
                if (item.cartridge)
                {
                    item.cartridge.SetParent(transform, true);
                    item.cartridgeFrom = item.cartridge.position;
                    item.cartridgeRotation = item.cartridge.rotation;
                }
            }
            if (!item.cartridge || item.cartridgeAttached || !item.pickStarted) return;
            var c = Mathf.Clamp01((item.time - item.pickStart) / PickSeconds);
            var e = Tween.Evaluate(Tween.Ease.InOutSine, c);
            var t = item.view.transform;
            item.cartridge.position = Vector3.Lerp(item.cartridgeFrom, CartridgeSeat(item), e) + Vector3.back * (0.4f * k * Mathf.Sin(c * Mathf.PI));
            item.cartridge.rotation = Quaternion.Slerp(item.cartridgeRotation, t.rotation * CartridgeRotation, e);
            if (c >= 1f) AttachCartridge(item);
        }


        void AttachCartridge(Item item)
        {
            var t = item.view.transform;
            item.cartridge.position = CartridgeSeat(item);
            item.cartridge.rotation = t.rotation * CartridgeRotation;
            item.cartridge.SetParent(t, true);
            item.cartridge.gameObject.SetActive(false);   // roket kartuşu "içine alır"; bırakana kadar görünmez
            item.cartridgeAttached = true;
        }


        void ReleaseCartridge(Item item)
        {
            if (!item.cartridge) return;
            item.cartridge.gameObject.SetActive(true);
            track.Depot.Return(item.cartridge);
            item.cartridge = null;
            item.cartridgeAttached = false;
        }
    }
}
