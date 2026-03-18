namespace EmberVox.Rendering.GraphicsPipeline;

public struct ShaderVariable
{
    public uint Location;
    public uint Format;
    public uint Size;

    public override string ToString()
    {
        return $"[ShaderVariable] location={Location} format={Format} size={Size}";
    }
}
