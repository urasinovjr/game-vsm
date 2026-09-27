using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameVSM
{
    // Spatial presentation of the server graph. It never awards scores or runs deadlines.
    [RequireComponent(typeof(ShiftClient), typeof(ShiftEnvironment))]
    public sealed class ShiftExperience : MonoBehaviour
    {
        public GameObject PassengerPrefab, WomanPrefab, WorkerPrefab;
        public Transform Luggage { get; private set; }
        public IReadOnlyList<ShiftPassenger> Passengers => people;
        public string Node => (string)client.Attempt?["state"]?["node"] ?? "";
        public Vector3 TargetPosition { get; private set; }
        public string TargetLabel { get; private set; } = "";
        // Verb for the contextual hint, e.g. "Проверить связь и отметить готовность".
        public string TargetAction { get; private set; } = "";
        public bool TargetIsPerson { get; private set; }
        public string ActionText { get; private set; } = "";
        // Kept until the next step so long feedback can be reread in the task card.
        public string LastFeedback { get; private set; } = "";
        public float Distance => Vector3.Distance(player.transform.position + Vector3.up, TargetPosition);
        public float ActionProgress { get; private set; }
        public string Feedback { get; private set; } = "";
        public bool Acting { get; private set; }
        public bool WaitingForPeople => people.Any(p => p.Travelling) || (Node == "medical_support" && colleague.Travelling);
        public bool ContextOpen => contextNode == Node;
        ShiftClient client;
        ShiftEnvironment environment;
        FirstPersonController player;
        FirstPersonHands hands;
        readonly List<ShiftPassenger> people = new();
        ShiftPassenger supervisor, colleague;
        static readonly Vector3 ColleaguePost = new(23.7f, 1.3f, .7f), Gangway = new(24.6f, 1.3f, -.25f);
        string previousNode = "", attemptId = "", contextNode = "";
        Vector3 bagTarget;
        Quaternion bagRotation;
        TrainMechanism[] doors;
        bool boarded;
        float carriage3Entry, carriage4Entry;
        float feedbackUntil;
        public event Action Updated;

        void Start()
        {
            client = GetComponent<ShiftClient>(); environment = GetComponent<ShiftEnvironment>(); player = environment.Player;
            doors = FindObjectsByType<TrainMechanism>(FindObjectsInactive.Include).Where(m => !m.Automatic && m.Kind == "sliding_entry_door").ToArray();
            carriage3Entry=doors.Select(d=>d.WorldBounds().center.x).First(x=>x>24.9f && x<48.9f);
            carriage4Entry=doors.Select(d=>d.WorldBounds().center.x).First(x=>x>.3f && x<24.3f);
            BuildCast(); BuildBag();
            hands = player.GetComponentInChildren<FirstPersonHands>();
            gameObject.AddComponent<ShiftAudio>();
            client.Changed += Synchronize; environment.Presented += Synchronize; Synchronize();
        }
        void OnDestroy() { if (client != null) client.Changed -= Synchronize; if (environment != null) environment.Presented -= Synchronize; }

        ShiftPassenger Person(GameObject prefab, string name, Vector3 position)
        {
            var go = Instantiate(prefab, position, Quaternion.identity, transform); go.name = name;
            return go.AddComponent<ShiftPassenger>();
        }
        void BuildCast()
        {
            if (PassengerPrefab == null || WomanPrefab == null || WorkerPrefab == null)
                throw new InvalidOperationException("Shift cast prefabs are not configured. Apply environments to shift.");
            supervisor = Person(WorkerPrefab, "Старший проводник", new Vector3(118, .04f, -5.7f));
            supervisor.transform.rotation = Quaternion.Euler(0, 90, 0);
            // Car 4 vestibule, beside the gangway and clear of Maria's line through the car 4 door (x 22.9).
            colleague = Person(WorkerPrefab, "Коллега", ColleaguePost);
            colleague.transform.rotation = Quaternion.Euler(0, 90, 0); // Facing the gangway the conductor comes through.
            colleague.gameObject.SetActive(false);
            var allSeats = FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t=>t.name.Contains("WalkSeat_")).ToArray();
            var seats = allSeats
                .Where(t => t.position.x > 27 && t.position.x < 45 && Mathf.Abs(t.position.z - .26f) < .06f)
                .OrderByDescending(t => t.position.x).ToArray();
            if (seats.Length < 8) throw new InvalidOperationException("Could not bind eight passenger seats in car 3.");
            for (int i = 0; i < 8; i++)
            {
                var seat = seats[Mathf.Min(i * 2, seats.Length - 1)];
                var person = Person(i % 2 == 0 ? PassengerPrefab : WomanPrefab, i == 0 ? "Алексей Ветров" : i == 1 ? "Мария Лебедева" : "Пассажир " + (i + 1), new Vector3(49 + i * 1.3f, 1.3f, -4.4f));
                person.Seat = seat.position; person.SeatYaw = seat.position.x > 37 ? 270 : 90;
                people.Add(person);
                person.gameObject.SetActive(false);
            }
            // Maria travels in car 4, as specified by the server's ticket.
            var alexSeat = allSeats.First(t=>t.name.Contains("WalkSeat_012.") && t.position.x>24.9f && t.position.x<48.9f);
            people[0].Seat = alexSeat.position;
            var mariaSeat = allSeats.First(t => t.name.Contains("WalkSeat_018.") && t.position.x > .3f && t.position.x < 24.3f);
            people[1].Seat = mariaSeat.position; people[1].SeatYaw = 270;
        }
        void BuildBag()
        {
            var visual = LuggageVisual.Create(transform, LuggageKind.TaskCase);
            Luggage = visual.transform; Luggage.name = "Чемодан задания";
            Luggage.position = new Vector3(46.4f,1.3f,-.22f);
            bagTarget = Luggage.position; bagRotation = Quaternion.identity;
        }

        public bool CanChoose(out string reason)
        {
            reason = "";
            if (!environment.IsPresented(Node)) { reason = environment.TransitionText; return false; }
            if (Node.Length == 0) { reason = "Начните смену."; return false; }
            if (Distance > 2.35f) { reason = "Подойдите: " + TargetLabel + $" · {Distance:F0} м"; return false; }
            if (!ContextOpen) { reason = "Посмотрите на цель: " + TargetLabel + ". " + InputHints.Action(TargetAction); return false; }
            if (Node is "board" or "depart" or "equipment" && (player.transform.position.y < 1.2f || Mathf.Abs(player.transform.position.z) > 1.4f))
            { reason = "Войдите в вагон через открытую дверь."; return false; }
            if (Node is "depart" or "alight" && people.Any(p => p.WaitingForPlayer))
            { reason = "Освободите проход: пассажир ждёт, чтобы пройти."; return false; }
            if (Node == "depart" && people.Any(p => p.Travelling || !p.Seated))
            { reason = "Дождитесь, пока пассажиры займут места."; return false; }
            if (Node == "alight" && people.Any(p => p.Travelling))
            { reason = "Дождитесь окончания высадки."; return false; }
            if (Node == "medical_support" && colleague.WaitingForPlayer)
            { reason = "Освободите проход: коллега идёт к пассажиру."; return false; }
            if (Node == "medical_support" && colleague.Travelling)
            { reason = "Коллега идёт к пассажиру. Оставайтесь рядом."; return false; }
            return !Acting;
        }

        public bool CanFocus
        {
            get
            {
                if (client == null || !environment.IsPresented(Node) || client.Attempt?["task"] is not JObject || Distance > 2.35f) return false;
                var delta = TargetPosition - player.View.transform.position;
                if (Vector3.Dot(player.View.transform.forward, delta.normalized) < .55f) return false;
                // Navigation obstacles and closed doors occlude interactions; the active
                // target's own collider is allowed (e.g. the colleague's capsule).
                foreach (var hit in Physics.RaycastAll(player.View.transform.position, delta.normalized, delta.magnitude - .35f))
                    if (hit.collider.GetComponentInParent<ShiftPassenger>() == null && hit.collider.GetComponentInParent<FirstPersonController>() == null &&
                        !(Node is "phone" or "phone_fix" or "medical" or "medical_support" && hit.collider.name == "Seat navigation"))
                        return false;
                return true;
            }
        }
        public bool Interact()
        {
            if (!CanFocus || Acting || client.Busy) return false;
            contextNode = Node;
            if (Direct)
            {
                if (CanChoose(out _)) Perform((string)client.Attempt["task"]["choices"][0]["id"]);
                else { player.SetMenu(true); Updated?.Invoke(); }
                return true;
            }
            player.SetMenu(true); Updated?.Invoke(); return true;
        }
        public void Perform(string choice, bool expedite = false)
        {
            if (!CanChoose(out var reason)) { Feedback = reason; feedbackUntil = Time.time + 4; Updated?.Invoke(); return; }
            ActionText = expedite ? "Переходите дальше…" : Doing(Node);
            StartCoroutine(Action(choice, expedite));
        }
        // Nodes completed by one physical action at the target rather than a dialogue choice.
        bool Direct => Node is "board" or "equipment" or "bag_action" or "alight" or "final_check" or "handover" or "medical_support";
        static string Doing(string node) => node switch
        {
            "brief" => "Получаете задание…", "board" => "Поднимаетесь в вагон…", "platform" => "Выходите на платформу…",
            "depart" => "Входите в вагон…", "inspect" or "inspection_fix" => "Осматриваете проход…",
            "equipment" => "Проверяете связь…", "bag_action" => "Размещаете чемодан…",
            "boarding1" or "boarding1_fix" or "boarding2" or "boarding2_fix" => "Проверяете билет…",
            "service_request" or "service_fix" => "Помогаете пассажиру…", "phone" or "phone_fix" => "Говорите с пассажиром…",
            "medical" => "Организуете помощь…", "medical_support" => "Передаёте ситуацию старшему…",
            "alight" => "Помогаете с высадкой…", "final_check" => "Осматриваете вагон…", "handover" => "Передаёте вагон…",
            _ => "Продолжаете смену…"
        };
        IEnumerator Action(string choice, bool expedite)
        {
            Acting = true; player.SetMenu(false);
            Vector3 confirmedPosition = bagTarget; Quaternion confirmedRotation = bagRotation;
            string node = Node;
            bool movingBag = node == "bag_action" || (node == "inspect" && choice == "report") || node == "inspection_fix";
            if (hands != null && !expedite)
            {
                var presentation = StartCoroutine(hands.Present(movingBag ? "bag_action" : node, Luggage));
                while (hands.Active) { ActionProgress = hands.Progress; yield return null; }
                yield return presentation;
                if (!hands.Succeeded)
                {
                    Luggage.SetPositionAndRotation(confirmedPosition, confirmedRotation);
                    Acting = false; ActionProgress = 0;
                    Feedback = "Проход к предмету занят. Подойдите ближе, освободив место для действия.";
                    feedbackUntil = Time.time + 6; Updated?.Invoke(); yield break;
                }
            }
            else yield return new WaitForSeconds(.5f);
            // Choose validates the same spatial gate; release Acting only during this synchronous call.
            Acting = false; client.Choose(choice, expedite); Acting = true;
            while (client.Busy) yield return null;
            if (Node == node && movingBag)
                Luggage.SetPositionAndRotation(confirmedPosition, confirmedRotation);
            Acting = false; ActionProgress = 0;
            Updated?.Invoke();
        }

        void Synchronize()
        {
            if (client.Attempt == null) return;
            if (!environment.IsPresented(Node))
            {
                contextNode = "";
                // The briefing worker belongs to the depot, not to the fixed train cast.
                if (environment.Location == "Depot") supervisor.gameObject.SetActive(false);
                return;
            }
            bool newAttempt = attemptId != (string)client.Attempt["id"];
            if (newAttempt) { attemptId = (string)client.Attempt["id"]; previousNode = ""; boarded = false; }
            if (Node == previousNode) return;
            string old = previousNode; previousNode = Node; contextNode = "";
            var events = client.Attempt["state"]?["events"] as JArray;
            LastFeedback = (string)events?.Last?["feedback"] ?? "";
            Feedback = LastFeedback.Length > 0 ? LastFeedback : "Подойдите к старшему проводнику. " + InputHints.Action("Поговорить");
            feedbackUntil = Time.time + Mathf.Max(7, Feedback.Length * .09f);
            bool depot = environment.Location == "Depot";
            supervisor.gameObject.SetActive(depot);
            colleague.gameObject.SetActive(!depot && Node != "transfer");
            bool boarding = Node is "platform" or "boarding1" or "boarding1_fix" or "boarding2" or "boarding2_fix" or "depart";
            bool arrival = Node is "alight" or "final_check" or "handover" or "complete";
            // Passengers already walking out manage their own disappearance out of view.
            foreach (var p in people) if (depot || Node == "transfer" || !p.Departed) p.gameObject.SetActive(!depot && Node != "transfer");
            if (boarding && !boarded)
            {
                for (int i = 0; i < people.Count; i++)
                    people[i].StandAt(new Vector3(carriage3Entry + 1.5f + i * 1.15f,1.3f,-3.45f - i % 2 * .12f),-90); // Queue along the car, door kept free for the conductor.
                boarded = true;
            }
            if (Node == "boarding2" && old != "boarding2_fix") Board(0);
            if (Node == "depart")
            {
                if (!people[0].Seated && !people[0].Travelling) Board(0);
                for (int i = 1; i < people.Count; i++) Board(i, i * 1.6f);
            }
            if (!depot && !boarding && !arrival && Node != "transfer")
                foreach (var p in people) { if (newAttempt) p.SitImmediately(); else p.Sit(); }
            if (Node == "alight")
                for (int i = 0; i < people.Count; i++)
                {
                    var p = people[i]; if (newAttempt) p.SitImmediately();
                    float entry = i == 1 ? carriage4Entry : carriage3Entry;
                    p.Walk(new[] { new Vector3(p.Seat.x,1.3f,-.24f),new Vector3(entry,1.3f,-.24f),new Vector3(entry,1.3f,-3.2f),new Vector3(entry-5-i*1.2f,1.3f,-5) },false,.8f+i*1.1f,true);
                }
            if (arrival && Node != "alight")
                for (int i=0;i<people.Count;i++) if (newAttempt) people[i].Vanish(); else if (!people[i].Travelling && !people[i].Departed) people[i].Leave();
            people[3].OnPhone = Node is "phone" or "phone_fix";
            people[5].Unwell = Node is "medical" or "medical_support";
            // Through the gangway centre, not the partition; afterwards back to the post so the aisle stays free for alighting.
            if (Node == "medical_support") colleague.Walk(new[] { Gangway, new Vector3(people[5].Seat.x,1.3f,-.25f) },false);
            else if (old == "medical_support") colleague.Walk(new[] { Gangway, ColleaguePost },false);
            bool obstructing = Node is "inspect" or "inspection_fix" or "service_request" or "service_fix" or "bag_action";
            bool wasObstructing = old is "inspect" or "inspection_fix" or "service_request" or "service_fix" or "bag_action";
            bagTarget = obstructing ? new Vector3(46.4f,1.3f,-.22f) : new Vector3(46.96f,2.625f,1.10f);
            bagRotation = obstructing ? Quaternion.identity : Quaternion.Euler(0,0,90);
            // The service request introduces another passenger's suitcase. Start it at the
            // aisle instead of flying the earlier inspection case back from the upper shelf.
            if (newAttempt || (obstructing && !wasObstructing))
            { Luggage.position = bagTarget; Luggage.rotation = bagRotation; }
            foreach (var d in doors)
            {
                d.TrainMoving = environment.Location == "Route";
                // The authored working platforms and depot access are on the negative-Z side.
                d.SetOpen(!d.TrainMoving && d.WorldBounds().center.z < 0 && (boarding || arrival || Node == "board"));
            }
            SetTarget();
            if (Node is "complete" or "stopped") player.SetMenu(true);
            else player.SetMenu(false);
            Updated?.Invoke();
        }
        void Board(int index, float delay = 0)
        {
            var p = people[index]; float entry = index == 1 ? carriage4Entry : carriage3Entry;
            p.Walk(new[] { new Vector3(entry,1.3f,-3.25f),new Vector3(entry,1.3f,-.24f),new Vector3(p.Seat.x,1.3f,-.24f),p.Seat },true,delay);
        }
        void SetTarget()
        {
            string target = (string)(client.Attempt?["task"] as JObject)?["target"];
            (TargetPosition,TargetLabel) = target switch
            {
                "supervisor" => (supervisor.transform.position + Vector3.up*1.2f,"Старший проводник"),
                "entry" => (new Vector3(47.5f,2.2f, Node is "depart" or "board" ? -.3f : -2.7f),"Вход в вагон № 3"),
                "passenger1" => (people[0].transform.position + Vector3.up*1.2f,"Алексей Ветров · пассажир"),
                "passenger2" => (people[1].transform.position + Vector3.up*1.2f,"Мария Лебедева · пассажир"),
                "luggage" => (new Vector3(46.4f,2.1f,.6f),Node == "final_check" ? "Багажная полка и проход" : "Чемодан у багажной полки"),
                "phone" => (people[3].transform.position + Vector3.up*1.2f,"Пассажир с телефоном"),
                "medical" => (people[5].transform.position + Vector3.up*1.2f,"Пассажир, которому стало плохо"),
                "colleague" => (colleague.transform.position + Vector3.up*1.2f,"Коллега в соседнем вагоне"),
                _ => (new Vector3(46.68f,2.35f,-.70f),Node switch { "equipment" => "Служебная зона · рабочая связь", "handover" => "Служебная зона · журнал замечаний", _ => "Служебная зона вагона" })
            };
            TargetIsPerson = target is "supervisor" or "passenger1" or "passenger2" or "phone" or "medical" or "colleague";
            TargetAction = Direct ? (string)client.Attempt?["task"]?["choices"]?[0]?["label"] ?? "" : TargetIsPerson ? "Поговорить" : "Выбрать действие";
        }
        void Update()
        {
            if (client == null || Luggage == null) return;
            if (!Acting)
            {
                Luggage.position = Vector3.MoveTowards(Luggage.position,bagTarget,2*Time.deltaTime);
                Luggage.rotation = Quaternion.RotateTowards(Luggage.rotation,bagRotation,120*Time.deltaTime);
            }
            if (Time.time > feedbackUntil) Feedback = "";
            if (client.Attempt != null) SetTarget();
        }
    }
}
