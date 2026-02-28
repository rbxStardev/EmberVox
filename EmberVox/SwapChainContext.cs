using Silk.NET.Vulkan.Extensions.KHR;

namespace EmberVox;

public class SwapChainContext : IDisposable
{
    public KhrSwapchain KhrSwapChainExtension { get; }
    public SwapchainKHR SwapChainKhr { get; }
    public ImageView[] SwapChainImageViews { get; }
    public Format SwapChainImageFormat { get; }
    public Extent2D SwapChainExtent { get; }

    private readonly SurfaceCapabilitiesKHR _surfaceCapabilities;
    private readonly SurfaceFormatKHR _surfaceFormat;
    private readonly PresentModeKHR _presentMode;
    private readonly Image[] _images;

    private readonly Vk _vk;
    private readonly SurfaceContext _surfaceContext;
    private readonly DeviceContext _deviceContext;
    private readonly IWindow _window;
    private readonly Instance _instance;

    public SwapChainContext(
        Vk vk,
        SurfaceContext surfaceContext,
        DeviceContext deviceContext,
        IWindow window,
        Instance instance
    )
    {
        _vk = vk;
        _surfaceContext = surfaceContext;
        _deviceContext = deviceContext;
        _window = window;
        _instance = instance;

        if (
            !_vk.TryGetDeviceExtension(
                _instance,
                deviceContext.LogicalDevice,
                out KhrSwapchain khrSwapChainExtension
            )
        )
            throw new Exception("Failed to get KhrSwapchain extension");
        KhrSwapChainExtension = khrSwapChainExtension;

        _surfaceCapabilities = GetSurfaceCapabilities();
        _surfaceFormat = GetSurfaceFormat();
        SwapChainImageFormat = _surfaceFormat.Format;
        SwapChainExtent = GetSwapChainExtent();
        _presentMode = GetSwapChainPresentMode();
        SwapChainKhr = CreateSwapChain();
        _images = GetSwapChainImages();
        SwapChainImageViews = CreateSwapChainImageViews();
    }

    private unsafe SwapchainKHR CreateSwapChain()
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
            OldSwapchain = default, // gives an error randomly IDK why, DO NOT TOUCH
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
        else
        {
            swapchainCreateInfo.ImageSharingMode = SharingMode.Exclusive;
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
        Span<uint> surfaceFormatCount = stackalloc uint[1];
        _surfaceContext.KhrSurfaceExtension.GetPhysicalDeviceSurfaceFormats(
            _deviceContext.PhysicalDevice,
            _surfaceContext.SurfaceKhr,
            surfaceFormatCount,
            Span<SurfaceFormatKHR>.Empty
        );

        SurfaceFormatKHR[] availableSurfaceFormats = new SurfaceFormatKHR[surfaceFormatCount[0]];
        _surfaceContext.KhrSurfaceExtension.GetPhysicalDeviceSurfaceFormats(
            _deviceContext.PhysicalDevice,
            _surfaceContext.SurfaceKhr,
            surfaceFormatCount,
            availableSurfaceFormats.AsSpan()
        );

        return ChooseSwapChainSurfaceFormat(availableSurfaceFormats);
    }

    private SurfaceFormatKHR ChooseSwapChainSurfaceFormat(SurfaceFormatKHR[] availableFormats)
    {
        foreach (SurfaceFormatKHR availableFormat in availableFormats)
        {
            if (
                availableFormat is
                { Format: Format.B8G8R8A8Srgb, ColorSpace: ColorSpaceKHR.SpaceSrgbNonlinearKhr }
            )
                return availableFormat;
        }

        return availableFormats[0];
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

    private PresentModeKHR GetSwapChainPresentMode()
    {
        Span<uint> presentModeCount = stackalloc uint[1];
        _surfaceContext.KhrSurfaceExtension.GetPhysicalDeviceSurfacePresentModes(
            _deviceContext.PhysicalDevice,
            _surfaceContext.SurfaceKhr,
            presentModeCount,
            Span<PresentModeKHR>.Empty
        );

        PresentModeKHR[] availablePresentModes = new PresentModeKHR[presentModeCount[0]];
        _surfaceContext.KhrSurfaceExtension.GetPhysicalDeviceSurfacePresentModes(
            _deviceContext.PhysicalDevice,
            _surfaceContext.SurfaceKhr,
            presentModeCount,
            availablePresentModes.AsSpan()
        );

        return ChooseSwapChainPresentMode(availablePresentModes);
    }

    private PresentModeKHR ChooseSwapChainPresentMode(PresentModeKHR[] availablePresentModes)
    {
        foreach (PresentModeKHR availablePresentMode in availablePresentModes)
        {
            if (availablePresentMode is PresentModeKHR.MailboxKhr)
                return availablePresentMode;
        }

        return PresentModeKHR.FifoKhr;
    }

    private Image[] GetSwapChainImages()
    {
        Span<uint> imageCount = stackalloc uint[1];
        KhrSwapChainExtension.GetSwapchainImages(
            _deviceContext.LogicalDevice,
            SwapChainKhr,
            imageCount,
            Span<Image>.Empty
        );

        Image[] images = new Image[imageCount[0]];
        KhrSwapChainExtension.GetSwapchainImages(
            _deviceContext.LogicalDevice,
            SwapChainKhr,
            imageCount,
            images.AsSpan()
        );

        return images;
    }

    private unsafe ImageView[] CreateSwapChainImageViews()
    {
        ImageView[] imageViews = new ImageView[_images.Length];

        ImageViewCreateInfo imageViewCreateInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            ViewType = ImageViewType.Type2D,
            Format = SwapChainImageFormat,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
        };

        for (int i = 0; i < _images.Length; i++)
        {
            imageViewCreateInfo.Image = _images[i];

            if (
                _vk.CreateImageView(
                    _deviceContext.LogicalDevice,
                    &imageViewCreateInfo,
                    null,
                    out imageViews[i]
                ) != Result.Success
            )
                throw new Exception("Failed to create ImageView for SwapChain");
        }

        return imageViews;
    }

    public void Dispose()
    {
        foreach (var imageView in SwapChainImageViews)
        {
            _vk.DestroyImageView(
                _deviceContext.LogicalDevice,
                imageView,
                ReadOnlySpan<AllocationCallbacks>.Empty
            );
        }

        KhrSwapChainExtension.DestroySwapchain(
            _deviceContext.LogicalDevice,
            SwapChainKhr,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        KhrSwapChainExtension.Dispose();

        GC.SuppressFinalize(this);
    }
}
