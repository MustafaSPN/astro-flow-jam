using System;
using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class ConveyorRunner : MonoBehaviour
    {
        /// <summary>Slottaki rokete dokunma. Bantta yer varsa roket banda zıplar; yoksa itme ile reddedilir.</summary>
        /// <returns>Dokunulan bir slot roketi bulunduysa true.</returns>
        public bool TryTapSlot(Vector3 world, out bool rejected)
        {
            rejected = false;
            var reach = 0.6f * track.Scale;
            for (var i = 0; i < slots.Length; i++)
            {
                var item = slots[i];
                if (item == null || item.state != State.Waiting || item.slot != i || item.queued) continue;
                var p = SlotCenter(i, item.units);
                if (Mathf.Abs(world.x - p.x) > reach * item.units || Mathf.Abs(world.y - p.y) > reach) continue;

                if (!belt.TryReserve(item.units))
                {
                    // Yörünge dolu: daha sert bir ret tepkisi (büyük itme + sarsıntı + titreşim).
                    item.view.PunchScale(OrbitTuning.TapPunch * 2.2f, OrbitTuning.TapPunchSeconds, OrbitTuning.TapPunchVibrato + 4, OrbitTuning.TapPunchElasticity);
                    item.view.ShakeScale(BoardTuning.ShakeSeconds * 1.3f, BoardTuning.ShakeStrength * 2f, BoardTuning.ShakeVibrato + 4);
                    Haptics.GateExit(this);
                    rejected = true;
                    return true;
                }
                // Slotunu boşaltır ve rampa sırasına girer: rampa boşsa doğrudan rampaya, değilse sıranın sonuna uçar.
                for (var u = 0; u < item.units; u++) slots[i + u] = null;
                item.slot = -1;
                item.queued = true;
                rampQueue.Add(item);
                StartJump(item, item.view.transform.position, false, track.QueueSpot(rampQueue.Count - 1));
                ServeRampQueue();
                CompactSlots();
                Sfx.Play(Sfx.RocketLaunch);             // bekleme yerinden kalkış: dönüşümle aynı roket sesi
                SlotLaunched?.Invoke();
                return true;
            }
            return false;
        }

        // Slottaki roketler her zaman sola dayalıdır. Bir slot boşalınca sağdakiler sırayla
        // sola zıplar (kısa sıçrama, alev yok); slota uçmakta olan roketin hedefi de kayar.
        void CompactSlots()
        {
            var next = 0;
            for (var i = 0; i < slots.Length; i++)
            {
                var item = slots[i];
                if (item == null) continue;
                slots[i] = null;
                slots[next] = item;
                if (item.slot != next)
                {
                    item.slot = next;
                    if (item.state == State.Waiting) StartHop(item);
                    else if (item.state == State.Jumping) item.jumpTo = SlotCenter(next, 1);
                }
                next++;
            }
        }

        // Bu kadar birimlik roket için yan yana boş slotların ilki; yoksa -1.
        int FindSlot(int units)
        {
            for (var i = 0; i + units <= slots.Length; i++)
            {
                var free = true;
                for (var u = 0; u < units; u++) free &= slots[i + u] == null;
                if (free) return i;
            }
            return -1;
        }

        Vector3 SlotCenter(int first, int units)
        {
            var sum = Vector3.zero;
            for (var u = 0; u < units; u++) sum += track.SlotPositions[first + u];
            return sum / units;
        }
    }
}
