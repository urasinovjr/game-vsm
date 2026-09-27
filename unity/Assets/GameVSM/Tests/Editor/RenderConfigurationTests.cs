using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameVSM.Tests
{
    public sealed class RenderConfigurationTests
    {
        [Test]
        public void EditorAndEveryQualityLevelHavePersistentUrpAssets()
        {
            AssertPersistentUrp(GraphicsSettings.defaultRenderPipeline, "Graphics default");
            for (int i = 0; i < QualitySettings.names.Length; i++)
                AssertPersistentUrp(QualitySettings.GetRenderPipelineAssetAt(i), QualitySettings.names[i]);
        }

        static void AssertPersistentUrp(RenderPipelineAsset asset, string context)
        {
            Assert.That(asset, Is.Not.Null, context + " must render URP materials before Play Mode starts.");
            Assert.That(asset.GetType().Name, Is.EqualTo("UniversalRenderPipelineAsset"), context);
            Assert.That(EditorUtility.IsPersistent(asset), Is.True,
                context + " must not reference a temporary capture clone.");
        }
    }
}
