using EmberVox.Rendering.Contexts;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Utils;

internal class ImageUtils
{
    public static Image CreateImage(
        Vk vk,
        Device logicalDevice,
        uint width,
        uint height,
        uint mipLevels,
        Format format,
        ImageTiling tiling,
        ImageUsageFlags usage
    )
    {
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D(width, height, 1),
            MipLevels =  mipLevels,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = tiling,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };

        Image image = default;
        vk.CreateImage(
            logicalDevice,
            new ReadOnlySpan<ImageCreateInfo>(ref imageInfo),
            ReadOnlySpan<AllocationCallbacks>.Empty,
            new Span<Image>(ref image)
        );

        return image;
    }

    public static ImageView CreateImageView(
        Vk vk,
        Device logicalDevice,
        Image? image,
        uint mipLevels,
        Format format,
        ImageAspectFlags aspectFlags
    )
    {
        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange(aspectFlags, 0, mipLevels, 0, 1),
        };
        if (image != null)
        {
            viewInfo.Image = (Image)image;
        }

        ImageView imageView = default;
        vk.CreateImageView(
            logicalDevice,
            new ReadOnlySpan<ImageViewCreateInfo>(ref viewInfo),
            ReadOnlySpan<AllocationCallbacks>.Empty,
            new Span<ImageView>(ref imageView)
        );

        return imageView;
    }

    public static unsafe void GenerateMipmaps(Vk vk, CommandContext commandContext, DeviceContext deviceContext, Image image, Format imageFormat, int textureWidth, int textureHeight, uint mipLevels)
    {
        FormatProperties formatProperties =
            vk.GetPhysicalDeviceFormatProperties(deviceContext.PhysicalDevice, imageFormat);
        if (!formatProperties.OptimalTilingFeatures.HasFlag(FormatFeatureFlags.SampledImageFilterLinearBit))
        {
            throw new Exception("Texture image format does not support linear blitting!");
        }
        
        CommandBuffer commandBuffer = commandContext.BeginSingleTimeCommands();

        ImageMemoryBarrier barrier = new ImageMemoryBarrier(StructureType.ImageMemoryBarrier, null,
            AccessFlags.TransferWriteBit, AccessFlags.TransferReadBit, ImageLayout.TransferDstOptimal,
            ImageLayout.TransferSrcOptimal, Vk.QueueFamilyIgnored, Vk.QueueFamilyIgnored, image);
        barrier.SubresourceRange.AspectMask = ImageAspectFlags.ColorBit;
        barrier.SubresourceRange.BaseArrayLayer = 0;
        barrier.SubresourceRange.LayerCount = 1;
        barrier.SubresourceRange.LevelCount = 1;

        uint mipWidth = (uint)textureWidth;
        uint mipHeight = (uint)textureHeight;

        for (uint i = 1; i < mipLevels; i++)
        {
            barrier.SubresourceRange.BaseMipLevel = i - 1;
            barrier.OldLayout = ImageLayout.TransferDstOptimal;
            barrier.NewLayout = ImageLayout.TransferSrcOptimal;
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.TransferReadBit;
            
            vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.TransferBit, default, ReadOnlySpan<MemoryBarrier>.Empty, ReadOnlySpan<BufferMemoryBarrier>.Empty, new ReadOnlySpan<ImageMemoryBarrier>(ref barrier));

            ImageBlit.SrcOffsetsBuffer offsets = new ImageBlit.SrcOffsetsBuffer();
            ImageBlit.DstOffsetsBuffer dstOffsets = new ImageBlit.DstOffsetsBuffer();
            offsets[0] = new Offset3D(0, 0, 0);
            offsets[1] = new Offset3D((int)mipWidth, (int)mipHeight, 1);
            dstOffsets[0] = new Offset3D(0, 0, 0);
            dstOffsets[1] = new Offset3D((int)(mipWidth > 1 ? mipWidth / 2 : 1), (int)(mipHeight > 2 ? mipHeight / 2 : 1), 1);

            ImageBlit blit = new ImageBlit()
            {
                SrcOffsets = offsets,
                DstOffsets = dstOffsets
            };
            blit.SrcSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, i - 1, 0, 1);
            blit.DstSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, i, 0, 1);
            
            vk.CmdBlitImage(commandBuffer, image, ImageLayout.TransferSrcOptimal, image, ImageLayout.TransferDstOptimal, new ReadOnlySpan<ImageBlit>(ref blit), Filter.Linear);

            barrier.OldLayout = ImageLayout.TransferSrcOptimal;
            barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
            barrier.SrcAccessMask = AccessFlags.TransferReadBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;
            
            vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, default, ReadOnlySpan<MemoryBarrier>.Empty, ReadOnlySpan<BufferMemoryBarrier>.Empty, new ReadOnlySpan<ImageMemoryBarrier>(ref barrier));

            if (mipWidth > 1) mipWidth /= 2;
            if (mipHeight > 1) mipHeight /= 2;
        }

        barrier.SubresourceRange.BaseMipLevel = mipLevels - 1;
        barrier.OldLayout = ImageLayout.TransferDstOptimal;
        barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
        barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
        barrier.DstAccessMask = AccessFlags.ShaderReadBit;
        
        vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, default, ReadOnlySpan<MemoryBarrier>.Empty, ReadOnlySpan<BufferMemoryBarrier>.Empty, new ReadOnlySpan<ImageMemoryBarrier>(ref barrier));
        
        commandContext.EndSingleTimeCommands(commandBuffer);
    }
}
