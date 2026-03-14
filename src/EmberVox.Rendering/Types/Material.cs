using EmberVox.Rendering.GraphicsPipeline;

namespace EmberVox.Rendering.Types;

public struct Material
{
    public Texture2D Texture;
    public GraphicsPipelineContext GraphicsPipelineContext;

    public Material(VulkanRenderer vulkanRenderer, string texturePath)
    {
        Texture = new Texture2D(
            vulkanRenderer.DeviceContext,
            vulkanRenderer.CommandContext,
            texturePath
        );
        GraphicsPipelineContext = new GraphicsPipelineContext(
            Texture,
            vulkanRenderer.DeviceContext,
            vulkanRenderer.SwapChainContext,
            vulkanRenderer.DepthContext,
            vulkanRenderer.UniformBuffers
        );
    }
}
