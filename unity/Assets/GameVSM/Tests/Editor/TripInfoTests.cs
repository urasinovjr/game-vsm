using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GameVSM.Tests
{
    public sealed class TripInfoTests
    {
        static string Flat(string text) => text.Replace('\n', ' ');

        [TestCase("alight"), TestCase("final_check"), TestCase("handover"), TestCase("complete"), TestCase("")]
        public void MoscowReportsArrivalFromPetersburgAndNeverADeparture(string node)
        {
            Assert.That(node == "" || ShiftEnvironment.ForNode(node) == "Leningradsky");
            foreach (var kind in new[] { SignKind.Board, SignKind.Stela })
            {
                string text = Flat(TripInfo.Text(kind, "Leningradsky", node));
                StringAssert.Contains("ПРИБЫЛ ИЗ САНКТ-ПЕТЕРБУРГА", text);
                StringAssert.DoesNotContain("ОТПРАВ", text);
                StringAssert.DoesNotContain("ПОСАДКА", text);
                StringAssert.DoesNotContain("САНКТ-ПЕТЕРБУРГ", text.Replace("ИЗ САНКТ-ПЕТЕРБУРГА", ""), "Petersburg only as origin");
            }
        }

        [Test]
        public void PetersburgBoardFollowsBoardingThenDeparture()
        {
            string boarding = Flat(TripInfo.Text(SignKind.Board, "Moskovsky", "boarding1"));
            string departing = Flat(TripInfo.Text(SignKind.Board, "Moskovsky", "depart"));
            StringAssert.Contains("МОСКВА  ·  ПОСАДКА", boarding);
            StringAssert.Contains("МОСКВА  ·  ОТПРАВЛЕНИЕ", departing);
            StringAssert.Contains("ПУТЬ 4", departing);
            StringAssert.DoesNotContain("ПРИБЫЛ", boarding);
        }

        [Test]
        public void TrackPlatformAndCarriageAreDistinctAndAgree()
        {
            Assert.That((TripInfo.Petersburg.Track, TripInfo.Petersburg.Platform), Is.EqualTo((4, 4)));
            Assert.That((TripInfo.Moscow.Track, TripInfo.Moscow.Platform), Is.EqualTo((3, 2)));
            foreach (var stop in new[] { TripInfo.Petersburg, TripInfo.Moscow })
            {
                StringAssert.Contains("ПУТЬ " + stop.Track, TripInfo.Text(SignKind.Board, stop.Location, ""));
                Assert.That(TripInfo.Text(SignKind.Carriage, stop.Location, ""), Is.EqualTo("ВАГОН 3"));
                // Our platform lies between our track (Z = 0) and the next one (Z = -12).
                string front = TripInfo.PlatformLabel(stop, -6, false), back = TripInfo.PlatformLabel(stop, -6, true);
                StringAssert.Contains("ПЛАТФОРМА " + stop.Platform, front);
                StringAssert.EndsWith("ПУТЬ " + stop.Track, front);
                StringAssert.StartsWith("ПУТЬ " + stop.Track, back);
                var carriage = stop.Signs.Single(s => s.Kind == SignKind.Carriage).Position;
                Assert.That(Mathf.Abs(carriage.x - TripInfo.CarriageDoor.x), Is.LessThan(2.5f), "Car sign stands at the car 3 door");
            }
            Assert.That(TripInfo.Petersburg.TrackAt(-12), Is.EqualTo(5));
            Assert.That(TripInfo.Moscow.TrackAt(-12), Is.EqualTo(2));
        }

        [Test]
        public void ExitIsNamedOnlyAtTheExitGroupAndOnTheWayToIt()
        {
            foreach (var stop in new[] { TripInfo.Petersburg, TripInfo.Moscow })
            {
                Assert.That(stop.ExitMentions, Is.InRange(2, 6));
                var portal = stop.Signs.Single(s => s.Kind == SignKind.ExitPortal).Position;
                Assert.That(Mathf.Abs(portal.z - stop.ExitGroup.z), Is.LessThan(.01f));
                Assert.That(portal.x - stop.ExitGroup.x, Is.InRange(0f, 2.5f), "Label is mounted on the exit portal");
                // Arrows point straight ahead (-X), so every pointer must stand on our platform beyond the exit.
                foreach (var s in stop.Signs.Where(s => s.Kind == SignKind.Exit || s.Exit))
                {
                    Assert.That(s.Position.x, Is.GreaterThan(stop.ExitGroup.x + 10));
                    Assert.That(s.Position.z, Is.InRange(-10.15f, -1.85f));
                }
            }
        }

        [Test]
        public void BuiltLeningradskyPrefabShowsTheArrivalModel()
        {
            var prefab = Resources.Load<GameObject>("Environments/Leningradsky");
            if (prefab == null || prefab.GetComponentInChildren<TripBoard>(true) == null)
                Assert.Inconclusive("Rebuild location prefabs: GameVSM/Art/Build location prefabs.");
            var texts = prefab.GetComponentsInChildren<TextMesh>(true).Select(t => Flat(t.text)).ToArray();
            Assert.That(texts.Count(t => t.Contains("ВЫХОД")), Is.InRange(1, 6));
            Assert.That(texts.Where(t => t.Contains("ОТПРАВ")), Is.Empty);
            Assert.That(texts.Where(t => t.Replace("ИЗ САНКТ-ПЕТЕРБУРГА", "").Contains("САНКТ-ПЕТЕРБУРГ")), Is.Empty);
        }
    }
}
