using UnityEngine;

namespace GameVSM
{
    [ExecuteAlways, RequireComponent(typeof(TextMesh))]
    public sealed class WorldText : MonoBehaviour
    {
        public Material Surface;
        void OnEnable() { Font.textureRebuilt += Rebuilt; Apply(); }
        void OnDisable() { Font.textureRebuilt -= Rebuilt; }
        void Rebuilt(Font font) { if (GetComponent<TextMesh>().font == font) Apply(); }
        void Apply()
        {
            var text = GetComponent<TextMesh>();
            if (Surface == null || text.font == null) return;
            Surface.mainTexture = text.font.material.mainTexture;
            GetComponent<MeshRenderer>().sharedMaterial = Surface;
        }
        void LateUpdate() { Apply(); }
    }
}
