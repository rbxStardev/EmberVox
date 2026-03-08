using EmberVox.Core.Types;

namespace EmberVox.Engine.Components;

public record struct MeshComponent
{
    public VertexData[] Vertices;
    public uint[] Indices;

    public MeshComponent(VertexData[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
    }
}
