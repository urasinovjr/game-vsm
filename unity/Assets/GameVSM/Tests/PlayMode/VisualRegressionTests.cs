using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameVSM.Tests
{
    public sealed class VisualRegressionTests
    {
        [UnityTest]
        public IEnumerator SaloonWindowShowsObjectsOutsideTheTrain()
        {
            yield return SceneManager.LoadSceneAsync("Metallostroy");yield return null;
            var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.transform.position=new Vector3(38.6f,2.43f,4);marker.transform.localScale=new Vector3(2,1,.1f);
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetColor("_BaseColor",Color.magenta);marker.GetComponent<Renderer>().sharedMaterial=mat;
            var camera=new GameObject("Window regression camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(38.6f,2.43f,1);camera.transform.rotation=Quaternion.identity;camera.fieldOfView=40;camera.nearClipPlane=.05f;
            var target=new RenderTexture(128,128,24);camera.targetTexture=target;
            var pixels=new Texture2D(128,128,TextureFormat.RGB24,false);var prior=RenderTexture.active;
            int visible=0;
            try
            {
                for(int i=0;i<4;i++)yield return null;
                camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();
                foreach(var p in pixels.GetPixels())if(p.r>p.g+.2f && p.b>p.g+.2f)visible++;
            }
            finally
            {
                RenderTexture.active=prior;camera.targetTexture=null;
                Object.Destroy(camera.gameObject);Object.Destroy(marker);Object.Destroy(mat);Object.Destroy(target);Object.Destroy(pixels);
            }
            Assert.That(visible,Is.GreaterThan(1000),"Exterior marker is hidden by the saloon glazing.");
        }

        [UnityTest]
        public IEnumerator RefrigeratedStockIsVisibleThroughClosedGlass()
        {
            yield return SceneManager.LoadSceneAsync("Metallostroy"); yield return null;
            var stock=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r=>r.name.Contains("Bottle")).ToArray();
            var camera=new GameObject("Bistro regression camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(4.65f,2.15f,.2f);camera.transform.LookAt(new Vector3(4.65f,2.15f,-1.1f));
            camera.fieldOfView=50;camera.nearClipPlane=.05f;
            var target=new RenderTexture(256,256,24);camera.targetTexture=target;
            var pixels=new Texture2D(256,256,TextureFormat.RGB24,false);
            var prior=RenderTexture.active;
            Color[] Read(){camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();return pixels.GetPixels();}
            int difference=0;
            try
            {
                for(int i=0;i<4;i++)yield return null;
                var full=Read();
                foreach(var r in stock)r.enabled=false;
                for(int i=0;i<4;i++)yield return null;
                var empty=Read();
                for(int i=0;i<full.Length;i++)if(Mathf.Abs(full[i].r-empty[i].r)+Mathf.Abs(full[i].g-empty[i].g)+Mathf.Abs(full[i].b-empty[i].b)>.12f)difference++;
            }
            finally
            {
                foreach(var r in stock)r.enabled=true;
                RenderTexture.active=prior;camera.targetTexture=null;
                Object.Destroy(camera.gameObject);Object.Destroy(target);Object.Destroy(pixels);
            }
            Assert.That(difference,Is.GreaterThan(150),"Removing the bottles barely changed the image: the closed glass hid the stock.");
        }

        [UnityTest]
        public IEnumerator BistroStockSurvivesWalkingAwayAndChangingLocations()
        {
            yield return SceneManager.LoadSceneAsync("Metallostroy"); yield return null;
            var world = Object.FindAnyObjectByType<ShiftEnvironment>();
            var stock = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include)
                .Where(r=>r.name.Contains("_Bottle") || r.name.Contains("Bistro_juice")).ToArray();
            Assert.That(stock.Length,Is.GreaterThan(20));
            foreach(string place in new[]{"Depot","Moskovsky","Route","Leningradsky"})
            {
                world.Show(place);
                foreach(float x in new[]{95f,0f,-92f,0f})
                {
                    world.Player.Teleport(new Vector3(x,1.31f,-.24f));
                    yield return new WaitForSeconds(.25f);
                    Assert.That(stock.All(r=>r != null && r.enabled && r.gameObject.activeInHierarchy),Is.True,"Bistro stock disappeared at "+place+" x="+x);
                }
            }
        }

        [UnityTest]
        public IEnumerator WalkUpDepotStairsThroughDoorWithoutTeleporting()
        {
            yield return SceneManager.LoadSceneAsync("Metallostroy"); yield return null;
            var p=Object.FindAnyObjectByType<FirstPersonController>();
            foreach(var m in Object.FindObjectsByType<TrainMechanism>(FindObjectsInactive.Include))
                if(m.Kind=="sliding_entry_door" && !m.Automatic) m.SetOpen(true);
            p.Teleport(new Vector3(47.5f,.06f,-6.2f));p.transform.rotation=Quaternion.identity;p.SetMenu(false);
            yield return new WaitForSeconds(1);
            // The test runner may change Game view focus while loading the scene.
            // Restore input immediately before testing the physical walking route.
            p.SetMenu(false);
            p.TouchMove=Vector2.up;
            float end=Time.time+4;
            while(Time.time<end && p.transform.position.z<-.45f) yield return null;
            p.TouchMove=Vector2.zero;
            Assert.That(p.transform.position.z,Is.GreaterThan(-.7f),"Door/stair route blocked at "+p.transform.position);
            Assert.That(p.transform.position.y,Is.InRange(1.25f,1.4f));
        }

        [UnityTest]
        public IEnumerator RouteMovesVisibleSceneryNotOnlyEmptyTransforms()
        {
            var route = Object.Instantiate(Resources.Load<GameObject>("Environments/Route"));
            var renderers = route.GetComponentsInChildren<MeshRenderer>();
            var positions = new Vector3[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) positions[i] = renderers[i].bounds.center;
            yield return new WaitForSeconds(1.2f);
            float largestMovement = 0;
            for (int i = 0; i < renderers.Length; i++)
                largestMovement = Mathf.Max(largestMovement, Vector3.Distance(positions[i], renderers[i].bounds.center));
            Object.Destroy(route);
            Assert.That(largestMovement, Is.GreaterThan(1), "The route animation did not move any visible mesh.");
        }

        [UnityTest]
        public IEnumerator TrainExteriorDoesNotDisappearAtTwoHundredMetres()
        {
            var root = new GameObject("Visibility regression");
            var observer = new GameObject("Observer");
            var car = GameObject.CreatePrimitive(PrimitiveType.Cube);
            car.transform.SetParent(root.transform);
            var visibility = root.AddComponent<TrainVisibility>();
            visibility.Observer = observer.transform;
            visibility.Zones = new[] { new TrainVisibility.Zone { Root = car, Bounds = new Bounds(Vector3.zero, new Vector3(25, 4, 3)), Interior = false } };
            observer.transform.position = new Vector3(200, 2, -6);
            yield return null;
            bool visible = car.activeSelf;
            Object.Destroy(root); Object.Destroy(observer);
            Assert.That(visible, Is.True, "A visible train body vanished solely because the observer moved 200 m away.");
        }

        [UnityTest]
        public IEnumerator OpaqueWallOccludesActualEnvironmentText()
        {
            yield return SceneManager.LoadSceneAsync("Metallostroy");
            yield return null;
            var world = Object.FindFirstObjectByType<ShiftEnvironment>();
            var source = world.Current.GetComponentInChildren<TextMesh>();
            Assert.That(source, Is.Not.Null);
            var label = Object.Instantiate(source.gameObject);
            label.layer = 30; label.transform.SetPositionAndRotation(new Vector3(1000, 0, 1), Quaternion.identity);
            label.transform.localScale = Vector3.one;
            var text = label.GetComponent<TextMesh>();
            text.text = "BLOCKED"; text.anchor = TextAnchor.MiddleCenter; text.characterSize = .3f; text.fontSize = 80; text.color = Color.red;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = 30; wall.transform.position = new Vector3(1000, 0, 0); wall.transform.localScale = new Vector3(4, 4, .2f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.SetColor("_BaseColor", Color.blue);
            wall.GetComponent<Renderer>().sharedMaterial = mat;
            var camera = new GameObject("Occlusion camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(1000, 0, -6); camera.cullingMask = 1 << 30;
            camera.orthographic = true; camera.orthographicSize = 2; camera.clearFlags = CameraClearFlags.SolidColor;
            var target = new RenderTexture(128, 128, 24); camera.targetTexture = target;
            var pixels = new Texture2D(128, 128, TextureFormat.RGB24, false);
            var prior = RenderTexture.active;
            int red = 0, unobstructedRed = 0;
            try
            {
                for(int frame=0;frame<4;frame++)yield return null;
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
                foreach (var pixel in pixels.GetPixels()) if (pixel.r > pixel.b + .1f) red++;
                wall.SetActive(false);
                for(int frame=0;frame<4;frame++)yield return null;
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath("../output/unity/mvp-review/text-positive-control.png"),pixels.EncodeToPNG());
                foreach (var pixel in pixels.GetPixels()) if (pixel.r > pixel.b + .1f) unobstructedRed++;
            }
            finally
            {
                RenderTexture.active = prior; camera.targetTexture = null;
                Object.Destroy(camera.gameObject); Object.Destroy(target); Object.Destroy(pixels);
                Object.Destroy(label); Object.Destroy(wall); Object.Destroy(mat);
            }
            Assert.That(red, Is.EqualTo(0), "Red station text was rendered through an opaque blue wall.");
            Assert.That(unobstructedRed, Is.GreaterThan(40), "Text must remain visible when the wall is removed.");
        }
    }
}
