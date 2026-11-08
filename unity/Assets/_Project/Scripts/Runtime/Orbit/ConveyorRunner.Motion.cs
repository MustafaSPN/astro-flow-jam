using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class ConveyorRunner : MonoBehaviour
    {
        // Zıplama: kapıdan ya da slottan yörüngeye, yörüngeden slota, slot içinde sola kayma (hop).
        void UpdateJump(Item item, float k)
        {
            if (item.time < 0f) return;
            var t = item.view.transform;
            if (item.launchSound)
            {
                // Büyük bloğun parçaları sırayla kalkar: her biri harekete başlarken kendi roket sesi.
                item.launchSound = false;
                Sfx.Play(Sfx.RocketLaunch);
            }
            var p = Mathf.Clamp01(item.time / item.flightSeconds);
            if (item.hop)
            {
                // Slot içinde sola kayma: küçük zıplama, burun yukarıda, alev yok.
                var hopPos = Vector3.Lerp(item.jumpFrom, item.jumpTo, p);
                hopPos.z -= Tween.JumpHeight(OrbitTuning.JumpHeight * 0.35f * k, OrbitTuning.JumpPeak, p);
                t.position = hopPos;
                if (p >= 1f) { item.state = State.Waiting; item.view.LandSquash(); }
                return;
            }
            // Yörüngeye giden uçuş hız kesmeden biter (başta yumuşak, sonda sabit hız); slota iniş yavaşlayarak.
            var e = item.jumpToBelt ? p * p * (2f - p) : Tween.Evaluate(Tween.Ease.InOutSine, p);
            var lift = Vector3.back * (FlightLift * k * Mathf.Sin(p * Mathf.PI) * (item.jumpToBelt ? 0.5f : 1f));
            var pos = Vector3.Lerp(item.jumpFrom, item.jumpTo, e) + lift;
            var velocity = pos - t.position;
            t.position = pos;
            // Burun uçuş yönüne döner; son kısımda varış yönüne (bant akışı ya da slotta yukarı) yumuşakça oturur.
            var arrival = Quaternion.identity;   // rampada ve slotta burun yukarı
            var heading = ((Vector2)velocity).sqrMagnitude > 1e-8f ? Nose((Vector2)velocity) : t.rotation;
            t.rotation = Quaternion.Slerp(heading, arrival, Mathf.Clamp01((p - 0.7f) / 0.3f));
            if (item.jumpToBelt) UpdatePick(item, k);
            if (p < 1f) return;
            t.rotation = arrival;
            if (item.jumpToBelt)
            {
                if (item.cartridge && !item.cartridgeAttached) AttachCartridge(item);
                ReleaseRamp(item);
                item.state = State.Riding;
                item.distance = 0f;
                item.pass.Reset();
                item.nextFire = Time.time;
            }
            else
            {
                item.view.LandSquash();
                item.state = State.Waiting;
                item.view.SetFlame(false);
            }
        }

        // Kilide kafa atma: hızlanarak kilide uçar; çarpınca patlamayla yok olur. Yok olduysa true.
        bool UpdateHeadbutt(Item item, float dt, float k)
        {
            var t = item.view.transform;
            var p = Mathf.Clamp01(item.time / HeadbuttSeconds);
            var e = p * p;
            t.position = Vector3.Lerp(item.jumpFrom, item.jumpTo, e) + Vector3.back * (1.2f * k * Mathf.Sin(p * Mathf.PI));
            var dir = (Vector2)(item.jumpTo - item.jumpFrom);
            if (dir.sqrMagnitude > 1e-6f) t.rotation = Quaternion.Slerp(t.rotation, Nose(dir), Mathf.Clamp01(dt * 14f));
            if (p < 1f) return false;
            headbuttImpact?.Invoke(item.jumpTo - item.jumpFrom);
            headbuttImpact = null;
            return true;
        }

        // Mermisi biten roket burnunun yönünde hızlanarak uçar ve ekrandan çıkar. Ekrandan çıktıysa true.
        bool UpdateFlyOff(Item item, float dt, float k)
        {
            var t = item.view.transform;
            var p = Mathf.Clamp01(item.time / FlyOffSeconds);
            var travel = FlyOffSpeed * k * (item.time + item.time * item.time * 2.5f);
            t.position = item.jumpFrom + item.flyDirection * travel + Vector3.back * (0.8f * k * p);
            t.rotation = Quaternion.Slerp(t.rotation, Nose(item.flyDirection), Mathf.Clamp01(dt * 10f));
            return p >= 1f;
        }
    }
}
