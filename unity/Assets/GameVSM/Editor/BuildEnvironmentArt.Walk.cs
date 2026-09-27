using System.Collections.Generic;
using GameVSM;
using UnityEngine;

namespace GameVSM.Editor
{
    // Walkable zones per location, in site-local metres (site root sits at the origin while building).
    // Builders call Walkable() for every surface the conductor may step on; everything else is closed.
    public static partial class BuildEnvironmentArt
    {
        static readonly List<WalkArea.Zone> walkZones = new();

        // y range covers feet height on that surface (floor ± step); keep ~0.3 m margin above.
        static void Walkable(string name, Vector3 min, Vector3 max) =>
            walkZones.Add(new WalkArea.Zone { Name = name, Min = Vector3.Min(min, max), Max = Vector3.Max(min, max) });

        // Convenience: a flat rectangle at floor height y.
        static void WalkableFloor(string name, float x0, float x1, float z0, float z1, float y) =>
            Walkable(name, new Vector3(x0, y - .35f, z0), new Vector3(x1, y + .45f, z1));

        static void FinishWalk()
        {
            // Route has no outdoor walking areas: an empty component still keeps the
            // player inside the train. Absence is reserved for legacy/test scenes.
            root.gameObject.AddComponent<WalkArea>().Zones = walkZones.ToArray();
            walkZones.Clear();
        }
    }
}
