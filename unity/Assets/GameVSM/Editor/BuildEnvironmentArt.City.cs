using System.Collections.Generic;
using UnityEngine;

namespace GameVSM.Editor
{
    // District character reconstructed from the saved location references; not cadastral geometry.
    // All of this is batched without shadows by Station(), outside the walking/NPC routes.
    public static partial class BuildEnvironmentArt
    {
        static void StationCityStreets(bool moscow)
        {
            // A service street separates rail buildings from the urban block. It is scenery,
            // not an invented pedestrian exit through the operational track throat.
            foreach (float z in new[] { -60f, 57f })
            {
                Box("Улица за железнодорожной территорией", new Vector3(60, -.08f, z), new Vector3(450, .1f, 9), "Asphalt");
                foreach (float side in new[] { -1f, 1f })
                {
                    Box("Тротуар городской улицы", new Vector3(60, .04f, z + side * 6), new Vector3(450, .22f, 3), "Tiles");
                    Box("Бортовой камень улицы", new Vector3(60, .12f, z + side * 4.6f), new Vector3(450, .28f, .2f), "Concrete");
                }
                for (float x = -152; x <= 272; x += 16)
                    Box("Прерывистая разметка улицы", new Vector3(x, -.02f, z), new Vector3(6, .012f, .12f), "White");
                for (float x = -148; x <= 268; x += 48)
                {
                    Column("Фонарь городской улицы", new Vector3(x, .15f, z + 5), 7, .08f, "Roof", 8);
                    Beam("Консоль фонаря", new Vector3(x, 7.15f, z + 5), new Vector3(x, 7.45f, z + 2.6f), .09f, "Roof");
                    Box("Светильник городской улицы", new Vector3(x, 7.43f, z + 2.6f), new Vector3(.45f, .12f, .85f), "Steel");
                }
            }
            // Low masonry boundary gives the broad station ground a visible end. No high-poly trees.
            foreach (float z in new[] { -32f, 46f })
            {
                Box("Основание ограды территории", new Vector3(63, .4f, z), new Vector3(446, .8f, .42f), moscow ? "Concrete" : "Brick");
                foreach (float y in new[] { .95f, 1.85f })
                    Box("Ригель ограды территории", new Vector3(63, y, z), new Vector3(446, .06f, .06f), "Roof");
                for (float x = -160; x <= 286; x += 4)
                    Box("Стойка ограды территории", new Vector3(x, 1.05f, z), new Vector3(.09f, 2.1f, .09f), "Roof");
            }
        }

