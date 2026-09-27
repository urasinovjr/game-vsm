using UnityEngine;
using UnityEngine.Rendering;

namespace GameVSM
{
    public sealed class NativeQuality : MonoBehaviour
    {
        public RenderPipelineAsset Desktop;
        public RenderPipelineAsset Android;
        void Awake()
        {
            QualitySettings.renderPipeline = Application.isMobilePlatform ? Android : Desktop;
            Application.targetFrameRate = Application.isMobilePlatform ? 30 : 60;
            // Horizontal only, but either way up: a phone held the other side does not show the game upside down.
            if (!Application.isMobilePlatform) return;
            Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
    }
}
