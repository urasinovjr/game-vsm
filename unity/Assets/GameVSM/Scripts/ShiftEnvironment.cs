using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameVSM
{
    // The server chooses the next task; this component finishes the physical journey before
    // its platform interactions become available. The train and its interior never move.
    public sealed class ShiftEnvironment : MonoBehaviour
    {
        public FirstPersonController Player;
        public GameObject Current;
        public string Location = "Depot";
        public bool Transitioning { get; private set; }
        public string TransitionText { get; private set; } = "";
        public float TravelSpeed => Transitioning ? journeySpeed : Current != null && Location == "Route" ? Current.GetComponent<RouteMotion>()?.Speed ?? 0 : 0;
        public event Action<string> Changed;
        public event Action Presented;
        ShiftClient client;
        string lastNode, attempt;
        Coroutine journey;
        Action restoreScenery;
        TrainMechanism[] doors;
        float journeySpeed, fade;
        public static string ForNode(string node) => node switch
        {
            "brief" or "board" or "inspect" or "inspection_fix" or "equipment" => "Depot",
            "platform" or "boarding1" or "boarding1_fix" or "boarding2" or "boarding2_fix" or "depart" => "Moskovsky",
            "alight" or "final_check" or "handover" or "complete" => "Leningradsky",
            _ => "Route"
        };
        public static string Title(string place) => place switch
        {
            "Depot" => "Металлострой · приёмка состава",
            "Moskovsky" => "Санкт-Петербург · Московский вокзал",
            "Leningradsky" => "Москва · Ленинградский вокзал",
            _ => "В пути · Санкт-Петербург — Москва"
        };
        public bool IsPresented(string node) => !Transitioning && Location == ForNode(node);

        void Start()
        {
            client = GetComponent<ShiftClient>();
            if (client != null) { client.Changed += Synchronize; Synchronize(); }
        }
        void OnDestroy() { if (client != null) client.Changed -= Synchronize; }
        void Synchronize()
        {
            string node = (string)client.Attempt?["state"]?["node"];
            string id = (string)client.Attempt?["id"];
            if (string.IsNullOrEmpty(node) || node == lastNode && id == attempt) return;
            bool resume = lastNode == null || id != attempt;
            lastNode = node; attempt = id;
            string next = ForNode(node);
            if (!resume && next != Location)
            {
                TravelTo(next);
                return;
            }
            Vector3 before = Player.transform.position;
            Show(next);
            if (!resume) Player.Teleport(before);
            // Restoring a saved task does not replay departure or put a conductor on moving tracks.
            var task = client.Attempt?["task"] as JObject;
            bool inside = (string)task?["location"] == "train";
            if (resume && (node is "inspect" or "inspection_fix" or "equipment" || Location == "Route" || inside))
                Player.Teleport(new Vector3(47.6f, 1.31f, -.24f));
            else if (resume && node is ("boarding1" or "boarding1_fix" or "boarding2" or "boarding2_fix" or "depart" or "alight"))
                Player.Teleport(new Vector3(48.4f, 1.32f, -4.4f));
            Presented?.Invoke();
        }

        // Also used by scene review: unlike Show, this keeps the conductor in the fixed train.
        public void TravelTo(string destination)
        {
            if (Transitioning || Location == destination) return;
            if (Resources.Load<GameObject>("Environments/" + destination) == null)
                throw new InvalidOperationException("Missing environment: " + destination);
            Transitioning = true;
            TransitionText = "Закрываются двери";
            doors = FindObjectsByType<TrainMechanism>(FindObjectsInactive.Include)
                .Where(d => !d.Automatic && d.Kind == "sliding_entry_door").ToArray();
            journey = StartCoroutine(Journey(destination));
        }
        IEnumerator Journey(string destination)
        {
            // Do not pull away while a person can still step out, or while a closing leaf is blocked.
            while (true)
            {
                bool aboard = Aboard();
                // A late network reply must not strand somebody who stepped out after confirming.
                // If they step out again during closure, reopen and wait; no scenery moves yet.
                foreach (var d in doors) if (d != null) { d.TrainMoving = aboard; d.SetOpen(!aboard && d.WorldBounds().center.z < 0); }
                if (aboard && doors.All(d => d == null || d.Phase <= .01f)) break;
                TransitionText = !aboard ? "Войдите в вагон: состав готовится к движению" :
                    doors.Any(d => d != null && d.Blocked) ? "Отойдите от дверной створки" : "Закрываются двери";
                yield return null;
            }
            if (Location != "Route")
            {
                TransitionText = Location == "Depot" ? "Подача на Московский вокзал" : "Поезд отправляется";
                restoreScenery = SuspendScenery();
                // A short visible pull-away, then an explicit time cut; no claim of a continuous railway map.
                const float duration = 3;
                Vector3 origin = Current.transform.position;
                for (float t = 0; t < duration; t += Time.deltaTime)
                {
                    journeySpeed = 4 * t;
                    Current.transform.position = origin + Vector3.left * (2 * t * t);
                    yield return null;
                }
                journeySpeed = 12;
                Current.transform.position = origin + Vector3.left * 18;
                yield return FadeTo(1, true);
                Place("Route", false);
                var route = Current.GetComponent<RouteMotion>();
                if (route != null) route.Speed = journeySpeed;
                yield return FadeTo(0);
            }
            if (destination != "Route")
            {
                TransitionText = destination == "Moskovsky" ? "Прибытие на Московский вокзал" : "Прибытие на Ленинградский вокзал";
                var route = Current.GetComponent<RouteMotion>();
                if (route != null)
                {
                    route.CruiseSpeed = 12; route.Acceleration = 9;
                    while (Mathf.Abs(route.Speed - 12) > .05f) { journeySpeed = route.Speed; yield return null; }
                }
                journeySpeed = 12;
                yield return FadeTo(1);
                Place(destination, false);
                restoreScenery = SuspendScenery();
                const float duration = 5;
                Current.transform.position = Vector3.right * 30;
                // Fade-in is part of the approach, so the arriving platform is never frozen at speed.
                for (float t = 0; t < duration; t += Time.deltaTime)
                {
                    float remaining = 1 - t / duration;
                    journeySpeed = 12 * remaining;
                    Current.transform.position = Vector3.right * (30 * remaining * remaining);
                    fade = Mathf.Max(0, 1 - t / .65f);
                    yield return null;
                }
                Current.transform.position = Vector3.zero;
                restoreScenery(); restoreScenery = null;
            }
            foreach (var d in doors) if (d != null) d.TrainMoving = destination == "Route";
            journeySpeed = 0; fade = 0; TransitionText = ""; Transitioning = false; journey = null;
            // ShiftExperience now places waiting passengers, opens station doors and exposes the task.
            Presented?.Invoke();
        }
        bool Aboard() => Player != null && WalkArea.InsideTrain(Player.transform.position) &&
            Mathf.Abs(Player.transform.position.z) < 1.4f;

        Action SuspendScenery()
        {
            // Moving platform colliders must never sweep through the fixed train. Walkers use world
            // waypoints, so pause them too; their positions follow the environment until it stops.
            var colliders = Current.GetComponentsInChildren<Collider>(true).Where(c => c.enabled).ToArray();
            var walkers = Current.GetComponentsInChildren<PassengerMotion>(true).Where(c => c.enabled).ToArray();
            var areas = Current.GetComponentsInChildren<WalkArea>(true).Where(c => c.enabled).ToArray();
            foreach (var c in colliders) c.enabled = false;
            foreach (var w in walkers) w.enabled = false;
            foreach (var a in areas) a.enabled = false;
            return () =>
            {
                foreach (var c in colliders) if (c != null) c.enabled = true;
                foreach (var w in walkers) if (w != null) w.enabled = true;
                foreach (var a in areas) if (a != null) a.enabled = true;
            };
        }
        IEnumerator FadeTo(float target, bool moveScenery = false)
        {
            while (!Mathf.Approximately(fade, target))
            {
                fade = Mathf.MoveTowards(fade, target, Time.deltaTime / .65f);
                if (moveScenery) Current.transform.position += Vector3.left * (journeySpeed * Time.deltaTime);
                else if (Location == "Route") journeySpeed = Current.GetComponent<RouteMotion>()?.Speed ?? journeySpeed;
                yield return null;
            }
        }
        void OnGUI()
        {
            if (fade <= 0) return;
            int depth = GUI.depth; Color color = GUI.color;
            GUI.depth = -10000; GUI.color = new Color(.025f, .035f, .045f, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = color; GUI.depth = depth;
        }
        public void Show(string location)
        {
            if (journey != null) StopCoroutine(journey);
            if (Transitioning && Current != null) { Current.transform.position = Vector3.zero; restoreScenery?.Invoke(); }
            restoreScenery = null;
            journey = null; Transitioning = false; TransitionText = ""; fade = journeySpeed = 0;
            Place(location, true);
        }
        void Place(string location, bool positionPlayer)
        {
            if (Current != null && Location == location) return;
            var asset = Resources.Load<GameObject>("Environments/" + location);
            if (asset == null) throw new InvalidOperationException("Missing environment: " + location);
            if (Current != null) { Current.SetActive(false); Destroy(Current); }
            restoreScenery = null;
            Current = Instantiate(asset); Location = location;
            if (positionPlayer) Player.Teleport(location == "Depot" ? new Vector3(128, .03f, -5.7f) :
                location == "Route" ? new Vector3(47.6f, 1.31f, -.24f) : new Vector3(52, 1.32f, -4.5f));
            Changed?.Invoke(Title(location));
        }
    }
}
