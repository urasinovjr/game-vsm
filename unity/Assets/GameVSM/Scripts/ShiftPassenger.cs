using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameVSM
{
    // A passenger keeps one body and one bag from queue to seat to the station exit.
    // Restores may place them immediately; ordinary boarding and alighting always animate.
    public sealed class ShiftPassenger : MonoBehaviour
    {
        enum SeatPhase { None, Aligning, Sitting, Retrieving, Standing }
        public bool Seated { get; private set; }
        // The optional walk from the platform to the city never holds a scenario choice.
        public bool Travelling => path.Count > 0 || seatPhase != SeatPhase.None;
        public bool Departed { get; private set; }
        public bool WaitingForPlayer { get; private set; }
        public bool OnPhone;
        public bool Unwell;
        public Vector3 Seat;
        public float SeatYaw;
        public Vector3 SeatRoot => Seat + Quaternion.Euler(0, SeatYaw, 0) * Vector3.forward * .32f;
        public LuggageVisual Luggage => bag;
        readonly Queue<Vector3> path = new();
        readonly Queue<Vector3> exit = new();
        bool leaveAtEnd, bagged, bagStowed, sitAtEnd;
        float blocked, pace = .85f, delay, seatElapsed;
        Vector3? aside;
        PassengerAnimation animation;
        SeatPhase seatPhase;
        LuggageVisual bag;
        Vector3 bagFrom;
        Transform phone;

        void Awake()
        {
            var ambient = GetComponent<PassengerMotion>();
            if (ambient != null) ambient.enabled = false;
            animation = GetComponent<PassengerAnimation>();
            if (animation == null) animation = gameObject.AddComponent<PassengerAnimation>();
            animation.Initialize();
            var handset = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handset.name = "Телефон пассажира"; phone = handset.transform;
            phone.SetParent(transform, false); phone.localScale = new Vector3(.065f, .135f, .012f);
            Destroy(handset.GetComponent<Collider>());
            var finish = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            finish.SetColor("_BaseColor", new Color(.025f, .035f, .045f)); finish.SetFloat("_Smoothness", .6f);
            handset.GetComponent<Renderer>().sharedMaterial = finish;
            handset.SetActive(false);
            bag = LuggageVisual.Create(transform, LuggageKind.Holdall);
            bag.gameObject.SetActive(false);
            pace *= .96f + .08f * WorkRoute.Seed(transform.position);
        }
        void OnEnable() { if (!Seated) LifeTraffic.Add(transform); }
        void OnDisable() => LifeTraffic.Remove(transform);

        public void Walk(IEnumerable<Vector3> points, bool sit, float startDelay = 0, bool leave = false)
        {
            Return(); path.Clear(); exit.Clear(); aside = null;
            var positions = points.ToArray();
            for (int i = 0; i < positions.Length; i++)
                path.Enqueue(sit && i == positions.Length - 1 ? SeatRoot : positions[i]);
            sitAtEnd = sit; delay = startDelay; leaveAtEnd = leave; blocked = 0; WaitingForPlayer = false;
            if (leave) bagged = true;
            if (!Seated) { SetCollider(true); LifeTraffic.Add(transform); }
            if (sit && path.Count == 0) Sit();
        }
        public void Leave()
        {
            Return(); path.Clear(); bagged = true; sitAtEnd = false; leaveAtEnd = true;
            if (Seated) BeginStanding(); else BeginExit();
        }
        public void Vanish()
        {
            path.Clear(); exit.Clear(); seatPhase = SeatPhase.None; Departed = true;
            WaitingForPlayer = false; gameObject.SetActive(false);
        }
        void Return() { if (Departed) { Departed = false; gameObject.SetActive(true); } }
        void BeginExit()
        {
            Departed = true; exit.Clear(); aside = null;
            float seed = WorkRoute.Seed(transform.position), x = transform.position.x, y = transform.position.y;
            float lane = seed < .55f ? -3.9f - seed : -4.9f + seed * .3f;
            delay = .4f + seed;
            exit.Enqueue(new Vector3(x - 2.2f - seed * 2, y, lane));
            exit.Enqueue(new Vector3(-100, y, lane));
            LifeTraffic.Add(transform);
        }

        // Continue an already-started boarding route instead of snapping everybody into chairs.
        public void Sit()
        {
            if (Seated || seatPhase != SeatPhase.None) return;
            Return(); sitAtEnd = true;
            if (path.Count > 0) return;
            if (Vector3.Distance(transform.position, SeatRoot) > .025f) path.Enqueue(SeatRoot);
            else seatPhase = SeatPhase.Aligning;
        }
        public void SitImmediately()
        {
            Return(); path.Clear(); exit.Clear(); seatPhase = SeatPhase.None;
            transform.SetPositionAndRotation(SeatRoot, Quaternion.Euler(0, SeatYaw, 0));
            animation.RestoreSeated(); Seated = true; bagged = bagStowed = true;
            WaitingForPlayer = false; SetCollider(false); LifeTraffic.Remove(transform);
            PlaceBag(StowGrip, Quaternion.Euler(0, SeatYaw, 0));
        }
        public void StandAt(Vector3 position, float yaw)
        {
            Return(); path.Clear(); exit.Clear(); seatPhase = SeatPhase.None;
            Seated = false; OnPhone = Unwell = false; bagged = true; bagStowed = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            animation.RestoreStanding(); SetCollider(true); LifeTraffic.Add(transform);
        }
        void SetCollider(bool value) { var collider = GetComponent<Collider>(); if (collider != null) collider.enabled = value; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (seatPhase != SeatPhase.None) { UpdateSeat(dt); return; }
            if (delay > 0)
            {
                delay -= dt;
                if (Seated) animation.SeatedPose(OnPhone, Unwell); else animation.Idle(false, dt);
                return;
            }
            var route = path.Count > 0 ? path : exit;
            if (route.Count > 0 && Seated) { BeginStanding(); return; }
            if (route.Count == 0)
            {
                if (Seated) animation.SeatedPose(OnPhone, Unwell); else animation.Idle(false, dt);
                if (Departed && !LifeTraffic.Seen(transform.position)) gameObject.SetActive(false);
                return;
            }
            if (route == exit && !LifeTraffic.Seen(transform.position) && LifeTraffic.Viewer != null &&
                (LifeTraffic.Viewer.transform.position - transform.position).sqrMagnitude > 625)
            { gameObject.SetActive(false); return; }
            var target = aside ?? route.Peek(); var delta = target - transform.position;
            var flat = new Vector3(delta.x, 0, delta.z);
            int blocker = flat.sqrMagnitude > .002f ? LifeTraffic.Blocker(transform, flat, .95f, .5f) : LifeTraffic.Free;
            WaitingForPlayer = blocker == LifeTraffic.Player && route == path;
            if (blocker != LifeTraffic.Free) blocked += dt; else blocked = 0;
            if (blocker == LifeTraffic.Player && route == path && path.Count == 1 && !sitAtEnd &&
                (flat.magnitude < 1.4f || blocked > WorkRoute.WaitTimeout))
            {
                path.Clear(); WaitingForPlayer = false; blocked = 0; animation.Idle(false, dt);
                if (leaveAtEnd) BeginExit(); return;
            }
            if (blocker != LifeTraffic.Free && route == path && path.Count > 1 && blocked > 1.5f && flat.magnitude < 1.6f &&
                Mathf.Repeat(blocked, 1.5f) < dt)
            {
                var next = path.ToArray()[1];
                if (LifeTraffic.Clear(transform, transform.position, next)) { path.Dequeue(); blocked = 0; return; }
            }
            if (blocker == LifeTraffic.Person && route == exit && aside == null && blocked > .6f)
            {
                var step = WorkRoute.Aside(transform.position, flat, .8f);
                if (LifeTraffic.Walkable(transform, step)) { aside = step; blocked = 0; return; }
            }
            if (blocker == LifeTraffic.Player || (blocker == LifeTraffic.Person && blocked < (route == exit ? 8 : 6)))
            { animation.Idle(false, dt); return; }
            bool stop = route.Count == 1 || aside.HasValue;
            if (!stop)
            {
                var next = route.ToArray()[1] - target;
                stop = Vector3.Angle(flat, next) > 25;
            }
            bool reached = animation.MoveTowards(target, pace, bagged && !bagStowed, dt, stop);
            if (!reached) return;
            if (aside != null) { aside = null; return; }
            route.Dequeue();
            if (route == path && path.Count == 0)
            {
                if (sitAtEnd) seatPhase = SeatPhase.Aligning;
                else if (leaveAtEnd) BeginExit();
            }
        }

        void BeginStanding()
        {
            OnPhone = Unwell = false; seatElapsed = 0;
            if (bagged && bagStowed)
            {
                bagFrom = bag.Grip.position; seatPhase = SeatPhase.Retrieving;
                animation.SeatedPose(false, false);
            }
            else { Seated = false; animation.StandUp(); seatPhase = SeatPhase.Standing; }
        }
        void UpdateSeat(float dt)
        {
            if (seatPhase == SeatPhase.Aligning)
            {
                if (!animation.FaceTowards(Quaternion.Euler(0, SeatYaw, 0), dt)) return;
                seatElapsed = 0; bagFrom = animation.RightPalm;
                animation.SitDown(); seatPhase = SeatPhase.Sitting; return;
            }
            seatElapsed += dt;
            if (seatPhase == SeatPhase.Retrieving)
            {
                if (seatElapsed < .6f) return;
                bagStowed = false; Seated = false; seatElapsed = 0;
                animation.StandUp(); seatPhase = SeatPhase.Standing; return;
            }
            animation.TickPose(dt);
            if (animation.PoseTransition) return;
            if (seatPhase == SeatPhase.Sitting)
            {
                Seated = true; bagStowed = bagged;
                SetCollider(false); LifeTraffic.Remove(transform);
            }
            else
            {
                Seated = false; SetCollider(true); LifeTraffic.Add(transform);
                if (leaveAtEnd && path.Count == 0) BeginExit();
            }
            seatPhase = SeatPhase.None;
        }

        Vector3 StowGrip
        {
            get
            {
                var rotation = Quaternion.Euler(0, SeatYaw, 0);
                float height = bag == null ? .32f : bag.Grip.localPosition.y - bag.Ground.localPosition.y;
                return Seat + rotation * new Vector3(.30f, height, .22f);
            }
        }
        void LateUpdate()
        {
            if (bag == null || phone == null) return;
            bag.gameObject.SetActive(bagged);
            if (bagged)
            {
                if (seatPhase == SeatPhase.Sitting)
                {
                    float t = Mathf.SmoothStep(0, 1, seatElapsed / PassengerAnimation.SeatSeconds);
                    animation.ReachRightPalm(Vector3.Lerp(bagFrom, StowGrip, t));
                    PlaceBag(animation.RightPalm, transform.rotation);
                }
                else if (seatPhase == SeatPhase.Retrieving)
                {
                    float t = Mathf.SmoothStep(0, 1, seatElapsed / .6f);
                    animation.ReachRightPalm(Vector3.Lerp(bagFrom, animation.RightPalm, t));
                    PlaceBag(animation.RightPalm, transform.rotation);
                }
                else if (bagStowed) PlaceBag(StowGrip, Quaternion.Euler(0, SeatYaw, 0));
                else PlaceBag(animation.RightPalm, transform.rotation);
            }
            phone.gameObject.SetActive(Seated && OnPhone);
            if (Seated && OnPhone)
            {
                phone.position = animation.RightPalm + transform.right * .018f;
                phone.rotation = Quaternion.LookRotation(transform.right, Vector3.up);
            }
        }
        void PlaceBag(Vector3 grip, Quaternion rotation)
        {
            bag.transform.rotation = rotation;
            bag.transform.position += grip - bag.Grip.position;
        }
    }
}
