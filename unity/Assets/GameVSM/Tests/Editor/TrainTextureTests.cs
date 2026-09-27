using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameVSM.Tests
{
    public sealed class TrainTextureTests
    {
        [Test]
        public void ImportedCarpetRetainsItsSourceImageResolution()
        {
            var textures = AssetDatabase.LoadAllAssetsAtPath("Assets/GameVSM/Art/Train/WhiteKrechet.glb").OfType<Texture2D>().ToArray();
            var carpet = textures.Single(t => t.name == "carpet_chevron");
            Assert.That(carpet.width, Is.EqualTo(1024), "A 4x4 import placeholder previously produced rainbow patches.");
            Assert.That(carpet.height, Is.EqualTo(1024));
            foreach (var texture in textures)
                Assert.That(Mathf.Min(texture.width, texture.height), Is.GreaterThan(4), texture.name);
        }
    }
}
