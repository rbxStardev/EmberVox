using EmberVox.Core.Logging;
using EmberVox.Core.Types;
using EmberVox.Rendering.ResourceManagement;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace EmberVox.Rendering.Contexts;

public sealed class DeviceContext : IResource
{
    private static readonly string[] DeviceExtensions = [KhrSwapchain.ExtensionName];

    private readonly Instance _instance;
    private readonly PhysicalDeviceMemoryProperties _memoryProperties;
    private readonly SurfaceContext _surfaceContext;

    public DeviceContext(Vk vk, Instance instance, SurfaceContext surfaceContext)
    {
        Api = vk;
        _instance = instance;
        _surfaceContext = surfaceContext;

        PhysicalDevice = PickPhysicalDevice();

        (uint graphicsQueueIndex, uint presentQueueIndex) = FindPhysicalDeviceQueueFamilies(
            PhysicalDevice
        );

        LogicalDevice = CreateLogicalDevice(graphicsQueueIndex, presentQueueIndex);
        GraphicsQueue = new QueueFamily
        {
            Index = graphicsQueueIndex,
            Queue = Api.GetDeviceQueue(LogicalDevice, graphicsQueueIndex, 0),
        };
        PresentQueue = new QueueFamily
        {
            Index = presentQueueIndex,
            Queue = Api.GetDeviceQueue(LogicalDevice, presentQueueIndex, 0),
        };

        _memoryProperties = Api.GetPhysicalDeviceMemoryProperties(PhysicalDevice);
    }

    public Vk Api { get; }
    public PhysicalDevice PhysicalDevice { get; }
    public Device LogicalDevice { get; }
    public QueueFamily GraphicsQueue { get; }
    public QueueFamily PresentQueue { get; }

    public void Dispose()
    {
        Api.DestroyDevice(LogicalDevice, ReadOnlySpan<AllocationCallbacks>.Empty);
        GC.SuppressFinalize(this);
    }

    public DeviceMemory AllocateMemory(
        MemoryRequirements memoryRequirements,
        MemoryPropertyFlags properties
    )
    {
        uint memoryTypeIndex = GetMemoryType(memoryRequirements.MemoryTypeBits, properties);

        MemoryAllocateInfo memoryAllocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memoryRequirements.Size,
            MemoryTypeIndex = memoryTypeIndex,
        };

        DeviceMemory imageMemory = default;
        if (
            Api.AllocateMemory(
                LogicalDevice,
                new ReadOnlySpan<MemoryAllocateInfo>(ref memoryAllocateInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<DeviceMemory>(ref imageMemory)
            ) != Result.Success
        )
            throw new Exception("Failed to allocate buffer memory");

        return imageMemory;
    }

    public uint GetMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        for (int i = 0; i < _memoryProperties.MemoryTypeCount; i++)
            if (
                // TODO - Make it so i dont have to cast uint twice, somehow
                (typeFilter & (uint)(1 << i)) != 0
                && (_memoryProperties.MemoryTypes[i].PropertyFlags & properties) == properties
            )
                return (uint)i;

