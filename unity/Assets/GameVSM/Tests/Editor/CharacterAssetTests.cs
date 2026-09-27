using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GameVSM.Tests
{
    public sealed class CharacterAssetTests
    {
        static readonly string[] RequiredClips = { "Idle", "Walk", "CarryWalk", "StartWalk", "StopWalk",
            "StopWalkRight", "TurnLeft", "TurnRight", "SitDown", "StandUp", "Seated", "Phone",
            "Talk", "Work", "UnwellSeated" };

        [Test]
        public void EveryPassengerHasCompleteSkeletonAndRenderableMaterials()
        {
            foreach (string name in new[] { "Passenger", "PassengerWoman", "DepotWorker", "PassengerWithBag" })
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameVSM/Art/Characters/" + name + ".prefab");
                Assert.That(prefab, Is.Not.Null, name);
                var animator = prefab.GetComponentInChildren<Animator>();
                var clips = animator.runtimeAnimatorController.animationClips.Select(c => c.name).ToArray();
                Assert.That(clips, Is.EquivalentTo(RequiredClips), name + " must have every authored action");
                Assert.That(prefab.GetComponentsInChildren<Animator>().Length, Is.EqualTo(2), name + " has two LOD skeletons");
                Assert.That(prefab.GetComponent<PassengerAnimation>(), Is.Not.Null, name + " has one playback owner");
                Assert.That(prefab.GetComponent<LODGroup>().lodCount, Is.EqualTo(2));
                foreach (var mesh in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    Assert.That(mesh.sharedMesh, Is.Not.Null);
                    Assert.That(mesh.bones, Has.None.Null);
                    foreach (var mat in mesh.sharedMaterials) Assert.That(mat.shader.isSupported, Is.True, name + " material");
                }
            }
            var withBag = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameVSM/Art/Characters/PassengerWithBag.prefab");
            var luggage = withBag.GetComponentInChildren<LuggageVisual>();
            Assert.That(luggage, Is.Not.Null);
            Assert.That(luggage.Kind, Is.EqualTo(LuggageKind.RollingCase));
            Assert.That(luggage.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null), Is.True);
            Assert.That(luggage.LocalBounds.Contains(luggage.Grip.localPosition), Is.True, "Grip belongs to the handle envelope.");
            var bones = withBag.GetComponentsInChildren<Transform>();
            Transform Bone(string suffix) => bones.First(b => b.name.EndsWith(":" + suffix) || b.name.EndsWith("_" + suffix));
            var shoulder = Bone("RightArm"); var elbow = Bone("RightForeArm"); var hand = Bone("RightHand");
            float reach = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position);
            Assert.That(Vector3.Distance(shoulder.position, luggage.Grip.position), Is.LessThan(reach - .02f),
                "The handle must be within the real arm reach in the standing pose.");
        }

        [Test]
        public void RollingCaseHandleCanBeHeldWithoutStretchingTheArm()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GameVSM/Art/Characters/PassengerWithBag.prefab");
            var specimen = Object.Instantiate(prefab);
            try
            {
                var animation = specimen.GetComponent<PassengerAnimation>();
                var luggage = specimen.GetComponentInChildren<LuggageVisual>();
                animation.Initialize();
                foreach (var animator in animation.Animators) animator.Update(.016f);
                animation.ReachRightPalm(luggage.Grip.position);
                Assert.That(Vector3.Distance(animation.RightPalm, luggage.Grip.position),
                    Is.LessThan(.025f), "The visible palm should meet the suitcase handle.");
                var bones = specimen.GetComponentsInChildren<Transform>();
                Transform Bone(string suffix) => bones.First(b => b.name.EndsWith(":" + suffix) || b.name.EndsWith("_" + suffix));
                var shoulder = Bone("RightArm"); var elbow = Bone("RightForeArm");
                var hand = Bone("RightHand"); var middle = Bone("RightHandMiddle1");
                Assert.That(elbow.position.y, Is.LessThan(shoulder.position.y), "The elbow must not rise above the shoulder.");
                Assert.That(elbow.position.y, Is.GreaterThan(hand.position.y), "The elbow must lead down to the handle.");
                Assert.That(Vector3.Dot((middle.position - hand.position).normalized, Vector3.down),
                    Is.GreaterThan(.9f), "The wrist must not turn the fingers sideways through the handle.");
            }
            finally { Object.DestroyImmediate(specimen); }
        }
    }
}
