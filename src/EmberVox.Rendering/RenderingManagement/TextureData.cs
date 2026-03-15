namespace EmberVox.Rendering.RenderingManagement;

public ref struct TextureData
{
    public uint Width { get; }
    public uint Height { get; }
    public ReadOnlySpan<byte> PixelData { get; }

    public TextureData(int width, int height, ReadOnlySpan<byte> pixelData)
    {
        Width = (uint)width;
        Height = (uint)height;
        PixelData = pixelData;
    }
}
