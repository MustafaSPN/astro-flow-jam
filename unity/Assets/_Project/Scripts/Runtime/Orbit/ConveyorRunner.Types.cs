using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class ConveyorRunner : MonoBehaviour
    {
        // Banttaki her roket (ve büyük bloğun her parçası) bir Item; uçan mermiler Projectile.
        enum State { Jumping, Riding, Waiting, Disappearing, Headbutt }

        sealed class Item
        {
            public RocketView view;
            public int units;
            public int ammo;
            public State state;
            public float time;
            public float distance;
            public float nextFire;
            public Vector3 jumpFrom;
            public Vector3 jumpTo;
            public bool jumpToBelt;
            public int slot = -1;
            public float length;
            public float spinFrom;
            public float flightSeconds;
            public bool hop;
            public Transform cartridge;
            public Vector3 cartridgeFrom;
            public Quaternion cartridgeRotation;
            public bool cartridgeAttached;
            public Vector3 flyDirection;
            public bool pickStarted;            // kartuş depodan çıktı (uçuşun son kısmında)
            public float pickStart;
            public bool launchSound;            // kapıdan çıkan parça: harekete başlarken roket sesi çalar
            public bool queued;                 // rampa sırasında (rampanın önündeki sıraya uçuyor ya da orada bekliyor)
            public readonly PassLines pass = new();   // bu geçişte ateş edilen hatlar (hat başına tek boncuk)
        }

        sealed class Projectile
        {
            public Item owner;
            public int bead;
            public Transform visual;
            public MeshRenderer renderer;
        }
    }
}
