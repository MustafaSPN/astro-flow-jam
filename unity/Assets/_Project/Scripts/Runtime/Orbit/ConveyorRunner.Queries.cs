using System;
using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class ConveyorRunner : MonoBehaviour
    {
        bool AnyRiderFitsSlots()
        {
            foreach (var item in items)
                if (OnOrbit(item) && item.ammo > 0 && FindSlot(item.units) >= 0) return true;
            return false;
        }

        // Vurabilen var mı: bantta bir roket, yörüngenin herhangi bir yerinden kendi renginde bir
        // boncuğu vurabilir mi?
        bool AnyRiderCanShoot()
        {
            foreach (var item in items)
                if (OnOrbit(item) && item.ammo > 0 && ShooterTargeting.CanHitFromOrbit(grid, item.view.Spec.color))
                    return true;
            return false;
        }

        // Bizim sapmamız: slotta bekleyen (ya da rampa sırasındaki) bir roket yörüngede yer bulup kendi rengini vurabilir mi?
        // Slotlar dolu olsa da oyuncu o roketi yörüngeye gönderip resmi açabilir (ör. ortadaki beyaz pikselleri vurup
        // arkadaki turuncuyu açığa çıkarır); bu durumda kaybedilmez.
        bool AnySlotRocketCanHelp()
        {
            foreach (var item in items)
            {
                if (item.ammo <= 0 || !ShooterTargeting.CanHitFromOrbit(grid, item.view.Spec.color)) continue;
                if (item.queued) return true;
                if (item.state == State.Waiting && item.slot >= 0 && belt.CanReserve(item.units)) return true;
            }
            return false;
        }

        // Yörüngeye uçmakta olan (slottan dokunulmuş ya da kapıdan yeni çıkmış) mermili roket var mı?
        bool AnyJoining()
        {
            foreach (var item in items)
                if (item.state == State.Jumping && item.jumpToBelt && item.ammo > 0) return true;
            return false;
        }
    }
}
