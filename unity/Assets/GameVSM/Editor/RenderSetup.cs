using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameVSM.Editor
{
    public static class RenderSetup
    {
        [MenuItem("GameVSM/Apply project URP profiles")]
        public static void Apply()
        {
            var desktop = Load("DesktopURP");
            var mobile = Load("AndroidURP");
            // Scene View and material previews render before any MonoBehaviour.Awake.
            // Both the fallback and quality overrides must reference saved assets.
            GraphicsSettings.defaultRenderPipeline = desktop;
            int current = QualitySettings.GetQualityLevel();
            try
            {
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = QualitySettings.names[i] == "Mobile" ? mobile : desktop;
                }
            }
            finally { QualitySettings.SetQualityLevel(current, false); }
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            Debug.Log("GAMEVSM_URP_CONFIGURED: persistent default and quality profiles");
        }

        static UniversalRenderPipelineAsset Load(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/GameVSM/Generated/" + name + ".asset");
            if (asset == null) throw new InvalidOperationException("Missing URP profile: " + name);
            return asset;
        }
    }
}
