using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameVSM.Tests
{
    public sealed class ShiftNetworkTests
    {
        [UnityTest, Explicit("Requires isolated regression server on 8011.")]
        public IEnumerator CannotReceiveBriefingFromAcrossTheDepot()
        {
            yield return SceneManager.LoadSceneAsync("Metallostroy");
            yield return null;
            var client = Object.FindAnyObjectByType<ShiftClient>();
            client.SessionFileName = "regression-gate.json";
            client.Connect("http://127.0.0.1:8011", false);
            yield return Wait(client);
            client.GetComponent<ShiftEnvironment>().Player.Teleport(new Vector3(95, .06f, -5));
            int revision = (int)client.Attempt["revision"];
            client.Choose("receive");
            while (client.Busy) yield return null;
            Assert.That((string)client.Attempt["state"]["node"], Is.EqualTo("brief"), "A remote menu click completed a physical briefing.");
            Assert.That((int)client.Attempt["revision"], Is.EqualTo(revision));
        }

        // Uses a separately started disposable FastAPI database, never the user's game.
        [UnityTest, Explicit("Start the isolated art-validation server on 8011 first.")]
        public IEnumerator FullShiftUsesBothStationsAndCompletesOnServer()
        {
            string save = Path.Combine(Application.persistentDataPath, "shift-art-validation.json");
            string previous = File.Exists(save) ? File.ReadAllText(save) : null;
            Assert.That(Application.isPlaying, Is.True);
            yield return SceneManager.LoadSceneAsync("Metallostroy");
            try
            {
                yield return null;
                var client = Object.FindAnyObjectByType<ShiftClient>();
                client.SessionFileName = "shift-art-validation.json";
                var environment = client.GetComponent<ShiftEnvironment>();
                client.Connect("http://127.0.0.1:8011", false);
                yield return Wait(client);
                bool petersburg = false, moscow = false, route = false;
                var experience = client.GetComponent<ShiftExperience>();
                Assert.That(experience.Passengers.Count, Is.EqualTo(8));
                Time.timeScale = 5;
                int count = 0;
                while (client.Attempt["task"] is Newtonsoft.Json.Linq.JObject && count++ < 40)
                {
                    float motionEnd = Time.realtimeSinceStartup + 30;
                    while (environment.Transitioning && Time.realtimeSinceStartup < motionEnd) yield return null;
                    Assert.That(environment.Transitioning, Is.False, environment.TransitionText);
                    var task = client.Attempt["task"];
                    string originalNode = experience.Node;
                    if (originalNode == "service_request")
                        Assert.That(Vector3.Distance(experience.Luggage.position, new Vector3(46.4f, 1.3f, -.22f)),
                            Is.LessThan(.1f), "The new passenger's suitcase must start on the aisle, not fly back from the earlier inspection shelf.");
                    yield return Approach(experience, environment.Player);
                    if ((string)client.Attempt["state"]["node"] == "depart")
                        Assert.That(experience.Passengers.All(p => p.Seated), Is.True, "Departure must wait for boarding.");
                    if (experience.Node == originalNode)
                    {
                        experience.Perform((string)task["choices"][0]["id"], (int)task["wait"] > 0);
                        while (experience.Acting) yield return null;
                    }
                    yield return Wait(client);
                    yield return null;
                    Assert.That(experience.Node, Is.Not.EqualTo(originalNode),
                        originalNode + " did not advance. Feedback: " + experience.Feedback +
                        "; client: " + client.Error + "; player: " + environment.Player.transform.position +
                        "; luggage: " + experience.Luggage.position);
                    petersburg |= environment.Location == "Moskovsky";
                    moscow |= environment.Location == "Leningradsky";
                    route |= environment.Location == "Route";
                    Assert.That(environment.Current, Is.Not.Null);
                }
                Assert.That(petersburg && moscow && route, Is.True);
                Assert.That((string)client.Attempt["state"]["status"], Is.EqualTo("complete"));
                Assert.That((int)client.Attempt["state"]["quality"], Is.EqualTo(100));
                Assert.That((int)client.Attempt["state"]["trust"], Is.EqualTo(100));
            }
            finally
            {
                Time.timeScale = 1;
                if (previous == null) { if (File.Exists(save)) File.Delete(save); }
                else File.WriteAllText(save, previous);
            }
        }

        static IEnumerator Approach(ShiftExperience experience, FirstPersonController player)
        {
            // This tests all spatial targets and real NPC progression. Locomotion along
            // stairs/doorways is covered separately, not claimed by these teleports.
            float end = Time.realtimeSinceStartup + 40;
            // Walkers never pass through the player: wait in the car 3 vestibule, off the door and aisle lines.
            if (experience.WaitingForPeople) { player.Teleport(new Vector3(48.4f, 1.31f, .9f)); yield return null; }
            while (experience.WaitingForPeople && Time.realtimeSinceStartup < end) yield return null;
            string node = experience.Node;
            Vector3 target = experience.TargetPosition;
            Vector3 feet = new Vector3(target.x+.9f,1.31f,-.24f);
            if (node == "brief") feet = target + new Vector3(-1.2f,-1.2f,0);
            if (node.StartsWith("boarding")) feet = target + new Vector3(-.9f,-1.2f,-.65f);
            if (node is "platform" or "alight") feet = new Vector3(47.5f,1.31f,-3.5f);
            player.Teleport(feet); player.SetMenu(false);
            yield return null;
            player.View.transform.LookAt(experience.TargetPosition);
            Assert.That(experience.Interact(), Is.True, "Cannot reach interaction: " + node + " from " + feet + " toward " + experience.TargetPosition);
            while (experience.Acting || experience.GetComponent<ShiftClient>().Busy) yield return null;
            if (experience.Node == node)
                Assert.That(experience.CanChoose(out var reason), Is.True, node + ": " + reason);
        }

        static IEnumerator Wait(ShiftClient client)
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (client.Busy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(client.Busy, Is.False, "Network request timed out.");
            Assert.That(client.Error, Is.Empty);
            Assert.That(client.Attempt, Is.Not.Null);
        }
    }
}
