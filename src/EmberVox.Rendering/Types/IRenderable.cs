using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Types;

public interface IRenderable
{
    public Sampler Sampler { get; }
    public ImageView ImageView { get; }
}
