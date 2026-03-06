using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace EmberVox.Rendering.Contexts;

internal sealed class SurfaceContext : IDisposable
{
    public KhrSurface KhrSurfaceExtension { get; }
    public SurfaceKHR SurfaceKhr { get; }

    private readonly Vk _vk;
    private readonly Instance _instance;
    private readonly IWindow _window;

    public SurfaceContext(Vk vk, Instance instance, IWindow window)
    {
        _vk = vk;
        _instance = instance;
        _window = window;

        if (!_vk.TryGetInstanceExtension(_instance, out KhrSurface khrSurfaceExtension))
            throw new Exception("Failed to get KhrSurface extension");

        KhrSurfaceExtension = khrSurfaceExtension;
        SurfaceKhr = CreateSurface();
    }

    public void Dispose()
    {
        KhrSurfaceExtension.DestroySurface(
            _instance,
            SurfaceKhr,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        KhrSurfaceExtension.Dispose();

        GC.SuppressFinalize(this);
    }

    private unsafe SurfaceKHR CreateSurface() =>
        _window.VkSurface!.Create<AllocationCallbacks>(_instance.ToHandle(), null).ToSurface();
}
