using EmberVox.Rendering.Contexts;

namespace EmberVox.Rendering.Types;

internal record struct MeshRenderInfo : IDisposable
{
    public readonly BufferContext VertexBuffer;
    public readonly BufferContext IndexBuffer;
    public readonly uint IndexCount;

    public MeshRenderInfo(BufferContext vertexBuffer, BufferContext indexBuffer, uint indexCount)
    {
        VertexBuffer = vertexBuffer;
        IndexBuffer = indexBuffer;
        IndexCount = indexCount;
    }

    public void Dispose()
    {
        IndexBuffer.Dispose();
        VertexBuffer.Dispose();

        GC.SuppressFinalize(this);
    }
}
