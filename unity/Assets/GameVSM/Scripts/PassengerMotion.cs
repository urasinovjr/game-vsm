using System;
using UnityEngine;

namespace GameVSM
{
    // Background routes and the task cast use one animation owner and one walking cadence.
    public sealed class PassengerMotion : MonoBehaviour
    {
        public Animator Animator;
        public Animator[] DetailAnimators;
        public LifeRole Role;
        public RouteStop[] Route = Array.Empty<RouteStop>();
        public bool Loop = true;
        public float Speed = .75f;
        public bool AfterArrival;
        public Transform Cart;
        public bool Moving => animation != null && animation.ActualSpeed > .02f;

        int index, visit;
        float seed, dwell, waited;
        bool detour, parked, working;
        Vector3 detourPoint;
        Quaternion faceTo;
        ShiftExperience experience;
        bool experienceSearched;
        PassengerAnimation animation;
        LuggageVisual caseVisual;

        void Start()
        {
            seed = WorkRoute.Seed(transform.position);
            animation = GetComponent<PassengerAnimation>();
            if (animation == null) animation = gameObject.AddComponent<PassengerAnimation>();
            animation.Initialize();
            caseVisual = GetComponentInChildren<LuggageVisual>(true);
            Speed *= .92f + .16f * seed;
            faceTo = transform.rotation;
            dwell = Route.Length > 0 ? 1 + 6 * seed : 0;
        }
        void OnEnable() { LifeTraffic.Add(transform); LifeTraffic.Add(Cart); }
        void OnDisable() { LifeTraffic.Remove(transform); LifeTraffic.Remove(Cart); }

        void Update()
        {
            if (Route.Length == 0 || animation == null) return;
            float dt = Time.deltaTime;
            if (AfterArrival && !ArrivalClear()) { animation.Idle(false, dt); return; }
            if (parked) { Park(dt); return; }
            if (dwell > 0)
            {
                dwell -= dt;
                if (animation.FaceTowards(faceTo, dt)) animation.Idle(working, dt);
                return;
            }
            var stop = Route[index];
            Vector3 target = detour ? detourPoint : stop.Point;
            Vector3 delta = target - transform.position; delta.y = 0;
            if (delta.sqrMagnitude < .0004f) { Reach(stop); return; }
            int blocker = LifeTraffic.Blocker(transform, delta, Cart != null ? 2.1f : 1.1f, .55f);
            if (blocker != LifeTraffic.Free)
            {
                animation.Idle(false, dt); waited += dt;
                if (blocker == LifeTraffic.Person && !detour && waited > .7f + seed)
                {
                    var aside = WorkRoute.Aside(transform.position, delta, .8f);
                    if (LifeTraffic.Walkable(transform, aside)) { detour = true; detourPoint = aside; waited = 0; }
                }
                if (WorkRoute.GiveUp(waited, seed)) { waited = 0; detour = false; Advance(); }
                return;
            }
            waited = 0;
            bool stopAtPoint = detour || stop.Kind != StopKind.Pass || index == Route.Length - 1;
            if (!stopAtPoint && index + 1 < Route.Length)
                stopAtPoint = Vector3.Angle(delta, Route[index + 1].Point - target) > 25;
            if (animation.MoveTowards(target, Speed, caseVisual != null, dt, stopAtPoint)) Reach(stop);
        }

        void LateUpdate()
        {
            if (animation != null && caseVisual != null && caseVisual.Kind == LuggageKind.RollingCase)
                animation.ReachRightPalm(caseVisual.Grip.position);
        }
        void Reach(RouteStop stop)
        {
            if (detour) { detour = false; return; }
            if (stop.Face) faceTo = Quaternion.Euler(0, stop.Yaw, 0);
            if (stop.Kind == StopKind.End) { parked = true; working = false; animation.Idle(false, Time.deltaTime); return; }
            dwell = WorkRoute.Dwell(stop.Dwell, seed, visit);
            working = stop.Kind == StopKind.Work && dwell > 0;
            Advance();
        }
        void Advance()
        {
            int next = WorkRoute.Next(index, Route.Length, Loop);
            if (next < 0) { parked = true; return; }
            if (next <= index) visit++;
            index = next;
        }
        void Park(float dt)
        {
            animation.Idle(false, dt);
            if (LifeTraffic.Seen(transform.position) || LifeTraffic.Seen(Route[0].Point)) return;
            transform.position = Route[0].Point; animation.RestoreStanding();
            parked = false; index = WorkRoute.Next(0, Route.Length, true);
            visit++; dwell = WorkRoute.Dwell(Route[0].Dwell, seed, visit);
        }
        bool ArrivalClear()
        {
            if (!experienceSearched) { experienceSearched = true; experience = FindAnyObjectByType<ShiftExperience>(); }
            if (experience == null) return true;
            var people = experience.Passengers;
            for (int i = 0; i < people.Count; i++) if (people[i].Travelling) return false;
            return true;
        }
    }
}
