using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace EmberVox.Rendering.Contexts;

internal sealed class CommandContext : IDisposable
{
    public CommandPool CommandPool { get; }
    public CommandBuffer[] CommandBuffers { get; private set; }

    private readonly Vk _vk;
    private readonly DeviceContext _deviceContext;
    private readonly SwapChainContext _swapChainContext;
    private readonly GraphicsPipelineContext _graphicsPipelineContext;
    private readonly uint _maxFramesInFlight;

    public CommandContext(
        Vk vk,
        DeviceContext deviceContext,
        SwapChainContext swapChainContext,
        GraphicsPipelineContext graphicsPipelineContext,
        uint maxFramesInFlight
    )
    {
        _vk = vk;
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;
        _graphicsPipelineContext = graphicsPipelineContext;
        _maxFramesInFlight = maxFramesInFlight;

        CommandPool = CreateCommandPool();
        CommandBuffers = new CommandBuffer[_maxFramesInFlight];

        CreateCommandBuffers();
    }

    private void CreateCommandBuffers()
    {
        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = _maxFramesInFlight,
        };

        if (
            _vk.AllocateCommandBuffers(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<CommandBufferAllocateInfo>(ref allocInfo),
                new Span<CommandBuffer>(CommandBuffers)
            ) != Result.Success
        )
        {
            throw new Exception("Failed to allocate command buffers");
        }
    }

    public unsafe void RecordCommandBuffer(
        uint imageIndex,
        int currentFrame,
        VertexBuffer vertexBuffer
    )
    {
        CommandBuffer commandBuffer = CommandBuffers[currentFrame];

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _vk.BeginCommandBuffer(
            commandBuffer,
            new ReadOnlySpan<CommandBufferBeginInfo>(ref beginInfo)
        );

        TransitionImageLayout(
            commandBuffer,
            imageIndex,
            ImageLayout.Undefined,
            ImageLayout.ColorAttachmentOptimal,
            default,
            AccessFlags2.ColorAttachmentWriteBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            PipelineStageFlags2.ColorAttachmentOutputBit
        );

        ClearValue clearColor = new(new ClearColorValue(0.0f, 0.0f, 0.0f, 1.0f));
        RenderingAttachmentInfo attachmentInfo = new()
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = _swapChainContext.SwapChainImageViews[imageIndex],
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = clearColor,
        };

        RenderingInfo renderingInfo = new()
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new Rect2D()
            {
                Offset = new Offset2D(0, 0),
                Extent = _swapChainContext.SwapChainExtent,
            },
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &attachmentInfo,
        };

        _vk.CmdBeginRendering(commandBuffer, new ReadOnlySpan<RenderingInfo>(ref renderingInfo));

        _vk.CmdBindPipeline(
            commandBuffer,
            PipelineBindPoint.Graphics,
            _graphicsPipelineContext.GraphicsPipeline
        );

        Buffer buffer = vertexBuffer.Buffer;
        _vk.CmdBindVertexBuffers(
            commandBuffer,
            0,
            new ReadOnlySpan<Buffer>(ref buffer),
            new ReadOnlySpan<ulong>([0])
        );

        Viewport viewport = new(
            0.0f,
            0.0f,
            _swapChainContext.SwapChainExtent.Width,
            _swapChainContext.SwapChainExtent.Height,
            0.0f,
            1.0f
        );
        _vk.CmdSetViewport(commandBuffer, 0, new ReadOnlySpan<Viewport>(ref viewport));

        Rect2D scissor = new(new Offset2D(0, 0), _swapChainContext.SwapChainExtent);
        _vk.CmdSetScissor(commandBuffer, 0, new ReadOnlySpan<Rect2D>(ref scissor));

        _vk.CmdDraw(commandBuffer, 3, 1, 0, 0);

        _vk.CmdEndRendering(commandBuffer);

        TransitionImageLayout(
            commandBuffer,
            imageIndex,
            ImageLayout.ColorAttachmentOptimal,
            ImageLayout.PresentSrcKhr,
            AccessFlags2.ColorAttachmentWriteBit,
            default,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            PipelineStageFlags2.BottomOfPipeBit
        );

        _vk.EndCommandBuffer(commandBuffer);
    }

    private unsafe void TransitionImageLayout(
        CommandBuffer commandBuffer,
        uint imageIndex,
        ImageLayout oldLayout,
        ImageLayout newLayout,
        AccessFlags2 srcAccessMask,
        AccessFlags2 dstAccessMask,
        PipelineStageFlags2 srcStageMask,
        PipelineStageFlags2 dstStageMask
    )
    {
        ImageMemoryBarrier2 barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = srcStageMask,
            SrcAccessMask = srcAccessMask,
            DstStageMask = dstStageMask,
            DstAccessMask = dstAccessMask,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = _swapChainContext.SwapChainImages[imageIndex],
            SubresourceRange = new ImageSubresourceRange()
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        DependencyInfo dependencyInfo = new()
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &barrier,
        };

        _vk.CmdPipelineBarrier2(
            commandBuffer,
            new ReadOnlySpan<DependencyInfo>(ref dependencyInfo)
        );
    }

    private CommandPool CreateCommandPool()
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = _deviceContext.GraphicsQueue.Index,
        };

        CommandPool commandPool = default;
        if (
            _vk.CreateCommandPool(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<CommandPoolCreateInfo>(ref poolInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<CommandPool>(ref commandPool)
            ) != Result.Success
        )
        {
            throw new Exception("Failed to create command pool");
        }

        return commandPool;
    }

    public void Dispose()
    {
        _vk.DestroyCommandPool(
            _deviceContext.LogicalDevice,
            CommandPool,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );

        GC.SuppressFinalize(this);
    }
}