        // Visible city block: plinth, recessed windows on the track side, cornice and roof.
        // Masses stand for the district's character, not cadastral footprints.
        static void CityBlock(string name, float x, float z, float length, float height, float depth, string wall, bool pitched, bool near = false)
        {
            float face = z < 0 ? 1 : -1, front = z + face * depth / 2;
            if (near) Bevel(name, new Vector3(x, height / 2, z), new Vector3(length, height, depth), wall, .12f);
            else Box(name, new Vector3(x, height / 2, z), new Vector3(length, height, depth), wall);
            Box("Цоколь", new Vector3(x, .6f, z), new Vector3(length + .3f, 1.2f, depth + .3f), "Concrete");
            Box("Карниз", new Vector3(x, height - .25f, z), new Vector3(length + .6f, .45f, depth + .6f), "White");
            foreach (float end in new[] { -1f, 1f })
                Box("Угловая лопатка", new Vector3(x + end * (length / 2 - .2f), height / 2, front + face * .06f), new Vector3(.6f, height - .6f, .14f), "White");
            if (pitched)
            {
                float rise = depth * .277f;
                foreach (float side in new[] { -1f, 1f })
                    Box("Скат кровли", new Vector3(x, height + rise / 2, z + side * depth / 4), new Vector3(length + .4f, .22f, depth * .58f), "Roof")
                        .transform.rotation = Quaternion.Euler(side * 29, 0, 0);
                foreach (float end in new[] { -1f, 1f })
                    CityGable(new Vector3(x + end * length / 2, height + rise / 2, z), rise, depth, wall);
                for (float a = -length / 2 + 5; a < length / 2 - 3; a += 9)
                    Box("Дымовая труба", new Vector3(x + a, height + rise + .3f, z - face * 1.5f), new Vector3(.8f, 1.8f, .8f), "Brick");
            }
            else
            {
                Box("Парапет", new Vector3(x, height + .45f, front - face * .15f), new Vector3(length, .9f, .3f), wall);
                Box("Кровельная установка", new Vector3(x - length / 5, height + 1.1f, z), new Vector3(Mathf.Min(8, length / 4), 2.2f, depth / 3), "Steel");
            }
            for (float y = 3.4f; y < height - 1.6f; y += 3.3f)
            {
                // End facades read from the platform looking along the tracks. The old bare
                // box ends betrayed the facade-only construction at every gap in the block.
                foreach (float end in new[] { -1f, 1f })
                    for (float dz = -depth / 2 + 3; dz < depth / 2 - 1; dz += 3.6f)
                    {
                        float fx = x + end * (length / 2 + .035f);
                        Box("Окно торца квартала", new Vector3(fx, y, z + dz), new Vector3(.045f, 1.7f, 1.3f), "Roof");
                        if (near) Box("Отлив торца квартала", new Vector3(fx + end * .06f, y - .9f, z + dz), new Vector3(.18f, .09f, 1.5f), "White");
                    }
                if (!near)
                {
                    Box("Ряд окон", new Vector3(x, y, front + face * .03f), new Vector3(length - 1.6f, 1.75f, .05f), "Roof");
                    for (float a = -length / 2 + 2.3f; a < length / 2 - 1.5f; a += 3)
                        Box("Простенок", new Vector3(x + a, y, front + face * .07f), new Vector3(1.1f, 1.8f, .08f), wall);
                    continue;
                }
                for (float a = -length / 2 + 2; a < length / 2 - 1; a += 3)
                {
                    Vector3 w = new(x + a, y, front);
                    Box("Окно в глубине ниши", w + new Vector3(0, 0, face * .03f), new Vector3(1.2f, 1.7f, .04f), "Roof");
                    foreach (float side in new[] { -1f, 1f })
                        Box("Откос окна", w + new Vector3(side * .67f, 0, face * .11f), new Vector3(.14f, 1.86f, .22f), "White");
                    Box("Перемычка", w + new Vector3(0, .93f, face * .11f), new Vector3(1.48f, .16f, .22f), "White");
                    Box("Отлив", w + new Vector3(0, -.9f, face * .15f), new Vector3(1.6f, .07f, .3f), "White");
                }
            }
            if (near)
            {
                float doorX = x - length * .27f;
                Bevel("Портал служебного корпуса", new Vector3(doorX, 1.5f, front + face * .1f), new Vector3(1.9f, 3, .3f), "Concrete", .04f);
                Box("Дверь служебного корпуса", new Vector3(doorX, 1.4f, front + face * .26f), new Vector3(1.4f, 2.6f, .05f), "Roof");
                Bevel("Входной козырёк корпуса", new Vector3(doorX, 3.15f, front + face * .75f), new Vector3(2.5f, .15f, 1.6f), "Steel", .025f);
                Box("Отмостка служебного корпуса", new Vector3(x, .05f, front + face), new Vector3(length + 2, .1f, 2), "Concrete");
                for (float dx = -length / 2 + 1; dx < length / 2; dx += 18)
                    Column("Водосток служебного корпуса", new Vector3(x + dx, .15f, front + face * .22f), height - .5f, .065f, "Steel", 8);
            }
        }

