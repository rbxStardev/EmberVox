using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Contexts;

public class CommandContext : IDisposable
{
    public CommandPool CommandPool { get; }
    public CommandBuffer CommandBuffer { get; }

    private readonly Vk _vk;
    private readonly DeviceContext _deviceContext;
    private readonly SwapChainContext _swapChainContext;
    private readonly GraphicsPipelineContext _graphicsPipelineContext;

    public CommandContext(
        Vk vk,
        DeviceContext deviceContext,
        SwapChainContext swapChainContext,
        GraphicsPipelineContext graphicsPipelineContext
    )
    {
        _vk = vk;
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;
        _graphicsPipelineContext = graphicsPipelineContext;

        CommandPool = CreateCommandPool();
        CommandBuffer = CreateCommandBuffer();
    }

    public unsafe void RecordCommandBuffer(uint imageIndex)
    {
        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        _vk.BeginCommandBuffer(
            CommandBuffer,
            new ReadOnlySpan<CommandBufferBeginInfo>(ref beginInfo)
        );

        TransitionImageLayout(
            imageIndex,
            ImageLayout.Undefined,
            ImageLayout.ColorAttachmentOptimal,
            default,
            AccessFlags2.ColorAttachmentWriteBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            PipelineStageFlags2.ColorAttachmentOutputBit
        );

        ClearValue clearColor = new ClearValue(new ClearColorValue(0.0f, 0.0f, 0.0f, 1.0f));
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
            RenderArea = new Rect2D
            {
                Offset = new Offset2D(0, 0),
                Extent = _swapChainContext.SwapChainExtent,
            },
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &attachmentInfo,
        };

        _vk.CmdBeginRendering(CommandBuffer, new ReadOnlySpan<RenderingInfo>(ref renderingInfo));

        _vk.CmdBindPipeline(
            CommandBuffer,
            PipelineBindPoint.Graphics,
            _graphicsPipelineContext.GraphicsPipeline
        );

        Viewport viewport = new Viewport(
            0.0f,
            0.0f,
            _swapChainContext.SwapChainExtent.Width,
            _swapChainContext.SwapChainExtent.Height,
            0.0f,
            1.0f
        );
        _vk.CmdSetViewport(CommandBuffer, 0, new ReadOnlySpan<Viewport>(ref viewport));

        Rect2D scissor = new Rect2D(new Offset2D(0, 0), _swapChainContext.SwapChainExtent);
        _vk.CmdSetScissor(CommandBuffer, 0, new ReadOnlySpan<Rect2D>(ref scissor));

        _vk.CmdDraw(CommandBuffer, 3, 1, 0, 0);

        _vk.CmdEndRendering(CommandBuffer);

        TransitionImageLayout(
            imageIndex,
            ImageLayout.ColorAttachmentOptimal,
            ImageLayout.PresentSrcKhr,
            AccessFlags2.ColorAttachmentWriteBit,
            default,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            PipelineStageFlags2.BottomOfPipeBit
        );

        _vk.EndCommandBuffer(CommandBuffer);
    }

    private unsafe void TransitionImageLayout(
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
            SubresourceRange = new ImageSubresourceRange
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
            CommandBuffer,
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

    private CommandBuffer CreateCommandBuffer()
    {
        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };

        CommandBuffer commandBuffer = default;
        if (
            _vk.AllocateCommandBuffers(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<CommandBufferAllocateInfo>(ref allocInfo),
                new Span<CommandBuffer>(ref commandBuffer)
            ) != Result.Success
        )
        {
            throw new Exception("Failed to create command buffer");
        }

        return commandBuffer;
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
