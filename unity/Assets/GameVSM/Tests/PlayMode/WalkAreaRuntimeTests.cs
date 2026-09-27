using NUnit.Framework;
using UnityEngine;

namespace GameVSM.Tests
{
    public sealed class WalkAreaRuntimeTests
    {
        GameObject root;
        WalkArea area;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Isolated walk area test");
            root.transform.position = new Vector3(1000, 0, 1000);
            area = root.AddComponent<WalkArea>();
            area.Zones = new[] { Zone(new Vector3(0, -.3f, 0), new Vector3(10, .5f, 2)) };
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        static WalkArea.Zone Zone(Vector3 min, Vector3 max) => new() { Name = "Test surface", Min = min, Max = max };

        [Test] public void CrossingAnEdgeSlidesAlongItWithoutSteppingOntoTracks()
        {
            var feet = root.transform.TransformPoint(new Vector3(4, 0, 1.95f));
            var result = WalkArea.ConstrainStep(feet, new Vector3(.2f, 0, .2f));
            Assert.That(result, Is.EqualTo(new Vector3(.2f, 0, 0)));
            Assert.That(WalkArea.Allows(feet + result), Is.True);
        }
        [Test] public void ALongStepCannotJumpAcrossAnUnauthorisedGap()
        {
            area.Zones = new[] { Zone(new(0, -.3f, 0), new(1, .5f, 2)), Zone(new(1.2f, -.3f, 0), new(3, .5f, 2)) };
            var feet = root.transform.TransformPoint(new Vector3(.9f, 0, 1));
            Assert.That(WalkArea.ConstrainStep(feet, new Vector3(.7f, 0, 0)), Is.EqualTo(Vector3.zero));
        }
        [Test] public void RestoredPlayerOutsideZonesCanMoveBackTowardsThePath()
        {
            var feet = root.transform.TransformPoint(new Vector3(4, 0, 2.1f));
            var step = new Vector3(0, 0, -.2f);
            Assert.That(WalkArea.ConstrainStep(feet, step), Is.EqualTo(step));
        }
        [Test] public void WalkingSurfaceFollowsMovedAndRotatedScenery()
        {
            root.transform.rotation = Quaternion.Euler(0, 90, 0);
            Assert.That(area.Contains(root.transform.TransformPoint(new Vector3(5, 0, 1))), Is.True);
            Assert.That(area.Contains(root.transform.TransformPoint(new Vector3(5, 0, 3))), Is.False);
        }
        [Test] public void EmptyRouteAreaAllowsCabinButNotRoofOrRemoteTrack()
        {
            area.Zones = System.Array.Empty<WalkArea.Zone>();
            Assert.That(WalkArea.InsideTrain(new Vector3(47.5f, 1.31f, 0)), Is.True);
            Assert.That(WalkArea.InsideTrain(new Vector3(47.5f, 4.2f, 0)), Is.False);
            Assert.That(WalkArea.InsideTrain(new Vector3(150, 1.31f, 0)), Is.False);
            Assert.That(WalkArea.Allows(new Vector3(1000, 0, 1000)), Is.False);
        }

    }
}
