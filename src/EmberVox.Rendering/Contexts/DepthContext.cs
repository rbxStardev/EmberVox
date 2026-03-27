using EmberVox.Rendering.ResourceManagement;
using EmberVox.Rendering.Utils;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Contexts;

public class DepthContext : IResource
{
    private readonly DeviceMemory _depthImageMemory;

    private readonly DeviceContext _deviceContext;
    private readonly MemoryRequirements _memoryRequirements;
    private readonly SwapChainContext _swapChainContext;

    public DepthContext(DeviceContext deviceContext, SwapChainContext swapChainContext)
    {
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;

        DepthImageFormat = FindDepthFormat();

        DepthImage = ImageUtils.CreateImage(
            _deviceContext.Api,
            _deviceContext.LogicalDevice,
            _swapChainContext.SwapChainExtent.Width,
            _swapChainContext.SwapChainExtent.Height,
            1,
            DepthImageFormat,
            ImageTiling.Optimal,
            ImageUsageFlags.DepthStencilAttachmentBit
        );

        _deviceContext.Api.GetImageMemoryRequirements(
            _deviceContext.LogicalDevice,
            DepthImage,
            new Span<MemoryRequirements>(ref _memoryRequirements)
        );
        _depthImageMemory = _deviceContext.AllocateMemory(
            _memoryRequirements,
            MemoryPropertyFlags.DeviceLocalBit
        );

        _deviceContext.Api.BindImageMemory(
            _deviceContext.LogicalDevice,
            DepthImage,
            _depthImageMemory,
            0
        );

        DepthImageView = ImageUtils.CreateImageView(
            _deviceContext.Api,
            _deviceContext.LogicalDevice,
            DepthImage,
            1,
            DepthImageFormat,
            ImageAspectFlags.DepthBit
        );
    }

    public Image DepthImage { get; }
    public ImageView DepthImageView { get; }
    public Format DepthImageFormat { get; }

    public void Dispose()
    {
        _deviceContext.Api.DestroyImageView(
            _deviceContext.LogicalDevice,
            DepthImageView,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _deviceContext.Api.FreeMemory(
            _deviceContext.LogicalDevice,
            _depthImageMemory,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _deviceContext.Api.DestroyImage(
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
