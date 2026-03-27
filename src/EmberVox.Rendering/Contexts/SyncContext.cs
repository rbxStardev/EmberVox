using EmberVox.Rendering.ResourceManagement;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace EmberVox.Rendering.Contexts;

public sealed class SyncContext : IResource
{
    private readonly DeviceContext _deviceContext;
    private readonly uint _maxFramesInFlight;
    private readonly SwapChainContext _swapChainContext;

    public SyncContext(
        DeviceContext deviceContext,
        SwapChainContext swapChainContext,
        uint maxFramesInFlight
    )
    {
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;
        _maxFramesInFlight = maxFramesInFlight;

        PresentCompleteSemaphores = new Semaphore[_maxFramesInFlight];
        RenderFinishedSemaphores = new Semaphore[_swapChainContext.SwapChainImages.Length];
        InFlightFences = new Fence[_maxFramesInFlight];

        CreateSyncObjects();
    }

    public Semaphore[] PresentCompleteSemaphores { get; }
    public Semaphore[] RenderFinishedSemaphores { get; }
    public Fence[] InFlightFences { get; }

    public void Dispose()
    {
        foreach (var fence in InFlightFences)
            _deviceContext.Api.DestroyFence(
                _deviceContext.LogicalDevice,
                fence,
                ReadOnlySpan<AllocationCallbacks>.Empty
            );
        foreach (var semaphore in RenderFinishedSemaphores)
            _deviceContext.Api.DestroySemaphore(
                _deviceContext.LogicalDevice,
                semaphore,
                ReadOnlySpan<AllocationCallbacks>.Empty
            );
        foreach (var semaphore in PresentCompleteSemaphores)
            _deviceContext.Api.DestroySemaphore(
                _deviceContext.LogicalDevice,
                semaphore,
                ReadOnlySpan<AllocationCallbacks>.Empty
            );

        GC.SuppressFinalize(this);
    }

    public Semaphore CreateSemaphore()
    {
        SemaphoreCreateInfo semaphoreInfo = new() { SType = StructureType.SemaphoreCreateInfo };

        Semaphore semaphore = default;
        if (
            _deviceContext.Api.CreateSemaphore(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<SemaphoreCreateInfo>(ref semaphoreInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<Semaphore>(ref semaphore)
            ) != Result.Success
        )
            throw new Exception("Failed to create semaphore");

        return semaphore;
    }

    public Fence CreateFence()
    {
        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };

        Fence fence = default;
        if (
            _deviceContext.Api.CreateFence(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<FenceCreateInfo>(ref fenceInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<Fence>(ref fence)
            ) != Result.Success
        )
            throw new Exception("Failed to create fence");

        return fence;
    }

    private void CreateSyncObjects()
    {
        for (int i = 0; i < _swapChainContext.SwapChainImages.Length; i++)
            RenderFinishedSemaphores[i] = CreateSemaphore();

        for (int i = 0; i < _maxFramesInFlight; i++)
        {
            PresentCompleteSemaphores[i] = CreateSemaphore();
            InFlightFences[i] = CreateFence();
        }
    }
}
