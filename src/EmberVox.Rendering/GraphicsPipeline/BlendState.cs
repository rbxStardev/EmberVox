using Silk.NET.Vulkan;

namespace EmberVox.Rendering.GraphicsPipeline;

public struct BlendState
{
    public ColorComponentFlags ColorWriteMask;
    public bool EnableBlend;
    public BlendOp ColorBlendOp;
    public BlendFactor SrcColorBlendFactor;
    public BlendFactor DstColorBlendFactor;
    public BlendOp AlphaBlendOp;
    public BlendFactor SrcAlphaBlendFactor;
    public BlendFactor DstAlphaBlendFactor;
}
