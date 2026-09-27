using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameVSM.Tests
{
    public sealed class PassengerTransitionTests
    {
        [UnityTest]
        public IEnumerator ReachingASeatDoesNotSnapIntoTheSeatedPose()
        {
            yield return SceneManager.LoadSceneAsync("Metallostroy");
            yield return null;
            var experience = Object.FindAnyObjectByType<ShiftExperience>();
            var seat = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .First(t => t.name.Contains("WalkSeat_012.") && t.position.x > 24.9f && t.position.x < 48.9f);
            var specimen = Object.Instantiate(experience.PassengerPrefab, seat.position, Quaternion.identity);
            try
            {
                var passenger = specimen.AddComponent<ShiftPassenger>();
                passenger.Seat = seat.position;
                passenger.SeatYaw = 270;
                passenger.StandAt(seat.position, 270);
                passenger.Walk(new[] { seat.position }, true);
                Assert.That(passenger.Seated, Is.False, "Boarding starts in a standing pose.");
                yield return null;
                Assert.That(passenger.Seated, Is.False, "A single frame must not complete a physical sit-down.");

                float started = Time.time;
                while (passenger.Travelling && Time.time - started < 6) yield return null;
                Assert.That(passenger.Travelling, Is.False, "Seat transition should finish.");
                Assert.That(passenger.Seated, Is.True);
                Assert.That(passenger.Luggage.gameObject.activeSelf, Is.True, "The bag remains visible after stowing.");

                var forward = Quaternion.Euler(0, passenger.SeatYaw, 0) * Vector3.forward;
                passenger.Walk(new[] { seat.position + forward * .8f }, false);
                yield return null;
                Assert.That(passenger.Travelling, Is.True, "Standing and retrieving are part of the route.");
                Assert.That(passenger.Seated, Is.True, "Standing must not complete in one frame.");
                started = Time.time;
                while (passenger.Travelling && Time.time - started < 6) yield return null;
                Assert.That(passenger.Travelling, Is.False, "Exit transition should finish.");
                Assert.That(passenger.Seated, Is.False);
                Assert.That(passenger.Luggage.gameObject.activeSelf, Is.True, "The retrieved bag stays visible.");
            }
            finally { Object.Destroy(specimen); }
        }
    }
}
