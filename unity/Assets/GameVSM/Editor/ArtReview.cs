using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameVSM.Editor
{
    public static class ArtReview
    {
        public static void Capture(string name, Vector3 position, Vector3 target)
        {
            var camera = new GameObject("Art review camera").AddComponent<Camera>();
            camera.transform.position = position; camera.transform.LookAt(target);
            camera.fieldOfView = 65; camera.nearClipPlane = .08f; camera.farClipPlane = 700;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
            var prior = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
                string folder = Path.GetFullPath("../output/unity/art-review"); Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                RenderTexture.active = prior; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }
        // Multi-location review runs through ArtProfile, with normal rendered frames between
        // scene changes so URP can retire reflection-probe cache entries safely.
        public static void Passenger()
        {
            var existing = UnityEngine.Object.FindObjectsByType<PassengerMotion>(FindObjectsSortMode.None);
            foreach (var person in existing) person.gameObject.SetActive(false);
            var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameVSM/Art/Characters/Passenger.prefab"));
            go.transform.position = new Vector3(43, .04f, -4.7f);
            var animator = go.GetComponentInChildren<Animator>();
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/GameVSM/Art/Characters/Passenger.glb").OfType<AnimationClip>().Single(c => c.name == "Idle");
            clip.SampleAnimation(animator.gameObject, 0);
            Capture("passenger-full", new Vector3(43, 1.35f, -1.9f), new Vector3(43, .95f, -4.7f));
            Capture("passenger-face", new Vector3(43, 1.62f, -3.55f), new Vector3(43, 1.55f, -4.7f));
            UnityEngine.Object.DestroyImmediate(go);
            foreach (var person in existing) person.gameObject.SetActive(true);
        }

        public static void BakeLocationReflections()
        {
            var environment = UnityEngine.Object.FindFirstObjectByType<ShiftEnvironment>();
            var original = environment.Current;
            original.SetActive(false);
            try
            {
                foreach (string site in new[] { "Depot", "Moskovsky", "Leningradsky" })
                {
                    string prefab = "Assets/GameVSM/Resources/Environments/" + site + ".prefab";
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
                    try
                    {
                        int index = 0;
                        foreach (var probe in go.GetComponentsInChildren<ReflectionProbe>())
                        {
                            string path = "Assets/GameVSM/Generated/Environment/" + site + "Reflection" + index++ + ".exr";
                            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
                            if (!Lightmapping.BakeReflectionProbe(probe, path)) throw new InvalidOperationException("Reflection bake failed: " + site);
                            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
                            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                            if (probe.customBakedTexture == null) throw new InvalidOperationException("Missing reflection cubemap: " + path);
                        }
                        PrefabUtility.SaveAsPrefabAsset(go, prefab);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(go); }
                }
            }
            finally { original.SetActive(true); }
            AssetDatabase.SaveAssets();
            Debug.Log("GAMEVSM_REFLECTIONS_BAKED");
        }
    }
}
