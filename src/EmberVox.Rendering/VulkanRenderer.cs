using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using EmberVox.Core.Extensions;
using EmberVox.Core.Logging;
using EmberVox.Platform;
using EmberVox.Rendering.Buffers;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.ResourceManagement;
using EmberVox.Rendering.Types;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Windowing;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace EmberVox.Rendering;

// TODO - HARDCODE IS DEAD, ABSTRACTION IS FUEL, THE API IS FULL.
// TODO - OBJECTIVE: ABSTRACT INTO FULL VULKAN API

public sealed class VulkanRenderer : IDisposable
{
    private static readonly string[] ValidationLayers = ["VK_LAYER_KHRONOS_validation"];

    private static readonly long StartTime = Stopwatch.GetTimestamp();

    private const int MaxFramesInFlight = 2;
#if DEBUG
    private const bool EnableValidationLayers = true;
#else
    private const bool EnableValidationLayers = false;
#endif

    private readonly WindowContext _windowContext;

    private readonly Instance _instance;
    private readonly DefaultDebugContext _debugContext;

    public ResourceManager ResourceManager { get; }

    public SurfaceContext SurfaceContext { get; }
    public DeviceContext DeviceContext { get; }
    public SwapChainContext SwapChainContext { get; }
    public CommandContext CommandContext { get; }
    public SyncContext SyncContext { get; }

    public DepthContext DepthContext { get; private set; }

    //private readonly BufferContext _vertexBuffer;
    //private readonly BufferContext _indexBuffer;
    public readonly List<BufferContext> UniformBuffers;

    private readonly Dictionary<Material, List<Mesh>> _meshesToRender = [];

    private int _frameIndex;
    private bool _frameBufferResized;

    public VulkanRenderer(WindowContext window)
    {
        Vk vk = Vk.GetApi();
        _windowContext = window;

        Logger.Info?.WriteLine("~ Initializing Vulkan... ~");
        Console.WriteLine();
        {
            Logger.Debug?.WriteLine("-----> Creating Instance... <-----");
            _instance = CreateInstance(vk);
            Logger.Debug?.WriteLine("-----> Instance creation: OK <-----");
            Console.WriteLine();

            Logger.Debug?.WriteLine("-----> Creating DebugMessenger... <-----");
            _debugContext = new DefaultDebugContext(vk, _instance);
            Logger.Debug?.WriteLine("-----> DebugMessenger creation: OK <-----");
            Console.WriteLine();

            Logger.Debug?.WriteLine("-----> Creating SurfaceContext... <-----");
            SurfaceContext = new SurfaceContext(vk, _instance, _windowContext.Handle);
            Logger.Debug?.WriteLine("-----> SurfaceContext creation: OK <-----");
            Console.WriteLine();

            Logger.Debug?.WriteLine("-----> Creating DeviceContext... <-----");
            DeviceContext = new DeviceContext(vk, _instance, SurfaceContext);
            Logger.Debug?.WriteLine("-----> DeviceContext creation: OK <-----");
            Console.WriteLine();

            Logger.Debug?.WriteLine("-----> Creating SwapChainContext... <-----");
            SwapChainContext = new SwapChainContext(
                SurfaceContext,
                DeviceContext,
                _windowContext.Handle,
                _instance
            );
            Logger.Debug?.WriteLine("-----> SwapChainContext creation: OK <-----");
            Console.WriteLine();
        }

        Logger.Info?.WriteLine("~ Vulkan successfully initialized. ~");
        Console.WriteLine();

        Logger.Info?.WriteLine("~ Initializing Graphics Pipeline... ~");
        Console.WriteLine();

        DepthContext = new DepthContext(DeviceContext, SwapChainContext);

        {
            Console.WriteLine();

            CommandContext = new CommandContext(DeviceContext, SwapChainContext, MaxFramesInFlight);
            SyncContext = new SyncContext(DeviceContext, SwapChainContext, MaxFramesInFlight);
        }

        Logger.Info?.WriteLine("~ Graphics Pipeline successfully initialized. ~");
        Console.WriteLine();

        Logger.Info?.WriteLine("~ Initializing Buffers... ~");
        Console.WriteLine();

        {
            // Uniforms

            UniformBuffers = [];
            CreateUniformBuffers();
        }

        Logger.Info?.WriteLine("~ Buffers successfully initialized. ~");
        Console.WriteLine();

        ResourceManager = new ResourceManager();

        PlugEvents();
    }

