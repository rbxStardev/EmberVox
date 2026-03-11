using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Utils;

public class ImageUtils
{
    public static Image CreateImage(
        Vk vk,
        Device logicalDevice,
        uint width,
        uint height,
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
            MipLevels = 1,
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
        Format format,
        ImageAspectFlags aspectFlags
    )
    {
        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange(aspectFlags, 0, 1, 0, 1),
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
}
