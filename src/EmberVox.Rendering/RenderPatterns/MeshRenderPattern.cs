using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.Types;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace EmberVox.Rendering.RenderPatterns;

internal record struct MeshRenderPattern : IRenderPattern
{
    public MeshRenderInfo MeshRenderInfo;

    public MeshRenderPattern(MeshRenderInfo meshRenderInfo)
    {
        MeshRenderInfo = meshRenderInfo;
    }

    public void Render(Vk vk, CommandBuffer commandBuffer, Pipeline pipeline)
    {
        vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, pipeline);
        //Console.WriteLine($"Drawing frame, imageIndex: {imageIndex}, currentFrame: {currentFrame}");

        Buffer vertexBuffer = MeshRenderInfo.VertexBuffer.Buffer;
        vk.CmdBindVertexBuffers(
            commandBuffer,
            0,
            new ReadOnlySpan<Buffer>(ref vertexBuffer),
            new ReadOnlySpan<ulong>([0])
        );

        Buffer indexBuffer = MeshRenderInfo.IndexBuffer.Buffer;
        vk.CmdBindIndexBuffer(commandBuffer, indexBuffer, 0, IndexType.Uint32);

        vk.CmdDrawIndexed(commandBuffer, MeshRenderInfo.IndexCount, 1, 0, 0, 0);
    }
}