    public void Dispose()
    {
        Logger.Debug?.WriteLine("Application closed, disposing...");

        {
            ResourceManager.Dispose();

            foreach (BufferContext uniformBuffer in UniformBuffers)
            {
                uniformBuffer.Dispose();
                Logger.Debug?.WriteLine("-> Disposed UniformBuffer");
            }

            SyncContext.Dispose();
            Logger.Debug?.WriteLine("-> Disposed SyncContext");

            CommandContext.Dispose();
            Logger.Debug?.WriteLine("-> Disposed CommandContext");

            DepthContext.Dispose();

            SwapChainContext.Dispose();
            Logger.Debug?.WriteLine("-> Disposed SwapChainContext");

            DeviceContext.Dispose();
            Logger.Debug?.WriteLine("-> Disposed DeviceContext");

            SurfaceContext.Dispose();
            Logger.Debug?.WriteLine("-> Disposed SurfaceContext");
        }

        if (EnableValidationLayers)
        {
            _debugContext.Dispose();
            Logger.Debug?.WriteLine("-> Disposed DebugContext");
        }

        DeviceContext.Api.DestroyInstance(_instance, ReadOnlySpan<AllocationCallbacks>.Empty);
        Logger.Debug?.WriteLine("-> Disposed Vulkan Instance");

        DeviceContext.Api.Dispose();
        Logger.Debug?.WriteLine("-> Disposed Vulkan API");

        Logger.Debug?.WriteLine("Application successfully disposed, exiting...");

        GC.SuppressFinalize(this);
    }

    private void PlugEvents()
    {
        _windowContext.Handle.Render += WindowOnRender;
        _windowContext.Handle.FramebufferResize += WindowOnFramebufferResize;
    }

    public void MainLoop()
    {
        _windowContext.Handle.Run();

        DeviceContext.Api.DeviceWaitIdle(DeviceContext.LogicalDevice);
    }

    public void RegisterMaterial(Material material)
    {
        _meshesToRender[material] = [];
        ResourceManager.SubmitResource(material.GraphicsPipelineContext);
        ResourceManager.SubmitResource(material.Texture);
    }

    public void RegisterMesh(Mesh mesh, Material material)
    {
        _meshesToRender[material].Add(mesh);
        ResourceManager.SubmitResource(mesh);
    }

    public void WindowOnFramebufferResize(Vector2D<int> newSize) => _frameBufferResized = true;

