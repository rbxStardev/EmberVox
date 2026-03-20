using Silk.NET.Vulkan;

namespace EmberVox.Rendering.ShaderReflection;

public struct ShaderBindings
{
    public uint BindingIndex;
    public uint Stride;
    public uint Offset;
    public uint SetIndex;
    public DescriptorType BindingType;

    public override string ToString()
    {
        return $"[{BindingType}] binding={BindingIndex} set={SetIndex} offset={Offset} size={Stride}";
    }
}
