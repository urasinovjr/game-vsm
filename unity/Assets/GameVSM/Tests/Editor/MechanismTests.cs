using NUnit.Framework;
using UnityEngine;

namespace GameVSM.Tests
{
    public sealed class MechanismTests
    {
        GameObject root;
        TrainMechanism mechanism;

        [SetUp]
        public void SetUp()
        {
            root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.transform.position = new Vector3(0, 2, 0);
            mechanism = root.AddComponent<TrainMechanism>();
            mechanism.Kind = "sliding_entry_door";
            mechanism.Plug = new Vector3(0, 0, -.2f);
            mechanism.Slide = new Vector3(1, 0, 0);
            // EditMode does not run the normal scene lifecycle.
            typeof(TrainMechanism).GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(mechanism, null);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void PlugClearsTheBodyBeforeTheLeafSlides()
        {
            mechanism.Toggle();
            mechanism.Advance(.25f / 1.4f, new Vector3(20, 0, 0), .2f);
            Assert.That(root.transform.position.x, Is.EqualTo(0).Within(.0001f));
            Assert.That(root.transform.position.z, Is.EqualTo(-.2f).Within(.0001f));
            mechanism.Advance(1, new Vector3(20, 0, 0), .2f);
            Assert.That(root.transform.position.x, Is.EqualTo(1).Within(.0001f));
        }

        [Test]
        public void EntryDoorCannotBeOpenedWhileTrainMoves()
        {
            mechanism.TrainMoving = true;
            Assert.That(mechanism.Toggle(), Is.False);
            Assert.That(mechanism.IsOpen, Is.False);
        }

        [Test]
        public void DoorStopsBeforeItSweepsIntoThePlayer()
        {
            mechanism.Toggle();
            mechanism.Advance(1, new Vector3(1, 1.3f, -.2f), .2f);
            Assert.That(mechanism.Blocked, Is.True);
            Assert.That(mechanism.Phase, Is.LessThan(1));
            Assert.That(mechanism.WorldBounds().max.x, Is.LessThanOrEqualTo(.8f));
        }
    }
}
