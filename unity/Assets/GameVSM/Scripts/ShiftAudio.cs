using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace GameVSM
{
    // Sound of the shift in four layers: place bed, nearby sources, one-off events, training cues.
    // Everything is original synthesized sound from AmbientSynth (no third-party audio or recordings
    // of people). Levels, rolloff and pauses: docs/planning/audio-cards-2026-09-27.md.
    //
    // | Layer                 | Volume        | Rolloff (m)   | Pause / trigger                      |
    // | Depot hall bed        | BedDepot      | 2D            | loop 8 s, crossfade ~2 s             |
    // | City bed (stations)   | BedStation    | 2D            | loop 8 s, crossfade ~2 s             |
    // | Nearby sources        | NearSource×k  | linear 1–22   | loops 2.5–8 s, random start          |
    // | Distant events        | DistantEvent  | 2D, pan ±0.7  | depot 7–16 s, stations 28–55 s       |
    // | Rolling / air / joint | Rolling*,…    | 2D            | follows RouteMotion.Speed, joint 25 m|
    // | Station chime         | Announcement  | 2D            | once per attempt: boarding1, alight  |
    // | Task cue              | TaskCue       | 2D            | new task, not on *_fix, ≥ 4 s apart  |
    public sealed class ShiftAudio : MonoBehaviour
    {
        // AudioSource volumes. Cues and the chime stay above every bed; beds, nearby sources and
        // distant events are ducked while a task dialogue or a reply is on screen.
        public const float BedDepot = .20f, BedStation = .16f, NearSource = .30f, DistantEvent = .12f;
        public const float RollingIdle = .03f, RollingCruise = .22f, Airflow = .08f, RailJoint = .10f;
        public const float Announcement = .30f, TaskCue = .32f;
        public const float DuckDialog = .35f, DuckMenu = .6f, DuckReplica = .8f, InsideCar = .7f;
        const float FadeRate = .1f, CueSpacing = 4, JointSpacing = 25, BogieSpacing = 17.5f;

        readonly Dictionary<string, AudioClip> bank = new();
        static readonly System.Func<AudioClip> PeterCity = () => AmbientSynth.City("Город: Петербург", 3, 1, .5f),
            MoscowCity = () => AmbientSynth.City("Город: Москва", 9, 1.3f, .65f), Group1 = () => AmbientSynth.Group(41), Group2 = () => AmbientSynth.Group(43);
        readonly List<(AudioSource source, float level)> near = new();
        readonly List<AudioSource> leaving = new();
        readonly HashSet<string> announced = new();
        readonly AudioSource[] beds = new AudioSource[2];
        ShiftEnvironment world;
        ShiftExperience experience;
        ShiftClient client;
        AudioSource rolling, airflow, train, cue, announce, distant;
        GameObject nearby, fading;
        string location, node, attempt;
        int bed;
        float heard, travelled, nextJoint, duck = 1, nextDistant, lastCue = -99, announceAt = -1;
        bool leadBogie = true;

        void Start()
        {
            world = GetComponent<ShiftEnvironment>(); experience = GetComponent<ShiftExperience>(); client = GetComponent<ShiftClient>();
            for (int i = 0; i < beds.Length; i++) beds[i] = Source(null, true, 0);
            rolling = Source(Get("rolling", AmbientSynth.Rolling), true, 0); rolling.Play();
            airflow = Source(Get("airflow", AmbientSynth.Airflow), true, 0); airflow.Play();
            train = Source(null, false, RailJoint); cue = Source(null, false, TaskCue);
            announce = Source(null, false, Announcement); distant = Source(null, false, DistantEvent);
            gameObject.AddComponent<SurfaceSteps>();
            StartCoroutine(Prewarm());
        }
        // Station and event clips take up to a few hundred ms to synthesize on a phone, so they are
        // computed on a worker thread while the depot plays; only the clip creation runs here.
        IEnumerator Prewarm()
        {
            var list = new (string, System.Func<AudioClip>)[]
            {
                ("cue", AmbientSynth.TaskCue), ("joint", AmbientSynth.RailJoint), ("far.clank", AmbientSynth.DistantClank),
                ("far.air", AmbientSynth.Pneumatic), ("chime", AmbientSynth.StationChime), ("far.horn", AmbientSynth.DistantHorn),
                ("bed.Moskovsky", PeterCity), ("near.cafe", AmbientSynth.Cafe), ("near.group", Group1), ("near.group2", Group2),
                ("bed.Leningradsky", MoscowCity),
            };
            foreach (var (key, make) in list)
            {
                if (bank.ContainsKey(key)) continue;
                var task = Task.Run(() => AmbientSynth.Samples(make));
                while (!task.IsCompleted) yield return null;
                if (!bank.ContainsKey(key) && task.Status == TaskStatus.RanToCompletion && task.Result.Item2 != null)
                    bank[key] = AmbientSynth.Create(task.Result);
                yield return null;
            }
        }
        AudioSource Source(AudioClip clip, bool loop, float volume)
        {
            var s = gameObject.AddComponent<AudioSource>(); s.clip = clip; s.loop = loop; s.volume = volume; s.playOnAwake = false; return s;
        }
        // Clips are generated on first use of a place and kept for the session.
        AudioClip Get(string key, System.Func<AudioClip> make)
        {
            // A clip still being prepared in the background is made here instead; the worker's result is then dropped.
            if (!bank.TryGetValue(key, out var clip)) bank[key] = clip = make();
            return clip;
        }

        void Update()
        {
            if (world == null || world.Player == null || experience == null) return;
            var player = world.Player;
            float target = player.MenuOpen && experience.ContextOpen ? DuckDialog : player.MenuOpen ? DuckMenu
                : experience.Feedback.Length > 0 || experience.Acting ? DuckReplica : 1;
            duck = Mathf.MoveTowards(duck, target, Time.deltaTime * 1.5f);
            if (location != world.Location && world.Current != null) Enter(world.Location);
            var p = player.transform.position; bool inside = p.y > 1.2f && Mathf.Abs(p.z) < 1.5f;
            UpdatePlace(inside); UpdateTrain(inside); UpdateEvents();
        }

        void Enter(string place)
        {
            location = place;
            if (fading != null) Destroy(fading);
            leaving.Clear(); foreach (var n in near) leaving.Add(n.source); near.Clear();
            // Nearby sources live under this object, not the swapped prefab, so they fade out instead of clicking off.
            fading = nearby; nearby = new GameObject("Звуки места · " + place); nearby.transform.SetParent(transform, false);
            bed = 1 - bed; var clip = place switch
            {
                "Depot" => Get("bed.Depot", AmbientSynth.DepotHall),
                "Moskovsky" => Get("bed.Moskovsky", PeterCity),
                "Leningradsky" => Get("bed.Leningradsky", MoscowCity),
                _ => null
            };
            beds[bed].clip = clip; beds[bed].volume = 0;
            if (clip != null) { beds[bed].Play(); beds[bed].timeSamples = Random.Range(0, clip.samples); }
            var parts = world.Current.GetComponentsInChildren<Transform>(true);
            if (place == "Depot")
            {
                Near(parts, "near.cabinet", "Электрошкаф", new Vector3(4, 1, 3), AmbientSynth.Transformer, 1, 8, .6f);
                Near(parts, "near.tools", "Тележка инструмента", new Vector3(-6, 1, 4), AmbientSynth.Workbench, 2, 20, 1);
                Near(parts, "near.fan", "Стеновая стойка", new Vector3(0, 5, -8), AmbientSynth.WallFan, 3, 22, .8f, 4);
            }
            else if (place != "Route")
            {
                Near(parts, "near.cafe", "Павильон кофе", new Vector3(10, 1, -4), AmbientSynth.Cafe, 2, 16, .8f);
                                // Benches keep their collision blocker after the site mesh combine, so the name survives in both stations.
                Near(parts, "near.group", "Коллизия скамьи", new Vector3(-7, 1, -2), Group1, 1.5f, 12, .8f);
                Near(parts, "near.group2", null, new Vector3(14, 1, -1.5f), Group2, 1.5f, 12, .7f);
            }
            nextDistant = Time.time + Random.Range(4f, 9f);
        }
        // Binds a looped 3D source to the nearest named object of the place, else to an offset from the player.
        void Near(Transform[] parts, string key, string match, Vector3 offset, System.Func<AudioClip> make, float min, float max, float level, float lift = 0)
        {
            var from = world.Player.transform.position; Vector3 at = from + offset; float best = 35 * 35;
            if (match != null)
                foreach (var t in parts)
                {
                    if (!t.name.Contains(match)) continue;
                    var renderer = t.GetComponentInChildren<Renderer>();
                    var point = (renderer != null ? renderer.bounds.center : t.position) + Vector3.up * lift;
                    float d = (point - from).sqrMagnitude; if (d < best) { best = d; at = point; }
                }
            var go = new GameObject(key); go.transform.SetParent(nearby.transform, false); go.transform.position = at;
            var s = go.AddComponent<AudioSource>(); s.clip = Get(key, make); s.loop = true; s.playOnAwake = false;
            s.spatialBlend = 1; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = min; s.maxDistance = max; s.dopplerLevel = 0; s.volume = 0;
            s.Play(); s.timeSamples = Random.Range(0, s.clip.samples);
            near.Add((s, NearSource * level));
        }

        void UpdatePlace(bool inside)
        {
            float step = Time.deltaTime * FadeRate, level = (location == "Depot" ? BedDepot : BedStation) * duck * (inside ? InsideCar : 1);
            for (int i = 0; i < beds.Length; i++)
            {
                var b = beds[i]; b.volume = Mathf.MoveTowards(b.volume, i == bed && b.clip != null ? level : 0, step);
                if (i != bed && b.volume <= 0 && b.isPlaying) b.Stop();
            }
            foreach (var (source, nearLevel) in near) source.volume = Mathf.MoveTowards(source.volume, nearLevel * duck, step * 2);
            bool silent = true;
            foreach (var source in leaving) if (source != null) { source.volume = Mathf.MoveTowards(source.volume, 0, step * 2); silent &= source.volume <= 0; }
            if (silent && fading != null) { Destroy(fading); fading = null; leaving.Clear(); }
            distant.volume = DistantEvent * duck; announce.volume = Announcement * duck;
        }

        // Rolling, airflow and rail joints follow the heard speed; it trails RouteMotion so arrival and departure never jump.
        void UpdateTrain(bool inside)
        {
            var motion = location == "Route" && world.Current != null ? world.Current.GetComponent<RouteMotion>() : null;
            float speed = world.TravelSpeed, cruise = motion != null && motion.CruiseSpeed > 0 ? motion.CruiseSpeed : 48;
            heard = Mathf.MoveTowards(heard, speed, Time.deltaTime * (speed > heard ? 6 : 10));
            float k = Mathf.Clamp01(heard / cruise), trainDuck = Mathf.Lerp(1, duck, .5f);
            rolling.volume = Mathf.Lerp(inside ? RollingIdle : 0, RollingCruise, Mathf.Sqrt(k)) * trainDuck; rolling.pitch = .55f + .5f * k;
            airflow.volume = Airflow * k * k * trainDuck; airflow.pitch = .8f + .4f * k;
            travelled += heard * Time.deltaTime;
            if (heard < 2) { nextJoint = travelled + 5; leadBogie = true; return; }
            if (travelled < nextJoint) return;
            train.pitch = .85f + .3f * k + Random.Range(-.03f, .03f);
            train.PlayOneShot(Get("joint", AmbientSynth.RailJoint), (.35f + .65f * k) * trainDuck);
            nextJoint += leadBogie ? BogieSpacing : JointSpacing - BogieSpacing; leadBogie = !leadBogie;
        }

        void UpdateEvents()
        {
            string now = experience.Node;
            if (!world.IsPresented(now)) return;
            if (now != node)
            {
                string id = (string)client?.Attempt?["id"] ?? "";
                if (id != attempt) { attempt = id; announced.Clear(); }
                node = now;
                // The chime belongs to the start of boarding and to arrival, once per attempt; restoring or a retry does not repeat it.
                if (now is "boarding1" or "alight" && announced.Add(now)) announceAt = Time.time + .8f;
                else if (now.Length > 0 && !now.EndsWith("_fix") && Time.time - lastCue > CueSpacing) { cue.PlayOneShot(Get("cue", AmbientSynth.TaskCue)); lastCue = Time.time; }
            }
            if (announceAt > 0 && Time.time >= announceAt) { announceAt = -1; announce.PlayOneShot(Get("chime", AmbientSynth.StationChime)); nextDistant = Mathf.Max(nextDistant, Time.time + 6); }
            if (Time.time < nextDistant || location is not ("Depot" or "Moskovsky" or "Leningradsky")) return;
            bool depot = location == "Depot";
            var clip = depot ? Random.value < .6f ? Get("far.clank", AmbientSynth.DistantClank) : Get("far.air", AmbientSynth.Pneumatic) : Get("far.horn", AmbientSynth.DistantHorn);
            distant.panStereo = Random.Range(-.7f, .7f); distant.pitch = Random.Range(.92f, 1.06f);
            distant.PlayOneShot(clip);
            nextDistant = Time.time + (depot ? Random.Range(7f, 16f) : Random.Range(28f, 55f));
        }

        void OnDestroy() { foreach (var clip in bank.Values) if (clip != null) Destroy(clip); }
    }
}
