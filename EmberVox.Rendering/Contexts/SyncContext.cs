using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace EmberVox.Rendering.Contexts;

internal sealed class SyncContext : IDisposable
{
    public Semaphore[] PresentCompleteSemaphores { get; private set; }
    public Semaphore[] RenderFinishedSemaphores { get; private set; }
    public Fence[] InFlightFences { get; private set; }

    private readonly Vk _vk;
    private readonly DeviceContext _deviceContext;
    private readonly SwapChainContext _swapChainContext;
    private readonly uint _maxFramesInFlight;

    public SyncContext(
        Vk vk,
        DeviceContext deviceContext,
        SwapChainContext swapChainContext,
        uint maxFramesInFlight
    )
    {
        _vk = vk;
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;
        _maxFramesInFlight = maxFramesInFlight;

        PresentCompleteSemaphores = new Semaphore[_maxFramesInFlight];
        RenderFinishedSemaphores = new Semaphore[_swapChainContext.SwapChainImages.Length];
        InFlightFences = new Fence[_maxFramesInFlight];

        CreateSyncObjects();
    }

    public void Dispose()
    {
        foreach (Fence fence in InFlightFences)
            _vk.DestroyFence(
                _deviceContext.LogicalDevice,
                fence,
                ReadOnlySpan<AllocationCallbacks>.Empty
            );
        foreach (Semaphore semaphore in RenderFinishedSemaphores)
            _vk.DestroySemaphore(
                _deviceContext.LogicalDevice,
                semaphore,
                ReadOnlySpan<AllocationCallbacks>.Empty
            );
        foreach (Semaphore semaphore in PresentCompleteSemaphores)
            _vk.DestroySemaphore(
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
            _vk.CreateSemaphore(
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
            _vk.CreateFence(
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