        throw new Exception("Failed to find suitable memory type!");
    }

    public Format FindSupportedFormat(
        List<Format> candidates,
        ImageTiling tiling,
        FormatFeatureFlags formatFeatures
    )
    {
        foreach (var format in candidates)
        {
            FormatProperties properties = default;
            Api.GetPhysicalDeviceFormatProperties(
                PhysicalDevice,
                format,
                new Span<FormatProperties>(ref properties)
            );

            if (
                tiling == ImageTiling.Linear
                && (properties.LinearTilingFeatures & formatFeatures) == formatFeatures
            )
                return format;

            if (
                tiling == ImageTiling.Optimal
                && (properties.OptimalTilingFeatures & formatFeatures) == formatFeatures
            )
                return format;
        }

        throw new Exception("Failed to find supported format!");
    }

    private unsafe PhysicalDevice PickPhysicalDevice()
    {
        var devices = Api.GetPhysicalDevices(_instance);
        if (devices.Count == 0)
            throw new Exception("Failed to find GPUs with Vulkan support!");

        Logger.Info?.WriteLine("Checking for suitable physical devices...");
        foreach (var device in devices)
        {
            if (!IsPhysicalDeviceSuitable(device))
                continue;

            Api.GetPhysicalDeviceProperties(device, out var properties);
            Logger.Info?.WriteLine("Found suitable physical device, listing properties...");
            Logger.Metric?.WriteLine(
                $"-> Device Name: {SilkMarshal.PtrToString((nint)properties.DeviceName)}"
            );
            Logger.Metric?.WriteLine($"-> Device Type: {properties.DeviceType}");
            Logger.Metric?.WriteLine($"-> Device Vendor ID: {properties.VendorID}");
            Logger.Metric?.WriteLine($"-> Device ID: {properties.DeviceID}");

            return device;
        }

        throw new Exception("Failed to find a suitable GPU!");
    }

    private unsafe bool IsPhysicalDeviceSuitable(PhysicalDevice device)
    {
        Logger.Info?.WriteLine("Checking if physical device is suitable...");

        bool hasVersion = Api.GetPhysicalDeviceProperties(device).ApiVersion >= Vk.Version13;
        Logger.Metric?.WriteLine("Physical device vulkan version is greater than Vk13 (Passed)");

        Span<uint> queueFamilyCount = stackalloc uint[1];
        Api.GetPhysicalDeviceQueueFamilyProperties(
            device,
            queueFamilyCount,
            Span<QueueFamilyProperties>.Empty
        );

        var queueFamilies = new QueueFamilyProperties[queueFamilyCount[0]];
        Api.GetPhysicalDeviceQueueFamilyProperties(
            device,
            queueFamilyCount,
            queueFamilies.AsSpan()
        );

        bool hasGraphicsQueue = queueFamilies.Any(queue =>
            (queue.QueueFlags & QueueFlags.GraphicsBit) != 0
        );
        Logger.Metric?.WriteLine("Physical device has graphics queue (Passed)");

        Span<uint> extensionCount = stackalloc uint[1];
        Api.EnumerateDeviceExtensionProperties(
            device,
            ReadOnlySpan<byte>.Empty,
            extensionCount,
            Span<ExtensionProperties>.Empty
        );

        var extensions = new ExtensionProperties[extensionCount[0]];
        Api.EnumerateDeviceExtensionProperties(
            device,
            ReadOnlySpan<byte>.Empty,
            extensionCount,
            extensions.AsSpan()
        );

        bool hasExtensions = DeviceExtensions.All(name =>
            extensions.Any(extension =>
                SilkMarshal.PtrToString((nint)extension.ExtensionName) == name
            )
        );
        Logger.Metric?.WriteLine("Physical device extensions are valid (Passed)");

        var supportedFeatures = Api.GetPhysicalDeviceFeatures(device);

        return hasVersion
            && hasGraphicsQueue
            && hasExtensions
            && supportedFeatures.SamplerAnisotropy;
    }

    private (uint graphics, uint present) FindPhysicalDeviceQueueFamilies(PhysicalDevice device)
    {
        Span<uint> queueFamilyCount = stackalloc uint[1];
        Api.GetPhysicalDeviceQueueFamilyProperties(
            device,
            queueFamilyCount,
            Span<QueueFamilyProperties>.Empty
        );

        var queueFamilies = new QueueFamilyProperties[queueFamilyCount[0]];
        Api.GetPhysicalDeviceQueueFamilyProperties(
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
            Features = new PhysicalDeviceFeatures { SamplerAnisotropy = true },
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
        var queueCreateInfoArray = new DeviceQueueCreateInfo[indices.Length];

        for (int i = 0; i < indices.Length; i++)
            queueCreateInfoArray[i] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = indices[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority,
            };

        //fixed (DeviceQueueCreateInfo* pQueueCreateInfos = queueCreateInfos)
        //{
        using ManagedPointer<DeviceQueueCreateInfo> queueCreateInfo = new(
            queueCreateInfoArray.Length
        );
        queueCreateInfoArray.CopyTo(queueCreateInfo.Span);

        deviceCreateInfo.QueueCreateInfoCount = (uint)queueCreateInfo.Length;
        deviceCreateInfo.PQueueCreateInfos = queueCreateInfo.Pointer;

        Device logicalDevice = default;
        if (
            Api.CreateDevice(
                PhysicalDevice,
                new ReadOnlySpan<DeviceCreateInfo>(ref deviceCreateInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<Device>(ref logicalDevice)
            ) != Result.Success
        )
            throw new Exception("Failed to create logical device");
        //}

        SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledExtensionNames);

        return logicalDevice;
    }
}
