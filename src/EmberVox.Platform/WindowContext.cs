using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace EmberVox.Platform;

public class WindowContext : IDisposable
{
    public IWindow Handle { get; }

    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;

    public WindowContext()
    {
        var options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(WindowWidth, WindowHeight),
            Title = "EmberVox: Vulkan",
            FramesPerSecond = 144,
        };

        Handle = Window.Create(options);
        Handle.Initialize();
    }

    public void Dispose()
    {
        Handle.Dispose();

        GC.SuppressFinalize(this);
    }
}
