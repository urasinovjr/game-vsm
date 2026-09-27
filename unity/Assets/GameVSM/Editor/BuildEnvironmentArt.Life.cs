using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameVSM.Editor
{
    // Calm working life (scene-finish plan, sections 6-7): each background person gets a
    // prepared route anchored to stable geometry. Life never starts timers or changes scores.
    public static partial class BuildEnvironmentArt
    {
        // Car 3 door (x≈47.5) is the training door on both stations; its queue and the
        // alighting stream own this strip of platform.
        static bool DoorZone(Vector3 p) => root.name != "Depot" && p.x > 43 && p.x < 60 && p.z > -5.4f && p.z < -1.9f;
        // Leningradsky: lanes the alighting passengers take to the concourse.
        static bool ExitLane(Vector3 p) => root.name == "Leningradsky" && p.x < 62 && p.z > -5.2f && p.z < -1.9f;

        static PassengerMotion Person(Vector3 p, float yaw, bool walk, string character = "Passenger",
            RouteStop[] route = null, LifeRole? role = null, bool? loop = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Assets + "/Art/Characters/" + character + ".prefab");
            if (asset == null) return null; // Character builder can be run before regenerating locations.
            // Background figures are moved behind the canopy columns rather than into a reserved strip.
            if (route == null && (DoorZone(p) || (!walk && ExitLane(p)))) p.z = -8.2f;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset); go.transform.SetParent(root, false);
            go.transform.position = p; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var motion = go.GetComponent<PassengerMotion>();
            motion.Role = role ?? (character == "DepotWorker" ? LifeRole.DepotWorker : LifeRole.Passenger);
            bool oneWay = false;
            motion.Route = route ?? (walk ? DefaultRoute(p, motion.Role, out oneWay) : Array.Empty<RouteStop>());
            motion.Loop = loop ?? !oneWay;
            return motion;
        }

        // Routes for the existing placements that only say "walks".
        static RouteStop[] DefaultRoute(Vector3 p, LifeRole role, out bool oneWay)
        {
            oneWay = false;
            if (role == LifeRole.DepotWorker)
            {
                // Arrive → own cabinet → tool trolley → pause, facing the work, never the train path.
                var cabinet = Nearest("Электрошкаф", p, 12);
                if (cabinet != null)
                {
                    float x = cabinet.Value.x, y = p.y;
                    return new[] {
                        new RouteStop(p, StopKind.Wait, 5),
                        new RouteStop(new Vector3(x, y, -10.45f), StopKind.Work, 8).Facing(180),
                        new RouteStop(new Vector3(x + 2, y, -9.85f), StopKind.Work, 5).Facing(180),
                        new RouteStop(new Vector3(x + 3.5f, y, -9.2f), StopKind.Wait, 6).Facing(90) };
                }
            }
            if (root.name == "Leningradsky")
            {
                // Arrived passengers go to the concourse; they are replaced only out of sight.
                oneWay = true;
                if (p.x > 43)
                {
                    // East of the training door: cross between canopy columns and use the far lane.
                    float gap = Gap(p.x - 6);
                    return new[] {
                        new RouteStop(p, StopKind.Wait, 8),
                        new RouteStop(new Vector3(gap, p.y, p.z)),
                        // Far lane inside the yellow line (-9.55), clear of the cleaner (-8.7) and stelas.
                        new RouteStop(new Vector3(gap, p.y, -9.3f)),
                        new RouteStop(new Vector3(-100, p.y, -9.3f), StopKind.End) };
                }
                return new[] {
                    new RouteStop(p, StopKind.Wait, 8),
                    new RouteStop(new Vector3(p.x - 6, p.y, p.z - .3f)),
                    new RouteStop(new Vector3(-100, p.y, p.z - .3f), StopKind.End) };
            }
            // Wait, walk to another spot along the same lane away from the training door, wait there.
            float dir = p.x > 47.5f ? 1 : -1;
            var target = new Vector3(p.x + dir * 11, p.y, p.z - .35f);
            return new[] { new RouteStop(p, StopKind.Wait, 12), new RouteStop(target, StopKind.Wait, 9).Facing(dir > 0 ? 270 : 90) };
        }

        // Midpoint between the two platform-canopy supports around x (the platform at z = -6).
        static float Gap(float x)
        {
            var columns = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Опора навеса" && Mathf.Abs(t.position.z + 6) < .5f).Select(t => t.position.x).OrderBy(c => c).ToArray();
            for (int i = 0; i + 1 < columns.Length; i++)
                if (columns[i] <= x && x < columns[i + 1]) return (columns[i] + columns[i + 1]) / 2;
            return x;
        }
        static Vector3? Nearest(string name, Vector3 p, float within)
        {
            var hit = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name)
                .OrderBy(t => (t.position - p).sqrMagnitude).FirstOrDefault();
            return hit != null && Vector3.Distance(hit.position, p) < within ? hit.position : (Vector3?)null;
        }

        // Added roles; at most two per site to stay within the measured frame budget.
        static void Life(string site)
        {
            if (site == "Depot")
            {
                // Delivery: stores → reception point by the cabinets and back, in the green service strip.
                var delivery = Person(new Vector3(86, .04f, -9.3f), 270, true, "DepotWorker", new[] {
                    new RouteStop(new Vector3(86, .04f, -9.3f), StopKind.Wait, 6).Facing(270),
                    new RouteStop(new Vector3(64, .04f, -9.3f), StopKind.Work, 8).Facing(180),
                    new RouteStop(new Vector3(75, .04f, -9.3f)) }, LifeRole.Delivery);
                if (delivery != null) delivery.Cart = Cart(delivery.transform);
                // Cleaning the entrance section, clear of the personnel walkway.
                Person(new Vector3(97, .04f, -9.4f), 90, true, "DepotWorker", new[] {
                    new RouteStop(new Vector3(97, .04f, -9.4f), StopKind.Work, 7).Facing(180),
                    new RouteStop(new Vector3(104, .04f, -9.6f), StopKind.Work, 7).Facing(0),
                    new RouteStop(new Vector3(108, .04f, -9.2f), StopKind.Wait, 4).Facing(270) }, LifeRole.Cleaner);
            }
            else if (site == "Moskovsky")
            {
                // Cleaning behind the canopy columns, never through the queue at car 3.
                Person(new Vector3(30, 1.3f, -8.3f), 90, true, "DepotWorker", new[] {
                    new RouteStop(new Vector3(30, 1.3f, -8.3f), StopKind.Work, 6),
                    new RouteStop(new Vector3(44, 1.3f, -8.9f)),
                    new RouteStop(new Vector3(58, 1.3f, -8.9f), StopKind.Work, 7),
                    new RouteStop(new Vector3(72, 1.3f, -8.4f), StopKind.Wait, 4).Facing(270) }, LifeRole.Cleaner);
                // Waits for boarding near the car, shifts weight, keeps waiting.
                Person(new Vector3(50.5f, 1.3f, -7.3f), 270, true, "PassengerWoman", new[] {
                    new RouteStop(new Vector3(50.5f, 1.3f, -7.3f), StopKind.Wait, 14).Facing(270),
                    new RouteStop(new Vector3(51.2f, 1.3f, -7.5f), StopKind.Wait, 9).Facing(250) });
            }
            else if (site == "Leningradsky")
            {
                // Cleans the car 3 section once alighting passengers have left it.
                var cleaner = Person(new Vector3(57.5f, 1.3f, -8.7f), 270, true, "DepotWorker", new[] {
                    new RouteStop(new Vector3(57.5f, 1.3f, -8.7f), StopKind.Wait, 5),
                    new RouteStop(new Vector3(50, 1.3f, -7.6f)),
                    new RouteStop(new Vector3(50, 1.3f, -4.3f), StopKind.Work, 6),
                    new RouteStop(new Vector3(45, 1.3f, -3.9f), StopKind.Work, 6),
                    new RouteStop(new Vector3(53, 1.3f, -3.8f), StopKind.Work, 6),
                    new RouteStop(new Vector3(50, 1.3f, -7.6f)) }, LifeRole.Cleaner);
                if (cleaner != null) cleaner.AfterArrival = true;
                // Someone meeting the train waits away from the doors, then leaves for the concourse.
                Person(new Vector3(24, 1.3f, -5.4f), 90, true, "PassengerWoman", new[] {
                    new RouteStop(new Vector3(24, 1.3f, -5.4f), StopKind.Wait, 25).Facing(90),
                    new RouteStop(new Vector3(21, 1.3f, -4.5f)),
                    new RouteStop(new Vector3(-100, 1.3f, -4.5f), StopKind.End) }, loop: false);
            }
        }

        // Flat stock trolley from primitives, pushed ahead; one solid box so the player cannot walk through it.
        static Transform Cart(Transform person)
        {
            var cart = new GameObject("Тележка доставки").transform; cart.SetParent(person, false);
            cart.localPosition = new Vector3(0, 0, .85f);
            void Part(string name, Vector3 p, Vector3 size, string material)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
                part.transform.SetParent(cart, false); part.transform.localPosition = p; part.transform.localScale = size;
                part.GetComponent<Renderer>().sharedMaterial = mats[material];
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            }
            Part("Платформа", new Vector3(0, .2f, .15f), new Vector3(.6f, .05f, .9f), "Blue");
            Part("Коробка запасов", new Vector3(0, .42f, .25f), new Vector3(.5f, .38f, .55f), "Concrete");
            Part("Упаковка фильтров", new Vector3(.02f, .7f, .2f), new Vector3(.4f, .18f, .4f), "White");
            foreach (float x in new[] { -.22f, .22f })
            {
                Part("Стойка ручки", new Vector3(x, .55f, -.28f), new Vector3(.03f, .7f, .03f), "Steel");
                foreach (float z in new[] { -.2f, .5f }) Part("Колесо", new Vector3(x, .07f, z), new Vector3(.05f, .14f, .14f), "Dark");
            }
            Part("Ручка", new Vector3(0, .9f, -.28f), new Vector3(.47f, .03f, .03f), "Steel");
            var solid = cart.gameObject.AddComponent<BoxCollider>();
            solid.center = new Vector3(0, .5f, .12f); solid.size = new Vector3(.62f, .95f, .95f);
            return cart;
        }
    }
}
