using System;
using System.Linq;
using UnityEngine;

namespace GameVSM
{
    // The single owner of all LOD animators. Routes ask for movement or a pose; they never
    // drive an Animator themselves. Translation and playback use the same measured speed.
    public sealed class PassengerAnimation : MonoBehaviour
    {
        public Animator[] Animators;
        public const float WalkMetresPerSecond = .85f;
        public const float StartStopSeconds = .6f;
        public const float SeatSeconds = 1.2f;
        public const float TurnDegreesPerSecond = 90 / .8f;
        public string State { get; private set; } = "Idle";
        public float ActualSpeed { get; private set; }
        public float PlaybackSpeed { get; private set; } = 1;
        public bool Seated => State is "Seated" or "Phone" or "UnwellSeated";
        public bool PoseTransition => State is "SitDown" or "StandUp";
        public bool Settling => PoseTransition || State is "StartWalk" or "StopWalk" or "StopWalkRight" or "TurnLeft" or "TurnRight";
        float speed, phase, elapsed, stopSpeed, turnDuration;
        Quaternion turnStart, turnEnd;
        bool initialized, carry;
        Transform[][] arms;
        Transform[] fingers;

        void Awake() => Initialize();
        public void Initialize()
        {
            if (initialized) return;
            if (Animators == null || Animators.Length == 0) Animators = GetComponentsInChildren<Animator>(true);
            Animators = Animators.Where(a => a != null).ToArray();
            arms = new Transform[Animators.Length][];
            fingers = new Transform[Animators.Length];
            string[] names = { "RightArm", "RightForeArm", "RightHand" };
            for (int i = 0; i < Animators.Length; i++)
            {
                var animator = Animators[i];
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var bones = animator.GetComponentsInChildren<Transform>(true);
                arms[i] = names.Select(n => bones.FirstOrDefault(b => b.name.EndsWith(":" + n) || b.name.EndsWith("_" + n))).ToArray();
                fingers[i] = bones.FirstOrDefault(b => b.name.EndsWith("RightHandMiddle1"));
            }
            initialized = true;
            SetState("Idle", 0, WorkRoute.Seed(transform.position));
        }

        public bool MoveTowards(Vector3 target, float maximumSpeed, bool carrying, float dt, bool stopAtTarget = true)
        {
            Initialize();
            if (PoseTransition) { TickPose(dt); return false; }
            carry = carrying;
            var delta = target - transform.position;
            if (State is "StopWalk" or "StopWalkRight")
            {
                float u0 = Mathf.Clamp01(elapsed / StartStopSeconds);
                elapsed = Mathf.Min(StartStopSeconds, elapsed + dt);
                float u1 = elapsed / StartStopSeconds;
                float area(float u) => u - u * u * u + .5f * u * u * u * u;
                Advance(target, stopSpeed * StartStopSeconds * (area(u1) - area(u0)), dt);
                SetRate(ClipLength(State) / StartStopSeconds);
                if (elapsed < StartStopSeconds) return false;
                speed = ActualSpeed = 0; SetState("Idle", .08f);
                return delta.magnitude < .03f || Vector3.Distance(transform.position, target) < .03f;
            }
            if (delta.magnitude <= .015f) { Idle(false, dt); return !Settling; }
            var flat = new Vector3(delta.x, 0, delta.z);
            if (flat.sqrMagnitude > .0001f)
            {
                var facing = Quaternion.LookRotation(flat);
                float angle = Quaternion.Angle(transform.rotation, facing);
                if (angle > 12 && speed < .1f)
                {
                    FaceTowards(facing, dt);
                    return false;
                }
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, TurnDegreesPerSecond * dt);
            }
            if (State is not ("Walk" or "CarryWalk" or "StartWalk")) SetState("StartWalk", .08f);
            if (State == "StartWalk")
            {
                // Integral of v = .85 * u²: the .6 s authored start travels exactly .17 m.
                float u0 = Mathf.Clamp01(elapsed / StartStopSeconds);
                elapsed = Mathf.Min(StartStopSeconds, elapsed + dt);
                float u1 = elapsed / StartStopSeconds;
                Advance(target, WalkMetresPerSecond * StartStopSeconds * (u1 * u1 * u1 - u0 * u0 * u0) / 3, dt);
                speed = WalkMetresPerSecond * u1 * u1;
                SetRate(ClipLength(State) / StartStopSeconds);
                if (elapsed >= StartStopSeconds) { phase = 0; SetState(carry ? "CarryWalk" : "Walk", .08f, phase); }
                return false;
            }
            maximumSpeed = Mathf.Max(0, maximumSpeed);
            if (stopAtTarget && delta.magnitude <= Mathf.Max(.025f, maximumSpeed * StartStopSeconds * .5f))
            {
                // This analytic stop ends at the waypoint after .6 s even between frame samples.
                stopSpeed = delta.magnitude * 2 / StartStopSeconds;
                SetState(phase >= .5f ? "StopWalkRight" : "StopWalk", .08f);
                SetRate(ClipLength(State) / StartStopSeconds);
                return false;
            }
            speed = Mathf.MoveTowards(speed, maximumSpeed, WalkMetresPerSecond / StartStopSeconds * dt);
            Advance(target, speed * dt, dt);
            string walk = carry ? "CarryWalk" : "Walk";
            if (State != walk) SetState(walk, .15f, phase);
            SetRate(ActualSpeed / WalkMetresPerSecond);
            phase = Mathf.Repeat(phase + dt * PlaybackSpeed / Mathf.Max(.01f, ClipLength(walk)), 1);
            return !stopAtTarget && Vector3.Distance(transform.position, target) <= .015f;
        }
        void Advance(Vector3 target, float distance, float dt)
        {
            var before = transform.position;
            transform.position = Vector3.MoveTowards(before, target, Mathf.Max(0, distance));
            ActualSpeed = dt > 0 ? Vector3.Distance(before, transform.position) / dt : 0;
        }

