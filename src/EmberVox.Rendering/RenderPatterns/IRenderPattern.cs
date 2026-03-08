using Silk.NET.Vulkan;

namespace EmberVox.Rendering.RenderPatterns;

public interface IRenderPattern
{
    public void Render(Vk vk, CommandBuffer commandBuffer, Pipeline pipeline);
}
