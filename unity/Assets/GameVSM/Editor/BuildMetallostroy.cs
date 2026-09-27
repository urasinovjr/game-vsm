using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GameVSM.Editor
{
    public static class BuildMetallostroy
    {
        const string Art = "Assets/GameVSM/Art";
        const string Generated = "Assets/GameVSM/Generated";
        const string ScenePath = "Assets/GameVSM/Scenes/Metallostroy.unity";
        static Transform environment;
        static int nextMesh;
        static readonly Dictionary<string, Material> materials = new();

        [MenuItem("GameVSM/Build Metallostroy import scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("Scene already exists. Preserve artist edits: move it before regenerating.");
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Train/WhiteKrechet.glb");
            if (imported == null) throw new InvalidOperationException("glTFast has not imported WhiteKrechet.glb yet.");
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory("Assets/GameVSM/Scenes");
            AssetDatabase.Refresh();
            CreateStudyMaterials.Create();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            environment = new GameObject("Металлострой — рабочая зона по фото").transform;
            materials.Clear();
            nextMesh = 0;
            MakeMaterials();
            BuildHall();
            var train = (GameObject)PrefabUtility.InstantiatePrefab(imported);
            train.name = "Белый Кречет — импортная копия";
            JObject binding = JObject.Parse(File.ReadAllText(Art + "/Train/TrainBindings.json"));
            BindTrain(train, binding);
            AddPlayer();
            train.GetComponent<TrainVisibility>().Observer = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>().transform;
            SetLighting();
            ConfigureQuality();
            PlayerSettings.companyName = "GameVSM";
            PlayerSettings.productName = "Проводник ВСМ";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "ru.gamevsm.conductor");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Local development server is HTTP; production deployment must use HTTPS.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("GAMEVSM_SCENE_READY " + ScenePath);
        }

        static void MakeMaterials()
        {
            foreach (string id in new[] { "concrete_floor_02", "blue_metal_plate", "white_plaster_rough_02", "metal_plate", "gravel_floor_02" })
                materials[id] = AssetDatabase.LoadAssetAtPath<Material>($"{Art}/Materials/{id}/{id}.mat");
            Tint("walkway", "concrete_floor_02", new Color(.46f, .24f, .13f), .2f);
            Tint("green", "concrete_floor_02", new Color(.16f, .31f, .25f), .25f);
            Tint("blue", "blue_metal_plate", new Color(.19f, .38f, .57f), .35f);
            Tint("rail", "metal_plate", new Color(.43f, .46f, .49f), .65f);
            Flat("yellow", new Color(.93f, .67f, .15f), .25f);
            Flat("white", new Color(.78f, .8f, .79f), .3f);
            Flat("dark", new Color(.07f, .09f, .1f), .35f);
            Flat("lamp", new Color(.88f, .94f, 1), .5f);
            materials["lamp"].EnableKeyword("_EMISSION");
            materials["lamp"].SetColor("_EmissionColor", new Color(1.8f, 1.95f, 2.1f));
        }

        static void Tint(string id, string source, Color color, float smoothness)
        {
            var material = new Material(materials[source]);
            material.name = id;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, Generated + "/" + id + ".mat");
            materials[id] = material;
        }

        static void Flat(string id, Color color, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = id; material.color = color;
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, Generated + "/" + id + ".mat");
            materials[id] = material;
        }

        static GameObject Box(string name, Vector3 centre, Vector3 size, string material, bool solid = true)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name; item.transform.SetParent(environment);
            item.transform.position = centre;
            // UVs in metres: changing the beam/floor size does not stretch the texture.
            Mesh mesh = UnityEngine.Object.Instantiate(item.GetComponent<MeshFilter>().sharedMesh);
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Vector3.Scale(vertices[i], size);
                Vector3 p = vertices[i], n = normals[i];
                uv[i] = Mathf.Abs(n.y) > .5f ? new Vector2(p.x, p.z) :
                    Mathf.Abs(n.x) > .5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.RecalculateBounds(); mesh.RecalculateTangents();
            string meshPath = Generated + "/Mesh_" + (++nextMesh).ToString("D4") + ".asset";
            AssetDatabase.CreateAsset(mesh, meshPath);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterial = materials[material];
            item.GetComponent<BoxCollider>().size = size;
            if (!solid) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            item.isStatic = true;
            return item;
        }

        static void Beam(string name, Vector3 from, Vector3 to, float width, string material)
        {
            Vector3 delta = to - from;
            var beam = Box(name, (from + to) / 2, new Vector3(width, delta.magnitude, width), material, false);
            beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
        }

        static void BuildHall()
        {
            // 27.6 m cross-section and 7.5 m track centres have a source. Length,
            // roof and equipment positions remain a photo-based reconstruction.
            Box("Бетонная плита", new Vector3(37, -.18f, 0), new Vector3(72, .36f, 27.6f), "concrete_floor_02");
            Box("Рабочий проход", new Vector3(37, .012f, -4.5f), new Vector3(72, .025f, 3.1f), "walkway", false);
            Box("Зона оборудования", new Vector3(37, .015f, -9.8f), new Vector3(72, .03f, 4.8f), "green", false);
            foreach (float z in new[] { -6.1f, -2.9f })
                Box("Жёлтая граница прохода", new Vector3(37, .03f, z), new Vector3(72, .012f, .09f), "yellow", false);
            foreach (float track in new[] { -7.5f, 0, 7.5f })
            {
                Box("Канал обслуживания", new Vector3(37, .022f, track), new Vector3(72, .04f, 1.1f), "dark", false);
                foreach (float side in new[] { -.76f, .76f })
                {
                    Box("Подошва рельса", new Vector3(37, .08f, track + side), new Vector3(72, .04f, .14f), "rail", false);
                    Box("Шейка рельса", new Vector3(37, .14f, track + side), new Vector3(72, .1f, .025f), "rail", false);
                    Box("Головка рельса", new Vector3(37, .205f, track + side), new Vector3(72, .045f, .07f), "rail", false);
                }
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Цоколь стены", new Vector3(37, 1.1f, side * 13.8f), new Vector3(72, 2.2f, .26f), "concrete_floor_02");
                Box("Стеновая панель", new Vector3(37, 5.7f, side * 13.8f), new Vector3(72, 2, .2f), "white_plaster_rough_02");
                for (float x = 3; x <= 72; x += 6)
                {
                    Box("Стальная колонна", new Vector3(x, 4, side * 13.5f), new Vector3(.32f, 8, .42f), "white");
                    Box("Стекло светового пояса", new Vector3(x - 3, 3.45f, side * 13.8f), new Vector3(5.7f, 2.3f, .07f), "lamp", false);
                    Box("Переплёт окна", new Vector3(x - 3, 3.45f, side * 13.72f), new Vector3(.06f, 2.4f, .06f), "white", false);
                }
            }
            for (float x = 3; x <= 72; x += 6)
            {
                Beam("Пояс фермы", new Vector3(x, 7.8f, -13.5f), new Vector3(x, 7.8f, 13.5f), .12f, "white");
                Beam("Верхний пояс", new Vector3(x, 9.2f, -13.5f), new Vector3(x, 9.2f, 13.5f), .13f, "white");
                for (float z = -13.5f; z < 13; z += 3)
                    Beam("Раскос фермы", new Vector3(x, 7.8f, z), new Vector3(x, 9.2f, z + 3), .08f, "white");
                foreach (float z in new[] { -4.5f, 4.5f })
                {
                    Beam("Подвес светильника", new Vector3(x, 7.8f, z), new Vector3(x, 6.6f, z), .025f, "dark");
                    Box("Линейный светильник", new Vector3(x, 6.55f, z), new Vector3(2, .07f, .24f), "lamp", false);
                }
            }
            Box("Кровля", new Vector3(37, 9.5f, 0), new Vector3(72, .15f, 27.6f), "white", false);
            // Lower and upper service structures are recognisable features in the photos.
            foreach (float z in new[] { 2.35f, 5.1f })
            {
                Box("Настил эстакады", new Vector3(37, 1.24f, z), new Vector3(28, .14f, .85f), "blue");
                for (float x = 24; x <= 50; x += 2)
                {
                    Box("Опора эстакады", new Vector3(x, .6f, z), new Vector3(.12f, 1.2f, .12f), "blue");
                    Beam("Стойка ограждения", new Vector3(x, 1.3f, z + .4f), new Vector3(x, 2.35f, z + .4f), .045f, "white");
                }
                foreach (float y in new[] { 1.85f, 2.35f })
                    Beam("Поручень", new Vector3(23, y, z + .4f), new Vector3(51, y, z + .4f), .04f, "white");
            }
            // A short access platform at W03's service entrance; placement is provisional.
            Box("Площадка входа W03", new Vector3(47.5f, 1.18f, -2.5f), new Vector3(2, .24f, 1.6f), "blue");
            for (int step = 0; step < 7; step++)
                Box("Ступень", new Vector3(47.5f, .09f * (step + 1), -5.1f + step * .29f),
                    new Vector3(1.3f, .18f * (step + 1), .3f), "metal_plate");
            for (int i = 0; i < 5; i++)
            {
                float x = 26 + i * 5;
                Box("Шкаф оборудования", new Vector3(x, .95f, -10), new Vector3(1.2f, 1.9f, .6f), "white");
                Box("Панель шкафа", new Vector3(x, 1.2f, -9.68f), new Vector3(.8f, .45f, .035f), "dark", false);
                Box("Ручка", new Vector3(x + .4f, .95f, -9.65f), new Vector3(.035f, .22f, .04f), "rail", false);
                Beam("Трубопровод", new Vector3(x, .1f, -10.5f), new Vector3(x, 3, -10.5f), .07f, "blue");
            }
        }

        static void BindTrain(GameObject train, JObject bindings)
        {
            var objects = train.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.Length > 7 && t.name[0] == 'n' && t.name[6] == '_').ToDictionary(t => t.name);
            int attached = 0;
            var zones = new List<TrainVisibility.Zone>();
            foreach (JObject record in bindings["nodes"])
            {
                if (!objects.TryGetValue((string)record["nodeName"], out Transform target))
                    throw new InvalidOperationException("Missing imported node: " + record["nodeName"]);
                JObject extra = (JObject)record["extras"];
                string entity = (string)extra["entity"];
                if (entity == "car_assembly" || entity == "walk_car" || entity == "walk_connection")
                {
                    var renderers = target.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        zones.Add(new TrainVisibility.Zone { Root = target.gameObject, Bounds = bounds, Interior = entity == "walk_car" });
                    }
                }
                string kind = (string)extra["interaction"];
                bool automatic = (string)extra["entity"] == "walk_door_leaf";
                if (!automatic && kind != "hinged_door" && kind != "folding_table" && kind != "sliding_entry_door") continue;
                var mechanism = target.gameObject.AddComponent<TrainMechanism>();
                mechanism.Kind = kind; mechanism.Label = (string)extra["interaction_label"];
                mechanism.Range = (float?)extra["range_m"] ?? 2.7f;
                mechanism.Axis = Vector(record["rotationAxis"]);
                mechanism.AngleDegrees = ((float?)extra["open_angle"] ?? 0) * Mathf.Rad2Deg -
                    ((float?)extra["closed_angle"] ?? 0) * Mathf.Rad2Deg;
                mechanism.Plug = Vector(record["plug"]); mechanism.Slide = Vector(record["slide"]);
                if (automatic)
                {
                    JObject portal = bindings["portals"].OfType<JObject>().Single(p => (string)p["id"] == (string)extra["portal_id"]);
                    mechanism.Kind = "sliding_entry_door"; mechanism.Automatic = true;
                    mechanism.TriggerX = (float)portal["x"];
                    mechanism.Slide = new Vector3(0, 0, -(float)extra["slide_sign"] * (float)portal["slide"]);
                }
                // Native mesh colliders follow the pivots and retain real openings.
                foreach (var mesh in target.GetComponentsInChildren<MeshFilter>())
                    mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                attached++;
            }
            var collision = new GameObject("Навигационные препятствия поезда").transform;
            foreach (JObject rect in bindings["obstacles"])
            {
                float x0 = (float)rect["minX"], x1 = (float)rect["maxX"], z0 = (float)rect["minZ"], z1 = (float)rect["maxZ"];
                var item = new GameObject("Obstacle"); item.transform.SetParent(collision);
                item.transform.position = new Vector3((x0 + x1) / 2, 2.1f, (z0 + z1) / 2);
                item.AddComponent<BoxCollider>().size = new Vector3(x1 - x0, 1.6f, z1 - z0);
            }
            var floor = new GameObject("Train walk floor");
            floor.transform.position = new Vector3(0, 1.25f, 0);
            floor.AddComponent<BoxCollider>().size = new Vector3(195, .1f, 2.9f);
            train.AddComponent<TrainVisibility>().Zones = zones.ToArray();
            foreach (var renderer in train.GetComponentsInChildren<MeshRenderer>())
                foreach (var material in renderer.sharedMaterials)
                    if (material != null) material.enableInstancing = true;
            Debug.Log($"GAMEVSM_BOUND_MECHANISMS {attached}");
        }

        static Vector3 Vector(JToken value) => value is JArray a ? new Vector3((float)a[0], (float)a[1], (float)a[2]) : Vector3.zero;

        static void AddPlayer()
        {
            var player = new GameObject("Проводник");
            player.transform.position = new Vector3(52, .05f, -5);
            player.transform.rotation = Quaternion.Euler(0, -65, 0);
            var body = player.AddComponent<CharacterController>();
            body.height = 1.75f; body.center = new Vector3(0, .875f, 0);
            body.radius = .2f; body.stepOffset = .22f; body.skinWidth = .025f;
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera"; cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.68f, 0);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .08f; camera.farClipPlane = 180; camera.fieldOfView = 65;
            cameraObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var controller = player.AddComponent<FirstPersonController>(); controller.View = camera;
            var app = new GameObject("Учебная смена");
            app.AddComponent<ShiftClient>();
            app.AddComponent<StudyHud>().Player = controller;
        }

        static void SetLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.57f, .64f, .7f);
            RenderSettings.ambientEquatorColor = new Color(.4f, .44f, .47f);
            RenderSettings.ambientGroundColor = new Color(.2f, .23f, .22f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.65f, .7f, .72f);
            RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 65; RenderSettings.fogEndDistance = 160;
            var sun = new GameObject("Облачный дневной свет").AddComponent<Light>();
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(46, -35, 0);
            sun.color = new Color(.88f, .94f, 1); sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .65f;
            RenderSettings.sun = sun;
            for (int i = 0; i < 4; i++)
            {
                var light = new GameObject("Рабочее освещение " + i).AddComponent<Light>();
                light.type = LightType.Point; light.transform.position = new Vector3(26 + i * 8, 5.7f, -3);
                light.range = 13; light.intensity = 2.4f; light.color = new Color(1, .93f, .82f);
            }
            var volume = new GameObject("Цвет и экспозиция").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            profile.Add<ColorAdjustments>(true).postExposure.Override(.25f);
            AssetDatabase.CreateAsset(profile, Generated + "/DepotVolume.asset"); volume.sharedProfile = profile;
        }

        static void ConfigureQuality()
        {
            var source = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (source == null) throw new InvalidOperationException("Universal 3D template required.");
            var desktop = UnityEngine.Object.Instantiate(source);
            desktop.name = "Desktop URP"; desktop.shadowDistance = 65; desktop.msaaSampleCount = 4;
            var android = UnityEngine.Object.Instantiate(source);
            android.name = "Android URP"; android.shadowDistance = 25; android.msaaSampleCount = 2; android.renderScale = .8f;
            AssetDatabase.CreateAsset(desktop, Generated + "/DesktopURP.asset");
            AssetDatabase.CreateAsset(android, Generated + "/AndroidURP.asset");
            var quality = new GameObject("Профили качества").AddComponent<NativeQuality>();
            quality.Desktop = desktop; quality.Android = android;
            RenderSetup.Apply();
        }
    }
}
