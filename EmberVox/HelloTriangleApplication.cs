using System.Runtime.InteropServices;
using EmberVox.Logging;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan.Extensions.EXT;

namespace EmberVox;

public class HelloTriangleApplication : IDisposable
{
    private const int WindowWidth = 800;
    private const int WindowHeight = 600;

    private static readonly string[] ValidationLayers = ["VK_LAYER_KHRONOS_validation"];

#if DEBUG
    private const bool EnableValidationLayers = true;
#else
    private const bool EnableValidationLayers = false;
#endif

    private readonly IWindow _window;

    private readonly Vk _vk;
    private readonly Instance _instance;

    private readonly DefaultDebugContext _debugContext;
    private readonly DeviceContext _deviceContext;
    private readonly SurfaceContext _surfaceContext;
    private readonly SwapChainContext _swapChainContext;

    public HelloTriangleApplication()
    {
        // Init Window

        WindowOptions options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(WindowWidth, WindowHeight),
            Title = "EmberVox: Vulkan",
            WindowBorder = WindowBorder.Fixed, // Basically makes it unresizable
        };

        _window = Window.Create(options);
        _window.Initialize();

        // Init Vulkan

        Logger.Info?.WriteLine("~ Initializing Vulkan... ~");
        Console.WriteLine();
        _vk = Vk.GetApi();

        Logger.Debug?.WriteLine("-----> Creating Instance... <-----");
        _instance = CreateInstance();
        Logger.Debug?.WriteLine("-----> Instance creation: OK <-----");
        Console.WriteLine();

        Logger.Debug?.WriteLine("-----> Creating DebugMessenger... <-----");
        _debugContext = new DefaultDebugContext(_vk, _instance);
        Logger.Debug?.WriteLine("-----> DebugMessenger creation: OK <-----");
        Console.WriteLine();

        Logger.Debug?.WriteLine("-----> Creating SurfaceContext... <-----");
        _surfaceContext = new SurfaceContext(_vk, _instance, _window);
        Logger.Debug?.WriteLine("-----> SurfaceContext creation: OK <-----");
        Console.WriteLine();

        Logger.Debug?.WriteLine("-----> Creating DeviceContext... <-----");
        _deviceContext = new DeviceContext(_vk, _instance, _surfaceContext);
        Logger.Debug?.WriteLine("-----> DeviceContext creation: OK <-----");
        Console.WriteLine();

        Logger.Debug?.WriteLine("-----> Creating SwapChain... <-----");
        _swapChainContext = new SwapChainContext(
            _vk,
            _surfaceContext,
            _deviceContext,
            _window,
            _instance
        );
        Logger.Debug?.WriteLine("-----> SwapChain creation: OK <-----");
        Console.WriteLine();

        Logger.Info?.WriteLine("~ Vulkan successfully initialized. ~");
        Console.WriteLine();

        MainLoop();
        Dispose();
    }

    private unsafe Instance CreateInstance()
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

        if (_vk.CreateInstance(&createInfo, null, out Instance instance) != Result.Success)
            throw new Exception("Failed to create Vulkan instance");

        // Cleanup
        SilkMarshal.Free((nint)appInfo.PApplicationName);
        SilkMarshal.Free((nint)appInfo.PEngineName);
        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

        if (EnableValidationLayers)
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);

        return instance;
    }

    private unsafe bool CheckValidationLayerSupport()
    {
        Span<uint> layerCount = stackalloc uint[1];
        _vk.EnumerateInstanceLayerProperties(layerCount, Span<LayerProperties>.Empty);

        LayerProperties[] availableLayers = new LayerProperties[layerCount[0]];

        _vk.EnumerateInstanceLayerProperties(layerCount, availableLayers.AsSpan());

        foreach (string required in ValidationLayers)
        {
            bool found = false;
            if (
                availableLayers.Any(layer =>
                    SilkMarshal.PtrToString((nint)layer.LayerName) == required
                )
            )
            {
                Logger.Info?.WriteLine($"Found support for validation layer: {required}");
                found = true;
            }

            if (found)
                continue;

            Logger.Warning?.WriteLine($"Validation layer not supported: {required}");
            return false;
        }

        return true;
    }

    private unsafe string[] GetRequiredInstanceExtensions()
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

    private unsafe bool CheckExtensionSupport(string[] required)
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

            if (
                extensionProperties.Any(prop =>
                    SilkMarshal.PtrToString((nint)prop.ExtensionName) == requiredName
                )
            )
            {
                Logger.Info?.WriteLine($"Found support for extension: {requiredName}");
                found = true;
            }

            if (found)
                continue;

            Logger.Warning?.WriteLine($"Extension not supported: {requiredName}");
            return false;
        }

        return true;
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
            _debugContext.Dispose();
        }

        _swapChainContext.Dispose();
        _surfaceContext.Dispose();
        _deviceContext.Dispose();

        _vk.DestroyInstance(_instance, ReadOnlySpan<AllocationCallbacks>.Empty);
        _vk.Dispose();
        _window.Dispose();

        Logger.Debug?.WriteLine("Application successfully disposed, exiting...");

        // Cute warning for the damn carbage collector so it stops shouting at me and shuts up
        GC.SuppressFinalize(this);
    }
}
