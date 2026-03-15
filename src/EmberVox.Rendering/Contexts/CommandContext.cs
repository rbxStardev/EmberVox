using System.ComponentModel;
using EmberVox.Rendering.Buffers;
using EmberVox.Rendering.ResourceManagement;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Contexts;

public sealed class CommandContext : IResource
{
    public CommandPool MainCommandPool { get; }
    public CommandBuffer[] CommandBuffers { get; }

    private readonly DeviceContext _deviceContext;
    private readonly SwapChainContext _swapChainContext;
    private readonly uint _maxFramesInFlight;

    public CommandContext(
        DeviceContext deviceContext,
        SwapChainContext swapChainContext,
        uint maxFramesInFlight
    )
    {
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;
        _maxFramesInFlight = maxFramesInFlight;

        MainCommandPool = CreateCommandPool(_deviceContext.GraphicsQueue.Index);
        CommandBuffers = new CommandBuffer[_maxFramesInFlight];

        CreateCommandBuffers(MainCommandPool, _maxFramesInFlight, CommandBuffers);
    }

    public void Dispose()
    {
        _deviceContext.Api.DestroyCommandPool(
            _deviceContext.LogicalDevice,
            MainCommandPool,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );

        GC.SuppressFinalize(this);
    }

    public unsafe void BeginCommandBufferRecording(
        DepthContext depthContext,
        uint imageIndex,
        int currentFrame
    )
    {
        CommandBuffer commandBuffer = CommandBuffers[currentFrame];

        CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
        var beginResult = _deviceContext.Api.BeginCommandBuffer(
            commandBuffer,
            new ReadOnlySpan<CommandBufferBeginInfo>(ref beginInfo)
        );

        TransitionImageLayout(
            commandBuffer,
            _swapChainContext.SwapChainImages[imageIndex],
            ImageLayout.Undefined,
            ImageLayout.ColorAttachmentOptimal,
            AccessFlags2.None,
            AccessFlags2.ColorAttachmentWriteBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            ImageAspectFlags.ColorBit
        );
        TransitionImageLayout(
            commandBuffer,
            depthContext.DepthImage,
            ImageLayout.Undefined,
            ImageLayout.DepthAttachmentOptimal,
            AccessFlags2.DepthStencilAttachmentWriteBit,
            AccessFlags2.DepthStencilAttachmentWriteBit,
            PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit,
            PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit,
            ImageAspectFlags.DepthBit
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

        ClearValue clearDepth = new ClearValue(null, new ClearDepthStencilValue(1.0f, 0));
        RenderingAttachmentInfo depthAttachmentInfo = new()
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = depthContext.DepthImageView,
            ImageLayout = ImageLayout.DepthAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            ClearValue = clearDepth,
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
            PDepthAttachment = &depthAttachmentInfo,
        };

        _deviceContext.Api.CmdBeginRendering(
            commandBuffer,
            new ReadOnlySpan<RenderingInfo>(ref renderingInfo)
        );

        Viewport viewport = new(
            0.0f,
            0.0f,
            _swapChainContext.SwapChainExtent.Width,
            _swapChainContext.SwapChainExtent.Height,
            0.0f,
            1.0f
        );
        _deviceContext.Api.CmdSetViewport(
            commandBuffer,
            0,
            new ReadOnlySpan<Viewport>(ref viewport)
        );

        Rect2D scissor = new(new Offset2D(0, 0), _swapChainContext.SwapChainExtent);
        _deviceContext.Api.CmdSetScissor(commandBuffer, 0, new ReadOnlySpan<Rect2D>(ref scissor));
    }

    public void EndCommandBufferRecording(uint imageIndex, int currentFrame)
    {
        CommandBuffer commandBuffer = CommandBuffers[currentFrame];

        _deviceContext.Api.CmdEndRendering(commandBuffer);

        TransitionImageLayout(
            commandBuffer,
            _swapChainContext.SwapChainImages[imageIndex],
            ImageLayout.ColorAttachmentOptimal,
            ImageLayout.PresentSrcKhr,
            AccessFlags2.ColorAttachmentWriteBit,
            AccessFlags2.None,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            PipelineStageFlags2.BottomOfPipeBit,
            ImageAspectFlags.ColorBit
        );

        var endResult = _deviceContext.Api.EndCommandBuffer(commandBuffer);
    }

