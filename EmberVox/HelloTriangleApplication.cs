using System.Runtime.InteropServices;
using EmberVox.Logging;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace EmberVox;

public unsafe class HelloTriangleApplication : IDisposable
{
    private const int WindowWidth = 800;
    private const int WindowHeight = 600;

    private static readonly string[] ValidationLayers = ["VK_LAYER_KHRONOS_validation"];
    private static readonly string[] DeviceExtensions = [KhrSwapchain.ExtensionName];

#if DEBUG
    private const bool EnableValidationLayers = true;
#else
    private const bool EnableValidationLayers = false;
#endif

    private IWindow _window;

    private Vk _vk = null!;
    private Instance _instance;

    private ExtDebugUtils _debugUtils = null!;
    private DebugUtilsMessengerEXT _debugMessenger;

    private PhysicalDevice _physicalDevice;
    private Device _device;
    private Queue _graphicsQueue;
    private Queue _presentQueue;

    private KhrSurface _khrSurface = null!;
    private SurfaceKHR _surfaceKhr;
    private KhrSwapchain _khrSwapChain = null!;
    private SwapchainKHR _swapChain;
    private Image[] _swapChainImages = null!;
    private ImageView[] _swapChainImageViews = null!;

    private Format _swapChainImageFormat = Format.Undefined;
    private Extent2D _swapChainExtent;

    public void Run()
    {
        InitWindow();
        InitVulkan();
        MainLoop();
        Dispose();
    }

    private void InitWindow()
    {
        WindowOptions options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(WindowWidth, WindowHeight),
            Title = "EmberVox: Vulkan",
            WindowBorder = WindowBorder.Fixed, // Basically makes it unresizable
        };

