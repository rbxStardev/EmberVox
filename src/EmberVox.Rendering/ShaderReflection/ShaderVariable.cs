using Silk.NET.Vulkan;

namespace EmberVox.Rendering.ShaderReflection;

public struct ShaderVariable
{
    public uint Location;
    public Format Format;
    public uint Stride;

    public override string ToString()
    {
        return $"[ShaderVariable] location={Location} format={Format} stride={Stride}";
    }
}