        public void Idle(bool work, float dt)
        {
            Initialize();
            if (PoseTransition) { TickPose(dt); return; }
            if (State is "Walk" or "CarryWalk" or "StartWalk")
                SetState(phase >= .5f ? "StopWalkRight" : "StopWalk", .1f);
            speed = ActualSpeed = 0;
            if (State is "StopWalk" or "StopWalkRight")
            {
                SetRate(ClipLength(State) / StartStopSeconds); elapsed += dt;
                if (elapsed < StartStopSeconds) return;
            }
            string idle = work ? "Work" : "Idle";
            if (State != idle) SetState(idle, .2f, WorkRoute.Seed(transform.position));
            SetRate(1);
        }

        // Finish a planted stop before turning. The root rotates with the authored stepping
        // action instead of spinning an idle body or translating sideways around a corner.
        public bool FaceTowards(Quaternion facing, float dt)
        {
            if (PoseTransition) { TickPose(dt); return false; }
            if (State is "Walk" or "CarryWalk" or "StartWalk" or "StopWalk" or "StopWalkRight")
            {
                Idle(false, dt);
                if (State != "Idle") return false;
            }
            float signed = Mathf.DeltaAngle(transform.eulerAngles.y, facing.eulerAngles.y);
            if (Mathf.Abs(signed) < .5f)
            {
                transform.rotation = facing;
                if (State is "TurnLeft" or "TurnRight") SetState("Idle", .08f);
                return true;
            }
            string turn = signed < 0 ? "TurnLeft" : "TurnRight";
            if (State != turn || Quaternion.Angle(turnEnd, facing) > 1)
            {
                turnStart = transform.rotation; turnEnd = facing;
                turnDuration = Mathf.Max(.12f, Mathf.Abs(signed) / TurnDegreesPerSecond);
                SetState(turn, .08f);
            }
            elapsed += dt;
            float u = Mathf.Clamp01(elapsed / turnDuration);
            transform.rotation = Quaternion.Slerp(turnStart, turnEnd, Mathf.SmoothStep(0, 1, u));
            SetRate(ClipLength(turn) / turnDuration);
            speed = ActualSpeed = 0;
            if (u < 1) return false;
            SetState("Idle", .08f); return true;
        }