    public unsafe void WindowOnRender(double deltaTime)
    {
        _windowContext.Handle.Title = $"EmberVox: Vulkan - {(int)(1.0 / deltaTime)} FPS";

        Semaphore presentCompleteSemaphore = SyncContext.PresentCompleteSemaphores[_frameIndex];
        Fence drawFence = SyncContext.InFlightFences[_frameIndex];
        CommandBuffer commandBuffer = CommandContext.CommandBuffers[_frameIndex];
        SwapchainKHR swapchain = SwapChainContext.SwapChainKhr;

        if (
            DeviceContext.Api.WaitForFences(
                DeviceContext.LogicalDevice,
                new ReadOnlySpan<Fence>(ref drawFence),
                true,
                ulong.MaxValue
            ) != Result.Success
        )
            throw new Exception("Failed to wait for fence!");

        uint imageIndex = 0;
        Result result = SwapChainContext.KhrSwapChainExtension.AcquireNextImage(
            DeviceContext.LogicalDevice,
            SwapChainContext.SwapChainKhr,
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

        Semaphore renderFinishedSemaphore = SyncContext.RenderFinishedSemaphores[imageIndex];

        DeviceContext.Api.ResetFences(
            DeviceContext.LogicalDevice,
            new ReadOnlySpan<Fence>(ref drawFence)
        );
        DeviceContext.Api.ResetCommandBuffer(commandBuffer, CommandBufferResetFlags.None);

        /*
        _commandContext.RecordCommandBuffer(
            _descriptorContext,
            imageIndex,
            _frameIndex,
            _vertexBuffer,
            _indexBuffer
        );
        */
        CommandContext.BeginCommandBufferRecording(DepthContext, imageIndex, _frameIndex);

        foreach (KeyValuePair<Material, List<Mesh>> keyValuePair in _meshesToRender)
        {
            DeviceContext.Api.CmdBindPipeline(
                commandBuffer,
                PipelineBindPoint.Graphics,
                keyValuePair.Key.GraphicsPipelineContext.GraphicsPipeline
            );

            DescriptorSet descriptorSet = keyValuePair
                .Key
                .GraphicsPipelineContext
                .DescriptorContext[(int)imageIndex];
            DeviceContext.Api.CmdBindDescriptorSets(
                commandBuffer,
                PipelineBindPoint.Graphics,
                keyValuePair.Key.GraphicsPipelineContext.PipelineLayout,
                0,
                new ReadOnlySpan<DescriptorSet>(ref descriptorSet),
                ReadOnlySpan<uint>.Empty
            );

            foreach (Mesh mesh in keyValuePair.Value)
            {
                Buffer vertexBuffer = mesh.VertexBuffer.Buffer;
                DeviceContext.Api.CmdBindVertexBuffers(
                    commandBuffer,
                    0,
                    new ReadOnlySpan<Buffer>(ref vertexBuffer),
                    new ReadOnlySpan<ulong>([0])
                );

                Buffer indexBuffer = mesh.IndexBuffer.Buffer;
                DeviceContext.Api.CmdBindIndexBuffer(
                    commandBuffer,
                    indexBuffer,
                    0,
                    IndexType.Uint32
                );

                DeviceContext.Api.CmdDrawIndexed(commandBuffer, mesh.IndexCount, 1, 0, 0, 0);
            }
        }

        CommandContext.EndCommandBufferRecording(imageIndex, _frameIndex);

        UpdateUniformBuffer((int)imageIndex, Quaternion.Identity, Vector3.Zero, Vector3.One, 70.0f);

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

        var submitResult = DeviceContext.Api.QueueSubmit(
            DeviceContext.GraphicsQueue.Queue,
            new ReadOnlySpan<SubmitInfo>(ref submitInfo),
            drawFence
        );
        //Logger.Info?.WriteLine($"QueueSubmit: {submitResult}");

        PresentInfoKHR presentInfoKhr = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &renderFinishedSemaphore,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex,
        };

        result = SwapChainContext.KhrSwapChainExtension.QueuePresent(
            DeviceContext.PresentQueue.Queue,
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
        while (
            _windowContext.Handle.FramebufferSize.X == 0
            || _windowContext.Handle.FramebufferSize.Y == 0
        )
            _windowContext.Handle.DoEvents();

        DeviceContext.Api.DeviceWaitIdle(DeviceContext.LogicalDevice);
        SwapChainContext.RecreateSwapChain();
        DepthContext.Dispose();
        DepthContext = new DepthContext(DeviceContext, SwapChainContext);
    }

    #region UniformBufferHandling

    private void CreateUniformBuffers()
    {
        ulong bufferSize = (ulong)Unsafe.SizeOf<UniformBufferObject>();

        for (int i = 0; i < SwapChainContext.SwapChainImages.Length; i++)
        {
            BufferContext uniformBuffer = new BufferContext(
                DeviceContext,
                bufferSize,
                BufferUsageFlags.UniformBufferBit
            );
            UniformBuffers.Add(uniformBuffer);
        }
    }

    private void UpdateUniformBuffer(
        int currentImage,
        Quaternion modelRotation,
        Vector3 modelPosition,
        Vector3 modelScale,
        float fieldOfView
    )
    {
        UniformBufferObject uniformBufferObject = new()
        {
            Model =
                Matrix4x4.CreateFromQuaternion(
                    Quaternion.CreateFromAxisAngle(Vector3.UnitY, float.Pi)
                )
                * Matrix4x4.CreateFromQuaternion(modelRotation)
                * Matrix4x4.CreateTranslation(modelPosition)
                * Matrix4x4.CreateScale(modelScale),
            View = Matrix4x4.CreateLookAt(-Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitY),
            Proj = Matrix4x4.CreatePerspectiveFieldOfView(
                float.DegreesToRadians(fieldOfView),
                (float)SwapChainContext.SwapChainExtent.Width
                    / SwapChainContext.SwapChainExtent.Height,
                0.1f,
                500.0f
            ),
        };
        var proj = uniformBufferObject.Proj;
        proj.M22 *= -1; // Flip Y (DO NOT TURN OFFFFFF)
        uniformBufferObject.Proj = proj;

        uniformBufferObject.AsBytes().CopyTo(UniformBuffers[currentImage].MappedMemory);
        /*Console.WriteLine(
            $"Updating UBO frame {currentImage}, time: {time}, content: {_uniformBuffers[currentImage].MappedMemory.ToString()}]"
        );*/
    }

    #endregion

    #region InstanceCreation

    private unsafe Instance CreateInstance(Vk vk)
    {
        Logger.Metric?.WriteLine(
            $"Checking for {ValidationLayers.Length} validation layer(s) support: {string.Join(", ", ValidationLayers)}"
        );

        if (EnableValidationLayers && !CheckValidationLayerSupport(vk))
            throw new Exception("Validation layer(s) not supported");

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

        if (!CheckExtensionSupport(vk, extensions))
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

        if (vk.CreateInstance(&createInfo, null, out Instance instance) != Result.Success)
            throw new Exception("Failed to create Vulkan instance");

        SilkMarshal.Free((nint)appInfo.PApplicationName);
        SilkMarshal.Free((nint)appInfo.PEngineName);
        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

        if (EnableValidationLayers)
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);

        return instance;
    }

