using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameVSM.Editor
{
    public static class NativeBuild
    {
        public static void Mac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/Conductor.app");
        public static void Android() => Build(BuildTarget.Android, "Builds/Android/Conductor.apk");

        public static void VerifyAndroidSupport()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Android Build Support is not available to this Editor.");
            UnityEngine.Debug.Log("GAMEVSM_ANDROID_SUPPORT_READY " + UnityEngine.Application.unityVersion);
        }

        static void Build(BuildTarget target, string output)
        {
            const string scene = "Assets/GameVSM/Scenes/Metallostroy.unity";
            if (!File.Exists(scene)) throw new InvalidOperationException("Generate and validate the depot scene first.");
            // A stale glTF import can retain 4x4 initialization textures without a compiler error.
            var textures = AssetDatabase.LoadAllAssetsAtPath("Assets/GameVSM/Art/Train/WhiteKrechet.glb").OfType<Texture2D>().ToArray();
            if (textures.Length == 0 || textures.Any(t => t.width <= 4 || t.height <= 4))
                throw new InvalidOperationException("Train textures contain import placeholders. Reimport WhiteKrechet.glb in the live Editor before building.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { scene }, locationPathName = output, target = target,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Native build failed: {report.summary.result}, {report.summary.totalErrors} errors");
        }
    }
}
