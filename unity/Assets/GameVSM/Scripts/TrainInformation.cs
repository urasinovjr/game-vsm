using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameVSM
{
    // Replaces only the imported demonstration lettering, leaving the model and its
    // dark ribbon panels intact. Route/status come from the same trip as station signs.
    public sealed class TrainInformation : MonoBehaviour
    {
        readonly List<TextMesh> labels = new();
        readonly List<float> widths = new();
        readonly List<Renderer> originals = new();
        ShiftEnvironment world;
        Material surface;
        string shown;
        void Start()
        {
            world = GetComponent<ShiftEnvironment>();
            var train = FindFirstObjectByType<TrainVisibility>();
            var font = Resources.Load<Font>("Manrope");
            if (train == null || font == null || world == null) return;
            surface = new Material(Shader.Find("GameVSM/World Text"));
            foreach (var original in train.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!original.name.Contains("Info_ribbon_text")) continue;
                var bounds = original.bounds;
                originals.Add(original); original.enabled = false;
                int count = Mathf.Max(1, Mathf.FloorToInt(bounds.size.x / 6.4f));
                float span = bounds.size.x / count;
                for (int i=0;i<count;i++)
                {
                    var item = new GameObject("Информация о текущем рейсе", typeof(TextMesh));
                    item.transform.SetParent(original.transform.parent, true);
                    float side = Mathf.Sign(bounds.center.z);
                    item.transform.position = new Vector3(bounds.min.x + span*(i+.5f), bounds.center.y, bounds.center.z-side*.008f);
                    item.transform.rotation = Quaternion.Euler(0, side > 0 ? 0 : 180, 0);
                    var text = item.GetComponent<TextMesh>();
                    text.font = font; text.fontSize = 64; text.characterSize = .018f;
                    text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
                    text.color = new Color(.94f,.92f,.82f);
                    text.text = Caption(world.Location, world.TransitionText);
                    // Fit the longest route line once. Status strings are shorter.
                    float width = text.GetComponent<Renderer>().bounds.size.x;
                    if (width > span-.3f) text.characterSize *= (span-.3f)/width;
                    var renderer = text.GetComponent<MeshRenderer>();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    item.AddComponent<WorldText>().Surface = surface;
                    labels.Add(text);
                    widths.Add(span-.3f);
                }
            }
            Refresh();
        }
        public static string Caption(string location, string transition) =>
            $"{TripInfo.Number}  {TripInfo.Origin} — {TripInfo.Destination}  ·  " +
            (!string.IsNullOrEmpty(transition) ? transition.ToUpperInvariant() : location switch
            {
                "Depot" => "ПОДГОТОВКА К РЕЙСУ",
                "Moskovsky" => "ПОСАДКА",
                "Leningradsky" => "ПРИБЫЛИ В МОСКВУ",
                _ => "В ПУТИ"
            });
        void Update() => Refresh();
        void Refresh()
        {
            if (world == null) return;
            string next = Caption(world.Location, world.TransitionText);
            if (shown == next) return;
            shown = next;
            for (int i=0;i<labels.Count;i++)
            {
                var label=labels[i]; label.text=next; label.characterSize=.018f;
                float width=label.GetComponent<Renderer>().bounds.size.x;
                if(width>widths[i])label.characterSize*=widths[i]/width;
            }
        }
        void OnDestroy()
        {
            foreach (var original in originals) if (original != null) original.enabled = true;
            foreach (var text in labels) if (text != null) Destroy(text.gameObject);
            if (surface != null) Destroy(surface);
        }
    }
}