    public CommandBuffer BeginSingleTimeCommands()
    {
        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = MainCommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };
        CommandBuffer commandBuffer = default;
        _deviceContext.Api.AllocateCommandBuffers(
            _deviceContext.LogicalDevice,
            new ReadOnlySpan<CommandBufferAllocateInfo>(ref allocateInfo),
            new Span<CommandBuffer>(ref commandBuffer)
        );

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        _deviceContext.Api.BeginCommandBuffer(
            commandBuffer,
            new ReadOnlySpan<CommandBufferBeginInfo>(ref beginInfo)
        );

        return commandBuffer;
    }

    public unsafe void EndSingleTimeCommands(CommandBuffer commandBuffer)
    {
        _deviceContext.Api.EndCommandBuffer(commandBuffer);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };
        _deviceContext.Api.QueueSubmit(
            _deviceContext.GraphicsQueue.Queue,
            new ReadOnlySpan<SubmitInfo>(ref submitInfo),
            default
        );
        _deviceContext.Api.QueueWaitIdle(_deviceContext.GraphicsQueue.Queue);
    }

    public void CopyBuffer(
        BufferContext srcBufferContext,
        BufferContext dstBufferContext,
        ulong size
    )
    {
        CommandBuffer commandBuffer = BeginSingleTimeCommands();

        BufferCopy copy = new() { Size = size };
        _deviceContext.Api.CmdCopyBuffer(
            commandBuffer,
            srcBufferContext.Buffer,
            dstBufferContext.Buffer,
            new ReadOnlySpan<BufferCopy>(ref copy)
        );

        EndSingleTimeCommands(commandBuffer);
    }

    public void CopyBufferToImage(BufferContext bufferContext, Image image, uint width, uint height)
    {
        CommandBuffer commandBuffer = BeginSingleTimeCommands();

        BufferImageCopy region = new()
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D(width, height, 1),
        };

        _deviceContext.Api.CmdCopyBufferToImage(
            commandBuffer,
            bufferContext.Buffer,
            image,
            ImageLayout.TransferDstOptimal,
            new ReadOnlySpan<BufferImageCopy>(ref region)
        );

        EndSingleTimeCommands(commandBuffer);
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
            _deviceContext.Api.AllocateCommandBuffers(
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
            _deviceContext.Api.CreateCommandPool(
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
        Image image,
        ImageLayout oldLayout,
        ImageLayout newLayout,
        AccessFlags2 srcAccessMask,
        AccessFlags2 dstAccessMask,
        PipelineStageFlags2 srcStageMask,
        PipelineStageFlags2 dstStageMask,
        ImageAspectFlags imageAspectFlags
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
            Image = image,
            SubresourceRange = new ImageSubresourceRange()
            {
                AspectMask = imageAspectFlags,
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

        _deviceContext.Api.CmdPipelineBarrier2(
            commandBuffer,
            new ReadOnlySpan<DependencyInfo>(ref dependencyInfo)
        );
    }

    public void TransitionImageLayout(
        Image image,
        uint mipLevels,
        ImageLayout oldLayout,
        ImageLayout newLayout
    )
    {
        CommandBuffer commandBuffer = BeginSingleTimeCommands();

        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            Image = image,
            SubresourceRange = new ImageSubresourceRange(
                ImageAspectFlags.ColorBit,
                0,
                mipLevels,
                0,
                1
            ),
        };

        PipelineStageFlags sourceStage;
        PipelineStageFlags destinationStage;

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = default;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;

            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (
            oldLayout == ImageLayout.TransferDstOptimal
            && newLayout == ImageLayout.ShaderReadOnlyOptimal
        )
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;

            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.FragmentShaderBit;
        }
        else
        {
            throw new InvalidEnumArgumentException("unsupported layout transition!");
        }

        _deviceContext.Api.CmdPipelineBarrier(
            commandBuffer,
            sourceStage,
            destinationStage,
            DependencyFlags.None,
            ReadOnlySpan<MemoryBarrier>.Empty,
            ReadOnlySpan<BufferMemoryBarrier>.Empty,
            new ReadOnlySpan<ImageMemoryBarrier>(ref barrier)
        );

        EndSingleTimeCommands(commandBuffer);
    }
}
