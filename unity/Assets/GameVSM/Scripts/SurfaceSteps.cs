using System.Collections.Generic;
using UnityEngine;

namespace GameVSM
{
    // Footsteps follow the ground under the conductor: depot concrete/asphalt, station stone,
    // car floor. Four synthesized variants per surface, never the same one twice in a row.
    public sealed class SurfaceSteps : MonoBehaviour
    {
        public const float Volume = .24f, Stride = .8f;
        const int Variants = 4;
        readonly Dictionary<AmbientSynth.Surface, AudioClip[]> clips = new();
        ShiftEnvironment world;
        ShiftExperience experience;
        AudioSource source;
        Vector3 previous;
        float walked;
        int last = -1;

        void Start()
        {
            world = GetComponent<ShiftEnvironment>(); experience = GetComponent<ShiftExperience>();
            source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.volume = Volume;
            if (world != null && world.Player != null) previous = world.Player.transform.position;
        }
        void Update()
        {
            if (world == null || world.Player == null) return;
            var player = world.Player; var position = player.transform.position;
            var delta = position - previous; previous = position; delta.y = 0;
            // Teleports between places and scripted actions are not steps.
            if (player.MenuOpen || delta.magnitude > .5f || (experience != null && experience.Acting)) return;
            walked += delta.magnitude;
            if (walked < Stride) return;
            walked = 0;
            var surface = Detect(position);
            if (!clips.TryGetValue(surface, out var set))
            {
                set = new AudioClip[Variants];
                for (int i = 0; i < Variants; i++) set[i] = AmbientSynth.Footstep(surface, i);
                clips[surface] = set;
            }
            int pick = Random.Range(0, Variants); if (pick == last) pick = (pick + 1) % Variants; last = pick;
            source.pitch = Random.Range(.95f, 1.05f);
            source.PlayOneShot(set[pick], Random.Range(.85f, 1f));
        }
        // Inside the car the code-wide rule applies (floor above 1.2 m, within the body width);
        // outside, the nearest ground collider's name or material picks asphalt/gravel.
        AmbientSynth.Surface Detect(Vector3 position)
        {
            if (position.y > 1.2f && Mathf.Abs(position.z) < 1.5f) return AmbientSynth.Surface.CarFloor;
            string ground = ""; float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(position + Vector3.up * .5f, Vector3.down, 1.5f))
            {
                if (hit.distance >= nearest || hit.collider.GetComponentInParent<FirstPersonController>() != null) continue;
                nearest = hit.distance; var renderer = hit.collider.GetComponent<Renderer>();
                ground = hit.collider.name + " " + (renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "");
            }
            if (Has(ground, "Asphalt", "Асфальт")) return AmbientSynth.Surface.Asphalt;
            if (Has(ground, "Gravel", "Щеб", "Grass", "Трав")) return AmbientSynth.Surface.Gravel;
            return world.Location == "Depot" ? AmbientSynth.Surface.Concrete : AmbientSynth.Surface.Stone;
        }
        static bool Has(string text, params string[] words)
        {
            foreach (var w in words) if (text.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        void OnDestroy() { foreach (var set in clips.Values) foreach (var clip in set) if (clip != null) Destroy(clip); }
    }
}
