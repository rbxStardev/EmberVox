using System.Diagnostics;
using EmberVox.Logging;
using EmberVox.Rendering.Contexts;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace EmberVox.Rendering;

public sealed class VulkanRenderer : IDisposable
{
    private static readonly string[] ValidationLayers = ["VK_LAYER_KHRONOS_validation"];
    private const int MaxFramesInFlight = 2;
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

    private readonly GraphicsPipelineContext _graphicsPipelineContext;
    private readonly CommandContext _commandContext;
    private readonly SyncContext _syncContext;

    private int _frameIndex = 0;
    private bool _frameBufferResized = false;

    public VulkanRenderer(IWindow window)
    {
        _window = window;

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

        Logger.Debug?.WriteLine("-----> Creating SwapChainContext... <-----");
        _swapChainContext = new SwapChainContext(
            _vk,
            _surfaceContext,
            _deviceContext,
            _window,
            _instance
        );
        Logger.Debug?.WriteLine("-----> SwapChainContext creation: OK <-----");
        Console.WriteLine();

        Logger.Info?.WriteLine("~ Vulkan successfully initialized. ~");
        Console.WriteLine();

        Logger.Info?.WriteLine("~ Initializing Graphics Pipeline... ~");
        Console.WriteLine();

        _graphicsPipelineContext = new GraphicsPipelineContext(
            _vk,
            _deviceContext,
            _swapChainContext
        );
        Console.WriteLine();

        _commandContext = new CommandContext(
            _vk,
            _deviceContext,
            _swapChainContext,
            _graphicsPipelineContext,
            MaxFramesInFlight
        );

        _syncContext = new SyncContext(_vk, _deviceContext, _swapChainContext, MaxFramesInFlight);

        Logger.Info?.WriteLine("~ Graphics Pipeline successfully initialized. ~");
        Console.WriteLine();
    }

    public void MainLoop()
    {
        _window.Render += WindowOnRender;
        _window.FramebufferResize += WindowOnFramebufferResize;

        _window.Run();

        _vk.DeviceWaitIdle(_deviceContext.LogicalDevice);
    }

    private void WindowOnFramebufferResize(Vector2D<int> newSize)
    {
        _frameBufferResized = true;
    }

    private unsafe void WindowOnRender(double deltaTime)
    {
        int fps = (int)(1.0 / deltaTime);
        _window.Title = $"EmberVox: Vulkan - {fps} FPS";

        Semaphore presentCompleteSemaphore = _syncContext.PresentCompleteSemaphores[_frameIndex];
        Fence drawFence = _syncContext.InFlightFences[_frameIndex];

        CommandBuffer commandBuffer = _commandContext.CommandBuffers[_frameIndex];
        SwapchainKHR swapchain = _swapChainContext.SwapChainKhr;

        if (
            _vk.WaitForFences(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<Fence>(ref drawFence),
                true,
                ulong.MaxValue
            ) != Result.Success
        )
        {
            throw new Exception("Failed to wait for fence!");
        }

        uint imageIndex = 0;
        Result result = _swapChainContext.KhrSwapChainExtension.AcquireNextImage(
            _deviceContext.LogicalDevice,
            _swapChainContext.SwapChainKhr,
            ulong.MaxValue,
            presentCompleteSemaphore,
            default,
            ref imageIndex
        );
        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapChain();
            return;
        }

        if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            Debug.Assert(result is Result.Timeout or Result.NotReady);
            throw new Exception("Failed to acquire swap chain image!");
        }

        Semaphore renderFinishedSemaphore = _syncContext.RenderFinishedSemaphores[imageIndex];

        _vk.ResetFences(_deviceContext.LogicalDevice, new ReadOnlySpan<Fence>(ref drawFence));

        _vk.ResetCommandBuffer(commandBuffer, CommandBufferResetFlags.None);
        _commandContext.RecordCommandBuffer(imageIndex, _frameIndex);

        PipelineStageFlags waitDestinationStageMask = PipelineStageFlags.ColorAttachmentOutputBit;

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &presentCompleteSemaphore,
            PWaitDstStageMask = &waitDestinationStageMask,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &renderFinishedSemaphore,
        };

        _vk.QueueSubmit(
            _deviceContext.GraphicsQueue.Queue,
            new ReadOnlySpan<SubmitInfo>(ref submitInfo),
            drawFence
        );

        PresentInfoKHR presentInfoKhr = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &renderFinishedSemaphore,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex,
        };

        result = _swapChainContext.KhrSwapChainExtension.QueuePresent(
            _deviceContext.PresentQueue.Queue,
            ref presentInfoKhr
        );
        if (
            result == Result.ErrorOutOfDateKhr
            || result == Result.SuboptimalKhr
            || _frameBufferResized
        )
        {
            _frameBufferResized = false;
            RecreateSwapChain();
        }
        else
        {
            Debug.Assert(result == Result.Success);
        }

        _frameIndex = (_frameIndex + 1) % MaxFramesInFlight;
    }

    private void RecreateSwapChain()
    {
        while (_window.FramebufferSize.X == 0 || _window.FramebufferSize.Y == 0)
        {
            _window.DoEvents();
        }

        _vk.DeviceWaitIdle(_deviceContext.LogicalDevice);

        _swapChainContext.RecreateSwapChain();
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

    public void Dispose()
    {
        // DO NOT change the order of disposal, as it's very important!

        Logger.Debug?.WriteLine("Application closed, disposing...");

        if (EnableValidationLayers)
        {
            _debugContext.Dispose();
        }

        _syncContext.Dispose();
        _commandContext.Dispose();
        _graphicsPipelineContext.Dispose();

        _swapChainContext.Dispose();
        _surfaceContext.Dispose();
        _deviceContext.Dispose();

        _vk.DestroyInstance(_instance, ReadOnlySpan<AllocationCallbacks>.Empty);
        _vk.Dispose();

        Logger.Debug?.WriteLine("Application successfully disposed, exiting...");

        // Cute warning for the damn carbage collector so it stops shouting at me and shuts up
        GC.SuppressFinalize(this);
    }
}
