using System.Diagnostics.CodeAnalysis;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.ShaderReflection;

public struct ShaderPushConstant
{
    public uint Size;
    public uint Offset;
    public uint AbsoluteOffset;
    public string Name;
    public ShaderStageFlags StageFlags;
}
