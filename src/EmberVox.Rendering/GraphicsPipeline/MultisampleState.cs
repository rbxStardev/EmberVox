using Silk.NET.Vulkan;

namespace EmberVox.Rendering.GraphicsPipeline;

public struct MultisampleState
{
    public SampleCountFlags RasterizationSamples;
    public bool SampleShadingEnable;
}
