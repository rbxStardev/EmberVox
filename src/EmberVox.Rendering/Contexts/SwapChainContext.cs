using EmberVox.Core.Logging;
using EmberVox.Rendering.ResourceManagement;
using EmberVox.Rendering.Utils;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace EmberVox.Rendering.Contexts;

public sealed class SwapChainContext : IResource
{
    public KhrSwapchain KhrSwapChainExtension { get; }
    public SwapchainKHR SwapChainKhr { get; private set; }
    public Image[] SwapChainImages { get; private set; }
    public ImageView[] SwapChainImageViews { get; private set; }
    public Format SwapChainImageFormat { get; }
    public Extent2D SwapChainExtent { get; private set; }

    private readonly SurfaceContext _surfaceContext;
    private readonly DeviceContext _deviceContext;
    private readonly IWindow _window;
    private readonly Instance _instance;
    private readonly SurfaceFormatKHR _surfaceFormat;
    private readonly PresentModeKHR _presentMode;
    private SurfaceCapabilitiesKHR _surfaceCapabilities;

    public SwapChainContext(
        SurfaceContext surfaceContext,
        DeviceContext deviceContext,
        IWindow window,
        Instance instance
    )
    {
        _surfaceContext = surfaceContext;
        _deviceContext = deviceContext;
        _window = window;
        _instance = instance;

        if (
            !_deviceContext.Api.TryGetDeviceExtension(
                _instance,
                deviceContext.LogicalDevice,
                out KhrSwapchain khrSwapChainExtension
            )
        )
            throw new Exception("Failed to get KhrSwapchain extension");
        KhrSwapChainExtension = khrSwapChainExtension;

        _surfaceCapabilities = GetSurfaceCapabilities();
        _surfaceFormat = GetSurfaceFormat();
        _presentMode = GetSwapChainPresentMode();

        SwapChainImageFormat = _surfaceFormat.Format;
        SwapChainExtent = GetSwapChainExtent();
        SwapChainKhr = CreateSwapChain(null);
        SwapChainImages = GetSwapChainImages();
        SwapChainImageViews = CreateSwapChainImageViews();

        Logger.Metric?.WriteLine($"SwapChain Image Format: {SwapChainImageFormat}");
        Logger.Metric?.WriteLine(
            $"SwapChain Extent: (x:{SwapChainExtent.Width}, y:{SwapChainExtent.Height})"
        );
        Logger.Metric?.WriteLine($"SwapChain Present Mode: {_presentMode}");
        Logger.Metric?.WriteLine($"SwapChain Images: {SwapChainImages.Length}");
        Logger.Metric?.WriteLine($"SwapChain Image Views: {SwapChainImageViews.Length}");
    }

    public void Dispose()
    {
        foreach (ImageView imageView in SwapChainImageViews)
            _deviceContext.Api.DestroyImageView(
                _deviceContext.LogicalDevice,
                imageView,
                ReadOnlySpan<AllocationCallbacks>.Empty
            );

        KhrSwapChainExtension.DestroySwapchain(
            _deviceContext.LogicalDevice,
            SwapChainKhr,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        KhrSwapChainExtension.Dispose();

        GC.SuppressFinalize(this);
    }

    public void RecreateSwapChain()
    {
        SwapchainKHR oldSwapChain = SwapChainKhr;

        _surfaceCapabilities = GetSurfaceCapabilities();
        SwapChainExtent = GetSwapChainExtent();
        SwapChainKhr = CreateSwapChain(oldSwapChain);

        foreach (ImageView imageView in SwapChainImageViews)
            _deviceContext.Api.DestroyImageView(
                _deviceContext.LogicalDevice,
                imageView,
                ReadOnlySpan<AllocationCallbacks>.Empty
            );

        KhrSwapChainExtension.DestroySwapchain(
            _deviceContext.LogicalDevice,
            oldSwapChain,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );

        SwapChainImages = GetSwapChainImages();
        SwapChainImageViews = CreateSwapChainImageViews();
    }

    private unsafe SwapchainKHR CreateSwapChain(SwapchainKHR? oldSwapChain)
    {
        Span<uint> imageCount = stackalloc uint[1];
        imageCount[0] = _surfaceCapabilities.MaxImageCount + 1;

        if (
            _surfaceCapabilities.MaxImageCount > 0
            && imageCount[0] > _surfaceCapabilities.MaxImageCount
        )
            imageCount[0] = _surfaceCapabilities.MaxImageCount;

        SwapchainCreateInfoKHR swapchainCreateInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surfaceContext.SurfaceKhr,
            MinImageCount = imageCount[0],
            ImageFormat = _surfaceFormat.Format,
            ImageColorSpace = _surfaceFormat.ColorSpace,
            ImageExtent = SwapChainExtent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = _surfaceCapabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = _presentMode,
            Clipped = true,
            OldSwapchain = oldSwapChain.GetValueOrDefault(),
        };

        uint* queueFamilyIndices = stackalloc[] {
            _deviceContext.GraphicsQueue.Index,
            _deviceContext.PresentQueue.Index,
        };

        if (_deviceContext.GraphicsQueue.Index != _deviceContext.PresentQueue.Index)
        {
            swapchainCreateInfo.ImageSharingMode = SharingMode.Concurrent;
            swapchainCreateInfo.QueueFamilyIndexCount = 2;
            swapchainCreateInfo.PQueueFamilyIndices = queueFamilyIndices;
        }

        if (
            KhrSwapChainExtension.CreateSwapchain(
                _deviceContext.LogicalDevice,
                &swapchainCreateInfo,
                null,
                out SwapchainKHR swapchainKhr
            ) != Result.Success
        )
            throw new Exception("Failed to create Swapchain");

        return swapchainKhr;
    }

