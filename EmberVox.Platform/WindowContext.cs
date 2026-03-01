using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace EmberVox.Platform;

public class WindowContext : IDisposable
{
    public IWindow Handle { get; }

    private const int WindowWidth = 800;
    private const int WindowHeight = 600;

    public WindowContext()
    {
        WindowOptions options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(WindowWidth, WindowHeight),
            Title = "EmberVox: Vulkan",
            WindowBorder = WindowBorder.Fixed, // Basically makes it unresizable
        };

        Handle = Silk.NET.Windowing.Window.Create(options);
        Handle.Initialize();
    }

    public void Dispose()
    {
        Handle.Dispose();

        GC.SuppressFinalize(this);
    }
}
