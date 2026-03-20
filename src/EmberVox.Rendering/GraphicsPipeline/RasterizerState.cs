using Silk.NET.Vulkan;

namespace EmberVox.Rendering.GraphicsPipeline;

public struct RasterizerState
{
    public PolygonMode PolygonMode;
    public FrontFace FrontFace;
    public CullModeFlags CullMode;
    public float LineWidth;
    public bool DepthClampEnable;
    public float DepthBiasClamp;
    public bool DepthBiasEnable;
    public float DepthBiasConstantFactor;
    public float DepthBiasSlopeFactor;
    public bool RasterizerDiscardEnable;
}
