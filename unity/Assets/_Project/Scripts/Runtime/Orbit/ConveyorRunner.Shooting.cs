using System;
using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class ConveyorRunner : MonoBehaviour
    {
        // Atış: zamanı gelmişse bu karede düşen atış sayısı kadar (en fazla 16) ateş eder.
        void Fire(Item item, float k)
        {
            if (item.ammo <= 0 || Time.time < item.nextFire) return;
            var interval = 1f / (OrbitTuning.ShotsPerSecond * ShootSpeedMultiplier(k));
            var shots = Mathf.Clamp(Mathf.FloorToInt((Time.deltaTime + 0.0001f) / interval), 1, OrbitTuning.MaxShotsPerFrame);
            var tolerance = OrbitTuning.AlignTolerance * k / field.Pitch;
            for (var n = 0; n < shots && item.ammo > 0; n++)
            {
                var from = item.view.transform.position;
                var bead = ShooterTargeting.Pick(grid, field.WorldToGrid(from), item.view.Spec.color, tolerance, item.pass);
                if (bead < 0) return;

                grid.TryReserve(bead);
                lastShooter = item;
                item.ammo--;
                item.view.SetCount(item.ammo);
                item.view.PunchScale(OrbitTuning.ShootPunch, OrbitTuning.ShootPunchSeconds, OrbitTuning.ShootPunchVibrato, OrbitTuning.ShootPunchElasticity);
                Haptics.Shoot();
                projectiles.Add(TakeProjectile(item, bead, from));
                item.nextFire = Mathf.Max(item.nextFire + interval, Time.time - interval);

                // Mermisi biten roket ekrandan çıkar; ama vurulacak piksel kalmadıysa (son atış) yörüngede kalır ve bölüm sonunda
                // oradan doğrudan kilide gider.
                if (item.ammo == 0 && grid.Unreserved > 0) StartDisappear(item);
            }
        }

        // Atışlar çok sık olduğu için mermiler havuzdan gelir; her atışta nesne oluşturulup yok edilmez.
        Projectile TakeProjectile(Item owner, int bead, Vector3 from)
        {
            var material = palette.For(owner.view.Spec.color);
            Projectile p;
            if (projectilePool.Count > 0)
            {
                p = projectilePool.Pop();
                p.visual.localPosition = from;
                p.renderer.sharedMaterial = material;
                p.visual.gameObject.SetActive(true);
            }
            else
            {
                var go = Primitives.Create(PrimitiveType.Sphere, "Projectile", transform, material, from, Vector3.one * ProjectileSize);
                p = new Projectile { visual = go.transform, renderer = go.GetComponent<MeshRenderer>() };
            }
            p.owner = owner;
            p.bead = bead;
            return p;
        }

        void ReturnProjectile(Projectile p)
        {
            p.owner = null;
            p.visual.gameObject.SetActive(false);
            projectilePool.Push(p);
        }

        void UpdateProjectiles(float dt, float k)
        {
            var step = OrbitTuning.ProjectileSpeed * k * dt;
            for (var i = projectiles.Count - 1; i >= 0; i--)
            {
                var p = projectiles[i];
                var target = field.CellWorld(p.bead);
                p.visual.position = Vector3.MoveTowards(p.visual.position, target, step);
                if ((p.visual.position - target).sqrMagnitude > 1e-6f) continue;

                ReturnProjectile(p);
                projectiles.RemoveAt(i);
                grid.Clear(p.bead);
                field.Pop(p.bead);
                Sfx.Play(Sfx.PixelPop);
                BeadCleared?.Invoke();
            }
        }
    }
}
