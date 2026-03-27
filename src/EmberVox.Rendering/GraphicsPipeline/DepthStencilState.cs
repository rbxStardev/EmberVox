using Silk.NET.Vulkan;

namespace EmberVox.Rendering.GraphicsPipeline;

public struct DepthStencilState
{
    public bool DepthTestEnable;
    public bool DepthWriteEnable;
    public CompareOp DepthCompareOp;
    public bool DepthBoundsTestEnable;
    public bool StencilTestEnable;
}