        static void CityGable(Vector3 centre, float rise, float depth, string wall)
        {
            float y = rise / 2, z = depth / 2;
            var a = new Vector3(-.16f, -y, -z); var b = new Vector3(-.16f, -y, z); var c = new Vector3(-.16f, y, 0);
            var d = new Vector3(.16f, -y, -z); var e = new Vector3(.16f, -y, z); var f = new Vector3(.16f, y, 0);
            var mesh = Polyhedron(new List<Vector3[]> { new[] { a, b, c }, new[] { d, e, f },
                new[] { a, b, e, d }, new[] { a, c, f, d }, new[] { b, c, f, e } }, "Фронтон городской кровли");
            // The shadowless city batch persists this low-poly mesh; no separate asset per gable.
            Place("Фронтон городской кровли", centre, mesh, wall, null);
        }
        static void PetersburgCity()
        {
            // Low, even eaves with pitched roofs, firewalls and chimneys; brick service volumes by the tracks.
            CityBlock("Кирпичное служебное здание", 22, -43, 97, 9, 17, "Brick", false, true);
            CityBlock("Служебный корпус у горловины", 150, -42, 46, 7, 12, "Brick", true, true);
            string[] walls = { "Plaster", "Brick", "Plaster", "White" };
            for (int i = 0; i < 7; i++)
            {
                float length = 38 + i % 3 * 8;
                CityBlock("Доходный дом", -130 + i * 56, -78 - i % 2 * 4, length, 19 + i % 3 * 1.5f, 18, walls[i % 4], true);
                CityBlock("Доходный дом", -110 + i * 54, 74 + i % 2 * 5, length + 6, 20 + i % 2 * 2, 20, walls[(i + 1) % 4], true);
            }
            for (int i = 0; i < 6; i++)
                CityBlock("Квартал второго плана", -150 + i * 80, 150 + i % 2 * 20, 64, 22 + i % 3 * 2, 30, walls[i % 4], true);
            // Generic domed silhouette on the skyline; not a specific building.
            // The existing drum begins at y=18; continue its footprint to the ground
            // without changing the visible roofline or claiming a particular landmark.
            Column("Основание купольного силуэта", new Vector3(40, -.25f, 260), 18.25f, 8, "Plaster");
            var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder); drum.name = "Барабан купола (силуэт)";
            drum.transform.SetParent(root, false); drum.transform.position = new Vector3(40, 30, 260); drum.transform.localScale = new Vector3(16, 12, 16);
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere); dome.name = "Купол (силуэт)";
            dome.transform.SetParent(root, false); dome.transform.position = new Vector3(40, 42, 260); dome.transform.localScale = new Vector3(17, 18, 17);
            foreach (var go in new[] { drum, dome })
            {
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.isStatic = true;
                go.GetComponent<Renderer>().sharedMaterial = mats[go == dome ? "Roof" : "Plaster"];
            }
        }
        static void MoscowCity()
        {
            // Mixed heights: offices with curtain walls, slab housing and a tiered high-rise silhouette.
            CityBlock("Служебный корпус вокзала", 10, -44, 90, 13, 18, "Plaster", false, true);
            for (int i = 0; i < 5; i++)
            {
                float x = -120 + i * 70, h = 28 + i % 3 * 9;
                Box("Офисный корпус", new Vector3(x, h / 2, -86), new Vector3(46, h, 24), "Dark");
                Box("Витражный фасад", new Vector3(x, h / 2, -73.9f), new Vector3(45, h - 2, .08f), "Glass");
                for (float y = 3.5f; y < h; y += 3.5f)
                    Box("Междуэтажный пояс", new Vector3(x, y, -73.8f), new Vector3(46, .3f, .12f), "Steel");
                for (float dx = -21; dx <= 21; dx += 7)
                    Box("Вертикальная стойка офисного фасада", new Vector3(x + dx, h / 2, -73.7f), new Vector3(.25f, h, .2f), "Steel");
                foreach (float end in new[] { -1f, 1f })
                    for (float y = 3.5f; y < h - 1; y += 3.5f)
                        Box("Окна торца офиса", new Vector3(x + end * 23.04f, y, -86), new Vector3(.06f, 1.8f, 20), "Steel");
                Box("Кровельное оборудование", new Vector3(x + 8, h + 1.2f, -86), new Vector3(10, 2.4f, 8), "Steel");
            }
            for (int i = 0; i < 6; i++)
                CityBlock("Жилой дом", -130 + i * 60, 80 + i % 2 * 8, 50, 27 + i % 2 * 6, 14, i % 2 == 0 ? "Plaster" : "White", false);
            CityBlock("Служебный корпус второго плана", 200, 115, 80, 10, 24, "Brick", true);
            // Tiered high-rise with a spire south-west of the tracks: a silhouette, not a model of a building.
            float tx = -70, tz = -175, y0 = 0;
            foreach (var (w, h) in new[] { (46f, 40f), (30f, 26f), (18f, 20f), (10f, 12f) })
            {
                Box("Ярус высотного здания", new Vector3(tx, y0 + h / 2, tz), new Vector3(w, h, w * .8f), "Plaster");
                Box("Карниз яруса", new Vector3(tx, y0 + h - .5f, tz), new Vector3(w + 1, 1, w * .8f + 1), "White");
                y0 += h;
            }
            Tube("Шпиль", new Vector3(tx, y0, tz), new Vector3(tx, y0 + 26, tz), 1.2f, "Yellow");
        }
    }
}
