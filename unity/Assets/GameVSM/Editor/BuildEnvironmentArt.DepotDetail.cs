using UnityEngine;

namespace GameVSM.Editor
{
    public static partial class BuildEnvironmentArt
    {
        // Authored staff route, not a statement about the real Metallostroy access regulations.
        // Only the rubber crossing connects the two sides of track 1. No yard, pit or roof zone.
        static void DepotWalkRoutes()
        {
            WalkableFloor("От калитки к переходу", 129.1f, 158, -14.2f, -12.4f, 0);
            WalkableFloor("Переход пути 1 по настилу", 129.1f, 130.9f, -14.2f, -5.2f, .1f);
            WalkableFloor("Подход к цеху", 113.5f, 130.9f, -6.25f, -4.8f, 0);
            WalkableFloor("Место инструктажа", 116.4f, 121, -6.25f, -4.2f, 0);
            WalkableFloor("Проход персонала в цехе", -113, 114.3f, -6.05f, -3.05f, .025f);
            Walkable("Лестница вагона 3", new Vector3(46.75f, -.35f, -5.5f), new Vector3(48.25f, 1.7f, -3.15f));
            WalkableFloor("Площадка у двери вагона 3", 46.65f, 48.35f, -3.4f, -1.4f, 1.3f);
        }

        // Near-field detail is concentrated at the start and hall portals, with shared meshes.
        // Dimensions are scenic estimates; the referenced 2009 cross-section remains unchanged.
        static void DepotEntryDetails()
        {
            var c = new Vector3(153.75f, 0, -16.4f);
            foreach (float dx in new[] { -1.94f, .54f })
                Bevel("Откос окна проходной", c + new Vector3(dx, 1.65f, 1.91f), new Vector3(.13f, 1.24f, .18f), "White", .02f);
            Bevel("Перемычка окна проходной", c + new Vector3(-.7f, 2.24f, 1.91f), new Vector3(2.6f, .13f, .18f), "White", .02f);
            Box("Импост окна проходной", c + new Vector3(-.7f, 1.65f, 1.87f), new Vector3(.055f, 1.1f, .055f), "White");
            foreach (float dx in new[] { .88f, 2.02f })
                Bevel("Откос двери проходной", c + new Vector3(dx, 1.2f, 1.91f), new Vector3(.12f, 2.24f, .16f), "White", .02f);
            Bevel("Ручка двери проходной", c + new Vector3(1.78f, 1.13f, 1.93f), new Vector3(.035f, .25f, .07f), "Steel", .012f);
            Box("Светильник проходной", c + new Vector3(1.45f, 2.45f, 1.95f), new Vector3(.34f, .1f, .12f), "Lamp");
            Downpipe(c + new Vector3(-2.31f, 2.91f, 1.75f), Vector3.forward, -.01f);

            // Stand legs already sit outside the path; a shallow cap and frame give it depth.
            Bevel("Козырёк стенда инструктажа", new Vector3(119.5f, 1.87f, -3.94f), new Vector3(2.02f, .07f, .3f), "Roof", .025f);
            foreach (float x in new[] { 118.53f, 120.47f })
                Bevel("Окантовка стенда инструктажа", new Vector3(x, 1.35f, -3.92f), new Vector3(.05f, 1.02f, .09f), "Steel", .01f);
            // Physical end of the painted walkway: the service drive remains closed to players.
            foreach (float x in new[] { 136f, 146, 156 })
            {
                Column("Столбик края пешеходной дорожки", new Vector3(x, 0, -14.65f), .9f, .075f, "Blue", 10, true);
                Column("Отражатель столбика", new Vector3(x, .68f, -14.65f), .12f, .08f, "White", 10);
            }
        }

        static void DepotHallDetails()
        {
            foreach (float x in new[] { -114f, 114f })
            {
                float outward = Mathf.Sign(x);
                Bevel("Торцевая кромка кровли", new Vector3(x + outward * .12f, 9.58f, 0), new Vector3(.44f, .24f, 27.9f), "Roof", .06f);
                foreach (float track in new[] { -7.5f, 0, 7.5f })
                {
                    // Rollers/rails stay against the jamb; the staff lane at z -5.7 stays open.
                    foreach (float side in new[] { -2.48f, 2.48f })
                    {
                        Bevel("Направляющая секционных ворот", new Vector3(x, 3, track + side), new Vector3(.42f, 6, .13f), "Steel", .025f);
                        for (float y = .6f; y < 6; y += 1.5f)
                            Box("Крепление направляющей", new Vector3(x + outward * .24f, y, track + side), new Vector3(.07f, .18f, .22f), "Roof");
                    }
                    foreach (float y in new[] { 5.7f, 5.95f, 6.2f })
                        Box("Шов поднятых ворот", new Vector3(x + outward * .18f, y, track), new Vector3(.025f, .025f, 4.8f), "Roof");
                }
                foreach (float z in new[] { -11.5f, -3.75f, 3.75f, 11.5f })
                {
                    // Same pier footprint as the existing wall, no new obstacle in the approach.
                    Bevel("Защитный цоколь простенка", new Vector3(x + outward * .035f, .37f, z), new Vector3(.37f, .74f, 2.4f), "Concrete", .045f);
                    Box("Отбойная накладка простенка", new Vector3(x + outward * .24f, .83f, z), new Vector3(.08f, .75f, .28f), "Yellow");
                }
                foreach (float z in new[] { -13.98f, 13.98f })
                    Bevel("Угловой нащельник корпуса", new Vector3(x, 4.8f, z), new Vector3(.5f, 9.6f, .22f), "White", .035f);
            }
            // Exhaust cowls articulate the low roof without adding a repeated forest of cylinders.
            foreach (float x in new[] { -75f, -15, 45, 99 })
            {
                Bevel("Основание вытяжки", new Vector3(x, 9.75f, 9), new Vector3(1.5f, .3f, 1.5f), "Roof", .06f);
                Column("Вытяжная шахта", new Vector3(x, 9.85f, 9), 1.05f, .42f, "Steel", 12);
                Bevel("Колпак вытяжки", new Vector3(x, 10.95f, 9), new Vector3(1.4f, .12f, 1.4f), "Roof", .04f);
            }
            // A few structural bands link the platform and its frame at the player's destination.
            Bevel("Торец посадочной площадки", new Vector3(47.5f, 1.23f, -3.35f), new Vector3(1.65f, .17f, .07f), "Blue", .025f);
            foreach (float x in new[] { 46.77f, 48.23f })
            {
                Bevel("Опорная пята площадки", new Vector3(x, .045f, -2.6f), new Vector3(.3f, .09f, .3f), "Steel", .025f);
                Bevel("Стойка посадочной площадки", new Vector3(x, .615f, -2.6f), new Vector3(.1f, 1.05f, .1f), "Blue", .015f);
            }
        }
    }
}
