using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameVSM.Tests
{
    public sealed class SceneStartupTests
    {
        [UnityTest]
        public IEnumerator DepotStartsWithPlayerAndHudAndKeepsPlayerAboveFloor()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    Assert.Ignore("Save or discard open scene edits before running SceneStartupTests.");
            EditorSceneManager.OpenScene("Assets/GameVSM/Scenes/Metallostroy.unity");
            yield return new EnterPlayMode();
            for (int i = 0; i < 5; i++) yield return null;
            var player = Object.FindFirstObjectByType<FirstPersonController>();
            Assert.That(player, Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<Canvas>(), Is.Not.Null);
            Assert.That(player.MenuOpen, Is.True);
            player.SetMenu(false);
            for (int i = 0; i < 60; i++) yield return null;
            Assert.That(player.transform.position.y, Is.InRange(-.1f, .5f),
                "Player must remain on the depot floor after gravity starts.");
            yield return new ExitPlayMode();
        }
    }
}