        _window = Window.Create(options);
        _window.Initialize();
    }

    private void InitVulkan()
    {
        Logger.Info?.WriteLine("~ Initializing Vulkan... ~");
        Console.WriteLine();
        _vk = Vk.GetApi();

        Logger.Debug?.WriteLine("-----> Creating Instance... <-----");
        CreateInstance();
        Logger.Debug?.WriteLine("-----> Instance creation: OK <-----");
        Console.WriteLine();

        Logger.Debug?.WriteLine("-----> Creating DebugMessenger... <-----");
        SetupDebugMessenger();
        Logger.Debug?.WriteLine("-----> DebugMessenger creation: OK <-----");
        Console.WriteLine();

        Logger.Debug?.WriteLine("-----> Creating Surface... <-----");
        CreateSurface();
        Logger.Debug?.WriteLine("-----> Surface creation: OK <-----");
        Console.WriteLine();

        Logger.Debug?.WriteLine("-----> Picking PhysicalDevice... <-----");
        PickPhysicalDevice();
        Logger.Debug?.WriteLine("-----> PhysicalDevice picking: OK <-----");
        Console.WriteLine();

        Logger.Debug?.WriteLine("-----> Creating LogicalDevice... <-----");
        CreateLogicalDevice();
        Logger.Debug?.WriteLine("-----> LogicalDevice creation: OK <-----");
        Console.WriteLine();

        Logger.Debug?.WriteLine("-----> Creating SwapChain... <-----");
        CreateSwapChain();
        Logger.Debug?.WriteLine("-----> SwapChain creation: OK <-----");
        Console.WriteLine();

        Logger.Debug?.WriteLine("-----> Creating SwapChainImageViews... <-----");
        CreateSwapChainImageViews();
        Logger.Debug?.WriteLine("-----> SwapChainImageViews creation: OK <-----");
        Console.WriteLine();

        Logger.Info?.WriteLine("~ Vulkan successfully initialized. ~");
        Console.WriteLine();
    }

    private void CreateInstance()
    {
        // Enable validation layers
        Logger.Metric?.WriteLine(
            $"Checking for {ValidationLayers.Length} validation layer(s) support: {string.Join(", ", ValidationLayers)}"
        );
        if (EnableValidationLayers && !CheckValidationLayerSupport())
            throw new Exception("Validation layer(s) not supported");

        // Using "(byte*)SilkMarshal.StringToPtr("string");" to turn strings to pointers since vulkan structs work with pointers/unmanaged types!
        // Dont forget to dispose after usage with "SilkMarshal.Free((nint)reference);".
        // Vulkan structs also need a SType specification on C#, or else the GPU won't be able to recognize the struct!
        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)SilkMarshal.StringToPtr("EmberVox"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)SilkMarshal.StringToPtr("No Engine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version13,
        };

        string[] extensions = GetRequiredInstanceExtensions();
        Logger.Metric?.WriteLine(
            $"Checking for {extensions.Length} extension(s) support: {string.Join(", ", extensions)}"
        );

        if (!CheckExtensionSupport(extensions))
            throw new Exception("Required extension(s) not supported");

        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)extensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions),
        };

        if (EnableValidationLayers)
        {
            Logger.Metric?.WriteLine(
                $"{ValidationLayers.Length} Enabled validation layer(s): {string.Join(", ", ValidationLayers)}"
            );
            createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;

            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);
        }
        else
        {
            createInfo.EnabledLayerCount = 0;
        }

        if (_vk.CreateInstance(&createInfo, null, out _instance) != Result.Success)
            throw new Exception("Failed to create Vulkan instance");

        // Cleanup
        SilkMarshal.Free((nint)appInfo.PApplicationName);
        SilkMarshal.Free((nint)appInfo.PEngineName);
        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

        if (EnableValidationLayers)
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
    }

    private bool CheckValidationLayerSupport()
    {
        Span<uint> layerCount = stackalloc uint[1];
        _vk.EnumerateInstanceLayerProperties(layerCount, Span<LayerProperties>.Empty);

        LayerProperties[] availableLayers = new LayerProperties[layerCount[0]];

        _vk.EnumerateInstanceLayerProperties(layerCount, availableLayers.AsSpan());

        foreach (string required in ValidationLayers)
        {
            bool found = false;
            foreach (LayerProperties layer in availableLayers)
            {
                // You might notice we dont cleanup after using a marshal function here,
                // this is because we're just converting an already existing pointer into something else we want to use!
                if (SilkMarshal.PtrToString((nint)layer.LayerName) == required)
                {
                    Logger.Info?.WriteLine($"Found support for validation layer: {required}");
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Logger.Warning?.WriteLine($"Validation layer not supported: {required}");
                return false;
            }
        }

        return true;
    }

    private string[] GetRequiredInstanceExtensions()
    {
        byte** extensions = _window.VkSurface!.GetRequiredExtensions(out uint extensionCount);

        string[] requiredExtensions = new string[extensionCount];
        for (int i = 0; i < extensionCount; i++)
        {
            requiredExtensions[i] = SilkMarshal.PtrToString((nint)extensions[i])!;
        }

        if (EnableValidationLayers)
        {
            Array.Resize(ref requiredExtensions, requiredExtensions.Length + 1);
            requiredExtensions[^1] = ExtDebugUtils.ExtensionName;
        }

        return requiredExtensions;
    }

    private bool CheckExtensionSupport(string[] required)
    {
        Span<uint> propertyCount = stackalloc uint[1];
        _vk.EnumerateInstanceExtensionProperties(
            ReadOnlySpan<byte>.Empty,
            propertyCount,
            Span<ExtensionProperties>.Empty
        );

        ExtensionProperties[] extensionProperties = new ExtensionProperties[propertyCount[0]];
        _vk.EnumerateInstanceExtensionProperties(
            ReadOnlySpan<byte>.Empty,
            propertyCount,
            extensionProperties.AsSpan()
        );

        foreach (string requiredName in required)
        {
            bool found = false;

            foreach (ExtensionProperties prop in extensionProperties)
            {
                if (SilkMarshal.PtrToString((nint)prop.ExtensionName) == requiredName)
                {
                    Logger.Info?.WriteLine($"Found support for extension: {required}");
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Logger.Warning?.WriteLine($"Extension not supported: {required}");
                return false;
            }
        }

        return true;
    }

    private void SetupDebugMessenger()
    {
        if (!EnableValidationLayers)
            return;

        DebugUtilsMessageSeverityFlagsEXT severityFlags =
            DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt
            | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt
            | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;

        DebugUtilsMessageTypeFlagsEXT messageTypeFlags =
            DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
            | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt
            | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;

        DebugUtilsMessengerCreateInfoEXT debugUtilsMessengerCreateInfoExt = new()
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = severityFlags,
            MessageType = messageTypeFlags,
            PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT(DebugCallback),
        };

        if (!_vk.TryGetInstanceExtension(_instance, out _debugUtils))
            throw new Exception("Failed to get ExtDebugUtils extension");

        if (
            _debugUtils.CreateDebugUtilsMessenger(
                _instance,
                &debugUtilsMessengerCreateInfoExt,
                null,
                out _debugMessenger
            ) != Result.Success
        )
        {
            throw new Exception("Failed to create debug messenger");
        }
    }

    private static uint DebugCallback(
        DebugUtilsMessageSeverityFlagsEXT severity,
        DebugUtilsMessageTypeFlagsEXT type,
        DebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData
    )
    {
        Logger.Error?.WriteLine(
            $"validation layer: type {type.ToString()} msg: {Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage)}"
        );

        return Vk.False;
    }

    private void CreateSurface()
    {
        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface))
            throw new Exception("Failed to get KhrSurface extension");

        _surfaceKhr = _window
            .VkSurface!.Create<AllocationCallbacks>(_instance.ToHandle(), null)
            .ToSurface();
    }

    private void PickPhysicalDevice()
    {
        IReadOnlyCollection<PhysicalDevice>? devices = _vk.GetPhysicalDevices(_instance);
        if (devices.Count == 0)
            throw new Exception("failed to find GPUs with Vulkan support!");

        Logger.Info?.WriteLine("Checking for suitable physical devices...");
        foreach (PhysicalDevice device in devices)
        {
            if (IsPhysicalDeviceSuitable(device))
            {
                _vk.GetPhysicalDeviceProperties(device, out PhysicalDeviceProperties properties);
                Logger.Info?.WriteLine("Found suitable physical device, listing properties...");
                Logger.Metric?.WriteLine(
                    $"Device Name: {SilkMarshal.PtrToString((nint)properties.DeviceName)}"
                );
                Logger.Metric?.WriteLine($"Device Type: {properties.DeviceType.ToString()}");
                Logger.Metric?.WriteLine($"Device Vendor ID: {properties.VendorID}");
                Logger.Metric?.WriteLine($"Device ID: {properties.DeviceID}");

                _physicalDevice = device;
                return;
            }
        }

        throw new Exception("failed to find a suitable GPU!");
    }

    private bool IsPhysicalDeviceSuitable(PhysicalDevice device)
    {
        Logger.Info?.WriteLine("Checking if physical device is suitable...");

        bool hasVersion = _vk.GetPhysicalDeviceProperties(device).ApiVersion >= Vk.Version13;
        Logger.Metric?.WriteLine("Physical device vulkan version is greater than Vk13 (Passed)");

        Span<uint> queueFamilyCount = stackalloc uint[1];
        _vk.GetPhysicalDeviceQueueFamilyProperties(
            device,
            queueFamilyCount,
            Span<QueueFamilyProperties>.Empty
        );

        QueueFamilyProperties[] queueFamilies = new QueueFamilyProperties[queueFamilyCount[0]];
        _vk.GetPhysicalDeviceQueueFamilyProperties(
            device,
            queueFamilyCount,
            queueFamilies.AsSpan()
        );

        bool hasGraphicsQueue = queueFamilies.Any(queueFamily =>
            (queueFamily.QueueFlags & QueueFlags.GraphicsBit) != 0
        );
        Logger.Metric?.WriteLine("Physical device has graphics queue (Passed)");

        Span<uint> extensionCount = stackalloc uint[1];
        _vk.EnumerateDeviceExtensionProperties(
            device,
            ReadOnlySpan<byte>.Empty,
            extensionCount,
            Span<ExtensionProperties>.Empty
        );

        ExtensionProperties[] extensions = new ExtensionProperties[extensionCount[0]];
        _vk.EnumerateDeviceExtensionProperties(
            device,
            ReadOnlySpan<byte>.Empty,
            extensionCount,
            extensions.AsSpan()
        );

        bool hasExtensions = DeviceExtensions.All(extensionName =>
            extensions.Any(extension =>
                SilkMarshal.PtrToString((nint)extension.ExtensionName) == extensionName
            )
        );
        Logger.Metric?.WriteLine("Physical device extensions are valid (Passed)");

        return hasVersion && hasGraphicsQueue && hasExtensions;
    }

    private (uint graphics, uint present) FindPhysicalDeviceQueueFamilies(PhysicalDevice device)
    {
        Span<uint> queueFamilyCount = stackalloc uint[1];
        _vk.GetPhysicalDeviceQueueFamilyProperties(
            device,
            queueFamilyCount,
            Span<QueueFamilyProperties>.Empty
        );

        QueueFamilyProperties[] queueFamilies = new QueueFamilyProperties[queueFamilyCount[0]];
        _vk.GetPhysicalDeviceQueueFamilyProperties(
            device,
            queueFamilyCount,
            queueFamilies.AsSpan()
        );

        uint? graphicsIndex = null;
        uint? presentIndex = null;

        for (uint i = 0; i < queueFamilies.Length; i++)
        {
            if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
                graphicsIndex = i;

            _khrSurface.GetPhysicalDeviceSurfaceSupport(
                device,
                i,
                _surfaceKhr,
                out var presentSupport
            );
            if (presentSupport)
                presentIndex = i;

            if (graphicsIndex.HasValue && presentIndex.HasValue)
                break;
        }

        if (!graphicsIndex.HasValue || !presentIndex.HasValue)
            throw new Exception("Failed to find a graphics queue family!");

        return (graphicsIndex.Value, presentIndex.Value);
    }

    private void CreateLogicalDevice()
    {
        (uint graphicsIndex, uint presentIndex) = FindPhysicalDeviceQueueFamilies(_physicalDevice);
        float queuePriority = 0.5f;

        PhysicalDeviceFeatures deviceFeatures = new();

        PhysicalDeviceExtendedDynamicStateFeaturesEXT physicalDeviceExtendedDynamicStateFeaturesExt =
            new()
            {
                SType = StructureType.PhysicalDeviceExtendedDynamicStateFeaturesExt,
                ExtendedDynamicState = true,
            };

        PhysicalDeviceVulkan13Features physicalDeviceVulkan13Features = new()
        {
            SType = StructureType.PhysicalDeviceVulkan13Features,
            DynamicRendering = true,
            PNext = &physicalDeviceExtendedDynamicStateFeaturesExt,
        };

        PhysicalDeviceFeatures2 physicalDeviceFeatures2 = new()
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            PNext = &physicalDeviceVulkan13Features,
        };

        DeviceCreateInfo deviceCreateInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            PNext = &physicalDeviceFeatures2,
            QueueCreateInfoCount = 1,
            EnabledExtensionCount = (uint)DeviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(DeviceExtensions),
        };

        HashSet<uint> uniqueIndices = [graphicsIndex, presentIndex];

        uint[] indices = uniqueIndices.ToArray();
        DeviceQueueCreateInfo[] queueCreateInfos = new DeviceQueueCreateInfo[indices.Length];

        for (int i = 0; i < indices.Length; i++)
        {
            queueCreateInfos[i] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = indices[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority,
            };
        }

        fixed (DeviceQueueCreateInfo* pQueueCreateInfos = queueCreateInfos)
        {
            deviceCreateInfo.QueueCreateInfoCount = (uint)queueCreateInfos.Length;
            deviceCreateInfo.PQueueCreateInfos = pQueueCreateInfos;

            if (
                _vk.CreateDevice(_physicalDevice, &deviceCreateInfo, null, out _device)
                != Result.Success
            )
                throw new Exception("Failed to create logical device");
        }

        _graphicsQueue = _vk.GetDeviceQueue(_device, graphicsIndex, 0);
        _presentQueue = _vk.GetDeviceQueue(_device, presentIndex, 0);

        //Cleanup
        SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledExtensionNames);
    }

    private void CreateSwapChain()
    {
        // Surface Capabilities
        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(
            _physicalDevice,
            _surfaceKhr,
            out SurfaceCapabilitiesKHR surfaceCapabilities
        );

        // Surface Format
        Span<uint> surfaceFormatCount = stackalloc uint[1];
        _khrSurface.GetPhysicalDeviceSurfaceFormats(
            _physicalDevice,
            _surfaceKhr,
            surfaceFormatCount,
            Span<SurfaceFormatKHR>.Empty
        );

        SurfaceFormatKHR[] availableSurfaceFormats = new SurfaceFormatKHR[surfaceFormatCount[0]];
        _khrSurface.GetPhysicalDeviceSurfaceFormats(
            _physicalDevice,
            _surfaceKhr,
            surfaceFormatCount,
            availableSurfaceFormats.AsSpan()
        );

        SurfaceFormatKHR swapChainSurfaceFormat = ChooseSwapSurfaceFormat(availableSurfaceFormats);

        // SwapChain Extent
        Extent2D swapChainExtent = ChooseSwapExtent(surfaceCapabilities);

        // Image
        Span<uint> imageCount = stackalloc uint[1];
        imageCount[0] = surfaceCapabilities.MaxImageCount + 1;

        if (
            surfaceCapabilities.MaxImageCount > 0
            && imageCount[0] > surfaceCapabilities.MaxImageCount
        )
            imageCount[0] = surfaceCapabilities.MaxImageCount;

        //Preset Modes
        Span<uint> presentModeCount = stackalloc uint[1];
        _khrSurface.GetPhysicalDeviceSurfacePresentModes(
            _physicalDevice,
            _surfaceKhr,
            presentModeCount,
            Span<PresentModeKHR>.Empty
        );

        PresentModeKHR[] availablePresentModes = new PresentModeKHR[presentModeCount[0]];
        _khrSurface.GetPhysicalDeviceSurfacePresentModes(
            _physicalDevice,
            _surfaceKhr,
            presentModeCount,
            availablePresentModes.AsSpan()
        );

        // Generate creation info
        SwapchainCreateInfoKHR swapchainCreateInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surfaceKhr,
            MinImageCount = imageCount[0],
            ImageFormat = swapChainSurfaceFormat.Format,
            ImageColorSpace = swapChainSurfaceFormat.ColorSpace,
            ImageExtent = swapChainExtent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = surfaceCapabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = ChooseSwapPresentMode(availablePresentModes),
            Clipped = true,
            OldSwapchain = default, // gives an error randomly idk why, DO NOT TOUCH
        };

        (uint graphicsIndex, uint presentIndex) = FindPhysicalDeviceQueueFamilies(_physicalDevice);
        uint* queueFamilyIndices = stackalloc[] { graphicsIndex, presentIndex };

        if (graphicsIndex != presentIndex)
        {
            swapchainCreateInfo.ImageSharingMode = SharingMode.Concurrent;
            swapchainCreateInfo.QueueFamilyIndexCount = 2;
            swapchainCreateInfo.PQueueFamilyIndices = queueFamilyIndices;
        }
        else
        {
            swapchainCreateInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        // Use the extension for creation
        if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapChain))
            throw new Exception("Failed to get KhrSwapchain extension");

        // And, finally, create it
        if (
            _khrSwapChain.CreateSwapchain(_device, &swapchainCreateInfo, null, out _swapChain)
            != Result.Success
        )
            throw new Exception("Failed to create Swapchain");

        _khrSwapChain.GetSwapchainImages(_device, _swapChain, imageCount, Span<Image>.Empty);

        _swapChainImages = new Image[imageCount[0]];
        _khrSwapChain.GetSwapchainImages(
            _device,
            _swapChain,
            imageCount,
            _swapChainImages.AsSpan()
        );

        _swapChainImageFormat = swapChainSurfaceFormat.Format;
        _swapChainExtent = swapChainExtent;
    }

    private SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
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

    private PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
    {
        foreach (PresentModeKHR availablePresentMode in availablePresentModes)
        {
            if (availablePresentMode is PresentModeKHR.MailboxKhr)
                return availablePresentMode;
        }

        return PresentModeKHR.FifoKhr;
    }

    private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;

        int width = _window.FramebufferSize.X;
        int height = _window.FramebufferSize.Y;

        return new Extent2D(
            (uint)
                Math.Clamp(
                    width,
                    capabilities.MinImageExtent.Width,
                    capabilities.MaxImageExtent.Width
                ),
            (uint)
                Math.Clamp(
                    height,
                    capabilities.MinImageExtent.Height,
                    capabilities.MaxImageExtent.Height
                )
        );
    }

    private void CreateSwapChainImageViews()
    {
        _swapChainImageViews = new ImageView[_swapChainImages.Length];

        ImageViewCreateInfo imageViewCreateInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            ViewType = ImageViewType.Type2D,
            Format = _swapChainImageFormat,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
        };

        for (int i = 0; i < _swapChainImages.Length; i++)
        {
            imageViewCreateInfo.Image = _swapChainImages[i];

            if (
                _vk.CreateImageView(
                    _device,
                    &imageViewCreateInfo,
                    null,
                    out _swapChainImageViews[i]
                ) != Result.Success
            )
                throw new Exception("Failed to create ImageView for SwapChain");
        }
    }

    private void MainLoop()
    {
        _window.Run();
    }

    public void Dispose()
    {
        // DO NOT change the order of disposal, as it's very important!

        Logger.Debug?.WriteLine("Application closed, disposing...");

        if (EnableValidationLayers)
        {
            _debugUtils.DestroyDebugUtilsMessenger(_instance, _debugMessenger, null);
            _debugUtils.Dispose();
        }

        foreach (var imageView in _swapChainImageViews)
        {
            _vk.DestroyImageView(_device, imageView, null);
        }

        _khrSwapChain.DestroySwapchain(_device, _swapChain, null);
        _khrSwapChain.Dispose();

        _khrSurface.DestroySurface(_instance, _surfaceKhr, null);
        _khrSurface.Dispose();

        _vk.DestroyDevice(_device, null);
        _vk.DestroyInstance(_instance, null);
        _vk.Dispose();
        _window.Dispose();

        Logger.Debug?.WriteLine("Application successfully disposed, exiting...");

        // Cute warning for the damn carbage collector so it stops shouting at me and shuts up
        GC.SuppressFinalize(this);
    }
}
