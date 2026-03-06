using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace EmberVox.Rendering.Contexts;

internal sealed class CommandContext : IDisposable
{
    public CommandPool MainCommandPool { get; }
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
        DescriptorContext descriptorContext,
        uint maxFramesInFlight
    )
    {
        _vk = vk;
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;
        _graphicsPipelineContext = graphicsPipelineContext;
        _maxFramesInFlight = maxFramesInFlight;

        MainCommandPool = CreateCommandPool(_deviceContext.GraphicsQueue.Index);
        CommandBuffers = new CommandBuffer[_maxFramesInFlight];

        CreateCommandBuffers(MainCommandPool, _maxFramesInFlight, CommandBuffers);
    }

    public void Dispose()
    {
        _vk.DestroyCommandPool(
            _deviceContext.LogicalDevice,
            MainCommandPool,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );

        GC.SuppressFinalize(this);
    }

    public unsafe void RecordCommandBuffer(
        DescriptorContext descriptorContext,
        uint imageIndex,
        int currentFrame,
        BufferContext vertexBuffer,
        BufferContext indexBuffer
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

        ClearValue clearColor = new(new ClearColorValue(255.0f, 0.0f, 255.0f, 1.0f));
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

        Buffer vertexBufferBuffer = vertexBuffer.Buffer;
        _vk.CmdBindVertexBuffers(
            commandBuffer,
            0,
            new ReadOnlySpan<Buffer>(ref vertexBufferBuffer),
            new ReadOnlySpan<ulong>([0])
        );

        Buffer indexBufferBuffer = indexBuffer.Buffer;
        _vk.CmdBindIndexBuffer(commandBuffer, indexBufferBuffer, 0, IndexType.Uint32);

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

        DescriptorSet descriptorSet = descriptorContext[currentFrame];
        _vk.CmdBindDescriptorSets(
            commandBuffer,
            PipelineBindPoint.Graphics,
            _graphicsPipelineContext.PipelineLayout,
            0,
            new ReadOnlySpan<DescriptorSet>(ref descriptorSet),
            ReadOnlySpan<uint>.Empty
        );
        _vk.CmdDrawIndexed(commandBuffer, 6, 1, 0, 0, 0);

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

    public unsafe void CopyBuffer(BufferContext srcBuffer, BufferContext dstBuffer, ulong size)
    {
        CommandPool commandCopyPool = CreateCommandPool(
            _deviceContext.GraphicsQueue.Index,
            CommandPoolCreateFlags.TransientBit
        );

        CommandBuffer commandCopyBuffer = default;
        CreateCommandBuffers(commandCopyPool, 1, new Span<CommandBuffer>(ref commandCopyBuffer));

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _vk.BeginCommandBuffer(
            commandCopyBuffer,
            new ReadOnlySpan<CommandBufferBeginInfo>(ref beginInfo)
        );

        BufferCopy copy = new() { Size = size };
        _vk.CmdCopyBuffer(
            commandCopyBuffer,
            srcBuffer.Buffer,
            dstBuffer.Buffer,
            new ReadOnlySpan<BufferCopy>(ref copy)
        );

        _vk.EndCommandBuffer(commandCopyBuffer);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandCopyBuffer,
        };
        _vk.QueueSubmit(
            _deviceContext.GraphicsQueue.Queue,
            new ReadOnlySpan<SubmitInfo>(ref submitInfo),
            default
        );
        _vk.QueueWaitIdle(_deviceContext.GraphicsQueue.Queue);

        _vk.DestroyCommandPool(
            _deviceContext.LogicalDevice,
            commandCopyPool,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
    }

    private void CreateCommandBuffers(
        CommandPool commandPool,
        uint commandBufferCount,
        Span<CommandBuffer> bufferAllocRef,
        CommandBufferLevel level = CommandBufferLevel.Primary
    )
    {
        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = commandPool,
            Level = level,
            CommandBufferCount = commandBufferCount,
        };

        if (
            _vk.AllocateCommandBuffers(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<CommandBufferAllocateInfo>(ref allocInfo),
                bufferAllocRef
            ) != Result.Success
        )
            throw new Exception("Failed to allocate command buffers");
    }

    private CommandPool CreateCommandPool(
        uint queueFamilyIndex,
        CommandPoolCreateFlags flags = CommandPoolCreateFlags.ResetCommandBufferBit
    )
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = flags,
            QueueFamilyIndex = queueFamilyIndex,
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
            throw new Exception("Failed to create command pool");

        return commandPool;
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
}
