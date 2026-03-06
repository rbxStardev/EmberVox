using EmberVox.Core.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace EmberVox.Rendering.Contexts;

internal sealed class DeviceContext : IDisposable
{
    public PhysicalDevice PhysicalDevice { get; }
    public Device LogicalDevice { get; }
    public QueueFamily GraphicsQueue { get; }
    public QueueFamily PresentQueue { get; }

    private static readonly string[] DeviceExtensions = [KhrSwapchain.ExtensionName];

    private readonly Vk _vk;
    private readonly Instance _instance;
    private readonly SurfaceContext _surfaceContext;
    private readonly PhysicalDeviceMemoryProperties _memoryProperties;

    public DeviceContext(Vk vk, Instance instance, SurfaceContext surfaceContext)
    {
        _vk = vk;
        _instance = instance;
        _surfaceContext = surfaceContext;

        PhysicalDevice = PickPhysicalDevice();

        (uint graphicsQueueIndex, uint presentQueueIndex) = FindPhysicalDeviceQueueFamilies(
            PhysicalDevice
        );

        LogicalDevice = CreateLogicalDevice(graphicsQueueIndex, presentQueueIndex);
        GraphicsQueue = new QueueFamily()
        {
            Index = graphicsQueueIndex,
            Queue = _vk.GetDeviceQueue(LogicalDevice, graphicsQueueIndex, 0),
        };
        PresentQueue = new QueueFamily()
        {
            Index = presentQueueIndex,
            Queue = _vk.GetDeviceQueue(LogicalDevice, presentQueueIndex, 0),
        };

        _memoryProperties = _vk.GetPhysicalDeviceMemoryProperties(PhysicalDevice);
    }

    public void Dispose()
    {
        _vk.DestroyDevice(LogicalDevice, ReadOnlySpan<AllocationCallbacks>.Empty);
        GC.SuppressFinalize(this);
    }

    public uint GetMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        for (int i = 0; i < _memoryProperties.MemoryTypeCount; i++)
        {
            if (
                (typeFilter & (uint)(1 << i)) != 0
                && (_memoryProperties.MemoryTypes[i].PropertyFlags & properties) == properties
            )
                return (uint)i;
        }

        throw new Exception("Failed to find suitable memory type!");
    }

    private unsafe PhysicalDevice PickPhysicalDevice()
    {
        IReadOnlyCollection<PhysicalDevice>? devices = _vk.GetPhysicalDevices(_instance);
        if (devices.Count == 0)
            throw new Exception("Failed to find GPUs with Vulkan support!");

        Logger.Info?.WriteLine("Checking for suitable physical devices...");
        foreach (PhysicalDevice device in devices)
        {
            if (!IsPhysicalDeviceSuitable(device))
                continue;

            _vk.GetPhysicalDeviceProperties(device, out PhysicalDeviceProperties properties);
            Logger.Info?.WriteLine("Found suitable physical device, listing properties...");
            Logger.Metric?.WriteLine(
                $"Device Name: {SilkMarshal.PtrToString((nint)properties.DeviceName)}"
            );
            Logger.Metric?.WriteLine($"Device Type: {properties.DeviceType}");
            Logger.Metric?.WriteLine($"Device Vendor ID: {properties.VendorID}");
            Logger.Metric?.WriteLine($"Device ID: {properties.DeviceID}");

            return device;
        }

        throw new Exception("Failed to find a suitable GPU!");
    }

    private unsafe bool IsPhysicalDeviceSuitable(PhysicalDevice device)
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

        bool hasGraphicsQueue = queueFamilies.Any(q =>
            (q.QueueFlags & QueueFlags.GraphicsBit) != 0
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

        bool hasExtensions = DeviceExtensions.All(name =>
            extensions.Any(e => SilkMarshal.PtrToString((nint)e.ExtensionName) == name)
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

            _surfaceContext.KhrSurfaceExtension.GetPhysicalDeviceSurfaceSupport(
                device,
                i,
                _surfaceContext.SurfaceKhr,
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

    private unsafe Device CreateLogicalDevice(uint graphicsIndex, uint presentIndex)
    {
        float queuePriority = 0.5f;

        PhysicalDeviceExtendedDynamicStateFeaturesEXT extendedDynamicState = new()
        {
            SType = StructureType.PhysicalDeviceExtendedDynamicStateFeaturesExt,
            ExtendedDynamicState = true,
        };

        PhysicalDeviceVulkan11Features vulkan11Features = new()
        {
            SType = StructureType.PhysicalDeviceVulkan11Features,
            ShaderDrawParameters = Vk.True,
            PNext = &extendedDynamicState,
        };

        PhysicalDeviceVulkan13Features vulkan13Features = new()
        {
            SType = StructureType.PhysicalDeviceVulkan13Features,
            DynamicRendering = true,
            Synchronization2 = true,
            PNext = &vulkan11Features,
        };

        PhysicalDeviceFeatures2 deviceFeatures2 = new()
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            PNext = &vulkan13Features,
        };

        DeviceCreateInfo deviceCreateInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            PNext = &deviceFeatures2,
            EnabledExtensionCount = (uint)DeviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(DeviceExtensions),
        };

        HashSet<uint> uniqueIndices = [graphicsIndex, presentIndex];
        uint[] indices = uniqueIndices.ToArray();
        DeviceQueueCreateInfo[] queueCreateInfos = new DeviceQueueCreateInfo[indices.Length];

        for (int i = 0; i < indices.Length; i++)
        {
            queueCreateInfos[i] = new DeviceQueueCreateInfo()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = indices[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority,
            };
        }

        Device logicalDevice;
        fixed (DeviceQueueCreateInfo* pQueueCreateInfos = queueCreateInfos)
        {
            deviceCreateInfo.QueueCreateInfoCount = (uint)queueCreateInfos.Length;
            deviceCreateInfo.PQueueCreateInfos = pQueueCreateInfos;

            if (
                _vk.CreateDevice(PhysicalDevice, &deviceCreateInfo, null, out logicalDevice)
                != Result.Success
            )
                throw new Exception("Failed to create logical device");
        }

        SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledExtensionNames);

        return logicalDevice;
    }
}
