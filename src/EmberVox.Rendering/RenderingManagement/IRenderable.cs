using EmberVox.Rendering.ResourceManagement;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.RenderingManagement;

public interface IRenderable : IResource
{
    public Sampler Sampler { get; }
    public ImageView ImageView { get; }
}
