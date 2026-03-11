using EmberVox.Rendering.Utils;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Contexts;

internal class DepthContext : IDisposable
{
    public Image DepthImage { get; }
    public ImageView DepthImageView { get; }
    public Format DepthImageFormat { get; }

    private readonly Vk _vk;
    private readonly DeviceContext _deviceContext;
    private readonly SwapChainContext _swapChainContext;
    private readonly MemoryRequirements _memoryRequirements;
    private readonly DeviceMemory _depthImageMemory;

    public DepthContext(Vk vk, DeviceContext deviceContext, SwapChainContext swapChainContext)
    {
        _vk = vk;
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;

        DepthImageFormat = FindDepthFormat();

        DepthImage = ImageUtils.CreateImage(
            _vk,
            _deviceContext.LogicalDevice,
            _swapChainContext.SwapChainExtent.Width,
            _swapChainContext.SwapChainExtent.Height,
            DepthImageFormat,
            ImageTiling.Optimal,
            ImageUsageFlags.DepthStencilAttachmentBit
        );

        _memoryRequirements = _vk.GetImageMemoryRequirements(
            _deviceContext.LogicalDevice,
            DepthImage
        );
        _depthImageMemory = _deviceContext.AllocateMemory(
            _memoryRequirements,
            MemoryPropertyFlags.DeviceLocalBit
        );

        _vk.BindImageMemory(_deviceContext.LogicalDevice, DepthImage, _depthImageMemory, 0);

        DepthImageView = ImageUtils.CreateImageView(
            _vk,
            _deviceContext.LogicalDevice,
            DepthImage,
            DepthImageFormat,
            ImageAspectFlags.DepthBit
        );
    }

    public void Dispose()
    {
        _vk.DestroyImageView(
            _deviceContext.LogicalDevice,
            DepthImageView,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _vk.FreeMemory(
            _deviceContext.LogicalDevice,
            _depthImageMemory,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _vk.DestroyImage(
            _deviceContext.LogicalDevice,
            DepthImage,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );

        GC.SuppressFinalize(this);
    }

    private Format FindDepthFormat()
    {
        return _deviceContext.FindSupportedFormat(
            [Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint],
            ImageTiling.Optimal,
            FormatFeatureFlags.DepthStencilAttachmentBit
        );
    }

    private bool HasStencilComponent(Format format)
    {
        return format == Format.D32SfloatS8Uint || format == Format.D24UnormS8Uint;
    }
}
