using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GameVSM.Editor
{
    // Modular, metre-scaled study reconstruction. See location research for surveyed
    // cross-section vs inferred length/placement. Never edits the train source asset.
    public static partial class BuildEnvironmentArt
    {
        const string Assets = "Assets/GameVSM";
        const string Art = Assets + "/Generated/Environment";
        const string Prefabs = Assets + "/Resources/Environments";
        static readonly Dictionary<string, Material> mats = new();
        static readonly Dictionary<Vector3, Mesh> boxes = new();
        static Transform root;
        static int meshId;
        static Font font;

        [MenuItem("GameVSM/Art/Build location prefabs")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Art); Directory.CreateDirectory(Prefabs);
            AssetDatabase.Refresh(); CreateStudyMaterials.Create();
            mats.Clear(); boxes.Clear(); bevels.Clear(); columns.Clear(); foliage.Clear(); meshId = 0;
            font = Resources.Load<Font>("Manrope");
            Materials();
            // Temporary site roots live in a scratch scene: the user's scene is not marked dirty,
            // so Apply() right after a build is not refused. Untitled scenes cannot coexist, so
            // with an untitled scene open the build falls back to the active scene.
            var userScene = SceneManager.GetActiveScene();
            var scratch = default(Scene);
            if (Enumerable.Range(0, SceneManager.sceneCount).All(i => !string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)))
            {
                scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scratch);
            }
            try { BuildSites(); }
            finally
            {
                if (scratch.IsValid())
                {
                    if (userScene.IsValid()) SceneManager.SetActiveScene(userScene);
                    EditorSceneManager.CloseScene(scratch, true);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("GAMEVSM_ENVIRONMENTS_BUILT: depot, two stations, route");
        }

        static void BuildSites()
        {
            foreach (string site in new[] { "Depot", "Moskovsky", "Leningradsky", "Route" })
            {
                root = new GameObject(site).transform; walkZones.Clear();
                var siteRoot = root.gameObject; // Route() and Shadowless() swap root while building.
                try
                {
                    if (site == "Depot") Depot();
                    else if (site == "Route") Route();
                    else Station(site == "Leningradsky");
                    Life(site);
                    FinishWalk();
                    CombineModules(site);
                    AuthoredModules(site);
                    // Scenery moves relative to the fixed train during journeys. It is
                    // already spatially combined; Unity static batching would pin it in place.
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                            GameObjectUtility.GetStaticEditorFlags(t.gameObject) & ~StaticEditorFlags.BatchingStatic);
                    PrefabUtility.SaveAsPrefabAsset(root.gameObject, Prefabs + "/" + site + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(siteRoot); }
            }
        }

        static void Save(UnityEngine.Object value, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing != null) { EditorUtility.CopySerialized(value, existing); UnityEngine.Object.DestroyImmediate(value); }
            else AssetDatabase.CreateAsset(value, path);
        }
        static void Material(string id, Color color, float gloss = .25f, string texture = null, float metal = 0)
        {
            Material m = texture == null ? new Material(Shader.Find("Universal Render Pipeline/Lit")) :
                new Material(AssetDatabase.LoadAssetAtPath<Material>($"{Assets}/Art/Materials/{texture}/{texture}.mat"));
            m.name = id; m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", gloss);
            m.SetFloat("_Metallic", metal); m.enableInstancing = true;
            // Non-metallic coatings must not inherit a metal mask from source steel.
            if (metal == 0) { m.SetTexture("_MetallicGlossMap", null); m.DisableKeyword("_METALLICSPECGLOSSMAP"); }
            if (texture != null) m.SetFloat("_BumpScale", id is "Walkway" or "ServiceGreen" ? .12f : .45f);
            Save(m, Art + "/" + id + ".mat");
            mats[id] = AssetDatabase.LoadAssetAtPath<Material>(Art + "/" + id + ".mat");
        }
        static void Materials()
        {
            Save(new Material(Shader.Find("GameVSM/World Text")), Art + "/WorldText.mat");
            mats["WorldText"] = AssetDatabase.LoadAssetAtPath<Material>(Art + "/WorldText.mat");
            Material("Concrete", new Color(.49f, .51f, .5f), .18f, "white_plaster_02");
            Material("Walkway", new Color(.63f, .31f, .14f), .4f, "concrete_floor_02");
            Material("ServiceGreen", new Color(.24f, .39f, .3f), .35f, "concrete_floor_02");
            Material("Blue", new Color(.09f, .28f, .46f), .36f);
            Material("White", new Color(.72f, .75f, .73f), .28f);
            Material("Plaster", new Color(.82f, .8f, .72f), .18f, "white_plaster_02");
            Material("Brick", new Color(.82f, .72f, .66f), .14f, "red_brick");
            Material("Steel", new Color(.55f, .58f, .6f), .62f, null, .85f);
            Material("Dark", new Color(.045f, .062f, .07f), .3f);
            Material("Roof", new Color(.18f, .21f, .22f), .33f);
            Material("Red", new Color(.51f, .065f, .048f), .3f);
            Material("Yellow", new Color(.9f, .62f, .12f), .3f);
            Material("Tiles", new Color(.88f, .87f, .84f), .22f, "large_floor_tiles_02");
            Material("Gravel", new Color(.64f, .62f, .57f), .08f, "gravel_floor_02");
            Material("Asphalt", new Color(.68f, .7f, .71f), .12f, "asphalt_02");
            Material("Bark", new Color(.22f,.19f,.14f), .08f);
            Material("Leaves", new Color(.12f,.24f,.095f), .04f);
            Material("LeavesLight", new Color(.28f,.34f,.12f), .04f);
            Material("Field", new Color(.48f,.39f,.2f), .08f);
            Material("Water", new Color(.17f,.31f,.35f), .92f);
            Material("Grass", Color.white, .04f);
            var grass = new Texture2D(256,256,TextureFormat.RGB24,true) { name = "Meadow variation", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[256*256];
            for(int y=0;y<256;y++) for(int x=0;x<256;x++)
            {
                float n=Mathf.PerlinNoise(x*.025f,y*.025f)*.7f+Mathf.PerlinNoise(x*.4f,y*.4f)*.3f;
                pixels[y*256+x]=Color.Lerp(new Color(.18f,.23f,.10f),new Color(.46f,.43f,.24f),n);
            }
            grass.SetPixels(pixels);grass.Apply();Save(grass,Art+"/Meadow.asset");
            mats["Grass"].SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Meadow.asset"));
            mats["Grass"].SetTextureScale("_BaseMap",new Vector2(.045f,.045f));
            Material("Glass", new Color(.36f, .49f, .54f), .88f, null, .25f);
            Material("Lamp", new Color(.9f, .92f, .88f), .5f);
            mats["Lamp"].EnableKeyword("_EMISSION"); mats["Lamp"].SetColor("_EmissionColor", new Color(2, 2.1f, 1.95f));
            mats["Glass"].SetFloat("_Surface", 1); mats["Glass"].SetFloat("_ZWrite", 0);
            mats["Glass"].SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mats["Glass"].SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mats["Glass"].EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mats["Glass"].SetColor("_BaseColor", new Color(.56f, .65f, .67f, .32f));
            mats["Glass"].renderQueue = 3000;
            foreach (string id in new[] { "Walkway", "ServiceGreen" })
            {
                mats[id].SetTexture("_BaseMap", null);
                mats[id].SetFloat("_BumpScale", .06f);
            }
            foreach (var material in mats.Values) EditorUtility.SetDirty(material);
        }

        static void CombineModules(string site)
        {
            // Combine nearby static pieces only. This preserves spatial culling and
            // colliders, and avoids thousands of individual window/sleeper draw calls.
            var groups = root.GetComponentsInChildren<MeshFilter>()
                .Where(f => f.gameObject.isStatic && f.GetComponent<MeshRenderer>() != null)
                .GroupBy(f =>
                {
                    // Shadowless background shares larger cells: fewer batches, culling kept near the route.
                    var r = f.GetComponent<MeshRenderer>(); var p = f.transform.position;
                    float cell = r.shadowCastingMode == ShadowCastingMode.Off && r.sharedMaterial.name is not ("Glass" or "Lamp") ? 96 : 24;
                    return (r.sharedMaterial, r.shadowCastingMode, Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.z / cell));
                });
            int index = 0;
            foreach (var group in groups.ToArray())
            {
                var filters = group.ToArray();
                // A lone piece stays as is only if its mesh is an asset; per-crown route foliage meshes are
                // not saved, and would turn into a missing mesh in the prefab.
                if (filters.Length < 2 && EditorUtility.IsPersistent(filters[0].sharedMesh)) continue;
                var mesh = new Mesh { name = site + " spatial batch", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(filters.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = root.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray(), true, true);
                string path = Art + "/" + site + "Batch" + (++index).ToString("D4") + ".asset";
                Save(mesh, path);
                var combined = new GameObject("Секция 24 м — " + group.Key.Item1.name, typeof(MeshFilter), typeof(MeshRenderer));
                combined.transform.SetParent(root, false); combined.isStatic = true;
                combined.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var renderer = combined.GetComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key.Item1;
                renderer.shadowCastingMode = group.Key.Item1.name is "Glass" or "Lamp" ? ShadowCastingMode.Off : group.Key.Item2;
                foreach (var filter in filters)
                {
                    if (filter.GetComponent<Collider>() != null)
                    {
                        UnityEngine.Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());
                        UnityEngine.Object.DestroyImmediate(filter);
                    }
                    else UnityEngine.Object.DestroyImmediate(filter.gameObject);
                }
            }
        }

        static GameObject Box(string name, Vector3 p, Vector3 size, string material, bool collision = false)
        {
            if (!boxes.TryGetValue(size, out var mesh))
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mesh = UnityEngine.Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
                UnityEngine.Object.DestroyImmediate(primitive);
                var v = mesh.vertices; var n = mesh.normals; var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    v[i] = Vector3.Scale(v[i], size);
                    uv[i] = Mathf.Abs(n[i].y) > .5f ? new Vector2(v[i].x, v[i].z) :
                        Mathf.Abs(n[i].x) > .5f ? new Vector2(v[i].z, v[i].y) : new Vector2(v[i].x, v[i].y);
                }
                mesh.name = "Metre UV module"; mesh.vertices = v; mesh.uv = uv;
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                // Shared dimensions share one mesh; generated IDs are deterministic.
                string path = Art + "/Module" + (++meshId).ToString("D4") + ".asset";
                Save(mesh, path); mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); boxes[size] = mesh;
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false); go.transform.localPosition = p;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = mats[material];
            renderer.shadowCastingMode = material is "Glass" or "Lamp" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            if (collision) go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true; return go;
        }
        static void Beam(string name, Vector3 a, Vector3 b, float width, string m)
        {
            var go = Box(name, (a + b) / 2, new Vector3(width, (b - a).magnitude, width), m);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }
        // Collision only where the tube is a physical obstacle on a walkable area (posts, masts, rails).
        static GameObject Tube(string name, Vector3 a, Vector3 b, float diameter, string m, bool collision = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name;
            go.transform.SetParent(root, false); go.transform.position = (a + b) / 2;
            go.transform.localScale = new Vector3(diameter, (b - a).magnitude / 2, diameter);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mats[m]; go.isStatic = true; return go;
        }
        static void Text(string text, Vector3 p, float size, float yaw = 0, Color? color = null)
        {
            var go = new GameObject(text, typeof(TextMesh)); go.transform.SetParent(root, false);
            go.transform.position = p; go.transform.rotation = Quaternion.Euler(0, yaw + 180, 0);
            var label = go.GetComponent<TextMesh>(); label.text = text; label.font = font;
            label.fontSize = 64; label.characterSize = size / 6; label.anchor = TextAnchor.MiddleCenter;
            label.color = color ?? Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = mats["WorldText"];
            go.AddComponent<WorldText>().Surface = mats["WorldText"];
        }
        // x: centre of the run; bed: inspection pit strip when there are no sleepers.
        static void Rail(float length, float z, bool sleepers, float x = 0, bool bed = true)
        {
            if (sleepers)
            {
                Box("Балластная призма", new Vector3(x, -.03f, z), new Vector3(length, .16f, 3.6f), "Gravel");
                for (float s = x - length / 2; s < x + length / 2; s += .7f)
                    Box("Железобетонная шпала", new Vector3(s, .07f, z), new Vector3(.24f, .15f, 2.65f), "Concrete");
            }
            else if (bed) Box("Смотровая канава", new Vector3(x, .004f, z), new Vector3(length, .02f, 1.15f), "Dark");
            foreach (float side in new[] { -.76f, .76f })
            {
                Box("Подошва рельса", new Vector3(x, .12f, z + side), new Vector3(length, .035f, .14f), "Steel");
                Box("Шейка рельса", new Vector3(x, .16f, z + side), new Vector3(length, .09f, .024f), "Steel");
                Box("Головка рельса", new Vector3(x, .215f, z + side), new Vector3(length, .042f, .072f), "Steel");
            }
        }
        static void Handrail(float a, float b, float y, float z, string m = "Steel", bool barrier = false)
        {
            for (float x = a; x <= b; x += 2)
            {
                Tube("Стойка ограждения", new Vector3(x, y, z), new Vector3(x, y + 1.05f, z), .045f, m);
                Box("Фланец стойки", new Vector3(x, y + .006f, z), new Vector3(.12f, .012f, .12f), m);
            }
            if (barrier) Barrier("Коллизия перил", new Vector3(a, y, z), new Vector3(b, y, z), 1.1f);
            foreach (float h in new[] { .5f, 1.05f })
                Tube("Поручень", new Vector3(a, y + h, z), new Vector3(b, y + h, z), .043f, m);
            Box("Борт настила", new Vector3((a + b) / 2, y + .055f, z), new Vector3(b - a, .11f, .025f), m);
        }
        // Finished visible sides: plinth, corner and bay pilasters, windows with reveals and sills on
        // all four sides, cornice, parapet with coping, downpipes into ground receivers and sheltered
        // entrances on the main side. A deterministic seed varies each facade; far ones get flat panes.
        static void Building(string name, float x, float z, float length, float height, float depth, string wall)
        {
            var rng = new System.Random(Mathf.RoundToInt(x * 31 + z * 17 + length * 7 + height * 3));
            bool far = new Vector2(x, z).magnitude > 140;
            string trim = wall == "White" ? "Concrete" : "White";
            Box(name, new Vector3(x, height / 2, z), new Vector3(length, height, depth), wall, true);
            Box("Цоколь", new Vector3(x, .15f, z), new Vector3(length + .24f, .9f, depth + .24f), "Concrete");
            Box("Карниз", new Vector3(x, height - .3f, z), new Vector3(length + .35f, .25f, depth + .35f), trim);
            Box("Кровля здания", new Vector3(x, height + .12f, z), new Vector3(length - .3f, .12f, depth - .3f), "Roof");
            foreach (int s in new[] { -1, 1 })
            {
                Box("Парапет", new Vector3(x, height + .35f, z + s * (depth / 2 - .12f)), new Vector3(length, .7f, .24f), wall);
                Box("Парапет", new Vector3(x + s * (length / 2 - .12f), height + .35f, z), new Vector3(.24f, .7f, depth), wall);
                Box("Отлив парапета", new Vector3(x, height + .73f, z + s * (depth / 2 - .12f)), new Vector3(length + .1f, .06f, .36f), "Steel");
                Box("Отлив парапета", new Vector3(x + s * (length / 2 - .12f), height + .73f, z), new Vector3(.36f, .06f, depth + .1f), "Steel");
                foreach (int t in new[] { -1, 1 })
                    Box("Угловая пилястра", new Vector3(x + s * length / 2, height / 2, z + t * depth / 2), new Vector3(.6f, height - .1f, .6f), trim);
            }
            int main = z < 0 ? 1 : -1;
            foreach (int s in new[] { -1, 1 })
            {
                Facade(x, z, true, s, length, depth / 2, height, s == main ? (length > 50 ? 2 : 1) : 0, rng, far, trim);
                Facade(x, z, false, s, depth, length / 2, height, 0, rng, far, trim);
            }
        }

        static void Depot()
        {
            DepotEntrance(); DepotSign();
            const float length = 228; // Inferred set length, not a measured depot dimension.
            Box("Территория", new Vector3(0, -.27f, 0), new Vector3(1400, .25f, 1200), "Grass", true);
            Box("Асфальтовый проезд", new Vector3(0, -.085f, -24), new Vector3(1400, .12f, 11), "Asphalt", true);
            Box("Плита цеха", new Vector3(0, -.18f, 0), new Vector3(length, .36f, 27.6f), "Concrete", true);
            Box("Проход персонала", new Vector3(0, .013f, -4.55f), new Vector3(length, .025f, 3), "Walkway");
            Box("Оборудование — зелёная зона", new Vector3(0, .014f, -10), new Vector3(length, .027f, 4.3f), "ServiceGreen");
            foreach (float z in new[] { -6.1f, -3.02f, -12.2f })
                Box("Граница безопасного прохода", new Vector3(0, .03f, z), new Vector3(length, .01f, .08f), "Yellow");
            foreach (float z in new[] { -7.5f, 0, 7.5f }) Rail(length, z, false);
            foreach (int sign in new[] { -1, 1 })
            {
                Box("Цоколь корпуса", new Vector3(0, 1.02f, sign * 13.8f), new Vector3(length, 2.04f, .28f), "Concrete", true);
                Box("Сэндвич-панели", new Vector3(0, 7.6f, sign * 13.8f), new Vector3(length, 4.1f, .18f), "White", true);
                Box("Подкрановый путь", new Vector3(0, 6.25f, sign * 11.9f), new Vector3(length, .48f, .28f), "Blue");
                for (float y = 2.8f; y <= 3.4f; y += .3f)
                    Tube("Магистраль коммуникаций", new Vector3(-113, y, sign * 13.45f), new Vector3(113, y, sign * 13.45f), .065f, "Blue");
                for (float x = -111; x < 114; x += 6)
                {
                    Box("Остекление корпуса", new Vector3(x, 3.8f, sign * 13.8f), new Vector3(5.8f, 3.35f, .04f), "Glass");
                    foreach (float d in new[] { -2.95f, -1.5f, 0, 1.5f, 2.95f })
                        Box("Стойка переплёта", new Vector3(x + d, 3.8f, sign * 13.8f), new Vector3(.055f, 3.4f, .12f), "White");
                    foreach (float y in new[] { 2.12f, 3.8f, 5.48f })
                        Box("Ригель переплёта", new Vector3(x, y, sign * 13.8f), new Vector3(6, .055f, .12f), "White");
                    Box("Стеновая стойка", new Vector3(x - 3, 4.6f, sign * 13.45f), new Vector3(.3f, 9.2f, .38f), "White", true);
                    Box("База стойки", new Vector3(x - 3, .12f, sign * 13.45f), new Vector3(.55f, .24f, .65f), "Blue");
                }
            }
            for (float x = -108; x <= 108; x += 6)
            {
                foreach (float y in new[] { 8, 9.25f })
                    Beam("Пояс стропильной фермы", new Vector3(x, y, -13.5f), new Vector3(x, y, 13.5f), .12f, "White");
                for (float z = -13.5f; z < 13; z += 3)
                {
                    Beam("Раскос фермы", new Vector3(x, 8, z), new Vector3(x, 9.25f, z + 1.5f), .07f, "White");
                    Beam("Раскос фермы", new Vector3(x, 9.25f, z + 1.5f), new Vector3(x, 8, z + 3), .07f, "White");
                }
                foreach (float z in new[] { -4.5f, 4.5f, -10f })
                {
                    Tube("Подвес", new Vector3(x, 8, z), new Vector3(x, 6.5f, z), .02f, "Steel");
                    Box("Корпус светильника", new Vector3(x, 6.46f, z), new Vector3(1.6f, .12f, .3f), "Roof");
                    Box("Рассеиватель", new Vector3(x, 6.39f, z), new Vector3(1.5f, .022f, .25f), "Lamp");
                }
            }
            foreach (float z in new[] { -10.3f, 0, 10.3f })
                Box("Полоса кровли", new Vector3(0, 9.55f, z), new Vector3(length, .16f, z == 0 ? 10 : 6.9f), "White");
            foreach (float z in new[] { -6, 6 })
            {
                Box("Зенитный фонарь", new Vector3(0, 9.62f, z), new Vector3(length, .035f, 1.8f), "Glass");
                for (float x = -114; x < 114; x += 2)
                    Box("Переплёт фонаря", new Vector3(x, 9.64f, z), new Vector3(.055f, .09f, 1.8f), "White");
            }
            foreach (float x in new[] { -80, 20, 72 })
            {
                // Two flanges + narrow web make the crane beam read as steelwork.
                foreach (float y in new[] { 6.1f, 7.1f }) Box("Пояс крановой балки", new Vector3(x, y, 0), new Vector3(.6f, .12f, 24), "Blue");
                Box("Стенка крановой балки", new Vector3(x, 6.6f, 0), new Vector3(.16f, .9f, 24), "Blue");
                for (float z = -11; z <= 11; z += 1.5f)
                    Box("Ребро жёсткости", new Vector3(x, 6.6f, z), new Vector3(.58f, .9f, .045f), "Blue");
                Box("Тележка тельфера", new Vector3(x, 7.35f, 3), new Vector3(1.4f, .5f, 1.3f), "Blue");
                Tube("Подвес крюка", new Vector3(x, 6.1f, 3), new Vector3(x, 4.8f, 3), .03f, "Steel");
            }
            foreach (float z in new[] { 2.45f, 5.05f })
            {
                Box("Сервисный настил", new Vector3(0, 1.22f, z), new Vector3(214, .16f, .85f), "Blue", true);
                Handrail(-106, 106, 1.3f, z + .42f, barrier: true);
                // Finished deck edges and closed ends.
                foreach (float e in new[] { -.43f, .43f }) Box("Кромочный уголок настила", new Vector3(0, 1.315f, z + e), new Vector3(214, .05f, .05f), "Steel");
                foreach (float x in new[] { -107f, 107 })
                {
                    foreach (float e in new[] { -.4f, .4f })
                        Tube("Торцевая стойка ограждения", new Vector3(x, 1.3f, z + e), new Vector3(x, 2.35f, z + e), .045f, "Steel", true);
                    foreach (float h in new[] { 1.8f, 2.35f }) Tube("Торцевой поручень", new Vector3(x, h, z - .4f), new Vector3(x, h, z + .4f), .043f, "Steel");
                }
                for (float x = -104; x <= 106; x += 4)
                    Box("Опора настила", new Vector3(x, .6f, z), new Vector3(.1f, 1.2f, .14f), "Blue", true);
            }
            AccessStairs();
            for (float x = -95; x < 108; x += 15) Cabinet(x);
            foreach (float x in new[] { -114, 114 })
            {
                Box("Надворотный ригель", new Vector3(x, 7.8f, 0), new Vector3(.25f, 3.4f, 27.6f), "White");
                foreach (float z in new[] { -11.5f, -3.75f, 3.75f, 11.5f })
                    Box("Простенок ворот", new Vector3(x, 3, z), new Vector3(.3f, 6, 2.4f), "White", true);
                foreach (float z in new[] { -7.5f, 0, 7.5f })
                {
                    Box("Поднятые секционные ворота", new Vector3(x, 6, z), new Vector3(.35f, .85f, 5), "Blue");
                    Text("ПУТЬ " + (z / 7.5f + 2), new Vector3(x + (x < 0 ? .2f : -.2f), 6.95f, z), .65f, x < 0 ? 90 : -90);
                }
            }
            Building("Служебный корпус", -42, 40, 66, 10, 17, "Brick");
            Building("Промышленный корпус — фон", 92, 62, 74, 14, 28, "Plaster");
            Building("Складской корпус", -100, 85, 110, 8, 25, "White");
            DepotTerritory();
            // Service yard details break the blank apron into a readable workplace.
            for (float x=-102;x<112;x+=12)
                foreach (int side in new[] { -1, 1 })
                {
                    Box("Шов наружных панелей",new Vector3(x,7.5f,side*13.92f),new Vector3(.035f,4,.035f),"Steel");
                    Downpipe(new Vector3(x,9.4f,side*14.05f),new Vector3(0,0,side),.01f);
                }
            foreach (float x in new[] {-114f,114f})
            {
                Box("Синяя фасадная полоса",new Vector3(x,8,0),new Vector3(.3f,.8f,27.7f),"Blue");
                Box("Козырёк входа персонала",new Vector3(x,3.2f,-12),new Vector3(3,.16f,2.8f),"Roof");
                float s = Mathf.Sign(x);
                Box("Дверь входа персонала",new Vector3(x+s*.18f,1.1f,-12),new Vector3(.05f,2.2f,1),"Blue");
                Box("Ступень входа персонала",new Vector3(x+s*.6f,.08f,-12),new Vector3(.8f,.16f,1.4f),"Concrete",true);
                Text("ВХОД ПЕРСОНАЛА",new Vector3(x+s*.2f,2.55f,-12),.09f,s*90);
            }
            Building("Ремонтная мастерская",65,-57,65,8,19,"White");
            for (float x=20;x<100;x+=10)
            {
                Tube("Защитный столбик",new Vector3(x,-.1f,-15),new Vector3(x,.9f,-15),.12f,"Yellow",true);
            }
            for (int i=0;i<4;i++) Person(new Vector3(-20+i*25,.04f,-9),90,i%2==0,"DepotWorker");
            Box("Информационный щит цеха",new Vector3(59,1.7f,-10.9f),new Vector3(2.5f,1.1f,.07f),"Blue");
            Text("МЕТАЛЛОСТРОЙ\nПРИЁМКА СОСТАВА · ПУТЬ 2",new Vector3(59,1.7f,-10.84f),.11f);
            Lights(true);
            Reflection(new Vector3(46, 3.6f, -3), new Vector3(36, 9, 27));
            Reflection(new Vector3(-36, 3.6f, 0), new Vector3(125, 9, 27));
            Person(new Vector3(43, .04f, -4.7f), 110, false, "DepotWorker");
        }

        static void AccessStairs()
        {
            Box("Площадка W03", new Vector3(47.5f, 1.22f, -2.52f), new Vector3(1.65f, .16f, 1.64f), "Blue", true);
            for (int step = 0; step < 7; step++)
            {
                float z = -5.15f + step * .29f, top = (step + 1) * .18f;
                Box("Тонкая проступь", new Vector3(47.5f, top - .025f, z), new Vector3(1.3f, .05f, .31f), "Steel");
                // Invisible filled step collider prevents feet dropping into the open steel frame.
                var c = new GameObject("Коллизия ступени").AddComponent<BoxCollider>();
                c.transform.SetParent(root, false); c.transform.position = new Vector3(47.5f, top / 2, z);
                c.size = new Vector3(1.3f, top, .3f);
                Box("Противоскользящая кромка", new Vector3(47.5f, top + .004f, z - .115f), new Vector3(1.25f, .012f, .05f), "Yellow");
            }
            foreach (float x in new[] { 46.82f, 48.18f })
            {
                Beam("Косоур", new Vector3(x, .07f, -5.35f), new Vector3(x, 1.22f, -3.22f), .1f, "Blue");
                Tube("Наклонный поручень", new Vector3(x, 1.02f, -5.35f), new Vector3(x, 2.25f, -3.22f), .042f, "Steel", true);
                foreach (float z in new[] { -5.35f, -3.22f })
                    Tube("Опора поручня", new Vector3(x, z < -4 ? .06f : 1.25f, z), new Vector3(x, z < -4 ? 1.02f : 2.25f, z), .04f, "Steel", true);
            }
        }
        static void Cabinet(float x)
        {
            Box("Электрошкаф", new Vector3(x, .95f, -11.5f), new Vector3(1.35f, 1.9f, .65f), "White", true);
            Box("Дверца шкафа", new Vector3(x, .98f, -11.16f), new Vector3(1.24f, 1.75f, .035f), "Concrete");
            Box("Панель управления", new Vector3(x, 1.37f, -11.13f), new Vector3(.4f, .22f, .022f), "Dark");
            Tube("Ручка шкафа", new Vector3(x + .48f, .85f, -11.09f), new Vector3(x + .48f, 1.1f, -11.09f), .025f, "Steel");
            Text("380 В", new Vector3(x, .8f, -11.09f), .15f);
            Box("Тележка инструмента", new Vector3(x + 2, .42f, -10.6f), new Vector3(.85f, .72f, .5f), "Blue", true);
            for (int i = 0; i < 4; i++) Box("Выдвижной ящик", new Vector3(x + 2, .22f + i * .15f, -10.34f), new Vector3(.75f, .12f, .022f), "Roof");
            foreach (float s in new[] { -.3f, .3f }) Tube("Колесо тележки", new Vector3(x + 2 + s, .075f, -10.85f), new Vector3(x + 2 + s, .075f, -10.35f), .13f, "Dark");
            // Workplace: riser to the wall cable tray, mat, a bench at every second cabinet.
            Tube("Кабельный стояк", new Vector3(x - .45f, 1.9f, -11.72f), new Vector3(x - .45f, 5.86f, -11.72f), .07f, "Steel");
            Tube("Кабельный стояк", new Vector3(x - .45f, 5.86f, -11.72f), new Vector3(x - .45f, 5.86f, -12.9f), .07f, "Steel");
            Box("Диэлектрический коврик", new Vector3(x, .006f, -10.62f), new Vector3(1.5f, .012f, .85f), "Dark");
            if (Mathf.RoundToInt((x + 95) / 15) % 2 == 0)
            {
                Box("Столешница верстака", new Vector3(x - 2.5f, .9f, -11.35f), new Vector3(1.6f, .06f, .72f), "Roof", true);
                foreach (float dx in new[] { -.72f, .72f })
                    foreach (float dz in new[] { -.3f, .3f })
                        Box("Ножка верстака", new Vector3(x - 2.5f + dx, .435f, -11.35f + dz), new Vector3(.06f, .87f, .06f), "Blue");
                Box("Полка верстака", new Vector3(x - 2.5f, .25f, -11.35f), new Vector3(1.5f, .03f, .62f), "Blue");
                Box("Тиски", new Vector3(x - 3.05f, 1, -11.1f), new Vector3(.2f, .14f, .18f), "Dark");
            }
            else Tube("Огнетушитель", new Vector3(x - 1.2f, 0, -11.6f), new Vector3(x - 1.2f, .62f, -11.6f), .17f, "Red");
        }
        // Territory fence run: posts every ~3 m, thicker end posts, a plinth, continuous wires and
        // one collider along the run. Runs are axis-aligned.
        static void Fence(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; float length = d.magnitude; int n = Mathf.Max(1, Mathf.RoundToInt(length / 3));
            for (int i = 0; i <= n; i++)
            {
                bool end = i == 0 || i == n;
                Box(end ? "Концевая стойка ограждения" : "Стойка ограждения территории", a + d * i / n + Vector3.up * 1.15f,
                    end ? new Vector3(.12f, 2.3f, .12f) : new Vector3(.07f, 2.3f, .07f), "Roof");
            }
            Box("Цоколь ограждения", (a + b) / 2, Mathf.Abs(d.x) > Mathf.Abs(d.z) ? new Vector3(length, .3f, .16f) : new Vector3(.16f, .3f, length), "Concrete");
            for (float h = .45f; h <= 2.15f; h += .3f) Beam("Проволока", a + Vector3.up * h, b + Vector3.up * h, .014f, "Roof");
            Beam("Верхний прогон ограждения", a + Vector3.up * 2.25f, b + Vector3.up * 2.25f, .04f, "Roof");
            Barrier("Коллизия ограждения", a, b, 2.3f);
        }

        static void ContactNetwork()
        {
            for (float x = -108; x < 275; x += 36)
            {
                foreach (float z in new[] { -28, 40 })
                {
                    Box("Мачта контактной сети", new Vector3(x, 5, z), new Vector3(.25f, 10, .35f), "Roof");
                    Box("Фундамент мачты", new Vector3(x, .4f, z), new Vector3(.75f, .8f, .9f), "Concrete");
                }
                Beam("Поперечина контактной сети", new Vector3(x, 9.7f, -28), new Vector3(x, 9.7f, 40), .12f, "Roof");
                for (int track = -2; track <= 3; track++)
                {
                    Beam("Подвес контактного провода", new Vector3(x, 9.7f, track * 12), new Vector3(x, 5.85f, track * 12), .025f, "Dark");
                    Tube("Изолятор", new Vector3(x, 9.2f, track * 12), new Vector3(x, 8.9f, track * 12), .12f, "White");
                }
            }
            for (int track = -2; track <= 3; track++)
            {
                Beam("Контактный провод", new Vector3(-126, 5.85f, track * 12), new Vector3(280, 5.85f, track * 12), .014f, "Dark");
                Beam("Несущий трос", new Vector3(-126, 7.3f, track * 12), new Vector3(280, 7.3f, track * 12), .018f, "Dark");
            }
        }
        static void Route()
        {
            var routeRoot = root;
            var chunks = new List<Transform>();
            for (int chunk = 0; chunk < 4; chunk++)
            {
                root = new GameObject("Перегон — участок " + chunk).transform;
                root.SetParent(routeRoot, false);
                Box("Земля", new Vector3(0, -.4f, 0), new Vector3(320, .4f, 1000), "Grass", true);
                Box("Насыпь", new Vector3(0,-.13f,2.5f),new Vector3(320,.3f,12),"Gravel");
                Rail(320, 0, true); Rail(320, 5, true);
                // Repeating near-field sleepers and poles establish optical flow from every window.
                for (float x = -160; x < 160; x += 40)
                {
                    Box("Мачта перегона", new Vector3(x,4.4f,-3.8f),new Vector3(.2f,8.8f,.24f),"Concrete");
                    Beam("Консоль",new Vector3(x,6.8f,-3.8f),new Vector3(x,5.9f,.7f),.06f,"Steel");
                    Tube("Изолятор",new Vector3(x,6.4f,-2.8f),new Vector3(x,6.1f,-2.8f),.15f,"White");
                }
                Beam("Контактный провод",new Vector3(-160,5.85f,0),new Vector3(160,5.85f,0),.014f,"Dark");
                for (int i = 0; i < 120; i++)
                {
                    float x = -158 + Mathf.Repeat(i * 47.73f + chunk * 19, 316);
                    float z = (i % 2 == 0 ? -1 : 1) * (18 + Mathf.Repeat(i * 21.13f, 145));
                    Tree(new Vector3(x,0,z),5 + i % 7);
                }
                for (int i=0;i<40;i++)
                {
                    float x=-155+i*7.9f;
                    Box("Стойка путевого ограждения",new Vector3(x,.75f,12),new Vector3(.07f,1.5f,.07f),"Concrete");
                    if(i%3==0)Tree(new Vector3(x,0,-10-Mathf.Repeat(i*2.71f,6)),1.8f+i%3*.35f);
                }
                for(int y=0;y<3;y++) Beam("Путевое ограждение",new Vector3(-158,.35f+y*.4f,12),new Vector3(158,.35f+y*.4f,12),.022f,"Steel");
                for (int i = 0; i < 9; i++)
                {
                    var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere); hill.name = "Мягкий рельеф";
                    hill.transform.SetParent(root,false); hill.transform.localPosition = new Vector3(-145+i*36,-5, i%2 == 0 ? -230 : 240);
                    hill.transform.localScale = new Vector3(110,25+i%3*8,170);
                    hill.GetComponent<Renderer>().sharedMaterial = mats["Grass"]; UnityEngine.Object.DestroyImmediate(hill.GetComponent<Collider>()); hill.isStatic = true;
                }
                if (chunk % 2 == 0)
                {
                    for (int i=0;i<4;i++) Building("Деревня за полем", -80+i*34, -110, 12,4+i%2,9,"Plaster");
                    Box("Поле",new Vector3(0,-.12f,-62),new Vector3(210,.025f,55),"Field");
                    for (int i=0;i<18;i++) Box("Борозда поля",new Vector3(-98+i*11,-.095f,-62),new Vector3(.3f,.02f,54),"Grass");
                }
                else
                {
                    Box("Вода за насыпью",new Vector3(25,-.08f,70),new Vector3(150,.03f,45),"Water");
                    for (int i=0;i<18;i++) Tree(new Vector3(-120+i*14,0,98),7+i%4);
                }
                CombineModules("RouteSegment" + chunk);
                foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
                root.localPosition = new Vector3(-480+chunk*320,0,0); chunks.Add(root);
            }
            root = routeRoot;
            var motion = root.gameObject.AddComponent<RouteMotion>(); motion.Scenery = chunks.ToArray();
            Lights(false);
        }
        static void Tree(Vector3 p, float height)
        {
            Tube("Ствол дерева", p, p + Vector3.up * height * .75f, .22f, "Bark");
            // Multiple uneven branch clusters avoid the silhouette of a sphere on a pole.
            for (int crown=0;crown<9;crown++)
            {
                float angle = crown * 2.4f + p.x;
                float radius=height*(.11f+(crown%3)*.045f);
                Vector3 offset = new Vector3(Mathf.Sin(angle)*radius,height*(.34f+crown*.063f),Mathf.Cos(angle)*radius);
                if(height>3)Beam("Ветвь",p+Vector3.up*height*.28f,p+offset,.055f,"Bark");
                var go = new GameObject("Листва", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root,false); go.transform.localPosition = p + offset;
                go.transform.localScale = new Vector3(height*.29f,height*.27f,height*.29f);
                go.GetComponent<MeshFilter>().sharedMesh=Foliage(crown % 4);
                go.GetComponent<Renderer>().sharedMaterial = mats[crown%2 == 0 ? "Leaves" : "LeavesLight"];
                go.isStatic = true;
            }
        }
        static void Lights(bool depot)
        {
            var light = new GameObject("Мягкий облачный свет").AddComponent<Light>(); light.transform.SetParent(root, false);
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(56, -32, 0);
            light.color = new Color(.92f, .96f, 1); light.intensity = depot ? .65f : 1.2f;
            light.shadows = LightShadows.Soft; light.shadowStrength = .55f; light.shadowBias = .035f;
            if (depot) for (float x = -96; x <= 102; x += 18)
            {
                var lamp = new GameObject("Заполнение рабочей зоны").AddComponent<Light>(); lamp.transform.SetParent(root, false);
                lamp.type = LightType.Point; lamp.transform.position = new Vector3(x, 5.4f, -4.5f);
                lamp.color = new Color(1, .95f, .87f); lamp.range = 15; lamp.intensity = 2.5f;
                lamp.shadows = LightShadows.None;
            }
        }
        static void Reflection(Vector3 p, Vector3 size)
        {
            var probe = new GameObject("Отражение окружения").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(root, false); probe.transform.position = p;
            probe.size = size; probe.boxProjection = true; probe.resolution = 128;
            probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.nearClipPlane = .2f; probe.farClipPlane = 160; probe.intensity = .65f;
        }

        [MenuItem("GameVSM/Art/Apply environments to shift")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != Assets + "/Scenes/Metallostroy.unity") throw new InvalidOperationException("Open Metallostroy first.");
            if (scene.isDirty) throw new InvalidOperationException("Save or discard open scene edits before applying art.");
            var old = GameObject.Find("Металлострой — рабочая зона по фото");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.transform.parent == null) UnityEngine.Object.DestroyImmediate(light.gameObject);
            var client = UnityEngine.Object.FindFirstObjectByType<ShiftClient>();
            var env = client.GetComponent<ShiftEnvironment>() ?? client.gameObject.AddComponent<ShiftEnvironment>();
            var experience = client.GetComponent<ShiftExperience>() ?? client.gameObject.AddComponent<ShiftExperience>();
            if (client.GetComponent<TrainInformation>() == null) client.gameObject.AddComponent<TrainInformation>();
            experience.PassengerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Assets + "/Art/Characters/Passenger.prefab");
            experience.WomanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Assets + "/Art/Characters/PassengerWoman.prefab");
            experience.WorkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Assets + "/Art/Characters/DepotWorker.prefab");
            var seats = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t=>t.name.Contains("WalkSeat_")).ToArray();
            foreach (var collider in UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include))
                if (collider.name is "Obstacle" or "Seat navigation")
                {
                    var c = collider.bounds.center;
                    var seat = seats.FirstOrDefault(t=>Mathf.Abs(t.position.x-c.x)<.15f && Mathf.Abs(t.position.z-c.z)<.15f);
                    if (seat != null)
                    {
                        collider.name = "Seat navigation";
                        var size = collider.size; size.y = 1.29f; collider.size = size;
                        var position = collider.transform.position; position.y = 1.945f; collider.transform.position = position;
                    }
                }
            if (env.Current != null) UnityEngine.Object.DestroyImmediate(env.Current);
            env.Current = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Depot.prefab"));
            env.Location = "Depot"; env.Player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            env.Player.View.farClipPlane = 650;
            var hdr = AssetDatabase.LoadAssetAtPath<Texture>(Assets + "/Art/Lighting/overcast_soil_puresky_2k.hdr");
            var sky = new Material(Shader.Find("Skybox/Panoramic")); sky.name = "Overcast sky";
            sky.SetTexture("_MainTex", hdr); sky.SetFloat("_Exposure", .8f); sky.SetFloat("_Rotation", 32);
            Save(sky, Art + "/OvercastSky.mat"); RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(Art + "/OvercastSky.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.61f, .65f, .67f);
            RenderSettings.ambientEquatorColor = new Color(.49f, .51f, .52f);
            RenderSettings.ambientGroundColor = new Color(.36f, .37f, .34f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 180; RenderSettings.fogEndDistance = 650;
            RenderSettings.fogColor = new Color(.64f, .68f, .69f);
            var volume = UnityEngine.Object.FindFirstObjectByType<Volume>();
            if (volume != null && volume.sharedProfile.TryGet<ColorAdjustments>(out var color)) color.postExposure.Override(0);
            TrainGlass.Apply();
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("GAMEVSM_ENVIRONMENTS_APPLIED");
        }
    }
}