    private unsafe bool CheckValidationLayerSupport(Vk vk)
    {
        Span<uint> layerCount = stackalloc uint[1];
        vk.EnumerateInstanceLayerProperties(layerCount, Span<LayerProperties>.Empty);

        LayerProperties[] availableLayers = new LayerProperties[layerCount[0]];
        vk.EnumerateInstanceLayerProperties(layerCount, availableLayers.AsSpan());

        foreach (string required in ValidationLayers)
        {
            if (
                availableLayers.All(layer =>
                    SilkMarshal.PtrToString((nint)layer.LayerName) != required
                )
            )
            {
                Logger.Warning?.WriteLine($"Validation layer not supported: {required}");
                return false;
            }

            Logger.Info?.WriteLine($"Found support for validation layer: {required}");
        }

        return true;
    }

    private unsafe string[] GetRequiredInstanceExtensions()
    {
        byte** extensions = _windowContext.Handle.VkSurface!.GetRequiredExtensions(
            out uint extensionCount
        );

        string[] requiredExtensions = new string[extensionCount];
        for (int i = 0; i < extensionCount; i++)
            requiredExtensions[i] = SilkMarshal.PtrToString((nint)extensions[i])!;

        if (EnableValidationLayers)
        {
            Array.Resize(ref requiredExtensions, requiredExtensions.Length + 1);
            requiredExtensions[^1] = ExtDebugUtils.ExtensionName;
        }

        return requiredExtensions;
    }

    private unsafe bool CheckExtensionSupport(Vk vk, string[] required)
    {
        Span<uint> propertyCount = stackalloc uint[1];
        vk.EnumerateInstanceExtensionProperties(
            ReadOnlySpan<byte>.Empty,
            propertyCount,
            Span<ExtensionProperties>.Empty
        );

        ExtensionProperties[] extensionProperties = new ExtensionProperties[propertyCount[0]];
        vk.EnumerateInstanceExtensionProperties(
            ReadOnlySpan<byte>.Empty,
            propertyCount,
            extensionProperties.AsSpan()
        );

        foreach (string requiredName in required)
        {
            if (
                extensionProperties.All(prop =>
                    SilkMarshal.PtrToString((nint)prop.ExtensionName) != requiredName
                )
            )
            {
                Logger.Warning?.WriteLine($"Extension not supported: {requiredName}");
                return false;
            }

            Logger.Info?.WriteLine($"Found support for extension: {requiredName}");
        }

        return true;
    }

    #endregion
}
