#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace GameVSM
{
    // Opt-in standalone smoke/profile run; never starts during normal gameplay.
    public sealed class ArtProfile : MonoBehaviour
    {
        string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--art-profile-output");
            if (at < 0 || at + 1 >= args.Length) return;
            Application.runInBackground = true;
            new GameObject("Art profile run").AddComponent<ArtProfile>().output = args[at + 1];
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            yield return null;
            var world = FindAnyObjectByType<ShiftEnvironment>();
            var results = new JArray();
            using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            using var passes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            foreach (string site in new[] { "Depot", "Moskovsky", "Leningradsky", "Route" })
            {
                world.Show(site); world.Player.SetMenu(false);
                for (int i = 0; i < 90; i++) yield return null;
                var frames = new List<float>();
                long drawTotal = 0, triangleTotal = 0, batchTotal = 0, passTotal = 0;
                for (int i = 0; i < 180; i++)
                {
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime * 1000);
                    drawTotal += draws.LastValue; triangleTotal += triangles.LastValue;
                    batchTotal += batches.LastValue; passTotal += passes.LastValue;
                }
                frames.Sort();
                var result = new JObject {
                    ["location"] = site, ["frames"] = frames.Count,
                    ["focused"] = Application.isFocused, ["vsync_count"] = QualitySettings.vSyncCount,
                    ["median_frame_ms"] = frames[frames.Count / 2], ["p95_frame_ms"] = frames[(int)(frames.Count * .95f)],
                    ["draw_calls_mean"] = draws.Valid && drawTotal > 0 ? new JValue(drawTotal / 180f) : JValue.CreateNull(),
                    ["batches_mean"] = batches.Valid && batchTotal > 0 ? new JValue(batchTotal / 180f) : JValue.CreateNull(),
                    ["setpass_mean"] = passes.Valid && passTotal > 0 ? new JValue(passTotal / 180f) : JValue.CreateNull(),
                    ["triangles_mean"] = triangles.Valid ? triangleTotal / 180 : -1,
                    ["unity_allocated_mb"] = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024),
                    ["player_y"] = world.Player.transform.position.y
                };
                results.Add(result);
                ScreenCapture.CaptureScreenshot(Path.Combine(output, site + "-game.png"));
                yield return new WaitForEndOfFrame();
                yield return null;
                if (site != "Route") yield return Gallery(world, site);
            }
            File.WriteAllText(Path.Combine(output, "native-profile.json"), new JObject {
                ["unity"] = Application.unityVersion, ["device"] = SystemInfo.deviceModel,
                ["gpu"] = SystemInfo.graphicsDeviceName, ["width"] = Screen.width, ["height"] = Screen.height,
                ["development_build"] = Debug.isDebugBuild, ["target_fps"] = Application.targetFrameRate,
                ["locations"] = results
            }.ToString());
            Debug.Log("GAMEVSM_NATIVE_PROFILE_COMPLETE");
            Application.Quit();
        }

        IEnumerator Gallery(ShiftEnvironment world, string site)
        {
            var player = world.Player;
            var camera = player.View.transform;
            var localPosition = camera.localPosition;
            var localRotation = camera.localRotation;
            player.enabled = false;
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases) canvas.enabled = false;
            var positions = new[] { new Vector3(54, site == "Depot" ? 1.72f : 3f, -6),
                new Vector3(70, site == "Depot" ? 1.72f : 3f, -7.8f), new Vector3(-85, 3.2f, -5),
                new Vector3(165, 140, -155), new Vector3(50, 2.5f, -8) };
            var targets = new[] { new Vector3(43, 2.9f, -.8f), new Vector3(-60, 4, -6),
                new Vector3(-145, 7, 7), new Vector3(-5, 0, 0), new Vector3(47.5f, 1.4f, -3.5f) };
            var names = new[] { "entry", "long", "terminal", "overview", "detail" };
            for (int i = 0; i < names.Length; i++)
            {
                camera.position = positions[i]; camera.LookAt(targets[i]);
                // Let URP update probe caches and culling between views.
                for (int frame = 0; frame < 8; frame++) yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(output, site + "-" + names[i] + ".png"));
                yield return new WaitForEndOfFrame(); yield return null;
            }
            if (site != "Depot")
            {
                camera.position=new Vector3(-76,2.8f,-8.6f);camera.LookAt(new Vector3(-82,2.6f,-7));
                for(int frame=0;frame<8;frame++)yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(output,site+"-kiosk.png"));
                yield return new WaitForEndOfFrame();yield return null;
            }
            if (site == "Moskovsky")
            {
                foreach (var person in world.Current.GetComponentsInChildren<PassengerMotion>())
                {
                    if (!person.name.Contains("WithBag") && !person.name.Contains("Woman")) continue;
                    person.enabled = false;
                    camera.position = person.transform.TransformPoint(new Vector3(.9f, 1.2f, 2.5f));
                    camera.LookAt(person.transform.TransformPoint(new Vector3(.2f, .95f, 0)));
                    for (int frame = 0; frame < 8; frame++) yield return null;
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, person.name.Replace("(Clone)", "") + ".png"));
                    yield return new WaitForEndOfFrame(); yield return null;
                    if (person.name.Contains("WithBag")) break;
                }
            }
            camera.localPosition = localPosition; camera.localRotation = localRotation;
            foreach (var canvas in canvases) canvas.enabled = true;
            player.enabled = true;
        }
    }
}
#endif
