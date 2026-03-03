using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace EmberVox.Rendering.Contexts;

public class SyncContext : IDisposable
{
    public Semaphore PresentCompleteSemaphore { get; }
    public Semaphore RenderFinishedSemaphore { get; }
    public Fence DrawFence { get; }

    private readonly Vk _vk;
    private readonly DeviceContext _deviceContext;

    public SyncContext(Vk vk, DeviceContext deviceContext)
    {
        _vk = vk;
        _deviceContext = deviceContext;

        PresentCompleteSemaphore = CreateSemaphore();
        RenderFinishedSemaphore = CreateSemaphore();
        DrawFence = CreateFence();
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
        {
            throw new Exception("Failed to create semaphore");
        }

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
        {
            throw new Exception("Failed to create fence");
        }

        return fence;
    }

    public void Dispose()
    {
        _vk.DestroyFence(
            _deviceContext.LogicalDevice,
            DrawFence,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _vk.DestroySemaphore(
            _deviceContext.LogicalDevice,
            RenderFinishedSemaphore,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _vk.DestroySemaphore(
            _deviceContext.LogicalDevice,
            PresentCompleteSemaphore,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );

        GC.SuppressFinalize(this);
    }
}
