using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameVSM.Tests
{
    public sealed class WalkAreaTests
    {
        [TestCase("Moskovsky")]
        [TestCase("Leningradsky")]
        public void BuiltStationConnectsDoorPlatformAndConcourse(string site)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameVSM/Resources/Environments/" + site + ".prefab");
            var zone = prefab.GetComponent<WalkArea>();
            Assert.That(zone, Is.Not.Null, "Rebuild location prefabs.");
            var path = new[] { new Vector3(47.5f,1.3f,-1.5f), new Vector3(47.5f,1.3f,-5), new Vector3(-132,1.3f,-5), new Vector3(-140,1.3f,-6) };
            for (int i=1;i<path.Length;i++)
                for (float t=0;t<=1;t+=.01f)
                    Assert.That(zone.Contains(Vector3.Lerp(path[i-1],path[i],t)), Is.True, site + " path " + i + " at " + t);
            Assert.That(zone.Contains(new Vector3(60,1.3f,0)), Is.False, "Track itself must not be an outdoor walking zone.");
            Assert.That(zone.Contains(new Vector3(150,1.3f,-6)), Is.False, "Beyond platform end.");
        }
    }
}
