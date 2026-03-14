using EmberVox.Rendering.GraphicsPipeline;
using EmberVox.Rendering.RenderingManagement;

namespace EmberVox.Rendering.Types;

public struct Material
{
    public IRenderable Renderable;
    public GraphicsPipelineContext GraphicsPipelineContext;

    public Material(VulkanRenderer vulkanRenderer, IRenderable renderable)
    {
        Renderable = renderable;
        GraphicsPipelineContext = new GraphicsPipelineContext(
            Renderable,
            vulkanRenderer.DeviceContext,
            vulkanRenderer.SwapChainContext,
            vulkanRenderer.DepthContext,
            vulkanRenderer.UniformBuffers
        );
    }
}
