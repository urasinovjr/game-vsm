using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameVSM.Tests
{
    public sealed class TrainInformationTests
    {
        [UnityTest]
        public IEnumerator ImportedDemoTelemetryIsReplacedByTheCurrentTrip()
        {
            yield return SceneManager.LoadSceneAsync("Metallostroy"); yield return null;
            var world = Object.FindFirstObjectByType<ShiftEnvironment>();
            var texts = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include)
                .Where(t => t.name == "Информация о текущем рейсе").ToArray();
            Assert.That(texts.Length, Is.GreaterThan(10), "Apply environments to install TrainInformation.");
            Assert.That(texts.All(t=>t.text.Contains("САНКТ-ПЕТЕРБУРГ — МОСКВА")), Is.True);
            var imported = Object.FindFirstObjectByType<TrainVisibility>().GetComponentsInChildren<MeshRenderer>(true)
                .Where(r=>r.name.Contains("Info_ribbon_text")).ToArray();
            Assert.That(imported, Is.Not.Empty);
            Assert.That(imported.All(r=>!r.enabled), Is.True, "Old 400 km/h demonstration lettering must not remain visible.");
            world.Show("Leningradsky"); yield return null; yield return null;
            Assert.That(texts.All(t=>t.text.Contains("ПРИБЫЛИ В МОСКВУ")), Is.True);
            Assert.That(texts.All(t=>!t.text.Contains("400") && !t.text.Contains("14:35")), Is.True);
        }
    }
}
