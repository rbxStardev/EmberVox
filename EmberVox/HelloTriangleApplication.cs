namespace EmberVox;

public class HelloTriangleApplication : IDisposable
{
    private const int WindowWidth = 800;
    private const int WindowHeight = 600;

    private IWindow _window;

    public void Run()
    {
        InitWindow();
        InitVulkan();
        MainLoop();
        Dispose();
    }

    private void InitWindow()
    {
        WindowOptions options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(WindowWidth, WindowHeight),
            Title = "Vulkan",
            WindowBorder = WindowBorder.Fixed, // Basically makes it unresizable
        };

        _window = Window.Create(options);
    }

    private void InitVulkan() { }

    private void MainLoop()
    {
        _window.Run();
    }

    public void Dispose()
    {
        _window.Dispose();

        GC.SuppressFinalize(this);
    }
}
