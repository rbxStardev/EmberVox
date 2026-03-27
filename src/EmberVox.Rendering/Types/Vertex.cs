using System.Numerics;

namespace EmberVox.Rendering.Types;

public record struct Vertex
{
    public Vector4 Color;
    public Vector3 Position;
    public Vector2 TexCoord;
}
