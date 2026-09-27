using UnityEngine;

namespace GameVSM
{
    // Station board text driven by the shift stage. Text changes only when the node changes;
    // geometry is baked into the location prefab.
    [RequireComponent(typeof(TextMesh))]
    public sealed class TripBoard : MonoBehaviour
    {
        public string Location;
        public SignKind Kind;
        public bool Back;
        ShiftClient client;
        string shown;

        void OnEnable()
        {
            client = FindFirstObjectByType<ShiftClient>();
            if (client != null) client.Changed += Refresh;
            shown = null; Refresh();
        }
        void OnDisable() { if (client != null) client.Changed -= Refresh; }
        void Refresh()
        {
            string node = (string)client?.Attempt?["state"]?["node"] ?? "";
            if (node == shown) return;
            shown = node;
            GetComponent<TextMesh>().text = TripInfo.Text(Kind, Location, node, Back);
        }
    }
}
