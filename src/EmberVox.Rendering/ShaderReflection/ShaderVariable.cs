using Silk.NET.Vulkan;

namespace EmberVox.Rendering.ShaderReflection;

public struct ShaderVariable
{
    public uint Location;
    public Format Format;
    public uint Stride;
    public string Name;

    public override string ToString()
    {
        return $"{Name}: location={Location}, format={Format}, stride={Stride}";
    }
}