        public void SitDown() { speed = ActualSpeed = 0; SetState("SitDown", .1f); SetRate(ClipLength(State) / SeatSeconds); }
        public void StandUp() { speed = ActualSpeed = 0; SetState("StandUp", .1f); SetRate(ClipLength(State) / SeatSeconds); }
        public void TickPose(float dt)
        {
            if (!PoseTransition) return;
            elapsed += dt;
            if (elapsed >= SeatSeconds) SetState(State == "SitDown" ? "Seated" : "Idle", .08f);
        }
        public void SeatedPose(bool phone, bool unwell)
        {
            string pose = unwell ? "UnwellSeated" : phone ? "Phone" : "Seated";
            if (State != pose) SetState(pose, .25f);
            SetRate(1);
        }
        public void RestoreSeated() { Initialize(); speed = ActualSpeed = 0; SetState("Seated", 0); SampleNow(); }
        public void RestoreStanding() { Initialize(); speed = ActualSpeed = 0; SetState("Idle", 0); SampleNow(); }

        void SetState(string state, float blend, float at = 0)
        {
            State = state; elapsed = 0;
            if (Animators == null) return;
            foreach (var animator in Animators)
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                animator.speed = 1;
                if (blend <= 0) animator.Play(state, 0, at);
                else animator.CrossFadeInFixedTime(state, blend, 0, at * ClipLength(state));
            }
            PlaybackSpeed = 1;
        }
        void SetRate(float value)
        {
            PlaybackSpeed = value;
            foreach (var animator in Animators) if (animator != null) animator.speed = value;
        }
        float ClipLength(string state)
        {
            foreach (var animator in Animators ?? Array.Empty<Animator>())
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                var clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.EndsWith(state, StringComparison.Ordinal));
                if (clip != null) return clip.length;
            }
            return state is "SitDown" or "StandUp" ? SeatSeconds : state is "StartWalk" or "StopWalk" or "StopWalkRight" ? StartStopSeconds : 1;
        }
        void SampleNow() { foreach (var animator in Animators) if (animator != null && animator.isActiveAndEnabled) animator.Update(0); }

        public Vector3 RightPalm
        {
            get
            {
                Initialize();
                if (arms.Length == 0 || arms[0][2] == null) return transform.position + transform.right * .28f + Vector3.up * .85f;
                var hand = arms[0][2];
                var forward = fingers[0] != null ? (fingers[0].position - hand.position).normalized : -transform.up;
                return hand.position + forward * .085f;
            }
        }

        // Small hand correction after the skeletal clip. Reach is clamped to real arm lengths;
        // both LOD skeletons get the same target, while leg/seat poses remain clip-owned.
        public void ReachRightPalm(Vector3 palm)
        {
            Initialize();
            for (int i = 0; i < arms.Length; i++)
            {
                var b = arms[i]; if (b.Any(x => x == null)) continue;
                var hand = b[2];
                // Keep the wrist close to its authored orientation. The forearm may bend to
                // the handle, but it must not turn the fingers sideways through the case.
                Vector3 fingerDirection = fingers[i] != null ?
                    (fingers[i].position - hand.position).normalized : -transform.up;
                Quaternion gripRotation = Quaternion.FromToRotation(fingerDirection, -transform.up) * hand.rotation;
                Vector3 palmOffset = -transform.up * .085f;
                for (int pass = 0; pass < 2; pass++)
                {
                    Vector3 target = palm - palmOffset, shoulder = b[0].position;
                    float upper = Vector3.Distance(shoulder, b[1].position), lower = Vector3.Distance(b[1].position, hand.position);
                    if (upper < .01f || lower < .01f) break;
                    Vector3 direction = target - shoulder;
                    float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(upper - lower) + .002f, upper + lower - .002f);
                    direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.down;
                    // Keep the elbow below the shoulder at a low handle. A pure sideways pole
                    // lifts it into an unnatural square bend.
                    Vector3 outward = Vector3.ProjectOnPlane(
                        Vector3.down + transform.right * .8f, direction).normalized;
                    float along = (upper * upper + distance * distance - lower * lower) / (2 * distance);
                    Vector3 elbow = shoulder + direction * along + outward * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
                    b[0].rotation = Quaternion.FromToRotation(b[1].position - shoulder, elbow - shoulder) * b[0].rotation;
                    b[1].rotation = Quaternion.FromToRotation(hand.position - b[1].position, shoulder + direction * distance - b[1].position) * b[1].rotation;
                    hand.rotation = gripRotation;
                }
            }
        }
    }
}
