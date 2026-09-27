using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameVSM.Editor
{
    // Station compositions: Moskovsky in St Petersburg (boarding) and Leningradsky in Moscow
    // (arrival). Reconstruction from photographs: heights, spans and column pitch are
    // estimates, not survey dimensions. Track, platform, car and exit texts come from TripInfo.
    public static partial class BuildEnvironmentArt
    {
        const float Deck = 1.28f, FacadeX = -142.5f;
        static readonly float[] PlatformAxes = { -18, -6, 6, 18, 30 };
        static readonly float[] Crowd = { 12, 22, 30, 66, 80, 88, 100, 92 };

        static void Station(bool moscow)
        {
            var stop = TripInfo.At(moscow ? "Leningradsky" : "Moskovsky");
            Box("Городская территория", new Vector3(0, -.25f, 0), new Vector3(900, .25f, 600), "Asphalt", true);
            for (int line = -2; line <= 3; line++) Rail(560, line * 12, true);
            foreach (float z in PlatformAxes) Platform(z, moscow);
            Box("Распределительная площадка", new Vector3(-135, .64f, 6), new Vector3(22, 1.28f, 72), "Tiles", true);
            WalkableFloor("Распределительная площадка и выход", -142, -123.6f, -29.6f, 41.6f, Deck);
            WalkableFloor("Порог вагона № 3", TripInfo.CarriageDoor.x - .8f, TripInfo.CarriageDoor.x + .8f, -2.65f, -1.2f, Deck);
            ContactNetwork();
            if (moscow) { foreach (float z in PlatformAxes) MoscowCanopy(z); MoscowConcourse(); MoscowTerminal(stop); }
            else { foreach (float z in PlatformAxes) PetersburgCanopy(z); PetersburgTerminal(stop); }
            StationDetails(moscow);
            Navigation(stop, moscow);
            Shadowless(stop.Location + "Detail", () => PlatformDetail(moscow));
            Shadowless(stop.Location + "City", () => { StationCityStreets(moscow); if (moscow) MoscowCity(); else PetersburgCity(); });
            // Background crowd stays outside x 32..64: queue at car 3 (49..58, z -3.45), alighting
            // stream (34..43, z -5) and the Life lanes; walkers (i % 3 == 0) have 11 m of free lane.
            for (int i = 0; i < 8; i++)
                Person(new Vector3(Crowd[i], 1.3f, -4.1f - (i % 3) * .75f), i % 2 == 0 ? 90 : -90, i % 3 == 0,
                    i == 2 ? "PassengerWithBag" : i % 2 == 0 ? "Passenger" : "PassengerWoman");
            Lights(false);
            Reflection(new Vector3(48, 3.5f, -5), new Vector3(50, 12, 22));
        }

        // Small parts and distant masses get their own batches without shadow casting. They are
        // left non-static (as route sections are) so the site-wide combine keeps that setting.
        static void Shadowless(string id, Action build)
        {
            var outer = root; root = new GameObject(id).transform; root.SetParent(outer, false);
            try
            {
                build();
                // Assign before batching so distant opaque pieces use the larger shadowless
                // cells. Assigning only after CombineModules left hundreds of small batches.
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                CombineModules(id);
            }
            finally
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
                root = outer;
            }
        }
        static void Blocker(string name, Vector3 p, Vector3 size)
        {
            var go = new GameObject(name, typeof(BoxCollider)); go.transform.SetParent(root, false);
            go.transform.localPosition = p; go.GetComponent<BoxCollider>().size = size; go.isStatic = true;
        }

        static void Platform(float z, bool moscow)
        {
            // Feet stay behind the edge; the shared concourse connects all five platforms.
            WalkableFloor("Перрон", -126, 125.1f, z - 3.65f, z + 3.65f, Deck);
            Box("Тело платформы", new Vector3(0, .61f, z), new Vector3(252, 1.22f, 8.3f), "Concrete", true);
            Box("Покрытие платформы", new Vector3(0, 1.245f, z), new Vector3(252, .06f, 8.3f), moscow ? "Tiles" : "Asphalt");
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Светлая кромка", new Vector3(0, 1.28f, z + side * 4), new Vector3(252, .03f, .25f), "White");
                Box("Предупредительная линия", new Vector3(0, 1.28f, z + side * 3.55f), new Vector3(252, .014f, .08f), "Yellow");
                Box("Свес кромки над путём", new Vector3(0, 1.13f, z + side * 4.18f), new Vector3(252, .24f, .08f), "Concrete");
            }
            // Open platform end: railing across the deck, the ramp is outside the playable area.
            for (float a = z - 3.9f; a <= z + 3.95f; a += 1.3f)
                Tube("Стойка торцевого ограждения", new Vector3(125.6f, Deck, a), new Vector3(125.6f, Deck + 1.1f, a), .05f, "Steel");
            foreach (float h in new[] { .55f, 1.1f })
                Tube("Поручень торцевого ограждения", new Vector3(125.6f, Deck + h, z - 3.9f), new Vector3(125.6f, Deck + h, z + 3.9f), .045f, "Steel");
            Box("Табличка торца", new Vector3(125.62f, Deck + .8f, z), new Vector3(.03f, .4f, .6f), "Red");
            Blocker("Коллизия торцевого ограждения", new Vector3(125.6f, Deck + .6f, z), new Vector3(.15f, 1.2f, 7.9f));
        }
        static void PlatformDetail(bool moscow)
        {
            foreach (float z in PlatformAxes)
            {
                foreach (float side in new[] { -1f, 1f })
                    for (float x = -126; x < 126; x += 3)
                        Box("Шов бортового камня", new Vector3(x, .98f, z + side * 4.235f), new Vector3(.018f, .45f, .025f), "Roof");
                // Moscow tiles show transverse joints; Petersburg asphalt shows drain inlets at columns.
                if (moscow) for (float x = -124.5f; x < 126; x += 3)
                    Box("Шов плитки", new Vector3(x, 1.277f, z), new Vector3(.025f, .01f, 7.9f), "Roof");
                else for (float x = -112; x <= 116; x += 24)
                    Box("Водоприёмная решётка", new Vector3(x + .5f, 1.279f, z), new Vector3(.45f, .012f, .45f), "Dark");
            }
        }

        static void PetersburgCanopy(float z)
        {
            // White Y supports with a red base and a dark soffit (photos of platform 4P).
            const float top = 6.7f;
            for (float x = -112; x <= 116; x += 12)
            {
                Box("Опора навеса", new Vector3(x, 3.4f, z), new Vector3(.32f, 4.25f, .38f), "White", true);
                Box("Красная база Y-опоры", new Vector3(x, 1.7f, z), new Vector3(.37f, .85f, .43f), "Red");
                Box("Узел разветвления Y-опоры", new Vector3(x, 4.45f, z), new Vector3(.4f, .62f, .52f), "White");
                foreach (float side in new[] { -1f, 1f })
                {
                    Beam("Ветвь Y-опоры", new Vector3(x, 4.2f, z), new Vector3(x, top, z + side * 3.2f), .22f, "White");
                    Box("Опорный столик ветви", new Vector3(x, top - .08f, z + side * 3.2f), new Vector3(.34f, .12f, .44f), "White");
                    Box("Линейный свет платформы", new Vector3(x + 3, 6.38f, z + side * 2), new Vector3(2.5f, .045f, .1f), "Lamp");
                }
                Box("Ребро навеса", new Vector3(x, top, z), new Vector3(.12f, .2f, 8.5f), "Roof");
                if (Mathf.RoundToInt(x + 112) % 24 == 0)
                    Tube("Водосточная труба", new Vector3(x + .3f, 6.6f, z), new Vector3(x + .3f, Deck + .08f, z), .11f, "Steel");
                if (Mathf.Abs(z + 6) > 1 && Mathf.RoundToInt(x) % 24 == 8) Bench(new Vector3(x + 3, 1.29f, z + .7f));
            }
            foreach (float side in new[] { -3.2f, 0, 3.2f })
                Box("Прогон навеса", new Vector3(0, top - .17f, z + side), new Vector3(252, .22f, .14f), "Roof");
            Box("Кровля продольного навеса", new Vector3(0, 6.83f, z), new Vector3(252, .17f, 8.6f), "Roof");
            foreach (float side in new[] { -1f, 1f })
                Box("Водосточный жёлоб", new Vector3(0, 6.8f, z + side * 4.3f), new Vector3(252, .19f, .12f), "Steel");
        }
        static void MoscowCanopy(float z)
        {
            // Dark round columns at a wider pitch, cantilever beams and a glazed ridge strip.
            const float top = 6.9f, start = -88, end = 126, mid = (start + end) / 2, length = end - start;
            for (float x = -82; x <= 116; x += 18)
            {
                Column("Тёмная круглая колонна платформы", new Vector3(x, Deck, z), top - .5f - Deck, .23f, "Roof", collision: true);
                Column("Опорная плита колонны", new Vector3(x, Deck, z), .05f, .4f, "Steel");
                Column("Капитель колонны", new Vector3(x, top - .65f, z), .35f, .36f, "Dark");
                Box("Консольная балка", new Vector3(x, top - .15f, z), new Vector3(.34f, .32f, 8.3f), "Roof");
                foreach (float side in new[] { -1f, 1f })
                    Beam("Подкос консоли", new Vector3(x, top - 1.9f, z), new Vector3(x, top - .3f, z + side * 2.8f), .15f, "Roof");
                if (Mathf.Abs(z + 6) > 1 && Mathf.RoundToInt(x + 82) % 36 == 0) Bench(new Vector3(x + 5, 1.29f, z + .9f));
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Кровля навеса платформы", new Vector3(mid, top + .05f, z + side * 2.6f), new Vector3(length, .16f, 3.4f), "Roof");
                Box("Линейный свет навеса", new Vector3(mid, top - .34f, z + side * 2.2f), new Vector3(length, .04f, .12f), "Lamp");
                Box("Водосточный жёлоб", new Vector3(mid, top, z + side * 4.3f), new Vector3(length, .2f, .12f), "Steel");
            }
            Box("Остеклённый конёк навеса", new Vector3(mid, top + .08f, z), new Vector3(length, .03f, 1.8f), "Glass");
            // Joint to the wave: transverse beam and a glazed step up to the concourse soffit.
            float wave = 9 + .85f * Mathf.Cos((z + 6) / 9);
            Box("Стыковая балка навесов", new Vector3(start, top + .15f, z), new Vector3(.5f, .6f, 8.8f), "Dark");
            Box("Остеклённый фриз стыка", new Vector3(start, (top + .45f + wave) / 2, z), new Vector3(.04f, wave - top - .45f, 8.6f), "Glass");
        }
        static void MoscowConcourse()
        {
            // Wave profile approximated from photos; not claimed as fabrication geometry.
            for (float x = -143; x < -88; x += 4)
                for (float z = -29; z < 43; z += 4)
                {
                    float y = 9 + .85f * Mathf.Cos((z + 6) / 9), nextY = 9 + .85f * Mathf.Cos((z + 10) / 9);
                    // The pane follows the same endpoints as its ribs; horizontal tiles left steps
                    // and daylight gaps at every joint of the curved concourse roof.
                    float rise = nextY - y;
                    Box("Стеклянная секция распределительного навеса", new Vector3(x + 2, (y + nextY) / 2, z + 2),
                        new Vector3(3.96f, .035f, Mathf.Sqrt(16 + rise * rise)), "Glass")
                        .transform.rotation = Quaternion.Euler(-Mathf.Atan2(rise, 4) * Mathf.Rad2Deg, 0, 0);
                    Beam("Поперечное ребро волны", new Vector3(x, y, z), new Vector3(x + 4, y, z), .12f, "Roof");
                    Beam("Продольное ребро волны", new Vector3(x, y, z), new Vector3(x, nextY, z + 4), .12f, "Roof");
                    if (x + 4 > -88) Beam("Краевая балка волны", new Vector3(x + 4, y, z), new Vector3(x + 4, nextY, z + 4), .3f, "Dark");
                }
            foreach (float x in new[] { -132f, -108f }) foreach (float z in new[] { -18f, 6f, 30f })
            {
                Column("Тёмная круглая колонна", new Vector3(x, Deck, z), 7.4f - Deck, .325f, "Roof", collision: true);
                Column("Опорная плита колонны", new Vector3(x, Deck, z), .06f, .55f, "Steel");
                Column("Узел опирания", new Vector3(x, 7.2f, z), .4f, .475f, "Dark");
                foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    var tip = new Vector3(x, 0, z) + d * 3;
                    tip.y = 9 + .85f * Mathf.Cos((tip.z + 6) / 9) - .1f;
                    Tube("Ветвь несущей колонны", new Vector3(x, 7.4f, z), tip, .3f, "Roof");
                }
            }
        }

        static void PetersburgTerminal(TripStop stop)
        {
            // Platform-side facade of the head building; not the Vosstaniya Square frontage.
            Box("Главный корпус — платформенный фасад", new Vector3(-149, 7.1f, 6), new Vector3(13, 14.2f, 72), "Plaster", true);
            // Plinth stops at the exit group so the door openings reach the floor.
            float gz = stop.ExitGroup.z;
            foreach (var (a, b) in new[] { (-30f, gz - 6), (gz + 6, 42f) })
                Box("Цоколь терминала", new Vector3(FacadeX + .12f, 1.55f, (a + b) / 2), new Vector3(.25f, .55f, b - a), "Concrete");
            Box("Карниз главного корпуса", new Vector3(FacadeX + .35f, 12.7f, 6), new Vector3(.7f, .45f, 74), "White");
            Box("Парапет", new Vector3(FacadeX + .2f, 14.6f, 6), new Vector3(.4f, .8f, 72), "Plaster");
            foreach (float y in new[] { 5.3f, 10.7f, 13.8f })
                Box("Горизонтальный пояс фасада", new Vector3(FacadeX + .6f, y, 6), new Vector3(.35f, .18f, 73), "White");
            for (float z = -28; z <= 40; z += 4)
                Box("Пилястра", new Vector3(FacadeX + .2f, 7, z), new Vector3(.45f, 11.6f, .45f), "White");
            for (float z = -26; z <= 38; z += 4)
            {
                bool exit = Mathf.Abs(z - stop.ExitGroup.z) < 5;
                Box("Высокое окно в нише", new Vector3(FacadeX + .08f, 8.2f, z), new Vector3(.12f, 4, 2.4f), "Roof");
                for (int arc = 0; arc < 12; arc++)
                {
                    float a = arc * Mathf.PI / 12, b = (arc + 1) * Mathf.PI / 12;
                    Beam("Арочный наличник", new Vector3(FacadeX + .63f, 9.7f + 1.2f * Mathf.Sin(a), z + 1.2f * Mathf.Cos(a)),
                        new Vector3(FacadeX + .63f, 9.7f + 1.2f * Mathf.Sin(b), z + 1.2f * Mathf.Cos(b)), .12f, "White");
                }
                Box("Оконный импост", new Vector3(FacadeX + .59f, 8.1f, z), new Vector3(.12f, 3.4f, .065f), "White");
                Box("Переплёт окна", new Vector3(FacadeX + .6f, 8.5f, z), new Vector3(.12f, .065f, 2.3f), "White");
                if (exit)
                {
                    // The only public doors on the platform side: three portals of one exit group.
                    Box("Портал выхода", new Vector3(FacadeX + .1f, 3.05f, z), new Vector3(.2f, 3.5f, 2.9f), "White");
                    Box("Глубина дверного проёма", new Vector3(FacadeX + .16f, 2.85f, z), new Vector3(.16f, 3.05f, 2.45f), "Dark");
                    foreach (float side in new[] { -.6f, .6f })
                        Box("Остеклённая дверь выхода", new Vector3(FacadeX + .26f, 2.5f, z + side), new Vector3(.05f, 2.35f, 1.1f), "Glass");
                }
                else
                {
                    Box("Окно первого этажа", new Vector3(FacadeX + .08f, 3.4f, z), new Vector3(.12f, 2.4f, 2), "Roof");
                    Box("Подоконник", new Vector3(FacadeX + .3f, 2.15f, z), new Vector3(.4f, .1f, 2.3f), "White");
                    Box("Средник окна", new Vector3(FacadeX + .16f, 3.4f, z), new Vector3(.06f, 2.4f, .06f), "White");
                }
            }
            Box("Козырёк над выходом", new Vector3(FacadeX + .9f, 4.95f, gz), new Vector3(1.8f, .18f, 12.2f), "Roof");
            foreach (float side in new[] { -6f, 6f })
                Beam("Кронштейн козырька", new Vector3(FacadeX + .05f, 4.1f, gz + side), new Vector3(FacadeX + 1.75f, 4.9f, gz + side), .1f, "Steel");
            Box("Панель надписи выхода", new Vector3(FacadeX + .48f, 5.75f, gz), new Vector3(.06f, .55f, 5.4f), "ServiceGreen");
            ClockAndName(stop, .5f, new Vector3(FacadeX + .8f, 13.32f, 6), .7f);
            TerminalEnds(false);
            // Transverse end hall over the concourse on light columns; flat, unlike Moscow's wave.
            Box("Кровля торцевого перрона", new Vector3(-133.5f, 7.75f, 6), new Vector3(18, .28f, 74), "Roof");
            for (float z = -30; z <= 42; z += 6)
                Box("Ферма торцевого перрона", new Vector3(-133.5f, 7.3f, z), new Vector3(18, .6f, .14f), "White");
            foreach (float z in new[] { -24f, -12f, 0f, 12f, 24f, 36f })
                Box("Колонна торцевого перрона", new Vector3(-127, 4.5f, z), new Vector3(.42f, 6.45f, .42f), "White", true);
        }
        static void MoscowTerminal(TripStop stop)
        {
            // Glazed lower vestibule under the wave, historic plaster wall with rectangular windows above.
            Box("Главный корпус — со стороны платформ", new Vector3(-149, 8, 6), new Vector3(13, 16, 72), "Plaster", true);
            Box("Тёмный вестибюль за стеклом", new Vector3(FacadeX + .03f, 3.9f, 6), new Vector3(.05f, 5.2f, 72), "Dark");
            Box("Стеклянный экран вестибюля", new Vector3(FacadeX + .25f, 3.9f, 6), new Vector3(.03f, 5.2f, 72), "Glass");
            for (float z = -30; z <= 42; z += 2)
                Box("Стойка витража", new Vector3(FacadeX + .3f, 3.9f, z), new Vector3(.12f, 5.2f, .08f), "Steel");
            foreach (float y in new[] { 1.4f, 3.7f, 6.45f })
                Box("Ригель витража", new Vector3(FacadeX + .3f, y, 6), new Vector3(.14f, .12f, 72), "Steel");
            Box("Пояс над вестибюлем", new Vector3(FacadeX + .15f, 6.9f, 6), new Vector3(.3f, .7f, 73), "Concrete");
            for (float z = -28; z <= 40; z += 4)
            {
                // One tall row above the wave soffit (max 9.85 m) so the glass never cuts a window.
                Box("Прямоугольное окно в нише", new Vector3(FacadeX + .05f, 12, z), new Vector3(.1f, 3.2f, 1.6f), "Roof");
                Box("Наличник", new Vector3(FacadeX + .12f, 13.75f, z), new Vector3(.24f, .22f, 2), "White");
                foreach (float side in new[] { -.9f, .9f })
                    Box("Откос окна", new Vector3(FacadeX + .12f, 12, z + side), new Vector3(.24f, 3.3f, .2f), "Plaster");
                Box("Подоконник", new Vector3(FacadeX + .15f, 10.35f, z), new Vector3(.3f, .1f, 1.9f), "White");
            }
            Box("Карниз главного корпуса", new Vector3(FacadeX + .4f, 15.2f, 6), new Vector3(.8f, .5f, 74), "White");
            float gz = stop.ExitGroup.z;
            foreach (float side in new[] { -6.2f, 6.2f })
                Box("Боковой пилон портала", new Vector3(FacadeX + .9f, 3.3f, gz + side), new Vector3(1.6f, 4, .35f), "Steel", true);
            Box("Ригель портала выхода", new Vector3(FacadeX + .9f, 5.1f, gz), new Vector3(1.6f, .7f, 12.75f), "Steel");
            foreach (float z in new[] { gz - 4, gz, gz + 4 })
                foreach (float side in new[] { -.6f, .6f })
                    Box("Автоматическая дверь выхода", new Vector3(FacadeX + .32f, 2.5f, z + side), new Vector3(.05f, 2.4f, 1.15f), "Glass");
            ClockAndName(stop, .3f, new Vector3(FacadeX + .3f, 14.48f, 6), .8f);
            TerminalEnds(true);
        }
        static void ClockAndName(TripStop stop, float clockX, Vector3 name, float nameSize)
        {
            var clock = GameObject.CreatePrimitive(PrimitiveType.Cylinder); clock.name = "Часы вокзала";
            clock.transform.SetParent(root, false); clock.transform.position = new Vector3(FacadeX + clockX, 11.9f, 6);
            clock.transform.rotation = Quaternion.Euler(0, 0, 90); clock.transform.localScale = new Vector3(1.25f, .06f, 1.25f);
            clock.GetComponent<Renderer>().sharedMaterial = mats["White"]; UnityEngine.Object.DestroyImmediate(clock.GetComponent<Collider>()); clock.isStatic = true;
            Beam("Минутная стрелка", new Vector3(FacadeX + clockX + .07f, 11.9f, 6), new Vector3(FacadeX + clockX + .07f, 12.37f, 6), .035f, "Dark");
            Beam("Часовая стрелка", new Vector3(FacadeX + clockX + .08f, 11.9f, 6), new Vector3(FacadeX + clockX + .08f, 11.78f, 5.73f), .045f, "Dark");
            for (int hour = 0; hour < 12; hour++)
            {
                float angle = hour * Mathf.PI / 6;
                var direction = new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle));
                var centre = new Vector3(FacadeX + clockX + .075f, 11.9f, 6);
                Beam("Деление циферблата", centre + direction * .49f, centre + direction * .55f, .025f, "Dark");
            }
            Text(stop.Station, name, nameSize, 90);
        }

        static void TerminalEnds(bool moscow)
        {
            float height = moscow ? 16 : 14.2f;
            foreach (float z in new[] { -30f, 42f })
            {
                float side = z < 0 ? -1 : 1;
                Bevel("Цоколь бокового фасада", new Vector3(-149, 1, z + side * .1f), new Vector3(13.2f, 2, .3f), "Concrete", .05f);
                Bevel("Карниз бокового фасада", new Vector3(-149, height - .8f, z + side * .15f), new Vector3(13.6f, .45f, .5f), "White", .04f);
                for (float x = -153; x <= -145; x += 4)
                    for (float y = 3.8f; y < height - 2; y += 4)
                    {
                        Box("Окно бокового фасада", new Vector3(x, y, z + side * .025f), new Vector3(1.6f, 2.2f, .04f), "Roof");
                        Box("Отлив бокового фасада", new Vector3(x, y - 1.15f, z + side * .14f), new Vector3(1.95f, .14f, .32f), "White");
                        foreach (float edge in new[] { -.87f, .87f })
                            Box("Наличник бокового фасада", new Vector3(x + edge, y, z + side * .12f), new Vector3(.14f, 2.4f, .24f), "White");
                    }
            }
            Box("Кровля терминала", new Vector3(-149, height + .08f, 6), new Vector3(13.4f, .16f, 72.4f), "Roof");
        }

        static void StationDetails(bool moscow)
        {
            // Stop blocks, bins, benches and a kiosk give scale and purpose. The zone x 18..62
            // next to car 3 stays clear for the queue, the door and alighting passengers.
            for (int track = -2; track <= 3; track++)
            {
                Box("Тупиковый упор", new Vector3(-120, .7f, track * 12), new Vector3(.45f, .35f, 2.6f), "Red", true);
                foreach (float z in new[] { -.76f, .76f }) Beam("Подкос упора", new Vector3(-121, .15f, track * 12 + z), new Vector3(-119.9f, .85f, track * 12 + z), .16f, "Roof");
            }
            foreach (float x in new[] { -80f, -32f, 14f, 66f, 100f })
            {
                Bench(new Vector3(x, 1.3f, -6.8f));
                Bevel("Урна", new Vector3(x - 1.6f, 1.72f, -6.8f), new Vector3(.45f, .85f, .45f), "Roof", .045f, true);
                Box("Отверстие урны", new Vector3(x - 1.6f, 2.03f, -6.565f), new Vector3(.32f, .14f, .012f), "Dark");
                if (moscow) Box("Щелевой лоток", new Vector3(x, 1.281f, -9.2f), new Vector3(2, .012f, .12f), "Steel");
                else Box("Дренажная решётка", new Vector3(x, 1.281f, -9.2f), new Vector3(2, .014f, .19f), "Roof");
            }
            Bevel("Павильон кофе", new Vector3(-101, 2.9f, -6.9f), new Vector3(4, 3.2f, 2.2f), "Roof", .08f, true);
            Bevel("Цоколь павильона", new Vector3(-101, 1.43f, -6.9f), new Vector3(4.08f, .28f, 2.28f), "Steel", .045f);
            Bevel("Фриз павильона", new Vector3(-101, 4.5f, -6.9f), new Vector3(4.25f, .2f, 2.45f), "Steel", .04f);
            // Ends are visible on the way to the terminal: frame a service door and side panel
            // rather than leaving a solid featureless cube. No prop enters the passing lane.
            foreach (float x in new[] { -103.035f, -98.965f })
            {
                Box("Панель торца павильона", new Vector3(x, 2.95f, -6.9f), new Vector3(.025f, 2.6f, 1.7f), "Blue");
                Box("Дверь обслуживания павильона", new Vector3(x + (x < -101 ? -.018f : .018f), 2.45f, -6.9f), new Vector3(.025f, 2.15f, .86f), "Steel");
            }
            Box("Витрина павильона", new Vector3(-101, 3.0f, -5.79f), new Vector3(3.5f, 1.7f, .02f), "Glass");
            Box("Прилавок павильона", new Vector3(-101, 2.2f, -5.65f), new Vector3(3.55f, .08f, .34f), "Steel");
            Box("Вывеска павильона", new Vector3(-101, 4.1f, -5.72f), new Vector3(3.8f, .48f, .06f), "Blue");
            Text("КОФЕ  ·  В ДОРОГУ", new Vector3(-101, 4.1f, -5.675f), .24f);
        }
        static void Bench(Vector3 p)
        {
            foreach (float x in new[] { -.75f, .75f })
            {
                Box("Ножка скамьи", p + new Vector3(x, .23f, 0), new Vector3(.1f, .46f, .46f), "Roof");
                Beam("Опора спинки", p + new Vector3(x, .35f, .15f), p + new Vector3(x, .87f, .25f), .045f, "Steel");
            }
            for (int slat = 0; slat < 5; slat++)
                Box("Рейка сиденья", p + new Vector3(0, .48f, -.21f + slat * .09f), new Vector3(2, .045f, .07f), "White");
            for (int slat = 0; slat < 3; slat++)
                Box("Рейка спинки", p + new Vector3(0, .64f + slat * .09f, .22f), new Vector3(2, .065f, .045f), "White");
            Blocker("Коллизия скамьи", p + new Vector3(0, .45f, 0), new Vector3(2, .9f, .55f));
        }

        static void Navigation(TripStop stop, bool moscow)
        {
            foreach (var sign in stop.Signs)
            {
                var p = sign.Position;
                switch (sign.Kind)
                {
                    case SignKind.Board:
                        Box("Табло поезда", p, new Vector3(.18f, .95f, 3.4f), "Dark");
                        foreach (bool back in new[] { false, true })
                            Live(stop, SignKind.Board, p + new Vector3(back ? -.1f : .1f, 0, 0), .19f, back, new Color(.91f, .86f, .65f));
                        Hangers(p + new Vector3(0, .47f, 0), 1.3f, Ceiling(moscow));
                        break;
                    case SignKind.Carriage:
                        Box("Указатель вагона", p, new Vector3(.06f, .55f, 1.5f), "White");
                        foreach (bool back in new[] { false, true })
                            Live(stop, SignKind.Carriage, p + new Vector3(back ? -.035f : .035f, 0, 0), .3f, back, new Color(.09f, .28f, .46f));
                        Hangers(p + new Vector3(0, .27f, 0), .5f, Ceiling(moscow));
                        break;
                    case SignKind.Platform:
                        Box("Указатель платформы", p, new Vector3(.1f, .55f, 4), "Blue");
                        foreach (bool back in new[] { false, true })
                            Live(stop, SignKind.Platform, p + new Vector3(back ? -.055f : .055f, 0, 0), .17f, back);
                        Hangers(p + new Vector3(0, .27f, 0), 1.6f, Ceiling(moscow));
                        break;
                    case SignKind.Exit:
                        Box("Указатель выхода", p, new Vector3(.08f, .95f, 2.4f), "ServiceGreen");
                        Text(TripInfo.ExitText, p + new Vector3(.045f, .2f, 0), .2f, 90);
                        Arrow(p + new Vector3(.06f, -.22f, 0), .36f);
                        Hangers(p + new Vector3(0, .47f, 0), 1.05f, Ceiling(moscow));
                        break;
                    case SignKind.ExitPortal:
                        Text(TripInfo.ExitText, p, .4f, 90);
                        break;
                    case SignKind.Stela: Stela(stop, sign); break;
                }
            }
            // Other platforms carry only their own numbers: no second copy of our trip.
            foreach (float z in PlatformAxes)
            {
                if (Mathf.Abs(z + 6) < 1) continue;
                var p = new Vector3(moscow ? -19 : -10, 4.1f, z);
                Box("Указатель платформы", p, new Vector3(.1f, .55f, 4), "Blue");
                foreach (bool back in new[] { false, true })
                    Text(TripInfo.PlatformLabel(stop, z, back), p + new Vector3(back ? -.055f : .055f, 0, 0), .17f, back ? -90 : 90);
                Hangers(p + new Vector3(0, .27f, 0), 1.6f, Ceiling(moscow));
            }
        }
        // World text bound to the shift stage; front faces +X, back is a separate unmirrored text.
        static void Live(TripStop stop, SignKind kind, Vector3 p, float size, bool back, Color? color = null)
        {
            Text(TripInfo.Text(kind, stop.Location, "", back), p, size, back ? -90 : 90, color);
            var board = root.GetChild(root.childCount - 1).gameObject.AddComponent<TripBoard>();
            board.Location = stop.Location; board.Kind = kind; board.Back = back;
        }
        // Underside of the platform canopy roof slab (see PetersburgCanopy / MoscowCanopy).
        static float Ceiling(bool moscow) => moscow ? 6.87f : 6.745f;
        static void Hangers(Vector3 top, float halfSpan, float ceiling)
        {
            foreach (float side in new[] { -halfSpan, halfSpan })
                Tube("Подвес указателя", top + new Vector3(0, 0, side), new Vector3(top.x, ceiling, top.z + side), .025f, "Roof");
        }
        // Arrow in the plane of a sign facing +X; pointing up means "straight ahead".
        static void Arrow(Vector3 centre, float length, float width = .06f)
        {
            var tip = centre + Vector3.up * length / 2;
            Beam("Стрелка указателя", centre - Vector3.up * length / 2, tip, width, "White");
            foreach (float side in new[] { -1f, 1f })
                Beam("Стрелка указателя", tip, tip + new Vector3(0, -length * .38f, side * length * .38f), width, "White");
        }
        static void Stela(TripStop stop, NavSign sign)
        {
            // Rounded matte stela: track number first, then the trip, exit last and only facing the exit.
            var p = sign.Position;
            Box("Основание стелы", p + new Vector3(0, .06f, 0), new Vector3(.62f, .12f, 1.05f), "Roof");
            Box("Корпус стелы", p + new Vector3(0, 1.24f, 0), new Vector3(.22f, 2.32f, .88f), "Steel", true);
            foreach (float side in new[] { -1f, 1f })
                Tube("Скруглённая кромка стелы", p + new Vector3(0, .12f, side * .44f), p + new Vector3(0, 2.4f, side * .44f), .22f, "Steel");
            foreach (bool back in new[] { false, true })
            {
                float s = back ? -1 : 1, yaw = back ? -90 : 90;
                Box("Утопленный дисплей", p + new Vector3(s * .115f, 1.39f, 0), new Vector3(.014f, 1.53f, .66f), "Dark");
                Text("ПУТЬ", p + new Vector3(s * .124f, 2.06f, 0), .09f, yaw);
                Text(stop.Track.ToString(), p + new Vector3(s * .124f, 1.78f, 0), .34f, yaw);
                Live(stop, SignKind.Stela, p + new Vector3(s * .124f, 1.33f, 0), .06f, back);
                Box("Акцент навигации", p + new Vector3(s * .124f, 1.0f, 0), new Vector3(.012f, .035f, .55f), "Blue");
                if (sign.Exit && !back)
                {
                    Text(TripInfo.ExitText, p + new Vector3(.124f, .88f, 0), .06f, yaw);
                    Arrow(p + new Vector3(.126f, .74f, 0), .14f, .025f);
                }
            }
        }

    }
}
