using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameVSM
{
    // Where the conductor may walk on foot. Zones are authored by the location builders in the
    // environment's local space, so they follow the scenery while it moves past the train.
    // Inside the train its own walls and doors limit movement, so the car corridor band is always allowed.
    public sealed class WalkArea : MonoBehaviour
    {
        [Serializable] public struct Zone { public string Name; public Vector3 Min, Max; }
        public Zone[] Zones = Array.Empty<Zone>();
        // Train body band in world space (train is fixed; scenery moves). Feet above the car floor sill.
        // Matches the existing 195 m collision floor built by BuildMetallostroy, not an
        // engineering dimension. The upper limit prevents a roof from counting as a cabin.
        public const float TrainFloorMin = 1.0f, TrainFloorMax = 2.2f, TrainHalfWidth = 1.62f;
        public const float TrainMinX = -97.5f, TrainMaxX = 97.5f;
        static readonly List<WalkArea> active = new();

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        public bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            foreach (var z in Zones)
                if (p.x >= z.Min.x && p.x <= z.Max.x && p.y >= z.Min.y && p.y <= z.Max.y && p.z >= z.Min.z && p.z <= z.Max.z)
                    return true;
            return false;
        }

        public static bool InsideTrain(Vector3 feet) =>
            feet.y > TrainFloorMin && feet.y < TrainFloorMax && Mathf.Abs(feet.z) < TrainHalfWidth && feet.x > TrainMinX && feet.x < TrainMaxX;

        // No authored areas (old prefabs, tests without environment) means no restriction.
        public static bool Allows(Vector3 feet)
        {
            if (active.Count == 0 || InsideTrain(feet)) return true;
            foreach (var area in active) if (area != null && area.Contains(feet)) return true;
            return false;
        }

        // Test the swept centre at short intervals: a large frame must not jump across a
        // narrow forbidden gap merely because its endpoint lies on another platform.
        public static Vector3 ConstrainStep(Vector3 feet, Vector3 step)
        {
            if (step.sqrMagnitude < 1e-8f || !Allows(feet)) return step;
            if (CanTraverse(feet, step)) return step;
            var alongX = new Vector3(step.x, 0, 0);
            var alongZ = new Vector3(0, 0, step.z);
            if (Mathf.Abs(step.x) >= Mathf.Abs(step.z))
            {
                if (CanTraverse(feet, alongX)) return alongX;
                if (CanTraverse(feet, alongZ)) return alongZ;
            }
            else
            {
                if (CanTraverse(feet, alongZ)) return alongZ;
                if (CanTraverse(feet, alongX)) return alongX;
            }
            return Vector3.zero;
        }

        static bool CanTraverse(Vector3 feet, Vector3 step)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(step.magnitude / .08f));
            for (int i = 1; i <= samples; i++)
                if (!Allows(feet + step * ((float)i / samples))) return false;
            return true;
        }
    }
}
