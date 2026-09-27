using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameVSM.Tests
{
    // Invariants of the generated Metallostroy prefab; rebuild it first with
    // GameVSM/Art/Build location prefabs. Opens an empty scene so the player and train do not interfere;
    // skipped when an open scene has unsaved edits, and the previous scene layout is reopened afterwards.
    public sealed class DepotLayoutTests
    {
        const string DepotPath = "Assets/GameVSM/Resources/Environments/Depot.prefab";
        // Staff route: wicket → painted path → crossing of track 1 → start and briefing → hall walkway.
        static readonly Vector3[] Route =
        {
            new(157, 0, -13.3f), new(130, 0, -13.3f), new(130, 0, -5.7f), new(128, 0, -5.7f),
            new(118, 0, -5.7f), new(49.5f, 0, -5.7f),
        };
        GameObject depot;
        SceneSetup[] previous;
        bool replaced;

        [OneTimeSetUp]
        public void Load()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    Assert.Ignore("Save or discard open scene edits before running DepotLayoutTests.");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(DepotPath);
            Assert.That(asset, Is.Not.Null, "Build location prefabs first.");
            previous = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            replaced = true;
            depot = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            Physics.SyncTransforms();
        }

        [OneTimeTearDown]
        public void Unload()
        {
            if (depot != null) Object.DestroyImmediate(depot);
            // Untitled scenes cannot be reopened; saved ones come back exactly as they were on disk.
            if (replaced && previous != null && previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
        }

        [Test]
        public void OrdinaryBuildKeepsDepotSign()
        {
            var sign = depot.GetComponentsInChildren<TextMesh>().FirstOrDefault(t => t.name == "Вывеска депо");
            Assert.That(sign, Is.Not.Null);
            Assert.That(sign.text, Does.Contain("МЕТАЛЛОСТРОЙ"));
            Assert.That(sign.GetComponent<MeshRenderer>().sharedMaterial, Is.Not.Null);
        }

        [Test]
        public void StaffRouteFromWicketToHallIsWalkable()
        {
            for (int i = 1; i < Route.Length; i++)
                for (float t = 0; t <= 1; t += .02f)
                {
                    var p = Vector3.Lerp(Route[i - 1], Route[i], t);
                    var blockers = Physics.OverlapCapsule(p + Vector3.up * .55f, p + Vector3.up * 1.6f, .25f)
                        .Where(c => c.GetComponentInParent<PassengerMotion>() == null).Select(c => c.name).ToArray();
                    Assert.That(blockers, Is.Empty, $"Route blocked at {p}");
                    Assert.That(Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 3), Is.True, $"No floor at {p}");
                    Assert.That(hit.point.y, Is.InRange(-.16f, .21f), $"Floor height at {p}");
                }
        }

        [Test]
        public void PerimeterIsClosedExceptWicketAndRailGates()
        {
            for (float z = -76; z < 108; z += 3.1f)
            {
                bool open = z is > -14.2f and < -12.4f || z is > -11 and < 11;
                bool blocked = Physics.Raycast(new Vector3(150, 1.2f, z), Vector3.right, 15);
                Assert.That(blocked, Is.EqualTo(!open), $"East fence at z {z}");
            }
            for (float x = -155; x < 155; x += 7.3f)
                Assert.That(Physics.Raycast(new Vector3(x, 1.2f, -70), Vector3.back, 15), Is.True, $"South fence at x {x}");
        }

        [Test]
        public void VisibleObstaclesHaveColliders()
        {
            foreach (string name in new[] { "Стеновая стойка", "Простенок ворот", "Электрошкаф", "Опора освещения", "Столб ворот", "Опора настила", "Коллизия ограждения", "Коллизия перил" })
            {
                var found = depot.GetComponentsInChildren<Transform>().Where(t => t.name == name).ToArray();
                Assert.That(found, Is.Not.Empty, name);
                Assert.That(found.All(t => t.GetComponent<Collider>() != null), Is.True, name);
            }
        }

        [Test]
        public void NothingIsBuriedBelowTheTerritory()
        {
            var buried = depot.GetComponentsInChildren<Renderer>().Where(r => r.bounds.max.y < -.2f).Select(r => r.name).ToArray();
            Assert.That(buried, Is.Empty);
        }
    }
}
