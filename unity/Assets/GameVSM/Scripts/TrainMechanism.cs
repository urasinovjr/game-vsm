using UnityEngine;

namespace GameVSM
{
    public sealed class TrainMechanism : MonoBehaviour
    {
        public string Kind;
        public string Label;
        public float Range = 2.7f;
        public Vector3 Axis;
        public float AngleDegrees;
        public Vector3 Plug;
        public Vector3 Slide;
        public bool TrainMoving;
        public bool Automatic;
        public float TriggerX;
        public bool Blocked { get; private set; }
        public float Phase { get; private set; }
        public bool IsOpen => target > .5f;
        Quaternion closedRotation;
        Vector3 closedPosition;
        Renderer[] surfaces;
        float target;

        void Awake()
        {
            closedRotation = transform.localRotation;
            closedPosition = transform.localPosition;
            surfaces = GetComponentsInChildren<Renderer>();
        }

        public Bounds WorldBounds()
        {
            Bounds result = new Bounds(transform.position, Vector3.zero);
            if (surfaces == null || surfaces.Length == 0) return result;
            result = surfaces[0].bounds;
            for (int i = 1; i < surfaces.Length; i++) result.Encapsulate(surfaces[i].bounds);
            return result;
        }

        public bool Toggle()
        {
            if (Automatic || (Kind == "sliding_entry_door" && TrainMoving)) return false;
            target = IsOpen ? 0 : 1;
            Blocked = false;
            return true;
        }

        public void SetOpen(bool open)
        {
            if (Automatic) return;
            target = open && !TrainMoving ? 1 : 0;
        }

        public void Advance(float deltaTime, Vector3 player, float radius)
        {
            if (Automatic) target = (Mathf.Abs(player.x - TriggerX) < 2.5f && Mathf.Abs(player.z) < 1.5f && player.y > 1) || LifeTraffic.NearDoor(TriggerX, 2) ? 1 : 0;
            if (Mathf.Abs(Phase - target) < .00001f) return;
            float next = Mathf.MoveTowards(Phase, target, Mathf.Max(0, deltaTime) * 1.4f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(next - Phase) / .015f));
            float start = Phase;
            for (int i = 1; i <= steps; i++)
            {
                float phase = Mathf.Lerp(start, next, (float)i / steps);
                Pose(phase);
                Bounds bounds = WorldBounds();
                Vector3 closest = bounds.ClosestPoint(player);
                bool vertical = player.y + 1.65f > bounds.min.y && player.y < bounds.max.y;
                if (vertical && Vector2.Distance(new Vector2(player.x, player.z),
                    new Vector2(closest.x, closest.z)) < radius)
                {
                    Pose(Phase);
                    Blocked = true;
                    return;
                }
                Phase = phase;
                Blocked = false;
            }
        }

        void Pose(float phase)
        {
            if (Kind == "sliding_entry_door")
                transform.localPosition = closedPosition + Plug * Mathf.Clamp01(phase / .25f)
                    + Slide * Mathf.Clamp01((phase - .25f) / .75f);
            else
                transform.localRotation = closedRotation * Quaternion.AngleAxis(AngleDegrees * phase, Axis);
        }
    }
}
