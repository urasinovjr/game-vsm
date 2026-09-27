using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GameVSM.Tests
{
    // Route rules for background life: stop order, bounded waiting, no lockstep, yielding.
    public sealed class WorkRouteTests
    {
        [Test]
        public void LoopingRouteWrapsAndOneWayRouteEnds()
        {
            Assert.AreEqual(1, WorkRoute.Next(0, 3, true));
            Assert.AreEqual(0, WorkRoute.Next(2, 3, true));
            Assert.AreEqual(-1, WorkRoute.Next(2, 3, false));
            Assert.AreEqual(-1, WorkRoute.Next(0, 0, true));
        }

        [Test]
        public void BlockedAgentGivesUpWithinBoundedTime()
        {
            foreach (float seed in new[] { 0f, .5f, .999f })
            {
                Assert.IsFalse(WorkRoute.GiveUp(1, seed));
                Assert.IsTrue(WorkRoute.GiveUp(WorkRoute.WaitTimeout * 1.25f, seed));
            }
        }

        [Test]
        public void NeighboursDoNotShareSeedsPausesOrPhases()
        {
            // Placements 1.3 m apart, like the platform queue.
            var seeds = Enumerable.Range(0, 8).Select(i => WorkRoute.Seed(new Vector3(49 + i * 1.3f, 1.3f, -4.4f))).ToArray();
            Assert.AreEqual(seeds.Length, seeds.Distinct().Count());
            Assert.AreEqual(seeds[3], WorkRoute.Seed(new Vector3(49 + 3 * 1.3f, 1.3f, -4.4f)), "Seed must be repeatable.");
            var pauses = seeds.Select(s => WorkRoute.Dwell(10, s, 0)).ToArray();
            Assert.Greater(pauses.Max() - pauses.Min(), 1.5f);
            Assert.IsTrue(pauses.All(p => p >= 7 && p <= 13));
            var phases = seeds.Select(s => WorkRoute.Phase(s, 0)).ToArray();
            Assert.Greater(phases.Max() - phases.Min(), .2f);
            // The same person does not repeat an identical pause on the next visit.
            Assert.AreNotEqual(WorkRoute.Dwell(10, seeds[0], 0), WorkRoute.Dwell(10, seeds[0], 1));
            Assert.AreEqual(0, WorkRoute.Dwell(0, seeds[0], 0));
        }

        [Test]
        public void OnlySomeoneInTheWalkingCorridorBlocks()
        {
            var self = new Vector3(0, 1.3f, 0); var east = Vector3.right;
            Assert.IsTrue(WorkRoute.Ahead(self, east, new Vector3(.8f, 1.3f, .2f), 1.1f, .55f));
            Assert.IsFalse(WorkRoute.Ahead(self, east, new Vector3(-.8f, 1.3f, 0), 1.1f, .55f), "behind");
            Assert.IsFalse(WorkRoute.Ahead(self, east, new Vector3(.8f, 1.3f, .9f), 1.1f, .55f), "beside");
            Assert.IsFalse(WorkRoute.Ahead(self, east, new Vector3(3, 1.3f, 0), 1.1f, .55f), "far");
            Assert.IsFalse(WorkRoute.Ahead(self, east, new Vector3(.8f, 0, 0), 1.1f, .55f), "other level");
        }

        [Test]
        public void TwoOncomingAgentsSideStepApart()
        {
            var a = new Vector3(0, 0, 0); var b = new Vector3(1, 0, 0);
            var aStep = WorkRoute.Aside(a, b - a, .8f); var bStep = WorkRoute.Aside(b, a - b, .8f);
            Assert.Greater(Mathf.Abs(aStep.z - bStep.z), 1.2f);
            Assert.IsFalse(WorkRoute.Ahead(aStep, b - a, bStep, 1.1f, .55f));
        }
    }
}
