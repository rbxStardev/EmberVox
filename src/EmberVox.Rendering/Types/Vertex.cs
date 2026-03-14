using System.Numerics;

namespace EmberVox.Rendering.Types;

public record struct Vertex
{
    public Vector3 Position;
    public Vector4 Color;
    public Vector2 TexCoord;
}
