using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameVSM.Tests
{
    public sealed class EnvironmentTransitionTests
    {
        [UnityTest]
        public IEnumerator StationsReplaceDepotWithoutReplacingTrainAndKeepFeetOnPlatform()
        {
            Assert.That(Application.isPlaying, Is.True);
            yield return SceneManager.LoadSceneAsync("Metallostroy");
            yield return null;
            var world = Object.FindFirstObjectByType<ShiftEnvironment>();
            Assert.That(world, Is.Not.Null);
            var train = Object.FindFirstObjectByType<TrainVisibility>();
            var mechanisms = Object.FindObjectsByType<TrainMechanism>(FindObjectsInactive.Include);
            Assert.That(mechanisms.Length, Is.GreaterThan(400));
            foreach (string site in new[] { "Moskovsky", "Route", "Leningradsky", "Depot" })
            {
                var previous = world.Current;
                world.Show(site);
                yield return null;
                Assert.That(previous == null, Is.True, "Old environment must be destroyed.");
                Assert.That(world.Current.activeInHierarchy, Is.True);
                Assert.That(Object.FindFirstObjectByType<TrainVisibility>(), Is.SameAs(train));
                Assert.That(Object.FindObjectsByType<TrainMechanism>(FindObjectsInactive.Include), Is.EquivalentTo(mechanisms));
                world.Player.SetMenu(false);
                for (int i = 0; i < 25; i++) yield return null;
                float y = world.Player.transform.position.y;
                Assert.That(y, site == "Depot" ? Is.InRange(-.05f, .2f) : Is.InRange(1.2f, 1.4f), site + " ground collision");
            }
        }
    }
}