    private SurfaceCapabilitiesKHR GetSurfaceCapabilities()
    {
        _surfaceContext.KhrSurfaceExtension.GetPhysicalDeviceSurfaceCapabilities(
            _deviceContext.PhysicalDevice,
            _surfaceContext.SurfaceKhr,
            out SurfaceCapabilitiesKHR surfaceCapabilities
        );

        return surfaceCapabilities;
    }

    private SurfaceFormatKHR GetSurfaceFormat()
    {
        Span<uint> count = stackalloc uint[1];
        _surfaceContext.KhrSurfaceExtension.GetPhysicalDeviceSurfaceFormats(
            _deviceContext.PhysicalDevice,
            _surfaceContext.SurfaceKhr,
            count,
            Span<SurfaceFormatKHR>.Empty
        );

        SurfaceFormatKHR[] formats = new SurfaceFormatKHR[count[0]];
        _surfaceContext.KhrSurfaceExtension.GetPhysicalDeviceSurfaceFormats(
            _deviceContext.PhysicalDevice,
            _surfaceContext.SurfaceKhr,
            count,
            formats.AsSpan()
        );

        foreach (SurfaceFormatKHR format in formats)
        {
            if (
                format is
                { Format: Format.B8G8R8A8Srgb, ColorSpace: ColorSpaceKHR.SpaceSrgbNonlinearKhr }
            )
                return format;
        }

        return formats[0];
    }

    private PresentModeKHR GetSwapChainPresentMode()
    {
        Span<uint> count = stackalloc uint[1];
        _surfaceContext.KhrSurfaceExtension.GetPhysicalDeviceSurfacePresentModes(
            _deviceContext.PhysicalDevice,
            _surfaceContext.SurfaceKhr,
            count,
            Span<PresentModeKHR>.Empty
        );

        PresentModeKHR[] presentModes = new PresentModeKHR[count[0]];
        _surfaceContext.KhrSurfaceExtension.GetPhysicalDeviceSurfacePresentModes(
            _deviceContext.PhysicalDevice,
            _surfaceContext.SurfaceKhr,
            count,
            presentModes.AsSpan()
        );

        foreach (PresentModeKHR presentMode in presentModes)
        {
            if (presentMode is PresentModeKHR.MailboxKhr)
                return presentMode;
        }

        return PresentModeKHR.FifoKhr;
    }

    private Extent2D GetSwapChainExtent()
    {
        if (_surfaceCapabilities.CurrentExtent.Width != uint.MaxValue)
            return _surfaceCapabilities.CurrentExtent;

        int width = _window.FramebufferSize.X;
        int height = _window.FramebufferSize.Y;

        return new Extent2D(
            (uint)
                Math.Clamp(
                    width,
                    _surfaceCapabilities.MinImageExtent.Width,
                    _surfaceCapabilities.MaxImageExtent.Width
                ),
            (uint)
                Math.Clamp(
                    height,
                    _surfaceCapabilities.MinImageExtent.Height,
                    _surfaceCapabilities.MaxImageExtent.Height
                )
        );
    }

    private Image[] GetSwapChainImages()
    {
        Span<uint> count = stackalloc uint[1];
        KhrSwapChainExtension.GetSwapchainImages(
            _deviceContext.LogicalDevice,
            SwapChainKhr,
            count,
            Span<Image>.Empty
        );

        Image[] images = new Image[count[0]];
        KhrSwapChainExtension.GetSwapchainImages(
            _deviceContext.LogicalDevice,
            SwapChainKhr,
            count,
            images.AsSpan()
        );

        return images;
    }

    private unsafe ImageView[] CreateSwapChainImageViews()
    {
        ImageView[] imageViews = new ImageView[SwapChainImages.Length];

        ImageViewCreateInfo createInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            ViewType = ImageViewType.Type2D,
            Format = SwapChainImageFormat,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
        };

        for (int i = 0; i < SwapChainImages.Length; i++)
        {
            createInfo.Image = SwapChainImages[i];

            imageViews[i] = ImageUtils.CreateImageView(
                _deviceContext.Api,
                _deviceContext.LogicalDevice,
                SwapChainImages[i],
                1,
                SwapChainImageFormat,
                ImageAspectFlags.ColorBit
            );
        }

        return imageViews;
    }
}
