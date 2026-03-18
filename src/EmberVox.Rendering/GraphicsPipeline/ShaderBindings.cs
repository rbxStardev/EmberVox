using EmberVox.Core.Logging;

namespace EmberVox.Rendering.GraphicsPipeline;

public struct ShaderBindings
{
    public uint BindingIndex;
    public uint Size;
    public uint Offset;
    public uint SetIndex;
    public ShaderBindingType BindingType;

    public override string ToString()
    {
        return $"[{BindingType}] binding={BindingIndex} set={SetIndex} offset={Offset} size={Size}";
    }
}
