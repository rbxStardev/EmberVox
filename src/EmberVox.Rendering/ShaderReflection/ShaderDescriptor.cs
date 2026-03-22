using Silk.NET.Vulkan;

namespace EmberVox.Rendering.ShaderReflection;

public struct ShaderDescriptor
{
    public ShaderStageFlags StageFlags;
    public uint BindingIndex;
    public uint SetIndex;
    public uint Stride;
    public uint Offset;
    public DescriptorType BindingType;
    public string Name;

    public override string ToString()
    {
        return $"[{BindingType}] {Name}: stage={StageFlags}, binding={BindingIndex}, set={SetIndex}, stride={Stride}, offset={Offset},";
    }
}
