using System.Numerics;

namespace EmberVox.Core.Types;

public record struct VertexData
{
    public Vector3 Position;
    public Vector4 Color;
    public Vector2 TexCoord;

    public VertexData(Vector3 position)
    {
        Position = position;
    }

    public VertexData(Vector4 color)
    {
        Color = color;
    }

    public VertexData(Vector2 texCoord)
    {
        TexCoord = texCoord;
    }

    public VertexData(Vector3 position, Vector2 texCoord)
    {
        Position = position;
        TexCoord = texCoord;
    }

    public VertexData(Vector3 position, Vector2 texCoord, Vector4 color)
    {
        Position = position;
        TexCoord = texCoord;
        Color = color;
    }
}
