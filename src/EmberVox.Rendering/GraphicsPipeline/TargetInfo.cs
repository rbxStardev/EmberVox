using System.Collections.Immutable;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.GraphicsPipeline;

public struct TargetInfo
{
    public ImmutableArray<ColorTargetDescription> ColorTargetDescriptions;
    public Format DepthAttachmentFormat;
}
