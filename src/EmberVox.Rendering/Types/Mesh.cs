using EmberVox.Core.Extensions;
using EmberVox.Rendering.Buffers;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.ResourceManagement;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Types;

public class Mesh : IResource
{
    public BufferContext VertexBuffer { get; init; }
    public BufferContext IndexBuffer { get; init; }
    public uint IndexCount { get; init; }

    public Mesh(
        DeviceContext deviceContext,
        CommandContext commandContext,
        ReadOnlySpan<Vertex> vertices,
        ReadOnlySpan<uint> indices
    )
    {
        // Vertex
        ulong vertexBufferSize = (ulong)vertices.AsBytes().Length;

        BufferContext stagingBuffer = new(
            deviceContext,
            vertexBufferSize,
            BufferUsageFlags.TransferSrcBit
        );
        vertices.AsBytes().CopyTo(stagingBuffer.MappedMemory);

        VertexBuffer = new BufferContext(
            deviceContext,
            vertexBufferSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit
        );

        commandContext.CopyBuffer(stagingBuffer, VertexBuffer, vertexBufferSize);
        stagingBuffer.Dispose();

        // Indices

        ulong indexBufferSize = (ulong)indices.AsBytes().Length;

        stagingBuffer = new BufferContext(
            deviceContext,
            indexBufferSize,
            BufferUsageFlags.TransferSrcBit
        );
        indices.AsBytes().CopyTo(stagingBuffer.MappedMemory);

        IndexBuffer = new BufferContext(
            deviceContext,
            indexBufferSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit
        );

        commandContext.CopyBuffer(stagingBuffer, IndexBuffer, indexBufferSize);
        stagingBuffer.Dispose();

        IndexCount = (uint)indices.Length;
    }

    public void Dispose()
    {
        IndexBuffer.Dispose();
        VertexBuffer.Dispose();

        GC.SuppressFinalize(this);
    }
}
