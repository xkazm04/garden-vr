using NUnit.Framework;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium.Tests
{
    public class UrpMsaaTests
    {
        // T-TER-042: GardenURP had m_MSAA 1, so alpha to coverage did nothing at runtime. The capture CLI renders to its
        // own 8x target, which hid it. 4x is the Quest-safe value (Meta calls 4x MSAA cheap on the Quest GPU).
        // Read by reflection: this test assembly does not reference the URP runtime.
        [Test]
        public void ActivePipelineAsset_Has4xMsaa_SoAlphaToCoverageWorksAtRuntime()
        {
            RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
            Assert.IsNotNull(asset, "the project must run the GardenURP asset");
            var property = asset.GetType().GetProperty("msaaSampleCount");
            Assert.IsNotNull(property, "URP asset has msaaSampleCount");
            Assert.AreEqual(4, (int)property.GetValue(asset));
        }
    }
}
