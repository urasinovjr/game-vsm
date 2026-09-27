using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameVSM
{
    // Background roles from the scene-finish plan, section 7. They never touch scores or timers.
    public enum LifeRole { Passenger, DepotWorker, Delivery, Cleaner }
    // Pass: walk through; Wait: stand/look; Work: gesture at a workplace; End: one-way route finished.
    public enum StopKind { Pass, Wait, Work, End }

    [Serializable]
    public struct RouteStop
    {
        public Vector3 Point;
        public StopKind Kind;
        public float Dwell;
        public bool Face;
        public float Yaw;
        public RouteStop(Vector3 point, StopKind kind = StopKind.Pass, float dwell = 0)
        { Point = point; Kind = kind; Dwell = dwell; Face = false; Yaw = 0; }
        public RouteStop Facing(float yaw) { Face = true; Yaw = yaw; return this; }
    }

    // Deterministic route rules; kept free of scene state so they can be tested in EditMode.
    public static class WorkRoute
    {
        public const float WaitTimeout = 6;

        // Stable 0..1 value per placement: rebuilt prefabs and tests behave the same way.
        public static float Seed(Vector3 p)
        {
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(p.x * 10) * 73856093u ^ (uint)Mathf.RoundToInt(p.y * 10) * 19349663u ^ (uint)Mathf.RoundToInt(p.z * 10) * 83492791u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xffffff) / (float)0x1000000;
            }
        }
        // Visit-dependent jitter keeps two agents with the same recipe from settling into lockstep.
        public static float Dwell(float authored, float seed, int visit) =>
            authored <= 0 ? 0 : authored * (.7f + .6f * Mathf.Repeat(seed * 7.31f + visit * .618f, 1));
        public static float Phase(float seed, int visit) => Mathf.Repeat(seed + visit * .377f, 1);
        // -1 means a one-way route has nothing left.
        public static int Next(int index, int count, bool loop) => count == 0 ? -1 : index + 1 < count ? index + 1 : loop ? 0 : -1;
        // A blocked background agent gives up its current stop instead of holding the shift.
        public static bool GiveUp(float waited, float seed) => waited > WaitTimeout * (.8f + .4f * seed);

        // True when `other` stands in the corridor the agent is about to walk into.
        public static bool Ahead(Vector3 self, Vector3 heading, Vector3 other, float reach, float clearance)
        {
            if (Mathf.Abs(other.y - self.y) > .9f) return false;
            var d = other - self; d.y = 0; heading.y = 0;
            if (heading.sqrMagnitude < 1e-6f) return false;
            heading.Normalize();
            float along = Vector3.Dot(d, heading);
            if (along <= 0 || along > reach) return false;
            return (d - heading * along).magnitude < clearance;
        }
        // Keep-right passing: two agents facing each other both step to their own right and clear.
        public static Vector3 Aside(Vector3 self, Vector3 heading, float width)
        {
            heading.y = 0; heading.Normalize();
            return self + Vector3.Cross(Vector3.up, heading) * width + heading * .6f;
        }
    }

    // People and carts that must not be walked through; the player is found once per scene.
    public static class LifeTraffic
    {
        public const int Free = 0, Person = 1, Player = 2;
        static readonly List<Transform> bodies = new();
        static FirstPersonController player;
        static int searchedFrame = -1000;

        public static void Add(Transform body) { if (body != null && !bodies.Contains(body)) bodies.Add(body); }
        public static void Remove(Transform body) => bodies.Remove(body);

        public static FirstPersonController Viewer
        {
            get
            {
                if (player == null && Time.frameCount - searchedFrame > 60)
                { searchedFrame = Time.frameCount; player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>(); }
                return player;
            }
        }

        public static int Blocker(Transform self, Vector3 heading, float reach, float clearance)
        {
            var p = self.position; var viewer = Viewer;
            // The player gets extra room: people yield before they would brush the camera.
            if (viewer != null && WorkRoute.Ahead(p, heading, viewer.transform.position, reach + .5f, clearance + .1f)) return Player;
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                var b = bodies[i];
                if (b == null) { bodies.RemoveAt(i); continue; }
                if (b == self || b.IsChildOf(self)) continue;
                if (WorkRoute.Ahead(p, heading, b.position, reach, clearance)) return Person;
            }
            return Free;
        }

        // A sidestep target is usable when it is on walkable ground and nobody stands there.
        public static bool Walkable(Transform self, Vector3 point)
        {
            var viewer = Viewer;
            if (viewer != null && (viewer.transform.position - point).sqrMagnitude < .5f) return false;
            foreach (var b in bodies) if (b != null && b != self && !b.IsChildOf(self) && (b.position - point).sqrMagnitude < .36f) return false;
            if (Physics.CheckSphere(point + Vector3.up * .9f, .28f, ~0, QueryTriggerInteraction.Ignore)) return false;
            return Physics.Raycast(point + Vector3.up * .5f, Vector3.down, .8f, ~0, QueryTriggerInteraction.Ignore);
        }

        // A shortcut past a blocked waypoint is taken only when no wall, partition or closed
        // door lies on the straight line; people and the player themselves do not count.
        public static bool Clear(Transform self, Vector3 from, Vector3 to)
        {
            var start = from + Vector3.up * .9f; var line = to + Vector3.up * .9f - start;
            if (line.sqrMagnitude < 1e-4f) return true;
            foreach (var hit in Physics.SphereCastAll(start, .2f, line.normalized, line.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = hit.collider.transform;
                if (t.IsChildOf(self) || t.GetComponentInParent<FirstPersonController>() != null) continue;
                bool person = false;
                foreach (var b in bodies) if (b != null && t.IsChildOf(b)) { person = true; break; }
                if (!person) return false;
            }
            return true;
        }

        // Automatic gangway doors also open for people walking through the train.
        public static bool NearDoor(float x, float reach)
        {
            foreach (var b in bodies)
            {
                if (b == null) continue;
                var p = b.position;
                if (Mathf.Abs(p.x - x) < reach && Mathf.Abs(p.z) < 1.5f && p.y > 1) return true;
            }
            return false;
        }

        // Conservative: anything within the view cone or very close counts as seen, ignoring occlusion.
        public static bool Seen(Vector3 point)
        {
            var viewer = Viewer;
            if (viewer == null || viewer.View == null) return false;
            var eye = viewer.View.transform.position;
            float distance = Vector3.Distance(eye, point);
            if (distance < 5) return true;
            if (distance > 85) return false; // Beyond the character LOD cull distance.
            var v = viewer.View.WorldToViewportPoint(point + Vector3.up);
            return v.z > 0 && v.x > -.2f && v.x < 1.2f && v.y > -.2f && v.y < 1.2f;
        }
    }
}
