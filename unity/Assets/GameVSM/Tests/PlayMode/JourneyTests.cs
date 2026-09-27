using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameVSM.Tests
{
    public sealed class JourneyTests
    {
        [UnityTest]
        public IEnumerator DepartureWaitsForConductorThenMovesSceneryWithoutMovingTrain()
        {
            yield return SceneManager.LoadSceneAsync("Metallostroy"); yield return null;
            var world = Object.FindFirstObjectByType<ShiftEnvironment>();
            var train = Object.FindFirstObjectByType<TrainVisibility>();
            Vector3 trainPosition = train.transform.position;
            world.Show("Moskovsky"); yield return null;
            var original = world.Current;
            var door = Object.FindObjectsByType<TrainMechanism>(FindObjectsInactive.Include)
                .First(d => !d.Automatic && d.Kind == "sliding_entry_door" && d.WorldBounds().center.x > 45 && d.WorldBounds().center.x < 49 && d.WorldBounds().center.z < 0);
            door.TrainMoving = false; door.SetOpen(true);
            world.Player.Teleport(new Vector3(47.5f, 1.31f, -3.5f));
            yield return new WaitForSeconds(1);
            Assert.That(door.Phase, Is.GreaterThan(.9f));
            world.TravelTo("Route");
            yield return new WaitForSeconds(1);
            Assert.That(world.Transitioning, Is.True);
            Assert.That(world.Current, Is.SameAs(original));
            Assert.That(original.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(world.TravelSpeed, Is.Zero, "Cannot leave the conductor on the platform.");
            Assert.That(door.IsOpen, Is.True, "Keep an entry available if the conductor stepped out before the reply.");
            Assert.That(door.TrainMoving, Is.False, "An outside conductor must be able to reboard.");
            Assert.That(Object.FindObjectsByType<TrainMechanism>(FindObjectsInactive.Include)
                .Where(d => !d.Automatic && d.Kind == "sliding_entry_door" && d.WorldBounds().center.z > 0).All(d => !d.IsOpen), Is.True,
                "The side away from the platform must remain closed.");
            world.Player.transform.rotation = Quaternion.identity; world.Player.SetMenu(false);
            world.Player.TouchMove = Vector2.up;
            float boardEnd = Time.realtimeSinceStartup + 5;
            while (world.Player.transform.position.z < -.45f && Time.realtimeSinceStartup < boardEnd) yield return null;
            world.Player.TouchMove = Vector2.zero;
            Assert.That(world.Player.transform.position.z, Is.GreaterThan(-.7f), "A waiting conductor must actually walk back through the door.");
            Vector3 playerPosition = world.Player.transform.position;
            yield return new WaitForSeconds(1);
            Assert.That(original.transform.position.x, Is.LessThan(-.5f), "Platform must visibly recede before the cut.");
            Assert.That(door.Phase, Is.LessThan(.01f));
            Assert.That(door.Toggle(), Is.False, "Door must be locked during departure.");
            Assert.That(original.GetComponentsInChildren<Collider>().All(c => !c.enabled), Is.True);
            yield return WaitForJourney(world);
            Assert.That(world.Location, Is.EqualTo("Route"));
            Assert.That(world.TravelSpeed, Is.GreaterThan(0));
            Assert.That(world.Player.transform.position.x, Is.EqualTo(playerPosition.x).Within(.01f));
            Assert.That(world.Player.transform.position.z, Is.EqualTo(playerPosition.z).Within(.01f));
            Assert.That(train.transform.position, Is.EqualTo(trainPosition));
        }

        [UnityTest]
        public IEnumerator ArrivalStopsAtAuthoredPlatformAndRestoresItsCollision()
        {
            yield return SceneManager.LoadSceneAsync("Metallostroy"); yield return null;
            var world = Object.FindFirstObjectByType<ShiftEnvironment>();
            world.Show("Route"); yield return null;
            world.Current.GetComponent<RouteMotion>().Speed = 24;
            world.Player.Teleport(new Vector3(38, 1.31f, -.24f));
            var before = world.Player.transform.position;
            world.TravelTo("Leningradsky");
            Assert.That(world.IsPresented("alight"), Is.False);
            bool sawApproach = false;
            float end = Time.realtimeSinceStartup + 20;
            while (world.Transitioning && Time.realtimeSinceStartup < end)
            {
                if (world.Location == "Leningradsky" && world.Current.transform.position.x > 1)
                {
                    sawApproach = true;
                    Assert.That(world.TravelSpeed, Is.GreaterThan(0));
                    Assert.That(world.IsPresented("alight"), Is.False);
                    Assert.That(world.Current.GetComponentsInChildren<Collider>().All(c => !c.enabled), Is.True);
                }
                yield return null;
            }
            Assert.That(sawApproach, Is.True, "Arrival must show the platform approach before it stops.");
            Assert.That(world.Transitioning, Is.False, world.TransitionText);
            Assert.That(world.Location, Is.EqualTo("Leningradsky"));
            Assert.That(world.TravelSpeed, Is.Zero);
            Assert.That(world.Current.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(world.Current.GetComponentsInChildren<Collider>().Any(c => c.enabled), Is.True);
            Assert.That(world.IsPresented("alight"), Is.True);
            Assert.That(world.Player.transform.position.x, Is.EqualTo(before.x).Within(.01f));
            Assert.That(world.Player.transform.position.z, Is.EqualTo(before.z).Within(.01f));
            world.Player.Teleport(new Vector3(52, 1.32f, -4.5f)); world.Player.SetMenu(false);
            yield return new WaitForSeconds(.5f);
            Assert.That(world.Player.transform.position.y, Is.InRange(1.2f, 1.4f), "Restored platform must support the conductor.");
        }

        static IEnumerator WaitForJourney(ShiftEnvironment world)
        {
            float end = Time.realtimeSinceStartup + 20;
            while (world.Transitioning && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(world.Transitioning, Is.False, world.TransitionText);
        }
    }
}
