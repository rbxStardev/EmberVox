using System.Runtime.InteropServices;
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
        _vk = Vk.GetApi();
        CreateInstance();
        SetupDebugMessenger();
        CreateSurface();
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateSwapChain();
        CreateSwapChainImageViews();
    }

    private void CreateInstance()
    {
        // Enable validation layers
        if (EnableValidationLayers && !CheckValidationLayerSupport())
            throw new Exception("Validation layer(s) not supported");

        // Using "(byte**)SilkMarshal.StringToPtr("string");" to turn strings to pointers since vulkan structs work with pointers/unmanaged types!
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
        uint layerCount;
        _vk.EnumerateInstanceLayerProperties(&layerCount, null);

        LayerProperties[] availableLayers = new LayerProperties[layerCount];

        // Since vulkan loves working with pointers, we make a new pointer with the adress of the variable we want to use using:
        // fixed(pointerType* pointer = variable)
        // We use fixed so we can pin the variable in memory while we're using it!
        // So basically now we have a pointer variable "pointing" to our original variable!
        fixed (LayerProperties* pAvailableLayers = availableLayers)
            _vk.EnumerateInstanceLayerProperties(&layerCount, pAvailableLayers);

        foreach (string required in ValidationLayers)
        {
            bool found = false;
            foreach (LayerProperties layer in availableLayers)
            {
                // You might notice we dont cleanup after using a marshal function here,
                // this is because we're just converting an already existing pointer into something else we want to use!
                if (SilkMarshal.PtrToString((nint)layer.LayerName) == required)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Console.WriteLine($"Validation layer not supported: {required}");
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
        uint propertyCount = 0;
        _vk.EnumerateInstanceExtensionProperties((byte*)null, &propertyCount, null);

        ExtensionProperties[] extensionProperties = new ExtensionProperties[propertyCount];
        fixed (ExtensionProperties* pExtensionProperties = extensionProperties)
        {
            _vk.EnumerateInstanceExtensionProperties(
                (byte*)null,
                &propertyCount,
                pExtensionProperties
            );
        }

        foreach (string requiredName in required)
        {
            bool found = false;

            foreach (ExtensionProperties prop in extensionProperties)
            {
                if (SilkMarshal.PtrToString((nint)prop.ExtensionName) == requiredName)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
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
        Console.Error.WriteLine(
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

        foreach (PhysicalDevice device in devices)
        {
            if (IsPhysicalDeviceSuitable(device))
            {
                _physicalDevice = device;
                return;
            }
        }

        throw new Exception("failed to find a suitable GPU!");
    }

    private bool IsPhysicalDeviceSuitable(PhysicalDevice device)
    {
        bool hasVersion = _vk.GetPhysicalDeviceProperties(device).ApiVersion >= Vk.Version13;

        uint queueFamilyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

        QueueFamilyProperties[] queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* pQueueFamilies = queueFamilies)
            _vk.GetPhysicalDeviceQueueFamilyProperties(
                device,
                ref queueFamilyCount,
                pQueueFamilies
            );

        bool hasGraphicsQueue = queueFamilies.Any(queueFamily =>
            (queueFamily.QueueFlags & QueueFlags.GraphicsBit) != 0
        );

        uint extensionCount = 0;
        _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, null);

        ExtensionProperties[] extensions = new ExtensionProperties[extensionCount];
        fixed (ExtensionProperties* pExtensions = extensions)
            _vk.EnumerateDeviceExtensionProperties(
                device,
                (byte*)null,
                ref extensionCount,
                pExtensions
            );

        bool hasExtensions = DeviceExtensions.All(extensionName =>
            extensions.Any(extension =>
                SilkMarshal.PtrToString((nint)extension.ExtensionName) == extensionName
            )
        );

        return hasVersion && hasGraphicsQueue && hasExtensions;
    }

    private (uint graphics, uint present) FindPhysicalDeviceQueueFamilies(PhysicalDevice device)
    {
        uint queueFamilyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

        QueueFamilyProperties[] queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* pQueueFamilies = queueFamilies)
            _vk.GetPhysicalDeviceQueueFamilyProperties(
                device,
                ref queueFamilyCount,
                pQueueFamilies
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
        uint surfaceFormatCount = 0;
        _khrSurface.GetPhysicalDeviceSurfaceFormats(
            _physicalDevice,
            _surfaceKhr,
            ref surfaceFormatCount,
            null
        );

        SurfaceFormatKHR[] availableSurfaceFormats = new SurfaceFormatKHR[surfaceFormatCount];
        fixed (SurfaceFormatKHR* pAvailableSurfaceFormats = availableSurfaceFormats)
            _khrSurface.GetPhysicalDeviceSurfaceFormats(
                _physicalDevice,
                _surfaceKhr,
                ref surfaceFormatCount,
                pAvailableSurfaceFormats
            );

        SurfaceFormatKHR swapChainSurfaceFormat = ChooseSwapSurfaceFormat(availableSurfaceFormats);

        // SwapChain Extent
        Extent2D swapChainExtent = ChooseSwapExtent(surfaceCapabilities);

        // Image
        uint imageCount = surfaceCapabilities.MaxImageCount + 1;

        if (surfaceCapabilities.MaxImageCount > 0 && imageCount > surfaceCapabilities.MaxImageCount)
            imageCount = surfaceCapabilities.MaxImageCount;

        //Preset Modes
        uint presentModeCount = 0;
        _khrSurface.GetPhysicalDeviceSurfacePresentModes(
            _physicalDevice,
            _surfaceKhr,
            ref presentModeCount,
            null
        );

        PresentModeKHR[] availablePresentModes = new PresentModeKHR[presentModeCount];
        fixed (PresentModeKHR* pPresentModes = availablePresentModes)
            _khrSurface.GetPhysicalDeviceSurfacePresentModes(
                _physicalDevice,
                _surfaceKhr,
                ref presentModeCount,
                pPresentModes
            );

        // Generate creation info
        SwapchainCreateInfoKHR swapchainCreateInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surfaceKhr,
            MinImageCount = imageCount,
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

        _khrSwapChain.GetSwapchainImages(_device, _swapChain, ref imageCount, null);

        _swapChainImages = new Image[imageCount];
        fixed (Image* pSwapChainImages = _swapChainImages)
            _khrSwapChain.GetSwapchainImages(_device, _swapChain, ref imageCount, pSwapChainImages);

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

        // Cute warning for the damn carbage collector so it stops shouting at me and shuts up
        GC.SuppressFinalize(this);
    }
}
