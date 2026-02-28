using Silk.NET.Vulkan.Extensions.KHR;

namespace EmberVox;

public unsafe class SurfaceContext : IDisposable
{
    public KhrSurface KhrSurfaceExtension { get; }
    public SurfaceKHR SurfaceKhr { get; }

    private Vk _vk;
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

    private SurfaceKHR CreateSurface()
    {
        return _window
            .VkSurface!.Create<AllocationCallbacks>(_instance.ToHandle(), null)
            .ToSurface();
    }

    public void Dispose()
    {
        KhrSurfaceExtension.DestroySurface(_instance, SurfaceKhr, null);
        KhrSurfaceExtension.Dispose();

        GC.SuppressFinalize(this);
    }
}
