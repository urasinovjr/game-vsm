using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameVSM.Editor
{
    // Metallostroy territory around the hall: staff route, perimeter, service yards and backdrop.
    // Staged reconstruction: the real checkpoint and its position are not confirmed by sources.
    // Route: wicket (158, -13.3) → painted path → crossing of track 1 at x 130 → start and
    // briefing at the east gate (128 → 118, z -5.7) → hall walkway → stair of car 3 at x 47.5.
    public static partial class BuildEnvironmentArt
    {
        const string Authored = Assets + "/Art/Environment";
        static readonly Vector3 DepotStart = new(128, .03f, -5.7f);
        static Mesh crownMesh;

        // Hand-made modules live outside Generated and are placed again on every build, so a
        // regeneration never drops them. Their world placement is stored in the module prefab.
        static void AuthoredModules(string site)
        {
            if (!AssetDatabase.IsValidFolder(Authored + "/" + site)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Authored + "/" + site }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                ((GameObject)PrefabUtility.InstantiatePrefab(asset)).transform.SetParent(root, true);
            }
        }

        // Kept for the existing command line. Entrance and sign are part of Depot() now, so this
        // only applies the rebuilt prefabs and stores the player's start on the staff route.
        [MenuItem("GameVSM/Art/Apply environments and place player at depot entrance")]
        public static void FinishEntrance()
        {
            Apply();
            var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            player.transform.SetPositionAndRotation(DepotStart, Quaternion.Euler(0, -90, 0));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        static void DepotSign()
        {
            var sign = new GameObject("Вывеска депо", typeof(TextMesh)); sign.transform.SetParent(root, false);
            sign.transform.localPosition = new Vector3(114.18f, 8, -7.5f); sign.transform.localRotation = Quaternion.Euler(0, 270, 0);
            var label = sign.GetComponent<TextMesh>(); label.text = "МЕТАЛЛОСТРОЙ\nПРИЁМКА СОСТАВА"; label.font = font;
            label.fontSize = 64; label.characterSize = .06f; label.anchor = TextAnchor.MiddleCenter;
            sign.GetComponent<MeshRenderer>().sharedMaterial = mats["WorldText"]; sign.AddComponent<WorldText>().Surface = mats["WorldText"];
        }

        // Concrete aprons at both hall gates; the east one carries the staff approach.
        static void DepotEntrance()
        {
            DepotWalkRoutes();
            Box("Площадка входа персонала", new Vector3(136, -.09f, 1.95f), new Vector3(44, .18f, 33.1f), "Concrete", true);
            Box("Площадка западных ворот", new Vector3(-137, -.09f, 1.95f), new Vector3(46, .18f, 33.1f), "Concrete", true);
            foreach (float x in new[] { 120f, 126, 132, 138, 144, 150, 156 })
                foreach (int s in new[] { -1, 1 })
                    Box("Деформационный шов", new Vector3(s * x, .002f, 1.95f), new Vector3(.025f, .006f, 33.1f), "Dark");
            // Painted route continues the hall walkway colour from the wicket to the gate.
            Box("Пешеходная дорожка", new Vector3(144.45f, .004f, -13.3f), new Vector3(27.1f, .008f, 1.8f), "Walkway");
            Box("Пешеходная дорожка", new Vector3(130, .004f, -11.65f), new Vector3(1.8f, .008f, 5.1f), "Walkway");
            Box("Пешеходная дорожка", new Vector3(122.55f, .004f, -5.55f), new Vector3(16.7f, .008f, 1.4f), "Walkway");
            foreach (float z in new[] { -14.24f, -12.36f })
                Box("Граница пешеходной дорожки", new Vector3(144.45f, .006f, z), new Vector3(27.1f, .01f, .08f), "Yellow");
            foreach (float z in new[] { -6.29f, -4.81f })
                Box("Граница пешеходной дорожки", new Vector3(121.65f, .006f, z), new Vector3(14.9f, .01f, .08f), "Yellow");
            // Crossing of track 1: rubber deck between the rails, below the controller step height.
            Box("Настил пешеходного перехода", new Vector3(130, .1f, -7.5f), new Vector3(1.8f, .2f, 3.2f), "Dark", true);
            // Invisible half steps on the path ends: 0.2 m is only 2 cm under stepOffset 0.22.
            foreach (float z in new[] { -9.25f, -5.75f })
            {
                var step = new GameObject("Коллизия подступа к настилу", typeof(BoxCollider)); step.transform.SetParent(root, false);
                step.transform.localPosition = new Vector3(130, .05f, z); step.GetComponent<BoxCollider>().size = new Vector3(1.8f, .1f, .3f); step.isStatic = true;
            }
            foreach (float z in new[] { -8.75f, -7.9f, -7.1f, -6.25f })
                Box("Полоса перехода", new Vector3(130, .204f, z), new Vector3(1.6f, .01f, .3f), "White");
            SignPost(new Vector3(131.7f, 0, -9.7f), 180, "БЕРЕГИСЬ ПОЕЗДА\nпереход по настилу", 1.4f, "Yellow", new Color(.05f, .05f, .05f));
            SignPost(new Vector3(132.3f, 0, -15.1f), 90, "ПРОХОД В ЦЕХ\nчерез переход", 1.3f, "Blue");
            // Briefing point next to the senior conductor, facing the path.
            foreach (float x in new[] { 118.6f, 120.4f })
                Tube("Стойка стенда", new Vector3(x, 0, -3.9f), new Vector3(x, 1.85f, -3.9f), .06f, "Steel", true);
            Box("Стенд инструктажа", new Vector3(119.5f, 1.35f, -3.9f), new Vector3(1.9f, .95f, .05f), "Blue");
            Text("МЕСТО ИНСТРУКТАЖА\nпуть 2 · вагон № 3", new Vector3(119.5f, 1.38f, -3.935f), .1f, 180);
            Checkpoint();
            DepotEntryDetails();
        }

        static void Checkpoint()
        {
            var c = new Vector3(153.75f, 0, -16.4f);
            Box("Проходная", c + new Vector3(0, 1.35f, 0), new Vector3(4.5f, 3, 3.6f), "White", true);
            Box("Цоколь проходной", c + new Vector3(0, .1f, 0), new Vector3(4.62f, .5f, 3.72f), "Concrete");
            Box("Кровля проходной", c + new Vector3(0, 3, 0), new Vector3(5.2f, .3f, 4.4f), "Roof");
            // Guard window faces the staff path; the door opens onto the apron.
            Box("Окно охраны", c + new Vector3(-.7f, 1.65f, 1.815f), new Vector3(2.4f, 1.1f, .03f), "Dark");
            Box("Стекло окна охраны", c + new Vector3(-.7f, 1.65f, 1.84f), new Vector3(2.4f, 1.1f, .02f), "Glass");
            Box("Подоконник", c + new Vector3(-.7f, 1.07f, 1.9f), new Vector3(2.6f, .06f, .26f), "White");
            Box("Дверь проходной", c + new Vector3(1.45f, 1.2f, 1.83f), new Vector3(1, 2.1f, .06f), "Blue");
            Box("Козырёк двери", c + new Vector3(1.45f, 2.55f, 2.3f), new Vector3(1.5f, .1f, .9f), "Roof");
            Text("ПРОХОДНАЯ", c + new Vector3(-.7f, 2.5f, 1.83f), .28f);
            Box("Табличка проходной", c + new Vector3(2.27f, 2.1f, 0), new Vector3(.04f, .8f, 3), "Blue");
            Text("ДЕПО МЕТАЛЛОСТРОЙ\nПРОХОДНАЯ", c + new Vector3(2.31f, 2.1f, 0), .15f, 90);
            Box("Тротуар к проходной", new Vector3(161.5f, -.1f, -13.3f), new Vector3(7, .1f, 2), "Concrete", true);
            Box("Тротуар к проходной", new Vector3(164, -.1f, -15.3f), new Vector3(2, .1f, 6.1f), "Concrete", true);
        }

        static void DepotTerritory()
        {
            OuterTracks(); Perimeter(); HallServices(); ServiceYards(); Backdrop();
            DepotHallDetails();
            foreach (int s in new[] { -1, 1 })
                Box("Отмостка корпуса", new Vector3(0, -.07f, s * 14.6f), new Vector3(228.6f, .16f, 1.6f), "Concrete");
            // Drive: curbs, drainage tray with grates, centre line; the staff parking opens onto it.
            Box("Бортовой камень", new Vector3(0, -.02f, -18.43f), new Vector3(1400, .25f, .15f), "Concrete");
            Box("Бортовой камень", new Vector3(-342.5f, -.02f, -29.57f), new Vector3(715, .25f, .15f), "Concrete");
            Box("Бортовой камень", new Vector3(402.5f, -.02f, -29.57f), new Vector3(595, .25f, .15f), "Concrete");
            Box("Водоотводный лоток", new Vector3(-1, -.018f, -18.75f), new Vector3(318, .02f, .3f), "Dark");
            for (float x = -150; x <= 150; x += 20)
                Box("Решётка дождеприёмника", new Vector3(x, -.006f, -18.75f), new Vector3(.8f, .012f, .34f), "Steel");
            for (float x = -600; x <= 600; x += 8)
                if (Mathf.Abs(x - 110) > 4) Box("Осевая разметка", new Vector3(x, -.022f, -24), new Vector3(3, .006f, .12f), "White");
            for (float z = -29; z < -18.8f; z += 1.1f)
                Box("Пешеходный переход", new Vector3(110, -.02f, z), new Vector3(3, .008f, .5f), "White");
            Box("Щебёночная полоса", new Vector3(-1, -.12f, -17.9f), new Vector3(318, .05f, .9f), "Gravel");
            Box("Северный проезд", new Vector3(0, -.085f, 22), new Vector3(228, .12f, 7), "Asphalt", true);
            foreach (float z in new[] { 18.43f, 25.57f }) Box("Бортовой камень", new Vector3(0, -.02f, z), new Vector3(228, .25f, .15f), "Concrete");
            for (float x = -140; x <= 140; x += 28) LampMast(new Vector3(x, 0, -17), Vector3.back);
            for (float x = -99; x <= 99; x += 33) LampMast(new Vector3(x, 0, 17.2f), Vector3.forward);
            foreach (int s in new[] { -1, 1 })
            {
                LampMast(new Vector3(s * 150, 0, 16.5f), Vector3.back);
                for (int i = 0; i < 10; i++) LampMast(new Vector3(s * (185 + i * 42), 0, -16.6f), Vector3.back);
            }
            LampMast(new Vector3(125.5f, 0, -10.6f), Vector3.forward); // lights the crossing of track 1
        }

        // Tracks leave the hall over concrete aprons, pass the rail gates and continue on ballast
        // under the overpasses; sleepers are modelled only where they read from the territory.
        static void OuterTracks()
        {
            foreach (float s in new[] { 1f, -1f })
            {
                float hall = 114 * s, gate = s > 0 ? 158 : -160, end = 480 * s;
                float apron = Mathf.Abs(gate - hall), open = Mathf.Abs(end - gate), mid = (gate + end) / 2;
                Box("Щебёночное основание путей", new Vector3(mid, -.1f, 0), new Vector3(open, .09f, 21), "Gravel");
                foreach (float z in new[] { -7.5f, 0, 7.5f })
                {
                    Rail(apron, z, false, (gate + hall) / 2, false);
                    foreach (float side in new[] { -.76f, .76f })
                        Box("Подрельсовый брус", new Vector3((gate + hall) / 2, .05f, z + side), new Vector3(apron, .1f, .22f), "Concrete");
                    Rail(open, z, false, mid, false);
                    Box("Балластная призма", new Vector3(mid, -.03f, z), new Vector3(open, .16f, 3.6f), "Gravel");
                    for (float x = .5f; x < 120; x += .7f)
                        Box("Железобетонная шпала", new Vector3(gate + s * x, .07f, z), new Vector3(.24f, .15f, 2.65f), "Concrete");
                }
                Overpass(400 * s);
            }
        }

        static void Overpass(float x)
        {
            Box("Путепровод", new Vector3(x, 7.6f, 0), new Vector3(10, 1.1f, 250), "Concrete");
            Box("Покрытие путепровода", new Vector3(x, 8.17f, 0), new Vector3(9.4f, .05f, 250), "Asphalt");
            foreach (float side in new[] { -4.85f, 4.85f }) Box("Парапет путепровода", new Vector3(x + side, 8.6f, 0), new Vector3(.3f, .9f, 250), "Concrete");
            foreach (float z in new[] { -12f, 12, -45, 45, -80, 80, -115, 115 })
                Box("Опора путепровода", new Vector3(x, 3.5f, z), new Vector3(1.4f, 7, 4), "Concrete");
            foreach (int s in new[] { -1, 1 })
                Box("Насыпь подхода", new Vector3(x, 3.1f, s * 155), new Vector3(16, 3, 62), "Grass").transform.rotation = Quaternion.Euler(s * 7, 0, 0);
        }

        static void Perimeter()
        {
            const float east = 158, west = -160, south = -80, north = 110;
            Fence(new Vector3(west, 0, south), new Vector3(east, 0, south));
            Fence(new Vector3(west, 0, north), new Vector3(east, 0, north));
            float[] rail = { -11.2f, -3.75f, 3.75f, 11.2f };
            foreach (float x in new[] { east, west })
            {
                float s = Mathf.Sign(x);
                Fence(new Vector3(x, 0, south), new Vector3(x, 0, -30.3f));
                // Service drive: closed sliding gate just inside the fence line.
                GatePost(new Vector3(x, 0, -30.3f)); GatePost(new Vector3(x, 0, -17.7f));
                GateLeaf(new Vector3(x - s * .3f, 0, -30.1f), Vector3.forward, 12.2f);
                // Rail gates stand open outwards, along the tracks.
                foreach (float z in rail) GatePost(new Vector3(x, 0, z));
                for (int i = 0; i < 3; i++)
                {
                    float a = rail[i] + .2f, b = rail[i + 1] - .2f;
                    GateLeaf(new Vector3(x + s * .2f, 0, a), new Vector3(s, 0, 0), (b - a) / 2);
                    GateLeaf(new Vector3(x + s * .2f, 0, b), new Vector3(s, 0, 0), (b - a) / 2);
                }
                Fence(new Vector3(x, 0, 11.2f), new Vector3(x, 0, north));
            }
            Fence(new Vector3(west, 0, -17.7f), new Vector3(west, 0, -11.2f));
            // Staff wicket beside the checkpoint; its leaf stands open inwards.
            Fence(new Vector3(east, 0, -17.7f), new Vector3(east, 0, -14.2f));
            Fence(new Vector3(east, 0, -12.4f), new Vector3(east, 0, -11.2f));
            GateLeaf(new Vector3(east - .1f, 0, -12.47f), Vector3.left, 1.6f);
        }

        static void GatePost(Vector3 p) => Box("Столб ворот", p + Vector3.up * 1.3f, new Vector3(.3f, 2.6f, .3f), "Blue", true);
        static void GateLeaf(Vector3 hinge, Vector3 dir, float width)
        {
            Vector3 end = hinge + dir * width, up = Vector3.up;
            foreach (float h in new[] { .2f, 2.1f }) Beam("Рама створки", hinge + up * h, end + up * h, .05f, "Blue");
            foreach (var p in new[] { hinge, end }) Beam("Стойка створки", p + up * .2f, p + up * 2.1f, .05f, "Blue");
            Beam("Раскос створки", hinge + up * .2f, end + up * 2.1f, .035f, "Blue");
            for (float h = .5f; h < 2; h += .3f) Beam("Сетка створки", hinge + up * h, end + up * h, .012f, "Roof");
            Barrier("Коллизия створки", hinge + up * .2f, end + up * .2f, 1.9f);
        }
        // Invisible wall along a run: separate posts alone would let the player slip through.
        static void Barrier(string name, Vector3 a, Vector3 b, float height)
        {
            var c = new GameObject(name, typeof(BoxCollider)); c.transform.SetParent(root, false);
            c.transform.position = (a + b) / 2 + Vector3.up * height / 2; c.transform.rotation = Quaternion.LookRotation(b - a);
            c.GetComponent<BoxCollider>().size = new Vector3(.1f, height, (b - a).magnitude); c.isStatic = true;
        }

        static void LampMast(Vector3 p, Vector3 toward)
        {
            Column("Опора освещения", p + Vector3.down * .15f, 8.15f, .1f, "Steel", 12, true);
            Bevel("Фундамент опоры", p, new Vector3(.5f, .3f, .5f), "Concrete", .04f);
            Beam("Кронштейн светильника", p + Vector3.up * 7.8f, p + Vector3.up * 8.1f + toward * 1.6f, .08f, "Steel");
            Box("Корпус уличного светильника", p + Vector3.up * 8.05f + toward * 1.85f, new Vector3(.6f, .14f, .6f), "Roof");
            Box("Рассеиватель уличного светильника", p + Vector3.up * 7.97f + toward * 1.85f, new Vector3(.5f, .02f, .5f), "Lamp");
        }

        // Post-mounted plate; yaw follows Text(): 0 reads from +z, 90 from +x, 180 from -z.
        static void SignPost(Vector3 p, float yaw, string text, float width, string plate, Color? ink = null)
        {
            Column("Стойка знака", p + Vector3.down * .15f, 2.45f, .03f, "Steel", 10, true);
            Vector3 face = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            bool alongX = Mathf.Abs(face.z) > .5f;
            Box("Знак", p + Vector3.up * 1.95f + face * .05f, alongX ? new Vector3(width, .6f, .03f) : new Vector3(.03f, .6f, width), plate);
            Text(text, p + Vector3.up * 1.95f + face * .075f, .09f, yaw, ink);
        }

        // Wall cable tray inside the hall; cabinet risers from Cabinet() end in it.
        static void HallServices()
        {
            Box("Кабельный лоток", new Vector3(0, 5.9f, -13.05f), new Vector3(226, .06f, .4f), "Steel");
            foreach (float e in new[] { -.2f, .2f })
                Box("Борт кабельного лотка", new Vector3(0, 5.96f, -13.05f + e), new Vector3(226, .1f, .02f), "Steel");
            foreach (float e in new[] { -.08f, .07f })
                Box("Кабель в лотке", new Vector3(0, 5.955f, -13.05f + e), new Vector3(226, .05f, .07f), "Dark");
            for (float x = -108; x <= 108; x += 6)
                Box("Кронштейн лотка", new Vector3(x, 5.83f, -13.15f), new Vector3(.05f, .14f, .25f), "Steel");
        }

        static void ServiceYards()
        {
            // Transformer substation feeding the hall over a cable trestle across the drive.
            var k = new Vector3(-62, 0, -41);
            Box("Трансформаторная подстанция", k + new Vector3(0, 1.45f, 0), new Vector3(8, 3.2f, 4.2f), "White", true);
            Box("Цоколь подстанции", k + new Vector3(0, .1f, 0), new Vector3(8.12f, .5f, 4.32f), "Concrete");
            Box("Кровля подстанции", k + new Vector3(0, 3.15f, 0), new Vector3(8.5f, .2f, 4.7f), "Roof");
            foreach (float x in new[] { -2.5f, 0, 2.5f })
            {
                Box("Дверь камеры трансформатора", k + new Vector3(x, 1.2f, 2.13f), new Vector3(1.5f, 2.2f, .05f), "Blue");
                Box("Решётка вентиляции", k + new Vector3(x, 2.05f, 2.165f), new Vector3(1.1f, .3f, .02f), "Dark");
                Box("Знак опасности", k + new Vector3(x, 1.5f, 2.17f), new Vector3(.3f, .3f, .015f), "Yellow");
            }
            Fence(k + new Vector3(-5, 0, -3), k + new Vector3(5, 0, -3));
            foreach (int s in new[] { -1, 1 })
            {
                Fence(k + new Vector3(s * 5, 0, -3), k + new Vector3(s * 5, 0, 3.2f));
                Fence(k + new Vector3(s * 5, 0, 3.2f), k + new Vector3(s * 1.2f, 0, 3.2f));
            }
            GateLeaf(k + new Vector3(-1.2f, 0, 3.2f), Vector3.right, 2.4f);
            SignPost(k + new Vector3(2.2f, 0, 3.5f), 0, "ОПАСНО\nнапряжение 10 кВ", 1.2f, "Yellow", new Color(.05f, .05f, .05f));
            foreach (float z in new[] { -36f, -31.2f, -16.8f })
            {
                Tube("Стойка кабельной эстакады", new Vector3(-62, -.15f, z), new Vector3(-62, 6.1f, z), .22f, "Steel", true);
                Box("Траверса эстакады", new Vector3(-62, 6.1f, z), new Vector3(.9f, .1f, .1f), "Steel");
            }
            Box("Кабельный лоток эстакады", new Vector3(-62, 6.2f, -25.4f), new Vector3(.6f, .1f, 22.9f), "Steel");
            foreach (float e in new[] { -.3f, .3f }) Box("Борт лотка эстакады", new Vector3(-62 + e, 6.26f, -25.4f), new Vector3(.03f, .12f, 22.9f), "Steel");
            foreach (float e in new[] { -.15f, .12f }) Beam("Силовой кабель", new Vector3(-62 + e, 6.28f, -36.8f), new Vector3(-62 + e, 6.28f, -14), .06f, "Dark");
            Beam("Кабельный спуск", new Vector3(-62, 6.2f, -36.8f), new Vector3(-62, 3.25f, -39.3f), .12f, "Dark");
            Box("Кабельный ввод", new Vector3(-62, 6.2f, -13.9f), new Vector3(.9f, .5f, .12f), "Concrete");

            // Staff parking inside the fence, opening onto the drive.
            Box("Стоянка персонала", new Vector3(60, -.09f, -34.05f), new Vector3(90, .11f, 9.1f), "Asphalt", true);
            Box("Бортовой камень", new Vector3(60, -.02f, -38.67f), new Vector3(90.3f, .25f, .15f), "Concrete");
            foreach (float x in new[] { 14.93f, 105.07f }) Box("Бортовой камень", new Vector3(x, -.02f, -34.05f), new Vector3(.15f, .25f, 9.1f), "Concrete");
            for (float x = 16.5f; x < 104; x += 2.7f) Box("Разметка стоянки", new Vector3(x, -.03f, -35.6f), new Vector3(.1f, .012f, 5), "White");
            string[] paint = { "Blue", "White", "Red", "Dark", "Concrete", "White" };
            int[] slots = { 3, 4, 9, 14, 20, 27 };
            for (int i = 0; i < slots.Length; i++) ParkedCar(new Vector3(16.5f + slots[i] * 2.7f + 1.35f, -.035f, -35.2f), paint[i]);

            // Workshop apron, containers, spare-part racks and a short wheelset storage track.
            Box("Площадка мастерской", new Vector3(65, -.09f, -43.05f), new Vector3(70, .11f, 8.9f), "Concrete", true);
            Box("Площадка контейнеров", new Vector3(23.2f, -.09f, -52), new Vector3(10, .1f, 8), "Concrete", true);
            string[] boxes = { "Blue", "Red", "ServiceGreen" };
            for (int i = 0; i < 3; i++) Container(new Vector3(20.5f + i * 2.7f, -.04f, -52), boxes[i]);
            Box("Площадка хранения", new Vector3(106, -.09f, -55), new Vector3(14, .1f, 16), "Concrete", true);
            for (int i = 0; i < 3; i++) Rack(new Vector3(109.5f, -.04f, -60.65f + i * 2.7f), i);
            foreach (float s in new[] { -.76f, .76f }) Box("Рельс накопителя", new Vector3(103 + s, .05f, -55), new Vector3(.07f, .14f, 12), "Steel");
            for (float z = -60.7f; z < -49; z += .7f) Box("Шпала накопителя", new Vector3(103, -.01f, z), new Vector3(2.4f, .1f, .24f), "Concrete");
            foreach (float z in new[] { -59.5f, -57, -54.5f, -52 }) WheelSet(new Vector3(103, .12f, z));
        }

        static void ParkedCar(Vector3 p, string paint)
        {
            Bevel("Кузов автомобиля", p + new Vector3(0, .6f, 0), new Vector3(1.78f, .7f, 4.4f), paint, .16f, true);
            Bevel("Остекление автомобиля", p + new Vector3(0, 1.15f, -.2f), new Vector3(1.58f, .7f, 2.3f), "Dark", .2f);
            Bevel("Крыша автомобиля", p + new Vector3(0, 1.5f, -.2f), new Vector3(1.36f, .08f, 1.88f), paint, .035f);
            foreach (float x in new[] { -.8f, .8f })
                Box("Средняя стойка кузова", p + new Vector3(x, 1.18f, -.3f), new Vector3(.055f, .52f, .12f), paint);
            foreach (float x in new[] { -.59f, .59f })
            {
                Bevel("Фара автомобиля", p + new Vector3(x, .7f, 2.19f), new Vector3(.38f, .16f, .05f), "White", .025f);
                Bevel("Задний фонарь", p + new Vector3(x, .7f, -2.19f), new Vector3(.34f, .18f, .05f), "Red", .025f);
            }
            foreach (float x in new[] { -.8f, .8f })
                foreach (float z in new[] { -1.35f, 1.4f })
                    Tube("Колесо автомобиля", p + new Vector3(x - .1f, .32f, z), p + new Vector3(x + .1f, .32f, z), .64f, "Dark");
        }
        static void Container(Vector3 p, string paint)
        {
            Box("Контейнер", p + new Vector3(0, 1.3f, 0), new Vector3(2.44f, 2.59f, 6.06f), paint, true);
            for (float z = -2.7f; z <= 2.71f; z += .6f)
                foreach (float s in new[] { -1.23f, 1.23f })
                    Box("Гофр контейнера", p + new Vector3(s, 1.3f, z), new Vector3(.04f, 2.4f, .12f), paint);
            foreach (float x in new[] { -.8f, -.35f, .35f, .8f })
                Tube("Запорная штанга", p + new Vector3(x, .2f, -3.06f), p + new Vector3(x, 2.4f, -3.06f), .04f, "Steel");
        }
        static void Rack(Vector3 p, int bay)
        {
            foreach (float x in new[] { -.55f, .55f })
            {
                foreach (float z in new[] { -1.35f, 1.35f })
                    Box("Стойка стеллажа", p + new Vector3(x, 1.5f, z), new Vector3(.08f, 3, .08f), "Blue");
                foreach (float y in new[] { 1f, 2, 2.9f })
                    Box("Балка стеллажа", p + new Vector3(x, y, 0), new Vector3(.05f, .1f, 2.7f), "Yellow");
            }
            foreach (float y in new[] { 1.06f, 2.06f })
                Box("Настил стеллажа", p + new Vector3(0, y, 0), new Vector3(1.1f, .03f, 2.7f), "Steel", y < 2);
            for (int i = 0; i < 2; i++)
                if ((bay + i) % 3 != 2) Box("Ящик с запчастями", p + new Vector3(0, i == 0 ? 1.38f : 2.38f, (i == 0 ? -.6f : .55f)), new Vector3(.8f, .6f, .9f), "Concrete");
        }
        static void WheelSet(Vector3 p)
        {
            Tube("Ось колёсной пары", p + new Vector3(-1, .475f, 0), p + new Vector3(1, .475f, 0), .18f, "Steel");
            foreach (float s in new[] { -1f, 1 })
                Tube("Колесо колёсной пары", p + new Vector3(s * .7f, .475f, 0), p + new Vector3(s * .85f, .475f, 0), .95f, "Dark", true);
        }

        // Mid-plane neighbours, tree belts and far masses so that no view from the route ends at
        // the ground edge. Everything outside the fence casts no shadows and has no collision.
        static void Backdrop()
        {
            Building("Склад соседнего предприятия", 225, -62, 70, 11, 26, "Plaster");
            Building("Административный корпус", 212, 48, 36, 15, 15, "Brick");
            Building("Цех соседнего предприятия", 285, 100, 90, 18, 34, "White");
            Building("Котельная", 300, -150, 34, 13, 22, "Brick");
            Building("Гаражный корпус", -228, -58, 64, 7, 22, "White");
            Building("Бытовой корпус", -214, 52, 34, 12, 14, "Plaster");
            Building("Корпус завода", -300, 115, 90, 17, 32, "Brick");
            Building("Жилой дом", -110, 200, 64, 27, 14, "Plaster");
            Building("Жилой дом", 5, 215, 80, 24, 14, "White");
            Building("Жилой дом", 130, 195, 56, 30, 15, "Plaster");
            Building("Склад", -90, -150, 90, 12, 30, "White");
            Building("Офисное здание", 55, -160, 48, 21, 18, "Brick");
            Building("Складской корпус", 170, -145, 60, 10, 24, "Plaster");
            float[,] masses =
            {
                { 500, -120, 60, 40, 22 }, { 520, 90, 80, 30, 30 }, { 470, 200, 50, 50, 18 }, { 540, 0, 40, 60, 16 },
                { -520, -100, 70, 40, 24 }, { -540, 0, 40, 60, 18 }, { -500, 120, 60, 40, 28 }, { -470, 230, 50, 40, 20 },
                { -300, 330, 90, 30, 35 }, { -150, 360, 70, 30, 45 }, { 40, 340, 100, 30, 30 }, { 220, 350, 80, 30, 38 },
                { 380, 300, 60, 40, 26 }, { -330, -320, 80, 30, 26 }, { -160, -340, 90, 30, 34 }, { 30, -330, 70, 30, 22 },
                { 200, -350, 80, 30, 40 }, { 380, -290, 60, 40, 24 },
            };
            string[] walls = { "Concrete", "Plaster", "White", "Brick" };
            for (int i = 0; i < masses.GetLength(0); i++)
            {
                var p = new Vector3(masses[i, 0], masses[i, 4] / 2, masses[i, 1]);
                var size = new Vector3(masses[i, 2], masses[i, 4], masses[i, 3]);
                Unshadowed(Box("Дальний корпус", p, size, walls[i % 4]));
                Unshadowed(Box("Кровля дальнего корпуса", p + Vector3.up * size.y / 2, new Vector3(size.x + .6f, .6f, size.z + .6f), "Roof"));
            }
            foreach (var (p, d, h) in new[] { (new Vector3(330, 0, -150), 4f, 62f), (new Vector3(-360, 0, 190), 3f, 48f) })
            {
                Unshadowed(Tube("Дымовая труба", p, p + Vector3.up * h, d, "White"));
                foreach (float y in new[] { h - 16, h - 6 })
                    Unshadowed(Tube("Маркировка трубы", p + Vector3.up * y, p + Vector3.up * (y + 4), d + .15f, "Red"));
            }
            var rng = new System.Random(1703);
            TreeBelt(rng, 164, 180, -125, 150, 7);
            TreeBelt(rng, -190, -166, -125, 150, 7);
            TreeBelt(rng, -200, 200, 116, 150, 8);
            TreeBelt(rng, -200, 200, -122, -86, 8);
            foreach (int s in new[] { -1, 1 })
                for (float x = 170; x < 470; x += 9 + (float)rng.NextDouble() * 5)
                    YardTree(new Vector3(s * x, -.14f, -32.6f - (float)rng.NextDouble()), 7 + (float)rng.NextDouble() * 5, rng, false);
            // Inside the fence a few trees with shadows: the yard between office and store, the works front.
            for (float x = -74; x < -46; x += 7 + (float)rng.NextDouble() * 3)
                YardTree(new Vector3(x, -.14f, 56 + (float)rng.NextDouble() * 8), 6 + (float)rng.NextDouble() * 3, rng, true);
            for (float x = 60; x < 126; x += 12 + (float)rng.NextDouble() * 5)
                YardTree(new Vector3(x, -.14f, 38), 7 + (float)rng.NextDouble() * 3, rng, true);
        }
        static GameObject Unshadowed(GameObject go) { go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; return go; }

        // Jittered grid with gaps: rows never line up and track/drive corridors stay open.
        static void TreeBelt(System.Random rng, float x0, float x1, float z0, float z1, float step)
        {
            for (float x = x0; x < x1; x += step)
                for (float z = z0; z < z1; z += step)
                {
                    if (rng.NextDouble() < .3) continue;
                    var p = new Vector3(x + (float)rng.NextDouble() * step * .8f, -.14f, z + (float)rng.NextDouble() * step * .8f);
                    if (Mathf.Abs(p.x) > 158 && p.z > -35 && p.z < 14) continue;
                    YardTree(p, 6 + (float)rng.NextDouble() * 8, rng, false);
                }
        }
        static void YardTree(Vector3 p, float height, System.Random rng, bool shadows)
        {
            height = Mathf.Round(height * 2) / 2; // quantised so trunks share a few box meshes
            var trunk = Box("Ствол дерева", p + Vector3.up * height * .2f, new Vector3(.28f, height * .4f, .28f), "Bark");
            if (!shadows) Unshadowed(trunk);
            int crowns = 2 + rng.Next(2);
            for (int i = 0; i < crowns; i++)
            {
                float r = height * (.28f + (float)rng.NextDouble() * .12f);
                var go = new GameObject("Крона", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false);
                go.transform.localPosition = p + new Vector3(((float)rng.NextDouble() - .5f) * r, height * (.55f + i * .14f), ((float)rng.NextDouble() - .5f) * r);
                go.transform.localRotation = Quaternion.Euler(0, rng.Next(360), 0);
                go.transform.localScale = new Vector3(r * 2, r * 1.7f, r * 2);
                go.GetComponent<MeshFilter>().sharedMesh = CrownMesh();
                go.GetComponent<MeshRenderer>().sharedMaterial = mats[rng.Next(2) == 0 ? "Leaves" : "LeavesLight"];
                if (!shadows) Unshadowed(go);
                go.isStatic = true;
            }
        }
        // Low-poly irregular crown (108 triangles) shared by all territory trees.
        static Mesh CrownMesh()
        {
            if (crownMesh != null) return crownMesh;
            const int lon = 9, lat = 6;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= lat; i++)
                for (int j = 0; j <= lon; j++)
                {
                    float a = Mathf.PI * i / lat, b = 2 * Mathf.PI * j / lon;
                    float r = i == 0 || i == lat ? .45f : .5f * (.8f + .35f * Mathf.PerlinNoise(i * .9f + 3, j % lon * .9f));
                    v.Add(new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b)) * r);
                }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int p = i * (lon + 1) + j, q = p + lon + 1;
                    t.AddRange(new[] { p, p + 1, q, p + 1, q + 1, q });
                }
            var mesh = new Mesh { name = "Tree crown" }; mesh.SetVertices(v); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            Save(mesh, Art + "/TreeCrown.asset");
            return crownMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/TreeCrown.asset");
        }

        // One side of a building. alongX faces sit at z ± half, the others at x ± half.
        static void Facade(float x, float z, bool alongX, int side, float span, float half, float height, int doors,
            System.Random rng, bool far, string trim)
        {
            Vector3 P(float a, float y, float o) => alongX ? new Vector3(x + a, y, z + side * (half + o)) : new Vector3(x + side * (half + o), y, z + a);
            Vector3 S(float a, float h, float d) => alongX ? new Vector3(a, h, d) : new Vector3(d, h, a);
            float pitch = 2.8f + (float)rng.NextDouble() * .8f, w = new[] { 1.1f, 1.3f, 1.5f }[rng.Next(3)], h = new[] { 1.5f, 1.7f, 1.9f }[rng.Next(3)];
            bool mullion = rng.Next(2) == 0; string pane = rng.Next(2) == 0 ? "Roof" : "Dark";
            int bay = 4 + rng.Next(3), count = Mathf.FloorToInt((span - 3.2f) / pitch) + 1;
            if (span < 4) return;
            float start = -(count - 1) * pitch / 2;
            var doorSlots = new HashSet<int>();
            for (int d = 0; d < doors; d++)
                doorSlots.Add(Mathf.Clamp(Mathf.RoundToInt((count - 1) * (d + 1f) / (doors + 1)) + rng.Next(3) - 1, 0, count - 1));
            Vector3 outward = alongX ? new Vector3(0, 0, side) : new Vector3(side, 0, 0);
            if (alongX) foreach (int s in new[] { -1, 1 }) Downpipe(P(s * (span / 2 - .55f), height - .45f, .18f), outward, -.13f);
            for (int i = 0; i < count; i++)
            {
                float a = start + i * pitch;
                if (i % bay == bay - 1 && i < count - 1)
                {
                    Box("Пилястра", P(a + pitch / 2, height / 2, .1f), S(.5f, height - .6f, .2f), trim);
                    if (alongX && i / bay % 2 == 1) Downpipe(P(a + pitch / 2 + .45f, height - .45f, .18f), outward, -.13f);
                }
                for (float y = 1.9f; y + h / 2 < height - .8f; y += 3)
                {
                    if (y < 3 && doorSlots.Contains(i)) { Entrance(P, S, a, far, trim); continue; }
                    if (rng.NextDouble() < .06) continue; // blind bay: stair or service room
                    Box("Остекление окна", P(a, y, .015f), S(w, h, .03f), pane);
                    Box("Отлив окна", P(a, y - h / 2 - .03f, .13f), S(w + .3f, .06f, .26f), trim);
                    if (far) continue;
                    // Reveals protrude around a flush pane, so the opening reads with depth.
                    foreach (int s in new[] { -1, 1 }) Box("Откос окна", P(a + s * (w / 2 + .06f), y, .08f), S(.12f, h + .12f, .16f), trim);
                    Box("Перемычка окна", P(a, y + h / 2 + .06f, .08f), S(w + .24f, .12f, .16f), trim);
                    Box("Импост окна", P(a, mullion ? y : y + h * .22f, .05f), mullion ? S(.05f, h, .04f) : S(w, .05f, .04f), trim);
                }
            }
        }
        static void Entrance(Func<float, float, float, Vector3> P, Func<float, float, float, Vector3> S, float a, bool far, string trim)
        {
            Box("Входная дверь", P(a, 1.6f, .13f), S(1.4f, 2.3f, .06f), "Dark");
            Box("Крыльцо", P(a, .15f, .75f), S(2.6f, .6f, 1.5f), "Concrete", true);
            Box("Ступень крыльца", P(a, 0, 1.72f), S(2.6f, .3f, .45f), "Concrete", true);
            Box("Козырёк входа", P(a, 3.05f, .85f), S(2.4f, .14f, 1.7f), "Roof");
            if (far) return;
            foreach (int s in new[] { -1, 1 }) Box("Откос двери", P(a + s * .76f, 1.65f, .12f), S(.12f, 2.4f, .24f), trim);
            Box("Перемычка двери", P(a, 2.83f, .12f), S(1.64f, .12f, .24f), trim);
            Box("Светильник входа", P(a, 2.92f, .3f), S(.35f, .08f, .15f), "Lamp");
        }
        // Downpipe from a roof funnel to an elbow and a concrete receiver on the ground.
        static void Downpipe(Vector3 top, Vector3 outward, float ground)
        {
            var foot = new Vector3(top.x, ground + .35f, top.z);
            Box("Воронка водостока", top + Vector3.down * .15f, new Vector3(.3f, .3f, .3f), "Steel");
            Tube("Водосточная труба", top, foot, .14f, "Steel");
            Beam("Отвод водостока", foot, foot + Vector3.down * .25f + outward * .3f, .14f, "Steel");
            Box("Водоприёмный лоток", new Vector3(foot.x, ground + .02f, foot.z) + outward * .75f,
                Mathf.Abs(outward.x) > .5f ? new Vector3(.9f, .08f, .4f) : new Vector3(.4f, .08f, .9f), "Concrete");
        }
    }
}
